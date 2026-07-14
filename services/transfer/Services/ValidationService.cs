using EBanking.TransferService.Services.Interfaces;
using EBanking.TransferService.Models;
using IbanNet;
using ValidationResult = EBanking.TransferService.Services.Interfaces.ValidationResult;

namespace EBanking.TransferService.Services;

public class ValidationService : IValidationService
{
    private readonly IIbanValidator _ibanValidator;

    public ValidationService(IIbanValidator ibanValidator)
    {
        _ibanValidator = ibanValidator;
    }

    public async Task<bool> ValidateAccountOwnershipAsync(string userId, string accountNumber)
    {
        // TODO: Implement actual account ownership validation
        // This would typically call the Account Service to verify ownership
        await Task.Delay(1); // Placeholder to make it async

        // For now, basic validation - account number should not be empty
        return !string.IsNullOrWhiteSpace(accountNumber) && !string.IsNullOrWhiteSpace(userId);
    }

    public bool ValidateIBAN(string iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            return false;

        var result = _ibanValidator.Validate(iban);
        return result.IsValid;
    }

    public bool ValidateTransferLimits(decimal amount, TransferType type)
    {
        if (amount <= 0)
            return false;

        // Define transfer limits based on type
        return type switch
        {
            TransferType.Internal => amount <= 100_000,    // €100k for internal transfers
            TransferType.External => amount <= 50_000,     // €50k for external transfers
            _ => amount <= 10_000 // Default limit
        };
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

        if (amount > 1_000_000)
        {
            return new ValidationResult(false, "Amount exceeds maximum transfer limit");
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

    public ValidationResult ValidateStandingOrderFrequency(string frequency)
    {
        var validFrequencies = new[] { "DAILY", "WEEKLY", "MONTHLY", "QUARTERLY", "YEARLY" };

        if (!validFrequencies.Contains(frequency.ToUpperInvariant()))
        {
            return new ValidationResult(false,
                $"Invalid frequency. Must be one of: {string.Join(", ", validFrequencies)}");
        }

        return new ValidationResult(true);
    }

    public ValidationResult ValidateDateRange(DateTime startDate, DateTime? endDate)
    {
        if (startDate < DateTime.UtcNow.Date)
        {
            return new ValidationResult(false, "Start date cannot be in the past");
        }

        if (endDate.HasValue && endDate.Value <= startDate)
        {
            return new ValidationResult(false, "End date must be after start date");
        }

        if (endDate.HasValue && endDate.Value > DateTime.UtcNow.AddYears(10))
        {
            return new ValidationResult(false, "End date cannot be more than 10 years in the future");
        }

        return new ValidationResult(true);
    }
}
