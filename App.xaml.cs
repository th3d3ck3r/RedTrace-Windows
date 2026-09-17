using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace RedTrace.Windows;

public partial class App : Application
{
    private MainWindow? mainWindow;
    private NativeTrayIcon? tray;
    private DispatcherTimer? smokeTimer;
    internal static bool IsExiting { get; private set; }

    public App()
    {
        try { InitializeComponent(); }
        catch (Exception error)
        {
            WriteCrashLog(error, "XAML initialization");
            MessageBox(IntPtr.Zero, $"RedTrace XAML initialization failed.\n\n{error.Message}\n\n{LogPath}", "RedTrace startup error", 0x10);
            throw;
        }
        UnhandledException += (_, e) => WriteCrashLog(e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            try { HookInstaller.Install(silent: true); } catch (Exception hookError) { WriteCrashLog(hookError, "Hook setup"); }
            mainWindow = new MainWindow();
            mainWindow.Activate();
            try { CreateTray(); } catch (Exception trayError) { WriteCrashLog(trayError, "Tray setup"); }
            if (Environment.GetCommandLineArgs().Any(value => string.Equals(value, "--smoke-test", StringComparison.OrdinalIgnoreCase))) StartSmokeTest();
        }
        catch (Exception error)
        {
            WriteCrashLog(error, "Startup");
            MessageBox(IntPtr.Zero, $"RedTrace could not start.\n\n{error.Message}\n\nA log was saved to:\n{LogPath}", "RedTrace startup error", 0x10);
            Exit();
        }
    }

    private void StartSmokeTest()
    {
        Trace("Smoke test started");
        var step = 0;
        smokeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        smokeTimer.Tick += (_, _) =>
        {
            if (step++ == 0) mainWindow!.RunSmokeTest();
            else { Trace("Smoke test passed"); Quit(); }
        };
        smokeTimer.Start();
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
        smokeTimer?.Stop();
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

    internal static void Trace(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTimeOffset.Now:O}] {message}\n");
        }
        catch { }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr window, string text, string caption, uint type);
}
