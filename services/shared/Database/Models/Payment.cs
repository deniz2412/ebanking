using System.ComponentModel.DataAnnotations;

namespace Shared.Database.Models;

public class Payment
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FromAccount { get; set; } = string.Empty;
    public string ToAccount { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? Reference { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, COMPLETED, FAILED, CANCELLED
    public string PaymentType { get; set; } = "INSTANT"; // INSTANT, SCHEDULED
    public DateTime? ScheduledDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? FailureReason { get; set; }
    
    // Metadata for logging (will be masked in logs)
    public string? BeneficiaryName { get; set; }
    public string? PayerName { get; set; }
}

public class PaymentTemplate
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ToAccount { get; set; } = string.Empty;
    public string? BeneficiaryName { get; set; }
    public decimal? DefaultAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? DefaultReference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
