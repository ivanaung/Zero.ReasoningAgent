using ScheduleApp.Models;

namespace ScheduleApp.Services;

public record WorkingCalendarRules(
    string TimeZoneId,
    TimeSpan Start,
    TimeSpan End,
    IReadOnlyCollection<DayOfWeek> WorkingDays);

public interface IWorkingCalendarService
{
    Task<WorkingCalendarRules> GetRulesAsync(CancellationToken cancellationToken = default);

    Task<DateTimeOffset> CalculateNextWorkingSlotAsync(
        DateTimeOffset reference,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    Task<(DateTimeOffset Start, DateTimeOffset End)> ScheduleAsync(
        DateTimeOffset? preferredStart,
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}

public class WorkingCalendarService(IAiSettingsService aiSettingsService) : IWorkingCalendarService
{
    public async Task<WorkingCalendarRules> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await aiSettingsService.GetAsync(cancellationToken);
        var workingDays = ParseWorkingDays(settings.WorkingDays);

        return new WorkingCalendarRules(
            settings.DefaultTimeZone,
            TimeSpan.Parse(settings.WorkingHoursStart),
            TimeSpan.Parse(settings.WorkingHoursEnd),
            workingDays);
    }

    public async Task<DateTimeOffset> CalculateNextWorkingSlotAsync(
        DateTimeOffset reference,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var rules = await GetRulesAsync(cancellationToken);
        var timeZone = SafeGetTimeZone(rules.TimeZoneId);
        var local = TimeZoneInfo.ConvertTime(reference, timeZone);
        var candidate = new DateTimeOffset(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0, local.Offset);

        while (true)
        {
            if (!rules.WorkingDays.Contains(candidate.DayOfWeek))
            {
                candidate = NextDay(candidate, rules.Start);
                continue;
            }

            var localTime = candidate.TimeOfDay;
            if (localTime < rules.Start)
            {
                candidate = new DateTimeOffset(candidate.Year, candidate.Month, candidate.Day, rules.Start.Hours, rules.Start.Minutes, 0, candidate.Offset);
            }
            else if (localTime >= rules.End || candidate.Add(duration).TimeOfDay > rules.End)
            {
                candidate = NextDay(candidate, rules.Start);
                continue;
            }

            return candidate.ToUniversalTime();
        }
    }

    public async Task<(DateTimeOffset Start, DateTimeOffset End)> ScheduleAsync(
        DateTimeOffset? preferredStart,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var start = await CalculateNextWorkingSlotAsync(preferredStart ?? DateTimeOffset.UtcNow, duration, cancellationToken);
        return (start, start.Add(duration));
    }

    private static IReadOnlyCollection<DayOfWeek> ParseWorkingDays(string raw)
    {
        var map = new Dictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mon"] = DayOfWeek.Monday,
            ["Tue"] = DayOfWeek.Tuesday,
            ["Wed"] = DayOfWeek.Wednesday,
            ["Thu"] = DayOfWeek.Thursday,
            ["Fri"] = DayOfWeek.Friday,
            ["Sat"] = DayOfWeek.Saturday,
            ["Sun"] = DayOfWeek.Sunday
        };

        var days = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(token => map.TryGetValue(token, out var day) ? day : (DayOfWeek?)null)
            .Where(day => day.HasValue)
            .Select(day => day!.Value)
            .Distinct()
            .ToArray();

        return days.Length > 0
            ? days
            : [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
    }

    private static DateTimeOffset NextDay(DateTimeOffset value, TimeSpan start)
    {
        var next = value.Date.AddDays(1);
        return new DateTimeOffset(next.Year, next.Month, next.Day, start.Hours, start.Minutes, 0, value.Offset);
    }

    private static TimeZoneInfo SafeGetTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }
}
