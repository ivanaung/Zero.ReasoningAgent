using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;

namespace ScheduleApp.Services;

public interface IDatabaseSchemaUpdater
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
}

public class DatabaseSchemaUpdater(AppDbContext context) : IDatabaseSchemaUpdater
{
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);
        await CreateTableIfMissingAsync("AiSettings", """
            CREATE TABLE AiSettings (
                Id INTEGER NOT NULL CONSTRAINT PK_AiSettings PRIMARY KEY,
                IsEnabled INTEGER NOT NULL,
                ProviderType INTEGER NOT NULL,
                ModelId TEXT NOT NULL,
                EndpointUrl TEXT NOT NULL,
                ApiKeyEncrypted TEXT NULL,
                Temperature REAL NOT NULL,
                MaxTokens INTEGER NOT NULL,
                SystemPrompt TEXT NOT NULL,
                EnableToolCalling INTEGER NOT NULL,
                EnableStreaming INTEGER NOT NULL,
                EnableProgressMonitoring INTEGER NOT NULL,
                DefaultTimeZone TEXT NOT NULL,
                WorkingHoursStart TEXT NOT NULL,
                WorkingHoursEnd TEXT NOT NULL,
                WorkingDays TEXT NOT NULL,
                AutoCreateLowRiskTasks INTEGER NOT NULL,
                RequireApprovalForScheduleChange INTEGER NOT NULL,
                RequireApprovalForTaskDeletion INTEGER NOT NULL,
                EnableProactiveAssist INTEGER NOT NULL DEFAULT 0,
                EnableNextHourRecommendations INTEGER NOT NULL DEFAULT 1,
                EnableTomorrowRecommendations INTEGER NOT NULL DEFAULT 1,
                EnablePreEventReminders INTEGER NOT NULL DEFAULT 1,
                PreEventReminderMinutes INTEGER NOT NULL DEFAULT 15,
                MorningDigestTime TEXT NOT NULL DEFAULT '08:30',
                AfternoonDigestTime TEXT NOT NULL DEFAULT '16:30',
                MaxRecommendationsPerNotification INTEGER NOT NULL DEFAULT 3,
                QuietHoursStart TEXT NOT NULL DEFAULT '22:00',
                QuietHoursEnd TEXT NOT NULL DEFAULT '06:00',
                SendInAppNotifications INTEGER NOT NULL DEFAULT 1,
                SendPushNotifications INTEGER NOT NULL DEFAULT 0,
                SendEmailNotifications INTEGER NOT NULL DEFAULT 0,
                EnableAiEnrichmentForNotifications INTEGER NOT NULL DEFAULT 1,
                NotificationLookaheadHours INTEGER NOT NULL DEFAULT 24,
                DigestLookaheadDays INTEGER NOT NULL DEFAULT 1,
                RecomputeOnTaskUpdate INTEGER NOT NULL DEFAULT 1,
                RecomputeOnAssignmentChange INTEGER NOT NULL DEFAULT 1,
                RecomputeOnDependencyChange INTEGER NOT NULL DEFAULT 1,
                RequireUserOptInForProactiveAssist INTEGER NOT NULL DEFAULT 1,
                DefaultUserTimeZone TEXT NOT NULL DEFAULT 'Pacific/Auckland',
                UpdatedAtUtc TEXT NOT NULL
            );
            """, cancellationToken);

        await CreateTableIfMissingAsync("AiActionAudits", """
            CREATE TABLE AiActionAudits (
                Id INTEGER NOT NULL CONSTRAINT PK_AiActionAudits PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                UserDisplayName TEXT NOT NULL,
                ActionType TEXT NOT NULL,
                Provider TEXT NULL,
                ModelId TEXT NULL,
                Summary TEXT NOT NULL,
                RequestPreview TEXT NULL,
                ResponsePreview TEXT NULL,
                Outcome TEXT NOT NULL,
                OccurredAtUtc TEXT NOT NULL
            );
            """, cancellationToken);

        await CreateTableIfMissingAsync("GoogleIntegrationSettings", """
            CREATE TABLE GoogleIntegrationSettings (
                Id INTEGER NOT NULL CONSTRAINT PK_GoogleIntegrationSettings PRIMARY KEY,
                IsEnabled INTEGER NOT NULL,
                ClientId TEXT NULL,
                PublicBaseUrl TEXT NULL,
                ClientSecretEncrypted TEXT NULL,
                RefreshTokenEncrypted TEXT NULL,
                AccessTokenEncrypted TEXT NULL,
                ConnectedEmail TEXT NULL,
                ScopeSet TEXT NOT NULL DEFAULT 'openid email profile https://www.googleapis.com/auth/gmail.readonly https://www.googleapis.com/auth/calendar.readonly',
                EnableEmailIntegration INTEGER NOT NULL DEFAULT 1,
                EnableCalendarIntegration INTEGER NOT NULL DEFAULT 1,
                EnableAiEmailTools INTEGER NOT NULL DEFAULT 1,
                EnableAiCalendarTools INTEGER NOT NULL DEFAULT 1,
                InboxCacheLimit INTEGER NOT NULL DEFAULT 100,
                AccessTokenExpiresUtc TEXT NULL,
                ConnectedAtUtc TEXT NULL,
                LastSyncUtc TEXT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            """, cancellationToken);

        await CreateTableIfMissingAsync("Users", """
            CREATE TABLE Users (
                Id TEXT NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
                Username TEXT NOT NULL,
                Email TEXT NULL,
                DisplayName TEXT NOT NULL,
                PasswordHash TEXT NULL,
                Role TEXT NOT NULL,
                AuthMode TEXT NOT NULL,
                IsActive INTEGER NOT NULL,
                MustChangePassword INTEGER NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IX_Users_Username ON Users (Username);
            CREATE UNIQUE INDEX IX_Users_Email ON Users (Email);
            """, cancellationToken);

        await CreateTableIfMissingAsync("UserGoogleAccounts", """
            CREATE TABLE UserGoogleAccounts (
                UserId TEXT NOT NULL CONSTRAINT PK_UserGoogleAccounts PRIMARY KEY,
                GoogleSubjectId TEXT NOT NULL,
                Email TEXT NOT NULL,
                DisplayName TEXT NULL,
                RefreshTokenEncrypted TEXT NULL,
                AccessTokenEncrypted TEXT NULL,
                PictureUrl TEXT NULL,
                ScopeSet TEXT NOT NULL,
                AccessTokenExpiresUtc TEXT NULL,
                LinkedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IX_UserGoogleAccounts_Email ON UserGoogleAccounts (Email);
            CREATE UNIQUE INDEX IX_UserGoogleAccounts_GoogleSubjectId ON UserGoogleAccounts (GoogleSubjectId);
            """, cancellationToken);

        await CreateTableIfMissingAsync("CachedEmailMessages", """
            CREATE TABLE CachedEmailMessages (
                Id INTEGER NOT NULL CONSTRAINT PK_CachedEmailMessages PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                MessageId TEXT NOT NULL,
                Subject TEXT NULL,
                Sender TEXT NULL,
                ReceivedAt TEXT NULL,
                Snippet TEXT NULL,
                WebUrl TEXT NULL,
                HtmlBody TEXT NULL,
                TextBody TEXT NULL,
                SyncedAtUtc TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IX_CachedEmailMessages_UserId_MessageId ON CachedEmailMessages (UserId, MessageId);
            CREATE INDEX IX_CachedEmailMessages_UserId_ReceivedAt ON CachedEmailMessages (UserId, ReceivedAt);
            """, cancellationToken);

        await CreateTableIfMissingAsync("McpApiKeys", """
            CREATE TABLE McpApiKeys (
                Id INTEGER NOT NULL CONSTRAINT PK_McpApiKeys PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                Name TEXT NOT NULL,
                KeyPrefix TEXT NOT NULL,
                KeyHash TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAtUtc TEXT NOT NULL,
                LastUsedAtUtc TEXT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IX_McpApiKeys_KeyPrefix ON McpApiKeys (KeyPrefix);
            CREATE INDEX IX_McpApiKeys_UserId_IsActive ON McpApiKeys (UserId, IsActive);
            """, cancellationToken);

        await CreateTableIfMissingAsync("TaskComments", """
            CREATE TABLE TaskComments (
                Id INTEGER NOT NULL CONSTRAINT PK_TaskComments PRIMARY KEY AUTOINCREMENT,
                TaskId INTEGER NOT NULL,
                AuthorId TEXT NOT NULL,
                AuthorName TEXT NOT NULL,
                Content TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                CONSTRAINT FK_TaskComments_Events_TaskId FOREIGN KEY (TaskId) REFERENCES Events (Id) ON DELETE CASCADE
            );
            """, cancellationToken);

        await CreateTableIfMissingAsync("ProjectComments", """
            CREATE TABLE ProjectComments (
                Id INTEGER NOT NULL CONSTRAINT PK_ProjectComments PRIMARY KEY AUTOINCREMENT,
                ProjectId INTEGER NOT NULL,
                AuthorId TEXT NOT NULL,
                AuthorName TEXT NOT NULL,
                Content TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL,
                CONSTRAINT FK_ProjectComments_Projects_ProjectId FOREIGN KEY (ProjectId) REFERENCES Projects (Id) ON DELETE CASCADE
            );
            """, cancellationToken);

        await CreateTableIfMissingAsync("NotificationScheduleEntries", """
            CREATE TABLE NotificationScheduleEntries (
                Id INTEGER NOT NULL CONSTRAINT PK_NotificationScheduleEntries PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                UserDisplayName TEXT NOT NULL,
                ProjectId INTEGER NULL,
                TaskId INTEGER NULL,
                NotificationType INTEGER NOT NULL,
                ScheduledForUtc TEXT NOT NULL,
                TimeZone TEXT NOT NULL,
                Status INTEGER NOT NULL,
                Priority INTEGER NOT NULL,
                ContextSnapshotJson TEXT NULL,
                LastComputedUtc TEXT NOT NULL,
                TriggerSource TEXT NOT NULL,
                DeduplicationKey TEXT NOT NULL,
                Message TEXT NOT NULL,
                ActionUrl TEXT NULL,
                RetryCount INTEGER NOT NULL,
                IsDismissed INTEGER NOT NULL,
                CreatedUtc TEXT NOT NULL,
                UpdatedUtc TEXT NOT NULL
            );
            CREATE INDEX IX_NotificationScheduleEntries_Status_ScheduledForUtc ON NotificationScheduleEntries (Status, ScheduledForUtc);
            CREATE INDEX IX_NotificationScheduleEntries_DeduplicationKey ON NotificationScheduleEntries (DeduplicationKey);
            """, cancellationToken);

        await CreateTableIfMissingAsync("UserProactivePreferences", """
            CREATE TABLE UserProactivePreferences (
                UserId TEXT NOT NULL CONSTRAINT PK_UserProactivePreferences PRIMARY KEY,
                UserDisplayName TEXT NOT NULL,
                IsOptedIn INTEGER NOT NULL,
                NextHourEnabled INTEGER NOT NULL,
                TomorrowDigestEnabled INTEGER NOT NULL,
                PreEventReminderEnabled INTEGER NOT NULL,
                ReminderMinutesBefore INTEGER NOT NULL,
                PreferredMorningDigestTime TEXT NOT NULL,
                PreferredAfternoonDigestTime TEXT NOT NULL,
                QuietHoursStart TEXT NOT NULL,
                QuietHoursEnd TEXT NOT NULL,
                PreferredChannels TEXT NOT NULL,
                TimeZone TEXT NOT NULL,
                UpdatedUtc TEXT NOT NULL
            );
            """, cancellationToken);

        await CreateTableIfMissingAsync("NotificationDeliveryLogs", """
            CREATE TABLE NotificationDeliveryLogs (
                Id INTEGER NOT NULL CONSTRAINT PK_NotificationDeliveryLogs PRIMARY KEY AUTOINCREMENT,
                NotificationScheduleEntryId INTEGER NOT NULL,
                UserId TEXT NOT NULL,
                Channel TEXT NOT NULL,
                SentUtc TEXT NOT NULL,
                Status TEXT NOT NULL,
                ProviderModel TEXT NULL,
                ErrorMessage TEXT NULL,
                CONSTRAINT FK_NotificationDeliveryLogs_NotificationScheduleEntries_NotificationScheduleEntryId FOREIGN KEY (NotificationScheduleEntryId) REFERENCES NotificationScheduleEntries (Id) ON DELETE CASCADE
            );
            CREATE INDEX IX_NotificationDeliveryLogs_Entry_Channel_Sent ON NotificationDeliveryLogs (NotificationScheduleEntryId, Channel, SentUtc);
            """, cancellationToken);

        await CreateTableIfMissingAsync("FinanceAccounts", """
            CREATE TABLE FinanceAccounts (
                Id INTEGER NOT NULL CONSTRAINT PK_FinanceAccounts PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                Name TEXT NOT NULL,
                Type INTEGER NOT NULL,
                Scope INTEGER NOT NULL,
                OpeningBalance TEXT NOT NULL,
                IsActive INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IX_FinanceAccounts_UserId_Name ON FinanceAccounts (UserId, Name);
            """, cancellationToken);

        await CreateTableIfMissingAsync("FinanceCategories", """
            CREATE TABLE FinanceCategories (
                Id INTEGER NOT NULL CONSTRAINT PK_FinanceCategories PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                Name TEXT NOT NULL,
                Type INTEGER NOT NULL,
                Scope INTEGER NOT NULL,
                IsDefault INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IX_FinanceCategories_UserId_Name_Type ON FinanceCategories (UserId, Name, Type);
            """, cancellationToken);

        await CreateTableIfMissingAsync("FinanceTransactions", """
            CREATE TABLE FinanceTransactions (
                Id INTEGER NOT NULL CONSTRAINT PK_FinanceTransactions PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                FinanceAccountId INTEGER NOT NULL,
                FinanceCategoryId INTEGER NOT NULL,
                ProjectId INTEGER NULL,
                Type INTEGER NOT NULL,
                Scope INTEGER NOT NULL,
                Amount TEXT NOT NULL,
                TransactionDate TEXT NOT NULL,
                Description TEXT NULL,
                PaymentMethod TEXT NULL,
                Notes TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IX_FinanceTransactions_UserId_TransactionDate ON FinanceTransactions (UserId, TransactionDate);
            """, cancellationToken);

        await CreateTableIfMissingAsync("FinanceBudgets", """
            CREATE TABLE FinanceBudgets (
                Id INTEGER NOT NULL CONSTRAINT PK_FinanceBudgets PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                FinanceCategoryId INTEGER NOT NULL,
                Scope INTEGER NOT NULL,
                Month INTEGER NOT NULL,
                Year INTEGER NOT NULL,
                BudgetAmount TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IX_FinanceBudgets_Unique ON FinanceBudgets (UserId, FinanceCategoryId, Scope, Month, Year);
            """, cancellationToken);

        await CreateTableIfMissingAsync("FinanceRecurringItems", """
            CREATE TABLE FinanceRecurringItems (
                Id INTEGER NOT NULL CONSTRAINT PK_FinanceRecurringItems PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                FinanceAccountId INTEGER NULL,
                FinanceCategoryId INTEGER NULL,
                ProjectId INTEGER NULL,
                Type INTEGER NOT NULL,
                Scope INTEGER NOT NULL,
                Name TEXT NOT NULL,
                Amount TEXT NOT NULL,
                Frequency INTEGER NOT NULL,
                NextDueDate TEXT NOT NULL,
                IsActive INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IX_FinanceRecurringItems_UserId_NextDueDate_IsActive ON FinanceRecurringItems (UserId, NextDueDate, IsActive);
            """, cancellationToken);

        await CreateTableIfMissingAsync("ZeroAssistantSettings", """
            CREATE TABLE ZeroAssistantSettings (
                Id INTEGER NOT NULL CONSTRAINT PK_ZeroAssistantSettings PRIMARY KEY,
                EnableVoice INTEGER NOT NULL DEFAULT 1,
                EnableLocalFileTools INTEGER NOT NULL DEFAULT 0,
                EnableVisionTools INTEGER NOT NULL DEFAULT 0,
                SearchProvider INTEGER NOT NULL DEFAULT 1,
                WhisperUrl TEXT NOT NULL DEFAULT 'http://localhost:10300',
                PiperUrl TEXT NOT NULL DEFAULT 'http://localhost:10200',
                PiperEndpoint TEXT NOT NULL DEFAULT '/',
                PiperVoice TEXT NOT NULL DEFAULT '',
                SearXngBaseUrl TEXT NOT NULL DEFAULT 'http://localhost:10100',
                HistoryLimit INTEGER NOT NULL DEFAULT 12,
                RequestTimeoutSeconds INTEGER NOT NULL DEFAULT 120,
                BrowserSpeechRate REAL NOT NULL DEFAULT 0.94,
                BrowserSpeechPitch REAL NOT NULL DEFAULT 0.98,
                MemoryLimit INTEGER NOT NULL DEFAULT 30,
                MaxUploadMegabytes INTEGER NOT NULL DEFAULT 25,
                UpdatedAtUtc TEXT NOT NULL,
                LegacyImportCompletedUtc TEXT NULL
            );
            """, cancellationToken);

        await CreateTableIfMissingAsync("ZeroMemoryItems", """
            CREATE TABLE ZeroMemoryItems (
                Id INTEGER NOT NULL CONSTRAINT PK_ZeroMemoryItems PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                Text TEXT NOT NULL,
                SortOrder INTEGER NOT NULL,
                CreatedUtc TEXT NOT NULL,
                UpdatedUtc TEXT NOT NULL
            );
            CREATE INDEX IX_ZeroMemoryItems_UserId_SortOrder ON ZeroMemoryItems (UserId, SortOrder);
            """, cancellationToken);

        await CreateTableIfMissingAsync("ZeroConversationMessages", """
            CREATE TABLE ZeroConversationMessages (
                Id INTEGER NOT NULL CONSTRAINT PK_ZeroConversationMessages PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                ConversationId TEXT NOT NULL,
                Role TEXT NOT NULL,
                Content TEXT NOT NULL,
                CreatedUtc TEXT NOT NULL
            );
            CREATE INDEX IX_ZeroConversationMessages_UserId_ConversationId_Id ON ZeroConversationMessages (UserId, ConversationId, Id);
            """, cancellationToken);

        await CreateTableIfMissingAsync("OperationalDatabaseSettings", """
            CREATE TABLE OperationalDatabaseSettings (
                Id INTEGER NOT NULL CONSTRAINT PK_OperationalDatabaseSettings PRIMARY KEY,
                IsEnabled INTEGER NOT NULL DEFAULT 0,
                Host TEXT NOT NULL DEFAULT 'localhost',
                Port INTEGER NOT NULL DEFAULT 5432,
                DatabaseName TEXT NOT NULL DEFAULT 'progress_operational',
                Username TEXT NOT NULL DEFAULT '',
                PasswordEncrypted TEXT NULL,
                SslMode TEXT NOT NULL DEFAULT 'Prefer',
                TrustServerCertificate INTEGER NOT NULL DEFAULT 0,
                UpdatedAtUtc TEXT NOT NULL
            );
            """, cancellationToken);

        await CreateTableIfMissingAsync("MarketDataProviderSettings", """
            CREATE TABLE MarketDataProviderSettings (
                Id INTEGER NOT NULL CONSTRAINT PK_MarketDataProviderSettings PRIMARY KEY AUTOINCREMENT,
                ProviderName INTEGER NOT NULL,
                IsEnabled INTEGER NOT NULL,
                IsPrimary INTEGER NOT NULL,
                ApiKeyEncrypted TEXT NULL,
                Notes TEXT NULL,
                LastTestResult TEXT NULL,
                LastTestDate TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IX_MarketDataProviderSettings_ProviderName ON MarketDataProviderSettings (ProviderName);
            """, cancellationToken);

        await CreateTableIfMissingAsync("TradingHoldings", """
            CREATE TABLE TradingHoldings (
                Id INTEGER NOT NULL CONSTRAINT PK_TradingHoldings PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                Symbol TEXT NOT NULL,
                Market TEXT NOT NULL,
                CompanyName TEXT NOT NULL,
                Quantity TEXT NOT NULL,
                AverageCost TEXT NOT NULL,
                Currency TEXT NOT NULL,
                CurrentPrice TEXT NOT NULL,
                Notes TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IX_TradingHoldings_UserId_Symbol_Market ON TradingHoldings (UserId, Symbol, Market);
            """, cancellationToken);

        await CreateTableIfMissingAsync("TradingWatchlistItems", """
            CREATE TABLE TradingWatchlistItems (
                Id INTEGER NOT NULL CONSTRAINT PK_TradingWatchlistItems PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                Symbol TEXT NOT NULL,
                Market TEXT NOT NULL,
                TargetBuyPrice TEXT NULL,
                TargetSellPrice TEXT NULL,
                AlertBelowPrice TEXT NULL,
                AlertAbovePrice TEXT NULL,
                Notes TEXT NULL,
                IsActive INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IX_TradingWatchlistItems_UserId_Symbol_Market ON TradingWatchlistItems (UserId, Symbol, Market);
            """, cancellationToken);

        await CreateTableIfMissingAsync("TradingJournalEntries", """
            CREATE TABLE TradingJournalEntries (
                Id INTEGER NOT NULL CONSTRAINT PK_TradingJournalEntries PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                Symbol TEXT NOT NULL,
                ActionType INTEGER NOT NULL,
                Price TEXT NULL,
                Quantity TEXT NULL,
                Reason TEXT NULL,
                Emotion TEXT NULL,
                LessonLearned TEXT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IX_TradingJournalEntries_UserId_Symbol_CreatedAt ON TradingJournalEntries (UserId, Symbol, CreatedAt);
            """, cancellationToken);

        await CreateTableIfMissingAsync("TradingAdvisorSnapshots", """
            CREATE TABLE TradingAdvisorSnapshots (
                Id INTEGER NOT NULL CONSTRAINT PK_TradingAdvisorSnapshots PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                ProviderLabel TEXT NOT NULL,
                ModelId TEXT NULL,
                RiskLevel TEXT NOT NULL,
                ConcentrationRisk TEXT NOT NULL,
                PossibleNextAction TEXT NOT NULL,
                Suggestion INTEGER NOT NULL,
                PortfolioSummary TEXT NOT NULL,
                Reasoning TEXT NOT NULL,
                Disclaimer TEXT NOT NULL,
                RawJson TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IX_TradingAdvisorSnapshots_UserId_CreatedAt ON TradingAdvisorSnapshots (UserId, CreatedAt);
            """, cancellationToken);

        await CreateTableIfMissingAsync("TradingPriceSnapshots", """
            CREATE TABLE TradingPriceSnapshots (
                Id INTEGER NOT NULL CONSTRAINT PK_TradingPriceSnapshots PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                Symbol TEXT NOT NULL,
                Market TEXT NOT NULL,
                Currency TEXT NOT NULL,
                Price TEXT NOT NULL,
                Source TEXT NOT NULL,
                IsLive INTEGER NOT NULL,
                CapturedAt TEXT NOT NULL
            );
            CREATE INDEX IX_TradingPriceSnapshots_UserId_Symbol_Market_CapturedAt ON TradingPriceSnapshots (UserId, Symbol, Market, CapturedAt);
            """, cancellationToken);

        await AddColumnIfMissingAsync("Events", "AssignedTo", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "EnableProactiveAssist", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "EnableNextHourRecommendations", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "EnableTomorrowRecommendations", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "EnablePreEventReminders", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "PreEventReminderMinutes", "INTEGER NOT NULL DEFAULT 15", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "MorningDigestTime", "TEXT NOT NULL DEFAULT '08:30'", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "AfternoonDigestTime", "TEXT NOT NULL DEFAULT '16:30'", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "MaxRecommendationsPerNotification", "INTEGER NOT NULL DEFAULT 3", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "QuietHoursStart", "TEXT NOT NULL DEFAULT '22:00'", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "QuietHoursEnd", "TEXT NOT NULL DEFAULT '06:00'", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "SendInAppNotifications", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "SendPushNotifications", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "SendEmailNotifications", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "EnableAiEnrichmentForNotifications", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "NotificationLookaheadHours", "INTEGER NOT NULL DEFAULT 24", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "DigestLookaheadDays", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "RecomputeOnTaskUpdate", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "RecomputeOnAssignmentChange", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "RecomputeOnDependencyChange", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "RequireUserOptInForProactiveAssist", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync("AiSettings", "DefaultUserTimeZone", "TEXT NOT NULL DEFAULT 'Pacific/Auckland'", cancellationToken);
        await AddColumnIfMissingAsync("GoogleIntegrationSettings", "EnableEmailIntegration", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync("GoogleIntegrationSettings", "EnableCalendarIntegration", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync("GoogleIntegrationSettings", "EnableAiEmailTools", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync("GoogleIntegrationSettings", "EnableAiCalendarTools", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync("GoogleIntegrationSettings", "InboxCacheLimit", "INTEGER NOT NULL DEFAULT 100", cancellationToken);
        await AddColumnIfMissingAsync("GoogleIntegrationSettings", "PublicBaseUrl", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync("CachedEmailMessages", "HtmlBody", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync("CachedEmailMessages", "TextBody", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync("ZeroAssistantSettings", "PiperVoice", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await AddColumnIfMissingAsync("ZeroAssistantSettings", "SearchProvider", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await AddColumnIfMissingAsync("ZeroAssistantSettings", "SearXngBaseUrl", "TEXT NOT NULL DEFAULT 'http://localhost:10100'", cancellationToken);
        await AddColumnIfMissingAsync("ZeroAssistantSettings", "LegacyImportCompletedUtc", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync("ProjectStages", "Description", "TEXT NULL", cancellationToken);
        await MigrateProjectStagesSchemaAsync(cancellationToken);

        if (!await context.AiSettings.AnyAsync(cancellationToken))
        {
            context.AiSettings.Add(new AiSettings());
        }

        if (!await context.GoogleIntegrationSettings.AnyAsync(cancellationToken))
        {
            context.GoogleIntegrationSettings.Add(new GoogleIntegrationSettings());
        }

        if (!await context.ZeroAssistantSettings.AnyAsync(cancellationToken))
        {
            context.ZeroAssistantSettings.Add(new ZeroAssistantSettings());
        }

        if (!await context.OperationalDatabaseSettings.AnyAsync(cancellationToken))
        {
            context.OperationalDatabaseSettings.Add(new OperationalDatabaseSettings());
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateTableIfMissingAsync(string tableName, string createSql, CancellationToken cancellationToken)
    {
        var exists = await TableExistsAsync(tableName, cancellationToken);
        if (!exists)
        {
            await context.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
        }
    }

    private async Task AddColumnIfMissingAsync(string tableName, string columnName, string columnDefinition, CancellationToken cancellationToken)
    {
        var exists = await ColumnExistsAsync(tableName, columnName, cancellationToken);
        if (!exists)
        {
            EnsureSafeIdentifier(tableName);
            EnsureSafeIdentifier(columnName);
#pragma warning disable EF1002
            await context.Database.ExecuteSqlRawAsync($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};", cancellationToken);
#pragma warning restore EF1002
        }
    }

    private async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken)
    {
        await using var connection = (SqliteConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        var result = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
        return result > 0;
    }

    private async Task<bool> ColumnExistsAsync(string tableName, string columnName, CancellationToken cancellationToken)
    {
        await using var connection = (SqliteConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task MigrateProjectStagesSchemaAsync(CancellationToken cancellationToken)
    {
        var hasProjectId = await ColumnExistsAsync("ProjectStages", "ProjectId", cancellationToken);
        if (!hasProjectId)
        {
            return;
        }

        // ProjectId exists. We need to recreate the table without ProjectId, SortOrder, Color
        // Since SQLite doesn't support DROP COLUMN well before v3.35, we do table rename.
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS ProjectStages_New (
                Id INTEGER NOT NULL CONSTRAINT PK_ProjectStages PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT NULL,
                CreatedAt TEXT NOT NULL
            );
            """, cancellationToken);

        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO ProjectStages_New (Id, Name, Description, CreatedAt)
            SELECT Id, Name, Description, CreatedAt FROM ProjectStages;
            """, cancellationToken);

        await context.Database.ExecuteSqlRawAsync("DROP TABLE ProjectStages;", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE ProjectStages_New RENAME TO ProjectStages;", cancellationToken);

        await context.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_ProjectStages_Name
            ON ProjectStages (Name);
            """, cancellationToken);
    }

    private static void EnsureSafeIdentifier(string identifier)
    {
        if (identifier.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
        {
            throw new InvalidOperationException($"Unsafe SQL identifier detected: {identifier}");
        }
    }
}
