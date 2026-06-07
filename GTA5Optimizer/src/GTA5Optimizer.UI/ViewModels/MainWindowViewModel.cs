using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Optimization;
using System.Collections.ObjectModel;

namespace GTA5Optimizer.UI.ViewModels;

/// <summary>
/// ViewModel главного окна
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerService _loggerService;

    [ObservableProperty]
    private bool _isOptimizing;

    [ObservableProperty]
    private OptimizationProfile _selectedProfile = OptimizationProfile.RPMode;

    [ObservableProperty]
    private ObservableCollection<ProfileConfig> _profiles = new();

    [ObservableProperty]
    private ProfileConfig? _selectedProfileConfig;

    [ObservableProperty]
    private bool _isGameRunning;

    [ObservableProperty]
    private string _gamePath = string.Empty;

    [ObservableProperty]
    private double _currentFPS;

    [ObservableProperty]
    private double _cpuUsage;

    [ObservableProperty]
    private double _gpuUsage;

    [ObservableProperty]
    private double _ramUsage;

    [ObservableProperty]
    private string _statusMessage = "Готов к оптимизации";

    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _loggerService = serviceProvider.GetRequiredService<ILoggerService>();
        _ = LoadProfilesAsync();
    }

    private async Task LoadProfilesAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var profileManager = scope.ServiceProvider.GetRequiredService<IProfileManager>();
            var profiles = await profileManager.GetAvailableProfilesAsync();
            Profiles = new ObservableCollection<ProfileConfig>(profiles);
            SelectedProfileConfig = Profiles.FirstOrDefault();
        }
        catch { }
    }

    [RelayCommand]
    private async Task OptimizeAsync()
    {
        IsOptimizing = true;
        StatusMessage = "Выполняется оптимизация...";

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var optimizer = scope.ServiceProvider.GetRequiredService<ISystemOptimizer>();
            var profileManager = scope.ServiceProvider.GetRequiredService<IProfileManager>();

            var profile = SelectedProfileConfig?.Profile ?? SelectedProfile;
            await profileManager.ApplyProfileAsync(profile);
            var success = await optimizer.ApplyOptimizationsAsync(profile);

            StatusMessage = success ? "Оптимизация завершена успешно" : "Оптимизация завершена с ошибками";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsOptimizing = false;
        }
    }

    [RelayCommand]
    private async Task RestoreDefaultsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var optimizer = scope.ServiceProvider.GetRequiredService<ISystemOptimizer>();
            await optimizer.RestoreDefaultsAsync();
            StatusMessage = "Настройки восстановлены по умолчанию";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка восстановления: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshMetricsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var monitor = scope.ServiceProvider.GetRequiredService<IPerformanceMonitor>();
            var metrics = await monitor.GetCurrentMetricsAsync();

            CurrentFPS = metrics.CurrentFPS;
            CpuUsage = metrics.CPUUsage;
            GpuUsage = metrics.GPUUsage;
            RamUsage = metrics.RAMUsagePercent;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка мониторинга: {ex.Message}";
        }
    }
}
