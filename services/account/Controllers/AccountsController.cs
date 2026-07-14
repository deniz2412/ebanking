using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EBanking.AccountService.Services.Interfaces;
using EBanking.AccountService.Models.DTOs;
using System.Security.Claims;

namespace EBanking.AccountService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly IPdfService _pdfService;
    private readonly ILogger<AccountsController> _logger;

    public AccountsController(
        IAccountService accountService,
        IPdfService pdfService,
        ILogger<AccountsController> logger)
    {
        _accountService = accountService;
        _pdfService = pdfService;
        _logger = logger;
    }

    /// <summary>
    /// Look up an account by its number. Object-level authorization is always enforced:
    /// the resolved account must belong to the authenticated principal, otherwise 403.
    /// </summary>
    [HttpGet("by-number/{accountNumber}")]
    public async Task<ActionResult<BalanceResponse>> GetByAccountNumber(string accountNumber)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var account = await _accountService.GetAccountByNumberAsync(accountNumber);
        if (account == null)
            return NotFound("Account not found");

        if (account.UserId != userId)
        {
            _logger.LogWarning("BOLA blocked: {UserId} tried to read account {AccountNumber} owned by {OwnerId}",
                userId, accountNumber, account.UserId);
            return Forbid();
        }

        return Ok(new BalanceResponse
        {
            Balance = account.Balance,
            Currency = account.Currency,
            UserId = account.UserId,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get current account balance for authenticated user
    /// </summary>
    [HttpGet("me/balance")]
    public async Task<ActionResult<BalanceResponse>> GetBalance()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var balance = await _accountService.GetBalanceAsync(userId);
            return Ok(new BalanceResponse
            {
                Balance = balance,
                Currency = "EUR",
                UserId = userId,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Account not found for user {UserId}: {Message}", userId, ex.Message);
            return NotFound("Account not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting balance for user {UserId}", userId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get transaction history with pagination and filtering
    /// </summary>
    [HttpGet("me/transactions")]
    public async Task<ActionResult<PagedTransactionResponse>> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
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
            var result = await _accountService.GetTransactionsAsync(userId, page, pageSize, from, to);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Account not found for user {UserId}: {Message}", userId, ex.Message);
            return NotFound("Account not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transactions for user {UserId}", userId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Download PDF statement for a specific month
    /// </summary>
    [HttpGet("me/statements/{year:int}/{month:int}.pdf")]
    public async Task<IActionResult> GetStatement(int year, int month)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (year < 2000 || year > DateTime.UtcNow.Year)
            return BadRequest("Invalid year");

        if (month < 1 || month > 12)
            return BadRequest("Invalid month");

        // Don't allow future statements
        var requestedDate = new DateTime(year, month, 1);
        if (requestedDate > DateTime.UtcNow)
            return BadRequest("Cannot generate statements for future dates");

        try
        {
            var transactions = await _accountService.GetTransactionsForPeriodAsync(userId, year, month);
            var pdfBytes = await _pdfService.GenerateStatementAsync(userId, transactions, year, month);

            return File(pdfBytes, "application/pdf", $"statement-{year}-{month:D2}.pdf");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Account not found for user {UserId}: {Message}", userId, ex.Message);
            return NotFound("Account not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating statement for user {UserId}, {Year}/{Month}", userId, year, month);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get account details for authenticated user
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<AccountDetailsResponse>> GetAccountDetails()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var account = await _accountService.GetAccountDetailsAsync(userId);
            if (account == null)
                return NotFound("Account not found");

            return Ok(account);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting account details for user {UserId}", userId);
            return StatusCode(500, "Internal server error");
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               User.FindFirst("sub")?.Value;
    }
}
