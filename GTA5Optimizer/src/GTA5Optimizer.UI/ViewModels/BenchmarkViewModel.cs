using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GTA5Optimizer.Core.Interfaces;

namespace GTA5Optimizer.UI.ViewModels;

public partial class BenchmarkViewModel : ObservableObject
{
    private readonly IBenchmarkService _benchmark;
    private readonly ISystemOptimizer _optimizer;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private int _selectedDuration = 30;
    [ObservableProperty] private BenchmarkResultDto? _result;
    [ObservableProperty] private BenchmarkComparisonDto? _comparison;
    [ObservableProperty] private string _statusText = "Выберите длительность и нажмите 'Запустить'";
    [ObservableProperty] private bool _hasBeforeSnapshot;

    // Duration chip bindings
    [ObservableProperty] private bool _isDuration15;
    [ObservableProperty] private bool _isDuration30 = true;
    [ObservableProperty] private bool _isDuration60;
    [ObservableProperty] private bool _isDuration120;

    partial void OnSelectedDurationChanged(int value)
    {
        IsDuration15 = value == 15;
        IsDuration30 = value == 30;
        IsDuration60 = value == 60;
        IsDuration120 = value == 120;
    }

    public int[] DurationOptions { get; } = { 15, 30, 60, 120 };

    public BenchmarkViewModel(IBenchmarkService benchmark, ISystemOptimizer optimizer)
    {
        _benchmark = benchmark;
        _optimizer = optimizer;
    }

    [RelayCommand]
    private async Task CaptureBeforeAsync()
    {
        try
        {
            IsRunning = true;
            StatusText = "Снимаю снимок 'До'...";
            var snapshot = await _benchmark.CaptureSnapshotAsync();
            Comparison = new BenchmarkComparisonDto { BeforeSnapshot = new SnapshotDto(snapshot) };
            HasBeforeSnapshot = true;
            StatusText = "Снимок 'До' сохранён. Запустите оптимизацию, затем нажмите 'Снять После'";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private async Task CaptureAfterAsync()
    {
        try
        {
            IsRunning = true;
            StatusText = "Снимаю снимок 'После'...";
            var snapshot = await _benchmark.CaptureSnapshotAsync();
            if (Comparison != null)
            {
                Comparison.AfterSnapshot = new SnapshotDto(snapshot);
                Comparison.Calculate();
            }
            StatusText = "Снимок 'После' сохранён. Сравнение готово.";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private async Task RunBenchmarkAsync()
    {
        try
        {
            IsRunning = true;
            StatusText = "Бенчмарк запущен. Играйте в GTA V для точных результатов...";
            var result = await _benchmark.RunBenchmarkAsync(TimeSpan.FromSeconds(SelectedDuration));
            Result = new BenchmarkResultDto(result);
            StatusText = $"Бенчмарк завершён. Средний FPS: {result.AverageFPS:F1}";
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка бенчмарка: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        Result = null;
        Comparison = null;
        HasBeforeSnapshot = false;
        StatusText = "Выберите длительность и нажмите 'Запустить'";
    }
}

public sealed class BenchmarkResultDto
{
    public double AverageFPS { get; }
    public double MinFPS { get; }
    public double MaxFPS { get; }
    public double OnePercentLowFPS { get; }
    public double PointOnePercentLowFPS { get; }
    public double AverageFrameTimeMs { get; }
    public double MaxFrameTimeMs { get; }
    public double AverageCPUUsage { get; }
    public double AverageGPUUsage { get; }
    public double AverageRAMUsagePercent { get; }
    public double PeakCPUTemperature { get; }
    public double PeakGPUTemperature { get; }
    public int StutterCount { get; }
    public string PerformanceGrade { get; }

    public BenchmarkResultDto(Core.Interfaces.BenchmarkResult result)
    {
        AverageFPS = result.AverageFPS;
        MinFPS = result.MinFPS;
        MaxFPS = result.MaxFPS;
        OnePercentLowFPS = result.OnePercentLowFPS;
        PointOnePercentLowFPS = result.PointOnePercentLowFPS;
        AverageFrameTimeMs = result.AverageFrameTimeMs;
        MaxFrameTimeMs = result.MaxFrameTimeMs;
        AverageCPUUsage = result.AverageCPUUsage;
        AverageGPUUsage = result.AverageGPUUsage;
        AverageRAMUsagePercent = result.AverageRAMUsagePercent;
        PeakCPUTemperature = result.PeakCPUTemperature;
        PeakGPUTemperature = result.PeakGPUTemperature;
        StutterCount = result.StutterCount;
        PerformanceGrade = result.AverageFPS switch
        {
            >= 120 => "Отлично",
            >= 60 => "Хорошо",
            >= 30 => "Удовлетворительно",
            _ => "Плохо"
        };
    }
}

public sealed class SnapshotDto
{
    public double FPS { get; }
    public double CPUUsage { get; }
    public double GPUUsage { get; }
    public double RAMUsage { get; }
    public double CPUTemp { get; }
    public double GPUTemp { get; }
    public double FrameTime { get; }

    public SnapshotDto(Core.Interfaces.BenchmarkSnapshot snapshot)
    {
        FPS = snapshot.CurrentFPS;
        CPUUsage = snapshot.CPUUsage;
        GPUUsage = snapshot.GPUUsage;
        RAMUsage = snapshot.RAMUsagePercent;
        CPUTemp = snapshot.CPUTemperature;
        GPUTemp = snapshot.GPUTemperature;
        FrameTime = snapshot.FrameTimeMs;
    }
}

public sealed class BenchmarkComparisonDto
{
    public SnapshotDto? BeforeSnapshot { get; set; }
    public SnapshotDto? AfterSnapshot { get; set; }
    public double FPS_Gain { get; private set; }
    public double FPS_GainPercent { get; private set; }
    public double FrameTime_Reduction { get; private set; }
    public string Summary { get; private set; } = "";

    public void Calculate()
    {
        if (BeforeSnapshot == null || AfterSnapshot == null) return;
        FPS_Gain = AfterSnapshot.FPS - BeforeSnapshot.FPS;
        FPS_GainPercent = BeforeSnapshot.FPS > 0 ? (FPS_Gain / BeforeSnapshot.FPS) * 100 : 0;
        FrameTime_Reduction = BeforeSnapshot.FrameTime - AfterSnapshot.FrameTime;
        Summary = FPS_GainPercent switch
        {
            > 10 => $"Значительный прирост: +{FPS_GainPercent:F1}% (+{FPS_Gain:F0} FPS)",
            > 0 => $"Небольшой прирост: +{FPS_GainPercent:F1}% (+{FPS_Gain:F0} FPS)",
            0 => "FPS не изменился",
            _ => $"FPS снизился: {FPS_GainPercent:F1}%"
        };
    }
}
