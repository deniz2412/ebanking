namespace EBanking.NotificationService.DTOs;

public record SubscribeRequest(
    string Endpoint,
    string P256dh,
    string Auth,
    string? UserAgent = null
);

public record NotificationSubscriptionResponse(
    Guid Id,
    string Endpoint,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    bool IsActive
);

public record SendNotificationRequest(
    string Title,
    string Body,
    string CorrelationId,
    string Priority = "NORMAL",
    string? Icon = null,
    string? Badge = null,
    object? Data = null
);

public record NotificationMessageResponse(
    Guid Id,
    string Title,
    string Body,
    string Type,
    string Priority,
    string Status,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? ReadAt,
    string? FailureReason
);

public record VapidKeysResponse(
    string PublicKey
);

public record PagedResult<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
