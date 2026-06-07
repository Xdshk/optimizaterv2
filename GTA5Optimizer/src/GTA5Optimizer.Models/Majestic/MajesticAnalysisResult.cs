namespace GTA5Optimizer.Models.Majestic;

public sealed class MajesticAnalysisResult
{
    public double CurrentFPS { get; set; }
    public double CPUUsage { get; set; }
    public double GPUUsage { get; set; }
    public double RAMUsage { get; set; }
    public double DiskReadSpeedMBps { get; set; }
    public long MemoryCleared { get; set; }
    public int ProcessesClosed { get; set; }
    public List<FPSIssue> FPSIssues { get; set; } = new();
    public string? Error { get; set; }
}

public sealed class FPSIssue
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}
