using GTA5Optimizer.Models.Settings;

namespace GTA5Optimizer.Core.Interfaces;

/// <summary>
/// Интерфейс сервиса настроек
/// </summary>
public interface ISettingsService
{
    Task<AppSettings> GetSettingsAsync();
    Task<bool> SaveSettingsAsync(AppSettings settings);
}