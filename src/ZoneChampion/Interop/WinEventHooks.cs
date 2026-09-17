using static ZoneChampion.Interop.Native;

namespace ZoneChampion.Interop;

/// <summary>
/// Subscribes to system-wide window events (create, move, minimize, focus...). Callbacks arrive on the thread that
/// created the hooks, which must pump messages — the WPF UI thread does.
/// </summary>
internal sealed class WinEventHooks : IDisposable
{
    private static readonly (uint Min, uint Max)[] Ranges =
    [
        (EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND),
        (EVENT_SYSTEM_MOVESIZESTART, EVENT_SYSTEM_MOVESIZEEND),
        (EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZEEND),
        (EVENT_OBJECT_CREATE, EVENT_OBJECT_HIDE),
        (EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_NAMECHANGE),
        (EVENT_OBJECT_CLOAKED, EVENT_OBJECT_UNCLOAKED),
    ];

    private readonly WinEventProc _callback; // held so the delegate isn't garbage collected
    private readonly List<nint> _hooks = new();
    private readonly Action<uint, nint> _handler;

    public WinEventHooks(Action<uint, nint> handler)
    {
        _handler = handler;
        _callback = OnEvent;
        foreach (var (min, max) in Ranges)
        {
            nint hook = SetWinEventHook(min, max, 0, _callback, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
            if (hook == 0)
            {
                Log.Warn($"SetWinEventHook failed for events 0x{min:X}-0x{max:X}");
            }
            else
            {
                _hooks.Add(hook);
            }
        }
    }

    private void OnEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd == 0 || idObject != OBJID_WINDOW || idChild != CHILDID_SELF)
        {
            return;
        }

        try
        {
            _handler(eventType, hwnd);
        }
        catch (Exception ex)
        {
            Log.Error("WinEvent handler failed", ex);
        }
    }

    public void Dispose()
    {
        foreach (var hook in _hooks)
        {
            UnhookWinEvent(hook);
        }

        _hooks.Clear();
    }
}
