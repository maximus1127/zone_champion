using System.Runtime.InteropServices;
using System.Threading;
using ZoneChampion.Core.Input;
using static ZoneChampion.Interop.Native;

namespace ZoneChampion.Interop;

/// <summary>A modifier chord to watch for and what to do when it's tapped.</summary>
/// <param name="Name">Used in log messages, e.g. "Toggle hotkey".</param>
/// <param name="OnTap">Called on the hook thread when the chord is tapped; marshal to the UI thread.</param>
internal sealed record HotkeyBinding(string Name, KeyChord Chord, Action OnTap);

/// <summary>
/// Watches the keyboard system-wide for taps of modifier chords (default Ctrl+Win and Ctrl+Alt+Win). Windows'
/// RegisterHotKey can't register modifier-only hotkeys, so this uses a low-level keyboard hook. The hook runs on its
/// own thread so busy UI work never delays anyone's typing (Windows also drops hooks that answer too slowly). Keys are
/// never blocked.
/// </summary>
internal sealed class HotkeyHook : IDisposable
{
    private const uint WM_RECONFIGURE = WM_APP + 1;

    // Marks our own injected keys so the hook ignores them.
    private static readonly nuint InjectedTag = 0x5A43484B;

    private readonly Thread _thread;
    private readonly ManualResetEventSlim _started = new();
    private readonly object _gate = new();
    private IReadOnlyList<HotkeyBinding> _requestedBindings = [];
    private HookProc? _callback;
    private uint _threadId;

    // Hook thread only.
    private nint _hook;
    private List<(HotkeyBinding Binding, ChordTapTracker Tracker)> _trackers = new();

    public HotkeyHook()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Hotkey hook" };
        _thread.Start();
        _started.Wait();
    }

    /// <summary>Sets the chords to watch for; with none, the hook is removed entirely.</summary>
    public void SetBindings(IReadOnlyList<HotkeyBinding> bindings)
    {
        lock (_gate)
        {
            _requestedBindings = bindings;
        }

        PostThreadMessage(_threadId, WM_RECONFIGURE, 0, 0);
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();

        // Create this thread's message queue before anyone posts to it.
        PeekMessage(out _, 0, 0, 0, PM_NOREMOVE);
        _started.Set();

        while (GetMessage(out var msg, 0, 0, 0) > 0)
        {
            if (msg.message == WM_RECONFIGURE)
            {
                Reconfigure();
            }
        }

        RemoveHook();
    }

    private void Reconfigure()
    {
        IReadOnlyList<HotkeyBinding> bindings;
        lock (_gate)
        {
            bindings = _requestedBindings;
        }

        // Settings reloads create new chord objects; only restart tracking when the keys actually changed.
        static string Describe(IEnumerable<HotkeyBinding> list) => string.Join(", ", list.Select(b => $"{b.Name}: {b.Chord}"));
        string description = Describe(bindings);
        if (description == Describe(_trackers.Select(t => t.Binding)))
        {
            return;
        }

        _trackers = bindings
            .Select(b => (b, new ChordTapTracker(b.Chord, vk => (GetAsyncKeyState(vk) & 0x8000) != 0)))
            .ToList();

        if (_trackers.Count == 0)
        {
            RemoveHook();
            Log.Info("Hotkeys turned off");
            return;
        }

        if (_hook == 0)
        {
            _callback = OnHook;
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _callback, GetModuleHandle(null), 0);
            if (_hook == 0)
            {
                Log.Warn($"Couldn't install keyboard hook for the hotkeys (error {Marshal.GetLastWin32Error()})");
                return;
            }
        }

        Log.Info($"Hotkeys: {description}");
    }

    private nint OnHook(int code, nint wParam, nint lParam)
    {
        try
        {
            if (code >= 0 && _trackers.Count > 0)
            {
                var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int message = (int)wParam;
                bool down = message is WM_KEYDOWN or WM_SYSKEYDOWN;
                bool up = message is WM_KEYUP or WM_SYSKEYUP;
                if (info.dwExtraInfo != InjectedTag && (down || up))
                {
                    var stroke = new KeyStroke((int)info.vkCode, (info.flags & LLKHF_EXTENDED) != 0, down);
                    var decisions = _trackers.Select(t => (t.Binding, Decision: t.Tracker.OnKey(stroke))).ToList();

                    // Every tracker asks for the same mask key; sending it once is enough.
                    if (decisions.Select(d => d.Decision.Inject).FirstOrDefault(i => i.Count > 0) is { } inject)
                    {
                        Send(inject);
                    }

                    foreach (var (binding, decision) in decisions)
                    {
                        if (decision.Toggle)
                        {
                            binding.OnTap();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Keyboard hook failed", ex);
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private static void Send(IReadOnlyList<KeyStroke> strokes)
    {
        var inputs = strokes.Select(s => new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)s.VirtualKey,
                    dwFlags = (s.Down ? 0 : KEYEVENTF_KEYUP) | (s.Extended ? KEYEVENTF_EXTENDEDKEY : 0),
                    dwExtraInfo = InjectedTag,
                },
            },
        }).ToArray();

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private void RemoveHook()
    {
        if (_hook != 0)
        {
            UnhookWindowsHookEx(_hook);
            _hook = 0;
        }
    }

    public void Dispose()
    {
        PostThreadMessage(_threadId, WM_QUIT, 0, 0);
        _thread.Join(TimeSpan.FromSeconds(2));
        _started.Dispose();
    }
}
