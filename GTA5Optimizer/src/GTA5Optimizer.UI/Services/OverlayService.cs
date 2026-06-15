using GTA5Optimizer.Core.Interfaces;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaFontFamily = System.Windows.Media.FontFamily;
using MediaFontWeights = System.Windows.Media.FontWeights;

namespace GTA5Optimizer.UI.Services;

/// <summary>
/// Сервис игрового оверлея (показывает FPS, CPU, GPU, температуру поверх игры)
/// </summary>
public class OverlayService : IDisposable
{
    private readonly IPerformanceMonitor _monitor;
    private Window? _overlayWindow;
    private TextBlock? _fpsText;
    private TextBlock? _cpuText;
    private TextBlock? _gpuText;
    private TextBlock? _ramText;
    private TextBlock? _cpuTempText;
    private TextBlock? _gpuTempText;
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
        _monitor.OnMetricsUpdated += OnMetricsUpdated;
    }

    private void Show()
    {
        if (_overlayWindow != null || _disposed) return;

        _overlayWindow = new Window
        {
            Title = "GTA5 Optimizer Overlay",
            Width = 200,
            Height = 160,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = MediaBrushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            Left = 10,
            Top = 10,
            ResizeMode = ResizeMode.NoResize
        };

        var grid = new Grid { Margin = new Thickness(8) };
        for (int i = 0; i < 6; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var bg = new Border
        {
            Background = new SolidColorBrush(MediaColor.FromArgb(160, 10, 10, 10)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6),
            Child = grid
        };

        _fpsText = CreateTextBlock(16, "#00E676");
        _cpuText = CreateTextBlock(11, "#00D4FF");
        _gpuText = CreateTextBlock(11, "#FF6D00");
        _ramText = CreateTextBlock(11, "#FFAB00");
        _cpuTempText = CreateTextBlock(11, "#22D3EE");
        _gpuTempText = CreateTextBlock(11, "#F59E0B");

        Grid.SetRow(_fpsText, 0);
        Grid.SetRow(_cpuText, 1);
        Grid.SetRow(_gpuText, 2);
        Grid.SetRow(_ramText, 3);
        Grid.SetRow(_cpuTempText, 4);
        Grid.SetRow(_gpuTempText, 5);

        grid.Children.Add(_fpsText);
        grid.Children.Add(_cpuText);
        grid.Children.Add(_gpuText);
        grid.Children.Add(_ramText);
        grid.Children.Add(_cpuTempText);
        grid.Children.Add(_gpuTempText);

        _overlayWindow.Content = bg;

        try
        {
            _overlayWindow.Show();
        }
        catch { }

        _ = RefreshOverlayAsync();
    }

    private static TextBlock CreateTextBlock(double fontSize, string color)
    {
        return new TextBlock
        {
            FontSize = fontSize,
            FontFamily = new MediaFontFamily("Consolas, Courier New, monospace"),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 1, 0, 1)
        };
    }

    private void OnMetricsUpdated(Models.Monitoring.PerformanceMetrics metrics)
    {
        try
        {
            if (!_isVisible || _overlayWindow == null) return;

            _overlayWindow.Dispatcher.Invoke(() =>
            {
                UpdateOverlayImmediate(metrics);
            });
        }
        catch { }
    }

    private async Task RefreshOverlayAsync()
    {
        try
        {
            var metrics = await _monitor.GetCurrentMetricsAsync();
            if (_overlayWindow != null && !_disposed)
            {
                await _overlayWindow.Dispatcher.InvokeAsync(() => UpdateOverlayImmediate(metrics));
            }
        }
        catch { }
    }

    private void UpdateOverlayImmediate(Models.Monitoring.PerformanceMetrics metrics)
    {
        if (_fpsText != null) _fpsText.Text = $"FPS: {metrics.CurrentFPS:F0}";
        if (_cpuText != null) _cpuText.Text = $"CPU: {metrics.CPUUsage:F0}%";
        if (_gpuText != null) _gpuText.Text = $"GPU: {metrics.GPUUsage:F0}%";
        if (_ramText != null) _ramText.Text = $"RAM: {metrics.RAMUsagePercent:F0}%";
        if (_cpuTempText != null) _cpuTempText.Text = $"CPU: {metrics.CPUTemperature:F0}°C";
        if (_gpuTempText != null) _gpuTempText.Text = $"GPU: {metrics.GPUTemperature:F0}°C";
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
