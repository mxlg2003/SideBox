using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace WinDock.Shell;

internal static class DwmBackdrop
{
    private const int GwlExStyle = -20;
    private const int WsExLayered = 0x00080000;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmsbtNone = 1;
    private const int WcaAccentPolicy = 19;
    private const int AccentDisabled = 0;

    public static bool TryApply(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            var source = HwndSource.FromHwnd(hwnd);
            if (source?.CompositionTarget is not null)
            {
                source.CompositionTarget.BackgroundColor = Colors.Transparent;
            }

            ForceLayered(hwnd);

            var none = DwmsbtNone;
            DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref none, sizeof(int));
            ClearAccent(hwnd);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void ForceLayered(IntPtr hwnd)
    {
        var ex = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        if ((ex & WsExLayered) == 0)
        {
            SetWindowLongPtr(hwnd, GwlExStyle, (IntPtr)(ex | WsExLayered));
        }
    }

    private static void ClearAccent(IntPtr hwnd)
    {
        var policy = new AccentPolicy { AccentState = AccentDisabled };
        var size = Marshal.SizeOf<AccentPolicy>();
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, ptr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                Data = ptr,
                SizeOfData = size
            };
            _ = SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }
}
