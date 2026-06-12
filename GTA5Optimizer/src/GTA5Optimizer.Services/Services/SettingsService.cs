using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Settings;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GTA5Optimizer.Services.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly SystemInfoDetector _systemInfo;
    private readonly string _settingsPath;
    private AppSettings _settings = new();

    public SettingsService(
        ILogger<SettingsService> logger,
        SystemInfoDetector systemInfo)
    {
        _logger = logger;
        _systemInfo = systemInfo;
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GTA5Optimizer", "settings.json");

        // Fire-and-forget load — first call awaits, subsequent use cached
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadSettingsAsync();

        // Auto-detect hardware if not yet detected (empty string = first run)
        if (string.IsNullOrEmpty(_settings.HardwareProfile.CPUName))
        {
            _logger.LogInformation("First run — auto-detecting hardware...");
            var hw = _systemInfo.DetectHardwareProfile();

            // Try to detect game locations for drive info
            try
            {
                var gameDetectorType = typeof(GameDetector);
                // We resolve game detector from DI in App.xaml.cs instead
                // For now just set hardware
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not auto-detect game drive info during init");
            }

            _settings.HardwareProfile = hw;
            await SaveSettingsAsync(_settings);
            _logger.LogInformation("Hardware profile auto-detected and saved");
        }
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
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            await File.WriteAllTextAsync(_settingsPath, json);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
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
                var loaded = JsonConvert.DeserializeObject<AppSettings>(json);
                if (loaded != null)
                    _settings = loaded;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings, using defaults");
            _settings = new AppSettings();
        }
    }
}
