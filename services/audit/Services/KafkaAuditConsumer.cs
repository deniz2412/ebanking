using System.Text.Json;
using Confluent.Kafka;
using EBanking.AuditService.DTOs;
using EBanking.AuditService.Services.Interfaces;

namespace EBanking.AuditService.Services;

/// <summary>
/// Consumes domain events from Kafka and appends them to the immutable,
/// hash-chained audit log. This is the control that answers STRIDE Repudiation:
/// events are recorded append-only faster than an attacker could alter them.
/// </summary>
public class KafkaAuditConsumer : BackgroundService
{
    private static readonly string[] Topics =
    {
        "transfer.created", "transfer.completed", "transfer.updated", "transfer.failed"
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KafkaAuditConsumer> _logger;

    public KafkaAuditConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<KafkaAuditConsumer> logger)
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
            GroupId = "audit-service",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            AllowAutoCreateTopics = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka consumer error: {Reason}", e.Reason))
            .Build();

        consumer.Subscribe(Topics);
        _logger.LogInformation("Audit consumer subscribed to {Topics}", string.Join(", ", Topics));

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
                    _logger.LogWarning(ex, "Error consuming audit event");
                    continue;
                }

                if (result?.Message?.Value is null)
                    continue;

                try
                {
                    await HandleEventAsync(result.Topic, result.Message.Value, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to record audit event: {Payload}", result.Message.Value);
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

    private async Task HandleEventAsync(string topic, string payload, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement.Clone();

        var eventType = GetString(root, "eventType") ?? topic;
        var userId = GetString(root, "userId");
        var transferId = GetScalar(root, "transferId");
        var eventId = GetString(root, "eventId");

        var request = new CreateAuditLogRequest(
            EventType: eventType,
            Service: "transfer-service",
            UserId: userId,
            EntityId: transferId,
            EntityType: "Transfer",
            Action: eventType,
            Data: root,
            IpAddress: "kafka",
            UserAgent: null,
            RequestId: eventId,
            SessionId: null);

        using var scope = _scopeFactory.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var response = await audit.CreateAuditLogAsync(request);

        _logger.LogInformation(
            "Audit log {Sequence} appended for {EventType} (transfer {TransferId})",
            response.SequenceNumber, eventType, transferId);
    }

    private static string? GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    // transferId serializes as a JSON number; read it (or any scalar) as text.
    private static string? GetScalar(JsonElement root, string name)
        => root.TryGetProperty(name, out var el)
           && el.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? el.ToString()
            : null;
}
