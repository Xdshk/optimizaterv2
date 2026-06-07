using GTA5Optimizer.Models.Enums;

namespace GTA5Optimizer.Models.System;

public enum ProcessPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Realtime = 3
}

public sealed class RunningProcess
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public long WorkingSet { get; set; }
    public long PrivateBytes { get; set; }
    public bool IsResponding { get; set; }
    public DateTime StartTime { get; set; }
    public double CPUUsage { get; set; }
    public ProcessActionType RecommendedAction { get; set; } = ProcessActionType.None;
}
