using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class UserGoogleAccount
{
    [MaxLength(120)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string GoogleSubjectId { get; set; } = string.Empty;

    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    [MaxLength(4000)]
    public string? RefreshTokenEncrypted { get; set; }

    [MaxLength(4000)]
    public string? AccessTokenEncrypted { get; set; }

    [MaxLength(500)]
    public string? PictureUrl { get; set; }

    [MaxLength(500)]
    public string ScopeSet { get; set; } = string.Empty;

    public DateTime? AccessTokenExpiresUtc { get; set; }

    public DateTime LinkedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
