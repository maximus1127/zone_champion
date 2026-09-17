namespace ZoneChampion.Core.Input;

/// <summary>
/// A set of modifier keys that must all be held, parsed from text like <c>"Ctrl+Win"</c>. Only modifiers are
/// allowed: holding them types nothing, so the chord never leaks characters into the focused app.
/// </summary>
public sealed class KeyChord
{
    public const int VK_LSHIFT = 0xA0;
    public const int VK_RSHIFT = 0xA1;
    public const int VK_LCONTROL = 0xA2;
    public const int VK_RCONTROL = 0xA3;
    public const int VK_LMENU = 0xA4;
    public const int VK_RMENU = 0xA5;
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;

    private static readonly (string Name, int[] Keys)[] KnownKeys =
    [
        ("Ctrl", [VK_LCONTROL, VK_RCONTROL]),
        ("LCtrl", [VK_LCONTROL]),
        ("RCtrl", [VK_RCONTROL]),
        ("Alt", [VK_LMENU, VK_RMENU]),
        ("LAlt", [VK_LMENU]),
        ("RAlt", [VK_RMENU]),
        ("Shift", [VK_LSHIFT, VK_RSHIFT]),
        ("LShift", [VK_LSHIFT]),
        ("RShift", [VK_RSHIFT]),
        ("Win", [VK_LWIN, VK_RWIN]),
        ("LWin", [VK_LWIN]),
        ("RWin", [VK_RWIN]),
    ];

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Control"] = "Ctrl",
        ["Windows"] = "Win",
    };

    private readonly (string Name, int[] Keys)[] _groups;

    private KeyChord((string Name, int[] Keys)[] groups) => _groups = groups;

    public static KeyChord Default { get; } = Parse("Ctrl+Win");

    /// <summary>Whether <paramref name="virtualKey"/> is one of this chord's keys.</summary>
    public bool Contains(int virtualKey) => _groups.Any(g => g.Keys.Contains(virtualKey));

    /// <summary>Whether the held keys cover every part of the chord (e.g. either Ctrl plus either Win).</summary>
    public bool IsSatisfiedBy(IEnumerable<int> heldKeys)
    {
        var held = heldKeys as ICollection<int> ?? heldKeys.ToList();
        return _groups.All(g => g.Keys.Any(held.Contains));
    }

    public static KeyChord Parse(string text) =>
        TryParse(text, out var chord, out var error) ? chord! : throw new FormatException(error);

    public static bool TryParse(string? text, out KeyChord? chord, out string? error)
    {
        chord = null;
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "no keys given";
            return false;
        }

        var groups = new List<(string Name, int[] Keys)>();
        foreach (var rawPart in text.Split('+'))
        {
            var part = rawPart.Trim();
            if (Aliases.TryGetValue(part, out var canonical))
            {
                part = canonical;
            }

            var match = KnownKeys.FirstOrDefault(k => k.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (match.Name is null)
            {
                error = $"\"{rawPart.Trim()}\" isn't a modifier key. Use {string.Join(", ", KnownKeys.Select(k => k.Name))} joined with +";
                return false;
            }

            if (groups.Any(g => g.Keys.Intersect(match.Keys).Any()))
            {
                error = $"\"{match.Name}\" overlaps another key in \"{text}\"";
                return false;
            }

            groups.Add(match);
        }

        chord = new KeyChord(groups.ToArray());
        return true;
    }

    public override string ToString() => string.Join("+", _groups.Select(g => g.Name));
}
