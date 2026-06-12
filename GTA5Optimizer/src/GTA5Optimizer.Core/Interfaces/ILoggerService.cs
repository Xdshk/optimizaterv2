using GTA5Optimizer.Models.Logging;

namespace GTA5Optimizer.Core.Interfaces;

public interface ILoggerService
{
    Task LogAsync(LogEntry entry);
    Task<List<LogEntry>> GetRecentLogsAsync(int count = 100);
    Task ClearLogsAsync();
}
