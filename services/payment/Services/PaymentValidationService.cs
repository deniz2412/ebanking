using EBanking.PaymentService.Services.Interfaces;
using IbanNet;
using ValidationResult = EBanking.PaymentService.Services.Interfaces.ValidationResult;

namespace EBanking.PaymentService.Services;

public class PaymentValidationService : IPaymentValidationService
{
    private readonly IIbanValidator _ibanValidator;

    public PaymentValidationService(IIbanValidator ibanValidator)
    {
        _ibanValidator = ibanValidator;
    }

    public ValidationResult ValidateIban(string iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            return new ValidationResult(false, "IBAN cannot be empty");
        }

        var result = _ibanValidator.Validate(iban);

        return new ValidationResult(
            result.IsValid,
            result.IsValid ? null : $"Invalid IBAN: {result.Error}"
        );
    }

    public ValidationResult ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            return new ValidationResult(false, "Amount must be greater than zero");
        }

        if (amount > 10_000_000) // 10M limit for payments
        {
            return new ValidationResult(false, "Amount exceeds maximum payment limit");
        }

        // Check for more than 2 decimal places
        if (decimal.Round(amount, 2) != amount)
        {
            return new ValidationResult(false, "Amount cannot have more than 2 decimal places");
        }

        return new ValidationResult(true);
    }

    public ValidationResult ValidateReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return new ValidationResult(true); // Reference is optional
        }

        if (reference.Length > 140)
        {
            return new ValidationResult(false, "Reference cannot exceed 140 characters");
        }

        // Basic validation for special characters that might cause issues
        if (reference.Contains('\n') || reference.Contains('\r') || reference.Contains('\t'))
        {
            return new ValidationResult(false, "Reference cannot contain line breaks or tabs");
        }

        return new ValidationResult(true);
    }

    public ValidationResult ValidateScheduledDate(DateTime? scheduledDate)
    {
        if (!scheduledDate.HasValue)
        {
            return new ValidationResult(true); // Scheduled date is optional for instant payments
        }

        if (scheduledDate.Value < DateTime.UtcNow.Date)
        {
            return new ValidationResult(false, "Scheduled date cannot be in the past");
        }

        if (scheduledDate.Value > DateTime.UtcNow.AddYears(1))
        {
            return new ValidationResult(false, "Scheduled date cannot be more than 1 year in the future");
        }

        return new ValidationResult(true);
    }

    public ValidationResult ValidatePaymentType(string paymentType)
    {
        var validTypes = new[] { "INSTANT", "SCHEDULED" };

        if (!validTypes.Contains(paymentType.ToUpperInvariant()))
        {
            return new ValidationResult(false,
                $"Invalid payment type. Must be one of: {string.Join(", ", validTypes)}");
        }

        return new ValidationResult(true);
    }
}
