using Microsoft.EntityFrameworkCore;
using ScheduleApp.Models;
using ScheduleApp.Modules.Trading;

namespace ScheduleApp.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ScheduleEvent> Events => Set<ScheduleEvent>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectStage> ProjectStages => Set<ProjectStage>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AiSettings> AiSettings => Set<AiSettings>();
    public DbSet<GoogleIntegrationSettings> GoogleIntegrationSettings => Set<GoogleIntegrationSettings>();
    public DbSet<UserGoogleAccount> UserGoogleAccounts => Set<UserGoogleAccount>();
    public DbSet<CachedEmailMessage> CachedEmailMessages => Set<CachedEmailMessage>();
    public DbSet<McpApiKey> McpApiKeys => Set<McpApiKey>();
    public DbSet<AiActionAudit> AiActionAudits => Set<AiActionAudit>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<ProjectComment> ProjectComments => Set<ProjectComment>();
    public DbSet<NotificationScheduleEntry> NotificationScheduleEntries => Set<NotificationScheduleEntry>();
    public DbSet<UserProactivePreference> UserProactivePreferences => Set<UserProactivePreference>();
    public DbSet<NotificationDeliveryLog> NotificationDeliveryLogs => Set<NotificationDeliveryLog>();
    public DbSet<FinanceAccount> FinanceAccounts => Set<FinanceAccount>();
    public DbSet<FinanceCategory> FinanceCategories => Set<FinanceCategory>();
    public DbSet<FinanceTransaction> FinanceTransactions => Set<FinanceTransaction>();
    public DbSet<FinanceBudget> FinanceBudgets => Set<FinanceBudget>();
    public DbSet<FinanceRecurringItem> FinanceRecurringItems => Set<FinanceRecurringItem>();
    public DbSet<ZeroAssistantSettings> ZeroAssistantSettings => Set<ZeroAssistantSettings>();
    public DbSet<ZeroMemoryItem> ZeroMemoryItems => Set<ZeroMemoryItem>();
    public DbSet<ZeroConversationMessage> ZeroConversationMessages => Set<ZeroConversationMessage>();
    public DbSet<OperationalDatabaseSettings> OperationalDatabaseSettings => Set<OperationalDatabaseSettings>();
    public DbSet<MarketDataProviderSettings> MarketDataProviderSettings => Set<MarketDataProviderSettings>();
    public DbSet<TradingHolding> TradingHoldings => Set<TradingHolding>();
    public DbSet<TradingWatchlistItem> TradingWatchlistItems => Set<TradingWatchlistItem>();
    public DbSet<TradingJournalEntry> TradingJournalEntries => Set<TradingJournalEntry>();
    public DbSet<TradingAdvisorSnapshot> TradingAdvisorSnapshots => Set<TradingAdvisorSnapshot>();
    public DbSet<TradingPriceSnapshot> TradingPriceSnapshots => Set<TradingPriceSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Color).HasMaxLength(20);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Color).HasMaxLength(20);
        });

        modelBuilder.Entity<ProjectStage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(120);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Email).HasMaxLength(320);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PasswordHash).HasMaxLength(4000);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(40);
            entity.Property(e => e.AuthMode).IsRequired().HasMaxLength(40);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<ScheduleEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.AssignedTo).HasMaxLength(120);
            entity.Property(e => e.Icon).HasMaxLength(50);
            entity.Property(e => e.AreaName).HasMaxLength(100);

            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Project)
                .WithMany(p => p.Events)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Stage)
                .WithMany(s => s.Events)
                .HasForeignKey(e => e.StageId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.DependsOn)
                .WithMany()
                .HasForeignKey(e => e.DependsOnId)
                .OnDelete(DeleteBehavior.NoAction); // Prevent cycles or cascading deletes
        });

        modelBuilder.Entity<AiSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ModelId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.EndpointUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ApiKeyEncrypted).HasMaxLength(4000);
            entity.Property(e => e.SystemPrompt).IsRequired().HasMaxLength(8000);
            entity.Property(e => e.DefaultTimeZone).IsRequired().HasMaxLength(80);
            entity.Property(e => e.WorkingHoursStart).IsRequired().HasMaxLength(5);
            entity.Property(e => e.WorkingHoursEnd).IsRequired().HasMaxLength(5);
            entity.Property(e => e.WorkingDays).IsRequired().HasMaxLength(64);
            entity.Property(e => e.MorningDigestTime).IsRequired().HasMaxLength(5);
            entity.Property(e => e.AfternoonDigestTime).IsRequired().HasMaxLength(5);
            entity.Property(e => e.QuietHoursStart).IsRequired().HasMaxLength(5);
            entity.Property(e => e.QuietHoursEnd).IsRequired().HasMaxLength(5);
            entity.Property(e => e.DefaultUserTimeZone).IsRequired().HasMaxLength(80);
        });

        modelBuilder.Entity<GoogleIntegrationSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientId).HasMaxLength(200);
            entity.Property(e => e.ClientSecretEncrypted).HasMaxLength(4000);
            entity.Property(e => e.RefreshTokenEncrypted).HasMaxLength(4000);
            entity.Property(e => e.AccessTokenEncrypted).HasMaxLength(4000);
            entity.Property(e => e.ConnectedEmail).HasMaxLength(320);
            entity.Property(e => e.ScopeSet).IsRequired().HasMaxLength(200);
            entity.Property(e => e.InboxCacheLimit).HasDefaultValue(100);
        });

        modelBuilder.Entity<UserGoogleAccount>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasMaxLength(120);
            entity.Property(e => e.GoogleSubjectId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(320);
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.RefreshTokenEncrypted).HasMaxLength(4000);
            entity.Property(e => e.AccessTokenEncrypted).HasMaxLength(4000);
            entity.Property(e => e.PictureUrl).HasMaxLength(500);
            entity.Property(e => e.ScopeSet).HasMaxLength(500);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.GoogleSubjectId).IsUnique();
        });

        modelBuilder.Entity<CachedEmailMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.MessageId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Subject).HasMaxLength(500);
            entity.Property(e => e.From).HasColumnName("Sender").HasMaxLength(500);
            entity.Property(e => e.Snippet).HasMaxLength(4000);
            entity.Property(e => e.WebUrl).HasMaxLength(500);
            entity.Property(e => e.HtmlBody);
            entity.Property(e => e.TextBody);
            entity.HasIndex(e => new { e.UserId, e.MessageId }).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.ReceivedAt });
        });

        modelBuilder.Entity<McpApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(120);
            entity.Property(e => e.KeyPrefix).IsRequired().HasMaxLength(32);
            entity.Property(e => e.KeyHash).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.KeyPrefix).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.IsActive });
        });

        modelBuilder.Entity<AiActionAudit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(120);
            entity.Property(e => e.UserDisplayName).HasMaxLength(120);
            entity.Property(e => e.ActionType).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Provider).HasMaxLength(120);
            entity.Property(e => e.ModelId).HasMaxLength(200);
            entity.Property(e => e.Summary).IsRequired().HasMaxLength(4000);
            entity.Property(e => e.RequestPreview).HasMaxLength(8000);
            entity.Property(e => e.ResponsePreview).HasMaxLength(8000);
            entity.Property(e => e.Outcome).HasMaxLength(40);
        });

        modelBuilder.Entity<TaskComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AuthorId).HasMaxLength(120);
            entity.Property(e => e.AuthorName).HasMaxLength(120);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(4000);
            entity.HasOne(e => e.Task)
                .WithMany()
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AuthorId).HasMaxLength(120);
            entity.Property(e => e.AuthorName).HasMaxLength(120);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(4000);
            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationScheduleEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.UserDisplayName).HasMaxLength(120);
            entity.Property(e => e.TimeZone).IsRequired().HasMaxLength(80);
            entity.Property(e => e.ContextSnapshotJson).HasMaxLength(8000);
            entity.Property(e => e.TriggerSource).HasMaxLength(80);
            entity.Property(e => e.DeduplicationKey).IsRequired().HasMaxLength(250);
            entity.Property(e => e.Message).HasMaxLength(2000);
            entity.Property(e => e.ActionUrl).HasMaxLength(500);
            entity.HasIndex(e => new { e.Status, e.ScheduledForUtc });
            entity.HasIndex(e => e.DeduplicationKey);

            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Task)
                .WithMany()
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UserProactivePreference>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasMaxLength(120);
            entity.Property(e => e.UserDisplayName).HasMaxLength(120);
            entity.Property(e => e.PreferredMorningDigestTime).HasMaxLength(5);
            entity.Property(e => e.PreferredAfternoonDigestTime).HasMaxLength(5);
            entity.Property(e => e.QuietHoursStart).HasMaxLength(5);
            entity.Property(e => e.QuietHoursEnd).HasMaxLength(5);
            entity.Property(e => e.PreferredChannels).HasMaxLength(120);
            entity.Property(e => e.TimeZone).HasMaxLength(80);
        });

        modelBuilder.Entity<NotificationDeliveryLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(120);
            entity.Property(e => e.Channel).HasMaxLength(40);
            entity.Property(e => e.Status).HasMaxLength(40);
            entity.Property(e => e.ProviderModel).HasMaxLength(120);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(e => new { e.NotificationScheduleEntryId, e.Channel, e.SentUtc });

            entity.HasOne(e => e.NotificationScheduleEntry)
                .WithMany()
                .HasForeignKey(e => e.NotificationScheduleEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FinanceAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(120);
            entity.HasIndex(e => new { e.UserId, e.Name });
        });

        modelBuilder.Entity<FinanceCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(120);
            entity.HasIndex(e => new { e.UserId, e.Name, e.Type });
        });

        modelBuilder.Entity<FinanceTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.PaymentMethod).HasMaxLength(80);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.HasIndex(e => new { e.UserId, e.TransactionDate });

            entity.HasOne(e => e.FinanceAccount)
                .WithMany()
                .HasForeignKey(e => e.FinanceAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.FinanceCategory)
                .WithMany()
                .HasForeignKey(e => e.FinanceCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FinanceBudget>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.HasIndex(e => new { e.UserId, e.FinanceCategoryId, e.Scope, e.Month, e.Year }).IsUnique();

            entity.HasOne(e => e.FinanceCategory)
                .WithMany()
                .HasForeignKey(e => e.FinanceCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FinanceRecurringItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.HasIndex(e => new { e.UserId, e.NextDueDate, e.IsActive });

            entity.HasOne(e => e.FinanceAccount)
                .WithMany()
                .HasForeignKey(e => e.FinanceAccountId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.FinanceCategory)
                .WithMany()
                .HasForeignKey(e => e.FinanceCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Project)
                .WithMany()
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ZeroAssistantSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SearchProvider).HasDefaultValue(ZeroSearchProvider.SearXNG);
            entity.Property(e => e.WhisperUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.PiperUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.PiperEndpoint).IsRequired().HasMaxLength(120);
            entity.Property(e => e.PiperVoice).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SearXngBaseUrl).IsRequired().HasMaxLength(500);
        });

        modelBuilder.Entity<ZeroMemoryItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Text).IsRequired().HasMaxLength(1000);
            entity.HasIndex(e => new { e.UserId, e.SortOrder });
        });

        modelBuilder.Entity<ZeroConversationMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.ConversationId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(40);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(8000);
            entity.HasIndex(e => new { e.UserId, e.ConversationId, e.Id });
        });

        modelBuilder.Entity<OperationalDatabaseSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Host).IsRequired().HasMaxLength(200);
            entity.Property(e => e.DatabaseName).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(120);
            entity.Property(e => e.PasswordEncrypted).HasMaxLength(4000);
            entity.Property(e => e.SslMode).IsRequired().HasMaxLength(40);
        });

        modelBuilder.Entity<MarketDataProviderSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ApiKeyEncrypted).HasMaxLength(4000);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.LastTestResult).HasMaxLength(500);
            entity.HasIndex(e => e.ProviderName).IsUnique();
        });

        modelBuilder.Entity<TradingHolding>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Market).IsRequired().HasMaxLength(40);
            entity.Property(e => e.CompanyName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.HasIndex(e => new { e.UserId, e.Symbol, e.Market }).IsUnique();
        });

        modelBuilder.Entity<TradingWatchlistItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Market).IsRequired().HasMaxLength(40);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.HasIndex(e => new { e.UserId, e.Symbol, e.Market }).IsUnique();
        });

        modelBuilder.Entity<TradingJournalEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Reason).HasMaxLength(2000);
            entity.Property(e => e.Emotion).HasMaxLength(200);
            entity.Property(e => e.LessonLearned).HasMaxLength(2000);
            entity.HasIndex(e => new { e.UserId, e.Symbol, e.CreatedAt });
        });

        modelBuilder.Entity<TradingAdvisorSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.ProviderLabel).IsRequired().HasMaxLength(120);
            entity.Property(e => e.ModelId).HasMaxLength(200);
            entity.Property(e => e.RiskLevel).IsRequired().HasMaxLength(80);
            entity.Property(e => e.ConcentrationRisk).IsRequired().HasMaxLength(80);
            entity.Property(e => e.PossibleNextAction).IsRequired().HasMaxLength(80);
            entity.Property(e => e.PortfolioSummary).IsRequired().HasMaxLength(4000);
            entity.Property(e => e.Reasoning).IsRequired().HasMaxLength(4000);
            entity.Property(e => e.Disclaimer).IsRequired().HasMaxLength(500);
            entity.Property(e => e.RawJson).IsRequired().HasMaxLength(8000);
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
        });

        modelBuilder.Entity<TradingPriceSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Market).IsRequired().HasMaxLength(40);
            entity.Property(e => e.Currency).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(120);
            entity.HasIndex(e => new { e.UserId, e.Symbol, e.Market, e.CapturedAt });
        });
    }
}
