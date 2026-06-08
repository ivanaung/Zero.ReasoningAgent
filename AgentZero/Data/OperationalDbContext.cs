using Microsoft.EntityFrameworkCore;
using ScheduleApp.Models;

namespace ScheduleApp.Data;

public class OperationalDbContext(DbContextOptions<OperationalDbContext> options) : DbContext(options)
{
    public DbSet<OperationalScheduleEvent> Events => Set<OperationalScheduleEvent>();
    public DbSet<OperationalZeroConversationMessage> ZeroConversationMessages => Set<OperationalZeroConversationMessage>();
    public DbSet<OperationalAiActionAudit> AiActionAudits => Set<OperationalAiActionAudit>();
    public DbSet<OperationalFinanceTransaction> FinanceTransactions => Set<OperationalFinanceTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OperationalScheduleEvent>(entity =>
        {
            entity.ToTable("Events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.StartDateTime).HasColumnType("timestamp without time zone");
            entity.Property(e => e.EndDateTime).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.AssignedTo).HasMaxLength(120);
            entity.Property(e => e.Icon).HasMaxLength(50);
            entity.Property(e => e.AreaName).HasMaxLength(100);
            entity.Property(e => e.RecurrenceRule);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.Priority).HasConversion<int>();
            entity.HasIndex(e => e.StartDateTime);
            entity.HasIndex(e => e.EndDateTime);
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.AssignedTo);
        });

        modelBuilder.Entity<OperationalZeroConversationMessage>(entity =>
        {
            entity.ToTable("ZeroConversationMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.ConversationId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(40);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(8000);
            entity.Property(e => e.CreatedUtc).HasColumnType("timestamp without time zone");
            entity.HasIndex(e => new { e.UserId, e.ConversationId, e.Id });
        });

        modelBuilder.Entity<OperationalAiActionAudit>(entity =>
        {
            entity.ToTable("AiActionAudits");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.UserId).HasMaxLength(120);
            entity.Property(e => e.UserDisplayName).HasMaxLength(120);
            entity.Property(e => e.ActionType).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Provider).HasMaxLength(120);
            entity.Property(e => e.ModelId).HasMaxLength(200);
            entity.Property(e => e.Summary).IsRequired().HasMaxLength(4000);
            entity.Property(e => e.RequestPreview).HasMaxLength(8000);
            entity.Property(e => e.ResponsePreview).HasMaxLength(8000);
            entity.Property(e => e.Outcome).HasMaxLength(40);
            entity.Property(e => e.OccurredAtUtc).HasColumnType("timestamp without time zone");
            entity.HasIndex(e => e.OccurredAtUtc);
            entity.HasIndex(e => e.ActionType);
        });

        modelBuilder.Entity<OperationalFinanceTransaction>(entity =>
        {
            entity.ToTable("FinanceTransactions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.PaymentMethod).HasMaxLength(80);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.TransactionDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.UpdatedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Scope).HasConversion<int>();
            entity.HasIndex(e => new { e.UserId, e.TransactionDate });
            entity.HasIndex(e => e.ProjectId);
        });
    }
}
