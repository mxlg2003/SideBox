using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using WinDock.Core;

namespace WinDock.App;

public sealed class NativeTrayIcon : IDisposable
{
    private const int WmUser = 0x0400;
    private const int WmTrayCallback = WmUser + 1024;
    private const int NimAdd = 0x00000000;
    private const int NimModify = 0x00000001;
    private const int NimDelete = 0x00000002;
    private const int NifMessage = 0x00000001;
    private const int NifIcon = 0x00000002;
    private const int NifTip = 0x00000004;
    private const int NifInfo = 0x00000010;
    private const int NiifInfo = 0x00000001;
    private const int NiifWarning = 0x00000002;

    private const int WmLButtonUp = 0x0202;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmRButtonUp = 0x0205;

    private readonly uint _taskbarCreated;
    private readonly HwndSource _source;
    private NotifyIconData _data;
    private bool _added;
    private bool _disposed;
    private bool _ownsIcon;
    private string _text = ProductInfo.Name;

    public event EventHandler? LeftClick;
    public event EventHandler? DoubleClick;
    public event EventHandler? RightClick;

    public NativeTrayIcon()
    {
        var parameters = new HwndSourceParameters("WinDock_Tray_Host")
        {
            WindowStyle = 0,
            Width = 0,
            Height = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");

        _data = new NotifyIconData
        {
            cbSize = Marshal.SizeOf<NotifyIconData>(),
            hWnd = _source.Handle,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip,
            uCallbackMessage = WmTrayCallback,
            hIcon = LoadTrayIcon(out _ownsIcon),
            szTip = _text
        };

        _added = Shell_NotifyIcon(NimAdd, ref _data);
    }

    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;
            if (_text.Length > 127)
            {
                _text = _text[..127];
            }

            if (!_added)
            {
                return;
            }

            _data.uFlags = NifTip;
            _data.szTip = _text;
            Shell_NotifyIcon(NimModify, ref _data);
        }
    }

    public void ShowBalloon(string title, string text, bool isWarning = false)
    {
        if (!_added)
        {
            return;
        }

        _data.uFlags = NifInfo;
        _data.szInfoTitle = title ?? string.Empty;
        _data.szInfo = text ?? string.Empty;
        _data.dwInfoFlags = isWarning ? NiifWarning : NiifInfo;
        Shell_NotifyIcon(NimModify, ref _data);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_added)
        {
            Shell_NotifyIcon(NimDelete, ref _data);
            _added = false;
        }

        if (_ownsIcon && _data.hIcon != nint.Zero)
        {
            DestroyIcon(_data.hIcon);
            _data.hIcon = nint.Zero;
            _ownsIcon = false;
        }

        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == (int)_taskbarCreated)
        {
            Rebuild();
            handled = true;
            return nint.Zero;
        }

        if (msg == WmTrayCallback)
        {
            var eventMsg = (int)lParam;
            if (eventMsg == WmLButtonUp)
            {
                LeftClick?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
            else if (eventMsg == WmLButtonDblClk)
            {
                DoubleClick?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
            else if (eventMsg == WmRButtonUp)
            {
                SetForegroundWindow(hwnd);
                RightClick?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
        }

        return nint.Zero;
    }

    private void Rebuild()
    {
        if (_disposed)
        {
            return;
        }

        if (_added)
        {
            Shell_NotifyIcon(NimDelete, ref _data);
            _added = false;
        }

        _data.uFlags = NifMessage | NifIcon | NifTip;
        _data.szTip = _text;
        _added = Shell_NotifyIcon(NimAdd, ref _data);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NotifyIconData lpdata);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint PrivateExtractIcons(
        string szFileName,
        int nIconIndex,
        int cxIcon,
        int cyIcon,
        nint[] phicon,
        int[] piconid,
        uint nIcons,
        uint flags);

    [DllImport("user32.dll")]
    private static extern nint LoadIcon(nint hInstance, nint lpIconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint hIcon);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private static nint LoadTrayIcon(out bool owns)
    {
        owns = false;
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path))
        {
            var size = GetSystemMetrics(SmCxSmIcon);
            if (size < 16)
            {
                size = 16;
            }

            var icons = new nint[1];
            var ids = new int[1];
            var extracted = PrivateExtractIcons(path, 0, size, size, icons, ids, 1, 0);
            if (extracted > 0 && icons[0] != nint.Zero)
            {
                owns = true;
                return icons[0];
            }
        }

        return LoadIcon(nint.Zero, 32512);
    }

    private const int SmCxSmIcon = 49;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public nint hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }
}
