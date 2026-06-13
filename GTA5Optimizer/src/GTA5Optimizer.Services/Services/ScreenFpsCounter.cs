using GTA5Optimizer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Считает FPS экрана через DWM (Desktop Window Manager) composition timing
/// + fallback на RTSS (RivaTuner Statistics Server) shared memory.
///
/// DWM метод: отслеживает реальные кадры композиции через DwmGetCompositionTimingInfo.
/// Для полноэкранной игры в exclusive fullscreen это = игровой FPS.
/// Для borderless/windowed — близко к реальному FPS.
///
/// RTSS метод: если установлен RivaTuner/MSI Afterburner, читает точный FPS
/// из shared memory.
/// </summary>
public sealed class ScreenFpsCounter : IScreenFpsCounter
{
    private readonly ILogger<ScreenFpsCounter> _logger;
    private Thread? _captureThread;
    private readonly CancellationTokenSource _cts = new();
    private double _currentFps;
    private readonly object _fpsLock = new();

    // DWM timing
    private readonly Stopwatch _dwmStopwatch = new();
    private double _dwmFpsAccum;
    private int _dwmFpsFrames;

    public double CurrentFPS
    {
        get { lock (_fpsLock) return _currentFps; }
    }

    public ScreenFpsCounter(ILogger<ScreenFpsCounter> logger)
    {
        _logger = logger;
    }

    public void StartCapture()
    {
        if (_captureThread != null) return;

        _captureThread = new Thread(CaptureLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
            Name = "ScreenFpsCounter"
        };
        _captureThread.Start();
    }

    public void StopCapture()
    {
        _cts.Cancel();
        _captureThread?.Join(2000);
        _captureThread = null;
        _dwmStopwatch.Stop();
    }

    private void CaptureLoop()
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                double fps = 0;

                // Method 1: RTSS shared memory (most accurate for games)
                fps = TryReadRtssFps();

                // Method 2: DWM composition timing
                if (fps <= 0)
                {
                    fps = ReadDwmComposedFps();
                }

                if (fps > 0)
                {
                    lock (_fpsLock)
                    {
                        // Exponential smoothing to prevent jitter
                        if (_currentFps > 0)
                            _currentFps = _currentFps * 0.65 + fps * 0.35;
                        else
                            _currentFps = fps;
                    }
                }

                Thread.Sleep(100); // 10 Hz polling
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Screen FPS counter failed");
        }
    }

    /// <summary>
    /// Читает FPS из RTSS (RivaTuner Statistics Server) shared memory.
    /// RTSS используется MSI Afterburner и другими оверлеями.
    /// Возвращает 0 если RTSS не запущен.
    /// </summary>
    private static double TryReadRtssFps()
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(RtssSharedMemName, MemoryMappedFileRights.Read);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            // RTSS Shared Memory v2 format:
            // Offset 0: uint32 signature "RTSS" (0x53535452 LE)
            // Offset 4: uint32 version (must be >= 2)
            // Offset 8: uint32 appEntrySize
            // Offset 12: uint32 time (GetTickCount)
            // Offset 16: uint32 frames
            // Offset 20: uint32 frameTime (microseconds)
            var sig = accessor.ReadUInt32(0);
            if (sig != RtssSignature)
                return 0;

            var version = accessor.ReadUInt32(4);
            if (version < 2)
                return 0;

            var frameTimeUs = accessor.ReadUInt32(20);
            if (frameTimeUs > 0)
            {
                var fps = 1_000_000.0 / frameTimeUs;
                if (fps > 1 && fps < 1000)
                    return fps;
            }
        }
        catch
        {
            // RTSS not running — normal, not an error
        }

        return 0;
    }

    /// <summary>
    /// Считает FPS через DWM composition timing.
    /// DwmGetCompositionTimingInfo даёт информацию о кадрах композиции.
    /// cFrames / cRefreshFrameDelta * rateRefresh = FPS.
    /// </summary>
    private double ReadDwmComposedFps()
    {
        try
        {
            var info = new DWM_TIMING_INFO();
            info.cbSize = Marshal.SizeOf<DWM_TIMING_INFO>();

            DwmGetCompositionTimingInfo(IntPtr.Zero, ref info);

            // Используем cFrames и cRefreshFrameDelta для расчёта FPS
            // cFrames — количество кадров с момента последнего вызова
            // cRefreshFrameDelta — количество тиков между кадрами
            // rateRefresh — частота обновления монитора (например 60/1 = 60Hz)

            if (info.cFrames > 0 && info.cRefreshFrameDelta > 0 &&
                info.rateRefresh.uiNumerator > 0 && info.rateRefresh.uiDenominator > 0)
            {
                var refreshRate = (double)info.rateRefresh.uiNumerator / info.rateRefresh.uiDenominator;
                var fps = (double)info.cFrames / info.cRefreshFrameDelta * refreshRate;

                if (fps > 1 && fps < 1000)
                {
                    // Smooth the value
                    _dwmFpsAccum += fps;
                    _dwmFpsFrames++;

                    if (_dwmFpsFrames >= 5) // Average over 5 samples
                    {
                        var smoothedFps = _dwmFpsAccum / _dwmFpsFrames;
                        _dwmFpsAccum = 0;
                        _dwmFpsFrames = 0;
                        return smoothedFps;
                    }
                }
            }
        }
        catch
        {
            // DWM not available
        }

        return 0;
    }

    private const string RtssSharedMemName = "RTSSSharedMemoryV2";
    private const uint RtssSignature = 0x53535452; // "RTSS"

    // DWM Interop
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetCompositionTimingInfo(
        IntPtr hwnd,
        ref DWM_TIMING_INFO pTimingInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_TIMING_INFO
    {
        public int cbSize;
        public DWM_RATE rateRefresh;
        public long qpcVBlank;
        public long cRefresh;
        public long cRefreshFrameDelta;
        public int cFrames;
        public int cFramesBuffered;
        public DWM_RATE rateCompose;
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
    private struct DWM_RATE
    {
        public uint uiNumerator;
        public uint uiDenominator;
    }

    public void Dispose()
    {
        StopCapture();
        _cts.Dispose();
    }
}
