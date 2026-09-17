using Microsoft.UI.Xaml;
using Forms = System.Windows.Forms;

namespace RedTrace.Windows;

public partial class App : Application
{
    private MainWindow? mainWindow;
    private Forms.NotifyIcon? tray;
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
        tray = new Forms.NotifyIcon { Text = "RedTrace", Visible = true };
        var iconPath = Path.Combine(AppContext.BaseDirectory, "RedTrace.ico");
        tray.Icon = File.Exists(iconPath) ? new System.Drawing.Icon(iconPath) : System.Drawing.SystemIcons.Application;
        tray.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                mainWindow?.DispatcherQueue.TryEnqueue(() => mainWindow.ToggleVisibility());
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show RedTrace", null, (_, _) => mainWindow?.DispatcherQueue.TryEnqueue(() => mainWindow.ShowAndActivate()));
        menu.Items.Add("New Runner", null, (_, _) => mainWindow?.DispatcherQueue.TryEnqueue(() => new MainWindow(PanelMode.Run).Activate()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => mainWindow?.DispatcherQueue.TryEnqueue(Quit));
        tray.ContextMenuStrip = menu;
    }

    private void Quit()
    {
        IsExiting = true;
        if (tray is not null) { tray.Visible = false; tray.Dispose(); }
        mainWindow?.CloseForReal();
        Exit();
    }
}
