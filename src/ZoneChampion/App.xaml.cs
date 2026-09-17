using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ZoneChampion;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private AppController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            // A tray utility should keep running; log and carry on.
            Log.Error("Unhandled UI exception", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Error("Unhandled exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => Log.Error("Unobserved task exception", args.Exception);

        var arguments = e.Args;
        if (arguments.Length > 0 && arguments[0] == "--diagnose")
        {
            Diagnostics.Write(arguments.Length > 1 ? arguments[1] : null);
            Shutdown();
            return;
        }

        _singleInstance = new Mutex(initiallyOwned: true, @"Local\ZoneChampion.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        _controller = new AppController();
        try
        {
            _controller.Start();
        }
        catch (Exception ex)
        {
            // Don't linger half-started while holding the single-instance lock.
            Log.Error("Startup failed", ex);
            MessageBox.Show($"Zone Champion couldn't start:\n\n{ex.Message}\n\nDetails are in {Log.FilePath}", "Zone Champion", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        if (arguments.Length > 1 && arguments[0] == "--snapshot")
        {
            int seconds = arguments.Length > 2 && int.TryParse(arguments[2], out int s) ? s : 3;
            TakeSnapshots(arguments[1], TimeSpan.FromSeconds(seconds));
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// <c>--snapshot &lt;folder&gt; [seconds]</c>: after a delay, renders each panel (and one right-click menu) to PNG
    /// files and writes panels.txt with each panel's position and icons, then exits. Renders the app's own visuals
    /// only; nothing else on screen is captured.
    /// </summary>
    private void TakeSnapshots(string folder, TimeSpan delay)
    {
        Directory.CreateDirectory(folder);
        var timer = new DispatcherTimer { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var panels = _controller!.Panels.Panels.Where(p => p.IsVisible).ToList();
            var report = new System.Text.StringBuilder();
            foreach (var panel in panels)
            {
                panel.SaveSnapshot(Path.Combine(folder, $"panel-{panel.Zone?.Monitor.DeviceId}-zone{panel.Zone?.Key.Number}.png"));
                report.AppendLine($"{panel.Zone?.Monitor.DeviceId} zone {panel.Zone?.Key.Number} {panel.Zone?.Bounds}");
                report.AppendLine($"  edge {panel.ViewModel.Theme.Edge}, inset {panel.ViewModel.Theme.Inset}, window {panel.ScreenBounds}");
                foreach (var item in panel.ViewModel.Items)
                {
                    var flags = (item.IsActive ? " active" : "") + (item.IsMinimized ? " minimized" : "") + (item.Icon is null ? " no-icon" : "");
                    report.AppendLine($"  -{flags} {item.Title}");
                }
            }

            File.WriteAllText(Path.Combine(folder, "panels.txt"), report.ToString());

            var withItems = panels.FirstOrDefault(p => p.ViewModel.Items.Count > 0);
            if (withItems is null)
            {
                Shutdown();
                return;
            }

            withItems.ShowMenu(withItems.ViewModel.Items[0]);
            var menuTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            menuTimer.Tick += (_, _) =>
            {
                menuTimer.Stop();
                withItems.SaveMenuSnapshot(Path.Combine(folder, "menu.png"));
                withItems.ClosePopups();
                Shutdown();
            };
            menuTimer.Start();
        };
        timer.Start();
    }
}
