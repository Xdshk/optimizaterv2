using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.System;
using System.Runtime.InteropServices;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Менеджер процессов для оптимизации приоритетов и управления процессами
/// </summary>
public class ProcessManager : IProcessManager
{
    private const int PROCESS_TERMINATE = 0x0001;
    private const int PROCESS_SET_INFORMATION = 0x0200;
    private const int PROCESS_QUERY_INFORMATION = 0x0400;
    private const int PROCESS_SET_PRIORITY_CLASS = 0x0008;
    private const int PROCESS_SUSPEND_RESUME = 0x0800;

    private const uint NORMAL_PRIORITY_CLASS = 0x0020;
    private const uint HIGH_PRIORITY_CLASS = 0x0080;
    private const uint REALTIME_PRIORITY_CLASS = 0x0100;
    private const uint IDLE_PRIORITY_CLASS = 0x0040;

    [DllImport("kernel32.dll")]
    private static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessAffinityMask(IntPtr hProcess, IntPtr dwProcessAffinityMask);

    [DllImport("ntdll.dll")]
    private static extern int NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll")]
    private static extern int NtResumeProcess(IntPtr processHandle);

    public Task<bool> SetProcessPriorityAsync(int processId, ProcessPriority priority)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            var priorityClass = priority switch
            {
                ProcessPriority.Low => IDLE_PRIORITY_CLASS,
                ProcessPriority.Normal => NORMAL_PRIORITY_CLASS,
                ProcessPriority.High => HIGH_PRIORITY_CLASS,
                ProcessPriority.Realtime => REALTIME_PRIORITY_CLASS,
                _ => NORMAL_PRIORITY_CLASS
            };

            return Task.FromResult(SetPriorityClass(process.Handle, priorityClass));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> SetProcessAffinityAsync(int processId, int affinityMask)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return Task.FromResult(SetProcessAffinityMask(process.Handle, (IntPtr)affinityMask));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> SuspendProcessAsync(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return Task.FromResult(NtSuspendProcess(process.Handle) == 0);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> ResumeProcessAsync(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return Task.FromResult(NtResumeProcess(process.Handle) == 0);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> KillProcessAsync(int processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            process.Kill();
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<List<RunningProcess>> GetRunningProcessesAsync()
    {
        var processes = new List<RunningProcess>();
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                processes.Add(new RunningProcess
                {
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName,
                    WorkingSet = process.WorkingSet64,
                    PrivateBytes = process.PrivateMemorySize64,
                    IsResponding = process.Responding,
                    StartTime = process.StartTime
                });
            }
            catch
            {
                // Process may have exited
            }
        }
        return Task.FromResult(processes);
    }

    public async Task<List<RunningProcess>> GetProcessesByNameAsync(string processName)
    {
        var processes = await GetRunningProcessesAsync();
        return processes.Where(p => p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public Task<bool> IsProcessRunningAsync(string processName)
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);
            return Task.FromResult(processes.Length > 0);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
