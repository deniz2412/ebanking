using EBanking.TransferService.Models;

namespace EBanking.TransferService.Services.Interfaces;

public interface IIdempotencyService
{
    Task<IdempotencyRecord?> GetIdempotencyRecordAsync(string key, string userId);
    Task SaveIdempotencyRecordAsync(string key, string userId, string requestHash, string? responseData, int statusCode);
}
