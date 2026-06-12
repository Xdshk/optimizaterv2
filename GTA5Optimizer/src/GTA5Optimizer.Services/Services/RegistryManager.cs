using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Optimization;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GTA5Optimizer.Services.Services;

public sealed class RegistryManager : IRegistryManager
{
    private readonly ILogger<RegistryManager> _logger;
    private readonly string _backupPath;
    private readonly Dictionary<string, string> _backupMapping = new();
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    public RegistryManager(ILogger<RegistryManager> logger)
    {
        _logger = logger;
        _backupPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GTA5Optimizer", "RegistryBackups");

        Directory.CreateDirectory(_backupPath);
        LoadExistingBackups();
    }

    private void LoadExistingBackups()
    {
        try
        {
            var manifestPath = Path.Combine(_backupPath, "manifest.json");
            if (File.Exists(manifestPath))
            {
                var json = File.ReadAllText(manifestPath);
                var entries = System.Text.Json.JsonSerializer.Deserialize<List<BackupManifestEntry>>(json);
                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        if (File.Exists(entry.BackupFilePath))
                        {
                            _backupMapping[entry.KeyPath] = entry.BackupFilePath;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load backup manifest");
        }
    }

    private async Task SaveManifestAsync()
    {
        try
        {
            var manifestPath = Path.Combine(_backupPath, "manifest.json");
            var entries = _backupMapping.Select(kvp => new BackupManifestEntry
            {
                KeyPath = kvp.Key,
                BackupFilePath = kvp.Value,
                Timestamp = File.GetLastWriteTime(kvp.Value)
            }).ToList();
            var json = System.Text.Json.JsonSerializer.Serialize(entries, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save backup manifest");
        }
    }

    public Task<bool> CreateRestorePointAsync(string description)
    {
        try
        {
            _logger.LogInformation("Creating system restore point: {Description}", description);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"Checkpoint-Computer -Description '{description.Replace("'", "''")}' -RestorePointType 'MODIFY_SETTINGS'\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                Verb = "runas"
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                process.WaitForExit();
                var success = process.ExitCode == 0;
                if (success)
                    _logger.LogInformation("System restore point created successfully");
                else
                    _logger.LogWarning("System restore point creation failed with exit code {ExitCode}", process.ExitCode);
                return Task.FromResult(success);
            }

            _logger.LogWarning("Failed to start restore point creation process");
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating system restore point");
            return Task.FromResult(false);
        }
    }

    public async Task<bool> BackupRegistryKeyAsync(string keyPath)
    {
        if (string.IsNullOrWhiteSpace(keyPath))
        {
            _logger.LogWarning("BackupRegistryKeyAsync called with empty keyPath");
            return false;
        }

        await _ioLock.WaitAsync();
        try
        {
            _logger.LogInformation("Backing up registry key: {KeyPath}", keyPath);

            // Validate key exists
            if (!RegistryKeyExists(keyPath))
            {
                _logger.LogWarning("Registry key does not exist: {KeyPath}", keyPath);
                return false;
            }

            // Create a safe filename from the key path
            var safeName = SanitizeKeyPath(keyPath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFile = Path.Combine(_backupPath, $"{safeName}_{timestamp}.reg");

            var psi = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"export \"{keyPath}\" \"{backupFile}\" /y",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogError("Failed to start reg.exe for export");
                return false;
            }

            process.WaitForExit();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError("reg.exe export failed for {KeyPath}: {Error}", keyPath, error);
                return false;
            }

            if (!File.Exists(backupFile))
            {
                _logger.LogError("Backup file was not created: {BackupFile}", backupFile);
                return false;
            }

            // Update mapping: keyPath -> latest backup file
            _backupMapping[keyPath] = backupFile;
            await SaveManifestAsync();

            _logger.LogInformation("Registry key backed up successfully: {KeyPath} -> {BackupFile}", keyPath, backupFile);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error backing up registry key: {KeyPath}", keyPath);
            return false;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<bool> RestoreRegistryKeyAsync(string keyPath)
    {
        if (string.IsNullOrWhiteSpace(keyPath))
        {
            _logger.LogWarning("RestoreRegistryKeyAsync called with empty keyPath");
            return false;
        }

        await _ioLock.WaitAsync();
        try
        {
            _logger.LogInformation("Restoring registry key: {KeyPath}", keyPath);

            // Find the backup for this specific key
            if (!_backupMapping.TryGetValue(keyPath, out var backupFile) || !File.Exists(backupFile))
            {
                // Try to find any backup for this key pattern
                var safeName = SanitizeKeyPath(keyPath);
                var candidates = Directory.GetFiles(_backupPath, $"{safeName}_*.reg");
                if (candidates.Length == 0)
                {
                    _logger.LogError("No backup found for registry key: {KeyPath}", keyPath);
                    return false;
                }
                backupFile = candidates.OrderByDescending(f => File.GetLastWriteTime(f)).First();
            }

            _logger.LogInformation("Using backup file: {BackupFile}", backupFile);

            // Validate backup file content
            var content = await File.ReadAllTextAsync(backupFile);
            if (string.IsNullOrWhiteSpace(content) || !content.Contains("Windows Registry Editor"))
            {
                _logger.LogError("Backup file is not a valid .reg file: {BackupFile}", backupFile);
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"import \"{backupFile}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogError("Failed to start reg.exe for import");
                return false;
            }

            process.WaitForExit();
            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError("reg.exe import failed for {KeyPath}: {Error}", keyPath, error);
                return false;
            }

            _logger.LogInformation("Registry key restored successfully: {KeyPath}", keyPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring registry key: {KeyPath}", keyPath);
            return false;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<List<string>> GetBackedUpKeysAsync()
    {
        await _ioLock.WaitAsync();
        try
        {
            return _backupMapping.Keys.ToList();
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<bool> DeleteBackupAsync(string keyPath)
    {
        await _ioLock.WaitAsync();
        try
        {
            if (_backupMapping.TryGetValue(keyPath, out var backupFile) && File.Exists(backupFile))
            {
                File.Delete(backupFile);
                _backupMapping.Remove(keyPath);
                await SaveManifestAsync();
                _logger.LogInformation("Backup deleted for key: {KeyPath}", keyPath);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting backup for key: {KeyPath}", keyPath);
            return false;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public Task<T?> ReadRegistryValueAsync<T>(string keyPath, string valueName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyPath) || string.IsNullOrWhiteSpace(valueName))
                return Task.FromResult<T?>(default);

            using var key = Registry.LocalMachine.OpenSubKey(keyPath, false);
            if (key == null)
            {
                _logger.LogDebug("Registry key not found: {KeyPath}", keyPath);
                return Task.FromResult<T?>(default);
            }

            var value = key.GetValue(valueName);
            if (value == null)
            {
                _logger.LogDebug("Registry value not found: {KeyPath}\\{ValueName}", keyPath, valueName);
                return Task.FromResult<T?>(default);
            }

            if (value is T typedValue)
                return Task.FromResult<T?>(typedValue);

            try
            {
                var converted = (T?)Convert.ChangeType(value, typeof(T));
                return Task.FromResult(converted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cannot convert registry value {KeyPath}\\{ValueName} to type {Type}",
                    keyPath, valueName, typeof(T).Name);
                return Task.FromResult<T?>(default);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading registry value: {KeyPath}\\{ValueName}", keyPath, valueName);
            return Task.FromResult<T?>(default);
        }
    }

    public async Task<bool> WriteRegistryValueAsync(string keyPath, string valueName, object value)
    {
        if (string.IsNullOrWhiteSpace(keyPath) || string.IsNullOrWhiteSpace(valueName))
        {
            _logger.LogWarning("WriteRegistryKeyAsync called with empty parameters");
            return false;
        }

        try
        {
            // Backup before writing
            var backupOk = await BackupRegistryKeyAsync(keyPath);
            if (!backupOk)
            {
                _logger.LogWarning("Failed to backup registry key before writing: {KeyPath}", keyPath);
            }

            using var key = Registry.LocalMachine.CreateSubKey(keyPath);
            if (key == null)
            {
                _logger.LogError("Failed to open/create registry key: {KeyPath}", keyPath);
                return false;
            }

            key.SetValue(valueName, value);
            _logger.LogInformation("Registry value written: {KeyPath}\\{ValueName}", keyPath, valueName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing registry value: {KeyPath}\\{ValueName}", keyPath, valueName);
            return false;
        }
    }

    public async Task<OptimizationResult> ApplyGameOptimizationsAsync()
    {
        var result = new OptimizationResult
        {
            Category = OptimizationCategory.RegistryTweaks,
            Timestamp = DateTime.Now
        };

        var stopwatch = Stopwatch.StartNew();
        var errors = new List<string>();

        try
        {
            // Disable GameDVR
            if (!await WriteRegistryValueAsync(
                @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0))
                errors.Add("Failed to disable GameDVR policy");

            if (!await WriteRegistryValueAsync(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "GameDVR_Enabled", 0))
                errors.Add("Failed to disable GameDVR");

            // Network optimizations
            if (!await WriteRegistryValueAsync(
                @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "TcpAckFrequency", 1))
                errors.Add("Failed to set TcpAckFrequency");

            if (!await WriteRegistryValueAsync(
                @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "TCPNoDelay", 1))
                errors.Add("Failed to set TCPNoDelay");

            // Telemetry
            if (!await WriteRegistryValueAsync(
                @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0))
                errors.Add("Failed to disable telemetry");

            result.Success = errors.Count == 0;
            result.Message = errors.Count == 0
                ? "Registry optimizations applied successfully"
                : $"Registry optimizations applied with {errors.Count} warning(s)";
            result.Details = string.Join("; ", errors);

            _logger.LogInformation("Registry optimizations completed. Success: {Success}, Errors: {Errors}",
                result.Success, errors.Count);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Registry optimization error: {ex.Message}";
            result.Exception = ex;
            _logger.LogError(ex, "Critical error during registry optimization");
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    private static bool RegistryKeyExists(string keyPath)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, false);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    private static string SanitizeKeyPath(string keyPath)
    {
        // Convert registry path to safe filename
        var sanitized = Regex.Replace(keyPath, @"[\\/:*?""<>|]", "_");
        return sanitized.TrimStart('_');
    }

    private sealed class BackupManifestEntry
    {
        public string KeyPath { get; set; } = string.Empty;
        public string BackupFilePath { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
