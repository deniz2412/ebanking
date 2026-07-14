using EBanking.PaymentService.Data;
using EBanking.PaymentService.DTOs;
using EBanking.PaymentService.Models;
using EBanking.PaymentService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EBanking.PaymentService.Services;

public class PaymentService : IPaymentService
{
    private readonly PaymentDbContext _context;
    private readonly IPaymentValidationService _validationService;
    private readonly IPaymentEventPublisher _eventPublisher;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        PaymentDbContext context,
        IPaymentValidationService validationService,
        IPaymentEventPublisher eventPublisher,
        ILogger<PaymentService> logger)
    {
        _context = context;
        _validationService = validationService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<PaymentResponse> CreatePaymentAsync(string userId, CreatePaymentRequest request)
    {
        // Validate input
        var validationErrors = new List<string>();

        var amountValidation = _validationService.ValidateAmount(request.Amount);
        if (!amountValidation.IsValid)
            validationErrors.Add(amountValidation.ErrorMessage!);

        var toAccountValidation = _validationService.ValidateIban(request.ToAccount);
        if (!toAccountValidation.IsValid)
            validationErrors.Add(toAccountValidation.ErrorMessage!);

        var fromAccountValidation = _validationService.ValidateIban(request.FromAccount);
        if (!fromAccountValidation.IsValid)
            validationErrors.Add(fromAccountValidation.ErrorMessage!);

        var referenceValidation = _validationService.ValidateReference(request.Reference);
        if (!referenceValidation.IsValid)
            validationErrors.Add(referenceValidation.ErrorMessage!);

        var paymentTypeValidation = _validationService.ValidatePaymentType(request.PaymentType);
        if (!paymentTypeValidation.IsValid)
            validationErrors.Add(paymentTypeValidation.ErrorMessage!);

        var scheduledDateValidation = _validationService.ValidateScheduledDate(request.ScheduledDate);
        if (!scheduledDateValidation.IsValid)
            validationErrors.Add(scheduledDateValidation.ErrorMessage!);

        if (validationErrors.Any())
        {
            throw new ArgumentException(string.Join("; ", validationErrors));
        }

        // Create payment
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FromAccount = request.FromAccount,
            ToAccount = request.ToAccount,
            Amount = request.Amount,
            Currency = request.Currency,
            Reference = request.Reference,
            PaymentType = request.PaymentType,
            ScheduledDate = request.ScheduledDate,
            BeneficiaryName = request.BeneficiaryName,
            Status = request.PaymentType == "INSTANT" ? "PROCESSING" : "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Publish event after transaction commit
            await _eventPublisher.PublishPaymentCreatedAsync(payment.Id, payment.FromAccount, payment.ToAccount, 
                payment.Amount, payment.Currency);

            _logger.LogInformation("Payment created - ID: {PaymentId}, Type: {PaymentType}, Amount: {Amount}",
                payment.Id, payment.PaymentType, payment.Amount);

            return MapToResponse(payment);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<PaymentResponse?> GetPaymentAsync(Guid id, string userId)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        return payment == null ? null : MapToResponse(payment);
    }

    public async Task<PagedResult<PaymentResponse>> GetUserPaymentsAsync(string userId, int page, int pageSize, string? status = null)
    {
        var query = _context.Payments.Where(p => p.UserId == userId);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(p => p.Status == status.ToUpperInvariant());
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var responses = payments.Select(MapToResponse);

        return new PagedResult<PaymentResponse>(responses, totalCount, page, pageSize, totalPages);
    }

    public async Task<PaymentResponse?> CancelPaymentAsync(Guid id, string userId)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

        if (payment == null)
            return null;

        if (payment.Status != "PENDING")
        {
            throw new InvalidOperationException($"Cannot cancel payment with status {payment.Status}");
        }

        var oldStatus = payment.Status;
        payment.Status = "CANCELLED";
        payment.ProcessedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Publish status change event
        await _eventPublisher.PublishPaymentStatusChangedAsync(payment.Id, oldStatus, payment.Status);

        _logger.LogInformation("Payment cancelled - ID: {PaymentId}, User: {UserId}", payment.Id, userId);

        return MapToResponse(payment);
    }

    public async Task ProcessScheduledPaymentsAsync()
    {
        var duePayments = await _context.Payments
            .Where(p => p.Status == "PENDING" &&
                       p.PaymentType == "SCHEDULED" &&
                       p.ScheduledDate <= DateTime.UtcNow)
            .ToListAsync();

        foreach (var payment in duePayments)
        {
            try
            {
                var oldStatus = payment.Status;
                payment.Status = "PROCESSING";
                payment.ProcessedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Publish status change
                await _eventPublisher.PublishPaymentStatusChangedAsync(payment.Id, oldStatus, payment.Status);

                _logger.LogInformation("Scheduled payment processed - ID: {PaymentId}", payment.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process scheduled payment - ID: {PaymentId}", payment.Id);

                payment.Status = "FAILED";
                payment.FailureReason = "Processing error";
                payment.ProcessedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
        }
    }

    private static PaymentResponse MapToResponse(Payment payment)
    {
        return new PaymentResponse(
            payment.Id,
            payment.FromAccount,
            payment.ToAccount,
            payment.Amount,
            payment.Currency,
            payment.Reference,
            payment.Status,
            payment.PaymentType,
            payment.ScheduledDate,
            payment.CreatedAt,
            payment.ProcessedAt,
            payment.FailureReason
        );
    }
}
