using GTA5Optimizer.Core.Interfaces;
using GTA5Optimizer.Models.Logging;
using Microsoft.Extensions.Logging;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using LogEntry = GTA5Optimizer.Models.Logging.LogEntry;

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

    public double CurrentFPS
    {
        get { lock (_fpsLock) return _currentFps; }
    }

    public ScreenFpsCounter(ILogger<ScreenFpsCounter> logger, ILoggerService logSrv)
    {
        _logger = logger;
        _logSrv = logSrv;

        var pmLogger = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information))
            .CreateLogger<PresentMonFpsCounter>();
        _presentMon = new PresentMonFpsCounter(pmLogger, "GTA5");
    }

    public void StartCapture()
    {
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
        _ = _logSrv.LogAsync(new LogEntry
        {
            Level = GTA5Optimizer.Models.Logging.LogLevel.Information,
            Category = "FPS",
            Message = "FPS counter started (PresentMon + RTSS + DWM fallback)"
        });
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

    private void PollLoop(CancellationToken token)
    {
        try
        {
            // Wait a bit for PresentMon to start producing data
            Thread.Sleep(1000);

            while (!token.IsCancellationRequested)
            {
                double fps = 0;
                string method = "none";

                try
                {
                    // Method 1: PresentMon (most accurate — direct DXGI hook)
                    fps = _presentMon.CurrentFPS;
                    if (fps > 0) method = "PresentMon";

                    // Method 2: RTSS shared memory
                    if (fps <= 0)
                    {
                        fps = TryReadRtssFps();
                        if (fps > 0) method = "RTSS";
                    }

                    // Method 3: DWM frame delta (borderless/windowed only)
                    if (fps <= 0)
                    {
                        fps = ReadDwmDeltaFps();
                        if (fps > 0) method = "DWM";
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

                        _logger.LogDebug("FPS source: {Method} — {Fps:F1} FPS", method, fps);
                        _ = _logSrv.LogAsync(new LogEntry
                        {
                            Level = GTA5Optimizer.Models.Logging.LogLevel.Debug,
                            Category = "FPS",
                            Message = $"FPS source: {method} — {fps:F1} FPS"
                        });
                    }
                    else
                    {
                        _logger.LogDebug("All FPS methods returned 0 — PresentMon={PM:F1}, method={Method}",
                            _presentMon.CurrentFPS, method);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "FPS poll loop error");
                    _ = _logSrv.LogAsync(new LogEntry
                    {
                        Level = GTA5Optimizer.Models.Logging.LogLevel.Error,
                        Category = "FPS",
                        Message = $"FPS poll loop error: {ex.Message}"
                    });
                }

                // Poll every 500ms
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
            _ = _logSrv.LogAsync(new LogEntry
            {
                Level = GTA5Optimizer.Models.Logging.LogLevel.Error,
                Category = "FPS",
                Message = $"FPS poll loop fatal error: {ex.Message}"
            });
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
