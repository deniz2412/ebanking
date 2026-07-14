using EBanking.AccountService.Models;
using Microsoft.EntityFrameworkCore;

namespace EBanking.AccountService.Data;

public class AccountDbContext : DbContext
{
    public AccountDbContext(DbContextOptions<AccountDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Statement> Statements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Account configuration
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.AccountNumber).IsUnique();
            entity.HasIndex(e => e.IBAN).IsUnique();

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.AccountNumber)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.IBAN)
                .IsRequired()
                .HasMaxLength(34);

            entity.Property(e => e.Balance)
                .HasPrecision(18, 2);

            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .HasDefaultValue("EUR");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Transaction configuration
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AccountId);
            entity.HasIndex(e => e.TransactionDate);
            entity.HasIndex(e => e.CorrelationId);
            entity.HasIndex(e => e.ExternalTransactionId);

            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.Amount)
                .HasPrecision(18, 2);

            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .HasDefaultValue("EUR");

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Reference)
                .HasMaxLength(100);

            entity.Property(e => e.CounterpartyName)
                .HasMaxLength(200);

            entity.Property(e => e.CounterpartyAccount)
                .HasMaxLength(34);

            entity.Property(e => e.CorrelationId)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.ExternalTransactionId)
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Statement configuration
        modelBuilder.Entity<Statement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.AccountId, e.Year, e.Month }).IsUnique();

            entity.Property(e => e.OpeningBalance)
                .HasPrecision(18, 2);

            entity.Property(e => e.ClosingBalance)
                .HasPrecision(18, 2);

            entity.Property(e => e.TotalDebits)
                .HasPrecision(18, 2);

            entity.Property(e => e.TotalCredits)
                .HasPrecision(18, 2);

            entity.Property(e => e.GeneratedBy)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.GeneratedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Account)
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed data for development
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            SeedDevelopmentData(modelBuilder);
        }
    }

    private static void SeedDevelopmentData(ModelBuilder modelBuilder)
    {
        // Sample account for testing
        modelBuilder.Entity<Account>().HasData(
            new Account
            {
                Id = 1,
                UserId = "test-user-123",
                AccountNumber = "1234567890",
                IBAN = "BA391234567890123456",
                Balance = 5000.00m,
                Currency = "EUR",
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                UpdatedAt = DateTime.UtcNow
            }
        );

        // Sample transactions
        var baseDate = DateTime.UtcNow.AddDays(-30);
        for (int i = 1; i <= 10; i++)
        {
            modelBuilder.Entity<Transaction>().HasData(
                new Transaction
                {
                    Id = i,
                    AccountId = 1,
                    Type = i % 2 == 0 ? "CREDIT" : "DEBIT",
                    Amount = (decimal)(100 + (i * 50)),
                    Currency = "EUR",
                    Description = $"Sample transaction {i}",
                    Reference = $"REF{i:D4}",
                    TransactionDate = baseDate.AddDays(i * 3),
                    CreatedAt = baseDate.AddDays(i * 3),
                    CorrelationId = Guid.NewGuid().ToString()
                }
            );
        }
    }
}
