using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using WinDock.Core;

namespace WinDock.Providers;

public sealed class Win32ShellLaunch : IShellLaunch
{
    private const uint SlrNoUi = 0x0001;
    private const uint SlrNoUpdate = 0x0008;
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiSysIconIndex = 0x000004000;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const uint ShgsiIcon = 0x00000100;
    private const uint ShgsiSysIconIndex = 0x00004000;
    private const int SiidApplication = 2;
    private const int SiidFolder = 3;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeNormal = 0x00000080;
    private const int ShilExtraLarge = 0x2;
    private const int IldTransparent = 0x00000001;
    private const byte StockIconMinOpaque = 80;

    private readonly string _cacheDirectory;

    public Win32ShellLaunch(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
        Directory.CreateDirectory(_cacheDirectory);
    }

    public IReadOnlyList<DockItem> CreatePins(IEnumerable<string> paths)
    {
        var pins = new List<DockItem>();
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var trimmed = path.Trim().Trim('"');
            if (DockPins.TryUrl(trimmed, out _))
            {
                continue;
            }

            pins.Add(CreatePin(trimmed));
        }

        return pins;
    }

    public DockItem CreatePin(string pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            throw new InvalidOperationException("A pin needs a path.");
        }

        var trimmed = pathOrUrl.Trim().Trim('"');
        if (DockPins.TryUrl(trimmed, out _))
        {
            throw new InvalidOperationException("Web URL pins are not supported.");
        }

        if (Directory.Exists(trimmed))
        {
            return DockPins.FromTarget(trimmed);
        }

        if (File.Exists(trimmed) && IsLink(trimmed))
        {
            if (TryResolveLink(trimmed, out var resolved, out var title, out var icon))
            {
                return DockPins.FromTarget(resolved, title, icon, trimmed);
            }

            return DockPins.FromTarget(trimmed, Path.GetFileNameWithoutExtension(trimmed), shortcutPath: trimmed);
        }

        var display = File.Exists(trimmed) || Directory.Exists(trimmed)
            ? Path.GetFileNameWithoutExtension(trimmed)
            : null;
        if (string.IsNullOrWhiteSpace(display) && Directory.Exists(trimmed))
        {
            display = new DirectoryInfo(trimmed).Name;
        }

        return DockPins.FromTarget(trimmed, string.IsNullOrWhiteSpace(display) ? null : display);
    }

    public bool TryLaunch(DockItem item)
    {
        if (!DockPins.IsPin(item.Type))
        {
            return false;
        }

        var target = !string.IsNullOrWhiteSpace(item.ShortcutPath) && File.Exists(item.ShortcutPath)
            ? item.ShortcutPath
            : item.Path;
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool TryActivate(DockItem item) => Win32WindowSwitch.TryActivate(item);

    public IReadOnlyList<DockWindowEntry> GetWindows(DockItem item) => Win32WindowSwitch.GetWindows(item);

    public bool ActivateWindow(nint hwnd) => Win32WindowSwitch.ActivateWindow(hwnd);

    public bool CloseWindow(nint hwnd) => Win32WindowSwitch.CloseWindow(hwnd);

    public string? GetIconFile(DockItem item)
    {
        if (!DockPins.IsPin(item.Type))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(item.IconPath) && File.Exists(item.IconPath))
        {
            return item.IconPath;
        }

        var source = item.Path;
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant()[..16];
        var cache = Path.Combine(_cacheDirectory, key + "-40c.png");
        if (File.Exists(cache)
            && (!File.Exists(source) || File.GetLastWriteTimeUtc(cache) >= File.GetLastWriteTimeUtc(source)))
        {
            return cache;
        }

        return TryExtractIcon(item, source, cache) ? cache : null;
    }

    public string? GetStockIconFile(DockPinStackKind kind)
    {
        var name = kind == DockPinStackKind.Folders ? "stock-folder-40g.png" : "stock-app-40g.png";
        var cache = Path.Combine(_cacheDirectory, name);
        if (File.Exists(cache))
        {
            return cache;
        }

        return TryExtractStockIcon(kind == DockPinStackKind.Folders ? SiidFolder : SiidApplication, cache)
            ? cache
            : null;
    }

    public IReadOnlySet<string> GetRunningImagePaths(IReadOnlySet<string> processNames)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (processNames.Count == 0)
        {
            return paths;
        }

        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception)
        {
            return paths;
        }

        foreach (var process in processes)
        {
            try
            {
                if (processNames.Contains(process.ProcessName))
                {
                    var image = NativeMethods.QueryProcessImage(process.Id);
                    if (!string.IsNullOrWhiteSpace(image))
                    {
                        paths.Add(DockPins.NormalizePath(image));
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return paths;
    }

    public IReadOnlyList<StartMenuApp> ListStartMenuApps()
    {
        var found = new List<StartMenuApp>();
        foreach (var root in StartMenuRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                found.Add(new StartMenuApp { Name = name, LnkPath = file });
            }
        }

        return StartMenuApps.Collect(found);
    }

    private static IEnumerable<string> StartMenuRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
    }

    private static bool IsLink(string path) =>
        string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase);

    private static bool TryResolveLink(string path, out string target, out string? title, out string? iconPath)
    {
        target = string.Empty;
        title = Path.GetFileNameWithoutExtension(path);
        iconPath = null;
        IShellLinkW? link = null;
        try
        {
            link = (IShellLinkW)Activator.CreateInstance(
                Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))!)!;
            ((IPersistFile)link).Load(path, 0);
            try
            {
                link.Resolve(IntPtr.Zero, SlrNoUi | SlrNoUpdate);
            }
            catch (Exception)
            {
            }

            var file = new StringBuilder(1024);
            link.GetPath(file, file.Capacity, IntPtr.Zero, 0);
            target = file.ToString();
            var description = new StringBuilder(260);
            link.GetDescription(description, description.Capacity);
            if (description.Length > 0)
            {
                title = description.ToString();
            }

            var icon = new StringBuilder(1024);
            link.GetIconLocation(icon, icon.Capacity, out _);
            var iconLocation = icon.ToString();
            if (!string.IsNullOrWhiteSpace(iconLocation) && File.Exists(iconLocation))
            {
                iconPath = iconLocation;
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = Path.GetFileNameWithoutExtension(path);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            if (link is not null)
            {
                Marshal.ReleaseComObject(link);
            }
        }
    }

    private static bool TryExtractIcon(DockItem item, string source, string cache)
    {
        uint attributes = 0;
        var extra = 0u;
        if (item.Type == DockItemType.Folder)
        {
            attributes = FileAttributeDirectory;
            if (!Directory.Exists(source))
            {
                extra = ShgfiUseFileAttributes;
            }
        }
        else if (!File.Exists(source) && !Directory.Exists(source))
        {
            attributes = FileAttributeNormal;
            extra = ShgfiUseFileAttributes;
        }

        var info = new ShFileInfo();
        var size = (uint)Marshal.SizeOf<ShFileInfo>();
        var index = SHGetFileInfo(source, attributes, out info, size, ShgfiSysIconIndex | extra);
        if (index != IntPtr.Zero && TrySaveImageListIcon(info.iIcon, cache))
        {
            return true;
        }

        var result = SHGetFileInfo(source, attributes, out info, size, ShgfiIcon | ShgfiLargeIcon | extra);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            return SaveIcon(info.hIcon, cache);
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private static bool TryExtractStockIcon(int siid, string cache)
    {
        var info = new ShStockIconInfo
        {
            cbSize = (uint)Marshal.SizeOf<ShStockIconInfo>()
        };
        if (SHGetStockIconInfo(siid, ShgsiSysIconIndex, ref info) == 0
            && TrySaveImageListIcon(info.iSysImageIndex, cache, StockIconMinOpaque))
        {
            return true;
        }

        info = new ShStockIconInfo
        {
            cbSize = (uint)Marshal.SizeOf<ShStockIconInfo>()
        };
        if (SHGetStockIconInfo(siid, ShgsiIcon, ref info) != 0 || info.hIcon == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            return SaveIcon(info.hIcon, cache, StockIconMinOpaque);
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private static bool TrySaveImageListIcon(int index, string cache) =>
        TrySaveImageListIcon(index, cache, IconAlpha.MinOpaque);

    private static bool TrySaveImageListIcon(int index, string cache, byte minOpaque)
    {
        var iid = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
        if (!TryGetImageList(ShilExtraLarge, ref iid, out var list) || list is null)
        {
            return false;
        }

        var handle = IntPtr.Zero;
        try
        {
            if (list.GetIcon(index, IldTransparent, out handle) != 0 || handle == IntPtr.Zero)
            {
                return false;
            }

            return SaveIcon(handle, cache, minOpaque);
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                DestroyIcon(handle);
            }
        }
    }

    private static bool SaveIcon(IntPtr handle, string cache, byte minOpaque = IconAlpha.MinOpaque)
    {
        using var icon = Icon.FromHandle(handle);
        using var bitmap = icon.ToBitmap();
        using var cropped = CropToContent(bitmap, minOpaque);
        Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
        cropped.Save(cache, ImageFormat.Png);
        return File.Exists(cache);
    }

    private static Bitmap CropToContent(Bitmap source, byte minOpaque = IconAlpha.MinOpaque)
    {
        var box = OpaqueBounds(source, minOpaque);
        if (box.Width <= 0 || box.Height <= 0 || box is { X: 0, Y: 0 } && box.Width == source.Width && box.Height == source.Height)
        {
            return (Bitmap)source.Clone();
        }

        var cropped = new Bitmap(box.Width, box.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(cropped);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.DrawImage(source, new Rectangle(0, 0, box.Width, box.Height), box, GraphicsUnit.Pixel);
        return cropped;
    }

    private static Rectangle OpaqueBounds(Bitmap bitmap, byte minOpaque = IconAlpha.MinOpaque)
    {
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[data.Stride * bitmap.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            var box = IconAlpha.OpaqueBounds(bytes, bitmap.Width, bitmap.Height, data.Stride, minOpaque);
            return box.Width <= 0
                ? Rectangle.Empty
                : new Rectangle(box.X, box.Y, box.Width, box.Height);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetStockIconInfo(int siid, uint uFlags, ref ShStockIconInfo psii);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        out ShFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    private static bool TryGetImageList(int size, ref Guid iid, out IImageList list)
    {
        if (SHGetImageListByName(size, ref iid, out list) == 0 && list is not null)
        {
            return true;
        }

        return SHGetImageListByOrdinal(size, ref iid, out list) == 0 && list is not null;
    }

    [DllImport("shell32.dll", EntryPoint = "SHGetImageList")]
    private static extern int SHGetImageListByName(int iImageList, ref Guid riid, out IImageList ppv);

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageListByOrdinal(int iImageList, ref Guid riid, out IImageList ppv);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, out int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, out int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, out int pi);
        [PreserveSig] int Draw(IntPtr pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, out IntPtr picon);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShStockIconInfo
    {
        public uint cbSize;
        public IntPtr hIcon;
        public int iSysImageIndex;
        public int iIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szPath;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
