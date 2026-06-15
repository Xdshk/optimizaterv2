using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Logging;
using Microsoft.Extensions.Logging;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using LogEntry = GTA5Optimizer.Models.Logging.LogLevel;
using LogEntryModel = GTA5Optimizer.Models.Logging.LogEntry;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Считает FPS через несколько методов (в порядке приоритета):
/// 1. PresentMon — перехватывает DXGI Present calls (самый точный, нужен PresentMon.exe)
/// 2. RTSS shared memory — MSI Afterburner/RivaTuner
/// 3. DWM timing — через дельту cFramesPresented (borderless/windowed)
/// </summary>
public sealed class ScreenFpsCounter : IScreenFpsCounter
{
    private readonly ILoggerService _logSrv;
    private readonly ILogger<ScreenFpsCounter> _logger;
    private readonly PresentMonFpsCounter _presentMon;
    private Thread? _pollThread;
    private CancellationTokenSource? _cts;
    private double _currentFps;
    private readonly object _fpsLock = new();

    // DWM delta tracking
    private long _lastFramesPresented;
    private DateTime _lastDwmPoll = DateTime.MinValue;
    private bool _dwmAvailable = true;

    // Which method is active
    private string _activeMethod = "none";
    private int _pollIteration;

    public double CurrentFPS
    {
        get { lock (_fpsLock) return _currentFps; }
    }

    public ScreenFpsCounter(ILogger<ScreenFpsCounter> logger, ILoggerService logSrv)
    {
        _logger = logger;
        _logSrv = logSrv;

        var pmLogger = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug))
            .CreateLogger<PresentMonFpsCounter>();
        _presentMon = new PresentMonFpsCounter(pmLogger, "GTA5");
    }

    public void StartCapture()
    {
        _logger.LogInformation("Starting FPS capture...");
        LogToUi("FPS", "Starting FPS capture...", GTA5Optimizer.Models.Logging.LogLevel.Information);

        _presentMon.StartCapture();

        _cts = new CancellationTokenSource();
        _pollThread = new Thread(() => PollLoop(_cts.Token))
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
            Name = "FpsCounter"
        };
        _pollThread.Start();

        _logger.LogInformation("FPS counter started (PresentMon + RTSS + DWM fallback)");
        LogToUi("FPS", "FPS counter started (PresentMon + RTSS + DWM fallback)", GTA5Optimizer.Models.Logging.LogLevel.Information);
    }

    public void StopCapture()
    {
        _presentMon.StopCapture();
        _cts?.Cancel();
        _pollThread?.Join(2000);
        _pollThread = null;
        _cts?.Dispose();
        _cts = null;
        lock (_fpsLock) _currentFps = 0;
        _activeMethod = "none";
    }

    private void LogToUi(string category, string message, GTA5Optimizer.Models.Logging.LogLevel level)
    {
        try
        {
            _ = _logSrv.LogAsync(new LogEntryModel
            {
                Level = level,
                Category = category,
                Message = message
            });
        }
        catch { }
    }

    private void PollLoop(CancellationToken token)
    {
        try
        {
            // Wait for PresentMon to start producing data
            Thread.Sleep(2000);

            while (!token.IsCancellationRequested)
            {
                _pollIteration++;
                double fps = 0;
                string method = "none";

                try
                {
                    // Method 1: PresentMon (most accurate — direct DXGI hook)
                    double pmFps = _presentMon.CurrentFPS;
                    if (pmFps > 0)
                    {
                        fps = pmFps;
                        method = "PresentMon";
                    }

                    // Log PresentMon status every 10 iterations (5 seconds)
                    if (_pollIteration % 10 == 0)
                    {
                        LogToUi("FPS", $"PresentMon.CurrentFPS = {pmFps:F1} (iteration {_pollIteration})",
                            GTA5Optimizer.Models.Logging.LogLevel.Debug);
                    }

                    // Method 2: RTSS shared memory
                    if (fps <= 0)
                    {
                        double rtssFps = TryReadRtssFps();
                        if (rtssFps > 0)
                        {
                            fps = rtssFps;
                            method = "RTSS";
                        }
                    }

                    // Method 3: DWM frame delta (borderless/windowed only)
                    if (fps <= 0)
                    {
                        double dwmFps = ReadDwmDeltaFps();
                        if (dwmFps > 0)
                        {
                            fps = dwmFps;
                            method = "DWM";
                        }
                    }

                    if (fps > 0 && fps < 1000)
                    {
                        lock (_fpsLock)
                        {
                            if (_currentFps > 0)
                                _currentFps = _currentFps * 0.7 + fps * 0.3;
                            else
                                _currentFps = fps;
                        }
                        _activeMethod = method;

                        if (_pollIteration % 5 == 0)
                        {
                            LogToUi("FPS", $"FPS source: {method} — {fps:F1} FPS (smoothed: {CurrentFPS:F1})",
                                GTA5Optimizer.Models.Logging.LogLevel.Information);
                        }
                    }
                    else
                    {
                        if (_pollIteration % 10 == 0)
                        {
                            LogToUi("FPS", $"All FPS methods returned 0 — PresentMon={pmFps:F1}, RTSS=0, DWM=0. Is the game running?",
                                GTA5Optimizer.Models.Logging.LogLevel.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "FPS poll loop error");
                    LogToUi("FPS", $"FPS poll loop error: {ex.Message}",
                        GTA5Optimizer.Models.Logging.LogLevel.Error);
                }

                Thread.Sleep(500);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FPS poll loop fatal error");
            LogToUi("FPS", $"FPS poll loop fatal error: {ex.Message}",
                GTA5Optimizer.Models.Logging.LogLevel.Error);
        }
    }

    private double ReadDwmDeltaFps()
    {
        if (!_dwmAvailable) return 0;

        try
        {
            var info = new DWM_TIMING_INFO();
            info.cbSize = Marshal.SizeOf<DWM_TIMING_INFO>();

            var hr = DwmGetCompositionTimingInfo(IntPtr.Zero, ref info);
            if (hr != 0)
            {
                _dwmAvailable = false;
                _logger.LogWarning("DWM not available (hr={Hr})", hr);
                return 0;
            }

            var now = DateTime.UtcNow;

            if (_lastDwmPoll == DateTime.MinValue || info.cFramesPresented <= _lastFramesPresented)
            {
                _lastDwmPoll = now;
                _lastFramesPresented = info.cFramesPresented;
                return 0;
            }

            var dt = (now - _lastDwmPoll).TotalSeconds;
            var df = info.cFramesPresented - _lastFramesPresented;

            _lastDwmPoll = now;
            _lastFramesPresented = info.cFramesPresented;

            if (dt <= 0 || dt > 5) return 0;

            var fps = df / dt;
            if (fps < 1 || fps > 1000) return 0;

            return fps;
        }
        catch (Exception ex)
        {
            _dwmAvailable = false;
            _logger.LogWarning(ex, "DWM read failed permanently");
            return 0;
        }
    }

    private static double TryReadRtssFps()
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting("RTSSSharedMemoryV2", MemoryMappedFileRights.Read);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            var sig = accessor.ReadUInt32(0);
            if (sig != 0x53535452) return 0;

            var version = accessor.ReadUInt32(4);
            if (version < 2) return 0;

            var frameTimeUs = accessor.ReadUInt32(20);
            if (frameTimeUs > 0)
            {
                var fps = 1_000_000.0 / frameTimeUs;
                if (fps > 1 && fps < 1000)
                    return fps;
            }
        }
        catch { }
        return 0;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmGetCompositionTimingInfo(IntPtr hwnd, ref DWM_TIMING_INFO pTimingInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_TIMING_INFO
    {
        public int cbSize;
        public RPC_RATE rateRefresh;
        public long qpcVBlank;
        public long cRefresh;
        public long cRefreshFrameDelta;
        public int cFrames;
        public int cFramesBuffered;
        public RPC_RATE rateCompose;
        public long qpcCompose;
        public long cFrame;
        public long cFramesPresented;
        public long cFrameComplete;
        public long cFramesDisplayed;
        public long cRefreshConfirmed;
        public int cFramesToConfirm;
        public long cRefreshConfirmedStart;
        public long cRefreshConfirmedStop;
        public long cFramesLastConfirmed;
        public long cFramesLastDisplayed;
        public long cFramesLastComplete;
        public long cFramesLastDrawn;
        public long cFramesLastSkipped;
        public long cFramesLastMissed;
        public long cRefreshFrameDeltas;
        public long cRefreshFrameDeltasStart;
        public long cRefreshFrameDeltasStop;
        public long cRefreshFrameDeltasLast;
        public long cRefreshFrameDeltasNext;
        public long cRefreshFrameDeltasLastComplete;
        public long cRefreshFrameDeltasLastDrawn;
        public long cRefreshFrameDeltasLastSkipped;
        public long cRefreshFrameDeltasLastMissed;
        public long cRefreshFrameDeltasLastConfirmed;
        public long cRefreshFrameDeltasLastDisplayed;
        public long cRefreshFrameDeltasLastComplete2;
        public long cRefreshFrameDeltasLastDrawn2;
        public long cRefreshFrameDeltasLastSkipped2;
        public long cRefreshFrameDeltasLastMissed2;
        public long cRefreshFrameDeltasLastConfirmed2;
        public long cRefreshFrameDeltasLastDisplayed2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RPC_RATE
    {
        public uint uiNumerator;
        public uint uiDenominator;
    }

    public void Dispose() => StopCapture();
}
