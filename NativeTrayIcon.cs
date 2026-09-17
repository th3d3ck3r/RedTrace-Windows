using System.Runtime.InteropServices;

namespace RedTrace.Windows;

public sealed class NativeTrayIcon : IDisposable
{
    private const int WM_APP = 0x8000, WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205, GWLP_WNDPROC = -4;
    private const uint NIM_ADD = 0, NIM_DELETE = 2, NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;
    private const uint IMAGE_ICON = 1, LR_LOADFROMFILE = 0x10, LR_DEFAULTSIZE = 0x40, MF_STRING = 0, MF_SEPARATOR = 0x800, TPM_RETURNCMD = 0x100;
    private readonly IntPtr hwnd, icon;
    private readonly Action toggle, show, runner, quit;
    private readonly WndProc wndProc;
    private readonly IntPtr oldWndProc;
    private NotifyIconData data;

    public NativeTrayIcon(IntPtr hwnd, string iconPath, Action toggle, Action show, Action runner, Action quit)
    {
        this.hwnd = hwnd; this.toggle = toggle; this.show = show; this.runner = runner; this.quit = quit;
        icon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
        wndProc = WindowProc; oldWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(wndProc));
        data = new NotifyIconData
        {
            cbSize = Marshal.SizeOf<NotifyIconData>(), hWnd = hwnd, uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP, uCallbackMessage = WM_APP + 77,
            hIcon = icon, szTip = "RedTrace", szInfo = string.Empty, szInfoTitle = string.Empty
        };
        Shell_NotifyIcon(NIM_ADD, ref data);
    }

    private IntPtr WindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WM_APP + 77)
        {
            var mouseMessage = unchecked((int)(long)lParam) & 0xFFFF;
            if (mouseMessage == WM_LBUTTONUP) toggle();
            else if (mouseMessage == WM_RBUTTONUP) ShowMenu();
            return IntPtr.Zero;
        }
        return CallWindowProc(oldWndProc, window, message, wParam, lParam);
    }

    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        AppendMenu(menu, MF_STRING, 1, "Show RedTrace"); AppendMenu(menu, MF_STRING, 2, "New Runner"); AppendMenu(menu, MF_SEPARATOR, 0, null); AppendMenu(menu, MF_STRING, 3, "Quit");
        GetCursorPos(out var point); SetForegroundWindow(hwnd);
        var command = TrackPopupMenu(menu, TPM_RETURNCMD, point.X, point.Y, 0, hwnd, IntPtr.Zero);
        DestroyMenu(menu);
        if (command == 1) show(); else if (command == 2) runner(); else if (command == 3) quit();
    }

    public void Dispose()
    {
        Shell_NotifyIcon(NIM_DELETE, ref data);
        if (oldWndProc != IntPtr.Zero) SetWindowLongPtr(hwnd, GWLP_WNDPROC, oldWndProc);
        if (icon != IntPtr.Zero) DestroyIcon(icon);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize; public IntPtr hWnd; public uint uID, uFlags, uCallbackMessage; public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags; public Guid guidItem; public IntPtr hBalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int cx, int cy, uint load);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr previous, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(IntPtr menu, uint flags, uint id, string? text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hWnd, IntPtr rect);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
}
