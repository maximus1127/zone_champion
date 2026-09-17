using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ZoneChampion;

/// <summary>The notification-area icon and its menu.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _hideItem;

    public TrayIcon()
    {
        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new DarkRenderer(),
            ShowImageMargin = false,
            Font = new Drawing.Font("Segoe UI", 9.5f),
            Padding = new Forms.Padding(2, 4, 2, 4),
        };

        menu.Items.Add(Item("Identify zones", () => IdentifyZones?.Invoke()));
        _hideItem = Item("Hide panels", () => TogglePanels?.Invoke());
        menu.Items.Add(_hideItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Item("Edit settings", () => EditSettings?.Invoke()));
        menu.Items.Add(Item("Open settings folder", () => OpenSettingsFolder?.Invoke()));
        menu.Items.Add(Item("Reload", () => Reload?.Invoke()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Item("Exit", () => Exit?.Invoke()));

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Zone Champion",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => Safely("Identify zones", () => IdentifyZones?.Invoke());
    }

    public event Action? IdentifyZones;
    public event Action? TogglePanels;
    public event Action? EditSettings;
    public event Action? OpenSettingsFolder;
    public event Action? Reload;
    public event Action? Exit;

    public void SetPanelsHidden(bool hidden)
    {
        _hideItem.Text = hidden ? "Show panels" : "Hide panels";
        _icon.Text = hidden ? "Zone Champion (panels hidden)" : "Zone Champion";
    }

    /// <summary>Shows the toggle hotkey beside the Hide/Show panels item, or nothing if it's turned off.</summary>
    public void SetToggleHotkey(string? hotkey) => _hideItem.ShortcutKeyDisplayString = hotkey ?? "";

    public void ShowWarning(string title, string message) =>
        _icon.ShowBalloonTip(8000, title, message, Forms.ToolTipIcon.Warning);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }

    private static Forms.ToolStripMenuItem Item(string text, Action onClick)
    {
        var item = new Forms.ToolStripMenuItem(text) { Padding = new Forms.Padding(8, 3, 16, 3) };
        item.Click += (_, _) => Safely(text, onClick);
        return item;
    }

    /// <summary>
    /// Tray callbacks run inside WinForms' message handling, where an exception would bypass WPF's handler and show
    /// the .NET crash dialog. Log it instead.
    /// </summary>
    private static void Safely(string action, Action callback)
    {
        try
        {
            callback();
        }
        catch (Exception ex)
        {
            Log.Error($"Tray action \"{action}\" failed", ex);
        }
    }

    private static Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/ZoneChampion.ico"));
        using var stream = resource!.Stream;
        return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }

    private sealed class DarkRenderer() : Forms.ToolStripProfessionalRenderer(new DarkColors())
    {
        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Drawing.Color.FromArgb(0xCD, 0xD6, 0xF4);
            base.OnRenderItemText(e);
        }
    }

    private sealed class DarkColors : Forms.ProfessionalColorTable
    {
        private static readonly Drawing.Color Background = Drawing.Color.FromArgb(0x1E, 0x1E, 0x2E);
        private static readonly Drawing.Color Hover = Drawing.Color.FromArgb(0x31, 0x32, 0x44);
        private static readonly Drawing.Color Line = Drawing.Color.FromArgb(0x45, 0x47, 0x5A);

        public override Drawing.Color ToolStripDropDownBackground => Background;
        public override Drawing.Color MenuBorder => Line;
        public override Drawing.Color MenuItemBorder => Hover;
        public override Drawing.Color MenuItemSelected => Hover;
        public override Drawing.Color MenuItemSelectedGradientBegin => Hover;
        public override Drawing.Color MenuItemSelectedGradientEnd => Hover;
        public override Drawing.Color ImageMarginGradientBegin => Background;
        public override Drawing.Color ImageMarginGradientMiddle => Background;
        public override Drawing.Color ImageMarginGradientEnd => Background;
        public override Drawing.Color SeparatorDark => Line;
        public override Drawing.Color SeparatorLight => Background;
    }
}
