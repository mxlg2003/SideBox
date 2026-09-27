using System.Runtime.InteropServices;
using WinDock.Core;

namespace WinDock.Shell;

public sealed class Win32AppBarService : IAppBarService
{
    private const uint AbmNew = 0x00000000;
    private const uint AbmRemove = 0x00000001;
    private const uint AbmQueryPos = 0x00000002;
    private const uint AbmSetPos = 0x00000003;
    private const uint AbmActivate = 0x00000006;
    private const uint AbmWindowPosChanged = 0x00000009;

    private const int AbeLeft = 0;
    private const int AbeTop = 1;
    private const int AbeRight = 2;
    private const int AbeBottom = 3;

    private const int WmUser = 0x0400;
    public const int WmAppBarCallback = WmUser + 1025;

    private readonly object _gate = new();
    private nint _registeredHwnd;
    private AppBarData _data;

    public bool IsRegistered
    {
        get
        {
            lock (_gate)
            {
                return _registeredHwnd != nint.Zero;
            }
        }
    }

    public AppBarRegistrationResult TryRegister(AppBarRequest request)
    {
        if (request.WindowHandle == nint.Zero)
        {
            return AppBarRegistrationResult.Fail("Invalid window handle.");
        }

        lock (_gate)
        {
            try
            {
                if (_registeredHwnd != nint.Zero && _registeredHwnd != request.WindowHandle)
                {
                    Remove();
                }

                _data = new AppBarData
                {
                    cbSize = Marshal.SizeOf<AppBarData>(),
                    hWnd = request.WindowHandle,
                    uCallbackMessage = WmAppBarCallback
                };

                if (_registeredHwnd == nint.Zero)
                {
                    var result = SHAppBarMessage(AbmNew, ref _data);
                    if (result == nint.Zero && Marshal.GetLastWin32Error() != 0)
                    {
                        return AppBarRegistrationResult.Fail("SHAppBarMessage(ABM_NEW) failed.");
                    }

                    _registeredHwnd = request.WindowHandle;
                }

                var uEdge = request.Edge switch
                {
                    DockEdge.Left => AbeLeft,
                    DockEdge.Top => AbeTop,
                    DockEdge.Right => AbeRight,
                    _ => AbeBottom
                };

                _data.uEdge = (uint)uEdge;
                _data.rc = new NativeRect
                {
                    Left = (int)Math.Round(request.Proposed.X),
                    Top = (int)Math.Round(request.Proposed.Y),
                    Right = (int)Math.Round(request.Proposed.Right),
                    Bottom = (int)Math.Round(request.Proposed.Bottom)
                };

                // Query desired position
                _ = SHAppBarMessage(AbmQueryPos, ref _data);

                var reqWidth = (int)Math.Round(request.Proposed.Width);
                var reqHeight = (int)Math.Round(request.Proposed.Height);

                switch (uEdge)
                {
                    case AbeTop:
                        _data.rc.Bottom = _data.rc.Top + reqHeight;
                        break;
                    case AbeBottom:
                        _data.rc.Top = _data.rc.Bottom - reqHeight;
                        break;
                    case AbeLeft:
                        _data.rc.Right = _data.rc.Left + reqWidth;
                        break;
                    case AbeRight:
                        _data.rc.Left = _data.rc.Right - reqWidth;
                        break;
                }

                // Set approved position and reserve work area
                _ = SHAppBarMessage(AbmSetPos, ref _data);

                var approved = new RectLogical(
                    _data.rc.Left,
                    _data.rc.Top,
                    Math.Max(1, _data.rc.Right - _data.rc.Left),
                    Math.Max(1, _data.rc.Bottom - _data.rc.Top));

                return new AppBarRegistrationResult
                {
                    Succeeded = true,
                    Approved = approved
                };
            }
            catch (Exception ex)
            {
                Remove();
                return AppBarRegistrationResult.Fail(ex.Message);
            }
        }
    }

    public void NotifyActivate()
    {
        lock (_gate)
        {
            if (_registeredHwnd != nint.Zero)
            {
                _ = SHAppBarMessage(AbmActivate, ref _data);
            }
        }
    }

    public void NotifyWindowPosChanged()
    {
        lock (_gate)
        {
            if (_registeredHwnd != nint.Zero)
            {
                _ = SHAppBarMessage(AbmWindowPosChanged, ref _data);
            }
        }
    }

    public void Remove()
    {
        lock (_gate)
        {
            if (_registeredHwnd == nint.Zero)
            {
                return;
            }

            try
            {
                _data.cbSize = Marshal.SizeOf<AppBarData>();
                _ = SHAppBarMessage(AbmRemove, ref _data);
            }
            catch (Exception)
            {
            }
            finally
            {
                _registeredHwnd = nint.Zero;
            }
        }
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern nint SHAppBarMessage(uint dwMessage, ref AppBarData pData);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public int cbSize;
        public nint hWnd;
        public int uCallbackMessage;
        public uint uEdge;
        public NativeRect rc;
        public nint lParam;
    }
}
