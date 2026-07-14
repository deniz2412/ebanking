namespace EBanking.AuditService.DTOs;

public record AuditLogResponse(
    Guid Id,
    long SequenceNumber,
    DateTime Timestamp,
    string EventType,
    string Service,
    string? UserId,
    string Action,
    string? EntityId,
    string? EntityType,
    string IpAddress,
    string? RequestId
);

public record CreateAuditLogRequest(
    string EventType,
    string Service,
    string? UserId,
    string? EntityId,
    string? EntityType,
    string Action,
    object Data,
    string IpAddress,
    string? UserAgent = null,
    string? RequestId = null,
    string? SessionId = null
);

public record IntegrityCheckResponse(
    Guid Id,
    DateTime CheckedAt,
    long FromSequence,
    long ToSequence,
    int RecordsChecked,
    bool IsValid,
    string[]? Issues
);

public record AuditSearchRequest(
    string? UserId = null,
    string? Service = null,
    string? EventType = null,
    string? Action = null,
    string? EntityId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 50
);

public record PagedResult<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
