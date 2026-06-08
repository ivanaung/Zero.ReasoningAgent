using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;
using ScheduleApp.Models;
using ScheduleApp.Models.ViewModels;

namespace ScheduleApp.Services;

public sealed record GoogleSignInResult(
    string UserId,
    string Email,
    string DisplayName,
    string? PictureUrl);

public interface IGoogleIntegrationService
{
    Task<GoogleIntegrationSettings> GetAsync(CancellationToken cancellationToken = default);
    Task<GoogleIntegrationSettingsInputViewModel> GetInputModelAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(GoogleIntegrationSettingsInputViewModel model, CancellationToken cancellationToken = default);
    Task<GoogleConnectionStatusViewModel> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<string?> BuildAuthorizationUrlAsync(string redirectUri, string? state = null, CancellationToken cancellationToken = default);
    Task<GoogleSignInResult> CompleteSignInAsync(string code, string redirectUri, string? preferredUserId = null, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoogleEmailMessageViewModel>> GetCachedInboxMessagesAsync(int maxResults = 10, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoogleEmailMessageViewModel>> GetInboxMessagesAsync(int maxResults = 10, string? query = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoogleCalendarEventViewModel>> GetUpcomingCalendarEventsAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, int maxResults = 10, CancellationToken cancellationToken = default);
}

public class GoogleIntegrationService(
    AppDbContext context,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider dataProtectionProvider,
    ICurrentUserService currentUserService,
    IUserAccountService userAccountService) : IGoogleIntegrationService
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string AuthorizeEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("ScheduleApp.GoogleIntegration.Secrets");

    public async Task<GoogleIntegrationSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await context.GoogleIntegrationSettings.FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
        {
            entity = new GoogleIntegrationSettings();
            context.GoogleIntegrationSettings.Add(entity);
            await context.SaveChangesAsync(cancellationToken);
        }

        entity.ClientSecret = Decrypt(entity.ClientSecretEncrypted);
        return entity;
    }

    public async Task<GoogleIntegrationSettingsInputViewModel> GetInputModelAsync(CancellationToken cancellationToken = default)
    {
        var entity = await GetAsync(cancellationToken);
        var account = await GetCurrentUserGoogleAccountAsync(cancellationToken);

        return new GoogleIntegrationSettingsInputViewModel
        {
            IsEnabled = entity.IsEnabled,
            ClientId = entity.ClientId,
            PublicBaseUrl = entity.PublicBaseUrl,
            ClientSecret = string.IsNullOrWhiteSpace(entity.ClientSecret) ? null : "configured",
            ScopeSet = entity.ScopeSet,
            EnableEmailIntegration = entity.EnableEmailIntegration,
            EnableCalendarIntegration = entity.EnableCalendarIntegration,
            EnableAiEmailTools = entity.EnableAiEmailTools,
            EnableAiCalendarTools = entity.EnableAiCalendarTools,
            InboxCacheLimit = entity.InboxCacheLimit <= 0 ? 100 : entity.InboxCacheLimit,
            ConnectedEmail = account?.Email,
            IsConnected = account != null,
            ConnectedAtUtc = account?.LinkedAtUtc,
            LastSyncUtc = entity.LastSyncUtc
        };
    }

    public async Task SaveAsync(GoogleIntegrationSettingsInputViewModel model, CancellationToken cancellationToken = default)
    {
        var entity = await context.GoogleIntegrationSettings.FirstOrDefaultAsync(cancellationToken) ?? new GoogleIntegrationSettings();
        if (entity.Id == 0)
        {
            entity.Id = 1;
            context.GoogleIntegrationSettings.Add(entity);
        }

        entity.IsEnabled = model.IsEnabled;
        entity.ClientId = model.ClientId?.Trim();
        entity.PublicBaseUrl = NormalizePublicBaseUrl(model.PublicBaseUrl);
        entity.ScopeSet = string.IsNullOrWhiteSpace(model.ScopeSet)
            ? "openid email profile https://www.googleapis.com/auth/gmail.readonly https://www.googleapis.com/auth/calendar.readonly"
            : model.ScopeSet.Trim();
        entity.EnableEmailIntegration = model.EnableEmailIntegration;
        entity.EnableCalendarIntegration = model.EnableCalendarIntegration;
        entity.EnableAiEmailTools = model.EnableAiEmailTools;
        entity.EnableAiCalendarTools = model.EnableAiCalendarTools;
        entity.InboxCacheLimit = Math.Clamp(model.InboxCacheLimit, 10, 500);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        if (model.ClearClientSecret)
        {
            entity.ClientSecretEncrypted = null;
        }
        else if (!string.IsNullOrWhiteSpace(model.ClientSecret) && !string.Equals(model.ClientSecret, "configured", StringComparison.Ordinal))
        {
            entity.ClientSecretEncrypted = _protector.Protect(model.ClientSecret.Trim());
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<GoogleConnectionStatusViewModel> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var entity = await GetAsync(cancellationToken);
        var account = await GetCurrentUserGoogleAccountAsync(cancellationToken);
        var isConfigured = entity.IsEnabled
            && !string.IsNullOrWhiteSpace(entity.ClientId)
            && !string.IsNullOrWhiteSpace(entity.ClientSecret);
        var isConnected = account != null;

        return new GoogleConnectionStatusViewModel
        {
            IsEnabled = entity.IsEnabled,
            IsConfigured = isConfigured,
            HasPublicBaseUrl = !string.IsNullOrWhiteSpace(entity.PublicBaseUrl),
            IsConnected = isConnected,
            EmailEnabled = entity.EnableEmailIntegration,
            CalendarEnabled = entity.EnableCalendarIntegration,
            ConnectedEmail = account?.Email,
            ConnectedAtUtc = account?.LinkedAtUtc,
            LastSyncUtc = entity.LastSyncUtc,
            InboxCacheLimit = entity.InboxCacheLimit <= 0 ? 100 : entity.InboxCacheLimit,
            StatusMessage = !entity.IsEnabled
                ? "Google integration is disabled."
                : !isConfigured
                    ? "Add a Google OAuth client id and client secret to enable Google sign-in."
                    : !currentUserService.IsAuthenticated
                        ? "Google sign-in is configured. Sign in with Google to link your account."
                        : !isConnected
                            ? "Your account is signed in but not linked for Gmail/Calendar access yet."
                            : $"Connected to {account!.Email}."
        };
    }

    public async Task<string?> BuildAuthorizationUrlAsync(string redirectUri, string? state = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetAsync(cancellationToken);
        if (!entity.IsEnabled || string.IsNullOrWhiteSpace(entity.ClientId) || string.IsNullOrWhiteSpace(entity.ClientSecret))
        {
            return null;
        }

        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = entity.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = entity.ScopeSet,
            ["access_type"] = "offline",
            ["include_granted_scopes"] = "true",
            ["prompt"] = "consent",
            ["state"] = state
        };

        return QueryHelpers.AddQueryString(AuthorizeEndpoint, parameters!);
    }

    public async Task<GoogleSignInResult> CompleteSignInAsync(string code, string redirectUri, string? preferredUserId = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetAsync(cancellationToken);
        if (!entity.IsEnabled || string.IsNullOrWhiteSpace(entity.ClientId) || string.IsNullOrWhiteSpace(entity.ClientSecret))
        {
            throw new InvalidOperationException("Google integration is not configured.");
        }

        var client = httpClientFactory.CreateClient();
        using var tokenResponse = await client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = entity.ClientId,
            ["client_secret"] = entity.ClientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        }), cancellationToken);

        tokenResponse.EnsureSuccessStatusCode();

        using var tokenDocument = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
        var accessToken = tokenDocument.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Google did not return an access token.");
        var refreshToken = tokenDocument.RootElement.TryGetProperty("refresh_token", out var refreshNode)
            ? refreshNode.GetString()
            : null;
        var expiresIn = tokenDocument.RootElement.TryGetProperty("expires_in", out var expiresNode)
            ? expiresNode.GetInt32()
            : 3600;
        var scopeSet = tokenDocument.RootElement.TryGetProperty("scope", out var scopeNode)
            ? scopeNode.GetString() ?? entity.ScopeSet
            : entity.ScopeSet;

        var profile = await FetchGoogleProfileAsync(accessToken, cancellationToken);
        var user = await userAccountService.FindOrProvisionGoogleUserAsync(profile.UserId, profile.Email, profile.DisplayName, preferredUserId, cancellationToken);
        var account = await context.UserGoogleAccounts.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        if (account == null)
        {
            account = new UserGoogleAccount
            {
                UserId = user.Id,
                LinkedAtUtc = DateTime.UtcNow
            };
            context.UserGoogleAccounts.Add(account);
        }

        account.GoogleSubjectId = profile.UserId;
        account.Email = profile.Email;
        account.DisplayName = profile.DisplayName;
        account.PictureUrl = profile.PictureUrl;
        account.AccessTokenEncrypted = _protector.Protect(accessToken);
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            account.RefreshTokenEncrypted = _protector.Protect(refreshToken);
        }
        account.AccessTokenExpiresUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60));
        account.ScopeSet = scopeSet;
        account.UpdatedAtUtc = DateTime.UtcNow;

        entity.LastSyncUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return profile with { UserId = user.Id };
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUserService.IsAuthenticated)
        {
            return;
        }

        var account = await context.UserGoogleAccounts.FirstOrDefaultAsync(item => item.UserId == currentUserService.UserId, cancellationToken);
        if (account == null)
        {
            return;
        }

        context.UserGoogleAccounts.Remove(account);

        var entity = await GetAsync(cancellationToken);
        entity.LastSyncUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GoogleEmailMessageViewModel>> GetCachedInboxMessagesAsync(int maxResults = 10, CancellationToken cancellationToken = default)
    {
        if (!currentUserService.IsAuthenticated)
        {
            return [];
        }

        var settings = await GetAsync(cancellationToken);
        var take = maxResults <= 0
            ? Math.Clamp(settings.InboxCacheLimit <= 0 ? 100 : settings.InboxCacheLimit, 10, 500)
            : Math.Clamp(maxResults, 1, 500);

        var cachedItems = await context.CachedEmailMessages
            .Where(item => item.UserId == currentUserService.UserId)
            .Select(item => new GoogleEmailMessageViewModel
            {
                Id = item.MessageId,
                Subject = item.Subject ?? string.Empty,
                From = item.From ?? string.Empty,
                ReceivedAt = item.ReceivedAt,
                Snippet = item.Snippet ?? string.Empty,
                WebUrl = item.WebUrl ?? string.Empty,
                HtmlBody = item.HtmlBody ?? string.Empty,
                TextBody = item.TextBody ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        return cachedItems
            .OrderByDescending(item => item.ReceivedAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(item => item.Id, StringComparer.Ordinal)
            .Take(take)
            .ToList();
    }

    public async Task<IReadOnlyList<GoogleEmailMessageViewModel>> GetInboxMessagesAsync(int maxResults = 10, string? query = null, CancellationToken cancellationToken = default)
    {
        var settings = await GetAsync(cancellationToken);
        if (!settings.IsEnabled || !settings.EnableEmailIntegration)
        {
            return [];
        }

        var account = await GetCurrentUserGoogleAccountAsync(cancellationToken);
        var token = await GetValidAccessTokenAsync(account, settings, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return [];
        }

        var items = await FetchInboxMessagesFromGoogleAsync(token, Math.Clamp(maxResults, 1, 500), query, cancellationToken);

        if (string.IsNullOrWhiteSpace(query) && currentUserService.IsAuthenticated)
        {
            await SaveInboxCacheAsync(currentUserService.UserId, items, settings, cancellationToken);
        }

        settings.LastSyncUtc = DateTime.UtcNow;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return items;
    }

    public async Task<IReadOnlyList<GoogleCalendarEventViewModel>> GetUpcomingCalendarEventsAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        var settings = await GetAsync(cancellationToken);
        if (!settings.IsEnabled || !settings.EnableCalendarIntegration)
        {
            return [];
        }

        var account = await GetCurrentUserGoogleAccountAsync(cancellationToken);
        var token = await GetValidAccessTokenAsync(account, settings, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return [];
        }

        var start = from ?? DateTimeOffset.UtcNow;
        var end = to ?? start.AddDays(7);

        var url = QueryHelpers.AddQueryString(
            "https://www.googleapis.com/calendar/v3/calendars/primary/events",
            new Dictionary<string, string?>
            {
                ["singleEvents"] = "true",
                ["orderBy"] = "startTime",
                ["timeMin"] = start.UtcDateTime.ToString("o"),
                ["timeMax"] = end.UtcDateTime.ToString("o"),
                ["maxResults"] = Math.Max(1, Math.Min(maxResults, 20)).ToString(CultureInfo.InvariantCulture)
            });

        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var items = new List<GoogleCalendarEventViewModel>();
        if (document.RootElement.TryGetProperty("items", out var itemsNode) && itemsNode.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemsNode.EnumerateArray())
            {
                items.Add(new GoogleCalendarEventViewModel
                {
                    Id = item.TryGetProperty("id", out var idNode) ? idNode.GetString() ?? string.Empty : string.Empty,
                    Title = item.TryGetProperty("summary", out var summaryNode) ? summaryNode.GetString() ?? "(untitled event)" : "(untitled event)",
                    Organizer = item.TryGetProperty("organizer", out var organizerNode) && organizerNode.TryGetProperty("email", out var emailNode) ? emailNode.GetString() : null,
                    Start = ReadCalendarDate(item, "start"),
                    End = ReadCalendarDate(item, "end"),
                    HtmlLink = item.TryGetProperty("htmlLink", out var linkNode) ? linkNode.GetString() : null
                });
            }
        }

        settings.LastSyncUtc = DateTime.UtcNow;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return items;
    }

    private async Task<UserGoogleAccount?> GetCurrentUserGoogleAccountAsync(CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated)
        {
            return null;
        }

        return await context.UserGoogleAccounts.FirstOrDefaultAsync(item => item.UserId == currentUserService.UserId, cancellationToken);
    }

    private async Task<string?> GetValidAccessTokenAsync(UserGoogleAccount? account, GoogleIntegrationSettings settings, CancellationToken cancellationToken)
    {
        if (account == null)
        {
            return null;
        }

        var currentAccessToken = Decrypt(account.AccessTokenEncrypted);
        if (!string.IsNullOrWhiteSpace(currentAccessToken) && account.AccessTokenExpiresUtc.HasValue && account.AccessTokenExpiresUtc > DateTime.UtcNow.AddMinutes(1))
        {
            return currentAccessToken;
        }

        var refreshToken = Decrypt(account.RefreshTokenEncrypted);
        if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            return null;
        }

        var client = httpClientFactory.CreateClient();
        using var response = await client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = settings.ClientId,
            ["client_secret"] = settings.ClientSecret!,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        }), cancellationToken);

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var accessToken = document.RootElement.GetProperty("access_token").GetString();
        var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expiresNode)
            ? expiresNode.GetInt32()
            : 3600;

        account.AccessTokenEncrypted = string.IsNullOrWhiteSpace(accessToken) ? null : _protector.Protect(accessToken);
        account.AccessTokenExpiresUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 60));
        account.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return accessToken;
    }

    private async Task<List<GoogleEmailMessageViewModel>> FetchInboxMessagesFromGoogleAsync(string token, int maxResults, string? query, CancellationToken cancellationToken)
    {
        var listUrl = QueryHelpers.AddQueryString(
            "https://gmail.googleapis.com/gmail/v1/users/me/messages",
            new Dictionary<string, string?>
            {
                ["maxResults"] = Math.Max(1, Math.Min(maxResults, 500)).ToString(CultureInfo.InvariantCulture),
                ["q"] = string.IsNullOrWhiteSpace(query) ? "in:inbox" : $"in:inbox {query.Trim()}"
            });

        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var listResponse = await client.GetAsync(listUrl, cancellationToken);
        listResponse.EnsureSuccessStatusCode();

        using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(cancellationToken));
        var messageIds = listDocument.RootElement.TryGetProperty("messages", out var messagesNode) && messagesNode.ValueKind == JsonValueKind.Array
            ? messagesNode.EnumerateArray().Select(item => item.GetProperty("id").GetString()).Where(id => !string.IsNullOrWhiteSpace(id)).Take(maxResults).ToList()
            : [];

        var items = new List<GoogleEmailMessageViewModel>();
        foreach (var messageId in messageIds)
        {
            using var detailResponse = await client.GetAsync(
                $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{messageId}?format=full",
                cancellationToken);
            detailResponse.EnsureSuccessStatusCode();

            using var detailDocument = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync(cancellationToken));
            var payload = detailDocument.RootElement;
            var payloadNode = payload.GetProperty("payload");
            var headers = payloadNode.GetProperty("headers").EnumerateArray().ToList();
            var bodies = ExtractMessageBodies(payloadNode);
            items.Add(new GoogleEmailMessageViewModel
            {
                Id = messageId!,
                Subject = GetHeader(headers, "Subject") ?? "(no subject)",
                From = GetHeader(headers, "From") ?? "Unknown",
                ReceivedAt = TryParseDate(GetHeader(headers, "Date")),
                Snippet = payload.TryGetProperty("snippet", out var snippetNode) ? snippetNode.GetString() ?? string.Empty : string.Empty,
                WebUrl = $"https://mail.google.com/mail/u/0/#inbox/{messageId}",
                HtmlBody = bodies.HtmlBody,
                TextBody = bodies.TextBody
            });
        }

        return items;
    }

    private async Task SaveInboxCacheAsync(
        string userId,
        IReadOnlyList<GoogleEmailMessageViewModel> messages,
        GoogleIntegrationSettings settings,
        CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return;
        }

        var cacheLimit = Math.Clamp(settings.InboxCacheLimit <= 0 ? 100 : settings.InboxCacheLimit, 10, 500);
        var messageIds = messages.Select(item => item.Id).Distinct(StringComparer.Ordinal).ToList();
        var existingItems = await context.CachedEmailMessages
            .Where(item => item.UserId == userId && messageIds.Contains(item.MessageId))
            .ToListAsync(cancellationToken);
        var existing = existingItems.ToDictionary(item => item.MessageId, StringComparer.Ordinal);

        foreach (var message in messages)
        {
            if (!existing.TryGetValue(message.Id, out var cached))
            {
                cached = new CachedEmailMessage
                {
                    UserId = userId,
                    MessageId = message.Id
                };
                context.CachedEmailMessages.Add(cached);
            }

            cached.Subject = TrimValue(message.Subject, 500);
            cached.From = TrimValue(message.From, 500);
            cached.ReceivedAt = message.ReceivedAt;
            cached.Snippet = TrimValue(message.Snippet, 4000);
            cached.WebUrl = TrimValue(message.WebUrl, 500);
            cached.HtmlBody = TrimValue(message.HtmlBody, 200000);
            cached.TextBody = TrimValue(message.TextBody, 100000);
            cached.SyncedAtUtc = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);

        var staleEntries = (await context.CachedEmailMessages
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.ReceivedAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(item => item.Id)
            .Skip(cacheLimit)
            .ToList();

        if (staleEntries.Count > 0)
        {
            context.CachedEmailMessages.RemoveRange(staleEntries);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<GoogleSignInResult> FetchGoogleProfileAsync(string accessToken, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        var subject = document.RootElement.TryGetProperty("id", out var idNode) ? idNode.GetString() : null;
        var email = document.RootElement.TryGetProperty("email", out var emailNode) ? emailNode.GetString() : null;
        var name = document.RootElement.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;
        var picture = document.RootElement.TryGetProperty("picture", out var pictureNode) ? pictureNode.GetString() : null;

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Google did not return a valid user profile.");
        }

        return new GoogleSignInResult(subject, email, name ?? email, picture);
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

    private static string? NormalizePublicBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().TrimEnd('/');
    }

    private static string? GetHeader(IEnumerable<JsonElement> headers, string name)
    {
        foreach (var header in headers)
        {
            var headerName = header.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;
            if (string.Equals(headerName, name, StringComparison.OrdinalIgnoreCase))
            {
                return header.TryGetProperty("value", out var valueNode) ? valueNode.GetString() : null;
            }
        }

        return null;
    }

    private static DateTimeOffset? TryParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
            ? parsed
            : null;
    }

    private static string TrimValue(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static (string HtmlBody, string TextBody) ExtractMessageBodies(JsonElement payload)
    {
        var htmlBodies = new List<string>();
        var textBodies = new List<string>();
        TraverseMessageParts(payload, htmlBodies, textBodies);

        return (
            HtmlBody: string.Join(Environment.NewLine, htmlBodies.Where(item => !string.IsNullOrWhiteSpace(item))),
            TextBody: string.Join(Environment.NewLine + Environment.NewLine, textBodies.Where(item => !string.IsNullOrWhiteSpace(item)))
        );
    }

    private static void TraverseMessageParts(JsonElement part, ICollection<string> htmlBodies, ICollection<string> textBodies)
    {
        if (part.TryGetProperty("mimeType", out var mimeTypeNode))
        {
            var mimeType = mimeTypeNode.GetString();
            var bodyData = TryDecodeBody(part);
            if (!string.IsNullOrWhiteSpace(bodyData))
            {
                if (string.Equals(mimeType, "text/html", StringComparison.OrdinalIgnoreCase))
                {
                    htmlBodies.Add(bodyData);
                }
                else if (string.Equals(mimeType, "text/plain", StringComparison.OrdinalIgnoreCase))
                {
                    textBodies.Add(bodyData);
                }
            }
        }

        if (part.TryGetProperty("parts", out var partsNode) && partsNode.ValueKind == JsonValueKind.Array)
        {
            foreach (var childPart in partsNode.EnumerateArray())
            {
                TraverseMessageParts(childPart, htmlBodies, textBodies);
            }
        }
    }

    private static string? TryDecodeBody(JsonElement part)
    {
        if (!part.TryGetProperty("body", out var bodyNode)
            || !bodyNode.TryGetProperty("data", out var dataNode))
        {
            return null;
        }

        var encoded = dataNode.GetString();
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        try
        {
            var normalized = encoded.Replace('-', '+').Replace('_', '/');
            var padding = normalized.Length % 4;
            if (padding > 0)
            {
                normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');
            }

            var bytes = Convert.FromBase64String(normalized);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static DateTimeOffset? ReadCalendarDate(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var node))
        {
            return null;
        }

        if (node.TryGetProperty("dateTime", out var dateTimeNode))
        {
            return TryParseDate(dateTimeNode.GetString());
        }

        if (node.TryGetProperty("date", out var dateNode) && DateTimeOffset.TryParse(dateNode.GetString(), out var allDay))
        {
            return allDay;
        }

        return null;
    }
}
