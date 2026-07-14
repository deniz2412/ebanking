using EBanking.AccountService.Models;
using EBanking.AccountService.Models.DTOs;

namespace EBanking.AccountService.Services.Interfaces;

public interface IAccountService
{
    Task<decimal> GetBalanceAsync(string userId);
    Task<PagedTransactionResponse> GetTransactionsAsync(string userId, int page, int pageSize, DateTime? from, DateTime? to);
    Task<IEnumerable<TransactionDto>> GetTransactionsForPeriodAsync(string userId, int year, int month);
    Task<Account?> GetAccountByUserIdAsync(string userId);
    Task<Account?> GetAccountByNumberAsync(string accountNumber);
    Task<AccountDetailsResponse?> GetAccountDetailsAsync(string userId);
}

public interface IPdfService
{
    Task<byte[]> GenerateStatementAsync(string userId, IEnumerable<TransactionDto> transactions, int year, int month);
}
