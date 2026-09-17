using System.Runtime.InteropServices;
using System.Threading;
using ZoneChampion.Core.Input;
using static ZoneChampion.Interop.Native;

namespace ZoneChampion.Interop;

/// <summary>
/// Watches the keyboard system-wide for a tap of a modifier chord (default Ctrl+Win). Windows' RegisterHotKey can't
/// register modifier-only hotkeys, so this uses a low-level keyboard hook. The hook runs on its own thread so busy
/// UI work never delays anyone's typing (Windows also drops hooks that answer too slowly). Keys are never blocked.
/// </summary>
internal sealed class ToggleHotkeyHook : IDisposable
{
    private const uint WM_RECONFIGURE = WM_APP + 1;

    // Marks our own injected keys so the hook ignores them.
    private static readonly nuint InjectedTag = 0x5A43484B;

    private readonly Action _onToggle;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _started = new();
    private readonly object _gate = new();
    private KeyChord? _requestedChord;
    private HookProc? _callback;
    private uint _threadId;

    // Hook thread only.
    private nint _hook;
    private ChordTapTracker? _tracker;

    /// <param name="onToggle">Called on the hook thread when the chord is tapped; marshal to the UI thread.</param>
    public ToggleHotkeyHook(Action onToggle)
    {
        _onToggle = onToggle;
        _thread = new Thread(Run) { IsBackground = true, Name = "Toggle hotkey hook" };
        _thread.Start();
        _started.Wait();
    }

    /// <summary>Sets the chord to watch for, or null to remove the hook entirely.</summary>
    public void SetChord(KeyChord? chord)
    {
        lock (_gate)
        {
            _requestedChord = chord;
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
        KeyChord? chord;
        lock (_gate)
        {
            chord = _requestedChord;
        }

        // Settings reloads create new chord objects; only restart tracking when the keys actually changed.
        if (chord?.ToString() == _tracker?.Chord.ToString())
        {
            return;
        }

        if (chord is null)
        {
            _tracker = null;
            RemoveHook();
            Log.Info("Toggle hotkey turned off");
            return;
        }

        _tracker = new ChordTapTracker(chord, vk => (GetAsyncKeyState(vk) & 0x8000) != 0);
        if (_hook == 0)
        {
            _callback = OnHook;
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _callback, GetModuleHandle(null), 0);
            if (_hook == 0)
            {
                Log.Warn($"Couldn't install keyboard hook for the toggle hotkey (error {Marshal.GetLastWin32Error()})");
                return;
            }
        }

        Log.Info($"Toggle hotkey: {chord}");
    }

    private nint OnHook(int code, nint wParam, nint lParam)
    {
        try
        {
            if (code >= 0 && _tracker is { } tracker)
            {
                var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int message = (int)wParam;
                bool down = message is WM_KEYDOWN or WM_SYSKEYDOWN;
                bool up = message is WM_KEYUP or WM_SYSKEYUP;
                if (info.dwExtraInfo != InjectedTag && (down || up))
                {
                    var decision = tracker.OnKey(new KeyStroke((int)info.vkCode, (info.flags & LLKHF_EXTENDED) != 0, down));
                    if (decision.Inject.Count > 0)
                    {
                        Send(decision.Inject);
                    }

                    if (decision.Toggle)
                    {
                        _onToggle();
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
