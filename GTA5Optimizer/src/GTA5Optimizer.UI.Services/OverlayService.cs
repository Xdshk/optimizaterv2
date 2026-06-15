using GTA5Optimizer.Core.Interfaces;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GTA5Optimizer.UI.Services;

/// <summary>
/// Сервис игрового оверлея (показывает FPS, CPU, GPU, температуру поверх игры)
/// Layout: two columns — left for labels, right for values. All text large and bold.
/// </summary>
public class OverlayService : IDisposable
{
    private readonly IPerformanceMonitor _monitor;
    private Window? _overlayWindow;
    private TextBlock? _fpsValue;
    private TextBlock? _cpuValue;
    private TextBlock? _gpuValue;
    private TextBlock? _ramValue;
    private TextBlock? _cpuTempValue;
    private TextBlock? _gpuTempValue;
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

        // Main window — compact, no wasted space
        _overlayWindow = new Window
        {
            Title = "GTA5 Optimizer Overlay",
            Width = 320,
            Height = 200,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            Left = 10,
            Top = 10,
            ResizeMode = ResizeMode.NoResize
        };

        // Two-column grid: labels | values
        var grid = new Grid { Margin = new Thickness(12, 10, 12, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 6; i++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var bg = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 10, 10, 15)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(0),
            Child = grid
        };

        double valueSize = 18;
        double labelSize = 12;

        // Row 0: FPS (big, full width, spans both columns)
        _fpsValue = CreateValueBlock(valueSize, "#00E676", FontWeights.Bold);
        _fpsValue.Text = "FPS: —";
        Grid.SetRow(_fpsValue, 0);
        Grid.SetColumnSpan(_fpsValue, 2);
        grid.Children.Add(_fpsValue);

        // Row 1: CPU% | CPU°C
        var cpuLabel = CreateLabelBlock("CPU:", labelSize, "#AAAAAA");
        _cpuValue = CreateValueBlock(valueSize, "#00D4FF", FontWeights.SemiBold);
        _cpuTempValue = CreateValueBlock(valueSize, "#22D3EE", FontWeights.SemiBold);
        Grid.SetRow(cpuLabel, 1); Grid.SetColumn(cpuLabel, 0);
        Grid.SetRow(_cpuValue, 1); Grid.SetColumn(_cpuValue, 1);
        _cpuTempValue.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetRow(_cpuTempValue, 1); Grid.SetColumn(_cpuTempValue, 1);
        grid.Children.Add(cpuLabel);
        grid.Children.Add(_cpuValue);
        grid.Children.Add(_cpuTempValue);

        // Row 2: GPU% | GPU°C
        var gpuLabel = CreateLabelBlock("GPU:", labelSize, "#AAAAAA");
        _gpuValue = CreateValueBlock(valueSize, "#FF6D00", FontWeights.SemiBold);
        _gpuTempValue = CreateValueBlock(valueSize, "#F59E0B", FontWeights.SemiBold);
        Grid.SetRow(gpuLabel, 2); Grid.SetColumn(gpuLabel, 0);
        Grid.SetRow(_gpuValue, 2); Grid.SetColumn(_gpuValue, 1);
        _gpuTempValue.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetRow(_gpuTempValue, 2); Grid.SetColumn(_gpuTempValue, 1);
        grid.Children.Add(gpuLabel);
        grid.Children.Add(_gpuValue);
        grid.Children.Add(_gpuTempValue);

        // Row 3: RAM%
        var ramLabel = CreateLabelBlock("RAM:", labelSize, "#AAAAAA");
        _ramValue = CreateValueBlock(valueSize, "#FFAB00", FontWeights.SemiBold);
        Grid.SetRow(ramLabel, 3); Grid.SetColumn(ramLabel, 0);
        Grid.SetRow(_ramValue, 3); Grid.SetColumn(_ramValue, 1);
        grid.Children.Add(ramLabel);
        grid.Children.Add(_ramValue);

        _overlayWindow.Content = bg;

        try { _overlayWindow.Show(); } catch { }

        _ = RefreshOverlayAsync();
    }

    private static TextBlock CreateValueBlock(double fontSize, string color, FontWeight weight)
    {
        return new TextBlock
        {
            FontSize = fontSize,
            FontFamily = new FontFamily("Consolas, Courier New, monospace"),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
            FontWeight = weight,
            Margin = new Thickness(0, 2, 0, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static TextBlock CreateLabelBlock(string text, double fontSize, string color)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontFamily = new FontFamily("Consolas, Courier New, monospace"),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
            FontWeight = FontWeights.Normal,
            Margin = new Thickness(0, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private void OnMetricsUpdated(Models.Monitoring.PerformanceMetrics metrics)
    {
        if (!_isVisible || _overlayWindow == null) return;
        try
        {
            _overlayWindow.Dispatcher.Invoke(() =>
            {
                if (_fpsValue != null) _fpsValue.Text = $"FPS: {metrics.CurrentFPS:F0}";
                if (_cpuValue != null) _cpuValue.Text = $"{metrics.CPUUsage:F0}%";
                if (_gpuValue != null) _gpuValue.Text = $"{metrics.GPUUsage:F0}%";
                if (_ramValue != null) _ramValue.Text = $"{metrics.RAMUsagePercent:F0}%";
                if (_cpuTempValue != null) _cpuTempValue.Text = $"{metrics.CPUTemperature:F0}°C";
                if (_gpuTempValue != null) _gpuTempValue.Text = $"{metrics.GPUTemperature:F0}°C";
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
                await _overlayWindow.Dispatcher.InvokeAsync(() =>
                {
                    if (_fpsValue != null) _fpsValue.Text = $"FPS: {metrics.CurrentFPS:F0}";
                    if (_cpuValue != null) _cpuValue.Text = $"{metrics.CPUUsage:F0}%";
                    if (_gpuValue != null) _gpuValue.Text = $"{metrics.GPUUsage:F0}%";
                    if (_ramValue != null) _ramValue.Text = $"{metrics.RAMUsagePercent:F0}%";
                    if (_cpuTempValue != null) _cpuTempValue.Text = $"{metrics.CPUTemperature:F0}°C";
                    if (_gpuTempValue != null) _gpuTempValue.Text = $"{metrics.GPUTemperature:F0}°C";
                });
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
