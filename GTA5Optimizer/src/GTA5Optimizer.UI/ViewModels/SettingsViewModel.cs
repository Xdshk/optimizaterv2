using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GTA5Optimizer.Models.Enums;

namespace GTA5Optimizer.UI.ViewModels;

/// <summary>
/// ViewModel для вкладки настроек
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private OptimizationProfile _selectedProfile = OptimizationProfile.RPMode;

    [ObservableProperty]
    private int _autoOptimizationInterval = 30;

    [ObservableProperty]
    private bool _enableMemoryCleanup = true;

    [ObservableProperty]
    private bool _closeBrowsers = true;

    [ObservableProperty]
    private bool _closeDiscordOverlay = true;

    [ObservableProperty]
    private double _memoryCleanupThreshold = 80.0;

    [RelayCommand]
    private void SaveSettings()
    {
        // Сохранение настроек в файл
    }

    [RelayCommand]
    private void ResetSettings()
    {
        SelectedProfile = OptimizationProfile.RPMode;
        AutoOptimizationInterval = 30;
        EnableMemoryCleanup = true;
        CloseBrowsers = true;
        CloseDiscordOverlay = true;
        MemoryCleanupThreshold = 80.0;
    }
}