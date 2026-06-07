using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Logging;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace GTA5Optimizer.UI.ViewModels;

/// <summary>
/// ViewModel для вкладки логов
/// </summary>
public partial class LogsViewModel : ObservableObject
{
    private readonly ILoggerService _loggerService;
    private readonly DispatcherTimer _refreshTimer;

    [ObservableProperty]
    private ObservableCollection<LogEntry> _logs = new();

    [ObservableProperty]
    private LogEntry? _selectedLog;

    [ObservableProperty]
    private bool _autoRefresh = true;

    public LogsViewModel(ILoggerService loggerService)
    {
        _loggerService = loggerService;
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += async (_, _) => await RefreshLogsAsync();
        _refreshTimer.Start();

        _ = RefreshLogsAsync();
    }

    [RelayCommand]
    private async Task RefreshLogs()
    {
        try
        {
            var logs = await _loggerService.GetRecentLogsAsync(200);
            Logs = new ObservableCollection<LogEntry>(logs);
        }
        catch
        {
            // Тихо игнорируем
        }
    }

    [RelayCommand]
    private async Task ClearLogs()
    {
        try
        {
            await _loggerService.ClearLogsAsync();
            Logs.Clear();
        }
        catch
        {
            // Тихо игнорируем
        }
    }

    public void Dispose()
    {
        _refreshTimer?.Stop();
    }
}
