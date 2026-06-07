using GTA5Optimizer.Models.Enums;
using System.Diagnostics;

namespace GTA5Optimizer.Models.Game;

/// <summary>
/// Информация об установленной GTA V
/// </summary>
public sealed class GameInfo
{
    public string InstallPath { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public Enums.DriveType DriveType { get; set; }
    public long DriveFreeSpace { get; set; }
    public long DriveTotalSpace { get; set; }
    public bool IsRunning { get; set; }
    public Process? GameProcess { get; set; }
    public int ProcessId { get; set; }
    public DateTime? LaunchTime { get; set; }
    public GameLauncher LauncherType { get; set; }
    public string? SocialClubPath { get; set; }
    public Dictionary<string, string> GraphicsSettings { get; set; } = new();

    public double DriveFreeSpaceGB => DriveFreeSpace / 1024.0 / 1024.0 / 1024.0;
    public double DriveTotalSpaceGB => DriveTotalSpace / 1024.0 / 1024.0 / 1024.0;
    public bool IsOnHDD => DriveType == Enums.DriveType.HDD;
    public bool NeedsSSDWarning => IsOnHDD && DriveFreeSpaceGB > 50;
}

public enum GameLauncher
{
    Unknown = 0,
    Steam = 1,
    EpicGames = 2,
    RockstarGames = 3,
    MajesticRP = 4
}