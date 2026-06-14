using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Optimization;
using Microsoft.Extensions.Logging;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Менеджер профилей оптимизации
/// </summary>
public class ProfileManager : IProfileManager
{
    private readonly ILogger<ProfileManager> _logger;
    private ProfileConfig _activeProfile = new();

    public ProfileManager(ILogger<ProfileManager> logger)
    {
        _logger = logger;
        _activeProfile = ProfileConfig.GetDefaultProfile(OptimizationProfile.MaximumFPS);
    }

    public async Task<ProfileConfig> GetActiveProfileAsync()
    {
        return await Task.FromResult(_activeProfile);
    }

    public async Task<bool> ApplyProfileAsync(OptimizationProfile profile)
    {
        try
        {
            _logger.LogInformation($"Применение профиля: {profile}");
            _activeProfile = ProfileConfig.GetDefaultProfile(OptimizationProfile.MaximumFPS);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при применении профиля {profile}");
            return false;
        }
    }

    public async Task<List<ProfileConfig>> GetAvailableProfilesAsync()
    {
        return await Task.FromResult(new List<ProfileConfig>
        {
            ProfileConfig.GetDefaultProfile(OptimizationProfile.MaximumFPS)
        });
    }
}