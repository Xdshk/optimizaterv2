using GTA5Optimizer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Считает FPS через несколько методов (в порядке приоритета):
/// 1. PresentMon — перехватывает DXGI Present calls (самый точный, нужен PresentMon.exe)
/// 2. RTSS shared memory — MSI Afterburner/RivaTuner
/// 3. DWM timing — через дельту cFramesPresented (работает всегда в composited режиме)
/// </summary>
public sealed class ScreenFpsCounter : IScreenFpsCounter
{
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

    public double CurrentFPS
    {
        get { lock (_fpsLock) return _currentFps; }
    }

    public ScreenFpsCounter(ILogger<ScreenFpsCounter> logger)
    {
        _logger = logger;

        // Create PresentMon counter — it'll gracefully handle if PresentMon.exe is missing
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

        _logger.LogInformation("FPS counter started");
    }

    public void StopCapture()
    {
        _presentMon.StopCapture();
        _cts?.Cancel();
        if (_pollThread != null)
        {
            _pollThread.Join(2000);
            _pollThread = null;
        }
        _cts?.Dispose();
        _cts = null;
        lock (_fpsLock) _currentFps = 0;
    }

    private void PollLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                double fps = 0;

                // Method 1: PresentMon (most accurate)
                fps = _presentMon.CurrentFPS;

                // Method 2: RTSS shared memory
                if (fps <= 0)
                    fps = TryReadRtssFps();

                // Method 3: DWM frame delta (always available on Win10/11)
                if (fps <= 0)
                    fps = ReadDwmDeltaFps();

                if (fps > 0 && fps <= 1000)
                {
                    lock (_fpsLock)
                    {
                        if (_currentFps > 0)
                            _currentFps = _currentFps * 0.6 + fps * 0.4;
                        else
                            _currentFps = fps;
                    }
                }

                Thread.Sleep(200); // 5 Hz polling
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FPS poll loop error");
        }
    }

    /// <summary>
    /// Reads FPS via DWM by tracking cFramesPresented delta over time.
    /// This works because DWM composits ALL desktop activity including game frames.
    /// Returns 0 if DWM is unavailable (e.g., from first call).
    /// </summary>
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
                return 0;
            }

            var now = DateTime.UtcNow;

            if (_lastDwmPoll == DateTime.MinValue || info.cFramesPresented <= _lastFramesPresented)
            {
                _lastDwmPoll = now;
                _lastFramesPresented = info.cFramesPresented;
                return 0; // First call — need delta
            }

            var dt = (now - _lastDwmPoll).TotalSeconds;
            var df = info.cFramesPresented - _lastFramesPresented;

            _lastDwmPoll = now;
            _lastFramesPresented = info.cFramesPresented;

            if (dt <= 0 || dt > 5) return 0; // Too long between polls

            var fps = df / dt;
            if (fps < 1 || fps > 1000) return 0;

            // Sanity: if desktop refresh is e.g. 60Hz but game runs at 200 FPS,
            // DWM still only composites at monitor refresh rate.
            // That's OK — we'll detect game FPS via PresentMon instead.
            return fps;
        }
        catch
        {
            _dwmAvailable = false;
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
            if (sig != 0x53535452) return 0; // "RTSS"

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
        catch { } // RTSS not running — normal

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
