using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Settings;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Сервис работы с настройками приложения
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsPath;
    private AppSettings _settings = new();

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GTA5Optimizer", "settings.json");

        _ = LoadSettingsAsync();
    }

    public async Task<AppSettings> GetSettingsAsync()
    {
        return await Task.FromResult(_settings);
    }

    public async Task<bool> SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            _settings = settings;
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            await File.WriteAllTextAsync(_settingsPath, json);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении настроек");
            return false;
        }
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке настроек");
            _settings = new AppSettings();
        }
    }
}