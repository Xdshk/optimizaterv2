using CommunityToolkit.Mvvm.ComponentModel;

namespace GTA5Optimizer.UI.ViewModels;

/// <summary>
/// Обёртка для сглаженных метрик — предотвращает мерцание UI
/// при быстрых изменениях значений. Использует экспоненциальное
/// скользящее среднее (EMA) для всех метрик.
/// </summary>
public partial class SmoothedMetrics : ObservableObject
{
    private const double Alpha = 0.3; // Smoothing factor (0-1, lower = smoother)

    [ObservableProperty] private double _currentFPS;
    [ObservableProperty] private double _cpuUsage;
    [ObservableProperty] private double _gpuUsage;
    [ObservableProperty] private double _ramUsagePercent;
    [ObservableProperty] private double _cpuUsageGame;
    [ObservableProperty] private double _cpuTemperature;
    [ObservableProperty] private double _gpuTemperature;
    [ObservableProperty] private double _gpuUsagePercent;
    [ObservableProperty] private double _diskActiveTimePercent;
    [ObservableProperty] private double _currentPing;

    /// <summary>
    /// Обновляет все метрики с применением EMA сглаживания.
    /// Первое значение принимается как есть, последующие — сглаживаются.
    /// </summary>
    public void Update(double fps, double cpu, double gpu, double ram,
        double cpuGame = -1, double cpuTemp = -1, double gpuTemp = -1,
        double gpuUsage = -1, double diskActive = -1, double ping = -1)
    {
        CurrentFPS = Smooth(CurrentFPS, fps);
        CpuUsage = Smooth(CpuUsage, cpu);
        GpuUsage = Smooth(GpuUsage, gpu);
        RamUsagePercent = Smooth(RamUsagePercent, ram);

        if (cpuGame >= 0) CpuUsageGame = Smooth(CpuUsageGame, cpuGame);
        if (cpuTemp >= 0) CpuTemperature = Smooth(CpuTemperature, cpuTemp);
        if (gpuTemp >= 0) GpuTemperature = Smooth(GpuTemperature, gpuTemp);
        if (gpuUsage >= 0) GpuUsagePercent = Smooth(GpuUsagePercent, gpuUsage);
        if (diskActive >= 0) DiskActiveTimePercent = Smooth(DiskActiveTimePercent, diskActive);
        if (ping >= 0) CurrentPing = Smooth(CurrentPing, ping);
    }

    private static double Smooth(double current, double newValue)
    {
        if (current == 0) return newValue;
        return current * (1 - Alpha) + newValue * Alpha;
    }
}
