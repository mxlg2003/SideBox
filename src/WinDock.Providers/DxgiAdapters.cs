using System.Runtime.InteropServices;
using WinDock.Core;

namespace WinDock.Providers;

internal static class DxgiAdapters
{
    private static readonly Guid FactoryIid = new("7b7166ec-21c7-44ae-b21a-c9ae321ae369");

    public static IReadOnlyList<GpuAdapterInfo> TryRead()
    {
        IDXGIFactory? factory = null;
        try
        {
            var hr = CreateDXGIFactory(FactoryIid, out var created);
            factory = created;
            if (hr < 0 || factory is null)
            {
                return [];
            }

            var list = new List<GpuAdapterInfo>();
            for (uint i = 0; ; i++)
            {
                IDXGIAdapter? adapter = null;
                try
                {
                    if (factory.EnumAdapters(i, out adapter) < 0 || adapter is null)
                    {
                        break;
                    }

                    if (adapter.GetDesc(out var desc) < 0)
                    {
                        continue;
                    }

                    if (desc.VendorId == 0x1414)
                    {
                        continue;
                    }

                    var name = string.IsNullOrWhiteSpace(desc.Description)
                        ? $"GPU {i}"
                        : desc.Description.Trim('\0', ' ');
                    list.Add(new GpuAdapterInfo
                    {
                        Id = LuidKey(desc.LuidLow, desc.LuidHigh),
                        Name = name
                    });
                }
                finally
                {
                    if (adapter is not null)
                    {
                        Marshal.ReleaseComObject(adapter);
                    }
                }
            }

            return list;
        }
        catch (Exception)
        {
            return [];
        }
        finally
        {
            if (factory is not null)
            {
                Marshal.ReleaseComObject(factory);
            }
        }
    }

    public static string LuidKey(uint low, int high) =>
        $"luid_0x{high:X8}_0x{low:X8}";

    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory([In] in Guid riid, out IDXGIFactory factory);

    [ComImport]
    [Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIFactory
    {
        void SetPrivateData();
        void SetPrivateDataInterface();
        void GetPrivateData();
        void GetParent();
        [PreserveSig]
        int EnumAdapters(uint adapter, out IDXGIAdapter adapterOut);
        void MakeWindowAssociation();
        void GetWindowAssociation();
        void CreateSwapChain();
        void CreateSoftwareAdapter();
    }

    [ComImport]
    [Guid("2411e7e1-12ac-4ccf-bd14-9798e8534dc0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIAdapter
    {
        void SetPrivateData();
        void SetPrivateDataInterface();
        void GetPrivateData();
        void GetParent();
        void EnumOutputs();
        [PreserveSig]
        int GetDesc(out DxgiAdapterDesc desc);
        void CheckInterfaceSupport();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DxgiAdapterDesc
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public uint LuidLow;
        public int LuidHigh;
    }
}
