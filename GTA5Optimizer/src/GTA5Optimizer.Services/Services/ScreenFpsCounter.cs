using GTA5Optimizer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace GTA5Optimizer.Services.Services;

/// <summary>
/// Считает FPS экрана через DXGI Desktop Duplication API.
/// Работает для любого приложения — не требует инъекции в игру.
/// Захватывает кадры с монитора и считает дельту между ними.
/// </summary>
public sealed class ScreenFpsCounter : IScreenFpsCounter
{
    private readonly ILogger<ScreenFpsCounter> _logger;
    private Thread? _captureThread;
    private readonly CancellationTokenSource _cts = new();
    private double _currentFps;
    private readonly object _fpsLock = new();

    // Frame timing
    private readonly Stopwatch _frameStopwatch = new();
    private readonly Queue<double> _frameDeltas = new();
    private const int MaxDeltaHistory = 120;
    private long _totalFrames;

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

        _frameStopwatch.Start();
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
        _frameStopwatch.Stop();
    }

    private void CaptureLoop()
    {
        try
        {
            // Initialize COM for this thread
            CoInitializeEx(IntPtr.Zero, COINIT_MULTITHREADED);

            using var duplication = new DesktopDuplication();
            var lastFrameTime = Stopwatch.GetTimestamp();

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    duplication.CaptureFrame();

                    var now = Stopwatch.GetTimestamp();
                    var deltaMs = (now - lastFrameTime) * 1000.0 / Stopwatch.Frequency;
                    lastFrameTime = now;

                    if (deltaMs > 0 && deltaMs < 1000) // Sanity: ignore >1s gaps
                    {
                        lock (_fpsLock)
                        {
                            _frameDeltas.Enqueue(deltaMs);
                            while (_frameDeltas.Count > MaxDeltaHistory)
                                _frameDeltas.Dequeue();

                            _totalFrames++;

                            // Calculate FPS from average frame time over recent history
                            if (_frameDeltas.Count >= 2)
                            {
                                double avgDelta = _frameDeltas.Average();
                                _currentFps = 1000.0 / avgDelta;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Frame capture failed, retrying...");
                    Thread.Sleep(100);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Screen FPS counter failed to initialize");
        }
        finally
        {
            CoUninitialize();
        }
    }

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    private const uint COINIT_MULTITHREADED = 0x0;

    public void Dispose()
    {
        StopCapture();
        _cts.Dispose();
    }
}

/// <summary>
/// DXGI Desktop Duplication wrapper — захватывает кадры экрана.
/// Использует COM interop с IDXGIOutputDuplication.
/// </summary>
internal sealed class DesktopDuplication : IDisposable
{
    private readonly IDXGIOutputDuplication* _duplication;
    private readonly IDXGIOutput1* _output1;
    private readonly IDXGIOutput* _output;
    private readonly IDXGIAdapter1* _adapter;
    private readonly ID3D11Device* _device;
    private bool _disposed;

    // SharpDX-style COM interop using raw pointers
    private static readonly Guid IID_IDXGIDevice = new("54ec77fa-1377-44e6-8c32-88fd5f448478");
    private static readonly Guid IID_IDXGIAdapter1 = new("29038f61-3839-4626-91fd-086879011a05");
    private static readonly Guid IID_IDXGIOutput = new("ae02eed4-bd79-437d-8696-23c6f6f3c658");
    private static readonly Guid IID_IDXGIOutput1 = new("00cddea8-939b-4b83-a340-a686226e6656");

    public DesktopDuplication()
    {
        // Create D3D11 device
        var hr = D3D11CreateDevice(
            IntPtr.Zero,
            D3D_DRIVER_TYPE_HARDWARE,
            IntPtr.Zero,
            0,
            null,
            0,
            D3D11_SDK_VERSION,
            out _device,
            null,
            out _);

        if (hr < 0) throw new COMException("Failed to create D3D11 device", hr);

        // Get DXGI device
        var dxgiDevice = (IDXGIDevice*)Marshal.GetComObjectForNativeVariant(
            (nint)_device).ToInterface(IID_IDXGIDevice);

        // Get adapter
        dxgiDevice->GetAdapter(out _adapter);

        // Get output (primary monitor)
        _adapter->EnumOutputs(0, out _output);

        // Get IDXGIOutput1
        var outputUnknown = (IUnknown*)_output;
        outputUnknown->QueryInterface(IID_IDXGIOutput1, out _output1);

        // Get IDXGIOutputDuplication
        var output1Unknown = (IUnknown*)_output1;
        output1Unknown->QueryInterface(IID_IDXGIOutput1, out _duplication);

        Marshal.ReleaseComObject((object)_output).Dispose();
    }

    public void CaptureFrame()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DesktopDuplication));

        var hr = _duplication->AcquireNextFrame(100, out var frameInfo, out var desktopResource);
        if (hr < 0)
        {
            if (hr == unchecked((int)0x887A0002)) // DXGI_ERROR_WAIT_TIMEOUT
                return;
            throw new COMException("AcquireNextFrame failed", hr);
        }

        try
        {
            // Frame acquired successfully — the timestamp is tracked by the caller
            // We just need to release it
        }
        finally
        {
            _duplication->ReleaseFrame();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_duplication != null) Marshal.ReleaseComObject((object)_duplication).Dispose();
        if (_output1 != null) Marshal.ReleaseComObject((object)_output1).Dispose();
        if (_output != null) Marshal.ReleaseComObject((object)_output).Dispose();
        if (_adapter != null) Marshal.ReleaseComObject((object)_adapter).Dispose();
        if (_device != null) Marshal.ReleaseComObject((object)_device).Dispose();
    }

    // DXGI structures
    [StructLayout(LayoutKind.Sequential)]
    internal struct DXGI_OUTDUPL_FRAME_INFO
    {
        public long LastPresentTime;
        public long LastMouseUpdateTime;
        public uint AccumulatedFrames;
        public bool RectsCoalesced;
        public bool ProtectedContentMaskedOut;
        public DXGI_OUTDUPL_POINTER_POSITION PositionPresent;
        public uint TotalMetadataBufferSize;
        public IntPtr DesktopSurface;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DXGI_OUTDUPL_POINTER_POSITION
    {
        public int PositionX;
        public int PositionY;
        public int Visible;
    }

    // P/Invoke
    [DllImport("d3d11.dll", PreserveSig = false)]
    private static extern void D3D11CreateDevice(
        IntPtr pAdapter,
        D3D_DRIVER_TYPE DriverType,
        IntPtr Software,
        uint Flags,
        D3D_FEATURE_LEVEL* pFeatureLevels,
        uint FeatureLevels,
        uint SDKVersion,
        out ID3D11Device* ppDevice,
        out D3D_FEATURE_LEVEL pFeatureLevel,
        out ID3D11DeviceContext* ppImmediateContext);

    private const uint D3D11_SDK_VERSION = 7;
    private const uint D3D_DRIVER_TYPE_HARDWARE = 1;

    [StructLayout(LayoutKind.Sequential)]
    internal struct D3D_FEATURE_LEVEL
    {
        public uint Level;
    }

    // COM interfaces (simplified vtable layouts)
    [Guid("db059506-27b3-42c4-b067-2f6a6ab8597e")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    internal interface IUnknown
    {
        [PreserveSig] int QueryInterface(ref Guid riid, out IntPtr ppvObject);
        [PreserveSig] uint AddRef();
        [PreserveSig] uint Release();
    }

    [Guid("54ec77fa-1377-44e6-8c32-88fd5f448478")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    internal interface IDXGIDevice
    {
        // IUnknown
        [PreserveSig] int QueryInterface(ref Guid riid, out IntPtr ppvObject);
        [PreserveSig] uint AddRef();
        [PreserveSig] uint Release();
        // IDXGIDevice
        [PreserveSig] int SetGPUThreadPriority(int Priority);
        [PreserveSig] int GetGPUThreadPriority(out int pPriority);
        [PreserveSig] int SetMaximumFrameLatency(uint MaxLatency);
        [PreserveSig] int GetMaximumFrameLatency(out uint pMaxLatency);
        [PreserveSig] int OfferResources(uint NumResources, IntPtr ppResources, DXGI_OFFER_RESOURCE_PRIORITY Priority);
        [PreserveSig] int ReclaimResources(uint NumResources, IntPtr ppResources, out bool pDiscarded);
        [PreserveSig] int EnqueueSetEvent(IntPtr hEvent);
        [PreserveSig] void Trim();
        [PreserveSig] int GetAdapter(out IDXGIAdapter1* pAdapter);
    }

    internal enum DXGI_OFFER_RESOURCE_PRIORITY
    {
        Low = 0,
        Normal = 1,
        High = 2
    }

    [Guid("29038f61-3839-4626-91fd-086879011a05")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    internal interface IDXGIAdapter1
    {
        // IUnknown
        [PreserveSig] int QueryInterface(ref Guid riid, out IntPtr ppvObject);
        [PreserveSig] uint AddRef();
        [PreserveSig] uint Release();
        // IDXGIAdapter
        [PreserveSig] int EnumOutputs(uint Output, out IDXGIOutput* ppOutput);
        [PreserveSig] int GetDesc(out DXGI_ADAPTER_DESC pDesc);
        [PreserveSig] int CheckInterfaceSupport(ref Guid InterfaceName, out long pUMDVersion);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DXGI_ADAPTER_DESC
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public IntPtr DedicatedVideoMemory;
        public IntPtr DedicatedSystemMemory;
        public IntPtr SharedSystemMemory;
        public long AdapterLuid;
    }

    [Guid("ae02eed4-bd79-437d-8696-23c6f6f3c658")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    internal interface IDXGIOutput
    {
        // IUnknown
        [PreserveSig] int QueryInterface(ref Guid riid, out IntPtr ppvObject);
        [PreserveSig] uint AddRef();
        [PreserveSig] uint Release();
        // IDXGIOutput
        [PreserveSig] int GetDesc(out DXGI_OUTPUT_DESC pDesc);
        [PreserveSig] int GetDisplayModeList(uint EnumFormat, uint Flags, out uint pNumModes, IntPtr pDesc);
        [PreserveSig] int FindClosestMatchingMode(IntPtr pModeToMatch, out IntPtr pClosestMatch, IntPtr pConcernedDevice);
        [PreserveSig] int WaitForVBlank();
        [PreserveSig] int TakeOwnership(IntPtr pDevice, bool Exclusive);
        [PreserveSig] void ReleaseOwnership();
        [PreserveSig] int GetGammaControlCapabilities(out IntPtr pGammaCapabilities);
        [PreserveSig] int SetGammaControl(IntPtr pArray);
        [PreserveSig] int GetGammaControl(out IntPtr pArray);
        [PreserveSig] int SetDisplaySurface(IntPtr pScanoutSurface);
        [PreserveSig] int GetDisplaySurfaceData(IntPtr pDestination);
        [PreserveSig] int GetFrameStatistics(out IntPtr pStats);
        [PreserveSig] int GetOverlayFilterOptions(out IntPtr pOptions);
        [PreserveSig] int SetOverlayFilterOptions(uint FilterOptions);
        [PreserveSig] int SetFrameStatistics(IntPtr pStats);
        [PreserveSig] int SetColorKey(IntPtr pColorKey);
        [PreserveSig] int SetCursorPosition(int X, int Y);
        [PreserveSig] int ShowCursor(bool Show);
        [PreserveSig] int GetDesc1(out DXGI_OUTPUT_DESC pDesc);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DXGI_OUTPUT_DESC
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.Struct)] public RECT DesktopCoordinates;
        public bool AttachedToDesktop;
        public DXGI_MODE_ROTATION Rotation;
        public IntPtr Monitor;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    internal enum DXGI_MODE_ROTATION
    {
        Unspecified = 0,
        Identity = 1,
        Rotate90 = 2,
        Rotate180 = 3,
        Rotate270 = 4
    }

    [Guid("00cddea8-939b-4b83-a340-a686226e6656")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    internal interface IDXGIOutput1
    {
        // IDXGIOutput vtable first
        [PreserveSig] int QueryInterface(ref Guid riid, out IntPtr ppvObject);
        [PreserveSig] uint AddRef();
        [PreserveSig] uint Release();
        [PreserveSig] int GetDesc(out DXGI_OUTPUT_DESC pDesc);
        [PreserveSig] int GetDisplayModeList(uint EnumFormat, uint Flags, out uint pNumModes, IntPtr pDesc);
        [PreserveSig] int FindClosestMatchingMode(IntPtr pModeToMatch, out IntPtr pClosestMatch, IntPtr pConcernedDevice);
        [PreserveSig] int WaitForVBlank();
        [PreserveSig] int TakeOwnership(IntPtr pDevice, bool Exclusive);
        [PreserveSig] void ReleaseOwnership();
        [PreserveSig] int GetGammaControlCapabilities(out IntPtr pGammaCapabilities);
        [PreserveSig] int SetGammaControl(IntPtr pArray);
        [PreserveSig] int GetGammaControl(out IntPtr pArray);
        [PreserveSig] int SetDisplaySurface(IntPtr pScanoutSurface);
        [PreserveSig] int GetDisplaySurfaceData(IntPtr pDestination);
        [PreserveSig] int GetFrameStatistics(out IntPtr pStats);
        [PreserveSig] int GetOverlayFilterOptions(out IntPtr pOptions);
        [PreserveSig] int SetOverlayFilterOptions(uint FilterOptions);
        [PreserveSig] int SetFrameStatistics(IntPtr pStats);
        [PreserveSig] int SetColorKey(IntPtr pColorKey);
        [PreserveSig] int SetCursorPosition(int X, int Y);
        [PreserveSig] int ShowCursor(bool Show);
        [PreserveSig] int GetDesc1(out DXGI_OUTPUT_DESC pDesc);
        // IDXGIOutput1
        [PreserveSig] int GetDisplayModeList1(uint EnumFormat, uint Flags, out uint pNumModes, IntPtr pDesc);
        [PreserveSig] int FindClosestMatchingMode1(IntPtr pModeToMatch, out IntPtr pClosestMatch, IntPtr pConcernedDevice);
        [PreserveSig] int GetDisplaySurfaceData1(IntPtr pDestination);
        [PreserveSig] int DuplicateOutput(IntPtr pDevice, out IDXGIOutputDuplication* ppOutputDuplication);
    }

    [Guid("191cfac3-a321-478d-9563-45b60e6b0e39")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    internal interface IDXGIOutputDuplication
    {
        [PreserveSig] int QueryInterface(ref Guid riid, out IntPtr ppvObject);
        [PreserveSig] uint AddRef();
        [PreserveSig] uint Release();
        [PreserveSig] void GetDesc(out DXGI_OUTDUPL_DESC pDesc);
        [PreserveSig] int AcquireNextFrame(uint TimeoutInMilliseconds, out DXGI_OUTDUPL_FRAME_INFO pFrameInfo, out IntPtr ppDesktopResource);
        [PreserveSig] int GetFrameDirtyRects(uint DirtyRectsBufferSize, IntPtr pDirtyRects, out uint pDirtyRectsBufferSizeRequired);
        [PreserveSig] int GetFrameMoveRects(uint MoveRectBufferSize, IntPtr pMoveRect, out uint pMoveRectsBufferSizeRequired);
        [PreserveSig] int GetFramePointerShape(uint PointerShapeBufferSize, IntPtr pPointerShape, out uint pPointerShapeBufferSizeRequired, out IntPtr pPointerShapeInfo);
        [PreserveSig] int MapDesktopSurface(out IntPtr pLockedRect);
        [PreserveSig] int UnMapDesktopSurface();
        [PreserveSig] int ReleaseFrame();
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DXGI_OUTDUPL_DESC
    {
        public DXGI_MODE_DESC ModeDesc;
        public DXGI_MODE_ROTATION Rotation;
        public bool DesktopImageInSystemMemory;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DXGI_MODE_DESC
    {
        public uint Width;
        public uint Height;
        public DXGI_RATIONAL RefreshRate;
        public uint Format;
        public uint ScanlineOrdering;
        public uint Scaling;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DXGI_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [Guid("a71b7e5b-4039-46e3-81f3-629a7613f831")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    internal interface ID3D11Device
    {
        [PreserveSig] int QueryInterface(ref Guid riid, out IntPtr ppvObject);
        [PreserveSig] uint AddRef();
        [PreserveSig] uint Release();
        [PreserveSig] int CreateBuffer(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppBuffer);
        [PreserveSig] int CreateTexture1D(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppTexture1D);
        [PreserveSig] int CreateTexture2D(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppTexture2D);
        [PreserveSig] int CreateTexture3D(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppTexture3D);
        [PreserveSig] int CreateShaderResourceView(IntPtr pResource, IntPtr pDesc, out IntPtr ppSRView);
        [PreserveSig] int CreateUnorderedAccessView(IntPtr pResource, IntPtr pDesc, out IntPtr ppUAView);
        [PreserveSig] int CreateRenderTargetView(IntPtr pResource, IntPtr pDesc, out IntPtr ppRTView);
        [PreserveSig] int CreateDepthStencilView(IntPtr pResource, IntPtr pDesc, out IntPtr ppDepthStencilView);
        [PreserveSig] int CreateInputLayout(IntPtr pInputElementDescs, uint NumElements, IntPtr pShaderBytecodeWithInputSignature, uint BytecodeLength, out IntPtr ppInputLayout);
        [PreserveSig] int CreateVertexShader(IntPtr pShaderBytecode, uint BytecodeLength, IntPtr pClassLinkage, out IntPtr ppVertexShader);
        [PreserveSig] int CreateGeometryShader(IntPtr pShaderBytecode, uint BytecodeLength, IntPtr pClassLinkage, out IntPtr ppGeometryShader);
        [PreserveSig] int CreateGeometryShaderWithStreamOutput(IntPtr pShaderBytecode, uint BytecodeLength, IntPtr pSODeclarationEntries, uint NumEntries, IntPtr pBufferStrides, uint NumStrides, uint RasterizedStream, IntPtr pClassLinkage, out IntPtr ppGeometryShader);
        [PreserveSig] int CreatePixelShader(IntPtr pShaderBytecode, uint BytecodeLength, IntPtr pClassLinkage, out IntPtr ppPixelShader);
        [PreserveSig] int CreateHullShader(IntPtr pShaderBytecode, uint BytecodeLength, IntPtr pClassLinkage, out IntPtr ppHullShader);
        [PreserveSig] int CreateDomainShader(IntPtr pShaderBytecode, uint BytecodeLength, IntPtr pClassLinkage, out IntPtr ppDomainShader);
        [PreserveSig] int CreateComputeShader(IntPtr pShaderBytecode, uint BytecodeLength, IntPtr pClassLinkage, out IntPtr ppComputeShader);
        [PreserveSig] int CreateClassLinkage(out IntPtr ppClassLinkage);
        [PreserveSig] int CreateBlendState(IntPtr pBlendStateDesc, out IntPtr ppBlendState);
        [PreserveSig] int CreateDepthStencilState(IntPtr pDepthStencilDesc, out IntPtr ppDepthStencilState);
        [PreserveSig] int CreateRasterizerState(IntPtr pRasterizerDesc, out IntPtr ppRasterizerState);
        [PreserveSig] int CreateSamplerState(IntPtr pSamplerDesc, out IntPtr ppSamplerState);
        [PreserveSig] int CreateQuery(IntPtr pQueryDesc, out IntPtr ppQuery);
        [PreserveSig] int CreatePredicate(IntPtr pPredicateDesc, out IntPtr ppPredicate);
        [PreserveSig] int CreateCounter(IntPtr pCounterDesc, out IntPtr ppCounter);
        [PreserveSig] int CreateDeferredContext(uint ContextFlags, out IntPtr ppDeferredContext);
        [PreserveSig] int OpenSharedResource(IntPtr hResource, ref Guid ReturnedInterface, out IntPtr ppResource);
        [PreserveSig] int CheckFormatSupport(uint Format, out uint pFormatSupport);
        [PreserveSig] int CheckMultisampleQualityLevels(uint Format, uint SampleCount, out uint pNumQualityLevels);
        [PreserveSig] int CheckCounterInfo(out IntPtr pCounterInfo);
        [PreserveSig] int CheckFeatureSupport(D3D11_FEATURE Feature, out IntPtr pFeatureSupportData, uint FeatureSupportDataSize);
        [PreserveSig] int GetPrivateData(ref Guid guid, out uint pDataSize, IntPtr pData);
        [PreserveSig] int SetPrivateData(ref Guid guid, uint DataSize, IntPtr pData);
        [PreserveSig] int SetPrivateDataInterface(ref Guid guid, IntPtr pData);
        [PreserveSig] int GetFeatureLevel(out D3D_FEATURE_LEVEL pFeatureLevel);
        [PreserveSig] int GetCreationFlags(out uint pFlags);
        [PreserveSig] int GetDeviceRemovedReason(out uint pReason);
        [PreserveSig] int GetImmediateContext(out IntPtr ppImmediateContext);
        [PreserveSig] int SetExceptionMode(uint RaiseFlags);
        [PreserveSig] int GetExceptionMode(out uint pRaiseFlags);
        [PreserveSig] int GetAdapter(out IDXGIAdapter1* pAdapter);
    }

    internal enum D3D11_FEATURE
    {
        D3D11_FEATURE_THREADING = 0,
        D3D11_FEATURE_DOUBLES = 1,
        D3D11_FEATURE_FORMAT_SUPPORT = 2,
        D3D11_FEATURE_FORMAT_SUPPORT2 = 3,
        D3D11_FEATURE_D3D10_X_OPTIONS = 4,
        D3D11_FEATURE_D3D11_OPTIONS = 5,
        D3D11_FEATURE_ARCHITECTURE_INFO = 6,
        D3D11_FEATURE_D3D9_OPTIONS = 7,
        D3D11_FEATURE_SHADER_MIN_PRECISION_SUPPORT = 8,
        D3D11_FEATURE_D3D11_OPTIONS1 = 9,
        D3D11_FEATURE_D3D9_OPTIONS1 = 10,
        D3D11_FEATURE_D3D11_OPTIONS2 = 11,
        D3D11_FEATURE_D3D11_OPTIONS3 = 12,
        D3D11_FEATURE_GPU_VIRTUAL_ADDRESS = 13,
        D3D11_FEATURE_D3D11_OPTIONS4 = 14,
        D3D11_FEATURE_SHADER_CACHE = 15,
        D3D11_FEATURE_D3D11_OPTIONS5 = 16
    }
}
