using EBanking.PaymentService.DTOs;
using EBanking.PaymentService.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EBanking.PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "PaymentAccess")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new payment
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var payment = await _paymentService.CreatePaymentAsync(userId, request);
            
            // Log payment creation with masked sensitive data
            _logger.LogInformation("Payment created - ID: {PaymentId}, User: {UserId}, Amount: {Amount}, Status: {Status}",
                payment.Id, userId, payment.Amount, payment.Status);
            
            return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Payment creation failed - User: {UserId}, Error: {Error}", userId, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating payment for user: {UserId}", userId);
            return StatusCode(500, "An error occurred while processing the payment");
        }
    }

    /// <summary>
    /// Get payment by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PaymentResponse>> GetPayment(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var payment = await _paymentService.GetPaymentAsync(id, userId);
            
            if (payment == null)
            {
                return NotFound($"Payment with ID {id} not found");
            }

            return Ok(payment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment {PaymentId} for user {UserId}", id, userId);
            return StatusCode(500, "An error occurred while retrieving the payment");
        }
    }

    /// <summary>
    /// Get user's payments with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PaymentResponse>>> GetPayments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var payments = await _paymentService.GetUserPaymentsAsync(userId, page, pageSize, status);
            return Ok(payments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payments for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving payments");
        }
    }

    /// <summary>
    /// Cancel a pending payment
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult<PaymentResponse>> CancelPayment(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var payment = await _paymentService.CancelPaymentAsync(id, userId);
            
            if (payment == null)
            {
                return NotFound($"Payment with ID {id} not found");
            }

            _logger.LogInformation("Payment cancelled - ID: {PaymentId}, User: {UserId}", id, userId);
            return Ok(payment);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Cannot cancel payment {PaymentId} for user {UserId}: {Error}", id, userId, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling payment {PaymentId} for user {UserId}", id, userId);
            return StatusCode(500, "An error occurred while cancelling the payment");
        }
    }
}
