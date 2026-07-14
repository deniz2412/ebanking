using EBanking.AuditService.Data;
using EBanking.AuditService.DTOs;
using EBanking.AuditService.Models;
using EBanking.AuditService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EBanking.AuditService.Services;

public class AuditService : IAuditService
{
    private readonly AuditDbContext _context;
    private readonly IHashService _hashService;
    private readonly IIntegrityService _integrityService;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        AuditDbContext context,
        IHashService hashService,
        IIntegrityService integrityService,
        ILogger<AuditService> logger)
    {
        _context = context;
        _hashService = hashService;
        _integrityService = integrityService;
        _logger = logger;
    }

    public async Task<AuditLogResponse> CreateAuditLogAsync(CreateAuditLogRequest request)
    {
        // Get the hash of the previous record for chain integrity
        var previousRecord = await _context.AuditLogs
            .OrderByDescending(l => l.SequenceNumber)
            .FirstOrDefaultAsync();

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = request.EventType,
            Service = request.Service,
            UserId = request.UserId,
            EntityId = request.EntityId,
            EntityType = request.EntityType,
            Action = request.Action,
            Data = JsonSerializer.Serialize(request.Data, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            }),
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            RequestId = request.RequestId,
            SessionId = request.SessionId,
            PreviousHash = previousRecord?.Hash
        };

        // Compute hash for this record (excluding the hash field itself)
        var recordData = new
        {
            auditLog.Id,
            // Canonicalize to UTC so the hash survives the DB round-trip (EF returns
            // DateTimeKind.Unspecified on read, which JSON-serializes without the 'Z').
            Timestamp = DateTime.SpecifyKind(auditLog.Timestamp, DateTimeKind.Utc),
            auditLog.EventType,
            auditLog.Service,
            auditLog.UserId,
            auditLog.EntityId,
            auditLog.EntityType,
            auditLog.Action,
            auditLog.Data,
            auditLog.IpAddress,
            auditLog.UserAgent,
            auditLog.RequestId,
            auditLog.SessionId
        };

        auditLog.Hash = _hashService.ComputeRecordHash(recordData, auditLog.PreviousHash);

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Audit log created - ID: {AuditId}, Sequence: {Sequence}, EventType: {EventType}",
            auditLog.Id, auditLog.SequenceNumber, auditLog.EventType);

        return MapToResponse(auditLog);
    }

    public async Task<PagedResult<AuditLogResponse>> SearchAuditLogsAsync(AuditSearchRequest request)
    {
        var query = _context.AuditLogs.AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(request.UserId))
            query = query.Where(l => l.UserId == request.UserId);

        if (!string.IsNullOrEmpty(request.Service))
            query = query.Where(l => l.Service == request.Service);

        if (!string.IsNullOrEmpty(request.EventType))
            query = query.Where(l => l.EventType == request.EventType);

        if (!string.IsNullOrEmpty(request.Action))
            query = query.Where(l => l.Action == request.Action);

        if (!string.IsNullOrEmpty(request.EntityId))
            query = query.Where(l => l.EntityId == request.EntityId);

        if (request.FromDate.HasValue)
            query = query.Where(l => l.Timestamp >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(l => l.Timestamp <= request.ToDate.Value);

        // Get total count for pagination
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        // Apply pagination and ordering
        var logs = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var responses = logs.Select(MapToResponse);

        return new PagedResult<AuditLogResponse>(responses, totalCount, request.Page, request.PageSize, totalPages);
    }

    public async Task<AuditLogResponse?> GetAuditLogAsync(Guid id)
    {
        var log = await _context.AuditLogs.FirstOrDefaultAsync(l => l.Id == id);
        return log == null ? null : MapToResponse(log);
    }

    public async Task<IntegrityCheckResponse> VerifyIntegrityAsync(long? fromSequence = null, long? toSequence = null)
    {
        // Default to checking the last 1000 records if no range specified
        if (!fromSequence.HasValue || !toSequence.HasValue)
        {
            var latestSequence = await _context.AuditLogs
                .MaxAsync(l => (long?)l.SequenceNumber) ?? 0;

            fromSequence ??= Math.Max(1, latestSequence - 999);
            toSequence ??= latestSequence;
        }

        var issues = await _integrityService.FindIntegrityIssuesAsync(fromSequence.Value, toSequence.Value);
        var recordsInRange = await _context.AuditLogs
            .CountAsync(l => l.SequenceNumber >= fromSequence && l.SequenceNumber <= toSequence);

        var integrityCheck = new IntegrityCheck
        {
            Id = Guid.NewGuid(),
            CheckedAt = DateTime.UtcNow,
            FromSequence = fromSequence.Value,
            ToSequence = toSequence.Value,
            RecordsChecked = recordsInRange,
            IsValid = !issues.Any(),
            Issues = issues.Any() ? JsonSerializer.Serialize(issues) : null,
            CheckedBy = "system"
        };

        _context.IntegrityChecks.Add(integrityCheck);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Integrity check completed - Range: {FromSequence}-{ToSequence}, Records: {RecordsChecked}, Valid: {IsValid}, Issues: {IssueCount}",
            fromSequence.Value, toSequence.Value, recordsInRange, integrityCheck.IsValid, issues.Length);

        return MapToResponse(integrityCheck, issues);
    }

    public async Task<IntegrityCheckResponse?> GetLatestIntegrityCheckAsync()
    {
        var latestCheck = await _context.IntegrityChecks
            .OrderByDescending(c => c.CheckedAt)
            .FirstOrDefaultAsync();

        if (latestCheck == null)
            return null;

        var issues = !string.IsNullOrEmpty(latestCheck.Issues) 
            ? JsonSerializer.Deserialize<string[]>(latestCheck.Issues) ?? Array.Empty<string>()
            : Array.Empty<string>();

        return MapToResponse(latestCheck, issues);
    }

    public async Task<IEnumerable<IntegrityCheckResponse>> GetIntegrityHistoryAsync(int limit = 10)
    {
        var checks = await _context.IntegrityChecks
            .OrderByDescending(c => c.CheckedAt)
            .Take(limit)
            .ToListAsync();

        return checks.Select(check =>
        {
            var issues = !string.IsNullOrEmpty(check.Issues)
                ? JsonSerializer.Deserialize<string[]>(check.Issues) ?? Array.Empty<string>()
                : Array.Empty<string>();

            return MapToResponse(check, issues);
        });
    }

    private static AuditLogResponse MapToResponse(AuditLog log)
    {
        return new AuditLogResponse(
            log.Id,
            log.SequenceNumber,
            log.Timestamp,
            log.EventType,
            log.Service,
            log.UserId,
            log.Action,
            log.EntityId,
            log.EntityType,
            log.IpAddress,
            log.RequestId
        );
    }

    private static IntegrityCheckResponse MapToResponse(IntegrityCheck check, string[] issues)
    {
        return new IntegrityCheckResponse(
            check.Id,
            check.CheckedAt,
            check.FromSequence,
            check.ToSequence,
            check.RecordsChecked,
            check.IsValid,
            issues.Any() ? issues : null
        );
    }
}
