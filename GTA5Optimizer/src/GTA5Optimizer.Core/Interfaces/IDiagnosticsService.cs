using GTA5Optimizer.Models.Monitoring;

namespace GTA5Optimizer.Core.Interfaces;

public interface IDiagnosticsService
{
    /// <summary>
    /// Runs a full system diagnostics check and returns all findings.
    /// </summary>
    Task<DiagnosticsResult> RunFullDiagnosticsAsync(CancellationToken ct = default);

    /// <summary>
    /// Analyzes GTA V settings.xml for performance issues.
    /// </summary>
    Task<GtaVSettingsAnalysis> AnalyzeGtaVSettingsAsync(string gtaVPath, CancellationToken ct = default);

    /// <summary>
    /// Calculates a PC readiness score for Majestic RP.
    /// </summary>
    Task<PcReadinessScore> CalculateReadinessScoreAsync(CancellationToken ct = default);
}

public sealed class DiagnosticsResult
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public List<DiagnosticIssue> Issues { get; set; } = new();
    public List<DiagnosticIssue> Warnings { get; set; } = new();
    public int TotalScore { get; set; }
    public bool HasCriticalIssues => Issues.Any(i => i.Severity == DiagnosticSeverity.Critical);
}

public sealed class DiagnosticIssue
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public DiagnosticSeverity Severity { get; set; }
    public string Category { get; set; } = string.Empty; // CPU, GPU, RAM, Disk, Network, Thermal, Game
}

public enum DiagnosticSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public sealed class GtaVSettingsAnalysis
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string SettingsPath { get; set; } = string.Empty;
    public List<SettingsIssue> Issues { get; set; } = new();
    public List<SettingsRecommendation> Recommendations { get; set; } = new();
    public int PerformanceScore { get; set; }
}

public sealed class SettingsIssue
{
    public string SettingName { get; set; } = string.Empty;
    public string CurrentValue { get; set; } = string.Empty;
    public string RecommendedValue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public SettingsIssueSeverity Severity { get; set; }
}

public enum SettingsIssueSeverity
{
    Info = 0,
    Performance = 1,
    Critical = 2
}

public sealed class SettingsRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int ExpectedFpsGain { get; set; }
}

public sealed class PcReadinessScore
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int CpuScore { get; set; }
    public int GpuScore { get; set; }
    public int RamScore { get; set; }
    public int StorageScore { get; set; }
    public int NetworkScore { get; set; }
    public int OverallScore { get; set; }
    public string CpuName { get; set; } = string.Empty;
    public string GpuName { get; set; } = string.Empty;
    public long TotalRAM_GB { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();
}
