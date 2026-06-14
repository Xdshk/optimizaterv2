using GTA5Optimizer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Считает FPS игры через Microsoft PresentMon — перехватывает DXGI Present calls.
/// Нужен файл PresentMon.exe рядом с приложением или в PATH.
/// </summary>
public sealed class PresentMonFpsCounter : IScreenFpsCounter
{
    private readonly ILogger<PresentMonFpsCounter> _logger;
    private readonly string _targetProcessName;
    private Process? _presentMonProcess;
    private double _currentFps;
    private readonly object _fpsLock = new();
    private volatile bool _isRunning;
    private DateTime _lastValidFrame = DateTime.UtcNow;

    public double CurrentFPS
    {
        get
        {
            lock (_fpsLock)
            {
                // If no data for 3 seconds, report 0 (game might be paused/closed)
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
                _logger.LogWarning(
                    "PresentMon.exe not found. Download: https://github.com/GameTechDev/PresentMon/releases");
                _isRunning = false;
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = presentMonPath,
                Arguments = $"-process_name {_targetProcessName}.exe -output_stdout -etl_file none -stop_existing_session -timed 0",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            _presentMonProcess = new Process { StartInfo = startInfo };
            _presentMonProcess.Start();
            _logger.LogInformation("PresentMon started for '{Process}' (PID: {Pid})",
                _targetProcessName, _presentMonProcess.Id);

            _ = Task.Run(() => ReadOutputLoop(_presentMonProcess));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start PresentMon");
            _isRunning = false;
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

            while (_isRunning && !process.HasExited)
            {
                var line = await stdout.ReadLineAsync();
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Skip header
                if (headerLine == null)
                {
                    headerLine = line;
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
        }
        catch (Exception ex) when (_isRunning)
        {
            _logger.LogWarning(ex, "PresentMon reading error");
        }
    }

    private static string? FindPresentMon()
    {
        var appDir = AppContext.BaseDirectory;

        // 1. Рядом с exe
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

        // 4. Стандартные пути
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
