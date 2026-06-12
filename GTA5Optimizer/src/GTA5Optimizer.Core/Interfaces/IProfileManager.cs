using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Optimization;

namespace GTA5Optimizer.Core.Interfaces;

public interface IProfileManager
{
    Task<ProfileConfig> GetActiveProfileAsync();
    Task<bool> ApplyProfileAsync(OptimizationProfile profile);
    Task<List<ProfileConfig>> GetAvailableProfilesAsync();
}
