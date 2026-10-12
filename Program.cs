using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ChromeRemoteBarHide;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => Diagnostics.Log($"UI exception: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Diagnostics.Log($"Unhandled exception: {e.ExceptionObject}");
        Diagnostics.Log("Starting ChromeRemoteBarHide");
        using var controller = new BarController();
        Application.Run();
        Diagnostics.Log("Stopped ChromeRemoteBarHide");
    }
}

internal enum BarMode
{
    Disabled,
    AutoHide,
    Top,
    Bottom,
    Left,
    Right
}

internal sealed class BarController : IDisposable
{
    private const int HotkeyHide = 1;
    private const int HotkeyRestore = 2;
    private const int HotkeyTop = 3;
    private const int HotkeyBottom = 4;
    private const int HotkeyLeft = 5;
    private const int HotkeyRight = 6;

    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly HotkeyWindow _hotkeyWindow;
    private readonly ToolStripMenuItem _modeItem;
    private readonly ToolStripMenuItem _hotkeyItem;
    private readonly HashSet<int> _registeredHotkeys = [];
    private BarMode _mode = BarMode.AutoHide;
    private int _lastMatchCount = -1;
    private string _restoreHotkeyName = "Ctrl+Alt+R";
    private bool _disposed;

    public BarController()
    {
        _modeItem = new ToolStripMenuItem("模式") { Enabled = false };
        _hotkeyItem = new ToolStripMenuItem("Hotkeys: registering") { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add("自動隱藏", null, (_, _) => SetMode(BarMode.AutoHide));
        menu.Items.Add("移到上方", null, (_, _) => SetMode(BarMode.Top));
        menu.Items.Add("移到下方", null, (_, _) => SetMode(BarMode.Bottom));
        menu.Items.Add("移到左側", null, (_, _) => SetMode(BarMode.Left));
        menu.Items.Add("移到右側", null, (_, _) => SetMode(BarMode.Right));
        menu.Items.Add("停用並顯示", null, (_, _) => SetMode(BarMode.Disabled));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("立即顯示提示", null, (_, _) => ShowMatchingWindows());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_modeItem);
        menu.Items.Add(_hotkeyItem);
        menu.Items.Add("結束", null, (_, _) => Application.Exit());

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Chrome Remote Desktop 浮窗控制",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ToggleHide();

        _hotkeyWindow = new HotkeyWindow(HandleHotkey);
        RegisterHotkeys();

        _timer = new System.Windows.Forms.Timer { Interval = 500 };
        _timer.Tick += (_, _) => ApplyToMatchingWindows();
        _timer.Start();
        UpdateModeText();
        UpdateHotkeyText();
    }

    private void RegisterHotkeys()
    {
        const uint controlAlt = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT;
        Register(HotkeyHide, controlAlt, Keys.H, "Ctrl+Alt+H");
        if (!Register(HotkeyRestore, controlAlt, Keys.R, "Ctrl+Alt+R"))
        {
            const uint controlAltShift = controlAlt | NativeMethods.MOD_SHIFT;
            if (Register(HotkeyRestore, controlAltShift, Keys.R, "Ctrl+Alt+Shift+R"))
            {
                _restoreHotkeyName = "Ctrl+Alt+Shift+R";
                Diagnostics.Log("Ctrl+Alt+R was unavailable; using fallback Ctrl+Alt+Shift+R");
            }
        }
        Register(HotkeyTop, controlAlt, Keys.T, "Ctrl+Alt+T");
        Register(HotkeyBottom, controlAlt, Keys.B, "Ctrl+Alt+B");
        Register(HotkeyLeft, controlAlt, Keys.L, "Ctrl+Alt+L");
        Register(HotkeyRight, controlAlt, Keys.K, "Ctrl+Alt+K");
    }

    private bool Register(int id, uint modifiers, Keys key, string name)
    {
        if (NativeMethods.RegisterHotKey(_hotkeyWindow.Handle, id, modifiers | NativeMethods.MOD_NOREPEAT, (uint)key))
        {
            _registeredHotkeys.Add(id);
            Diagnostics.Log($"Registered hotkey {name}");
            return true;
        }

        Diagnostics.Log($"Failed to register hotkey {name}; Win32 error {Marshal.GetLastWin32Error()}");
        return false;
    }

    private void HandleHotkey(int id)
    {
        try
        {
            switch (id)
            {
                case HotkeyHide:
                    SetMode(BarMode.AutoHide);
                    break;
                case HotkeyRestore:
                    SetMode(BarMode.Disabled);
                    break;
                case HotkeyTop:
                    SetMode(BarMode.Top);
                    break;
                case HotkeyBottom:
                    SetMode(BarMode.Bottom);
                    break;
                case HotkeyLeft:
                    SetMode(BarMode.Left);
                    break;
                case HotkeyRight:
                    SetMode(BarMode.Right);
                    break;
            }
            Diagnostics.Log($"Handled hotkey id {id}; mode {_mode}");
        }
        catch (Exception ex)
        {
            Diagnostics.Log($"Hotkey handler exception: {ex}");
        }
    }

    private void ToggleHide()
    {
        SetMode(_mode == BarMode.AutoHide ? BarMode.Disabled : BarMode.AutoHide);
    }

    private void SetMode(BarMode mode)
    {
        _mode = mode;
        UpdateModeText();
        ApplyToMatchingWindows();
    }

    private void UpdateHotkeyText()
    {
        var status = _registeredHotkeys.Count == 6
            ? "Hotkeys: registered"
            : $"Hotkeys: {_registeredHotkeys.Count}/6 registered (see debug.log)";
        if (_restoreHotkeyName != "Ctrl+Alt+R")
            status += $"; restore: {_restoreHotkeyName}";
        _hotkeyItem.Text = status;
    }

    private void UpdateModeText()
    {
        _modeItem.Text = $"目前模式：{_mode switch
        {
            BarMode.AutoHide => "自動隱藏",
            BarMode.Top => "上方",
            BarMode.Bottom => "下方",
            BarMode.Left => "左側",
            BarMode.Right => "右側",
            _ => "停用"
        }}";
    }

    private void ApplyToMatchingWindows()
    {
        try
        {
            var matches = FindSharingBars();
            if (matches.Count != _lastMatchCount)
            {
                _lastMatchCount = matches.Count;
                Diagnostics.Log($"Matching sharing bars: {matches.Count}; mode {_mode}");
            }

            foreach (var hwnd in matches)
            {
                if (_mode == BarMode.AutoHide)
                {
                    NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
                    continue;
                }

                if (_mode == BarMode.Disabled)
                {
                    NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);
                    continue;
                }

                MoveWindow(hwnd, _mode);
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Log($"Window scan exception: {ex}");
        }
    }

    private void ShowMatchingWindows()
    {
        foreach (var hwnd in FindSharingBars())
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);
    }

    private static void MoveWindow(IntPtr hwnd, BarMode mode)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
            return;

        var monitor = Screen.FromHandle(hwnd).WorkingArea;
        var width = Math.Max(1, rect.Right - rect.Left);
        var height = Math.Max(1, rect.Bottom - rect.Top);
        const int margin = 8;

        var x = monitor.Left + (monitor.Width - width) / 2;
        var y = monitor.Top + margin;
        switch (mode)
        {
            case BarMode.Bottom:
                y = monitor.Bottom - height - margin;
                break;
            case BarMode.Left:
                x = monitor.Left + margin;
                y = monitor.Top + (monitor.Height - height) / 2;
                break;
            case BarMode.Right:
                x = monitor.Right - width - margin;
                y = monitor.Top + (monitor.Height - height) / 2;
                break;
        }

        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            x,
            y,
            width,
            height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    private static IReadOnlyList<IntPtr> FindSharingBars()
    {
        var matches = new List<IntPtr>();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindow(hwnd) || !IsSharingBar(hwnd))
                return true;

            matches.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return matches;
    }

    private static bool IsSharingBar(IntPtr hwnd)
    {
        var className = NativeMethods.GetClassName(hwnd);
        if (!string.Equals(className, "#32770", StringComparison.Ordinal) &&
            !className.Contains("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
            return false;

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0 || width > 900 || height > 320)
            return false;

        var text = GetWindowTreeText(hwnd).ToLowerInvariant();
        var status = text.Contains("共用電腦", StringComparison.Ordinal) ||
                     text.Contains("共享电脑", StringComparison.Ordinal) ||
                     text.Contains("目前正與", StringComparison.Ordinal) ||
                     text.Contains("currently shared with", StringComparison.Ordinal) ||
                     text.Contains("desktop is currently shared", StringComparison.Ordinal) ||
                     text.Contains("your computer is currently shared", StringComparison.Ordinal) ||
                     text.Contains("currently sharing this machine", StringComparison.Ordinal) ||
                     text.Contains("目前正在與其他使用者共用", StringComparison.Ordinal) ||
                     text.Contains("正在與其他使用者共用", StringComparison.Ordinal);
        var stop = text.Contains("停止共用", StringComparison.Ordinal) ||
                   text.Contains("停止共享", StringComparison.Ordinal) ||
                   text.Contains("stop sharing", StringComparison.Ordinal);
        var continueSharing = text.Contains("continue sharing", StringComparison.Ordinal) ||
                              text.Contains("continue", StringComparison.Ordinal) ||
                              text.Contains("繼續共用", StringComparison.Ordinal) ||
                              text.Contains("繼續分享", StringComparison.Ordinal) ||
                              text.Contains("繼續", StringComparison.Ordinal);

        // Remote support uses a Stop Sharing button; remote access can show a
        // continuation prompt instead, so both are valid sharing indicators.
        return status && (stop || continueSharing);
    }

    private static string GetWindowTreeText(IntPtr hwnd)
    {
        var builder = new StringBuilder();
        AppendWindowText(hwnd, builder);
        NativeMethods.EnumChildWindows(hwnd, (child, _) =>
        {
            AppendWindowText(child, builder);
            return true;
        }, IntPtr.Zero);
        return builder.ToString();
    }

    private static void AppendWindowText(IntPtr hwnd, StringBuilder builder)
    {
        var text = NativeMethods.GetWindowText(hwnd);
        if (text.Length > 0)
        {
            builder.Append(' ');
            builder.Append(text);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Stop();
        _timer.Dispose();
        foreach (var id in _registeredHotkeys)
            NativeMethods.UnregisterHotKey(_hotkeyWindow.Handle, id);
        _hotkeyWindow.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}

internal static class Diagnostics
{
    private static readonly object Gate = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ChromeRemoteBarHide");
    private static readonly string LogPath = Path.Combine(DirectoryPath, "debug.log");

    internal static void Log(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostics must never terminate the utility.
        }
    }
}

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private readonly Action<int> _onHotkey;

    public HotkeyWindow(Action<int> onHotkey)
    {
        _onHotkey = onHotkey;
        CreateHandle(new CreateParams());
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
            _onHotkey(m.WParam.ToInt32());
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        DestroyHandle();
    }
}

internal static class NativeMethods
{
    internal const int WM_HOTKEY = 0x0312;
    internal const uint MOD_ALT = 0x0001;
    internal const uint MOD_CONTROL = 0x0002;
    internal const uint MOD_SHIFT = 0x0004;
    internal const uint MOD_NOREPEAT = 0x4000;
    internal const int SW_HIDE = 0;
    internal const int SW_SHOWNOACTIVATE = 4;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;
    internal static readonly IntPtr HWND_TOPMOST = new(-1);

    internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    internal static string GetWindowText(IntPtr hwnd)
    {
        var builder = new StringBuilder(512);
        GetWindowTextW(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    internal static string GetClassName(IntPtr hwnd)
    {
        var builder = new StringBuilder(256);
        GetClassNameW(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }
}
