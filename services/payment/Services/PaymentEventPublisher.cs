using EBanking.PaymentService.Services.Interfaces;
using Confluent.Kafka;
using System.Text.Json;

namespace EBanking.PaymentService.Services;

public class PaymentEventPublisher : IPaymentEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<PaymentEventPublisher> _logger;

    public PaymentEventPublisher(IConfiguration configuration, ILogger<PaymentEventPublisher> logger)
    {
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = configuration.GetConnectionString("Kafka"),
            ClientId = "payment-service",
            Acks = Acks.All,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000,
            RequestTimeoutMs = 30000,
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogError("Kafka error: {Error}", e.Reason))
            .Build();
    }

    public async Task PublishPaymentCreatedAsync(Guid paymentId, string fromAccount, string toAccount,
        decimal amount, string currency)
    {
        var eventData = new
        {
            EventType = "PaymentCreated",
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            PaymentId = paymentId,
            FromAccount = MaskAccount(fromAccount),
            ToAccount = MaskAccount(toAccount),
            Amount = amount,
            Currency = currency
        };

        await PublishEventAsync("payment.created", paymentId.ToString(), eventData);
    }

    public async Task PublishPaymentStatusChangedAsync(Guid paymentId, string oldStatus, string newStatus)
    {
        var eventData = new
        {
            EventType = "PaymentStatusChanged",
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            PaymentId = paymentId,
            OldStatus = oldStatus,
            NewStatus = newStatus
        };

        await PublishEventAsync("payment.status-changed", paymentId.ToString(), eventData);
    }

    public async Task PublishPaymentProcessedAsync(Guid paymentId, bool success, string? failureReason = null)
    {
        var eventData = new
        {
            EventType = "PaymentProcessed",
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            PaymentId = paymentId,
            Success = success,
            FailureReason = failureReason
        };

        await PublishEventAsync("payment.processed", paymentId.ToString(), eventData);
    }

    private async Task PublishEventAsync(string topic, string key, object eventData)
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = key,
                Value = JsonSerializer.Serialize(eventData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };

            var result = await _producer.ProduceAsync(topic, message);

            _logger.LogInformation("Published event to topic {Topic} with key {Key} at offset {Offset}",
                topic, key, result.Offset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event to topic {Topic} with key {Key}", topic, key);
            throw;
        }
    }

    private static string MaskAccount(string account)
    {
        if (account.Length <= 8)
            return "****";

        return account[..4] + "****" + account[^4..];
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
}
