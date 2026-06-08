using System.Security.Claims;

namespace ScheduleApp.Services;

public interface ICurrentUserService
{
    string UserId { get; }

    string DisplayName { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);
}

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User?.Identity?.Name
        ?? "anonymous";

    public string DisplayName => User?.Identity?.Name ?? "Anonymous";

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
