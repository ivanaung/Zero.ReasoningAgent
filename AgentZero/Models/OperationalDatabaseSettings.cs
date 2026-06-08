using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class OperationalDatabaseSettings
{
    public int Id { get; set; } = 1;

    public bool IsEnabled { get; set; }

    [MaxLength(200)]
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5432;

    [MaxLength(120)]
    public string DatabaseName { get; set; } = "progress_operational";

    [MaxLength(120)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? PasswordEncrypted { get; set; }

    [MaxLength(40)]
    public string SslMode { get; set; } = "Prefer";

    public bool TrustServerCertificate { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
