namespace Shared.Database.Models;

public class Transfer
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FromAccountNumber { get; set; } = string.Empty;
    public string ToAccountNumber { get; set; } = string.Empty;
    public string ToAccountName { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }

    public TransferType Type { get; set; }
    public TransferStatus Status { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // For audit and error tracking
    public string? ErrorMessage { get; set; }
    public string? ExternalTransactionId { get; set; }
}

public class StandingOrder
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FromAccountNumber { get; set; } = string.Empty;
    public string ToAccountNumber { get; set; } = string.Empty;
    public string ToAccountName { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }

    public Frequency Frequency { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime NextExecutionDate { get; set; }

    public bool IsActive { get; set; } = true;
    public int ExecutionCount { get; set; } = 0;
    public int? MaxExecutions { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<StandingOrderExecution> Executions { get; set; } = new List<StandingOrderExecution>();
}

public class StandingOrderExecution
{
    public int Id { get; set; }
    public int StandingOrderId { get; set; }
    public StandingOrder StandingOrder { get; set; } = null!;

    public DateTime ExecutedAt { get; set; }
    public ExecutionStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TransferReference { get; set; }
}

public enum TransferType
{
    Internal = 1,    // Same bank
    External = 2     // Different bank/institution
}

public enum TransferStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

public enum Frequency
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Quarterly = 4,
    Yearly = 5
}

public enum ExecutionStatus
{
    Success = 1,
    Failed = 2,
    Skipped = 3
}
