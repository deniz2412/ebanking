using EBanking.PaymentService.Models;
using Microsoft.EntityFrameworkCore;

namespace EBanking.PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<Payment> Payments { get; set; }
    public DbSet<PaymentTemplate> PaymentTemplates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        // Payment configuration
        modelBuilder.Entity<Payment>(entity =>
        {
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

        base.OnModelCreating(modelBuilder);
    }
}
