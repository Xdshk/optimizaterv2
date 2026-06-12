using GTA5Optimizer.Models.Optimization;

namespace GTA5Optimizer.Core.Interfaces;

public interface IRegistryManager
{
    Task<bool> CreateRestorePointAsync(string description);
    Task<bool> BackupRegistryKeyAsync(string keyPath);
    Task<bool> RestoreRegistryKeyAsync(string keyPath);
    Task<List<string>> GetBackedUpKeysAsync();
    Task<bool> DeleteBackupAsync(string keyPath);
    Task<T?> ReadRegistryValueAsync<T>(string keyPath, string valueName);
    Task<bool> WriteRegistryValueAsync(string keyPath, string valueName, object value);
}
