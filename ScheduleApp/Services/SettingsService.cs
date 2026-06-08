using System.Text.Json;
using ScheduleApp.Models;

namespace ScheduleApp.Services;

public interface ISettingsService
{
    Task<UserSettings> GetSettingsAsync();
    Task SaveSettingsAsync(UserSettings settings);
}

public class SettingsService(IWebHostEnvironment env) : ISettingsService
{
    private readonly string _settingsPath = Path.Combine(env.ContentRootPath, "settings.json");
    private static UserSettings? _cachedSettings;

    public async Task<UserSettings> GetSettingsAsync()
    {
        if (_cachedSettings != null) return _cachedSettings;

        if (!File.Exists(_settingsPath))
        {
            _cachedSettings = new UserSettings();
            await SaveSettingsAsync(_cachedSettings);
            return _cachedSettings;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_settingsPath);
            _cachedSettings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }
        catch
        {
            _cachedSettings = new UserSettings();
        }

        return _cachedSettings;
    }

    public async Task SaveSettingsAsync(UserSettings settings)
    {
        _cachedSettings = settings;
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsPath, json);
    }
}
