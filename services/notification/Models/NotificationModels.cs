namespace EBanking.NotificationService.Models;

public class NotificationSubscription
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public class NotificationMessage
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Badge { get; set; }
    public string? Data { get; set; } // JSON data for the notification
    public string Type { get; set; } = string.Empty; // PAYMENT, TRANSFER, ACCOUNT, SECURITY
    public string Priority { get; set; } = "NORMAL"; // LOW, NORMAL, HIGH, URGENT
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, SENT, DELIVERED, FAILED, READ
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime? NextRetryAt { get; set; }
}

public class NotificationTemplate
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty; // transfer.created, payment.processed, etc.
    public string TitleTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string Priority { get; set; } = "NORMAL";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
