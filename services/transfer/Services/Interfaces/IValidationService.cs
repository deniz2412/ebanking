using EBanking.TransferService.Models;

namespace EBanking.TransferService.Services.Interfaces;

public interface IValidationService
{
    Task<bool> ValidateAccountOwnershipAsync(string userId, string accountNumber);
    bool ValidateIBAN(string iban);
    bool ValidateTransferLimits(decimal amount, TransferType type);
    ValidationResult ValidateIban(string iban);
    ValidationResult ValidateAmount(decimal amount);
    ValidationResult ValidateReference(string? reference);
    ValidationResult ValidateStandingOrderFrequency(string frequency);
    ValidationResult ValidateDateRange(DateTime startDate, DateTime? endDate);
}

public record ValidationResult(bool IsValid, string? ErrorMessage = null);
