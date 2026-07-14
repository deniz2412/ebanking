using EBanking.AuditService.DTOs;
using EBanking.AuditService.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EBanking.AuditService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AuditAccess")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IAuditService auditService, ILogger<AuditController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Create audit log entry
    /// </summary>
    [HttpPost("logs")]
    public async Task<ActionResult<AuditLogResponse>> CreateAuditLog([FromBody] CreateAuditLogRequest request)
    {
        try
        {
            var auditLog = await _auditService.CreateAuditLogAsync(request);
            
            _logger.LogInformation("Audit log created - ID: {AuditId}, EventType: {EventType}, Service: {Service}",
                auditLog.Id, auditLog.EventType, auditLog.Service);
            
            return CreatedAtAction(nameof(GetAuditLog), new { id = auditLog.Id }, auditLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating audit log for event: {EventType}", request.EventType);
            return StatusCode(500, "An error occurred while creating audit log");
        }
    }

    /// <summary>
    /// Get audit log by ID
    /// </summary>
    [HttpGet("logs/{id}")]
    public async Task<ActionResult<AuditLogResponse>> GetAuditLog(Guid id)
    {
        try
        {
            var auditLog = await _auditService.GetAuditLogAsync(id);
            
            if (auditLog == null)
            {
                return NotFound($"Audit log with ID {id} not found");
            }

            return Ok(auditLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit log {AuditId}", id);
            return StatusCode(500, "An error occurred while retrieving audit log");
        }
    }

    /// <summary>
    /// Search audit logs with filtering and pagination
    /// </summary>
    [HttpGet("logs")]
    public async Task<ActionResult<PagedResult<AuditLogResponse>>> SearchAuditLogs([FromQuery] AuditSearchRequest request)
    {
        try
        {
            // Restrict access to user's own logs unless admin
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User.IsInRole("audit-admin") || User.HasClaim("scope", "admin:audit");
            
            if (!isAdmin && !string.IsNullOrEmpty(request.UserId) && request.UserId != userId)
            {
                return Forbid("Cannot access audit logs for other users");
            }

            // If not admin and no specific user requested, default to current user
            if (!isAdmin && string.IsNullOrEmpty(request.UserId))
            {
                request = request with { UserId = userId };
            }

            var auditLogs = await _auditService.SearchAuditLogsAsync(request);
            return Ok(auditLogs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching audit logs");
            return StatusCode(500, "An error occurred while searching audit logs");
        }
    }

    /// <summary>
    /// Verify audit chain integrity
    /// </summary>
    [HttpPost("integrity/verify")]
    [Authorize(Roles = "audit-admin")]
    public async Task<ActionResult<IntegrityCheckResponse>> VerifyIntegrity(
        [FromQuery] long? fromSequence = null,
        [FromQuery] long? toSequence = null)
    {
        try
        {
            var verificationResult = await _auditService.VerifyIntegrityAsync(fromSequence, toSequence);
            
            _logger.LogInformation("Integrity verification completed - Range: {FromSequence}-{ToSequence}, Valid: {IsValid}",
                fromSequence, toSequence, verificationResult.IsValid);
            
            return Ok(verificationResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during integrity verification - Range: {FromSequence}-{ToSequence}", 
                fromSequence, toSequence);
            return StatusCode(500, "An error occurred during integrity verification");
        }
    }

    /// <summary>
    /// Get latest integrity check result
    /// </summary>
    [HttpGet("integrity/latest")]
    [Authorize(Roles = "audit-admin")]
    public async Task<ActionResult<IntegrityCheckResponse>> GetLatestIntegrityCheck()
    {
        try
        {
            var latestCheck = await _auditService.GetLatestIntegrityCheckAsync();
            
            if (latestCheck == null)
            {
                return NotFound("No integrity checks found");
            }

            return Ok(latestCheck);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest integrity check");
            return StatusCode(500, "An error occurred while retrieving integrity check");
        }
    }

    /// <summary>
    /// Get integrity check history
    /// </summary>
    [HttpGet("integrity/history")]
    [Authorize(Roles = "audit-admin")]
    public async Task<ActionResult<IEnumerable<IntegrityCheckResponse>>> GetIntegrityHistory(
        [FromQuery] int limit = 10)
    {
        try
        {
            var history = await _auditService.GetIntegrityHistoryAsync(limit);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving integrity history");
            return StatusCode(500, "An error occurred while retrieving integrity history");
        }
    }

    /// <summary>
    /// Get audit statistics (admin only)
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Roles = "audit-admin")]
    public async Task<ActionResult<object>> GetAuditStats()
    {
        try
        {
            // Simple stats for demonstration
            var searchRequest = new AuditSearchRequest(Page: 1, PageSize: 1);
            var result = await _auditService.SearchAuditLogsAsync(searchRequest);
            
            var stats = new
            {
                TotalRecords = result.TotalCount,
                LastVerification = await _auditService.GetLatestIntegrityCheckAsync(),
                SystemHealth = "OK"
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit statistics");
            return StatusCode(500, "An error occurred while retrieving statistics");
        }
    }
}
