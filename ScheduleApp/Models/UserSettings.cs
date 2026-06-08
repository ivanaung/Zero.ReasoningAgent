using System.ComponentModel.DataAnnotations;

namespace ScheduleApp.Models;

public class UserSettings
{
    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$", ErrorMessage = "Invalid time format (HH:mm)")]
    public string DayStartTime { get; set; } = "08:00";

    [RegularExpression(@"^([01]\d|2[0-3]):([0-5]\d)$", ErrorMessage = "Invalid time format (HH:mm)")]
    public string DayEndTime { get; set; } = "20:00";

    public string Theme { get; set; } = "Light";

    public bool ShowFullDayInCalendar { get; set; } = false;

    public string? Clock1TimeZone { get; set; }
    public string? Clock2TimeZone { get; set; }
    public string? Clock3TimeZone { get; set; }
    public string? Clock4TimeZone { get; set; }
}
