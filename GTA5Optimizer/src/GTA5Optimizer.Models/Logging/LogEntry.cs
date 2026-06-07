namespace GTA5Optimizer.Models.Logging;

/// <summary>
/// Запись лога приложения
/// </summary>
public sealed class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? Source { get; set; }
    public int ThreadId { get; set; } = Environment.CurrentManagedThreadId;
    public long ProcessId { get; set; } = Environment.ProcessId;
    public Exception? Exception { get; set; }
    public Dictionary<string, object>? Properties { get; set; }

    public string FormattedMessage => $"[{Timestamp:HH:mm:ss.fff}] [{Level}] [{Category}] {Message}";
    public string ShortTime => Timestamp.ToString("HH:mm:ss");
    public string LevelIcon => Level switch
    {
        LogLevel.Trace => "🔍",
        LogLevel.Debug => "🐛",
        LogLevel.Information => "ℹ️",
        LogLevel.Warning => "⚠️",
        LogLevel.Error => "❌",
        LogLevel.Critical => "💥",
        _ => "📝"
    };
}

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5
}

public static class LogCategories
{
    public const string System = "SYSTEM";
    public const string Optimization = "OPTIMIZATION";
    public const string Monitoring = "MONITORING";
    public const string Memory = "MEMORY";
    public const string Process = "PROCESS";
    public const string Registry = "REGISTRY";
    public const string Disk = "DISK";
    public const string Network = "NETWORK";
    public const string Game = "GAME";
    public const string Majestic = "MAJESTIC";
    public const string Profile = "PROFILE";
    public const string Rollback = "ROLLBACK";
    public const string UI = "UI";
    public const string Security = "SECURITY";
}