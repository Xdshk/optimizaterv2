using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Logging;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace GTA5Optimizer.UI.Services;

/// <summary>
/// Сервис системного трея
/// </summary>
public class TrayService : IDisposable
{
    private readonly ILoggerService _loggerService;
    private readonly IPerformanceMonitor _monitor;
    private NotifyIcon? _notifyIcon;
    private bool _disposed;

    public event Action? ShowRequested;
    public event Action? OptimizeRequested;
    public event Action? ExitRequested;

    public TrayService(ILoggerService loggerService, IPerformanceMonitor monitor)
    {
        _loggerService = loggerService;
        _monitor = monitor;
    }

    public void Initialize()
    {
        try
        {
            var icon = System.Drawing.SystemIcons.Application;

            _notifyIcon = new NotifyIcon
            {
                Icon = icon,
                Visible = true,
                Text = "GTA5 Optimizer"
            };

            var contextMenu = new ContextMenuStrip();

            var showItem = new ToolStripMenuItem("Открыть", null, (_, _) => ShowRequested?.Invoke());
            showItem.Font = new System.Drawing.Font(showItem.Font, System.Drawing.FontStyle.Bold);
            contextMenu.Items.Add(showItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var optimizeItem = new ToolStripMenuItem("⚡ Оптимизировать", null, (_, _) => OptimizeRequested?.Invoke());
            contextMenu.Items.Add(optimizeItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Выход", null, (_, _) => ExitRequested?.Invoke());
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke();

            _ = _loggerService.LogAsync(new GTA5Optimizer.Models.Logging.LogEntry
            {
                Level = GTA5Optimizer.Models.Logging.LogLevel.Information,
                Category = "SYSTEM",
                Message = "Трей инициализирован"
            });
        }
        catch (Exception ex)
        {
            _ = _loggerService.LogAsync(new GTA5Optimizer.Models.Logging.LogEntry
            {
                Level = GTA5Optimizer.Models.Logging.LogLevel.Warning,
                Category = "SYSTEM",
                Message = "Не удалось инициализировать трей",
                Details = ex.Message
            });
        }
    }

    public void UpdateTooltip(string text)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
        }
    }

    public void ShowNotification(string title, string message)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _notifyIcon?.Dispose();
            _disposed = true;
        }
    }
}
