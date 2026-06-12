using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Logging;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace GTA5Optimizer.Services.Services;

public sealed class LoggerService : ILoggerService, IDisposable
{
    private readonly ILogger<LoggerService> _logger;
    private readonly string _logFilePath;
    private readonly ConcurrentQueue<LogEntry> _logBuffer = new();
    private readonly Channel<LogEntry> _logChannel;
    private readonly CancellationTokenSource _cts = new();
    private const int MaxBufferSize = 5000;

    public LoggerService(ILogger<LoggerService> logger)
    {
        _logger = logger;
        _logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GTA5Optimizer", "logs", "optimization.log");

        Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);

        _logChannel = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        // Start background writer
        _ = Task.Run(ProcessLogQueueAsync);
    }

    public async Task LogAsync(LogEntry entry)
    {
        await _logChannel.Writer.WriteAsync(entry, _cts.Token);
    }

    public Task<List<LogEntry>> GetRecentLogsAsync(int count = 100)
    {
        var logs = _logBuffer.TakeLast(count).ToList();
        return Task.FromResult(logs);
    }

    public Task ClearLogsAsync()
    {
        try
        {
            while (_logBuffer.TryDequeue(out _)) { }
            if (File.Exists(_logFilePath))
                File.Delete(_logFilePath);
            _logger.LogInformation("Logs cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear logs");
        }
        return Task.CompletedTask;
    }

    private async Task ProcessLogQueueAsync()
    {
        await foreach (var entry in _logChannel.Reader.ReadAllAsync(_cts.Token))
        {
            try
            {
                // Add to in-memory buffer
                _logBuffer.Enqueue(entry);
                while (_logBuffer.Count > MaxBufferSize)
                    _logBuffer.TryDequeue(out _);

                // Write to file
                var line = FormatLogEntry(entry);
                await File.AppendAllTextAsync(_logFilePath, line + Environment.NewLine, _cts.Token);

                // Also log to Microsoft.Extensions.Logging at appropriate level
                var logLevel = entry.Level switch
                {
                    LogLevel.Trace or LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
                    LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
                    LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
                    LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
                    LogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical,
                    _ => Microsoft.Extensions.Logging.LogLevel.Information
                };
                _logger.Log(logLevel, "[{Category}] {Message}", entry.Category, entry.Message);
            }
            catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process log entry");
            }
        }
    }

    private static string FormatLogEntry(LogEntry entry)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] ");
        sb.Append($"[{entry.Level}] ");
        sb.Append($"[{entry.Category}] ");
        sb.Append(entry.Message);

        if (!string.IsNullOrEmpty(entry.Details))
            sb.Append($" | {entry.Details}");

        if (entry.Exception != null)
            sb.Append($" | Exception: {entry.Exception.GetType().Name}: {entry.Exception.Message}");

        return sb.ToString();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _logChannel.Writer.Complete();
        _cts.Dispose();
    }
}
