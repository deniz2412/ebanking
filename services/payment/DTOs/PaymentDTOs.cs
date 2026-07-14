namespace EBanking.PaymentService.DTOs;

public record CreatePaymentRequest(
    string FromAccount,
    string ToAccount,
    decimal Amount,
    string Currency = "EUR",
    string? Reference = null,
    string PaymentType = "INSTANT",
    DateTime? ScheduledDate = null,
    string? BeneficiaryName = null
);

public record PaymentResponse(
    Guid Id,
    string FromAccount,
    string ToAccount,
    decimal Amount,
    string Currency,
    string? Reference,
    string Status,
    string PaymentType,
    DateTime? ScheduledDate,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    string? FailureReason
);

public record CreatePaymentTemplateRequest(
    string Name,
    string ToAccount,
    string? BeneficiaryName = null,
    decimal? DefaultAmount = null,
    string Currency = "EUR",
    string? DefaultReference = null
);

public record PaymentTemplateResponse(
    Guid Id,
    string Name,
    string ToAccount,
    string? BeneficiaryName,
    decimal? DefaultAmount,
    string Currency,
    string? DefaultReference,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    bool IsActive
);

public record UpdatePaymentTemplateRequest(
    string? Name = null,
    string? ToAccount = null,
    string? BeneficiaryName = null,
    decimal? DefaultAmount = null,
    string? Currency = null,
    string? DefaultReference = null,
    bool? IsActive = null
);

public record PagedResult<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
