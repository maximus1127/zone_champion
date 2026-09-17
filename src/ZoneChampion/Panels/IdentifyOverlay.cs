using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ZoneChampion.Core.Zones;
using static ZoneChampion.Interop.Native;

namespace ZoneChampion.Panels;

/// <summary>
/// A click-through label covering one zone for a few seconds, showing the monitor id and zone number to use in
/// the settings file.
/// </summary>
internal sealed class IdentifyOverlay : Window
{
    private readonly Zone _zone;
    private nint _hwnd;

    public IdentifyOverlay(Zone zone, bool panelEnabled, TimeSpan duration)
    {
        _zone = zone;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        IsHitTestVisible = false;
        Title = "Zone Champion zone label";

        var accent = PanelTheme.Brush("#89B4FA", 1);
        var text = PanelTheme.Brush("#CDD6F4", 1);
        var subtle = PanelTheme.Brush("#CDD6F4", 0.65);
        var font = new FontFamily("Segoe UI Variable Display, Segoe UI");

        var label = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        label.Children.Add(new TextBlock
        {
            Text = zone.Key.Number.ToString(),
            FontFamily = font,
            FontSize = 88,
            FontWeight = FontWeights.SemiBold,
            Foreground = accent,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, -12, 0, -4),
        });
        label.Children.Add(new TextBlock
        {
            Text = $"\"monitor\": \"{zone.Monitor.DeviceId}\", \"zone\": {zone.Key.Number}",
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 15,
            Foreground = text,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        label.Children.Add(new TextBlock
        {
            Text = panelEnabled ? $"{zone.Bounds.Width} × {zone.Bounds.Height} px" : "panel turned off in settings",
            FontFamily = font,
            FontSize = 13,
            Foreground = subtle,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
        });

        Content = new Border
        {
            BorderBrush = PanelTheme.Brush("#89B4FA", 0.85),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(12),
            Background = PanelTheme.Brush("#89B4FA", 0.08),
            Child = new Border
            {
                Background = PanelTheme.Brush("#1E1E2E", 0.92),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(32, 18, 32, 18),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = label,
            },
        };

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            SetExStyle(_hwnd, GetExStyle(_hwnd) | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
            Place();
        };

        // Moving onto a monitor with different scaling makes WPF resize the window; put it back.
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Place);

        var timer = new DispatcherTimer(duration, DispatcherPriority.Normal, (s, _) =>
        {
            ((DispatcherTimer)s!).Stop();
            Close();
        }, Dispatcher);
        timer.Start();
    }

    private void Place()
    {
        var bounds = _zone.Bounds;
        SetWindowPos(_hwnd, HWND_TOPMOST, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SWP_NOACTIVATE);
    }
}
