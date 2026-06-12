using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Enums;
using GTA5Optimizer.Models.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Diagnostics;
using System.Management;
using System.Text.RegularExpressions;

namespace GTA5Optimizer.Services.Services;

public sealed class GameDetector : IGameDetector
{
    private readonly ILogger<GameDetector> logger;
    private GameInfo? _cachedGameInfo;
    private MajesticInfo? _cachedMajesticInfo;
    private DateTime _lastGameCheck = DateTime.MinValue;
    private DateTime _lastMajesticCheck = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    public GameDetector(ILogger<GameDetector> logger)
    {
        this.logger = logger;
    }

    public async Task<GameInfo> DetectGameAsync()
    {
        if (_cachedGameInfo != null && DateTime.UtcNow - _lastGameCheck < CacheDuration)
            return _cachedGameInfo;

        var gameInfo = new GameInfo();

        try
        {
            // Check if GTA5 is running
            var gtaProcesses = Process.GetProcessesByName("GTA5");
            if (gtaProcesses.Length > 0)
            {
                gameInfo.IsRunning = true;
                gameInfo.GameProcess = gtaProcesses[0];
                gameInfo.ProcessId = gtaProcesses[0].Id;
                try { gameInfo.LaunchTime = gtaProcesses[0].StartTime; }
                catch (Exception ex) { logger.LogDebug(ex, "Could not get GTA5 start time"); }
            }

            // Find install path
            var installPath = await FindGtaVInstallPathAsync();
            gameInfo.InstallPath = installPath;
            gameInfo.ExecutablePath = Path.Combine(installPath, "GTA5.exe");

            // Detect drive type
            if (!string.IsNullOrEmpty(installPath))
            {
                gameInfo.DriveType = await GetDriveTypeAsync(installPath);
                try
                {
                    var drive = new System.IO.DriveInfo(Path.GetPathRoot(installPath)!);
                    gameInfo.DriveFreeSpace = drive.AvailableFreeSpace;
                    gameInfo.DriveTotalSpace = drive.TotalSize;
                }
                catch (Exception ex) { logger.LogDebug(ex, "Could not get drive info"); }
            }

            // Detect version
            gameInfo.Version = DetectGameVersion(installPath);

            // Detect launcher
            gameInfo.LauncherType = DetectLauncher();

            _cachedGameInfo = gameInfo;
            _lastGameCheck = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error detecting GTA V");
        }

        return gameInfo;
    }

    public async Task<MajesticInfo> DetectMajesticRPAsync()
    {
        if (_cachedMajesticInfo != null && DateTime.UtcNow - _lastMajesticCheck < CacheDuration)
            return _cachedMajesticInfo;

        var majesticInfo = new MajesticInfo();

        try
        {
            var majesticProcesses = Process.GetProcessesByName("MajesticRP");
            if (majesticProcesses.Length > 0)
            {
                majesticInfo.IsRunning = true;
                majesticInfo.LauncherProcess = majesticProcesses[0];
                majesticInfo.ProcessId = majesticProcesses[0].Id;
                try { majesticInfo.LastLaunchTime = majesticProcesses[0].StartTime; }
                catch (Exception ex) { logger.LogDebug(ex, "Could not get Majestic start time"); }
            }

            var installPath = await FindMajesticPathAsync();
            majesticInfo.InstallPath = installPath;
            majesticInfo.ExecutablePath = Path.Combine(installPath, "MajesticRP.exe");

            if (!string.IsNullOrEmpty(installPath))
            {
                majesticInfo.DriveType = await GetDriveTypeAsync(installPath);

                majesticInfo.CachePath = Path.Combine(installPath, "cache");
                majesticInfo.LogsPath = Path.Combine(installPath, "logs");

                if (Directory.Exists(majesticInfo.CachePath))
                {
                    try
                    {
                        majesticInfo.CacheSize = Directory.GetFiles(majesticInfo.CachePath, "*", SearchOption.AllDirectories)
                            .Sum(f => new FileInfo(f).Length);
                    }
                    catch (Exception ex) { logger.LogWarning(ex, "Could not calculate Majestic cache size"); }
                }
            }

            _cachedMajesticInfo = majesticInfo;
            _lastMajesticCheck = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error detecting Majestic RP");
        }

        return majesticInfo;
    }

    public Task<bool> IsGameRunningAsync()
    {
        try
        {
            var processes = Process.GetProcessesByName("GTA5");
            return Task.FromResult(processes.Length > 0);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Error checking if game is running");
            return Task.FromResult(false);
        }
    }

    private async Task<string> FindGtaVInstallPathAsync()
    {
        // 1. Steam - registry
        var steamPath = ReadRegistryValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 271590", "InstallLocation");
        if (!string.IsNullOrEmpty(steamPath) && IsValidGtaVPath(steamPath))
            return steamPath;

        // 2. Steam - libraryfolders.vdf scanning
        var steamLibraryPath = await ScanSteamLibrariesAsync();
        if (!string.IsNullOrEmpty(steamLibraryPath) && IsValidGtaVPath(steamLibraryPath))
            return steamLibraryPath;

        // 3. Rockstar Games Launcher
        var rockstarPath = ReadRegistryValue(@"SOFTWARE\Rockstar Games\Grand Theft Auto V", "InstallFolder");
        if (!string.IsNullOrEmpty(rockstarPath) && IsValidGtaVPath(rockstarPath))
            return rockstarPath;

        rockstarPath = ReadRegistryValue(@"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V", "InstallFolder");
        if (!string.IsNullOrEmpty(rockstarPath) && IsValidGtaVPath(rockstarPath))
            return rockstarPath;

        // 4. Epic Games Launcher
        var epicPath = await ScanEpicGamesAsync();
        if (!string.IsNullOrEmpty(epicPath) && IsValidGtaVPath(epicPath))
            return epicPath;

        // 5. Xbox Game Pass / Microsoft Store
        var windowsAppsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages");
        if (Directory.Exists(windowsAppsPath))
        {
            foreach (var dir in Directory.GetDirectories(windowsAppsPath, "RockstarGames.GTA*"))
            {
                var installDir = Path.Combine(dir,"LocalCache","Local","Rockstar Games","GTA V");
                if (IsValidGtaVPath(installDir) || IsValidGtaVPath(Path.Combine(dir, "LocalCache")))
                {
                    var exePath = FindExeInDirectory(dir, "GTA5.exe");
                    if (exePath != null) return Path.GetDirectoryName(exePath)!;
                }
            }
        }

        // 6. WMI search
        var wmiPath = SearchWmiForGtaV();
        if (!string.IsNullOrEmpty(wmiPath))
            return wmiPath;

        // 7. Search all drives for GTA5.exe
        var driveSearch = await SearchAllDrivesAsync("GTA5.exe");
        if (!string.IsNullOrEmpty(driveSearch))
            return Path.GetDirectoryName(driveSearch)!;

        logger.LogWarning("Could not find GTA V installation");
        return string.Empty;
    }

    private async Task<string> FindMajesticPathAsync()
    {
        // 1. Registry
        var regPath = ReadRegistryValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MajesticRP", "InstallLocation");
        if (!string.IsNullOrEmpty(regPath) && File.Exists(Path.Combine(regPath, "MajesticRP.exe")))
            return regPath;

        // 2. Common paths
        var searchPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MajesticRP"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MajesticRP"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MajesticRP"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MajesticRP"),
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(Path.Combine(path, "MajesticRP.exe")))
                return path;
        }

        // 3. Search all drives
        var driveSearch = await SearchAllDrivesAsync("MajesticRP.exe");
        if (!string.IsNullOrEmpty(driveSearch))
            return Path.GetDirectoryName(driveSearch)!;

        logger.LogWarning("Could not find Majestic RP installation");
        return string.Empty;
    }

    private async Task<string?> ScanSteamLibrariesAsync()
    {
        try
        {
            // Find Steam install path
            var steamExePath = ReadRegistryValue(@"SOFTWARE\Valve\Steam", "InstallPath")
                ?? ReadRegistryValue(@"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");

            if (string.IsNullOrEmpty(steamExePath))
                return null;

            // Read libraryfolders.vdf
            var libraryVdfPath = Path.Combine(steamExePath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryVdfPath))
                return null;

            var vdfContent = await File.ReadAllTextAsync(libraryVdfPath);
            // Parse paths from VDF format: "path"    "D:\\SteamLibrary"
            var pathMatches = Regex.Matches(vdfContent, @"""path""\s+""([^""]+)""");
            foreach (Match match in pathMatches)
            {
                var libraryPath = match.Groups[1].Value.Replace("\\\\", "\\");
                var gtaPath = Path.Combine(libraryPath, "steamapps", "common", "Grand Theft Auto V");
                if (IsValidGtaVPath(gtaPath))
                    return gtaPath;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to scan Steam libraries");
        }
        return null;
    }

    private async Task<string?> ScanEpicGamesAsync()
    {
        try
        {
            var epicManifestPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests");

            if (!Directory.Exists(epicManifestPath))
                return null;

            foreach (var manifestFile in Directory.GetFiles(epicManifestPath, "*.item"))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(manifestFile);
                    if (content.Contains("Grand Theft Auto V") || content.Contains("GTA5") || content.Contains("gta-v"))
                    {
                        var installMatch = Regex.Match(content, @"""InstallLocation"":\s*""([^""]+)""");
                        if (installMatch.Success)
                        {
                            var path = installMatch.Groups[1].Value.Replace("\\\\", "\\");
                            if (IsValidGtaVPath(path))
                                return path;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to read Epic Games manifest file");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to scan Epic Games library");
        }
        return null;
    }

    private string? SearchWmiForGtaV()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_Product WHERE Name LIKE '%Grand Theft Auto V%'");
            foreach (ManagementObject? mo in searcher.Get())
            {
                var installLocation = mo["InstallLocation"]?.ToString();
                if (!string.IsNullOrEmpty(installLocation) && IsValidGtaVPath(installLocation))
                    return installLocation;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "WMI search for GTA V failed");
        }
        return null;
    }

    private async Task<string?> SearchAllDrivesAsync(string fileName)
    {
        var fixedDrives = System.IO.DriveInfo.GetDrives()
            .Where(d => d.DriveType == System.IO.DriveType.Fixed && d.IsReady)
            .ToList();

        var commonSubdirs = new[] { "Games", "SteamLibrary", "Program Files", "Program Files (x86)", "" };

        foreach (var drive in fixedDrives)
        {
            var root = drive.RootDirectory.FullName;

            foreach (var subdir in commonSubdirs)
            {
                var searchRoot = string.IsNullOrEmpty(subdir) ? root : Path.Combine(root, subdir);
                if (!Directory.Exists(searchRoot)) continue;

                try
                {
                    var files = await Task.Run(() =>
                        Directory.GetFiles(searchRoot, fileName, SearchOption.AllDirectories));
                    if (files.Length > 0)
                        return files[0];
                }
                catch (UnauthorizedAccessException) { }
                catch (PathTooLongException) { }
                catch (Exception ex) { logger.LogTrace(ex, "Search error in {Path}", searchRoot); }
            }
        }
        return null;
    }

    /// <summary>
    /// Determines the actual type of drive (HDD, SATA SSD, NVMe SSD) using WMI Win32_DiskDrive.
    /// </summary>
    async Task<GTA5Optimizer.Models.Enums.DriveType> GetDriveTypeAsync(string path)
    {
        try
        {
            var driveLetter = Path.GetPathRoot(path)?.TrimEnd('\\');
            if (string.IsNullOrEmpty(driveLetter))
                return GTA5Optimizer.Models.Enums.DriveType.Unknown;

            // Map volume to physical disk
            await using var volumeToPartitionSearcher = new ManagementObjectSearcher(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}'}}" +
                "WHERE_assocClass=Win32_LogicalDiskToPartition");

            string? deviceId = null;
            foreach (ManagementObject? obj in volumeToPartitionSearcher.Get())
            {
                var deviceIdStr = obj["DeviceID"]?.ToString();
                if (!string.IsNullOrEmpty(deviceIdStr))
                {
                    // Get the disk drive from partition
                    await using var diskSearcher = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{deviceIdStr}'}}" +
                        "WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                    foreach (ManagementObject? diskObj in diskSearcher.Get())
                    {
                        var mediaType = diskObj["MediaType"]?.ToString() ?? "";
                        var model = diskObj["Model"]?.ToString() ?? "";

                        // NVMe detection
                        if (model.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ||
                            mediaType.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
                        {
                            logger.LogDebug("Drive {DriveLetter} detected as NVMe SSD ({Model})", driveLetter, model);
                            return GTA5Optimizer.Models.Enums.DriveType.NVMe;
                        }

                        // SSD detection via media type
                        if (mediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                            mediaType.Contains("Solid State", StringComparison.OrdinalIgnoreCase))
                        {
                            logger.LogDebug("Drive {DriveLetter} detected as SATA SSD ({Model})", driveLetter, model);
                            return GTA5Optimizer.Models.Enums.DriveType.SSD;
                        }

                        // SSD detection via rotation rate (0 or null for SSD)
                        try
                        {
                            using var detailSearcher = new ManagementObjectSearcher(
                                "SELECT * FROM MSFT_PhysicalDisk WHERE DeviceId=" + diskObj["Index"]);
                            foreach (ManagementObject? detail in detailSearcher.Get())
                            {
                                var rotationRate = detail["RotationRate"];
                                if (rotationRate != null)
                                {
                                    var rate = Convert.ToUInt32(rotationRate);
                                    if (rate == 0 || rate == 1)
                                    {
                                        logger.LogDebug("Drive {DriveLetter} detected as SSD (rotation rate: {Rate})", driveLetter, rate);
                                        return GTA5Optimizer.Models.Enums.DriveType.SSD;
                                    }
                                }

                                var mediaKind = detail["MediaType"]?.ToString();
                                if (mediaKind == "3" || mediaKind == "4") // SSD or SCM
                                    return GTA5Optimizer.Models.Enums.DriveType.SSD;
                            }
                        }
                                catch (Exception ex)
                        {
                            logger.LogTrace(ex, "Failed to get physical disk details");
                        }

                        // Spindle speed check via Win32_DiskDrive
                        var spindleSpeed = diskObj["DefaultBlockSize"];
                        if (mediaType.Contains("Fixed", StringComparison.OrdinalIgnoreCase) ||
                            mediaType.Contains("Removable", StringComparison.OrdinalIgnoreCase))
                        {
                            // Could be either — use heuristics
                            if (model.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                                model.Contains("Samsung", StringComparison.OrdinalIgnoreCase) ||
                                model.Contains("Kingston", StringComparison.OrdinalIgnoreCase) ||
                                model.Contains("Crucial", StringComparison.OrdinalIgnoreCase) ||
                                model.Contains("WD Blue", StringComparison.OrdinalIgnoreCase) ||
                                model.Contains("SanDisk", StringComparison.OrdinalIgnoreCase))
                            {
                                logger.LogDebug("Drive {DriveLetter} detected as SSD by model name ({Model})", driveLetter, model);
                                return GTA5Optimizer.Models.Enums.DriveType.SSD;
                            }

                            logger.LogDebug("Drive {DriveLetter} detected as HDD ({Model})", driveLetter, model);
                            return GTA5Optimizer.Models.Enums.DriveType.HDD;
                        }

                        break; // Found the disk, stop
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to determine drive type for {Path}", path);
        }

        return GTA5Optimizer.Models.Enums.DriveType.Unknown;
    }

    private static bool IsValidGtaVPath(string path)
    {
        return !string.IsNullOrEmpty(path) && File.Exists(Path.Combine(path, "GTA5.exe"));
    }

    private string? ReadRegistryValue(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath, false);
            return key?.GetValue(valueName)?.ToString();
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "Failed to read registry HKLM\\{KeyPath}\\{ValueName}", keyPath, valueName);
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, false);
                return key?.GetValue(valueName)?.ToString();
            }
            catch (Exception ex2)
            {
                logger.LogTrace(ex2, "Failed to read registry HKCU\\{KeyPath}\\{ValueName}", keyPath, valueName);
            }
        }
        return null;
    }

    private static string DetectGameVersion(string installPath)
    {
        try
        {
            if (!string.IsNullOrEmpty(installPath))
            {
                var exePath = Path.Combine(installPath, "GTA5.exe");
                if (File.Exists(exePath))
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                    return versionInfo.FileVersion ?? "Unknown";
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not detect game version: {ex.Message}");
        }
        return "Unknown";
    }

    private static GameLauncher DetectLauncher()
    {
        try
        {
            // Check processes
            if (Process.GetProcessesByName("steam").Length > 0)
                return GameLauncher.Steam;
            if (Process.GetProcessesByName("EpicGamesLauncher").Length > 0)
                return GameLauncher.EpicGames;
            if (Process.GetProcessesByName("RockstarService").Length > 0)
                return GameLauncher.RockstarGames;
            if (Process.GetProcessesByName("MajesticRP").Length > 0)
                return GameLauncher.MajesticRP;
        }
        catch { }
        return GameLauncher.Unknown;
    }

    private static string? FindExeInDirectory(string rootDir, string exeName)
    {
        try
        {
            return Directory.GetFiles(rootDir, exeName, SearchOption.AllDirectories).FirstOrDefault();
        }
        catch { }
        return null;
    }
}
