using Microsoft.UI.Xaml;

namespace RedTrace.Windows;

public partial class App : Application
{
    private MainWindow? mainWindow;
    private NativeTrayIcon? tray;
    internal static bool IsExiting { get; private set; }

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        HookInstaller.Install(silent: true);
        mainWindow = new MainWindow();
        mainWindow.Activate();
        CreateTray();
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
}
