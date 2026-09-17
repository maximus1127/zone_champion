using System.Runtime.InteropServices;
using static ZoneChampion.Interop.Native;

namespace ZoneChampion.Interop;

/// <summary>
/// Reports every mouse button press anywhere on screen while alive. Used to dismiss the panel menu when the user
/// clicks another app: a popup on a never-activated window doesn't receive those clicks through mouse capture.
/// Keep instances short-lived; the callback runs on the creating (UI) thread for every press.
/// </summary>
internal sealed class MouseDownWatcher : IDisposable
{
    private readonly HookProc _callback; // held so the delegate isn't garbage collected
    private readonly Action<int, int> _onMouseDown;
    private nint _hook;

    public MouseDownWatcher(Action<int, int> onMouseDown)
    {
        _onMouseDown = onMouseDown;
        _callback = OnHook;
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _callback, GetModuleHandle(null), 0);
        if (_hook == 0)
        {
            Log.Warn($"Couldn't install mouse hook (error {Marshal.GetLastWin32Error()})");
        }
    }

    private nint OnHook(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && (int)wParam is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN)
        {
            var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            try
            {
                _onMouseDown(info.pt.X, info.pt.Y);
            }
            catch (Exception ex)
            {
                Log.Error("Mouse hook handler failed", ex);
            }
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != 0)
        {
            UnhookWindowsHookEx(_hook);
            _hook = 0;
        }
    }
}
