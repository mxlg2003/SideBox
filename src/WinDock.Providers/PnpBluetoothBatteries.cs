using System.Runtime.InteropServices;
using System.Text;
using WinDock.Core;

namespace WinDock.Providers;

/// <summary>
/// Bluetooth headphone battery is on PnP nodes (Hands-Free AG), not WinRT BluetoothDevice.
/// DIGCF_PRESENT still lists paired devices after they disconnect; last-known percent stays.
/// </summary>
internal static class PnpBluetoothBatteries
{
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfAllClasses = 0x00000004;
    private const uint DevPropTypeByte = 0x00000003;
    private const uint DevPropTypeBoolean = 0x00000011;
    private const uint DevPropTypeString = 0x00000012;
    private static readonly IntPtr InvalidHandle = new(-1);

    private static readonly DevPropKey BatteryLevel = new(new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), 2);
    private static readonly DevPropKey FriendlyName = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
    private static readonly DevPropKey Name = new(new Guid("B725F130-47EF-101A-A5F1-02608C9EEBAC"), 10);
    private static readonly DevPropKey IsConnected = new(new Guid("83DA6326-97A6-4088-9453-A1923F573B29"), 15);
    private static readonly DevPropKey DeviceAddress = new(new Guid("2BD67D8B-8BEB-48D5-87E0-6CDA3428040A"), 1);

    public static IReadOnlyList<PowerDeviceSnapshot> Read()
    {
        try
        {
            var connected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<Candidate>();
            foreach (var enumerator in new[] { "BTHENUM", "BTHLE", "BTHHFENUM" })
            {
                Scan(enumerator, connected, candidates);
            }

            var devices = new List<PowerDeviceSnapshot>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidates)
            {
                if (!PowerFormat.IsLivePeripheral(candidate.Connected, candidate.Address, connected))
                {
                    continue;
                }

                var name = PowerFormat.CleanDeviceName(candidate.Name);
                if (!seen.Add(name))
                {
                    continue;
                }

                devices.Add(new PowerDeviceSnapshot
                {
                    Id = candidate.Id ?? name,
                    Name = name,
                    Kind = PowerFormat.KindFromName(name),
                    Percent = candidate.Percent
                });
            }

            return devices;
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static void Scan(string enumerator, HashSet<string> connected, List<Candidate> candidates)
    {
        var set = SetupDiGetClassDevsW(IntPtr.Zero, enumerator, IntPtr.Zero, DigcfPresent | DigcfAllClasses);
        if (set == IntPtr.Zero || set == InvalidHandle)
        {
            return;
        }

        try
        {
            var cbSize = (uint)Marshal.SizeOf<SpDevinfoData>();
            for (uint index = 0; ; index++)
            {
                var info = new SpDevinfoData { cbSize = cbSize };
                if (!SetupDiEnumDeviceInfo(set, index, ref info))
                {
                    break;
                }

                var instanceId = InstanceId(set, ref info);
                var address = AddressOf(set, ref info, instanceId);
                var connectedFlag = ReadConnected(set, ref info);
                if (connectedFlag == true && address is not null)
                {
                    connected.Add(address);
                }

                if (!TryByte(set, ref info, BatteryLevel, out var percent))
                {
                    continue;
                }

                if (!TryString(set, ref info, FriendlyName, out var rawName)
                    && !TryString(set, ref info, Name, out rawName))
                {
                    continue;
                }

                if (IsJunk(rawName))
                {
                    continue;
                }

                candidates.Add(new Candidate(instanceId, rawName, percent, address, connectedFlag));
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }
    }

    private static string? AddressOf(IntPtr set, ref SpDevinfoData info, string? instanceId)
    {
        if (TryString(set, ref info, DeviceAddress, out var raw))
        {
            var fromProperty = PowerFormat.NormalizeBluetoothAddress(raw);
            if (fromProperty is not null)
            {
                return fromProperty;
            }
        }

        return AddressFromInstance(instanceId);
    }

    private static string? AddressFromInstance(string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        const string marker = "DEV_";
        var index = instanceId.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index >= 0 && TryMac(instanceId, index + marker.Length, out var fromDev))
        {
            return fromDev;
        }

        var lastAmp = instanceId.LastIndexOf('&');
        return lastAmp >= 0 && TryMac(instanceId, lastAmp + 1, out var fromTail) ? fromTail : null;
    }

    private static bool TryMac(string text, int start, out string mac)
    {
        mac = "";
        if (start < 0 || start + 12 > text.Length)
        {
            return false;
        }

        for (var i = 0; i < 12; i++)
        {
            if (!Uri.IsHexDigit(text[start + i]))
            {
                return false;
            }
        }

        if (start + 12 < text.Length && Uri.IsHexDigit(text[start + 12]))
        {
            return false;
        }

        mac = text.Substring(start, 12).ToUpperInvariant();
        return true;
    }

    private static bool? ReadConnected(IntPtr set, ref SpDevinfoData info) =>
        TryBool(set, ref info, IsConnected, out var value) ? value : null;

    private static bool IsJunk(string name) =>
        string.IsNullOrWhiteSpace(name)
        || Contains(name, "Enumerator", "RFCOMM Protocol", "Generic Attribute", "Personal Area Network");

    private static bool Contains(string name, params string[] parts) =>
        parts.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase));

    private static bool TryByte(IntPtr set, ref SpDevinfoData info, DevPropKey key, out byte value)
    {
        value = 0;
        var buffer = new byte[4];
        if (!SetupDiGetDevicePropertyW(set, ref info, key, out var type, buffer, (uint)buffer.Length, out var needed, 0))
        {
            if (needed <= buffer.Length)
            {
                return false;
            }

            buffer = new byte[needed];
            if (!SetupDiGetDevicePropertyW(set, ref info, key, out type, buffer, (uint)buffer.Length, out needed, 0))
            {
                return false;
            }
        }

        if (needed < 1)
        {
            return false;
        }

        value = buffer[0];
        return type is DevPropTypeByte or 0x00000007 && value <= 100;
    }

    private static bool TryBool(IntPtr set, ref SpDevinfoData info, DevPropKey key, out bool value)
    {
        value = false;
        var buffer = new byte[4];
        if (!SetupDiGetDevicePropertyW(set, ref info, key, out var type, buffer, (uint)buffer.Length, out var needed, 0)
            || type != DevPropTypeBoolean
            || needed < 1)
        {
            return false;
        }

        value = buffer[0] != 0;
        return true;
    }

    private static bool TryString(IntPtr set, ref SpDevinfoData info, DevPropKey key, out string value)
    {
        value = "";
        SetupDiGetDevicePropertyW(set, ref info, key, out _, null, 0, out var needed, 0);
        if (needed == 0)
        {
            return false;
        }

        var buffer = new byte[needed];
        if (!SetupDiGetDevicePropertyW(set, ref info, key, out var type, buffer, (uint)buffer.Length, out _, 0)
            || type != DevPropTypeString)
        {
            return false;
        }

        value = Encoding.Unicode.GetString(buffer).TrimEnd('\0').Trim();
        return value.Length > 0;
    }

    private static string? InstanceId(IntPtr set, ref SpDevinfoData info)
    {
        var buffer = new StringBuilder(512);
        return SetupDiGetDeviceInstanceIdW(set, ref info, buffer, (uint)buffer.Capacity, IntPtr.Zero)
            ? buffer.ToString()
            : null;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string? enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SpDevinfoData deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDevicePropertyW(
        IntPtr deviceInfoSet,
        ref SpDevinfoData deviceInfoData,
        in DevPropKey propertyKey,
        out uint propertyType,
        byte[]? propertyBuffer,
        uint propertyBufferSize,
        out uint requiredSize,
        uint flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInstanceIdW(
        IntPtr deviceInfoSet,
        ref SpDevinfoData deviceInfoData,
        StringBuilder deviceInstanceId,
        uint deviceInstanceIdSize,
        IntPtr requiredSize);

    private readonly record struct Candidate(string? Id, string Name, byte Percent, string? Address, bool? Connected);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SpDevinfoData
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly struct DevPropKey(Guid fmtid, uint pid)
    {
        public readonly Guid Fmtid = fmtid;
        public readonly uint Pid = pid;
    }
}
