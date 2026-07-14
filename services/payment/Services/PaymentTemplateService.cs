using EBanking.PaymentService.Data;
using EBanking.PaymentService.DTOs;
using EBanking.PaymentService.Models;
using EBanking.PaymentService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EBanking.PaymentService.Services;

public class PaymentTemplateService : IPaymentTemplateService
{
    private readonly PaymentDbContext _context;
    private readonly IPaymentValidationService _validationService;
    private readonly ILogger<PaymentTemplateService> _logger;

    public PaymentTemplateService(
        PaymentDbContext context,
        IPaymentValidationService validationService,
        ILogger<PaymentTemplateService> logger)
    {
        _context = context;
        _validationService = validationService;
        _logger = logger;
    }

    public async Task<PaymentTemplateResponse> CreateTemplateAsync(string userId, CreatePaymentTemplateRequest request)
    {
        // Validate input
        var validationErrors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
            validationErrors.Add("Template name is required");

        var toAccountValidation = _validationService.ValidateIban(request.ToAccount);
        if (!toAccountValidation.IsValid)
            validationErrors.Add(toAccountValidation.ErrorMessage!);

        if (request.DefaultAmount.HasValue)
        {
            var amountValidation = _validationService.ValidateAmount(request.DefaultAmount.Value);
            if (!amountValidation.IsValid)
                validationErrors.Add(amountValidation.ErrorMessage!);
        }

        var referenceValidation = _validationService.ValidateReference(request.DefaultReference);
        if (!referenceValidation.IsValid)
            validationErrors.Add(referenceValidation.ErrorMessage!);

        if (validationErrors.Any())
        {
            throw new ArgumentException(string.Join("; ", validationErrors));
        }

        // Check for duplicate names
        var existingTemplate = await _context.PaymentTemplates
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Name == request.Name && t.IsActive);

        if (existingTemplate != null)
        {
            throw new ArgumentException($"A template with name '{request.Name}' already exists");
        }

        var template = new PaymentTemplate
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            ToAccount = request.ToAccount,
            BeneficiaryName = request.BeneficiaryName,
            DefaultAmount = request.DefaultAmount,
            Currency = request.Currency,
            DefaultReference = request.DefaultReference,
            CreatedAt = DateTime.UtcNow
        };

        _context.PaymentTemplates.Add(template);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment template created - ID: {TemplateId}, Name: {TemplateName}",
            template.Id, template.Name);

        return MapToResponse(template);
    }

    public async Task<PaymentTemplateResponse?> GetTemplateAsync(Guid id, string userId)
    {
        var template = await _context.PaymentTemplates
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        return template == null ? null : MapToResponse(template);
    }

    public async Task<IEnumerable<PaymentTemplateResponse>> GetUserTemplatesAsync(string userId, bool activeOnly = true)
    {
        var query = _context.PaymentTemplates.Where(t => t.UserId == userId);

        if (activeOnly)
        {
            query = query.Where(t => t.IsActive);
        }

        var templates = await query
            .OrderBy(t => t.Name)
            .ToListAsync();

        return templates.Select(MapToResponse);
    }

    public async Task<PaymentTemplateResponse?> UpdateTemplateAsync(Guid id, string userId, UpdatePaymentTemplateRequest request)
    {
        var template = await _context.PaymentTemplates
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (template == null)
            return null;

        // Validate updates
        var validationErrors = new List<string>();

        if (request.ToAccount != null)
        {
            var toAccountValidation = _validationService.ValidateIban(request.ToAccount);
            if (!toAccountValidation.IsValid)
                validationErrors.Add(toAccountValidation.ErrorMessage!);
        }

        if (request.DefaultAmount.HasValue)
        {
            var amountValidation = _validationService.ValidateAmount(request.DefaultAmount.Value);
            if (!amountValidation.IsValid)
                validationErrors.Add(amountValidation.ErrorMessage!);
        }

        var referenceValidation = _validationService.ValidateReference(request.DefaultReference);
        if (!referenceValidation.IsValid)
            validationErrors.Add(referenceValidation.ErrorMessage!);

        if (validationErrors.Any())
        {
            throw new ArgumentException(string.Join("; ", validationErrors));
        }

        // Check for duplicate names (if name is being changed)
        if (request.Name != null && request.Name != template.Name)
        {
            var existingTemplate = await _context.PaymentTemplates
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Name == request.Name && t.IsActive && t.Id != id);

            if (existingTemplate != null)
            {
                throw new ArgumentException($"A template with name '{request.Name}' already exists");
            }
        }

        // Apply updates
        if (request.Name != null) template.Name = request.Name;
        if (request.ToAccount != null) template.ToAccount = request.ToAccount;
        if (request.BeneficiaryName != null) template.BeneficiaryName = request.BeneficiaryName;
        if (request.DefaultAmount.HasValue) template.DefaultAmount = request.DefaultAmount;
        if (request.Currency != null) template.Currency = request.Currency;
        if (request.DefaultReference != null) template.DefaultReference = request.DefaultReference;
        if (request.IsActive.HasValue) template.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment template updated - ID: {TemplateId}, Name: {TemplateName}",
            template.Id, template.Name);

        return MapToResponse(template);
    }

    public async Task<bool> DeleteTemplateAsync(Guid id, string userId)
    {
        var template = await _context.PaymentTemplates
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (template == null)
            return false;

        // Soft delete by marking as inactive
        template.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Payment template deleted - ID: {TemplateId}, Name: {TemplateName}",
            template.Id, template.Name);

        return true;
    }

    private static PaymentTemplateResponse MapToResponse(PaymentTemplate template)
    {
        return new PaymentTemplateResponse(
            template.Id,
            template.Name,
            template.ToAccount,
            template.BeneficiaryName,
            template.DefaultAmount,
            template.Currency,
            template.DefaultReference,
            template.CreatedAt,
            template.LastUsedAt,
            template.IsActive
        );
    }
}
