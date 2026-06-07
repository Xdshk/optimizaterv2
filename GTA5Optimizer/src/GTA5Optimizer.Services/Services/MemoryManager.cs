using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Optimization;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Менеджер памяти для оптимизации RAM
/// </summary>
public class MemoryManager : IMemoryManager
{
    private readonly ILogger<MemoryManager> _logger;

    public MemoryManager(ILogger<MemoryManager> logger)
    {
        _logger = logger;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

    [DllImport("psapi.dll")]
    private static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS counters, uint size);

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_MEMORY_COUNTERS
    {
        public uint cb;
        public uint PageFaultCount;
        public UIntPtr PeakWorkingSetSize;
        public UIntPtr WorkingSetSize;
        public UIntPtr QuotaPeakPagedPoolUsage;
        public UIntPtr QuotaPagedPoolUsage;
        public UIntPtr QuotaPeakNonPagedPoolUsage;
        public UIntPtr QuotaNonPagedPoolUsage;
        public UIntPtr PeakPagefileUsage;
        public UIntPtr PagefileUsage;
        public UIntPtr PeakCommittedCharge;
        public UIntPtr CommittedCharge;
        public UIntPtr CommitChargeLimit;
        public UIntPtr CommittedChargePeak;
    }

    public async Task<MemoryOptimizationResult> OptimizeMemoryAsync()
    {
        var result = new MemoryOptimizationResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var initialAvailable = await GetAvailableMemoryAsync();

            await ClearStandbyMemoryAsync();
            await TrimWorkingSetsAsync();

            stopwatch.Stop();

            var finalAvailable = await GetAvailableMemoryAsync();

            result.Success = true;
            result.MemoryFreedBytes = Math.Max(0, finalAvailable - initialAvailable);
            result.Duration = stopwatch.Elapsed;
            result.Details = $"Освобождено {result.MemoryFreedBytes / 1024.0 / 1024.0:F1} MB";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при оптимизации памяти");
            result.Success = false;
            result.Exception = ex;
        }

        return result;
    }

    public Task<long> GetAvailableMemoryAsync()
    {
        return Task.Run(() =>
        {
            var memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                return (long)memStatus.ullAvailPhys;
            }
            return 0L;
        });
    }

    [DllImport("kernel32.dll", SetLastError = true)]
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

    public Task<long> GetStandbyMemoryAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT StandbyListSize FROM Win32_PerfFormattedData_PerfOS_Memory");
                foreach (System.Management.ManagementObject? mo in searcher.Get())
                {
                    return Convert.ToInt64(mo["StandbyListSize"]) * 1024;
                }
            }
            catch { }
            return 0L;
        });
    }

    public Task<bool> ClearStandbyMemoryAsync()
    {
        try
        {
            var handle = Process.GetCurrentProcess().Handle;
            return Task.FromResult(EmptyWorkingSet(handle));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке standby памяти");
            return Task.FromResult(false);
        }
    }

    public Task<bool> TrimWorkingSetAsync(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return Task.FromResult(EmptyWorkingSet(process.Handle));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private Task TrimWorkingSetsAsync()
    {
        return Task.Run(() =>
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.ProcessName.Equals("Idle", StringComparison.OrdinalIgnoreCase) ||
                        proc.ProcessName.Equals("System", StringComparison.OrdinalIgnoreCase))
                        continue;

                    EmptyWorkingSet(proc.Handle);
                }
                catch { }
            }
        });
    }
}
