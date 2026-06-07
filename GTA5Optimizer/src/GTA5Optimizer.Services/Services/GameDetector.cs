using GTA5Optimizer.Core.Interfaces;
using Enums = GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Обнаружитель игры и Majestic RP
/// </summary>
public class GameDetector : IGameDetector
{
    private readonly ILogger<GameDetector> _logger;

    public GameDetector(ILogger<GameDetector> logger)
    {
        _logger = logger;
    }

    public Task<GameInfo> DetectGameAsync()
    {
        var gameInfo = new GameInfo();

        try
        {
            var gtaProcesses = Process.GetProcessesByName("GTA5");
            if (gtaProcesses.Length > 0)
            {
                gameInfo.IsRunning = true;
                gameInfo.GameProcess = gtaProcesses[0];
                gameInfo.ProcessId = gtaProcesses[0].Id;
                gameInfo.LaunchTime = gtaProcesses[0].StartTime;
            }

            var installPath = FindInstallPath();
            gameInfo.InstallPath = installPath;
            gameInfo.ExecutablePath = Path.Combine(gameInfo.InstallPath, "GTA5.exe");

            if (!string.IsNullOrEmpty(gameInfo.InstallPath))
            {
                gameInfo.DriveType = GetDriveType(gameInfo.InstallPath);
            }

            gameInfo.Version = DetectGameVersion();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обнаружении GTA V");
        }

        return Task.FromResult(gameInfo);
    }

    public Task<MajesticInfo> DetectMajesticRPAsync()
    {
        var majesticInfo = new MajesticInfo();

        try
        {
            var majesticProcesses = Process.GetProcessesByName("MajesticRP");
            if (majesticProcesses.Length > 0)
            {
                majesticInfo.IsRunning = true;
                majesticInfo.LauncherProcess = majesticProcesses[0];
                majesticInfo.ProcessId = majesticProcesses[0].Id;
            }

            majesticInfo.InstallPath = FindMajesticPath();
            majesticInfo.ExecutablePath = Path.Combine(majesticInfo.InstallPath, "MajesticRP.exe");

            if (!string.IsNullOrEmpty(majesticInfo.InstallPath))
            {
                majesticInfo.DriveType = GetDriveType(majesticInfo.InstallPath);
            }

            if (!string.IsNullOrEmpty(majesticInfo.InstallPath))
            {
                majesticInfo.CachePath = Path.Combine(majesticInfo.InstallPath, "cache");
                majesticInfo.LogsPath = Path.Combine(majesticInfo.InstallPath, "logs");

                if (Directory.Exists(majesticInfo.CachePath))
                {
                    majesticInfo.CacheSize = Directory.GetFiles(majesticInfo.CachePath, "*", SearchOption.AllDirectories)
                        .Sum(f => new FileInfo(f).Length);
                }
            }

            if (majesticInfo.IsRunning)
            {
                var gtaProcess = Process.GetProcessesByName("GTA5").FirstOrDefault();
                if (gtaProcess != null)
                {
                    majesticInfo.RelatedProcesses.Add(gtaProcess);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обнаружении Majestic RP");
        }

        return Task.FromResult(majesticInfo);
    }

    public Task<bool> IsGameRunningAsync()
    {
        try
        {
            var processes = Process.GetProcessesByName("GTA5");
            return Task.FromResult(processes.Length > 0);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static string FindInstallPath()
    {
        try
        {
            // Steam registry path
            using var steamKey = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 57953");
            if (steamKey?.GetValue("InstallLocation") is string steamPath)
                return steamPath;

            // Rockstar Games Launcher registry path
            using var rockstarKey = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Rockstar Games Launcher");

            // Check common paths
            var commonPaths = new[]
            {
                @"C:\Program Files\Rockstar Games\GTA V",
                @"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V",
                @"D:\Games\GTA V",
                @"D:\Steam\steamapps\common\Grand Theft Auto V"
            };

            foreach (var candidatePath in commonPaths)
            {
                if (Directory.Exists(candidatePath) && File.Exists(Path.Combine(candidatePath, "GTA5.exe")))
                    return candidatePath;
            }
        }
        catch (Exception ex)
        {
            // Logged by caller
        }

        return string.Empty;
    }

    private static string FindMajesticPath()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MajesticRP");
            if (key?.GetValue("InstallLocation") is string path)
                return path;

            var commonPaths = new[]
            {
                @"C:\Program Files\MajesticRP",
                @"C:\Program Files (x86)\MajesticRP",
                @"%USERPROFILE%\AppData\Local\MajesticRP"
            };

            foreach (var candidatePath in commonPaths)
            {
                var expandedPath = Environment.ExpandEnvironmentVariables(candidatePath);
                if (Directory.Exists(expandedPath) && File.Exists(Path.Combine(expandedPath, "MajesticRP.exe")))
                    return expandedPath;
            }
        }
        catch { }

        return string.Empty;
    }

    private static Enums.DriveType GetDriveType(string path)
    {
        try
        {
            var drive = new System.IO.DriveInfo(Path.GetPathRoot(path)!);
            return drive.DriveType == System.IO.DriveType.Fixed ? Enums.DriveType.SSD : Enums.DriveType.HDD;
        }
        catch
        {
            return Enums.DriveType.HDD;
        }
    }

    private static string DetectGameVersion()
    {
        try
        {
            var searcher = new ManagementObjectSearcher(
                "SELECT Version FROM Win32_FileInfo WHERE FileName = 'GTA5.exe'");
            foreach (ManagementObject? mo in searcher.Get())
            {
                return mo["Version"]?.ToString() ?? "1.0.0";
            }
        }
        catch { }

        return "Unknown";
    }
}
