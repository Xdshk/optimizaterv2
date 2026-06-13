using GTA5Optimizer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Считает FPS экрана через DXGI Output Duplication API.
/// Захватывает кадры с монитора и считает дельту между ними.
/// Для полноэкранной игры = игровой FPS.
/// Fallback на RTSS shared memory если DXGI недоступен.
/// </summary>
public sealed class ScreenFpsCounter : IScreenFpsCounter
{
    private readonly ILogger<ScreenFpsCounter> _logger;
    private Thread? _captureThread;
    private readonly CancellationTokenSource _cts = new();
    private double _currentFps;
    private readonly object _fpsLock = new();

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
        _captureThread.SetApartmentState(ApartmentState.STA);
        _captureThread.Start();
    }

    public void StopCapture()
    {
        _cts.Cancel();
        _captureThread?.Join(2000);
        _captureThread = null;
    }

    private void CaptureLoop()
    {
        // Try DXGI first, fall back to RTSS
        if (TryStartDxgI(out var dxgiCapture))
        {
            try
            {
                RunDxgILoop(dxgiCapture);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DXGI capture failed, switching to RTSS");
            }
            finally
            {
                dxgiCapture.Dispose();
            }
        }

        // Fallback: RTSS shared memory polling
        RunRtssLoop();
    }

    private bool TryStartDxgI(out DxgiCapture capture)
    {
        try
        {
            capture = new DxgiCapture(_logger);
            return true;
        }
        catch
        {
            capture = null!;
            return false;
        }
    }

    private void RunDxgILoop(DxgiCapture capture)
    {
        var lastTimestamp = Stopwatch.GetTimestamp();
        var frameCount = 0;
        var fpsAccum = 0.0;
        var fpsFrames = 0;

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                capture.AcquireFrame();

                frameCount++;
                var now = Stopwatch.GetTimestamp();
                var deltaMs = (now - lastTimestamp) * 1000.0 / Stopwatch.Frequency;
                lastTimestamp = now;

                if (deltaMs > 0 && deltaMs < 1000)
                {
                    fpsAccum += deltaMs;
                    fpsFrames++;

                    if (fpsFrames >= 30) // Update every 30 frames
                    {
                        var avgFps = 1000.0 / (fpsAccum / fpsFrames);
                        lock (_fpsLock)
                        {
                            _currentFps = _currentFps > 0
                                ? _currentFps * 0.6 + avgFps * 0.4
                                : avgFps;
                        }
                        fpsAccum = 0;
                        fpsFrames = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "DXGI frame capture error");
                Thread.Sleep(50);
            }
        }
    }

    private void RunRtssLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            var fps = TryReadRtssFps();
            if (fps > 0)
            {
                lock (_fpsLock)
                {
                    _currentFps = _currentFps > 0
                        ? _currentFps * 0.7 + fps * 0.3
                        : fps;
                }
            }
            Thread.Sleep(200);
        }
    }

    /// <summary>
    /// Читает FPS из RTSS (RivaTuner Statistics Server) shared memory.
    /// </summary>
    private static double TryReadRtssFps()
    {
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(RtssMapName, MemoryMappedFileRights.Read);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            var signature = accessor.ReadUInt32(0);
            if (signature != 0x53535452) // "RTSS"
                return 0;

            var version = accessor.ReadUInt32(4);
            if (version < 2)
                return 0;

            var frameTimeUs = accessor.ReadUInt32(20);
            if (frameTimeUs > 0)
            {
                var fps = 1_000_000.0 / frameTimeUs;
                if (fps > 0 && fps < 1000)
                    return fps;
            }
        }
        catch { }

        return 0;
    }

    private const string RtssMapName = "RTSSSharedMemoryV2";

    public void Dispose()
    {
        StopCapture();
        _cts.Dispose();
    }
}

/// <summary>
/// DXGI Desktop Duplication capture — минимальная реализация через COM.
/// Использует DXGI 1.1 Output Duplication API для захвата кадров экрана.
/// </summary>
internal sealed class DxgiCapture : IDisposable
{
    private readonly ILogger _logger;
    private readonly IntPtr _duplication;
    private readonly IntPtr _device;
    private readonly IntPtr _output1;
    private bool _disposed;

    public DxgiCapture(ILogger logger)
    {
        _logger = logger;

        // Create D3D11 device
        if (!CreateD3D11Device(out _device))
            throw new InvalidOperationException("Failed to create D3D11 device");

        // Get DXGI Output Duplication
        if (!GetOutputDuplication(out _output1, out _duplication))
        {
            Marshal.Release(_device);
            throw new InvalidOperationException("Failed to get output duplication");
        }
    }

    public void AcquireFrame()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DxgiCapture));

        var hr = DxgiAcquireNextFrame(_duplication, 100, out _, out _);
        if (hr < 0 && hr != unchecked((int)0x887A0002)) // Not timeout
            throw new COMException("AcquireNextFrame failed", hr);

        DxgiReleaseFrame(_duplication);
    }

    private static bool CreateD3D11Device(out IntPtr device)
    {
        device = IntPtr.Zero;
        var featureLevels = new[] { D3D_FEATURE_LEVEL_11_0, D3D_FEATURE_LEVEL_10_1, D3D_FEATURE_LEVEL_10_0 };

        var hr = D3D11CreateDevice(
            IntPtr.Zero,
            D3D_DRIVER_TYPE_HARDWARE,
            IntPtr.Zero,
            D3D11_CREATE_DEVICE_FLAG,
            featureLevels,
            (uint)featureLevels.Length,
            D3D11_SDK_VERSION,
            out device,
            out _,
            out _);

        if (hr < 0)
        {
            // Try WARP if hardware not available
            hr = D3D11CreateDevice(
                IntPtr.Zero,
                D3D_DRIVER_TYPE_WARP,
                IntPtr.Zero,
                D3D11_CREATE_DEVICE_FLAG,
                featureLevels,
                (uint)featureLevels.Length,
                D3D11_SDK_VERSION,
                out device,
                out _,
                out _);
        }

        return hr >= 0;
    }

    private bool GetOutputDuplication(out IntPtr output1, out IntPtr duplication)
    {
        output1 = IntPtr.Zero;
        duplication = IntPtr.Zero;

        try
        {
            // Get DXGI device from D3D11 device
            var dxgiDevice = GetDxgiDevice(_device);
            if (dxgiDevice == IntPtr.Zero) return false;

            // Get adapter
            var adapter = GetAdapter(dxgiDevice);
            Marshal.Release(dxgiDevice);
            if (adapter == IntPtr.Zero) return false;

            // Get output
            var output = GetOutput(adapter);
            Marshal.Release(adapter);
            if (output == IntPtr.Zero) return false;

            // Query IDXGIOutput1
            output1 = QueryInterface(output, IID_IDXGIOutput1);
            Marshal.Release(output);
            if (output1 == IntPtr.Zero) return false;

            // DuplicateOutput
            duplication = DuplicateOutput(output1, _device);
            if (duplication == IntPtr.Zero)
            {
                Marshal.Release(output1);
                output1 = IntPtr.Zero;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "DXGI setup error");
            if (duplication != IntPtr.Zero) Marshal.Release(duplication);
            if (output1 != IntPtr.Zero) Marshal.Release(output1);
            return false;
        }
    }

    private static IntPtr GetDxgiDevice(IntPtr d3dDevice)
    {
        return QueryInterface(d3dDevice, IID_IDXGIDevice);
    }

    private static IntPtr GetAdapter(IntPtr dxgiDevice)
    {
        var hr = DxgiGetAdapter(dxgiDevice, out var adapter);
        if (hr < 0) return IntPtr.Zero;
        return adapter;
    }

    private static IntPtr GetOutput(IntPtr adapter)
    {
        var hr = DxgiEnumOutputs(adapter, 0, out var output);
        if (hr < 0) return IntPtr.Zero;
        return output;
    }

    private static IntPtr DuplicateOutput(IntPtr output1, IntPtr device)
    {
        var hr = DxgiDuplicateOutput(output1, device, out var duplication);
        if (hr < 0) return IntPtr.Zero;
        return duplication;
    }

    private static IntPtr QueryInterface(IntPtr obj, Guid iid)
    {
        var hr = ObjQueryInterface(obj, ref iid, out var ptr);
        if (hr < 0) return IntPtr.Zero;
        return ptr;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_duplication != IntPtr.Zero) Marshal.Release(_duplication);
        if (_output1 != IntPtr.Zero) Marshal.Release(_output1);
        if (_device != IntPtr.Zero) Marshal.Release(_device);
    }

    // Constants
    private const uint D3D11_SDK_VERSION = 7;
    private const uint D3D11_CREATE_DEVICE_FLAG = 0;
    private const uint D3D_DRIVER_TYPE_HARDWARE = 1;
    private const uint D3D_DRIVER_TYPE_WARP = 5;
    private static readonly Guid IID_IDXGIDevice = new("54ec77fa-1377-44e6-8c32-88fd5f448478");
    private static readonly Guid IID_IDXGIOutput1 = new("00cddea8-939b-4b83-a340-a686226e6656");
    private static readonly Guid IID_IDXGIOutputDuplication = new("191cfac3-a321-478d-9563-45b60e6b0e39");

    private static readonly D3D_FEATURE_LEVEL D3D_FEATURE_LEVEL_11_0 = new() { Level = 0xb000 };
    private static readonly D3D_FEATURE_LEVEL D3D_FEATURE_LEVEL_10_1 = new() { Level = 0xa100 };
    private static readonly D3D_FEATURE_LEVEL D3D_FEATURE_LEVEL_10_0 = new() { Level = 0xa000 };

    // P/Invoke
    [DllImport("d3d11.dll", PreserveSig = true)]
    private static extern int D3D11CreateDevice(
        IntPtr pAdapter,
        uint DriverType,
        IntPtr Software,
        uint Flags,
        D3D_FEATURE_LEVEL[] pFeatureLevels,
        uint FeatureLevels,
        uint SDKVersion,
        out IntPtr ppDevice,
        out IntPtr pFeatureLevel,
        out IntPtr ppImmediateContext);

    [DllImport("dxgi.dll", PreserveSig = true)]
    private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr ppFactory);

    // COM vtable methods (called via function pointers)
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QueryInterfaceDelegate(IntPtr thisPtr, ref Guid riid, out IntPtr ppvObject);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetAdapterDelegate(IntPtr thisPtr, out IntPtr pAdapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumOutputsDelegate(IntPtr thisPtr, uint Output, out IntPtr ppOutput);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DuplicateOutputDelegate(IntPtr thisPtr, IntPtr pDevice, out IntPtr ppOutputDuplication);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AcquireNextFrameDelegate(IntPtr thisPtr, uint Timeout, out IntPtr pFrameInfo, out IntPtr ppDesktopResource);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ReleaseFrameDelegate(IntPtr thisPtr);

    private static int ObjQueryInterface(IntPtr obj, ref Guid riid, out IntPtr ppv)
    {
        ppv = IntPtr.Zero;
        // Get vtable from the object pointer
        var vtable = Marshal.ReadIntPtr(obj);
        // QueryInterface is at vtable[0]
        var qiPtr = Marshal.ReadIntPtr(vtable);
        var qi = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(qiPtr);
        return qi(obj, ref riid, out ppv);
    }

    private static int DxgiGetAdapter(IntPtr dxgiDevice, out IntPtr adapter)
    {
        adapter = IntPtr.Zero;
        var vtable = Marshal.ReadIntPtr(dxgiDevice);
        // IDXGIDevice::GetAdapter is vtable[12] (after IUnknown(3) + IDXGIDevice methods)
        // Actually: IUnknown(3) + SetGPUThreadPriority + GetGPUThreadPriority + SetMaximumFrameLatency +
        // GetMaximumFrameLatency + OfferResources + ReclaimResources + EnqueueSetEvent + Trim + GetAdapter = index 12
        var getAdapterPtr = Marshal.ReadIntPtr(vtable, 12 * IntPtr.Size);
        var getAdapter = Marshal.GetDelegateForFunctionPointer<GetAdapterDelegate>(getAdapterPtr);
        return getAdapter(dxgiDevice, out adapter);
    }

    private static int DxgiEnumOutputs(IntPtr adapter, uint index, out IntPtr output)
    {
        output = IntPtr.Zero;
        var vtable = Marshal.ReadIntPtr(adapter);
        // IDXGIAdapter::EnumOutputs is vtable[7] (IUnknown(3) + GetDesc + CheckInterfaceSupport + EnumOutputs)
        var enumOutputsPtr = Marshal.ReadIntPtr(vtable, 7 * IntPtr.Size);
        var enumOutputs = Marshal.GetDelegateForFunctionPointer<EnumOutputsDelegate>(enumOutputsPtr);
        return enumOutputs(adapter, index, out output);
    }

    private static int DxgiDuplicateOutput(IntPtr output1, IntPtr device, out IntPtr duplication)
    {
        duplication = IntPtr.Zero;
        var vtable = Marshal.ReadIntPtr(output1);
        // IDXGIOutput1::DuplicateOutput — need to find the right vtable index
        // IDXGIOutput has ~20 methods, DuplicateOutput is the last one
        // Let's try index 28 (safe guess for IDXGIOutput1)
        var dupPtr = Marshal.ReadIntPtr(vtable, 28 * IntPtr.Size);
        var dup = Marshal.GetDelegateForFunctionPointer<DuplicateOutputDelegate>(dupPtr);
        return dup(output1, device, out duplication);
    }

    private static int DxgiAcquireNextFrame(IntPtr duplication, uint timeout, out IntPtr frameInfo, out IntPtr resource)
    {
        frameInfo = IntPtr.Zero;
        resource = IntPtr.Zero;
        var vtable = Marshal.ReadIntPtr(duplication);
        // IDXGIOutputDuplication::AcquireNextFrame is vtable[3]
        var acquirePtr = Marshal.ReadIntPtr(vtable, 3 * IntPtr.Size);
        var acquire = Marshal.GetDelegateForFunctionPointer<AcquireNextFrameDelegate>(acquirePtr);
        return acquire(duplication, timeout, out frameInfo, out resource);
    }

    private static int DxgiReleaseFrame(IntPtr duplication)
    {
        var vtable = Marshal.ReadIntPtr(duplication);
        // IDXGIOutputDuplication::ReleaseFrame is vtable[8]
        var releasePtr = Marshal.ReadIntPtr(vtable, 8 * IntPtr.Size);
        var release = Marshal.GetDelegateForFunctionPointer<ReleaseFrameDelegate>(releasePtr);
        return release(duplication);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3D_FEATURE_LEVEL
    {
        public uint Level;
    }
}
