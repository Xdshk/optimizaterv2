using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Logging;
using Microsoft.Extensions.Logging;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Сервис логирования
/// </summary>
public class LoggerService : ILoggerService
{
    private readonly ILogger<LoggerService> _logger;
    private readonly string _logFilePath;
    private readonly object _lock = new();
    private List<LogEntry> _logBuffer = new();
    private const int MAX_BUFFER_SIZE = 1000;

    public LoggerService(ILogger<LoggerService> logger)
    {
        _logger = logger;
        _logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GTA5Optimizer", "logs", "optimization.log");

        Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
    }

    public Task LogAsync(LogEntry entry)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                _logBuffer.Add(entry);

                if (_logBuffer.Count > MAX_BUFFER_SIZE)
                {
                    _logBuffer.RemoveAt(0);
                }

                try
                {
                    File.AppendAllText(_logFilePath, FormatLogEntry(entry) + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при записи лога");
                }
            }
        });
    }

    public Task<List<LogEntry>> GetRecentLogsAsync(int count = 100)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                return _logBuffer.TakeLast(count).ToList();
            }
        });
    }

    public Task ClearLogsAsync()
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                _logBuffer.Clear();
            }

            try
            {
                if (File.Exists(_logFilePath))
                {
                    File.Delete(_logFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке логов");
            }
        });
    }

    private static string FormatLogEntry(LogEntry entry)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] ");
        sb.Append($"[{entry.Level}] ");
        sb.Append($"[{entry.Category}] ");
        sb.Append(entry.Message);

        if (!string.IsNullOrEmpty(entry.Details))
        {
            sb.Append($" | {entry.Details}");
        }

        if (entry.Exception != null)
        {
            sb.Append($" | Exception: {entry.Exception.Message}");
        }

        return sb.ToString();
    }
}
