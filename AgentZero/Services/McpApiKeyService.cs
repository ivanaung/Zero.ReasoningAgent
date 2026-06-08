using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public interface IMcpApiKeyService
{
    Task<McpAccessSettingsViewModel> GetSettingsViewModelAsync(string? newlyGeneratedApiKey = null, CancellationToken cancellationToken = default);
    Task<string> GenerateForCurrentUserAsync(string name, CancellationToken cancellationToken = default);
    Task RevokeForCurrentUserAsync(int keyId, CancellationToken cancellationToken = default);
    Task<AppUser?> ValidateAsync(string providedKey, CancellationToken cancellationToken = default);
}

public class McpApiKeyService(
    AppDbContext context,
    ICurrentUserService currentUserService) : IMcpApiKeyService
{
    private const string ApiKeyPrefix = "mcp_";

    public async Task<McpAccessSettingsViewModel> GetSettingsViewModelAsync(string? newlyGeneratedApiKey = null, CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId;
        var keys = await context.McpApiKeys
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return new McpAccessSettingsViewModel
        {
            NewlyGeneratedApiKey = newlyGeneratedApiKey,
            Keys = keys.Select(item => new McpApiKeyListItemViewModel
            {
                Id = item.Id,
                Name = item.Name,
                KeyPrefix = item.KeyPrefix,
                IsActive = item.IsActive,
                CreatedAtUtc = item.CreatedAtUtc,
                LastUsedAtUtc = item.LastUsedAtUtc
            }).ToList()
        };
    }

    public async Task<string> GenerateForCurrentUserAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!currentUserService.IsAuthenticated)
        {
            throw new InvalidOperationException("A signed-in user is required to generate an MCP API key.");
        }

        var cleanName = string.IsNullOrWhiteSpace(name) ? "External Agent" : name.Trim();
        var randomBytes = RandomNumberGenerator.GetBytes(24);
        var secret = Convert.ToHexString(randomBytes).ToLowerInvariant();
        var keyPrefix = secret[..8];
        var fullKey = $"{ApiKeyPrefix}{keyPrefix}_{secret}";

        context.McpApiKeys.Add(new McpApiKey
        {
            UserId = currentUserService.UserId,
            Name = cleanName,
            KeyPrefix = keyPrefix,
            KeyHash = ComputeHash(fullKey),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
        return fullKey;
    }

    public async Task RevokeForCurrentUserAsync(int keyId, CancellationToken cancellationToken = default)
    {
        var key = await context.McpApiKeys.FirstOrDefaultAsync(item => item.Id == keyId && item.UserId == currentUserService.UserId, cancellationToken);
        if (key == null)
        {
            return;
        }

        key.IsActive = false;
        key.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AppUser?> ValidateAsync(string providedKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providedKey) || !providedKey.StartsWith(ApiKeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var separatorIndex = providedKey.IndexOf('_', ApiKeyPrefix.Length);
        if (separatorIndex < 0 || separatorIndex <= ApiKeyPrefix.Length)
        {
            return null;
        }

        var prefix = providedKey.Substring(ApiKeyPrefix.Length, separatorIndex - ApiKeyPrefix.Length);
        var key = await context.McpApiKeys
            .Where(item => item.KeyPrefix == prefix && item.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
        if (key == null)
        {
            return null;
        }

        var computedHash = ComputeHash(providedKey);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(computedHash), Encoding.UTF8.GetBytes(key.KeyHash)))
        {
            return null;
        }

        var user = await context.Users.FirstOrDefaultAsync(item => item.Id == key.UserId && item.IsActive, cancellationToken);
        if (user == null)
        {
            return null;
        }

        key.LastUsedAtUtc = DateTime.UtcNow;
        key.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return user;
    }

    private static string ComputeHash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
