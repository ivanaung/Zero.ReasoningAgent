using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using ScheduleApp.Data;

namespace ScheduleApp.Services;

public interface IGoogleOAuthRedirectUriBuilder
{
    Task<string> BuildCallbackUriAsync(HttpRequest request, CancellationToken cancellationToken = default);
}

public class GoogleOAuthRedirectUriBuilder(
    AppDbContext context,
    IConfiguration configuration) : IGoogleOAuthRedirectUriBuilder
{
    private const string CallbackPath = "/account/google-callback";

    public async Task<string> BuildCallbackUriAsync(HttpRequest request, CancellationToken cancellationToken = default)
    {
        var settingsPublicBaseUrl = await context.GoogleIntegrationSettings
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => item.PublicBaseUrl)
            .FirstOrDefaultAsync(cancellationToken);
        var publicBaseUrl = string.IsNullOrWhiteSpace(settingsPublicBaseUrl)
            ? configuration["GoogleOAuth:PublicBaseUrl"]
            : settingsPublicBaseUrl;

        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return Combine(publicBaseUrl, CallbackPath);
        }

        return UriHelper.BuildAbsolute(
            request.Scheme,
            request.Host,
            request.PathBase,
            CallbackPath);
    }

    private static string Combine(string baseUrl, string path)
    {
        var normalizedBaseUrl = baseUrl.Trim().TrimEnd('/');
        var normalizedPath = path.StartsWith('/') ? path : $"/{path}";
        return $"{normalizedBaseUrl}{normalizedPath}";
    }
}
