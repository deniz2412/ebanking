using EBanking.PaymentService.DTOs;
using EBanking.PaymentService.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EBanking.PaymentService.Controllers;

[ApiController]
[Route("api/payment-templates")]
[Authorize(Policy = "PaymentAccess")]
public class PaymentTemplatesController : ControllerBase
{
    private readonly IPaymentTemplateService _templateService;
    private readonly ILogger<PaymentTemplatesController> _logger;

    public PaymentTemplatesController(IPaymentTemplateService templateService, ILogger<PaymentTemplatesController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new payment template
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PaymentTemplateResponse>> CreateTemplate([FromBody] CreatePaymentTemplateRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var template = await _templateService.CreateTemplateAsync(userId, request);
            
            // Log template creation with masked account details
            _logger.LogInformation("Payment template created - ID: {TemplateId}, User: {UserId}, Name: {TemplateName}",
                template.Id, userId, template.Name);
            
            return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, template);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Template creation failed - User: {UserId}, Error: {Error}", userId, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating template for user: {UserId}", userId);
            return StatusCode(500, "An error occurred while creating the template");
        }
    }

    /// <summary>
    /// Get template by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PaymentTemplateResponse>> GetTemplate(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var template = await _templateService.GetTemplateAsync(id, userId);
            
            if (template == null)
            {
                return NotFound($"Template with ID {id} not found");
            }

            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving template {TemplateId} for user {UserId}", id, userId);
            return StatusCode(500, "An error occurred while retrieving the template");
        }
    }

    /// <summary>
    /// Get user's payment templates
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PaymentTemplateResponse>>> GetTemplates([FromQuery] bool activeOnly = true)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var templates = await _templateService.GetUserTemplatesAsync(userId, activeOnly);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving templates for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving templates");
        }
    }

    /// <summary>
    /// Update payment template
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<PaymentTemplateResponse>> UpdateTemplate(Guid id, [FromBody] UpdatePaymentTemplateRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var template = await _templateService.UpdateTemplateAsync(id, userId, request);
            
            if (template == null)
            {
                return NotFound($"Template with ID {id} not found");
            }

            // Log template update with masked sensitive details
            _logger.LogInformation("Payment template updated - ID: {TemplateId}, User: {UserId}, Name: {TemplateName}",
                template.Id, userId, template.Name);
            
            return Ok(template);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Template update failed - Template: {TemplateId}, User: {UserId}, Error: {Error}", 
                id, userId, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template {TemplateId} for user {UserId}", id, userId);
            return StatusCode(500, "An error occurred while updating the template");
        }
    }

    /// <summary>
    /// Delete payment template
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteTemplate(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var success = await _templateService.DeleteTemplateAsync(id, userId);
            
            if (!success)
            {
                return NotFound($"Template with ID {id} not found");
            }

            _logger.LogInformation("Payment template deleted - ID: {TemplateId}, User: {UserId}", id, userId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template {TemplateId} for user {UserId}", id, userId);
            return StatusCode(500, "An error occurred while deleting the template");
        }
    }
}
