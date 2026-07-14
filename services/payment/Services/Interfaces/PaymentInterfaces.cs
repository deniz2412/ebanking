using EBanking.PaymentService.DTOs;

namespace EBanking.PaymentService.Services.Interfaces;

public interface IPaymentService
{
    Task<PaymentResponse> CreatePaymentAsync(string userId, CreatePaymentRequest request);
    Task<PaymentResponse?> GetPaymentAsync(Guid id, string userId);
    Task<PagedResult<PaymentResponse>> GetUserPaymentsAsync(string userId, int page, int pageSize, string? status = null);
    Task<PaymentResponse?> CancelPaymentAsync(Guid id, string userId);
    Task ProcessScheduledPaymentsAsync();
}

public interface IPaymentTemplateService
{
    Task<PaymentTemplateResponse> CreateTemplateAsync(string userId, CreatePaymentTemplateRequest request);
    Task<PaymentTemplateResponse?> GetTemplateAsync(Guid id, string userId);
    Task<IEnumerable<PaymentTemplateResponse>> GetUserTemplatesAsync(string userId, bool activeOnly = true);
    Task<PaymentTemplateResponse?> UpdateTemplateAsync(Guid id, string userId, UpdatePaymentTemplateRequest request);
    Task<bool> DeleteTemplateAsync(Guid id, string userId);
}

public interface IPaymentValidationService
{
    ValidationResult ValidateIban(string iban);
    ValidationResult ValidateAmount(decimal amount);
    ValidationResult ValidateReference(string? reference);
    ValidationResult ValidateScheduledDate(DateTime? scheduledDate);
    ValidationResult ValidatePaymentType(string paymentType);
}

public interface IPaymentEventPublisher
{
    Task PublishPaymentCreatedAsync(Guid paymentId, string fromAccount, string toAccount, decimal amount, string currency);
    Task PublishPaymentStatusChangedAsync(Guid paymentId, string oldStatus, string newStatus);
    Task PublishPaymentProcessedAsync(Guid paymentId, bool success, string? failureReason = null);
}

public record ValidationResult(bool IsValid, string? ErrorMessage = null);
