namespace ZoneChampion.Core.Input;

/// <summary>A physical or synthetic key event. <paramref name="Extended"/> is the extended-key flag.</summary>
public readonly record struct KeyStroke(int VirtualKey, bool Extended, bool Down);

/// <param name="Inject">Synthetic key events to send, in order. Real key events are never blocked.</param>
/// <param name="Toggle">The chord was tapped: toggle the panels.</param>
public sealed record ChordDecision(IReadOnlyList<KeyStroke> Inject, bool Toggle)
{
    public static ChordDecision None { get; } = new([], false);
}

/// <summary>
/// Detects a "tap" of a modifier chord such as Ctrl+Win: every chord key pressed, then all of them released, with no
/// other key pressed in between. Shortcuts that start with the chord (Ctrl+Win+Left...) never count as a tap.
/// Fed every keyboard event system-wide; pure logic, the Windows hook lives in the app.
/// </summary>
/// <param name="isKeyDown">
/// Reports whether a key is currently down. Used to forget keys whose release was never seen (e.g. released on the
/// lock screen), which would otherwise block the next tap.
/// </param>
public sealed class ChordTapTracker(KeyChord chord, Func<int, bool>? isKeyDown = null)
{
    /// <summary>An unassigned virtual key (the one AutoHotkey uses) that apps and Windows ignore.</summary>
    public const int MaskKey = 0xE8;

    private readonly HashSet<int> _down = new();
    private bool _armed;
    private bool _spoiled;

    public KeyChord Chord { get; } = chord;

    public ChordDecision OnKey(KeyStroke key)
    {
        if (!Chord.Contains(key.VirtualKey))
        {
            if (key.Down && _down.Count > 0)
            {
                _spoiled = true;
            }

            return ChordDecision.None;
        }

        if (key.Down)
        {
            _down.Add(key.VirtualKey);
            if (_armed || _spoiled || !Chord.IsSatisfiedBy(_down))
            {
                return ChordDecision.None;
            }

            _armed = true;

            // Letting go of Win (or Alt) with no other key pressed since it went down opens the Start menu (or the
            // focused app's menu bar). A no-op key press in between prevents that.
            return _down.Any(NeedsMask)
                ? new ChordDecision([new KeyStroke(MaskKey, false, true), new KeyStroke(MaskKey, false, false)], false)
                : ChordDecision.None;
        }

        _down.Remove(key.VirtualKey);
        if (isKeyDown is not null)
        {
            _down.RemoveWhere(k => !isKeyDown(k));
        }

        if (_down.Count > 0)
        {
            return ChordDecision.None;
        }

        bool tapped = _armed && !_spoiled;
        _armed = false;
        _spoiled = false;
        return tapped ? new ChordDecision([], true) : ChordDecision.None;
    }

    private static bool NeedsMask(int virtualKey) => virtualKey is
        KeyChord.VK_LWIN or KeyChord.VK_RWIN or KeyChord.VK_LMENU or KeyChord.VK_RMENU;
}
