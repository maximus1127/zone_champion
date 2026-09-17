using ZoneChampion.Core.Zones;

namespace ZoneChampion.Core.Panels;

/// <summary>
/// Remembers the icon order of each zone's panel. New windows are appended; windows the user dragged keep
/// their slot. Order lives in memory only and resets when the app restarts.
/// </summary>
public sealed class PanelOrdering
{
    private readonly Dictionary<ZoneKey, List<nint>> _order = new();
    private readonly Dictionary<ZoneKey, List<nint>> _visible = new();

    /// <param name="assignments">Every tracked window and its zone, oldest-discovered first.</param>
    /// <param name="isAlive">
    /// Whether a window that is not currently assigned still exists (e.g. it's on another virtual desktop).
    /// Such windows keep their slot so they come back in the same place.
    /// </param>
    public void Apply(IReadOnlyList<(nint Window, ZoneKey Zone)> assignments, Func<nint, bool> isAlive)
    {
        var zoneOf = new Dictionary<nint, ZoneKey>(assignments.Count);
        foreach (var (window, zone) in assignments)
        {
            zoneOf[window] = zone;
        }

        foreach (var (zone, list) in _order)
        {
            list.RemoveAll(w => zoneOf.TryGetValue(w, out var assigned) ? assigned != zone : !isAlive(w));
        }

        foreach (var (window, zone) in assignments)
        {
            if (!_order.TryGetValue(zone, out var list))
            {
                list = new List<nint>();
                _order[zone] = list;
            }

            if (!list.Contains(window))
            {
                list.Add(window);
            }
        }

        _visible.Clear();
        foreach (var (zone, list) in _order)
        {
            _visible[zone] = list.Where(w => zoneOf.TryGetValue(w, out var assigned) && assigned == zone).ToList();
        }
    }

    /// <summary>The windows currently shown in a zone's panel, in display order.</summary>
    public IReadOnlyList<nint> Get(ZoneKey zone) =>
        _visible.TryGetValue(zone, out var list) ? list : Array.Empty<nint>();

    /// <summary>Moves a window to <paramref name="newIndex"/> among the zone's currently shown windows.</summary>
    public bool Move(ZoneKey zone, nint window, int newIndex)
    {
        if (!_order.TryGetValue(zone, out var list) || !_visible.TryGetValue(zone, out var visible))
        {
            return false;
        }

        int oldVisibleIndex = visible.IndexOf(window);
        if (oldVisibleIndex < 0)
        {
            return false;
        }

        newIndex = Math.Clamp(newIndex, 0, visible.Count - 1);
        if (newIndex == oldVisibleIndex)
        {
            return false;
        }

        visible.RemoveAt(oldVisibleIndex);
        list.Remove(window);

        if (newIndex < visible.Count)
        {
            list.Insert(list.IndexOf(visible[newIndex]), window);
        }
        else
        {
            list.Insert(list.IndexOf(visible[^1]) + 1, window);
        }

        visible.Insert(newIndex, window);
        return true;
    }
}
