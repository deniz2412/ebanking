using EBanking.AuditService.Models;
using Microsoft.EntityFrameworkCore;

namespace EBanking.AuditService.Data;

public class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }

    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<IntegrityCheck> IntegrityChecks { get; set; }
    public DbSet<AuditLogEntry> AuditLogEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // AuditLogEntry configuration (new hash chain model)
        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
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
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Issues).HasMaxLength(2000);
            entity.Property(e => e.CheckedBy).IsRequired().HasMaxLength(100);

            entity.HasIndex(e => e.CheckedAt);
            entity.HasIndex(e => new { e.FromSequence, e.ToSequence });
            entity.HasIndex(e => e.IsValid);
        });

        base.OnModelCreating(modelBuilder);
    }
}
