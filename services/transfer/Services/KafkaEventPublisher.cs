using EBanking.TransferService.Services.Interfaces;
using Confluent.Kafka;
using System.Text.Json;

namespace EBanking.TransferService.Services;

public class KafkaEventPublisher : IKafkaEventPublisher, IEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IConfiguration configuration, ILogger<KafkaEventPublisher> logger)
    {
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = configuration.GetConnectionString("Kafka"),
            ClientId = "transfer-service",
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

    public async Task PublishTransferCreatedAsync(Guid transferId, string fromAccount, string toAccount,
        decimal amount, string currency, string? reference)
    {
        var eventData = new
        {
            EventType = "TransferCreated",
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            TransferId = transferId,
            FromAccount = fromAccount,
            ToAccount = toAccount,
            Amount = amount,
            Currency = currency,
            Reference = reference
        };

        await PublishEventAsync("transfer.created", transferId.ToString(), eventData);
    }

    public async Task PublishTransferUpdatedAsync(Guid transferId, string oldStatus, string newStatus)
    {
        var eventData = new
        {
            EventType = "TransferUpdated",
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            TransferId = transferId,
            OldStatus = oldStatus,
            NewStatus = newStatus
        };

        await PublishEventAsync("transfer.updated", transferId.ToString(), eventData);
    }

    public async Task PublishStandingOrderCreatedAsync(Guid standingOrderId, string fromAccount,
        string toAccount, decimal amount, string frequency)
    {
        var eventData = new
        {
            EventType = "StandingOrderCreated",
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            StandingOrderId = standingOrderId,
            FromAccount = fromAccount,
            ToAccount = toAccount,
            Amount = amount,
            Frequency = frequency
        };

        await PublishEventAsync("standing-order.created", standingOrderId.ToString(), eventData);
    }

    public async Task PublishStandingOrderExecutedAsync(Guid standingOrderId, Guid transferId)
    {
        var eventData = new
        {
            EventType = "StandingOrderExecuted",
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            StandingOrderId = standingOrderId,
            TransferId = transferId
        };

        await PublishEventAsync("standing-order.executed", standingOrderId.ToString(), eventData);
    }

    // IEventPublisher implementation
    public async Task PublishTransferCreatedAsync(Models.Transfer transfer)
    {
        var eventData = new
        {
            EventType = "TransferCreated",
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            TransferId = transfer.Id,
            UserId = transfer.UserId,
            FromAccount = transfer.FromAccountNumber,
            ToAccount = transfer.ToAccountNumber,
            Amount = transfer.Amount,
            Currency = transfer.Currency,
            Description = transfer.Description,
            Type = transfer.Type.ToString(),
            Status = transfer.Status.ToString()
        };

        await PublishEventAsync("transfer.created", transfer.Id.ToString(), eventData);
    }

    public async Task PublishTransferCompletedAsync(Models.Transfer transfer)
    {
        var eventData = new
        {
            EventType = "TransferCompleted",
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            TransferId = transfer.Id,
            UserId = transfer.UserId,
            FromAccount = transfer.FromAccountNumber,
            ToAccount = transfer.ToAccountNumber,
            Amount = transfer.Amount,
            Currency = transfer.Currency,
            Status = transfer.Status.ToString()
        };

        await PublishEventAsync("transfer.completed", transfer.Id.ToString(), eventData);
    }

    public async Task PublishTransferFailedAsync(Models.Transfer transfer)
    {
        var eventData = new
        {
            EventType = "TransferFailed",
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            TransferId = transfer.Id,
            FromAccount = transfer.FromAccountNumber,
            ToAccount = transfer.ToAccountNumber,
            Amount = transfer.Amount,
            Currency = transfer.Currency,
            Status = transfer.Status,
            ErrorMessage = transfer.ErrorMessage
        };

        await PublishEventAsync("transfer.failed", transfer.Id.ToString(), eventData);
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

    public void Dispose()
    {
        _producer?.Dispose();
    }
}
