using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
}

public static class AppAuthModes
{
    public const string Local = "Local";
    public const string Google = "Google";
    public const string Both = "Both";
}

public class AppUser
{
    [MaxLength(120)]
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    [MaxLength(120)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(320)]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? PasswordHash { get; set; }

    [MaxLength(40)]
    public string Role { get; set; } = AppRoles.User;

    [MaxLength(40)]
    public string AuthMode { get; set; } = AppAuthModes.Local;

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
