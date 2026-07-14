using EBanking.AuditService.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EBanking.AuditService.Services;

public class HashService : IHashService
{
    public string ComputeHash(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public string ComputeRecordHash(object record, string? previousHash = null)
    {
        var recordJson = JsonSerializer.Serialize(record, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var hashInput = previousHash != null ? $"{previousHash}|{recordJson}" : recordJson;
        return ComputeHash(hashInput);
    }

    public bool VerifyHash(string data, string hash)
    {
        var computedHash = ComputeHash(data);
        return string.Equals(computedHash, hash, StringComparison.OrdinalIgnoreCase);
    }
}
