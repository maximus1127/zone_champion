using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ZoneChampion.Core.Geometry;
using ZoneChampion.Core.Panels;
using ZoneChampion.Core.Zones;
using ZoneChampion.Interop;
using static ZoneChampion.Interop.Native;

namespace ZoneChampion.Panels;

/// <summary>What a panel asks of the rest of the app.</summary>
internal interface IPanelHost
{
    void Activate(nint handle);
    void Reorder(ZoneKey zone, nint handle, int newIndex);
    void SetTag(nint handle, string? color);
    void CloseWindow(nint handle);
    IReadOnlyList<(ZoneKey Key, string Label)> GetMoveTargets(ZoneKey from);
    void MoveToZone(nint handle, ZoneKey zone);
}

/// <summary>
/// One floating panel. It never takes focus: clicking it must leave the user's window active so "click the active
/// window's icon to minimize" works, and so the panel doesn't flash the previous app's title bar.
/// </summary>
internal partial class PanelWindow : Window
{
    private const double DragThreshold = 5;

    private readonly IPanelHost _host;
    private readonly DispatcherTimer _titleTimer;
    private nint _hwnd;
    private Zone? _zone;
    private PanelTheme? _appliedTheme;
    private PanelItemViewModel? _hovered;
    private PanelItemViewModel? _pressed;
    private Point _pressPoint;
    private bool _dragging;
    private MouseDownWatcher? _menuClickWatcher;

    public PanelWindow(PanelViewModel viewModel, IPanelHost host)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        _host = host;

        _titleTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher);
        _titleTimer.Tick += (_, _) =>
        {
            _titleTimer.Stop();
            if (_hovered is not null)
            {
                ShowTitle(_hovered);
            }
        };

        SourceInitialized += OnSourceInitialized;

        // With SizeToContent, SizeChanged fires before the native window is resized; wait for layout to finish so
        // Reposition measures the new size.
        SizeChanged += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Reposition);
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Reposition);

        MenuPopup.Opened += (_, _) =>
        {
            _menuClickWatcher?.Dispose();
            _menuClickWatcher = new MouseDownWatcher(OnMouseDownWhileMenuOpen);
        };
        MenuPopup.Closed += (_, _) =>
        {
            _menuClickWatcher?.Dispose();
            _menuClickWatcher = null;
        };
        Closed += (_, _) => _menuClickWatcher?.Dispose();

        Items.PreviewMouseLeftButtonDown += OnItemsMouseDown;
        Items.PreviewMouseMove += OnItemsMouseMove;
        Items.PreviewMouseLeftButtonUp += OnItemsMouseUp;
        Items.PreviewMouseRightButtonUp += OnItemsRightMouseUp;
        Items.LostMouseCapture += (_, _) => ResetPress();
        Items.MouseLeave += (_, _) =>
        {
            if (_pressed is null)
            {
                SetHovered(null);
            }
        };
        Scroller.PreviewMouseWheel += OnMouseWheel;
        ViewModel.Items.CollectionChanged += (_, _) =>
        {
            if (_hovered is not null && !ViewModel.Items.Contains(_hovered))
            {
                SetHovered(null);
            }
        };
    }

    public PanelViewModel ViewModel { get; }

    public Zone? Zone => _zone;

    /// <summary>The panel window's rectangle in physical screen pixels (including the transparent shadow inset).</summary>
    public RectI ScreenBounds => _hwnd != 0 && GetWindowRect(_hwnd, out var rect) ? rect.ToRectI() : default;

    public void Configure(Zone zone, PanelTheme theme)
    {
        bool moved = _zone != zone;
        _zone = zone;
        ViewModel.Theme = theme;

        if (!ReferenceEquals(theme, _appliedTheme))
        {
            ApplyTheme(theme);
        }

        if (moved || !ReferenceEquals(theme, _appliedTheme))
        {
            // Never grow past the zone; extra icons scroll.
            double scale = zone.Monitor.Scale;
            if (PanelPlacement.IsVertical(theme.Edge))
            {
                MaxHeight = zone.Bounds.Height / scale + 2 * theme.Inset;
                MaxWidth = double.PositiveInfinity;
            }
            else
            {
                MaxWidth = zone.Bounds.Width / scale + 2 * theme.Inset;
                MaxHeight = double.PositiveInfinity;
            }
        }

        _appliedTheme = theme;
        Reposition();
    }

    public void SetShown(bool shown)
    {
        if (shown && !IsVisible)
        {
            Show();
            Reposition();
        }
        else if (!shown && IsVisible)
        {
            ClosePopups();
            SetHovered(null);
            ResetPress();
            Items.ReleaseMouseCapture();
            Hide();
        }
    }

    public void ReassertTopmost()
    {
        if (_hwnd != 0 && IsVisible)
        {
            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }

    public void ClosePopups()
    {
        _titleTimer.Stop();
        TitlePopup.IsOpen = false;
        MenuPopup.IsOpen = false;
    }

    public void CloseMenu() => MenuPopup.IsOpen = false;

    /// <summary>Any press outside the menu dismisses it, including clicks in other apps.</summary>
    private void OnMouseDownWhileMenuOpen(int x, int y)
    {
        if (PresentationSource.FromVisual(MenuRoot) is HwndSource source
            && GetWindowRect(source.Handle, out var menu)
            && x >= menu.Left && x < menu.Right && y >= menu.Top && y < menu.Bottom)
        {
            return;
        }

        // Close after the hook returns; never do UI work inside a low-level hook callback.
        Dispatcher.BeginInvoke(CloseMenu);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        long exStyle = GetExStyle(_hwnd);
        SetExStyle(_hwnd, (exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW);
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);

        // Start on the right monitor so the first layout already uses its DPI.
        if (_zone is not null)
        {
            var center = _zone.Bounds.Center;
            SetWindowPos(_hwnd, HWND_TOPMOST, center.X, center.Y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }

    private static nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_MOUSEACTIVATE)
        {
            handled = true;
            return MA_NOACTIVATE;
        }

        return 0;
    }

    private void ApplyTheme(PanelTheme theme)
    {
        Resources["PopupBackground"] = theme.PopupBackground;
        Resources["PopupForeground"] = theme.PopupForeground;
        Resources["PopupSubtle"] = theme.PopupSubtle;
        Resources["PopupHover"] = theme.PopupHover;
        Resources["PopupBorder"] = theme.PopupBorder;
        Resources["PopupFontFamily"] = theme.FontFamily;
        Resources["PopupFontSize"] = theme.FontSize;

        Chrome.Effect = theme.ShadowEnabled
            ? new DropShadowEffect
            {
                BlurRadius = theme.ShadowBlur,
                ShadowDepth = theme.ShadowDepth,
                Direction = 270,
                Opacity = theme.ShadowOpacity,
                Color = Colors.Black,
            }
            : null;

        ClosePopups();
    }

    /// <summary>Puts the panel against its zone edge. Works in physical pixels so it lands exactly on FancyZones' zones.</summary>
    private void Reposition()
    {
        if (_hwnd == 0 || _zone is null || !GetWindowRect(_hwnd, out var rect))
        {
            return;
        }

        var theme = ViewModel.Theme;
        var size = new SizeI(rect.Right - rect.Left, rect.Bottom - rect.Top);
        var origin = PanelPlacement.ComputeOrigin(
            _zone.Bounds,
            theme.Edge,
            theme.Style.Alignment,
            size,
            _zone.Monitor.Scale,
            theme.Style.EdgeOffset,
            theme.Style.AlignmentOffset,
            theme.Inset);

        // Large offsets could push the panel's center onto a neighboring monitor, whose different scaling would make
        // Windows rescale it and bounce it back and forth. Keep the center on the zone's monitor.
        var monitor = _zone.Monitor.Bounds;
        int x = Math.Clamp(origin.X, monitor.Left - size.Width / 2 + 1, Math.Max(monitor.Left - size.Width / 2 + 1, monitor.Right - size.Width / 2 - 1));
        int y = Math.Clamp(origin.Y, monitor.Top - size.Height / 2 + 1, Math.Max(monitor.Top - size.Height / 2 + 1, monitor.Bottom - size.Height / 2 - 1));

        if (x != rect.Left || y != rect.Top)
        {
            SetWindowPos(_hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }

    // ---- Mouse: click, drag to reorder, hover title, right-click menu ----

    private void OnItemsMouseDown(object sender, MouseButtonEventArgs e)
    {
        var item = ItemFrom(e.OriginalSource);
        if (item is null)
        {
            return;
        }

        _pressed = item;
        _pressPoint = e.GetPosition(Items);
        _dragging = false;
        Items.CaptureMouse();
        e.Handled = true;
    }

    private void OnItemsMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressed is null || !Items.IsMouseCaptured)
        {
            // The gaps between tiles aren't part of any tile; keep the current hover there so the title doesn't
            // flicker while moving along the panel. MouseLeave clears it.
            if (ItemFrom(e.OriginalSource) is { } item)
            {
                SetHovered(item);
            }

            return;
        }

        var point = e.GetPosition(Items);
        if (!_dragging && (Math.Abs(point.X - _pressPoint.X) > DragThreshold || Math.Abs(point.Y - _pressPoint.Y) > DragThreshold))
        {
            _dragging = true;
            _pressed.IsDragging = true;
            ClosePopups();
        }

        if (_dragging)
        {
            DragTo(point);
        }
    }

    private void OnItemsMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_pressed is null)
        {
            return;
        }

        var item = _pressed;
        bool wasDrag = _dragging;
        ResetPress();
        Items.ReleaseMouseCapture();
        e.Handled = true;

        if (!wasDrag)
        {
            ClosePopups();
            _host.Activate(item.Handle);
        }
    }

    private void OnItemsRightMouseUp(object sender, MouseButtonEventArgs e)
    {
        var item = ItemFrom(e.OriginalSource);
        if (item is not null)
        {
            ShowMenu(item);
            e.Handled = true;
        }
    }

    private void ResetPress()
    {
        if (_pressed is not null)
        {
            _pressed.IsDragging = false;
        }

        _pressed = null;
        _dragging = false;
    }

    /// <summary>Moves the dragged icon to the slot under the pointer; the other icons shift live.</summary>
    private void DragTo(Point point)
    {
        var items = ViewModel.Items;
        var dragged = _pressed!;
        int from = items.IndexOf(dragged);
        if (from < 0)
        {
            return;
        }

        bool vertical = PanelPlacement.IsVertical(ViewModel.Theme.Edge);
        double pointer = vertical ? point.Y : point.X;
        int target = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (i == from || Items.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container)
            {
                continue;
            }

            var position = container.TranslatePoint(new Point(0, 0), Items);
            double middle = vertical ? position.Y + container.ActualHeight / 2 : position.X + container.ActualWidth / 2;
            if (pointer > middle)
            {
                target++;
            }
        }

        if (target != from)
        {
            items.Move(from, target);
            Items.UpdateLayout();
            _host.Reorder(ViewModel.Key, dragged.Handle, target);
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!PanelPlacement.IsVertical(ViewModel.Theme.Edge))
        {
            Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset - e.Delta / 3.0);
            e.Handled = true;
        }
    }

    private void SetHovered(PanelItemViewModel? item)
    {
        if (ReferenceEquals(item, _hovered))
        {
            return;
        }

        if (_hovered is not null)
        {
            _hovered.IsHovered = false;
        }

        _hovered = item;
        _titleTimer.Stop();

        if (item is null)
        {
            TitlePopup.IsOpen = false;
            return;
        }

        item.IsHovered = true;
        var style = ViewModel.Theme.Style;
        if (MenuPopup.IsOpen || !style.ShowTitleOnHover)
        {
            return;
        }

        if (TitlePopup.IsOpen)
        {
            ShowTitle(item); // already showing a title: follow the pointer without the delay
        }
        else
        {
            _titleTimer.Interval = TimeSpan.FromMilliseconds(style.TitleDelayMs);
            _titleTimer.Start();
        }
    }

    private void ShowTitle(PanelItemViewModel item)
    {
        if (ContainerFor(item) is not { } container)
        {
            return;
        }

        TitlePopup.IsOpen = false;
        TitleBorder.DataContext = item;
        TitleBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        PlacePopup(TitlePopup, container, TitleBorder.DesiredSize, extraMargin: 0);
        TitlePopup.IsOpen = true;
    }

    public void ShowMenu(PanelItemViewModel item)
    {
        // Not mid-drag: the drag owns mouse capture, which the menu needs.
        if (_pressed is not null || ContainerFor(item) is not { } container)
        {
            return;
        }

        _titleTimer.Stop();
        TitlePopup.IsOpen = false;
        var theme = ViewModel.Theme;

        MenuTitle.Text = string.IsNullOrWhiteSpace(item.Title) ? "(untitled window)" : item.Title;

        Swatches.Children.Clear();
        foreach (var color in theme.TagColors)
        {
            bool selected = string.Equals(color, item.TagColor, StringComparison.OrdinalIgnoreCase);
            var swatch = new Button
            {
                Style = (Style)Resources["SwatchButton"],
                Background = PanelTheme.Brush(color, 1),
                Tag = selected ? "Selected" : null,
                ToolTip = null,
            };
            swatch.Click += (_, _) => CloseMenuThen(() => _host.SetTag(item.Handle, selected ? null : color));
            Swatches.Children.Add(swatch);
        }

        var clear = new Button
        {
            Style = (Style)Resources["SwatchButton"],
            Background = Brushes.Transparent,
            Tag = item.TagColor is null ? "Selected" : null,
            Content = new TextBlock { Text = "✕", FontSize = 10, Foreground = theme.PopupSubtle },
        };
        clear.Click += (_, _) => CloseMenuThen(() => _host.SetTag(item.Handle, null));
        Swatches.Children.Add(clear);

        MoveTargets.Children.Clear();
        foreach (var (key, label) in _host.GetMoveTargets(ViewModel.Key))
        {
            var button = new Button { Style = (Style)Resources["MenuButton"], Content = label };
            button.Click += (_, _) => CloseMenuThen(() => _host.MoveToZone(item.Handle, key));
            MoveTargets.Children.Add(button);
        }

        MoveSection.Visibility = MoveTargets.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        CloseWindowButton.Click -= OnCloseWindowClick;
        CloseWindowButton.Click += OnCloseWindowClick;
        CloseWindowButton.Tag = item;

        MenuRoot.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        PlacePopup(MenuPopup, container, MenuRoot.DesiredSize, extraMargin: 12);
        MenuPopup.IsOpen = true;
    }

    private void OnCloseWindowClick(object sender, RoutedEventArgs e)
    {
        if (CloseWindowButton.Tag is PanelItemViewModel item)
        {
            CloseMenuThen(() => _host.CloseWindow(item.Handle));
        }
    }

    private void CloseMenuThen(Action action)
    {
        MenuPopup.IsOpen = false;
        action();
    }

    /// <summary>Opens a popup beside the panel, centered on the icon.</summary>
    /// <param name="extraMargin">Transparent margin inside the popup (room for its shadow) to compensate for.</param>
    private void PlacePopup(Popup popup, FrameworkElement target, Size popupSize, double extraMargin)
    {
        var theme = ViewModel.Theme;
        popup.PlacementTarget = target;
        popup.Placement = theme.PopupPlacement;

        bool vertical = PanelPlacement.IsVertical(theme.Edge);
        double centerAlong = vertical
            ? (target.ActualHeight - popupSize.Height) / 2
            : (target.ActualWidth - popupSize.Width) / 2;

        popup.HorizontalOffset = vertical ? theme.PopupHorizontalOffset - Math.Sign(theme.PopupHorizontalOffset) * extraMargin : centerAlong;
        popup.VerticalOffset = vertical ? centerAlong : theme.PopupVerticalOffset - Math.Sign(theme.PopupVerticalOffset) * extraMargin;
    }

    private FrameworkElement? ContainerFor(PanelItemViewModel item) =>
        Items.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;

    private static PanelItemViewModel? ItemFrom(object source) =>
        (source as FrameworkElement)?.DataContext as PanelItemViewModel;

    // ---- Snapshots (for --snapshot) ----

    public void SaveSnapshot(string path) => SaveVisual(this, ActualWidth, ActualHeight, path);

    public void SaveMenuSnapshot(string path) => SaveVisual(MenuRoot, MenuRoot.ActualWidth, MenuRoot.ActualHeight, path);

    private void SaveVisual(Visual visual, double width, double height, string path)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(width * dpi.DpiScaleX),
            (int)Math.Ceiling(height * dpi.DpiScaleY),
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
