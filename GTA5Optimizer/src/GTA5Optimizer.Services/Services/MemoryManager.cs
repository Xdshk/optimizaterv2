using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Optimization;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GTA5Optimizer.Services.Services;

public sealed class MemoryManager : IMemoryManager
{
    private readonly ILogger<MemoryManager> _logger;

    // NtSetSystemInformation constants
    private const int SystemMemoryListInformation = 80;
    private const int MemoryPurgeStandbyList = 4;
    private const int MemoryFlushModifiedList = 3;
    private const int MemoryCombineMemoryLists = 35;

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetSystemInformation(int SystemInformationClass, ref int SystemInformation, int SystemInformationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
        }
    }

    public MemoryManager(ILogger<MemoryManager> logger)
    {
        _logger = logger;
    }

    public async Task<MemoryOptimizationResult> OptimizeMemoryAsync()
    {
        var result = new MemoryOptimizationResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var initialAvailable = GetAvailablePhysicalMemory();
            long standbyCleared = 0;
            long workingSetTrimmed = 0;

            // Step 1: Clear standby memory using NtSetSystemInformation
            standbyCleared = await ClearStandbyMemoryAsync();

            // Step 2: Trim working sets of non-essential processes
            workingSetTrimmed = await TrimWorkingSetsAsync();

            stopwatch.Stop();

            var finalAvailable = GetAvailablePhysicalMemory();
            var totalFreed = Math.Max(0, finalAvailable - initialAvailable);

            result.Success = true;
            result.MemoryFreedBytes = totalFreed;
            result.StandbyMemoryCleared = standbyCleared;
            result.Duration = stopwatch.Elapsed;

            var details = new List<string>();
            if (standbyCleared > 0)
                details.Add($"Standby cleared: {standbyCleared / 1024.0 / 1024.0:F0} MB");
            if (workingSetTrimmed > 0)
                details.Add($"Working sets trimmed: {workingSetTrimmed / 1024.0 / 1024.0:F0} MB");
            details.Add($"Total freed: {totalFreed / 1024.0 / 1024.0:F1} MB");
            result.Details = string.Join(", ", details);

            _logger.LogInformation("Memory optimization completed: {Details}", result.Details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during memory optimization");
            result.Success = false;
            result.Exception = ex;
            result.Details = $"Error: {ex.Message}";
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    /// <summary>
    /// Clears the standby memory list using NtSetSystemInformation.
    /// This requires SeProfileSingleProcessPrivilege (typically needs admin).
    /// </summary>
    public Task<long> ClearStandbyMemoryAsync()
    {
        return Task.Run(() =>
        {
            long cleared = 0;
            try
            {
                var before = GetStandbyMemorySize();

                int purgeCommand = MemoryPurgeStandbyList;
                int status = NtSetSystemInformation(SystemMemoryListInformation, ref purgeCommand, sizeof(int));

                if (status == 0) // STATUS_SUCCESS
                {
                    var after = GetStandbyMemorySize();
                    cleared = Math.Max(0, before - after);
                    _logger.LogInformation("Standby memory cleared: {ClearedMB:F0} MB (before: {BeforeMB:F0}, after: {AfterMB:F0})",
                        cleared / 1024.0 / 1024.0, before / 1024.0 / 1024.0, after / 1024.0 / 1024.0);
                }
                else
                {
                    _logger.LogWarning("NtSetSystemInformation(MemoryPurgeStandbyList) returned status 0x{Status:X8}. " +
                        "May require administrator privileges or unsupported on this OS version.", status);

                    // Fallback: trim modified page list as well
                    try
                    {
                        int flushCommand = MemoryFlushModifiedList;
                        NtSetSystemInformation(SystemMemoryListInformation, ref flushCommand, sizeof(int));
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear standby memory via NtSetSystemInformation");
            }
            return cleared;
        });
    }

    public Task<long> GetAvailableMemoryAsync()
    {
        return Task.FromResult(GetAvailablePhysicalMemory());
    }

    public Task<long> GetStandbyMemoryAsync()
    {
        return Task.FromResult(GetStandbyMemorySize());
    }

    public Task<bool> TrimWorkingSetAsync(int processId)
    {
        return Task.Run(() =>
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                var before = process.WorkingSet64;
                var result = EmptyWorkingSet(process.Handle);
                var after = process.WorkingSet64;
                _logger.LogDebug("Trimmed working set for PID {PID} ({ProcessName}): {BeforeMB:F0} -> {AfterMB:F0} MB",
                    processId, process.ProcessName, before / 1024.0 / 1024.0, after / 1024.0 / 1024.0);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to trim working set for PID {PID}", processId);
                return false;
            }
        });
    }

    private Task<long> TrimWorkingSetsAsync()
    {
        return Task.Run(() =>
        {
            long totalTrimmed = 0;
            var systemProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Idle", "System", "Registry", "smss", "csrss", "wininit",
                "services", "lsass", "winlogon", "dwm", "svchost",
                "fontdrvinit", "sihost", "taskhostw", "explorer", "searchhost",
                "shellexperiencehost", "runtimebroker", "csrss", "winlogon",
                "GTA5Optimizer", "GTA5Optimizer.UI" // Don't trim ourselves
            };

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (systemProcesses.Contains(process.ProcessName))
                        continue;

                    if (process.WorkingSet64 < 10 * 1024 * 1024) // Skip processes using < 10MB
                        continue;

                    var before = process.WorkingSet64;
                    if (EmptyWorkingSet(process.Handle))
                    {
                        try
                        {
                            process.Refresh();
                            var after = process.WorkingSet64;
                            if (before > after)
                                totalTrimmed += (before - after);
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Could not trim process {ProcessName} (PID {PID})", process.ProcessName, process.Id);
                }
            }

            _logger.LogInformation("Working sets trimmed: {TrimmedMB:F0} MB", totalTrimmed / 1024.0 / 1024.0);
            return totalTrimmed;
        });
    }

    private static long GetAvailablePhysicalMemory()
    {
        var memStatus = new MEMORYSTATUSEX();
        return GlobalMemoryStatusEx(memStatus) ? (long)memStatus.ullAvailPhys : 0;
    }

    private static long GetStandbyMemorySize()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT StandbyCacheNormalPrioritySize, StandbyCacheReserveSize FROM Win32_PerfFormattedData_PerfOS_Memory");
            foreach (System.Management.ManagementObject? mo in searcher.Get())
            {
                var normal = Convert.ToInt64(mo["StandbyCacheNormalPrioritySize"]);
                var reserve = Convert.ToInt64(mo["StandbyCacheReserveSize"]);
                return normal + reserve;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get standby memory size: {ex.Message}");
        }
        return 0;
    }
}
