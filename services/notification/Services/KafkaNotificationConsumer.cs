using System.Text.Json;
using Confluent.Kafka;
using EBanking.NotificationService.DTOs;
using EBanking.NotificationService.Services.Interfaces;

namespace EBanking.NotificationService.Services;

/// <summary>
/// Consumes transfer lifecycle events from Kafka and turns them into user
/// notifications (persisted, and pushed via Web Push when a subscription exists).
/// This is the consumer side of the golden-path async flow: Transfer publishes
/// TransferCompleteEvent → Notification consumes → Web Push.
/// </summary>
public class KafkaNotificationConsumer : BackgroundService
{
    private static readonly string[] Topics = { "transfer.completed", "transfer.created" };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KafkaNotificationConsumer> _logger;

    public KafkaNotificationConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<KafkaNotificationConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);

    private async Task ConsumeLoop(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _configuration.GetConnectionString("Kafka"),
            GroupId = "notification-service",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            AllowAutoCreateTopics = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka consumer error: {Reason}", e.Reason))
            .Build();

        consumer.Subscribe(Topics);
        _logger.LogInformation("Notification consumer subscribed to {Topics}", string.Join(", ", Topics));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result;
                try
                {
                    result = consumer.Consume(TimeSpan.FromSeconds(1));
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning(ex, "Error consuming notification event");
                    continue;
                }

                if (result?.Message?.Value is null)
                    continue;

                try
                {
                    await HandleEventAsync(result.Message.Value, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process notification event: {Payload}", result.Message.Value);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task HandleEventAsync(string payload, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        var eventType = GetString(root, "eventType") ?? "TransferEvent";
        var userId = GetString(root, "userId");
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Skipping {EventType} notification: no userId in event", eventType);
            return;
        }

        var amount = GetDecimal(root, "amount");
        var currency = GetString(root, "currency") ?? "EUR";
        var toAccount = GetString(root, "toAccount") ?? "recipient";
        var transferId = GetScalar(root, "transferId") ?? Guid.NewGuid().ToString();

        var (title, body) = eventType switch
        {
            "TransferCompleted" => ("Transfer completed",
                $"Your transfer of {amount:0.00} {currency} to {toAccount} has completed."),
            "TransferCreated" => ("Transfer submitted",
                $"Your transfer of {amount:0.00} {currency} to {toAccount} has been submitted."),
            _ => ("Transfer update", $"Update on your transfer to {toAccount}.")
        };

        var request = new SendNotificationRequest(
            Title: title,
            Body: body,
            CorrelationId: transferId,
            Priority: "NORMAL");

        using var scope = _scopeFactory.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var response = await notifications.SendNotificationAsync(userId, request);

        _logger.LogInformation(
            "Notification {NotificationId} created for user {UserId} from {EventType} (status {Status})",
            response.Id, userId, eventType, response.Status);
    }

    private static string? GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static decimal GetDecimal(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number
            ? el.GetDecimal()
            : 0m;

    // transferId serializes as a JSON number; read it (or any scalar) as text.
    private static string? GetScalar(JsonElement root, string name)
        => root.TryGetProperty(name, out var el)
           && el.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? el.ToString()
            : null;
}
