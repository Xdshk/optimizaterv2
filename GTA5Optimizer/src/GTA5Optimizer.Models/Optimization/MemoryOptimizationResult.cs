namespace GTA5Optimizer.Models.Optimization;

public sealed class MemoryOptimizationResult
{
    public bool Success { get; set; }
    public long MemoryFreedBytes { get; set; }
    public long StandbyMemoryCleared { get; set; }
    public TimeSpan Duration { get; set; }
    public string Details { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}
