using System.Runtime.InteropServices;
using System.Windows.Interop;
using WinDock.Core;

namespace WinDock.Shell;

public sealed class ShellHost : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int WmDisplayChange = 0x007E;
    private const int HotkeyNext = 1;
    private const int HotkeyPrev = 2;
    private const int HotkeyUndo = 3;
    private const int HotkeyReveal = 4;

    private readonly HwndSource _source;
    private readonly uint _taskbarCreated;
    private readonly WinEventDelegate _winEvent;
    private readonly List<nint> _hooks = [];
    private bool _disposed;

    public ShellHost()
    {
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        _winEvent = OnWinEvent;
        _source = new HwndSource(new HwndSourceParameters("WinDockHost")
        {
            ParentWindow = new IntPtr(-3),
            Width = 0,
            Height = 0,
            WindowStyle = 0
        });
        _source.AddHook(WndProc);
        Hook(EventSystemForeground);
        Hook(EventSystemMoveSizeEnd);
        Hook(EventSystemMinimizeEnd);
    }

    public event EventHandler? DisplayChanged;

    public event EventHandler? TaskbarCreated;

    public event EventHandler? ZorderHint;

    public event EventHandler<HotkeyAction>? HotkeyPressed;

    public nint Handle => _source.Handle;

    /// <summary>Registers hotkeys. Returns false when <see cref="HotkeySettings.RevealDock"/> is set but could not be registered.</summary>
    public bool BindHotkeys(HotkeySettings hotkeys)
    {
        UnregisterHotKey(_source.Handle, HotkeyNext);
        UnregisterHotKey(_source.Handle, HotkeyPrev);
        UnregisterHotKey(_source.Handle, HotkeyUndo);
        UnregisterHotKey(_source.Handle, HotkeyReveal);
        return TryRegister(HotkeyReveal, hotkeys.RevealDock);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var hook in _hooks)
        {
            if (hook != nint.Zero)
            {
                UnhookWinEvent(hook);
            }
        }

        _hooks.Clear();
        UnregisterHotKey(_source.Handle, HotkeyNext);
        UnregisterHotKey(_source.Handle, HotkeyPrev);
        UnregisterHotKey(_source.Handle, HotkeyUndo);
        UnregisterHotKey(_source.Handle, HotkeyReveal);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private bool TryRegister(int id, string? chord)
    {
        if (string.IsNullOrWhiteSpace(chord))
        {
            return true;
        }

        if (!HotkeyParser.TryParse(chord, out var modifiers, out var vk))
        {
            return false;
        }

        return RegisterHotKey(_source.Handle, id, (uint)modifiers, (uint)vk);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            var action = (int)wParam switch
            {
                HotkeyNext => HotkeyAction.NextProfile,
                HotkeyPrev => HotkeyAction.PreviousProfile,
                HotkeyUndo => HotkeyAction.UndoProfile,
                HotkeyReveal => HotkeyAction.RevealDock,
                _ => (HotkeyAction?)null
            };
            if (action is { } hotkey)
            {
                HotkeyPressed?.Invoke(this, hotkey);
                handled = true;
            }
        }
        else if (msg == WmDisplayChange)
        {
            DisplayChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (_taskbarCreated != 0 && (uint)msg == _taskbarCreated)
        {
            TaskbarCreated?.Invoke(this, EventArgs.Empty);
        }

        return 0;
    }

    private void Hook(uint eventId)
    {
        var handle = SetWinEventHook(
            eventId,
            eventId,
            nint.Zero,
            _winEvent,
            0,
            0,
            WinEventOutOfContext | WinEventSkipOwnProcess);
        if (handle != nint.Zero)
        {
            _hooks.Add(handle);
        }
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint eventThread, uint eventTime)
    {
        if (_disposed)
        {
            return;
        }

        ZorderHint?.Invoke(this, EventArgs.Empty);
    }

    private delegate void WinEventDelegate(
        nint hWinEventHook,
        uint eventType,
        nint hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);

    private const uint EventSystemForeground = 0x0003;
    private const uint EventSystemMoveSizeEnd = 0x000B;
    private const uint EventSystemMinimizeEnd = 0x0017;
    private const uint WinEventOutOfContext = 0x0000;
    private const uint WinEventSkipOwnProcess = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(nint hWinEventHook);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);
}
