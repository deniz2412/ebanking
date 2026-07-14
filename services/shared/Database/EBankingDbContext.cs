using Microsoft.EntityFrameworkCore;
using Shared.Database.Models;

namespace Shared.Database;

public class EBankingDbContext : DbContext
{
    public EBankingDbContext(DbContextOptions<EBankingDbContext> options) : base(options)
    {
    }

    // Account Service Tables
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Statement> Statements { get; set; }

    // Payment Service Tables
    public DbSet<Payment> Payments { get; set; }
    public DbSet<PaymentTemplate> PaymentTemplates { get; set; }

    // Transfer Service Tables
    public DbSet<Transfer> Transfers { get; set; }
    public DbSet<StandingOrder> StandingOrders { get; set; }
    public DbSet<StandingOrderExecution> StandingOrderExecutions { get; set; }
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }

    // Audit Service Tables
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<AuditLogEntry> AuditLogEntries { get; set; }
    public DbSet<IntegrityCheck> IntegrityChecks { get; set; }

    // Notification Service Tables
    public DbSet<NotificationSubscription> NotificationSubscriptions { get; set; }
    public DbSet<NotificationMessage> NotificationMessages { get; set; }
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure all entity models
        ConfigureAccountModels(modelBuilder);
        ConfigurePaymentModels(modelBuilder);
        ConfigureTransferModels(modelBuilder);
        ConfigureAuditModels(modelBuilder);
        ConfigureNotificationModels(modelBuilder);

        // Seed data for development
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            SeedDevelopmentData(modelBuilder);
        }
    }

    private static void ConfigureAccountModels(ModelBuilder modelBuilder)
    {
        // Account configuration
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("Accounts");
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
            entity.ToTable("Transactions");
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
            entity.ToTable("Statements");
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
    }

    private static void ConfigurePaymentModels(ModelBuilder modelBuilder)
    {
        // Payment configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FromAccount).IsRequired().HasMaxLength(34); // IBAN max length
            entity.Property(e => e.ToAccount).IsRequired().HasMaxLength(34);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entity.Property(e => e.Reference).HasMaxLength(140);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.PaymentType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.Property(e => e.BeneficiaryName).HasMaxLength(100);
            entity.Property(e => e.PayerName).HasMaxLength(100);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
        });

        // PaymentTemplate configuration
        modelBuilder.Entity<PaymentTemplate>(entity =>
        {
            entity.ToTable("PaymentTemplates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ToAccount).IsRequired().HasMaxLength(34);
            entity.Property(e => e.BeneficiaryName).HasMaxLength(100);
            entity.Property(e => e.DefaultAmount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
            entity.Property(e => e.DefaultReference).HasMaxLength(140);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsActive });
        });
    }

    private static void ConfigureTransferModels(ModelBuilder modelBuilder)
    {
        // Transfer configuration
        modelBuilder.Entity<Transfer>(entity =>
        {
            entity.ToTable("Transfers");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IdempotencyKey);
            entity.HasIndex(e => e.CorrelationId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.RequestedAt);

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.FromAccountNumber)
                .IsRequired()
                .HasMaxLength(34);

            entity.Property(e => e.ToAccountNumber)
                .IsRequired()
                .HasMaxLength(34);

            entity.Property(e => e.ToAccountName)
                .IsRequired()
                .HasMaxLength(200);

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

            entity.Property(e => e.IdempotencyKey)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.CorrelationId)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(1000);

            entity.Property(e => e.ExternalTransactionId)
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Standing Order configuration
        modelBuilder.Entity<StandingOrder>(entity =>
        {
            entity.ToTable("StandingOrders");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.NextExecutionDate);

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.FromAccountNumber)
                .IsRequired()
                .HasMaxLength(34);

            entity.Property(e => e.ToAccountNumber)
                .IsRequired()
                .HasMaxLength(34);

            entity.Property(e => e.ToAccountName)
                .IsRequired()
                .HasMaxLength(200);

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

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Standing Order Execution configuration
        modelBuilder.Entity<StandingOrderExecution>(entity =>
        {
            entity.ToTable("StandingOrderExecutions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.StandingOrderId);
            entity.HasIndex(e => e.ExecutedAt);
            entity.HasIndex(e => e.Status);

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(1000);

            entity.Property(e => e.TransferReference)
                .HasMaxLength(100);

            entity.HasOne(e => e.StandingOrder)
                .WithMany(so => so.Executions)
                .HasForeignKey(e => e.StandingOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Idempotency Record configuration
        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("IdempotencyRecords");
            entity.HasKey(e => e.Key);
            entity.HasIndex(e => new { e.Key, e.UserId }).IsUnique();
            entity.HasIndex(e => e.ExpiresAt);

            entity.Property(e => e.Key)
                .HasMaxLength(100);

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.RequestHash)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.ResponseData)
                .HasMaxLength(4000);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureAuditModels(ModelBuilder modelBuilder)
    {
        // AuditLogEntry configuration (new hash chain model)
        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("AuditLogEntries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.AggregateType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AggregateId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.EventData).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(100);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.Property(e => e.CausationId).HasMaxLength(100);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PreviousHash).HasMaxLength(64);
            entity.Property(e => e.EventHash).IsRequired().HasMaxLength(64);

            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.AggregateType);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CorrelationId);
            entity.HasIndex(e => e.EventHash).IsUnique();
        });

        // AuditLog configuration (legacy, keeping for compatibility)
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SequenceNumber)
                  .ValueGeneratedOnAdd()
                  .UseIdentityColumn(1, 1);

            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Service).IsRequired().HasMaxLength(50);
            entity.Property(e => e.UserId).HasMaxLength(100);
            entity.Property(e => e.EntityId).HasMaxLength(100);
            entity.Property(e => e.EntityType).HasMaxLength(50);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Data).IsRequired();
            entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(45); // IPv6 support
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.RequestId).HasMaxLength(100);
            entity.Property(e => e.SessionId).HasMaxLength(100);
            entity.Property(e => e.PreviousHash).HasMaxLength(64); // SHA-256 hex
            entity.Property(e => e.Hash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Signature).HasMaxLength(512);

            // Indexes for efficient querying
            entity.HasIndex(e => e.SequenceNumber).IsUnique();
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Service);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.HasIndex(e => new { e.Service, e.Timestamp });
            entity.HasIndex(e => new { e.UserId, e.Timestamp });
            entity.HasIndex(e => e.Hash);
        });

        // IntegrityCheck configuration
        modelBuilder.Entity<IntegrityCheck>(entity =>
        {
            entity.ToTable("IntegrityChecks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Issues).HasMaxLength(2000);
            entity.Property(e => e.CheckedBy).IsRequired().HasMaxLength(100);

            entity.HasIndex(e => e.CheckedAt);
            entity.HasIndex(e => new { e.FromSequence, e.ToSequence });
            entity.HasIndex(e => e.IsValid);
        });
    }

    private static void ConfigureNotificationModels(ModelBuilder modelBuilder)
    {
        // NotificationSubscription configuration
        modelBuilder.Entity<NotificationSubscription>(entity =>
        {
            entity.ToTable("NotificationSubscriptions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Endpoint).IsRequired().HasMaxLength(500);
            entity.Property(e => e.P256dh).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Auth).IsRequired().HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsActive });
            entity.HasIndex(e => e.Endpoint).IsUnique();
        });

        // NotificationMessage configuration
        modelBuilder.Entity<NotificationMessage>(entity =>
        {
            entity.ToTable("NotificationMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Body).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Icon).HasMaxLength(500);
            entity.Property(e => e.Badge).HasMaxLength(500);
            entity.Property(e => e.Data).HasMaxLength(2000); // JSON data
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Priority).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.FailureReason).HasMaxLength(500);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.UserId, e.Status });
            entity.HasIndex(e => new { e.Status, e.NextRetryAt });
        });

        // NotificationTemplate configuration
        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.ToTable("NotificationTemplates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TitleTemplate).IsRequired().HasMaxLength(200);
            entity.Property(e => e.BodyTemplate).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.IconUrl).HasMaxLength(500);
            entity.Property(e => e.Priority).IsRequired().HasMaxLength(20);

            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => new { e.EventType, e.IsActive });
        });
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