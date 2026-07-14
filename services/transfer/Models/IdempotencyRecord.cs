using System.ComponentModel.DataAnnotations;

namespace EBanking.TransferService.Models;

public class IdempotencyRecord
{
    [Key]
    public string Key { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public string? ResponseData { get; set; }

    public int? ResponseStatusCode { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }
}
