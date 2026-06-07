using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Optimization;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Diagnostics;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Менеджер работы с реестром
/// </summary>
public class RegistryManager : IRegistryManager
{
    private readonly ILogger<RegistryManager> _logger;
    private readonly string _backupPath;

    public RegistryManager(ILogger<RegistryManager> logger)
    {
        _logger = logger;
        _backupPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GTA5Optimizer", "RegistryBackups");

        Directory.CreateDirectory(_backupPath);
    }

    public Task<bool> CreateRestorePointAsync(string description)
    {
        try
        {
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
                return Task.FromResult(process.ExitCode == 0);
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании точки восстановления");
            return Task.FromResult(false);
        }
    }

    public Task<bool> BackupRegistryKeyAsync(string keyPath)
    {
        try
        {
            var backupFile = Path.Combine(_backupPath, $"{Guid.NewGuid():N}.reg");

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
            if (process != null)
            {
                process.WaitForExit();
                return Task.FromResult(process.ExitCode == 0);
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при создании резервной копии ключа {keyPath}");
            return Task.FromResult(false);
        }
    }

    public Task<bool> RestoreRegistryKeyAsync(string keyPath)
    {
        try
        {
            var backupFiles = Directory.GetFiles(_backupPath, "*.reg");
            if (backupFiles.Length == 0)
                return Task.FromResult(false);

            var latestBackup = backupFiles.OrderByDescending(f => File.GetLastWriteTime(f)).First();

            var psi = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"import \"{latestBackup}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                process.WaitForExit();
                return Task.FromResult(process.ExitCode == 0);
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при восстановлении ключа {keyPath}");
            return Task.FromResult(false);
        }
    }

    public Task<T?> ReadRegistryValueAsync<T>(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, false);
            if (key == null)
                return Task.FromResult<T?>(default);

            var value = key.GetValue(valueName);
            if (value == null)
                return Task.FromResult<T?>(default);

            if (value is T typedValue)
                return Task.FromResult<T?>(typedValue);

            // Try conversion for common types
            try
            {
                var converted = (T?)Convert.ChangeType(value, typeof(T));
                return Task.FromResult(converted);
            }
            catch
            {
                return Task.FromResult<T?>(default);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при чтении значения {valueName} из {keyPath}");
            return Task.FromResult<T?>(default);
        }
    }

    public async Task<bool> WriteRegistryValueAsync(string keyPath, string valueName, object value)
    {
        try
        {
            await BackupRegistryKeyAsync(keyPath);

            using var key = Registry.LocalMachine.CreateSubKey(keyPath);
            if (key == null)
                return false;

            key.SetValue(valueName, value);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при записи значения {valueName} в {keyPath}");
            return false;
        }
    }

    /// <summary>
    /// Оптимизация реестра для игры
    /// </summary>
    public async Task<OptimizationResult> ApplyGameOptimizationsAsync()
    {
        var result = new OptimizationResult
        {
            Category = OptimizationCategory.RegistryTweaks,
            Timestamp = DateTime.Now
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await WriteRegistryValueAsync(
                @"SOFTWARE\Policies\Microsoft\Windows\GameDVR",
                "AllowGameDVR", 0);

            await WriteRegistryValueAsync(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR",
                "GameDVR_Enabled", 0);

            await WriteRegistryValueAsync(
                @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters",
                "TcpAckFrequency", 1);

            await WriteRegistryValueAsync(
                @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters",
                "TCPNoDelay", 1);

            await WriteRegistryValueAsync(
                @"SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                "AllowTelemetry", 0);

            result.Success = true;
            result.Message = "Оптимизация реестра завершена успешно";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Ошибка при оптимизации реестра: {ex.Message}";
            result.Exception = ex;
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }
}
