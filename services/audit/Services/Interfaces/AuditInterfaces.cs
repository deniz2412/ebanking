using EBanking.AuditService.DTOs;

namespace EBanking.AuditService.Services.Interfaces;

public interface IAuditService
{
    Task<AuditLogResponse> CreateAuditLogAsync(CreateAuditLogRequest request);
    Task<PagedResult<AuditLogResponse>> SearchAuditLogsAsync(AuditSearchRequest request);
    Task<AuditLogResponse?> GetAuditLogAsync(Guid id);
    Task<IntegrityCheckResponse> VerifyIntegrityAsync(long? fromSequence = null, long? toSequence = null);
    Task<IntegrityCheckResponse?> GetLatestIntegrityCheckAsync();
    Task<IEnumerable<IntegrityCheckResponse>> GetIntegrityHistoryAsync(int limit = 10);
}

public interface IHashService
{
    string ComputeHash(string input);
    string ComputeRecordHash(object record, string? previousHash = null);
    bool VerifyHash(string data, string hash);
}

public interface IIntegrityService
{
    Task<bool> VerifyChainIntegrityAsync(long fromSequence, long toSequence);
    Task<string[]> FindIntegrityIssuesAsync(long fromSequence, long toSequence);
    Task<bool> RepairChainAsync(long fromSequence, long toSequence);
}

// Kafka consumer interface removed due to architecture simplification
