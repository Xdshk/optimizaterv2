using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Game;
using GTA5Optimizer.Models.Majestic;
using GTA5Optimizer.Models.Monitoring;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Анализатор Majestic RP
/// </summary>
public class MajesticAnalyzer : IMajesticAnalyzer
{
    private readonly IGameDetector _gameDetector;
    private readonly IPerformanceMonitor _performanceMonitor;

    public MajesticAnalyzer(IGameDetector gameDetector, IPerformanceMonitor performanceMonitor)
    {
        _gameDetector = gameDetector;
        _performanceMonitor = performanceMonitor;
    }

    public async Task<MajesticAnalysisResult> AnalyzeAsync()
    {
        var result = new MajesticAnalysisResult();

        try
        {
            var gameInfo = await _gameDetector.DetectGameAsync();
            var metrics = await _performanceMonitor.GetCurrentMetricsAsync();

            result.CurrentFPS = metrics.CurrentFPS;
            result.CPUUsage = metrics.CPUUsage;
            result.GPUUsage = metrics.GPUUsage;
            result.RAMUsage = metrics.RAMUsagePercent;
            result.DiskReadSpeedMBps = metrics.DiskReadSpeedMBps;
            result.MemoryCleared = metrics.TotalMemoryFreed;
            result.ProcessesClosed = metrics.TotalProcessesClosed;

            result.FPSIssues = AnalyzeFPSIssues(metrics, gameInfo);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    private static List<FPSIssue> AnalyzeFPSIssues(PerformanceMetrics metrics, GameInfo gameInfo)
    {
        var issues = new List<FPSIssue>();

        if (gameInfo.IsOnHDD && metrics.DiskReadSpeedMBps < 100)
        {
            issues.Add(new FPSIssue
            {
                Type = "Disk",
                Severity = "Critical",
                Description = "GTA V установлен на HDD, текстуры грузятся медленно",
                Recommendation = "Перенесите игру на SSD для улучшения производительности"
            });
        }

        if (metrics.CPUUsage > 90)
        {
            issues.Add(new FPSIssue
            {
                Type = "CPU",
                Severity = "High",
                Description = $"Высокая нагрузка на CPU: {metrics.CPUUsage:F1}%",
                Recommendation = "Закройте фоновые приложения"
            });
        }

        if (metrics.RAMUsagePercent > 85)
        {
            issues.Add(new FPSIssue
            {
                Type = "RAM",
                Severity = "High",
                Description = $"Низкое количество свободной памяти: {100 - metrics.RAMUsagePercent:F1}%",
                Recommendation = "Выполните очистку памяти"
            });
        }

        if (metrics.GPUUsage > 95)
        {
            issues.Add(new FPSIssue
            {
                Type = "GPU",
                Severity = "Medium",
                Description = $"GPU перегружен: {metrics.GPUUsage:F1}%",
                Recommendation = "Уменьшите настройки графики"
            });
        }

        return issues;
    }
}
