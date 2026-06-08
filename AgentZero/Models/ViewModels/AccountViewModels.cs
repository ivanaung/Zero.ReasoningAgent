using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models.ViewModels;

public class LoginViewModel
{
    [Required]
    [MaxLength(120)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    public bool ShowGoogleLogin { get; set; }
}
