using EBanking.TransferService.Data;
using EBanking.TransferService.Models;
using EBanking.TransferService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EBanking.TransferService.Services;

public class IdempotencyService : IIdempotencyService
{
    private readonly TransferDbContext _context;

    public IdempotencyService(TransferDbContext context)
    {
        _context = context;
    }

    public async Task<IdempotencyRecord?> GetIdempotencyRecordAsync(string key, string userId)
    {
        return await _context.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Key == key && r.UserId == userId && r.ExpiresAt > DateTime.UtcNow);
    }

    public async Task SaveIdempotencyRecordAsync(string key, string userId, string requestHash, string? responseData, int statusCode)
    {
        var record = new IdempotencyRecord
        {
            Key = key,
            UserId = userId,
            RequestHash = requestHash,
            ResponseData = responseData,
            ResponseStatusCode = statusCode,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24) // 24 hour expiry
        };

        _context.IdempotencyRecords.Add(record);
        await _context.SaveChangesAsync();
    }
}
