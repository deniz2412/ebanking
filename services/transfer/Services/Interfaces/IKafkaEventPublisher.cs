namespace EBanking.TransferService.Services.Interfaces;

public interface IKafkaEventPublisher
{
    Task PublishTransferCreatedAsync(Guid transferId, string fromAccount, string toAccount, 
        decimal amount, string currency, string? reference);
    Task PublishTransferUpdatedAsync(Guid transferId, string oldStatus, string newStatus);
    Task PublishStandingOrderCreatedAsync(Guid standingOrderId, string fromAccount, 
        string toAccount, decimal amount, string frequency);
    Task PublishStandingOrderExecutedAsync(Guid standingOrderId, Guid transferId);
}
