using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Logging;
using GTA5Optimizer.Models.Optimization;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace GTA5Optimizer.UI.ViewModels;

/// <summary>
/// ViewModel главного окна
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerService _loggerService;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly IGameDetector _gameDetector;
    private readonly DispatcherTimer _metricsTimer;
    private readonly DispatcherTimer _gameStatusTimer;

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
    private string _gameStatusText = "GTA V: не запущена";

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

    [ObservableProperty]
    private ObservableCollection<OptimizationResult> _optimizationResults = new();

    // Sub-viewmodels
    public MonitorViewModel Monitor { get; }
    public LogsViewModel Logs { get; }
    public SettingsViewModel Settings { get; }

    public MainWindowViewModel(
        IServiceProvider serviceProvider,
        ILoggerService loggerService,
        IPerformanceMonitor performanceMonitor,
        IGameDetector gameDetector,
        MonitorViewModel monitor,
        LogsViewModel logs,
        SettingsViewModel settings)
    {
        _serviceProvider = serviceProvider;
        _loggerService = loggerService;
        _performanceMonitor = performanceMonitor;
        _gameDetector = gameDetector;
        Monitor = monitor;
        Logs = logs;
        Settings = settings;

        // Запускаем мониторинг производительности
        _performanceMonitor.StartMonitoring();
        _performanceMonitor.OnMetricsUpdated += OnMetricsUpdated;

        // Таймер обновления метрик в UI (каждые 2 сек)
        _metricsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _metricsTimer.Tick += async (_, _) => await RefreshMetricsAsync();
        _metricsTimer.Start();

        // Таймер проверки статуса игры (каждые 5 сек)
        _gameStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _gameStatusTimer.Tick += async (_, _) => await CheckGameStatusAsync();
        _gameStatusTimer.Start();

        _ = LoadProfilesAsync();
        _ = CheckGameStatusAsync();
    }

    private void OnMetricsUpdated(Models.Monitoring.PerformanceMetrics metrics)
    {
        // Обновляется из фонового потока монитора — используем Dispatcher
        App.Current?.Dispatcher.Invoke(() =>
        {
            CurrentFPS = metrics.CurrentFPS;
            CpuUsage = metrics.CPUUsage;
            GpuUsage = metrics.GPUUsage;
            RamUsage = metrics.RAMUsagePercent;
        });
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
                Level = LogLevel.Error,
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
        catch
        {
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

            StatusMessage = success ? "✅ Оптимизация завершена успешно" : "⚠️ Оптимизация завершена с ошибками";

            // Обновляем логи после оптимизации
            await Logs.RefreshLogsCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Ошибка: {ex.Message}";
            await _loggerService.LogAsync(new LogEntry
            {
                Level = LogLevel.Error,
                Category = LogCategories.Optimization,
                Message = "Ошибка оптимизации",
                Details = ex.Message,
                Exception = ex
            });
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
            var success = await optimizer.RestoreDefaultsAsync();
            StatusMessage = success ? "✅ Настройки восстановлены" : "⚠️ Ошибка восстановления";

            await Logs.RefreshLogsCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Ошибка восстановления: {ex.Message}";
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
        catch
        {
            // Тихо игнорируем ошибки обновления метрик
        }
    }
}
