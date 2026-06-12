using GTA5Optimizer.Models.Optimization;

namespace GTA5Optimizer.Core.Interfaces;

public interface IMemoryManager
{
    Task<MemoryOptimizationResult> OptimizeMemoryAsync();
    Task<long> GetAvailableMemoryAsync();
    Task<long> GetStandbyMemoryAsync();
    Task<long> ClearStandbyMemoryAsync();
    Task<bool> TrimWorkingSetAsync(int processId);
}
