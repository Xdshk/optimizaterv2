using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Logging;
using GTA5Optimizer.Models.Settings;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace GTA5Optimizer.UI.ViewModels;

/// <summary>
/// ViewModel для вкладки настроек
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ILoggerService _loggerService;
    private readonly string _settingsPath;

    [ObservableProperty]
    private string _selectedProfileName = "RP Mode";

    [ObservableProperty]
    private int _autoOptimizationInterval = 30;

    [ObservableProperty]
    private bool _enableAutoOptimization = true;

    [ObservableProperty]
    private bool _enableMemoryCleanup = true;

    [ObservableProperty]
    private bool _closeBrowsers = true;

    [ObservableProperty]
    private bool _closeDiscordOverlay = true;

    [ObservableProperty]
    private double _memoryCleanupThreshold = 80.0;

    [ObservableProperty]
    private ObservableCollection<string> _availableProfiles = new()
    {
        "Everyday Mode",
        "RP Mode",
        "Massive Online Mode",
        "Maximum FPS Mode"
    };

    public SettingsViewModel(ILoggerService loggerService)
    {
        _loggerService = loggerService;
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GTA5Optimizer", "settings.json");

        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    SelectedProfileName = settings.DefaultProfile.ToString();
                    AutoOptimizationInterval = settings.AutoOptimizationIntervalSeconds;
                    EnableAutoOptimization = settings.EnableAutoOptimization;
                    EnableMemoryCleanup = settings.EnableMemoryCleanup;
                    CloseBrowsers = settings.CloseBrowsers;
                    CloseDiscordOverlay = settings.CloseDiscordOverlay;
                    MemoryCleanupThreshold = settings.MemoryCleanupThresholdPercent;
                }
            }
        }
        catch (Exception ex)
        {
            await _loggerService.LogAsync(new LogEntry
            {
                Level = LogLevel.Warning,
                Category = LogCategories.UI,
                Message = "Не удалось загрузить настройки, используются значения по умолчанию",
                Details = ex.Message
            });
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        try
        {
            var profile = SelectedProfileName switch
            {
                "Everyday Mode" => OptimizationProfile.Everyday,
                "RP Mode" => OptimizationProfile.RPMode,
                "Massive Online Mode" => OptimizationProfile.MassiveOnline,
                "Maximum FPS Mode" => OptimizationProfile.MaximumFPS,
                _ => OptimizationProfile.RPMode
            };

            var settings = new AppSettings
            {
                DefaultProfile = profile,
                AutoOptimizationIntervalSeconds = AutoOptimizationInterval,
                EnableAutoOptimization = EnableAutoOptimization,
                EnableMemoryCleanup = EnableMemoryCleanup,
                CloseBrowsers = CloseBrowsers,
                CloseDiscordOverlay = CloseDiscordOverlay,
                MemoryCleanupThresholdPercent = MemoryCleanupThreshold
            };

            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsPath, json);

            await _loggerService.LogAsync(new LogEntry
            {
                Level = LogLevel.Information,
                Category = "SETTINGS",
                Message = "Настройки сохранены"
            });
        }
        catch (Exception ex)
        {
            await _loggerService.LogAsync(new LogEntry
            {
                Level = LogLevel.Error,
                Category = LogCategories.UI,
                Message = "Ошибка сохранения настроек",
                Details = ex.Message
            });
        }
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedProfileName = "RP Mode";
        AutoOptimizationInterval = 30;
        EnableAutoOptimization = true;
        EnableMemoryCleanup = true;
        CloseBrowsers = true;
        CloseDiscordOverlay = true;
        MemoryCleanupThreshold = 80.0;
    }
}
