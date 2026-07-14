namespace Shared.Database.Models;

public class AuditLogEntry
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string EventData { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? CorrelationId { get; set; }
    public string? CausationId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? PreviousHash { get; set; }
    public string EventHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public Guid Id { get; set; }
    public long SequenceNumber { get; set; } // Auto-incrementing sequence
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? EntityId { get; set; }
    public string? EntityType { get; set; }
    public string Action { get; set; } = string.Empty; // CREATE, UPDATE, DELETE, READ
    public string Data { get; set; } = string.Empty; // JSON data
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? RequestId { get; set; }
    public string? SessionId { get; set; }
    public string? PreviousHash { get; set; } // Hash of previous record for integrity chain
    public string Hash { get; set; } = string.Empty; // SHA-256 hash of this record
    public string? Signature { get; set; } // Digital signature for non-repudiation
}

public class IntegrityCheck
{
    public Guid Id { get; set; }
    public DateTime CheckedAt { get; set; }
    public long FromSequence { get; set; }
    public long ToSequence { get; set; }
    public int RecordsChecked { get; set; }
    public bool IsValid { get; set; }
    public string? Issues { get; set; } // JSON array of integrity issues
    public string CheckedBy { get; set; } = string.Empty; // System or UserId
}
