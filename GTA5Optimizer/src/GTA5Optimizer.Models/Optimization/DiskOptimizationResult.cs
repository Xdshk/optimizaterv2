namespace GTA5Optimizer.Models.Optimization;

public sealed class DiskOptimizationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long CacheClearedBytes { get; set; }
    public TimeSpan Duration { get; set; }
    public Exception? Exception { get; set; }
}
