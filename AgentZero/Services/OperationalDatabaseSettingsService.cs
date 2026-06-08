using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ScheduleApp.Data;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public interface IOperationalDatabaseSettingsService
{
    Task<OperationalDatabaseInputViewModel> GetInputAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(OperationalDatabaseInputViewModel model, CancellationToken cancellationToken = default);

    Task<OperationalDatabaseTestResultViewModel> TestAsync(OperationalDatabaseInputViewModel? model = null, CancellationToken cancellationToken = default);

    Task<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default);
}

public class OperationalDatabaseSettingsService(
    AppDbContext context,
    IDataProtectionProvider dataProtectionProvider,
    ILogger<OperationalDatabaseSettingsService> logger) : IOperationalDatabaseSettingsService
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("ScheduleApp.OperationalDatabase.Password");

    public async Task<OperationalDatabaseInputViewModel> GetInputAsync(CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateAsync(cancellationToken);
        var password = Decrypt(entity.PasswordEncrypted);

        return new OperationalDatabaseInputViewModel
        {
            IsEnabled = entity.IsEnabled,
            Host = entity.Host,
            Port = entity.Port,
            DatabaseName = entity.DatabaseName,
            Username = entity.Username,
            HasPassword = !string.IsNullOrWhiteSpace(password),
            SslMode = entity.SslMode,
            TrustServerCertificate = entity.TrustServerCertificate,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    public async Task SaveAsync(OperationalDatabaseInputViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateAsync(cancellationToken);

        entity.IsEnabled = model.IsEnabled;
        entity.Host = model.Host.Trim();
        entity.Port = model.Port;
        entity.DatabaseName = model.DatabaseName.Trim();
        entity.Username = model.Username.Trim();
        entity.SslMode = NormalizeSslMode(model.SslMode);
        entity.TrustServerCertificate = model.TrustServerCertificate;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        if (model.ClearPassword)
        {
            entity.PasswordEncrypted = null;
        }
        else if (!string.IsNullOrWhiteSpace(model.Password))
        {
            entity.PasswordEncrypted = _protector.Protect(model.Password);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<OperationalDatabaseTestResultViewModel> TestAsync(OperationalDatabaseInputViewModel? model = null, CancellationToken cancellationToken = default)
    {
        var connectionString = model == null
            ? await BuildSavedConnectionStringAsync(cancellationToken)
            : await BuildConnectionStringFromInputAsync(model, cancellationToken);

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "select current_database(), current_user, version();";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (await reader.ReadAsync(cancellationToken))
            {
                return new OperationalDatabaseTestResultViewModel
                {
                    Success = true,
                    Message = "PostgreSQL connection test succeeded.",
                    Database = reader.GetString(0),
                    User = reader.GetString(1),
                    ServerVersion = reader.GetString(2)
                };
            }

            return new OperationalDatabaseTestResultViewModel
            {
                Success = true,
                Message = "PostgreSQL connection opened successfully."
            };
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or InvalidOperationException or ArgumentException)
        {
            logger.LogWarning(ex, "PostgreSQL operational database connection test failed for host {Host}.", GetSafeHost(model));
            return new OperationalDatabaseTestResultViewModel
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateAsync(cancellationToken);
        if (!entity.IsEnabled)
        {
            return null;
        }

        return BuildConnectionString(
            entity.Host,
            entity.Port,
            entity.DatabaseName,
            entity.Username,
            Decrypt(entity.PasswordEncrypted),
            entity.SslMode,
            entity.TrustServerCertificate);
    }

    private async Task<OperationalDatabaseSettings> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var entity = await context.OperationalDatabaseSettings.FirstOrDefaultAsync(cancellationToken);
        if (entity != null)
        {
            return entity;
        }

        entity = new OperationalDatabaseSettings();
        context.OperationalDatabaseSettings.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task<string> BuildSavedConnectionStringAsync(CancellationToken cancellationToken)
    {
        var entity = await GetOrCreateAsync(cancellationToken);
        return BuildConnectionString(
            entity.Host,
            entity.Port,
            entity.DatabaseName,
            entity.Username,
            Decrypt(entity.PasswordEncrypted),
            entity.SslMode,
            entity.TrustServerCertificate);
    }

    private async Task<string> BuildConnectionStringFromInputAsync(OperationalDatabaseInputViewModel model, CancellationToken cancellationToken)
    {
        var existing = await context.OperationalDatabaseSettings.FirstOrDefaultAsync(cancellationToken);
        var password = model.ClearPassword
            ? null
            : !string.IsNullOrWhiteSpace(model.Password)
                ? model.Password
                : Decrypt(existing?.PasswordEncrypted);

        return BuildConnectionString(
            model.Host,
            model.Port,
            model.DatabaseName,
            model.Username,
            password,
            model.SslMode,
            model.TrustServerCertificate);
    }

    private static string BuildConnectionString(
        string host,
        int port,
        string databaseName,
        string username,
        string? password,
        string sslMode,
        bool trustServerCertificate)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host.Trim(),
            Port = port,
            Database = databaseName.Trim(),
            Username = username.Trim(),
            Password = password ?? string.Empty,
            SslMode = Enum.Parse<SslMode>(NormalizeSslMode(sslMode), ignoreCase: true),
            Timeout = 5,
            CommandTimeout = 10,
            Pooling = false
        };

        return builder.ConnectionString;
    }

    private static string NormalizeSslMode(string? sslMode)
    {
        var candidate = string.IsNullOrWhiteSpace(sslMode) ? "Prefer" : sslMode.Trim();
        return Enum.TryParse<SslMode>(candidate, ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : SslMode.Prefer.ToString();
    }

    private string? Decrypt(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(encrypted);
        }
        catch
        {
            return null;
        }
    }

    private static string GetSafeHost(OperationalDatabaseInputViewModel? model)
    {
        return string.IsNullOrWhiteSpace(model?.Host) ? "saved configuration" : model.Host;
    }
}
