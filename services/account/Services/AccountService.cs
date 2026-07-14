using EBanking.AccountService.Data;
using EBanking.AccountService.Models;
using EBanking.AccountService.Models.DTOs;
using EBanking.AccountService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EBanking.AccountService.Services;

public class AccountService : IAccountService
{
    private readonly AccountDbContext _context;
    private readonly ILogger<AccountService> _logger;
    
    public AccountService(AccountDbContext context, ILogger<AccountService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public async Task<decimal> GetBalanceAsync(string userId)
    {
        var account = await GetAccountByUserIdAsync(userId);
        if (account == null)
        {
            _logger.LogWarning("Account not found for user {UserId}", userId);
            throw new ArgumentException("Account not found", nameof(userId));
        }
        
        return account.Balance;
    }
    
    public async Task<PagedTransactionResponse> GetTransactionsAsync(
        string userId, 
        int page, 
        int pageSize, 
        DateTime? from, 
        DateTime? to)
    {
        var account = await GetAccountByUserIdAsync(userId);
        if (account == null)
            throw new ArgumentException("Account not found", nameof(userId));
            
        var query = _context.Transactions
            .Where(t => t.AccountId == account.Id);
            
        if (from.HasValue)
            query = query.Where(t => t.TransactionDate >= from.Value);
            
        if (to.HasValue)
            query = query.Where(t => t.TransactionDate <= to.Value);
            
        var totalCount = await query.CountAsync();
        
        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                Type = t.Type,
                Amount = t.Amount,
                Currency = t.Currency,
                Description = t.Description,
                Reference = t.Reference,
                CounterpartyName = t.CounterpartyName,
                CounterpartyAccount = t.CounterpartyAccount,
                TransactionDate = t.TransactionDate,
                CorrelationId = t.CorrelationId
            })
            .ToListAsync();
            
        return new PagedTransactionResponse
        {
            Transactions = transactions,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };
    }
    
    public async Task<IEnumerable<TransactionDto>> GetTransactionsForPeriodAsync(string userId, int year, int month)
    {
        var account = await GetAccountByUserIdAsync(userId);
        if (account == null)
            throw new ArgumentException("Account not found", nameof(userId));
            
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);
        
        return await _context.Transactions
            .Where(t => t.AccountId == account.Id && 
                       t.TransactionDate >= startDate && 
                       t.TransactionDate <= endDate)
            .OrderBy(t => t.TransactionDate)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                Type = t.Type,
                Amount = t.Amount,
                Currency = t.Currency,
                Description = t.Description,
                Reference = t.Reference,
                CounterpartyName = t.CounterpartyName,
                CounterpartyAccount = t.CounterpartyAccount,
                TransactionDate = t.TransactionDate,
                CorrelationId = t.CorrelationId
            })
            .ToListAsync();
    }
    
    public async Task<Account?> GetAccountByUserIdAsync(string userId)
    {
        return await _context.Accounts
            .FirstOrDefaultAsync(a => a.UserId == userId);
    }

    public async Task<Account?> GetAccountByNumberAsync(string accountNumber)
    {
        return await _context.Accounts
            .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);
    }

    public async Task<AccountDetailsResponse?> GetAccountDetailsAsync(string userId)
    {
        var account = await _context.Accounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (account == null)
            return null;

        var lastTransaction = account.Transactions
            .OrderByDescending(t => t.TransactionDate)
            .FirstOrDefault();

        return new AccountDetailsResponse
        {
            AccountNumber = account.AccountNumber,
            IBAN = account.IBAN,
            Balance = account.Balance,
            Currency = account.Currency,
            CreatedAt = account.CreatedAt,
            LastTransactionDate = lastTransaction?.TransactionDate ?? account.CreatedAt,
            TotalTransactions = account.Transactions.Count
        };
    }
    
    public async Task<Transaction> CreateTransactionAsync(string userId, decimal amount, string type, 
        string description, string? reference = null, string? counterpartyName = null, 
        string? counterpartyAccount = null, string? correlationId = null)
    {
        var account = await GetAccountByUserIdAsync(userId);
        if (account == null)
            throw new ArgumentException("Account not found", nameof(userId));

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Create transaction
            var newTransaction = new Transaction
            {
                AccountId = account.Id,
                Type = type,
                Amount = amount,
                Currency = account.Currency,
                Description = description,
                Reference = reference,
                CounterpartyName = counterpartyName,
                CounterpartyAccount = counterpartyAccount,
                TransactionDate = DateTime.UtcNow,
                CorrelationId = correlationId ?? Guid.NewGuid().ToString()
            };

            _context.Transactions.Add(newTransaction);

            // Update account balance
            if (type == "CREDIT")
                account.Balance += amount;
            else if (type == "DEBIT")
                account.Balance -= amount;

            account.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Transaction {TransactionId} created for account {AccountId}, new balance: {Balance}",
                newTransaction.Id, account.Id, account.Balance);

            return newTransaction;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
