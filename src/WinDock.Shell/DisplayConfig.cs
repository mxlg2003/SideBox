using System.Runtime.InteropServices;

namespace WinDock.Shell;

internal static class DisplayConfig
{
    private const uint QdcOnlyActivePaths = 2;
    private const int DeviceInfoGetSourceName = 1;
    private const int DeviceInfoGetTargetName = 2;

    public static string? DevicePathFor(string gdiDeviceName)
    {
        if (string.IsNullOrWhiteSpace(gdiDeviceName))
        {
            return null;
        }

        try
        {
            if (GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out var pathCount, out var modeCount) != 0
                || pathCount == 0)
            {
                return null;
            }

            var paths = new PathInfo[pathCount];
            var modes = new ModeInfo[Math.Max(1, modeCount)];
            var pathLen = pathCount;
            var modeLen = modeCount;
            if (QueryDisplayConfig(QdcOnlyActivePaths, ref pathLen, paths, ref modeLen, modes, nint.Zero) != 0)
            {
                return null;
            }

            for (var i = 0; i < pathLen; i++)
            {
                var source = new SourceDeviceName();
                source.Header.Type = DeviceInfoGetSourceName;
                source.Header.Size = (uint)Marshal.SizeOf<SourceDeviceName>();
                source.Header.AdapterId = paths[i].SourceInfo.AdapterId;
                source.Header.Id = paths[i].SourceInfo.Id;
                if (DisplayConfigGetDeviceInfo(ref source) != 0)
                {
                    continue;
                }

                var gdi = source.ViewGdiDeviceName?.TrimEnd('\0');
                if (!string.Equals(gdi, gdiDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var target = new TargetDeviceName();
                target.Header.Type = DeviceInfoGetTargetName;
                target.Header.Size = (uint)Marshal.SizeOf<TargetDeviceName>();
                target.Header.AdapterId = paths[i].TargetInfo.AdapterId;
                target.Header.Id = paths[i].TargetInfo.Id;
                if (DisplayConfigGetDeviceInfo(ref target) != 0)
                {
                    return null;
                }

                var path = target.MonitorDevicePath?.TrimEnd('\0');
                return string.IsNullOrWhiteSpace(path) ? null : path;
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] PathInfo[] pathInfoArray,
        ref uint numModeArrayElements,
        [Out] ModeInfo[] modeInfoArray,
        nint currentTopologyId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DisplayConfigGetDeviceInfo(ref SourceDeviceName request);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DisplayConfigGetDeviceInfo(ref TargetDeviceName request);

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PathInfo
    {
        public PathSourceInfo SourceInfo;
        public PathTargetInfo TargetInfo;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PathSourceInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public Rational RefreshRate;
        public uint ScanLineOrdering;
        public bool TargetAvailable;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rational
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ModeInfo
    {
        public uint InfoType;
        public uint Id;
        public Luid AdapterId;
        public ModeInfoUnion Info;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct ModeInfoUnion
    {
        [FieldOffset(0)] public TargetMode TargetMode;
        [FieldOffset(0)] public SourceMode SourceMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TargetMode
    {
        public ulong PixelRate;
        public uint HSyncFreqNum;
        public uint HSyncFreqDen;
        public uint VSyncFreqNum;
        public uint VSyncFreqDen;
        public uint ActiveWidth;
        public uint ActiveHeight;
        public uint TotalWidth;
        public uint TotalHeight;
        public uint VideoStandard;
        public uint ScanLineOrdering;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SourceMode
    {
        public uint Width;
        public uint Height;
        public uint PixelFormat;
        public PointL Position;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointL
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInfoHeader
    {
        public int Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SourceDeviceName
    {
        public DeviceInfoHeader Header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ViewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TargetDeviceName
    {
        public DeviceInfoHeader Header;
        public uint Flags;
        public uint OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string MonitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string MonitorDevicePath;
    }
}
