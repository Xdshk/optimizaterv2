using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GTA5Optimizer.Core.Interfaces;
using System.Collections.ObjectModel;

namespace GTA5Optimizer.UI.ViewModels;

public partial class DiagnosticsViewModel : ObservableObject
{
    private readonly IDiagnosticsService _diagnostics;
    private readonly IGameDetector _gameDetector;
    private readonly ILoggerService _loggerService;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private int _totalScore;
    [ObservableProperty] private ObservableCollection<DiagnosticIssueDto> _issues = new();
    [ObservableProperty] private ObservableCollection<DiagnosticIssueDto> _warnings = new();
    [ObservableProperty] private bool _hasIssues;
    [ObservableProperty] private bool _hasWarnings;
    [ObservableProperty] private bool _hasGtaVAnalysis;
    [ObservableProperty] private GtaVSettingsDto? _gtaVAnalysis;
    [ObservableProperty] private PcReadinessDto? _readinessScore;
    [ObservableProperty] private string _statusText = "Нажмите 'Запустить диагностику' для анализа системы";

    public DiagnosticsViewModel(
        IDiagnosticsService diagnostics,
        IGameDetector gameDetector,
        ILoggerService loggerService)
    {
        _diagnostics = diagnostics;
        _gameDetector = gameDetector;
        _loggerService = loggerService;
    }

    [RelayCommand]
    private async Task RunDiagnosticsAsync()
    {
        IsRunning = true;
        StatusText = "Выполняется диагностика...";
        Issues.Clear();
        Warnings.Clear();

        try
        {
            var result = await _diagnostics.RunFullDiagnosticsAsync();

            foreach (var issue in result.Issues)
                Issues.Add(new DiagnosticIssueDto(issue));

            foreach (var warning in result.Warnings)
                Warnings.Add(new DiagnosticIssueDto(warning));

            HasIssues = Issues.Count > 0;
            HasWarnings = Warnings.Count > 0;

            TotalScore = result.TotalScore;
            StatusText = BuildStatusText(result.HasCriticalIssues, result.Issues.Count, result.Warnings.Count);

            // Run GTA V settings analysis
            var gameInfo = await _gameDetector.DetectGameAsync();
            if (!string.IsNullOrEmpty(gameInfo.InstallPath))
            {
                var analysis = await _diagnostics.AnalyzeGtaVSettingsAsync(gameInfo.InstallPath);
                GtaVAnalysis = new GtaVSettingsDto(analysis);
                HasGtaVAnalysis = true;
            }
            else
            {
                HasGtaVAnalysis = false;
            }

            // Run PC readiness score
            var score = await _diagnostics.CalculateReadinessScoreAsync();
            ReadinessScore = new PcReadinessDto(score);
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка диагностики: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private static string BuildStatusText(bool hasCriticalIssues, int issueCount, int warningCount)
    {
        if (hasCriticalIssues)
            return $"Критические проблемы: {issueCount}. Проверьте блок «Проблемы».";

        if (issueCount > 0 && warningCount > 0)
            return $"Диагностика завершена. Найдено проблем: {issueCount}, предупреждений: {warningCount}.";

        if (issueCount > 0)
            return $"Диагностика завершена. Найдено проблем: {issueCount}.";

        if (warningCount > 0)
            return $"Диагностика завершена. Предупреждения: {warningCount}.";

        return $"Диагностика завершена. Оценка: {TotalScore}/100";
    }
}

public sealed class DiagnosticIssueDto
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string Recommendation { get; }
    public string Severity { get; }
    public string Category { get; }
    public string SeverityColor { get; }

    public DiagnosticIssueDto(Core.Interfaces.DiagnosticIssue issue)
    {
        Id = issue.Id;
        Title = issue.Title;
        Description = issue.Description;
        Recommendation = issue.Recommendation;
        Severity = issue.Severity.ToString();
        Category = issue.Category;
        SeverityColor = issue.Severity switch
        {
            Core.Interfaces.DiagnosticSeverity.Critical => "#FF4444",
            Core.Interfaces.DiagnosticSeverity.Warning => "#FFAA00",
            _ => "#00FF88"
        };
    }
}

public sealed class GtaVSettingsDto
{
    public string SettingsPath { get; }
    public int PerformanceScore { get; }
    public int IssueCount { get; }
    public List<string> TopIssues { get; }
    public List<string> TopRecommendations { get; }

    public GtaVSettingsDto(Core.Interfaces.GtaVSettingsAnalysis analysis)
    {
        SettingsPath = analysis.SettingsPath;
        PerformanceScore = analysis.PerformanceScore;
        IssueCount = analysis.Issues.Count;
        TopIssues = analysis.Issues
            .Where(i => i.Severity >= Core.Interfaces.SettingsIssueSeverity.Performance)
            .Select(i => $"{i.SettingName}: {i.CurrentValue} → {i.RecommendedValue}")
            .Take(5).ToList();
        TopRecommendations = analysis.Recommendations
            .Select(r => $"{r.Title} (~+{r.ExpectedFpsGain} FPS)")
            .Take(5).ToList();
    }
}

public sealed class PcReadinessDto
{
    public int CpuScore { get; }
    public int GpuScore { get; }
    public int RamScore { get; }
    public int StorageScore { get; }
    public int NetworkScore { get; }
    public int OverallScore { get; }
    public string CpuName { get; }
    public string GpuName { get; }
    public long TotalRAM_GB { get; }
    public string Summary { get; }
    public List<string> Strengths { get; }
    public List<string> Weaknesses { get; }
    public string OverallColor { get; }

    public PcReadinessDto(Core.Interfaces.PcReadinessScore score)
    {
        CpuScore = score.CpuScore;
        GpuScore = score.GpuScore;
        RamScore = score.RamScore;
        StorageScore = score.StorageScore;
        NetworkScore = score.NetworkScore;
        OverallScore = score.OverallScore;
        CpuName = score.CpuName;
        GpuName = score.GpuName;
        TotalRAM_GB = score.TotalRAM_GB;
        Summary = score.Summary;
        Strengths = score.Strengths;
        Weaknesses = score.Weaknesses;
        OverallColor = score.OverallScore switch
        {
            >= 75 => "#00E676",
            >= 50 => "#FFAA00",
            _ => "#FF4444"
        };
    }
}
