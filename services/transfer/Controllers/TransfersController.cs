using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EBanking.TransferService.Models.DTOs;
using EBanking.TransferService.Services.Interfaces;
using System.Security.Claims;

namespace EBanking.TransferService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "TransferAccess")]
public class TransfersController : ControllerBase
{
    private readonly ITransferService _transferService;
    private readonly ILogger<TransfersController> _logger;

    public TransfersController(ITransferService transferService, ILogger<TransfersController> logger)
    {
        _transferService = transferService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new transfer (internal or external)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TransferResponse>> CreateTransfer([FromBody] CreateTransferRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
            return BadRequest("Idempotency-Key header is required");

        try
        {
            var result = await _transferService.CreateTransferAsync(userId, request, idempotencyKey);
            
            if (result.Status == Models.TransferStatus.Failed)
                return BadRequest(new { error = result.ErrorMessage });
                
            return CreatedAtAction(nameof(GetTransfer), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid transfer request from user {UserId}: {Message}", userId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transfer for user {UserId}", userId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get transfer by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TransferResponse>> GetTransfer(int id)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var transfer = await _transferService.GetTransferAsync(userId, id);
            if (transfer == null)
                return NotFound();

            return Ok(transfer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transfer {TransferId} for user {UserId}", id, userId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get user's transfer history with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedTransferResponse>> GetTransfers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Models.TransferStatus? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        try
        {
            var result = await _transferService.GetTransfersAsync(userId, page, pageSize, status, from, to);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transfers for user {UserId}", userId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
    }
}

[ApiController]
[Route("api/standing-orders")]
[Authorize(Policy = "TransferAccess")]
public class StandingOrdersController : ControllerBase
{
    private readonly ITransferService _transferService;
    private readonly ILogger<StandingOrdersController> _logger;

    public StandingOrdersController(ITransferService transferService, ILogger<StandingOrdersController> logger)
    {
        _transferService = transferService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new standing order
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<StandingOrderResponse>> CreateStandingOrder([FromBody] CreateStandingOrderRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
            return BadRequest("Idempotency-Key header is required");

        try
        {
            var result = await _transferService.CreateStandingOrderAsync(userId, request, idempotencyKey);
            return CreatedAtAction(nameof(GetStandingOrder), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid standing order request from user {UserId}: {Message}", userId, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating standing order for user {UserId}", userId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get standing order by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<StandingOrderResponse>> GetStandingOrder(int id)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var standingOrder = await _transferService.GetStandingOrderAsync(userId, id);
            if (standingOrder == null)
                return NotFound();

            return Ok(standingOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting standing order {StandingOrderId} for user {UserId}", id, userId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get user's standing orders with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedStandingOrderResponse>> GetStandingOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isActive = null)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        try
        {
            var result = await _transferService.GetStandingOrdersAsync(userId, page, pageSize, isActive);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting standing orders for user {UserId}", userId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Cancel/deactivate a standing order
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelStandingOrder(int id)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var success = await _transferService.CancelStandingOrderAsync(userId, id);
            if (!success)
                return NotFound();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling standing order {StandingOrderId} for user {UserId}", id, userId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
    }
}
