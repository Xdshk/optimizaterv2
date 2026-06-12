using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Optimization;
using GTA5Optimizer.UI.Services;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using GTA5LogLevel = GTA5Optimizer.Models.Logging.LogLevel;
using LogEntry = GTA5Optimizer.Models.Logging.LogEntry;
using LogCategories = GTA5Optimizer.Models.Logging.LogCategories;
using System.Windows;

namespace GTA5Optimizer.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerService _loggerService;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly IGameDetector _gameDetector;
    private readonly TrayService _trayService;
    private readonly OverlayService _overlayService;
    private readonly DispatcherTimer _metricsTimer;
    private readonly DispatcherTimer _gameStatusTimer;
    private readonly DispatcherTimer _trayUpdateTimer;

    [ObservableProperty] private bool _isOptimizing;
    [ObservableProperty] private OptimizationProfile _selectedProfile = OptimizationProfile.RPMode;
    [ObservableProperty] private ObservableCollection<ProfileConfig> _profiles = new();
    [ObservableProperty] private ProfileConfig? _selectedProfileConfig;
    [ObservableProperty] private bool _isGameRunning;
    [ObservableProperty] private string _gamePath = string.Empty;
    [ObservableProperty] private string _gameStatusText = "GTA V: не запущена";
    [ObservableProperty] private double _currentFPS;
    [ObservableProperty] private double _cpuUsage;
    [ObservableProperty] private double _gpuUsage;
    [ObservableProperty] private double _ramUsage;
    [ObservableProperty] private string _statusMessage = "Готов к оптимизации";
    [ObservableProperty] private ObservableCollection<OptimizationResult> _optimizationResults = new();
    private bool _overlayEnabled;
    private bool _autoStartEnabled;
    [ObservableProperty] private string _version = "v1.0.0";

    public MonitorViewModel Monitor { get; }
    public LogsViewModel Logs { get; }
    public SettingsViewModel Settings { get; }

    public bool OverlayEnabled
    {
        get => _overlayEnabled;
        set
        {
            if (SetProperty(ref _overlayEnabled, value))
            {
                _overlayService.IsVisible = value;
                _ = _loggerService.LogAsync(new LogEntry
                {
                    Level = GTA5LogLevel.Information,
                    Category = "UI",
                    Message = value ? "Оверлей включён" : "Оверлей выключен"
                });
            }
        }
    }

    public bool AutoStartEnabled
    {
        get => _autoStartEnabled;
        set
        {
            if (SetProperty(ref _autoStartEnabled, value))
            {
                if (value) AutoStartService.Enable();
                else AutoStartService.Disable();
            }
        }
    }

    public MainWindowViewModel(
        IServiceProvider serviceProvider,
        ILoggerService loggerService,
        IPerformanceMonitor performanceMonitor,
        IGameDetector gameDetector,
        TrayService trayService,
        OverlayService overlayService,
        MonitorViewModel monitor,
        LogsViewModel logs,
        SettingsViewModel settings)
    {
        _serviceProvider = serviceProvider;
        _loggerService = loggerService;
        _performanceMonitor = performanceMonitor;
        _gameDetector = gameDetector;
        _trayService = trayService;
        _overlayService = overlayService;
        Monitor = monitor;
        Logs = logs;
        Settings = settings;

        _autoStartEnabled = AutoStartService.IsEnabled;

        _performanceMonitor.StartMonitoring();
        _performanceMonitor.OnMetricsUpdated += OnMetricsUpdated;

        _metricsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _metricsTimer.Tick += async (_, _) => await RefreshMetricsAsync();
        _metricsTimer.Start();

        _gameStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _gameStatusTimer.Tick += async (_, _) => await CheckGameStatusAsync();
        _gameStatusTimer.Start();

        _trayUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _trayUpdateTimer.Tick += (_, _) => UpdateTrayTooltip();
        _trayUpdateTimer.Start();

        _trayService.OptimizeRequested += async () => await OptimizeAsync();

        _ = LoadProfilesAsync();
        _ = CheckGameStatusAsync();
    }

    private void OnMetricsUpdated(GTA5Optimizer.Models.Monitoring.PerformanceMetrics metrics)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            CurrentFPS = metrics.CurrentFPS;
            CpuUsage = metrics.CPUUsage;
            GpuUsage = metrics.GPUUsage;
            RamUsage = metrics.RAMUsagePercent;
        });
    }

    private void UpdateTrayTooltip()
    {
        _trayService.UpdateTooltip($"GTA5 Optimizer | FPS: {CurrentFPS:F0} | CPU: {CpuUsage:F0}% | RAM: {RamUsage:F0}%");
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
        catch (Exception ex)
        {
            await _loggerService.LogAsync(new LogEntry
            {
                Level = GTA5LogLevel.Error,
                Category = LogCategories.UI,
                Message = "Ошибка загрузки профилей",
                Details = ex.Message
            });
        }
    }

    private async Task CheckGameStatusAsync()
    {
        try
        {
            var gameInfo = await _gameDetector.DetectGameAsync();
            IsGameRunning = gameInfo.IsRunning;
            GamePath = gameInfo.InstallPath;
            GameStatusText = gameInfo.IsRunning
                ? $"GTA V: запущена (PID: {gameInfo.ProcessId})"
                : "GTA V: не запущена";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check game status");
            GameStatusText = "GTA V: статус неизвестен";
        }
    }

    [RelayCommand]
    private async Task OptimizeAsync()
    {
        IsOptimizing = true;
        StatusMessage = "Выполняется оптимизация...";
        OptimizationResults.Clear();

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var optimizer = scope.ServiceProvider.GetRequiredService<ISystemOptimizer>();
            var profileManager = scope.ServiceProvider.GetRequiredService<IProfileManager>();

            var profile = SelectedProfileConfig?.Profile ?? SelectedProfile;
            await profileManager.ApplyProfileAsync(profile);
            var success = await optimizer.ApplyOptimizationsAsync(profile);

            StatusMessage = success ? "✅ Оптимизация завершена" : "⚠️ Завершена с ошибками";
            _trayService.ShowNotification("GTA5 Optimizer", StatusMessage);
            await Logs.RefreshLogsCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Ошибка: {ex.Message}";
            await _loggerService.LogAsync(new LogEntry
            {
                Level = GTA5LogLevel.Error,
                Category = LogCategories.Optimization,
                Message = "Ошибка оптимизации",
                Details = ex.Message,
                Exception = ex
            });
        }
        finally { IsOptimizing = false; }
    }

    [RelayCommand]
    private async Task RestoreDefaultsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var optimizer = scope.ServiceProvider.GetRequiredService<ISystemOptimizer>();
            var success = await optimizer.RestoreDefaultsAsync();
            StatusMessage = success ? "✅ Настройки восстановлены" : "⚠️ Ошибка восстановления";
            await Logs.RefreshLogsCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Ошибка: {ex.Message}";
            await _loggerService.LogAsync(new LogEntry
            {
                Level = GTA5LogLevel.Error,
                Category = LogCategories.Rollback,
                Message = "Ошибка восстановления настроек",
                Details = ex.Message,
                Exception = ex
            });
        }
    }

    [RelayCommand]
    private async Task RefreshMetricsAsync()
    {
        try
        {
            var metrics = await _performanceMonitor.GetCurrentMetricsAsync();
            CurrentFPS = metrics.CurrentFPS;
            CpuUsage = metrics.CPUUsage;
            GpuUsage = metrics.GPUUsage;
            RamUsage = metrics.RAMUsagePercent;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to refresh metrics");
        }
    }

    public void Dispose()
    {
        _metricsTimer.Stop();
        _gameStatusTimer.Stop();
        _trayUpdateTimer.Stop();
        _overlayService.Dispose();
    }
}
