using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using ZoneChampion.Core.Settings;
using ZoneChampion.Core.Zones;

namespace ZoneChampion.Panels;

internal abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(name);
        return true;
    }
}

/// <summary>What a panel item should show; produced by <see cref="PanelManager"/> on every refresh.</summary>
internal readonly record struct PanelItemState(nint Handle, string Title, bool IsActive, bool IsMinimized, string? TagColor);

internal sealed class PanelItemViewModel(nint handle, PanelViewModel panel) : ObservableObject
{
    private string _title = "";
    private ImageSource? _icon;
    private bool _isActive;
    private bool _isMinimized;
    private bool _isHovered;
    private bool _isDragging;
    private string? _tagColor;

    public nint Handle { get; } = handle;
    public PanelViewModel Panel { get; } = panel;
    public PanelTheme Theme => Panel.Theme;

    /// <summary>Pixel size of the icon last requested for this item (depends on icon size and monitor scaling).</summary>
    public int IconPixels { get; set; }

    public string Title { get => _title; set => Set(ref _title, value); }

    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (Set(ref _icon, value))
            {
                Raise(nameof(PlaceholderVisibility));
            }
        }
    }

    public bool IsActive { get => _isActive; set => SetVisual(ref _isActive, value); }
    public bool IsMinimized { get => _isMinimized; set => SetVisual(ref _isMinimized, value); }
    public bool IsHovered { get => _isHovered; set => SetVisual(ref _isHovered, value); }
    public bool IsDragging { get => _isDragging; set => SetVisual(ref _isDragging, value); }
    public string? TagColor { get => _tagColor; set => SetVisual(ref _tagColor, value); }

    public Visibility PlaceholderVisibility => Icon is null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ActiveVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HoverVisibility => IsHovered || IsDragging ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IndicatorVisibility => IsActive && Theme.ShowIndicator ? Visibility.Visible : Visibility.Collapsed;
    public double IconOpacity => IsDragging ? 0.7 : IsMinimized ? Theme.MinimizedOpacity : 1;
    public double TileScale => IsDragging ? 1.08 : 1;

    public Brush? TagBrush => TagColor is null ? null : PanelTheme.Brush(TagColor, 1);
    public Visibility TagDotVisibility => TagColor is not null && Theme.TagStyle == TagStyle.Dot ? Visibility.Visible : Visibility.Collapsed;
    public Brush? TagTileBrush => TagColor is not null && Theme.TagStyle == TagStyle.Tile ? PanelTheme.Brush(TagColor, Theme.TagTileOpacity) : null;

    public void Apply(PanelItemState state)
    {
        Title = state.Title;
        IsActive = state.IsActive;
        IsMinimized = state.IsMinimized;
        TagColor = state.TagColor;
    }

    /// <summary>Re-evaluates everything derived from the theme.</summary>
    public void RefreshTheme()
    {
        Raise(nameof(Theme));
        RaiseVisuals();
    }

    private void SetVisual<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Set(ref field, value, name))
        {
            RaiseVisuals();
        }
    }

    private void RaiseVisuals()
    {
        Raise(nameof(ActiveVisibility));
        Raise(nameof(HoverVisibility));
        Raise(nameof(IndicatorVisibility));
        Raise(nameof(IconOpacity));
        Raise(nameof(TileScale));
        Raise(nameof(TagBrush));
        Raise(nameof(TagDotVisibility));
        Raise(nameof(TagTileBrush));
    }
}

internal sealed class PanelViewModel(ZoneKey key, PanelTheme theme) : ObservableObject
{
    private PanelTheme _theme = theme;

    public ZoneKey Key { get; } = key;
    public ObservableCollection<PanelItemViewModel> Items { get; } = new();

    public PanelTheme Theme
    {
        get => _theme;
        set
        {
            if (Set(ref _theme, value))
            {
                foreach (var item in Items)
                {
                    item.RefreshTheme();
                }
            }
        }
    }

    public PanelItemViewModel? Find(nint handle) => Items.FirstOrDefault(i => i.Handle == handle);

    /// <summary>Updates <see cref="Items"/> to match <paramref name="states"/> with minimal changes, so hover state,
    /// icons and item containers survive refreshes.</summary>
    /// <returns>Items that were created and still need an icon.</returns>
    public List<PanelItemViewModel> Sync(IReadOnlyList<PanelItemState> states)
    {
        var wanted = states.Select(s => s.Handle).ToHashSet();
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(Items[i].Handle))
            {
                Items.RemoveAt(i);
            }
        }

        var created = new List<PanelItemViewModel>();
        for (int index = 0; index < states.Count; index++)
        {
            var state = states[index];
            int existing = -1;
            for (int j = index; j < Items.Count; j++)
            {
                if (Items[j].Handle == state.Handle)
                {
                    existing = j;
                    break;
                }
            }

            PanelItemViewModel item;
            if (existing < 0)
            {
                item = new PanelItemViewModel(state.Handle, this);
                Items.Insert(index, item);
                created.Add(item);
            }
            else
            {
                item = Items[existing];
                if (existing != index)
                {
                    Items.Move(existing, index);
                }
            }

            item.Apply(state);
        }

        return created;
    }
}
