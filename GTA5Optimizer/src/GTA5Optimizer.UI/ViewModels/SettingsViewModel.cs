using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Settings;
using GTA5LogLevel = GTA5Optimizer.Models.Logging.LogLevel;
using LogEntry = GTA5Optimizer.Models.Logging.LogEntry;
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
                    SelectedProfileName = settings.ActiveProfile switch
                    {
                        OptimizationProfile.Everyday => "Everyday Mode",
                        OptimizationProfile.RPMode => "RP Mode",
                        OptimizationProfile.MassiveOnline => "Massive Online Mode",
                        OptimizationProfile.MaximumFPS => "Maximum FPS Mode",
                        _ => "RP Mode"
                    };
                    AutoOptimizationInterval = settings.AutoOptimizationIntervalSeconds;
                    EnableAutoOptimization = settings.EnableAutoOptimization;
                }
            }
        }
        catch (Exception ex)
        {
            await _loggerService.LogAsync(new LogEntry
            {
                Level = GTA5LogLevel.Warning,
                Category = "SETTINGS",
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
                ActiveProfile = profile,
                AutoOptimizationIntervalSeconds = AutoOptimizationInterval,
                EnableAutoOptimization = EnableAutoOptimization
            };

            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsPath, json);

            await _loggerService.LogAsync(new LogEntry
            {
                Level = GTA5LogLevel.Information,
                Category = "SETTINGS",
                Message = "Настройки сохранены"
            });
        }
        catch (Exception ex)
        {
            await _loggerService.LogAsync(new LogEntry
            {
                Level = GTA5LogLevel.Error,
                Category = "SETTINGS",
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
