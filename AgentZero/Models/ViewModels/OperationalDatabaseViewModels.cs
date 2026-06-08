using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models.ViewModels;

public class OperationalDatabaseInputViewModel
{
    public bool IsEnabled { get; set; }

    [Required]
    [MaxLength(200)]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 5432;

    [Required]
    [MaxLength(120)]
    [Display(Name = "Database")]
    public string DatabaseName { get; set; } = "progress_operational";

    [Required]
    [MaxLength(120)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Password { get; set; }

    public bool ClearPassword { get; set; }

    public bool HasPassword { get; set; }

    [Required]
    [MaxLength(40)]
    public string SslMode { get; set; } = "Prefer";

    public bool TrustServerCertificate { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}

public class OperationalDatabaseTestResultViewModel
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? Database { get; set; }

    public string? User { get; set; }

    public string? ServerVersion { get; set; }

    public DateTime TestedAtUtc { get; set; } = DateTime.UtcNow;
}

public class OperationalDatabasePageViewModel
{
    public OperationalDatabaseInputViewModel Settings { get; set; } = new();

    public OperationalDatabaseTestResultViewModel? TestResult { get; set; }

    public bool Saved { get; set; }

    public IReadOnlyList<string> SslModes { get; init; } =
    [
        "Disable",
        "Allow",
        "Prefer",
        "Require",
        "VerifyCA",
        "VerifyFull"
    ];
}
