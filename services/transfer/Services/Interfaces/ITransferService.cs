using EBanking.TransferService.Models;
using EBanking.TransferService.Models.DTOs;

namespace EBanking.TransferService.Services.Interfaces;

public interface ITransferService
{
    Task<TransferResponse> CreateTransferAsync(string userId, CreateTransferRequest request, string idempotencyKey);
    Task<TransferResponse?> GetTransferAsync(string userId, int transferId);
    Task<PagedTransferResponse> GetTransfersAsync(string userId, int page, int pageSize, TransferStatus? status = null, DateTime? from = null, DateTime? to = null);

    Task<StandingOrderResponse> CreateStandingOrderAsync(string userId, CreateStandingOrderRequest request, string idempotencyKey);
    Task<StandingOrderResponse?> GetStandingOrderAsync(string userId, int standingOrderId);
    Task<PagedStandingOrderResponse> GetStandingOrdersAsync(string userId, int page, int pageSize, bool? isActive = null);
    Task<bool> CancelStandingOrderAsync(string userId, int standingOrderId);
}

public interface IEventPublisher
{
    Task PublishTransferCreatedAsync(Transfer transfer);
    Task PublishTransferCompletedAsync(Transfer transfer);
    Task PublishTransferFailedAsync(Transfer transfer);
}
