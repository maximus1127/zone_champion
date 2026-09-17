using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static ZoneChampion.Interop.Native;

namespace ZoneChampion.Tracking;

/// <summary>
/// Loads crisp, correctly sized app icons on a background thread. Tries, in order: the app's registered identity
/// (Store apps, PWAs, apps with a Start menu entry), the executable's own icon, then the icon the window reports.
/// </summary>
internal sealed class IconLoader : IDisposable
{
    // Processes that host other apps' windows, where the executable's icon would be misleading.
    private static readonly HashSet<string> GenericHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApplicationFrameHost.exe", "java.exe", "javaw.exe", "python.exe", "pythonw.exe", "rundll32.exe",
        "dllhost.exe", "mmc.exe", "wscript.exe", "cscript.exe", "electron.exe",
    };

    private readonly BlockingCollection<Request> _queue = new();
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<string, BitmapSource?> _cache = new(); // worker thread only
    private readonly Thread _thread;

    private sealed record Request(nint Hwnd, uint ProcessId, string? ExePath, int Size, Action<BitmapSource?> Callback);

    public IconLoader(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _thread = new Thread(Run) { IsBackground = true, Name = "Icon loader" };
        _thread.SetApartmentState(ApartmentState.STA); // shell COM objects want STA
        _thread.Start();
    }

    /// <summary>Queues an icon load; <paramref name="callback"/> runs on the UI thread with null if nothing was found.</summary>
    public void Load(TrackedWindow window, int sizePx, Action<BitmapSource?> callback)
    {
        if (!_queue.IsAddingCompleted)
        {
            _queue.Add(new Request(window.Handle, window.ProcessId, window.ExePath, sizePx, callback));
        }
    }

    private void Run()
    {
        foreach (var request in _queue.GetConsumingEnumerable())
        {
            BitmapSource? image = null;
            try
            {
                image = LoadIcon(request);
            }
            catch (Exception ex)
            {
                Log.Warn($"Icon load failed for {request.ExePath}: {ex.Message}");
            }

            _dispatcher.BeginInvoke(() => request.Callback(image));
        }
    }

    private BitmapSource? LoadIcon(Request request)
    {
        var exeName = Path.GetFileName(request.ExePath ?? "");

        var appId = GetWindowAppId(request.Hwnd) ?? GetPackagedAppId(request.Hwnd, request.ProcessId, exeName);
        if (appId is not null)
        {
            var image = Cached($"app|{appId}|{request.Size}", () => FromShellItem(@"shell:AppsFolder\" + appId, request.Size));
            if (image is not null)
            {
                return image;
            }
        }

        bool genericHost = GenericHosts.Contains(exeName);
        if (request.ExePath is not null && !genericHost)
        {
            var image = Cached($"exe|{request.ExePath}|{request.Size}", () => FromFile(request.ExePath, request.Size));
            if (image is not null)
            {
                return image;
            }
        }

        return FromWindow(request.Hwnd)
            ?? (request.ExePath is not null ? Cached($"exe|{request.ExePath}|{request.Size}", () => FromFile(request.ExePath, request.Size)) : null);
    }

    private BitmapSource? Cached(string key, Func<BitmapSource?> load)
    {
        if (!_cache.TryGetValue(key, out var image))
        {
            image = load();
            _cache[key] = image;
        }

        return image;
    }

    /// <summary>The AppUserModelID a window explicitly declares (Chrome, Edge, Explorer, PWAs...).</summary>
    private static string? GetWindowAppId(nint hwnd)
    {
        var iid = typeof(IPropertyStore).GUID;
        if (SHGetPropertyStoreForWindow(hwnd, ref iid, out var store) != 0 || store is null)
        {
            return null;
        }

        try
        {
            var key = PKEY_AppUserModel_ID;
            if (store.GetValue(ref key, out var value) != 0)
            {
                return null;
            }

            try
            {
                return value.vt == VT_LPWSTR && value.p != 0 ? Marshal.PtrToStringUni(value.p) : null;
            }
            finally
            {
                PropVariantClear(ref value);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    /// <summary>The package identity of a Store/MSIX app. UWP windows are hosted by ApplicationFrameHost.exe,
    /// so look at the process behind the hosted CoreWindow instead.</summary>
    private static string? GetPackagedAppId(nint hwnd, uint processId, string exeName)
    {
        if (exeName.Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
        {
            var coreWindow = FindWindowEx(hwnd, 0, "Windows.UI.Core.CoreWindow", null);
            if (coreWindow == 0)
            {
                return null;
            }

            GetWindowThreadProcessId(coreWindow, out processId);
        }

        nint process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == 0)
        {
            return null;
        }

        try
        {
            var buffer = new char[512];
            uint length = (uint)buffer.Length;
            return GetApplicationUserModelId(process, ref length, buffer) == 0
                ? new string(buffer, 0, (int)Math.Max(0, length - 1))
                : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private static BitmapSource? FromShellItem(string parsingName, int size)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        if (SHCreateItemFromParsingName(parsingName, 0, ref iid, out var factory) != 0 || factory is null)
        {
            return null;
        }

        try
        {
            if (factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF_ICONONLY, out nint bitmap) != 0 || bitmap == 0)
            {
                return null;
            }

            try
            {
                return FromHBitmap(bitmap);
            }
            finally
            {
                DeleteObject(bitmap);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
    }

    private static BitmapSource? FromFile(string path, int size)
    {
        if (SHDefExtractIcon(path, 0, 0, out nint icon, 0, (uint)size) != 0 || icon == 0)
        {
            return null;
        }

        try
        {
            return FromHIcon(icon);
        }
        finally
        {
            DestroyIcon(icon);
        }
    }

    /// <summary>The icon the window itself reports. These handles belong to the app, so they're not destroyed.</summary>
    private static BitmapSource? FromWindow(nint hwnd)
    {
        nint icon = 0;
        foreach (int type in new[] { ICON_BIG, ICON_SMALL2, ICON_SMALL })
        {
            if (SendMessageTimeout(hwnd, WM_GETICON, type, 0, SMTO_ABORTIFHUNG | SMTO_BLOCK, 200, out icon) != 0 && icon != 0)
            {
                break;
            }
        }

        if (icon == 0)
        {
            icon = GetClassLongPtr(hwnd, GCLP_HICON);
        }

        if (icon == 0)
        {
            icon = GetClassLongPtr(hwnd, GCLP_HICONSM);
        }

        return icon == 0 ? null : FromHIcon(icon);
    }

    private static BitmapSource FromHIcon(nint icon)
    {
        var source = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        var copy = new WriteableBitmap(source);
        copy.Freeze();
        return copy;
    }

    /// <summary>
    /// Copies a 32-bit DIB with its alpha channel intact (Imaging.CreateBitmapSourceFromHBitmap drops alpha, which
    /// paints icons on black squares).
    /// </summary>
    private static BitmapSource? FromHBitmap(nint bitmap)
    {
        if (GetObject(bitmap, Marshal.SizeOf<DIBSECTION>(), out var dib) == 0 || dib.dsBm.bmBits == 0 || dib.dsBm.bmBitsPixel != 32)
        {
            var fallback = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            fallback.Freeze();
            return fallback;
        }

        int width = dib.dsBm.bmWidth;
        int height = Math.Abs(dib.dsBm.bmHeight);
        int stride = width * 4;
        var pixels = new byte[stride * height];
        Marshal.Copy(dib.dsBm.bmBits, pixels, 0, pixels.Length);

        if (dib.dsBmih.biHeight > 0)
        {
            // Bottom-up DIB: flip rows.
            var row = new byte[stride];
            for (int y = 0; y < height / 2; y++)
            {
                int top = y * stride;
                int bottom = (height - 1 - y) * stride;
                Buffer.BlockCopy(pixels, top, row, 0, stride);
                Buffer.BlockCopy(pixels, bottom, pixels, top, stride);
                Buffer.BlockCopy(row, 0, pixels, bottom, stride);
            }
        }

        bool hasAlpha = false;
        bool premultiplied = true;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte alpha = pixels[i + 3];
            hasAlpha |= alpha != 0;
            if (pixels[i] > alpha || pixels[i + 1] > alpha || pixels[i + 2] > alpha)
            {
                premultiplied = false;
            }
        }

        if (!hasAlpha)
        {
            for (int i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }
        }

        var format = hasAlpha && premultiplied ? PixelFormats.Pbgra32 : PixelFormats.Bgra32;
        var image = BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
        image.Freeze();
        return image;
    }

    public void Dispose() => _queue.CompleteAdding();
}
