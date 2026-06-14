using GTA5Optimizer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Считает FPS игры через Microsoft PresentMon — перехватывает DXGI Present calls.
/// Работает с ЛЮБОЙ игрой в ЛЮБОМ режиме (fullscreen, borderless, windowed).
/// Поддерживает D3D11, D3D12, Vulkan, OpenGL.
///
/// PresentMon: https://github.com/GameTechDev/PresentMon
/// Нужен файл PresentMon.exe рядом с приложением или в PATH.
/// </summary>
public sealed class PresentMonFpsCounter : IScreenFpsCounter
{
    private readonly ILogger<PresentMonFpsCounter> _logger;
    private readonly string _targetProcessName;
    private Process? _presentMonProcess;
    private double _currentFps;
    private readonly object _fpsLock = new();
    private bool _isRunning;
    private readonly object _startStopLock = new();

    // PresentMon CSV column indices
    private const int Col_ProcessId = 0;
    private const int Col_ProcessName = 1;
    private const int Col_SwapChainAddress = 2;
    private const int Col_Runtime = 3;
    private const int Col_SyncInterval = 4;
    private const int Col_PresentFlags = 5;
    private const int Col_AllowsTearing = 6;
    private const int Col_PresentMode = 7;
    private const int Col_WasBatched = 8;
    private const int Col_DwmFrame = 9;
    private const int Col_Dropped = 10;
    private const int Col_TimeInSeconds = 11;
    private const int Col_msInPresentAPI = 12;
    private const int Col_msBetweenPresents = 13;  // ← главная колонка для FPS
    private const int Col_msBetweenDisplayChange = 14;
    private const int Col_msUntilRenderComplete = 15;
    private const int Col_msUntilDisplayed = 16;

    public double CurrentFPS
    {
        get { lock (_fpsLock) return _currentFps; }
    }

    /// <summary>
    /// Создаёт PresentMon FPS counter.
    /// </summary>
    /// <param name="logger">Логгер</param>
    /// <param name="targetProcessName">Имя процесса игры (например "GTA5")</param>
    public PresentMonFpsCounter(ILogger<PresentMonFpsCounter> logger, string targetProcessName = "GTA5")
    {
        _logger = logger;
        _targetProcessName = targetProcessName;
    }

    public void StartCapture()
    {
        lock (_startStopLock)
        {
            if (_isRunning) return;
            _isRunning = true;

            try
            {
                var presentMonPath = FindPresentMon();
                if (presentMonPath == null)
                {
                    _logger.LogWarning("PresentMon.exe not found. FPS monitoring via PresentMon disabled. " +
                        "Download from: https://github.com/GameTechDev/PresentMon/releases");
                    _isRunning = false;
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = presentMonPath,
                    Arguments = $"-process_name {_targetProcessName}.exe -output_stdout -etl_file none -stop_existing_session",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                _presentMonProcess = new Process { StartInfo = startInfo };
                _presentMonProcess.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logger.LogDebug("PresentMon stderr: {Data}", e.Data);
                };

                _presentMonProcess.Start();
                _presentMonProcess.BeginErrorReadLine();

                _logger.LogInformation("PresentMon started for process '{Process}' (PID: {Pid})",
                    _targetProcessName, _presentMonProcess.Id);

                // Start reading stdout in background
                _ = Task.Run(() => ReadOutputLoop(_presentMonProcess));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start PresentMon");
                _isRunning = false;
            }
        }
    }

    public void StopCapture()
    {
        lock (_startStopLock)
        {
            if (!_isRunning) return;
            _isRunning = false;

            try
            {
                if (_presentMonProcess != null && !_presentMonProcess.HasExited)
                {
                    // Graceful shutdown: send Ctrl+C
                    try
                    {
                        _presentMonProcess.StandardInput.Close();
                    }
                    catch { }

                    if (!_presentMonProcess.WaitForExit(3000))
                    {
                        _presentMonProcess.Kill(entireProcessTree: true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error stopping PresentMon");
            }
            finally
            {
                _presentMonProcess?.Dispose();
                _presentMonProcess = null;
                lock (_fpsLock) _currentFps = 0;
            }
        }
    }

    private async Task ReadOutputLoop(Process process)
    {
        try
        {
            var stdout = process.StandardOutput;
            var recentFrameTimes = new Queue<double>(120); // last 120 frames for smoothing
            string? headerLine = null;

            while (_isRunning && !process.HasExited)
            {
                var line = await stdout.ReadLineAsync();
                if (line == null) break;

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line)) continue;

                // First non-empty line is the CSV header
                if (headerLine == null)
                {
                    headerLine = line;
                    _logger.LogDebug("PresentMon header: {Header}", headerLine);
                    continue;
                }

                try
                {
                    var parts = line.Split(',', StringSplitOptions.TrimEntries);
                    if (parts.Length <= Col_msBetweenPresents) continue;

                    // Parse msBetweenPresents — time between consecutive presents
                    if (!double.TryParse(parts[Col_msBetweenPresents],
                            NumberStyles.Float, CultureInfo.InvariantCulture, out var msBetweenPresents))
                        continue;

                    if (msBetweenPresents <= 0 || msBetweenPresents > 1000)
                        continue;

                    var fps = 1000.0 / msBetweenPresents;

                    // Clamp to reasonable range
                    if (fps < 1 || fps > 1000) continue;

                    // Smoothing: average over recent frames
                    recentFrameTimes.Enqueue(fps);
                    if (recentFrameTimes.Count > 120)
                        recentFrameTimes.Dequeue();

                    lock (_fpsLock)
                    {
                        // Exponential moving average for display
                        if (_currentFps > 0)
                            _currentFps = _currentFps * 0.7 + fps * 0.3;
                        else
                            _currentFps = fps;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error parsing PresentMon line: {Line}", line);
                }
            }
        }
        catch (Exception ex) when (_isRunning)
        {
            _logger.LogWarning(ex, "PresentMon output reading ended unexpectedly");
        }
    }

    /// <summary>
    /// Ищет PresentMon.exe в нескольких местах.
    /// </summary>
    private static string? FindPresentMon()
    {
        // 1. Рядом с нашим приложением
        var appDir = AppContext.BaseDirectory;
        var localPath = Path.Combine(appDir, "PresentMon.exe");
        if (File.Exists(localPath)) return localPath;

        // 2. В подпапке tools
        var toolsPath = Path.Combine(appDir, "tools", "PresentMon.exe");
        if (File.Exists(toolsPath)) return toolsPath;

        // 3. В PATH
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(dir.Trim(), "PresentMon.exe");
            if (File.Exists(fullPath)) return fullPath;
        }

        // 4. Стандартные пути установки
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var commonPaths = new[]
        {
            Path.Combine(programFiles, "PresentMon", "PresentMon.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PresentMon", "PresentMon.exe"),
            @"C:\Tools\PresentMon\PresentMon.exe",
        };

        foreach (var p in commonPaths)
            if (File.Exists(p)) return p;

        return null;
    }

    public void Dispose()
    {
        StopCapture();
    }
}
