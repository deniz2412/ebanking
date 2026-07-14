using EBanking.AuditService.Data;
using EBanking.AuditService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EBanking.AuditService.Services;

public class IntegrityService : IIntegrityService
{
    private readonly AuditDbContext _context;
    private readonly IHashService _hashService;
    private readonly ILogger<IntegrityService> _logger;

    public IntegrityService(AuditDbContext context, IHashService hashService, ILogger<IntegrityService> logger)
    {
        _context = context;
        _hashService = hashService;
        _logger = logger;
    }

    public async Task<bool> VerifyChainIntegrityAsync(long fromSequence, long toSequence)
    {
        var logs = await _context.AuditLogs
            .Where(l => l.SequenceNumber >= fromSequence && l.SequenceNumber <= toSequence)
            .OrderBy(l => l.SequenceNumber)
            .ToListAsync();

        if (!logs.Any())
            return true;

        for (int i = 0; i < logs.Count; i++)
        {
            var currentLog = logs[i];
            var expectedPreviousHash = i == 0 ? null : logs[i - 1].Hash;

            // Verify previous hash link
            if (currentLog.PreviousHash != expectedPreviousHash)
            {
                _logger.LogWarning("Hash chain broken at sequence {Sequence}: expected previous hash {Expected}, got {Actual}",
                    currentLog.SequenceNumber, expectedPreviousHash, currentLog.PreviousHash);
                return false;
            }

            // Verify current record hash. Must match AuditService.CreateAuditLogAsync,
            // which hashes the record WITHOUT SequenceNumber (a DB-generated identity
            // not yet assigned when the hash is computed). Ordering is protected by the
            // PreviousHash chain link and the sequence-gap check above.
            var recordData = new
            {
                currentLog.Id,
                Timestamp = DateTime.SpecifyKind(currentLog.Timestamp, DateTimeKind.Utc),
                currentLog.EventType,
                currentLog.Service,
                currentLog.UserId,
                currentLog.EntityId,
                currentLog.EntityType,
                currentLog.Action,
                currentLog.Data,
                currentLog.IpAddress,
                currentLog.UserAgent,
                currentLog.RequestId,
                currentLog.SessionId
            };

            var expectedHash = _hashService.ComputeRecordHash(recordData, currentLog.PreviousHash);
            if (currentLog.Hash != expectedHash)
            {
                _logger.LogWarning("Record hash mismatch at sequence {Sequence}: expected {Expected}, got {Actual}",
                    currentLog.SequenceNumber, expectedHash, currentLog.Hash);
                return false;
            }
        }

        return true;
    }

    public async Task<string[]> FindIntegrityIssuesAsync(long fromSequence, long toSequence)
    {
        var issues = new List<string>();
        
        var logs = await _context.AuditLogs
            .Where(l => l.SequenceNumber >= fromSequence && l.SequenceNumber <= toSequence)
            .OrderBy(l => l.SequenceNumber)
            .ToListAsync();

        if (!logs.Any())
            return Array.Empty<string>();

        // Check for sequence gaps
        for (int i = 1; i < logs.Count; i++)
        {
            if (logs[i].SequenceNumber != logs[i - 1].SequenceNumber + 1)
            {
                issues.Add($"Sequence gap detected: {logs[i - 1].SequenceNumber} -> {logs[i].SequenceNumber}");
            }
        }

        // Check hash chain integrity
        for (int i = 0; i < logs.Count; i++)
        {
            var currentLog = logs[i];
            var expectedPreviousHash = i == 0 ? null : logs[i - 1].Hash;

            if (currentLog.PreviousHash != expectedPreviousHash)
            {
                issues.Add($"Broken hash chain at sequence {currentLog.SequenceNumber}: " +
                          $"expected previous hash '{expectedPreviousHash}', got '{currentLog.PreviousHash}'");
            }

            // Verify record hash (must match creation: no SequenceNumber in the hash).
            var recordData = new
            {
                currentLog.Id,
                Timestamp = DateTime.SpecifyKind(currentLog.Timestamp, DateTimeKind.Utc),
                currentLog.EventType,
                currentLog.Service,
                currentLog.UserId,
                currentLog.EntityId,
                currentLog.EntityType,
                currentLog.Action,
                currentLog.Data,
                currentLog.IpAddress,
                currentLog.UserAgent,
                currentLog.RequestId,
                currentLog.SessionId
            };

            var expectedHash = _hashService.ComputeRecordHash(recordData, currentLog.PreviousHash);
            if (currentLog.Hash != expectedHash)
            {
                issues.Add($"Invalid record hash at sequence {currentLog.SequenceNumber}: " +
                          $"expected '{expectedHash}', got '{currentLog.Hash}'");
            }
        }

        // Check timestamp order
        for (int i = 1; i < logs.Count; i++)
        {
            if (logs[i].Timestamp < logs[i - 1].Timestamp)
            {
                issues.Add($"Timestamp order violation at sequence {logs[i].SequenceNumber}: " +
                          $"{logs[i].Timestamp} < {logs[i - 1].Timestamp}");
            }
        }

        return issues.ToArray();
    }

    public async Task<bool> RepairChainAsync(long fromSequence, long toSequence)
    {
        _logger.LogInformation("Starting chain repair from sequence {FromSequence} to {ToSequence}", 
            fromSequence, toSequence);

        var logs = await _context.AuditLogs
            .Where(l => l.SequenceNumber >= fromSequence && l.SequenceNumber <= toSequence)
            .OrderBy(l => l.SequenceNumber)
            .ToListAsync();

        if (!logs.Any())
        {
            _logger.LogInformation("No records found in range for repair");
            return true;
        }

        var updated = 0;
        var previousHash = fromSequence > 1 ? 
            await GetHashAtSequenceAsync(fromSequence - 1) : null;

        foreach (var log in logs)
        {
            var recordData = new
            {
                log.Id,
                Timestamp = DateTime.SpecifyKind(log.Timestamp, DateTimeKind.Utc),
                log.EventType,
                log.Service,
                log.UserId,
                log.EntityId,
                log.EntityType,
                log.Action,
                log.Data,
                log.IpAddress,
                log.UserAgent,
                log.RequestId,
                log.SessionId
            };

            var correctHash = _hashService.ComputeRecordHash(recordData, previousHash);
            
            if (log.Hash != correctHash || log.PreviousHash != previousHash)
            {
                log.PreviousHash = previousHash;
                log.Hash = correctHash;
                updated++;
                
                _logger.LogInformation("Repaired record at sequence {Sequence}", log.SequenceNumber);
            }

            previousHash = log.Hash;
        }

        if (updated > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Chain repair completed: {UpdatedCount} records updated", updated);
        }
        else
        {
            _logger.LogInformation("Chain repair completed: no updates needed");
        }

        return true;
    }

    private async Task<string?> GetHashAtSequenceAsync(long sequence)
    {
        var log = await _context.AuditLogs
            .FirstOrDefaultAsync(l => l.SequenceNumber == sequence);
        
        return log?.Hash;
    }
}
