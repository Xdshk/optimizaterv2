using GTA5Optimizer.Models.System;

namespace GTA5Optimizer.Core.Interfaces;

public interface IProcessManager
{
    Task<bool> SetProcessPriorityAsync(int processId, ProcessPriority priority);
    Task<bool> SetProcessAffinityAsync(int processId, int affinityMask);
    Task<bool> SuspendProcessAsync(int processId);
    Task<bool> ResumeProcessAsync(int processId);
    Task<bool> KillProcessAsync(int processId);
    Task<List<RunningProcess>> GetRunningProcessesAsync();
    Task<List<RunningProcess>> GetProcessesByNameAsync(string processName);
    Task<bool> IsProcessRunningAsync(string processName);
}
