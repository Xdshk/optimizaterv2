using GTA5Optimizer.Models.Enums;

namespace GTA5Optimizer.Core.Interfaces;

public interface ISystemOptimizer
{
    Task<bool> ApplyOptimizationsAsync(OptimizationProfile profile);
    Task<bool> RestoreDefaultsAsync();
    Task<bool> EnableHighPerformanceModeAsync();
    Task<bool> DisableHighPerformanceModeAsync();
}
