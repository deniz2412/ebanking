using System.ComponentModel.DataAnnotations;

namespace EBanking.TransferService.Models.DTOs;

public class CreateTransferRequest
{
    [Required(ErrorMessage = "From account number is required")]
    public string FromAccountNumber { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "To account number is required")]
    public string ToAccountNumber { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "To account name is required")]
    public string ToAccountName { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, 1000000, ErrorMessage = "Amount must be between 0.01 and 1,000,000")]
    public decimal Amount { get; set; }
    
    public string Currency { get; set; } = "EUR";
    
    [Required(ErrorMessage = "Description is required")]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string Description { get; set; } = string.Empty;
    
    [StringLength(100, ErrorMessage = "Reference cannot exceed 100 characters")]
    public string? Reference { get; set; }
    
    public TransferType Type { get; set; } = TransferType.Internal;
}

public class CreateStandingOrderRequest
{
    [Required(ErrorMessage = "From account number is required")]
    public string FromAccountNumber { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "To account number is required")]
    public string ToAccountNumber { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "To account name is required")]
    public string ToAccountName { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, 100000, ErrorMessage = "Amount must be between 0.01 and 100,000")]
    public decimal Amount { get; set; }
    
    public string Currency { get; set; } = "EUR";
    
    [Required(ErrorMessage = "Description is required")]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string Description { get; set; } = string.Empty;
    
    [StringLength(100, ErrorMessage = "Reference cannot exceed 100 characters")]
    public string? Reference { get; set; }
    
    [Required(ErrorMessage = "Frequency is required")]
    public Frequency Frequency { get; set; }
    
    [Required(ErrorMessage = "Start date is required")]
    public DateTime StartDate { get; set; }
    
    public DateTime? EndDate { get; set; }
    
    [Range(1, 1000, ErrorMessage = "Max executions must be between 1 and 1000")]
    public int? MaxExecutions { get; set; }
}

public class TransferResponse
{
    public int Id { get; set; }
    public string FromAccountNumber { get; set; } = string.Empty;
    public string ToAccountNumber { get; set; } = string.Empty;
    public string ToAccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public TransferType Type { get; set; }
    public TransferStatus Status { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public class StandingOrderResponse
{
    public int Id { get; set; }
    public string FromAccountNumber { get; set; } = string.Empty;
    public string ToAccountNumber { get; set; } = string.Empty;
    public string ToAccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public Frequency Frequency { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime NextExecutionDate { get; set; }
    public bool IsActive { get; set; }
    public int ExecutionCount { get; set; }
    public int? MaxExecutions { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PagedTransferResponse
{
    public IEnumerable<TransferResponse> Transfers { get; set; } = new List<TransferResponse>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class PagedStandingOrderResponse
{
    public IEnumerable<StandingOrderResponse> StandingOrders { get; set; } = new List<StandingOrderResponse>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
