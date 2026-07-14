using EBanking.AccountService.Data;
using EBanking.AccountService.Models;
using Microsoft.EntityFrameworkCore;

namespace EBanking.AccountService.Services;

public class DatabaseSeeder
{
    private readonly AccountDbContext _context;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(AccountDbContext context, ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            // Ensure database is created
            await _context.Database.EnsureCreatedAsync();

            // Apply any pending migrations
            if ((await _context.Database.GetPendingMigrationsAsync()).Any())
            {
                _logger.LogInformation("Applying database migrations...");
                await _context.Database.MigrateAsync();
            }

            // Seed test data if not exists
            if (!await _context.Accounts.AnyAsync())
            {
                _logger.LogInformation("Seeding initial account data...");
                await SeedTestAccountsAsync();
            }

            // Ensure the golden-path principals always own an account:
            //  - "dev-user-id":  the dev-auth bypass principal (DevelopmentAuthenticationHandler)
            //  - "test-user-id": the Keycloak realm user "testuser" (its token sub)
            await EnsureDemoAccountAsync("dev-user-id", "1009999999", "BA391009999999000001");
            await EnsureDemoAccountAsync("test-user-id", "1008888888", "BA391008888888000001");

            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding database");
            throw;
        }
    }

    private async Task SeedTestAccountsAsync()
    {
        var testAccounts = new List<Account>
        {
            new Account
            {
                UserId = "test-user-1",
                AccountNumber = "1001234567",
                IBAN = "BA391001234567890123",
                Balance = 15000.00m,
                Currency = "EUR",
                CreatedAt = DateTime.UtcNow.AddMonths(-12),
                UpdatedAt = DateTime.UtcNow
            },
            new Account
            {
                UserId = "test-user-2",
                AccountNumber = "1001234568",
                IBAN = "BA391001234567890124",
                Balance = 25000.00m,
                Currency = "EUR",
                CreatedAt = DateTime.UtcNow.AddMonths(-8),
                UpdatedAt = DateTime.UtcNow
            }
        };

        _context.Accounts.AddRange(testAccounts);
        await _context.SaveChangesAsync();

        // Seed sample transactions
        await SeedTransactionsAsync(testAccounts);
    }

    private async Task EnsureDemoAccountAsync(string userId, string accountNumber, string iban)
    {
        if (await _context.Accounts.AnyAsync(a => a.UserId == userId))
            return;

        _logger.LogInformation("Seeding account for {UserId}...", userId);
        var account = new Account
        {
            UserId = userId,
            AccountNumber = accountNumber,
            IBAN = iban,
            Balance = 5000.00m,
            Currency = "EUR",
            CreatedAt = DateTime.UtcNow.AddMonths(-3),
            UpdatedAt = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        await SeedTransactionsAsync(new List<Account> { account });
    }

    private async Task SeedTransactionsAsync(List<Account> accounts)
    {
        var random = new Random();
        var transactions = new List<Transaction>();

        foreach (var account in accounts)
        {
            // Generate transactions for the past 3 months
            for (int month = 0; month < 3; month++)
            {
                var monthStart = DateTime.UtcNow.AddMonths(-month).Date.AddDays(-(DateTime.UtcNow.Day - 1));
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                // Generate 15-25 transactions per month
                var transactionCount = random.Next(15, 26);
                
                for (int i = 0; i < transactionCount; i++)
                {
                    var transactionDate = monthStart.AddDays(random.Next(0, (monthEnd - monthStart).Days));
                    var isCredit = random.NextDouble() > 0.6; // 40% chance of credit
                    var amount = (decimal)(random.NextDouble() * 1000 + 50);

                    transactions.Add(new Transaction
                    {
                        AccountId = account.Id,
                        Type = isCredit ? "CREDIT" : "DEBIT",
                        Amount = Math.Round(amount, 2),
                        Currency = "EUR",
                        Description = GenerateTransactionDescription(isCredit, random),
                        Reference = $"TXN{DateTime.UtcNow.Ticks}{i:D3}",
                        CounterpartyName = GenerateCounterpartyName(random),
                        CounterpartyAccount = GenerateIBAN(random),
                        TransactionDate = transactionDate,
                        CreatedAt = transactionDate,
                        CorrelationId = Guid.NewGuid().ToString(),
                        ExternalTransactionId = $"EXT{DateTime.UtcNow.Ticks}{i}"
                    });
                }
            }
        }

        _context.Transactions.AddRange(transactions);
        await _context.SaveChangesAsync();
    }

    private static string GenerateTransactionDescription(bool isCredit, Random random)
    {
        var creditDescriptions = new[]
        {
            "Salary payment",
            "Investment return",
            "Freelance payment",
            "Dividend payment",
            "Bonus payment",
            "Transfer from savings",
            "Refund",
            "Government benefit"
        };

        var debitDescriptions = new[]
        {
            "Grocery shopping",
            "Utility bill payment",
            "Online purchase",
            "Restaurant payment",
            "Gas station payment",
            "Insurance premium",
            "Subscription service",
            "ATM withdrawal",
            "Transfer to savings",
            "Loan payment",
            "Pharmacy purchase",
            "Mobile phone bill"
        };

        var descriptions = isCredit ? creditDescriptions : debitDescriptions;
        return descriptions[random.Next(descriptions.Length)];
    }

    private static string GenerateCounterpartyName(Random random)
    {
        var companies = new[]
        {
            "Mercator d.d.",
            "BH Telecom",
            "Elektroprivreda BiH",
            "Sarajevogas",
            "Konzum BiH",
            "UniCredit Bank",
            "McDonald's",
            "Tesco",
            "DM Drogerie",
            "Bingo",
            "Emmezeta",
            "IKEA",
            "Government of BiH",
            "Tax Administration"
        };

        return companies[random.Next(companies.Length)];
    }

    private static string GenerateIBAN(Random random)
    {
        var bankCodes = new[] { "1001", "1002", "1003", "1004", "1005" };
        var bankCode = bankCodes[random.Next(bankCodes.Length)];
        var accountNumber = random.Next(100000, 999999);
        
        return $"BA39{bankCode}{accountNumber:D6}789012";
    }
}
