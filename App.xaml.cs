using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace RedTrace.Windows;

public partial class App : Application
{
    private MainWindow? mainWindow;
    private NativeTrayIcon? tray;
    internal static bool IsExiting { get; private set; }

    public App()
    {
        UnhandledException += (_, e) => { WriteCrashLog(e.Exception); e.Handled = true; };
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            try { HookInstaller.Install(silent: true); } catch (Exception hookError) { WriteCrashLog(hookError, "Hook setup"); }
            mainWindow = new MainWindow();
            mainWindow.Activate();
            try { CreateTray(); } catch (Exception trayError) { WriteCrashLog(trayError, "Tray setup"); }
        }
        catch (Exception error)
        {
            WriteCrashLog(error, "Startup");
            MessageBox(IntPtr.Zero, $"RedTrace could not start.\n\n{error.Message}\n\nA log was saved to:\n{LogPath}", "RedTrace startup error", 0x10);
            Exit();
        }
    }

    private void CreateTray()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "RedTrace.ico");
        tray = new NativeTrayIcon(
            mainWindow!.WindowHandle,
            iconPath,
            () => mainWindow.DispatcherQueue.TryEnqueue(mainWindow.ToggleVisibility),
            () => mainWindow.DispatcherQueue.TryEnqueue(mainWindow.ShowAndActivate),
            () => mainWindow.DispatcherQueue.TryEnqueue(() => new MainWindow(PanelMode.Run).Activate()),
            () => mainWindow.DispatcherQueue.TryEnqueue(Quit));
    }

    private void Quit()
    {
        IsExiting = true;
        tray?.Dispose();
        mainWindow?.CloseForReal();
        Exit();
    }

    private static string LogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RedTrace", "crash.log");

    private static void WriteCrashLog(Exception error, string area = "Unhandled")
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTimeOffset.Now:O}] {area}\n{error}\n\n");
        }
        catch { }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr window, string text, string caption, uint type);
}
