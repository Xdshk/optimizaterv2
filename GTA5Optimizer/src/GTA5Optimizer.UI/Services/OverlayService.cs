using GTA5Optimizer.Core.Interfaces;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace GTA5Optimizer.UI.Services;

/// <summary>
/// Сервис игрового оверлея (показывает FPS, CPU, GPU поверх игры)
/// </summary>
public class OverlayService : IDisposable
{
    private readonly IPerformanceMonitor _monitor;
    private Window? _overlayWindow;
    private TextBlock? _fpsText;
    private TextBlock? _cpuText;
    private TextBlock? _gpuText;
    private TextBlock? _ramText;
    private bool _isVisible;
    private bool _disposed;

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            if (value) Show();
            else Hide();
        }
    }

    public OverlayService(IPerformanceMonitor monitor)
    {
        _monitor = monitor;
        // Subscribe to metrics updates instead of polling
        _monitor.OnMetricsUpdated += OnMetricsUpdated;
    }

    private void Show()
    {
        if (_overlayWindow != null || _disposed) return;

        _overlayWindow = new Window
        {
            Title = "GTA5 Optimizer Overlay",
            Width = 180,
            Height = 120,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            Left = 10,
            Top = 10,
            ResizeMode = ResizeMode.NoResize
        };

        var grid = new Grid { Margin = new Thickness(8) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var bg = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 10, 10, 10)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6),
            Child = grid
        };

        _fpsText = CreateTextBlock(16, "#00E676");
        _cpuText = CreateTextBlock(12, "#00D4FF");
        _gpuText = CreateTextBlock(12, "#FF6D00");
        _ramText = CreateTextBlock(12, "#FFAB00");

        Grid.SetRow(_fpsText, 0);
        Grid.SetRow(_cpuText, 1);
        Grid.SetRow(_gpuText, 2);
        Grid.SetRow(_ramText, 3);

        grid.Children.Add(_fpsText);
        grid.Children.Add(_cpuText);
        grid.Children.Add(_gpuText);
        grid.Children.Add(_ramText);

        _overlayWindow.Content = bg;

        try
        {
            _overlayWindow.Show();
        }
        catch { }

        // Get initial values without blocking the UI thread
        _ = RefreshOverlayAsync();
    }

    private static TextBlock CreateTextBlock(double fontSize, string color)
    {
        return new TextBlock
        {
            FontSize = fontSize,
            FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New, monospace"),
            Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 1, 0, 1)
        };
    }

    private void OnMetricsUpdated(Models.Monitoring.PerformanceMetrics metrics)
    {
        try
        {
            // Only update if overlay is visible
            if (!_isVisible || _overlayWindow == null) return;

            _overlayWindow.Dispatcher.Invoke(() =>
            {
                UpdateOverlayImmediate(metrics);
            });
        }
        catch { }
    }

    private void UpdateOverlayImmediate(Models.Monitoring.PerformanceMetrics? metrics = null)
    {
        try
        {
            if (metrics == null)
            {
                // Fallback: get cached metrics
                var task = _monitor.GetCurrentMetricsAsync();
                task.Wait();
                metrics = task.Result;
            }

            if (_fpsText != null) _fpsText.Text = $"FPS: {metrics.CurrentFPS:F0}";
            if (_cpuText != null) _cpuText.Text = $"CPU: {metrics.CPUUsage:F0}%";
            if (_gpuText != null) _gpuText.Text = $"GPU: {metrics.GPUUsage:F0}%";
            if (_ramText != null) _ramText.Text = $"RAM: {metrics.RAMUsagePercent:F0}%";
        }
        catch { }
    }

    private void Hide()
    {
        try { _overlayWindow?.Close(); } catch { }
        _overlayWindow = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _monitor.OnMetricsUpdated -= OnMetricsUpdated;
        Hide();
    }
}
