using EBanking.TransferService.Models;
using Microsoft.EntityFrameworkCore;

namespace EBanking.TransferService.Data;

public class TransferDbContext : DbContext
{
    public TransferDbContext(DbContextOptions<TransferDbContext> options) : base(options)
    {
    }

    public DbSet<Transfer> Transfers { get; set; }
    public DbSet<StandingOrder> StandingOrders { get; set; }
    public DbSet<StandingOrderExecution> StandingOrderExecutions { get; set; }
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Transfer configuration
        modelBuilder.Entity<Transfer>(entity =>
        {
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
}
