using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GTA5Optimizer.UI.Services;

/// <summary>
/// Глобальные горячие клавиши
/// </summary>
public class HotkeyService : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_ALT = 0x0001;
    private const uint VK_O = 0x4F;
    private const int HOTKEY_ID = 9000;

    private IntPtr _windowHandle;
    private HwndSource? _source;
    private bool _registered;

    public event Action? OptimizeRequested;

    public void Initialize(Window window)
    {
        _windowHandle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(HwndHook);

        try
        {
            _registered = RegisterHotKey(_windowHandle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_O);
        }
        catch { }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            OptimizeRequested?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            _registered = false;
        }
        _source?.RemoveHook(HwndHook);
    }
}
