using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;

namespace ScheduleApp.Services;

public interface IZeroLegacyImportService
{
    Task ImportIfNeededAsync(CancellationToken cancellationToken = default);
}

public class ZeroLegacyImportService(
    AppDbContext context,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<ZeroLegacyImportService> logger) : IZeroLegacyImportService
{
    public async Task ImportIfNeededAsync(CancellationToken cancellationToken = default)
    {
        var settings = await context.ZeroAssistantSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings == null || settings.LegacyImportCompletedUtc.HasValue)
        {
            return;
        }

        var legacyPath = ResolveLegacyPath();
        if (string.IsNullOrWhiteSpace(legacyPath) || !File.Exists(legacyPath))
        {
            return;
        }

        var targetUser = await context.Users
            .OrderByDescending(user => user.Role == AppRoles.Admin)
            .ThenBy(user => user.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (targetUser == null)
        {
            return;
        }

        await using var connection = new SqliteConnection($"Data Source={legacyPath}");
        await connection.OpenAsync(cancellationToken);

        await ImportSettingsAsync(connection, settings, cancellationToken);
        await ImportMemoryAsync(connection, targetUser.Id, cancellationToken);
        await ImportConversationAsync(connection, targetUser.Id, cancellationToken);

        settings.LegacyImportCompletedUtc = DateTime.UtcNow;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Imported legacy ZeroAssistant data from {LegacyPath} into Progress schedule database.", legacyPath);
    }

    private string? ResolveLegacyPath()
    {
        var configured = configuration["ZeroAssistant:LegacyDatabasePath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(environment.ContentRootPath, configured);
        }

        const string knownLocalPath = @"D:\AH_PROJECTS\AssistantZero\ZeroAssistant\App_Data\zero.db";
        return File.Exists(knownLocalPath) ? knownLocalPath : null;
    }

    private static async Task ImportSettingsAsync(SqliteConnection connection, ZeroAssistantSettings settings, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT WhisperUrl, PiperUrl, PiperEndpoint, HistoryLimit, RequestTimeoutSeconds, BrowserSpeechRate, BrowserSpeechPitch
            FROM ZeroSettings
            WHERE Id = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return;
        }

        settings.WhisperUrl = reader.GetString(0);
        settings.PiperUrl = reader.GetString(1);
        settings.PiperEndpoint = reader.GetString(2);
        settings.HistoryLimit = reader.GetInt32(3);
        settings.RequestTimeoutSeconds = reader.GetInt32(4);
        settings.BrowserSpeechRate = reader.GetDouble(5);
        settings.BrowserSpeechPitch = reader.GetDouble(6);
    }

    private async Task ImportMemoryAsync(SqliteConnection connection, string userId, CancellationToken cancellationToken)
    {
        if (await context.ZeroMemoryItems.AnyAsync(item => item.UserId == userId, cancellationToken))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Text, SortOrder, CreatedUtc, UpdatedUtc FROM MemoryItems ORDER BY SortOrder ASC;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            context.ZeroMemoryItems.Add(new ZeroMemoryItem
            {
                UserId = userId,
                Text = reader.GetString(0),
                SortOrder = reader.GetInt32(1),
                CreatedUtc = ReadDate(reader, 2),
                UpdatedUtc = ReadDate(reader, 3)
            });
        }
    }

    private async Task ImportConversationAsync(SqliteConnection connection, string userId, CancellationToken cancellationToken)
    {
        if (await context.ZeroConversationMessages.AnyAsync(item => item.UserId == userId, cancellationToken))
        {
            return;
        }

        var conversationId = "legacy-zero";
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Role, Content, CreatedUtc FROM ConversationMessages ORDER BY Id ASC;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            context.ZeroConversationMessages.Add(new ZeroConversationMessage
            {
                UserId = userId,
                ConversationId = conversationId,
                Role = reader.GetString(0),
                Content = reader.GetString(1),
                CreatedUtc = ReadDate(reader, 2)
            });
        }
    }

    private static DateTime ReadDate(SqliteDataReader reader, int index)
    {
        return DateTime.TryParse(reader.GetString(index), out var parsed)
            ? parsed
            : DateTime.UtcNow;
    }
}
