using System.Runtime.InteropServices;

namespace WinDock.Providers;

/// <summary>
/// One PDH wildcard query for GPU engine utilization. Instance names are filtered
/// while walking the result array, so the process does not keep a counter object per engine.
/// </summary>
internal sealed class GpuEngineQuery : IDisposable
{
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhMoreData = 0x800007D2;
    private const uint PdhNoData = 0x800007D5;
    private const uint PdhRetry = 0x800007D4;
    private const uint PdhInvalidData = 0xC0000BC6;

    private IntPtr _query;
    private IntPtr _counter;
    private IntPtr _items;
    private uint _itemsBytes;
    private long _openedAt;
    private int _failures;

    public bool IsOpen => _query != IntPtr.Zero;

    public bool TryOpen()
    {
        Close();
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0 || _query == IntPtr.Zero)
        {
            _query = IntPtr.Zero;
            return false;
        }

        if (PdhAddEnglishCounter(_query, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out _counter) != 0)
        {
            Close();
            return false;
        }

        _ = PdhCollectQueryData(_query);
        _openedAt = Environment.TickCount64;
        _failures = 0;
        return true;
    }

    public bool TryCollect(string luid, bool splitEngines, out EngineReading reading)
    {
        reading = default;
        if (_query == IntPtr.Zero || string.IsNullOrWhiteSpace(luid))
        {
            return false;
        }

        var status = PdhCollectQueryData(_query);
        if (status != 0)
        {
            NoteFailure(status);
            return false;
        }

        if (Environment.TickCount64 - _openedAt < 200)
        {
            return false;
        }

        _failures = 0;
        if (!TryFormat(out var count))
        {
            return false;
        }

        double max = 0, max3d = 0, maxCopy = 0, maxVideo = 0, maxCompute = 0;
        var any = false;
        var any3d = false;
        var anyCopy = false;
        var anyVideo = false;
        var anyCompute = false;
        var size = Marshal.SizeOf<PdhItem>();
        for (var i = 0; i < count; i++)
        {
            var item = Marshal.PtrToStructure<PdhItem>(_items + (i * size));
            if ((item.Status & 0x80000000) != 0 || item.Name == IntPtr.Zero)
            {
                continue;
            }

            if (!NativeContains(item.Name, luid))
            {
                continue;
            }

            var value = Math.Clamp(item.Value, 0, 100);
            any = true;
            max = Math.Max(max, value);
            if (!splitEngines)
            {
                continue;
            }

            switch (Classify(item.Name))
            {
                case EngineKind.Copy:
                    anyCopy = true;
                    maxCopy = Math.Max(maxCopy, value);
                    break;
                case EngineKind.Video:
                    anyVideo = true;
                    maxVideo = Math.Max(maxVideo, value);
                    break;
                case EngineKind.Compute:
                    anyCompute = true;
                    maxCompute = Math.Max(maxCompute, value);
                    break;
                default:
                    any3d = true;
                    max3d = Math.Max(max3d, value);
                    break;
            }
        }

        if (!any)
        {
            return false;
        }

        reading = new EngineReading(
            max,
            splitEngines && any3d ? max3d : null,
            splitEngines && anyCopy ? maxCopy : null,
            splitEngines && anyVideo ? maxVideo : null,
            splitEngines && anyCompute ? maxCompute : null);
        return true;
    }

    public void Dispose() => Close();

    private void NoteFailure(uint status)
    {
        if (status is PdhNoData or PdhRetry or PdhInvalidData)
        {
            return;
        }

        if (++_failures < 3)
        {
            return;
        }

        _failures = 0;
        TryOpen();
    }

    private bool TryFormat(out int count)
    {
        count = 0;
        var bytes = _itemsBytes == 0 ? 256u * 1024u : _itemsBytes;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            EnsureBuffer(bytes);
            var size = bytes;
            var status = PdhGetFormattedCounterArray(_counter, PdhFmtDouble, ref size, out var itemCount, _items);
            if (status == PdhMoreData)
            {
                bytes = Math.Max(size + 4096, bytes * 2);
                continue;
            }

            if (status != 0)
            {
                return false;
            }

            count = (int)itemCount;
            return true;
        }

        return false;
    }

    private void EnsureBuffer(uint bytes)
    {
        if (_items != IntPtr.Zero && _itemsBytes >= bytes)
        {
            return;
        }

        if (_items != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_items);
        }

        _items = Marshal.AllocHGlobal((int)bytes);
        _itemsBytes = bytes;
    }

    private void Close()
    {
        if (_query != IntPtr.Zero)
        {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
            _counter = IntPtr.Zero;
        }

        if (_items != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_items);
            _items = IntPtr.Zero;
            _itemsBytes = 0;
        }
    }

    private static EngineKind Classify(IntPtr name)
    {
        const string marker = "engtype_";
        var offset = 0;
        while (offset < 4096)
        {
            var ch = (char)Marshal.ReadInt16(name, offset);
            if (ch == '\0')
            {
                break;
            }

            if (StartsWith(name, offset, marker))
            {
                return KindAfter(name, offset + (marker.Length * 2));
            }

            offset += 2;
        }

        return EngineKind.ThreeD;
    }

    private static EngineKind KindAfter(IntPtr name, int byteOffset)
    {
        Span<char> token = stackalloc char[24];
        var length = 0;
        while (length < token.Length)
        {
            var ch = (char)Marshal.ReadInt16(name, byteOffset + (length * 2));
            if (ch == '\0' || ch == '_')
            {
                break;
            }

            token[length++] = ch;
        }

        var type = token[..length];
        if (Contains(type, "Copy"))
        {
            return EngineKind.Copy;
        }

        if (Contains(type, "Video") || Contains(type, "Codec"))
        {
            return EngineKind.Video;
        }

        if (Contains(type, "Compute"))
        {
            return EngineKind.Compute;
        }

        return EngineKind.ThreeD;
    }

    private static bool Contains(ReadOnlySpan<char> text, string needle)
    {
        if (needle.Length == 0 || text.Length < needle.Length)
        {
            return false;
        }

        for (var i = 0; i <= text.Length - needle.Length; i++)
        {
            if (text.Slice(i, needle.Length).Equals(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NativeContains(IntPtr unicode, string needle)
    {
        var offset = 0;
        while (true)
        {
            var ch = (char)Marshal.ReadInt16(unicode, offset);
            if (ch == '\0')
            {
                return false;
            }

            if (StartsWith(unicode, offset, needle))
            {
                return true;
            }

            offset += 2;
            if (offset > 4096)
            {
                return false;
            }
        }
    }

    private static bool StartsWith(IntPtr unicode, int byteOffset, string needle)
    {
        for (var i = 0; i < needle.Length; i++)
        {
            var ch = (char)Marshal.ReadInt16(unicode, byteOffset + (i * 2));
            if (ch == '\0')
            {
                return false;
            }

            if (char.ToUpperInvariant(ch) != char.ToUpperInvariant(needle[i]))
            {
                return false;
            }
        }

        return true;
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    private struct PdhItem
    {
        [FieldOffset(0)]
        public IntPtr Name;

        [FieldOffset(8)]
        public uint Status;

        [FieldOffset(16)]
        public double Value;
    }

    private enum EngineKind
    {
        ThreeD,
        Copy,
        Video,
        Compute
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArray(
        IntPtr counter,
        uint format,
        ref uint bufferSize,
        out uint itemCount,
        IntPtr itemBuffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}

internal readonly record struct EngineReading(
    double Percent,
    double? ThreeD,
    double? Copy,
    double? Video,
    double? Compute);
