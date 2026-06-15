using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Считает FPS игры через Microsoft PresentMon — перехватывает DXGI Present calls.
/// Нужен файл PresentMon.exe рядом с приложением или в PATH.
/// </summary>
public sealed class PresentMonFpsCounter : IDisposable
{
    private readonly ILogger<PresentMonFpsCounter> _logger;
    private readonly string _targetProcessName;
    private Process? _presentMonProcess;
    private double _currentFps;
    private readonly object _fpsLock = new();
    private volatile bool _isRunning;
    private DateTime _lastValidFrame = DateTime.UtcNow;
    private string? _lastError;

    public double CurrentFPS
    {
        get
        {
            lock (_fpsLock)
            {
                if ((DateTime.UtcNow - _lastValidFrame).TotalSeconds > 3)
                    return 0;
                return _currentFps;
            }
        }
    }

    public PresentMonFpsCounter(ILogger<PresentMonFpsCounter> logger, string targetProcessName = "GTA5")
    {
        _logger = logger;
        _targetProcessName = targetProcessName;
    }

    public void StartCapture()
    {
        if (_isRunning) return;
        _isRunning = true;

        try
        {
            var presentMonPath = FindPresentMon();
            if (presentMonPath == null)
            {
                _lastError = "PresentMon.exe not found in app dir, tools/, PATH, or standard locations";
                _logger.LogWarning("PresentMon.exe not found. Download: https://github.com/GameTechDev/PresentMon/releases");
                _isRunning = false;
                return;
            }

            _logger.LogInformation("Found PresentMon at: {Path}", presentMonPath);

            // Try multiple process name variants
            // GTA V can be: GTA5.exe, GTA5, or even custom names via launchers
            var processNames = new[] { _targetProcessName, "GTA5", "GTA5.exe", "GTA V", "GTAV" };
            var targetName = processNames[0]; // Use the configured one

            var startInfo = new ProcessStartInfo
            {
                FileName = presentMonPath,
                Arguments = $"-process_name {targetName}.exe -output_stdout -etl_file none -stop_existing_session -timed 0",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(presentMonPath) ?? AppContext.BaseDirectory
            };

            _logger.LogInformation("Starting PresentMon: {Exe} {Args}", presentMonPath, startInfo.Arguments);

            _presentMonProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _presentMonProcess.Exited += (_, _) =>
            {
                _logger.LogWarning("PresentMon process exited with code {Code}", _presentMonProcess.ExitCode);
                _isRunning = false;
            };

            _presentMonProcess.Start();
            _logger.LogInformation("PresentMon started (PID: {Pid})", _presentMonProcess.Id);

            // Read stderr in background — PresentMon writes errors there
            _ = Task.Run(() => ReadStderrLoop(_presentMonProcess));
            // Read stdout (FPS data)
            _ = Task.Run(() => ReadOutputLoop(_presentMonProcess));
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _logger.LogError(ex, "Failed to start PresentMon");
            _isRunning = false;
        }
    }

    private async Task ReadStderrLoop(Process process)
    {
        try
        {
            var stderr = process.StandardError;
            while (_isRunning && !process.HasExited)
            {
                var line = await stderr.ReadLineAsync();
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                _logger.LogWarning("PresentMon stderr: {Line}", line);

                // Common PresentMon errors:
                // "Failed to find process" — wrong process name
                // "Failed to initialize" — admin rights issue
                // "A trace session is already in use" — another PresentMon running
                if (line.Contains("Failed to find", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("No matching", StringComparison.OrdinalIgnoreCase))
                {
                    _lastError = $"PresentMon can't find process '{_targetProcessName}.exe'. Is the game running?";
                }
                else if (line.Contains("access", StringComparison.OrdinalIgnoreCase) ||
                         line.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
                         line.Contains("privilege", StringComparison.OrdinalIgnoreCase))
                {
                    _lastError = "PresentMon needs admin rights. Run GTA5Optimizer as administrator.";
                }
                else if (line.Contains("already in use", StringComparison.OrdinalIgnoreCase) ||
                         line.Contains("existing session", StringComparison.OrdinalIgnoreCase))
                {
                    _lastError = "Another PresentMon/ETL session is running. Close other monitoring tools.";
                }
            }
        }
        catch (Exception ex) when (_isRunning)
        {
            _logger.LogDebug(ex, "PresentMon stderr read error");
        }
    }

    public void StopCapture()
    {
        if (!_isRunning) return;
        _isRunning = false;

        try
        {
            if (_presentMonProcess != null && !_presentMonProcess.HasExited)
            {
                try { _presentMonProcess.StandardInput.Close(); } catch { }
                if (!_presentMonProcess.WaitForExit(3000))
                    _presentMonProcess.Kill(entireProcessTree: true);
            }
        }
        catch { }
        finally
        {
            _presentMonProcess?.Dispose();
            _presentMonProcess = null;
            lock (_fpsLock) _currentFps = 0;
        }
    }

    private async Task ReadOutputLoop(Process process)
    {
        try
        {
            var stdout = process.StandardOutput;
            string? headerLine = null;
            int lineCount = 0;

            while (_isRunning && !process.HasExited)
            {
                var line = await stdout.ReadLineAsync();
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                lineCount++;

                // Skip header
                if (headerLine == null)
                {
                    headerLine = line;
                    _logger.LogInformation("PresentMon header: {Header}", line);
                    continue;
                }

                try
                {
                    var parts = line.Split(',', StringSplitOptions.TrimEntries);
                    if (parts.Length <= 13) continue;

                    // msBetweenPresents is column index 13
                    if (!double.TryParse(parts[13],
                            NumberStyles.Float, CultureInfo.InvariantCulture, out var ms))
                        continue;

                    if (ms <= 0 || ms > 1000) continue;

                    var fps = 1000.0 / ms;
                    if (fps < 1 || fps > 1000) continue;

                    lock (_fpsLock)
                    {
                        if (_currentFps > 0)
                            _currentFps = _currentFps * 0.7 + fps * 0.3;
                        else
                            _currentFps = fps;
                        _lastValidFrame = DateTime.UtcNow;
                    }
                }
                catch { /* skip malformed lines */ }
            }

            _logger.LogInformation("PresentMon output loop ended. Total lines read: {Count}", lineCount);
        }
        catch (Exception ex) when (_isRunning)
        {
            _logger.LogWarning(ex, "PresentMon reading error");
        }
    }

    private static string? FindPresentMon()
    {
        var appDir = AppContext.BaseDirectory;

        // 1. Next to exe
        var local = Path.Combine(appDir, "PresentMon.exe");
        if (File.Exists(local)) return local;

        // 2. tools/
        var tools = Path.Combine(appDir, "tools", "PresentMon.exe");
        if (File.Exists(tools)) return tools;

        // 3. PATH
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var full = Path.Combine(dir.Trim(), "PresentMon.exe");
            if (File.Exists(full)) return full;
        }

        // 4. Standard paths
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.Combine(pf, "PresentMon", "PresentMon.exe"),
            Path.Combine(localAppData, "PresentMon", "PresentMon.exe"),
            @"C:\Tools\PresentMon\PresentMon.exe",
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        return null;
    }

    public void Dispose() => StopCapture();
}
