using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Logging;

namespace GTA5Optimizer.UI.ViewModels;

/// <summary>
/// ViewModel для вкладки логов
/// </summary>
public partial class LogsViewModel : ObservableObject
{
    private readonly ILoggerService _loggerService;
    private readonly Timer _refreshTimer;

    [ObservableProperty]
    private ObservableCollection<LogEntry> _logs = new();

    [ObservableProperty]
    private LogEntry? _selectedLog;

    public LogsViewModel(ILoggerService loggerService)
    {
        _loggerService = loggerService;
        _refreshTimer = new Timer(async _ => await RefreshLogsAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    [RelayCommand]
    private async Task RefreshLogsAsync()
    {
        var logs = await _loggerService.GetRecentLogsAsync(100);
        Logs = new ObservableCollection<LogEntry>(logs);
    }

    [RelayCommand]
    private async Task ClearLogsAsync()
    {
        await _loggerService.ClearLogsAsync();
        Logs.Clear();
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }
}