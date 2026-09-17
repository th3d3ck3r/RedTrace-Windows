using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace RedTrace.Windows;

public sealed class MainWindow : Window
{
    private readonly PanelMode? dedicated;
    private readonly AppWindow appWindow;
    private readonly IntPtr hwnd;
    internal IntPtr WindowHandle => hwnd;
    private readonly Grid root = new();
    private readonly Border contentHost = new();
    private readonly Grid dashboard = new();
    private readonly Grid cardGrid = new();
    private readonly StackPanel tabBar = new() { Orientation = Orientation.Horizontal, Spacing = 4, Padding = new Thickness(9, 7, 9, 6) };
    private readonly Dictionary<PanelMode, FrameworkElement> panels = [];
    private readonly Dictionary<PanelMode, Border> panelCards = [];
    private readonly List<PanelMode> order = [PanelMode.Watch, PanelMode.Run, PanelMode.Codex, PanelMode.Btop];
    private readonly HashSet<PanelMode> visible = [PanelMode.Watch, PanelMode.Run, PanelMode.Codex, PanelMode.Btop];
    private readonly SystemSampler toolbarSampler = new();
    private readonly DispatcherTimer toolbarTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly TextBlock stats = new();
    private readonly Button layoutButton;
    private Button topmostButton = null!;
    private bool cards = true;
    private int columns;
    private bool fit = true;
    private bool closeForReal;
    private bool topmost = true;
    private bool cardRenderPending;
    private int renderedAutoColumns = -1;
    private PanelMode selectedTab = PanelMode.Watch;

    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint colorKey, byte alpha, uint flags);
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)] private static extern bool ChooseColor(ref ChooseColorData data);

    public MainWindow(PanelMode? dedicated = null)
    {
        this.dedicated = dedicated;
        Title = dedicated is null ? "RedTrace" : $"RedTrace {dedicated}";
        hwnd = WindowNative.GetWindowHandle(this);
        appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
        appWindow.Resize(new SizeInt32(dedicated is null ? 1120 : dedicated == PanelMode.Btop ? 840 : 760, dedicated is null ? 720 : dedicated == PanelMode.Btop ? 580 : 480));
        var iconPath = Path.Combine(AppContext.BaseDirectory, "RedTrace.ico");
        if (File.Exists(iconPath)) appWindow.SetIcon(iconPath);
        appWindow.Closing += OnClosing;
        if (appWindow.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = true;

        try { SystemBackdrop = new DesktopAcrylicBackdrop(); } catch { /* Solid glass fallback below. */ }
        ExtendsContentIntoTitleBar = true;
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(43) }); root.RowDefinitions.Add(new RowDefinition());
        root.Background = Ui.Brush("#70030407"); Content = root;
        var titleBar = BuildTitleBar(out layoutButton); root.Children.Add(titleBar); SetTitleBar(titleBar);
        contentHost.Background = Ui.Brush("#74050609"); contentHost.BorderBrush = Ui.Brush("#46FF3B4D"); contentHost.BorderThickness = new Thickness(1); contentHost.CornerRadius = new CornerRadius(12); contentHost.Margin = new Thickness(10, 0, 10, 10);
        Grid.SetRow(contentHost, 1); root.Children.Add(contentHost);
        ApplyWindowOpacity(Preferences.Opacity);
        UpdateTopmostButton();

        if (dedicated is PanelMode only)
        {
            panels[only] = CreatePanel(only);
            contentHost.Child = panels[only];
        }
        else
        {
            foreach (var mode in Enum.GetValues<PanelMode>()) panels[mode] = CreatePanel(mode);
            BuildDashboard();
            ApplyLayout();
        }
        root.SizeChanged += (_, _) => ScheduleResponsiveCardRender();
        toolbarTimer.Tick += (_, _) => RefreshStats(); toolbarTimer.Start(); RefreshStats();
    }

    private Grid BuildTitleBar(out Button layout)
    {
        var bar = new Grid { Background = new SolidColorBrush(Colors.Transparent), Padding = new Thickness(10, 5, 142, 5) };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); bar.ColumnDefinitions.Add(new ColumnDefinition()); bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout = new Button { Content = dedicated is null ? "▦  CARDS  ▾" : $"{Ui.Icon(dedicated.Value)}  {dedicated.ToString()!.ToUpperInvariant()}", Height = 29, MinWidth = 96, Padding = new Thickness(10, 2, 10, 2), Background = Ui.Brush("#91201419"), BorderBrush = Ui.Brush("#805B232C"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Foreground = Ui.RedBrush, FontFamily = new FontFamily("Cascadia Mono"), FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        if (dedicated is null) layout.Flyout = CardsFlyout(); bar.Children.Add(layout);
        stats.Foreground = Ui.MutedBrush; stats.FontFamily = new FontFamily("Cascadia Mono"); stats.FontSize = 9; stats.HorizontalAlignment = HorizontalAlignment.Right; stats.VerticalAlignment = VerticalAlignment.Center; stats.Margin = new Thickness(10, 0, 10, 0); Grid.SetColumn(stats, 1); bar.Children.Add(stats);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (dedicated is null) actions.Children.Add(Ui.IconButton("", "Switch Cards / Tabs", ToggleLayout));
        actions.Children.Add(Ui.IconButton("", "New window", ShowNewWindowMenu));
        actions.Children.Add(Ui.IconButton("", "Card layout", ShowLayoutMenu));
        actions.Children.Add(Ui.IconButton("", "Appearance", ShowAppearanceMenu));
        topmostButton = Ui.IconButton("", "Always on top", ToggleTopmost); actions.Children.Add(topmostButton);
        Grid.SetColumn(actions, 2); bar.Children.Add(actions);
        return bar;
    }

    private void RefreshStats()
    {
        toolbarSampler.Sample();
        stats.Text = $"CPU {toolbarSampler.Cpu:0}%   GPU {toolbarSampler.Gpu}   RAM {toolbarSampler.Memory:0}%";
    }

    private void ToggleLayout()
    {
        cards = !cards; layoutButton.Content = cards ? "▦  CARDS  ▾" : "▣  TABS"; layoutButton.Flyout = cards ? CardsFlyout() : null; ApplyLayout();
    }

    private void BuildDashboard()
    {
        dashboard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dashboard.RowDefinitions.Add(new RowDefinition());
        dashboard.Children.Add(tabBar);
        cardGrid.ColumnSpacing = 10; cardGrid.RowSpacing = 10; cardGrid.Padding = new Thickness(10);
        Grid.SetRow(cardGrid, 1); dashboard.Children.Add(cardGrid);
        foreach (var mode in order)
        {
            var card = Card(mode, panels[mode]);
            panelCards[mode] = card;
            cardGrid.Children.Add(card);
        }
        contentHost.Child = dashboard;
    }

    private void ApplyLayout()
    {
        if (dedicated is not null) return;
        var active = order.Where(visible.Contains).ToArray();
        if (active.Length == 0) { foreach (var card in panelCards.Values) card.Visibility = Visibility.Collapsed; return; }
        if (!visible.Contains(selectedTab)) selectedTab = active[0];
        tabBar.Visibility = cards ? Visibility.Collapsed : Visibility.Visible;
        if (!cards) BuildTabBar(active);
        cardGrid.ColumnDefinitions.Clear(); cardGrid.RowDefinitions.Clear();
        var width = Math.Max(400, contentHost.ActualWidth - 24);
        var count = cards ? (columns == 0 ? width >= 1650 ? 4 : width >= 1180 ? 3 : width >= 700 ? 2 : 1 : columns) : 1;
        count = Math.Min(count, active.Length);
        if (cards && columns == 0) renderedAutoColumns = count;
        var rows = cards ? (int)Math.Ceiling(active.Length / (double)count) : 1;
        for (var i = 0; i < count; i++) cardGrid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < rows; i++) cardGrid.RowDefinitions.Add(new RowDefinition { Height = cards && !fit ? new GridLength(280) : new GridLength(1, GridUnitType.Star) });
        foreach (var pair in panelCards) pair.Value.Visibility = cards ? (visible.Contains(pair.Key) ? Visibility.Visible : Visibility.Collapsed) : (pair.Key == selectedTab ? Visibility.Visible : Visibility.Collapsed);
        for (var i = 0; i < active.Length; i++)
        {
            var card = panelCards[active[i]];
            Grid.SetColumn(card, cards ? i % count : 0); Grid.SetRow(card, cards ? i / count : 0);
        }
    }

    private void ScheduleResponsiveCardRender()
    {
        if (!cards || dedicated is not null || columns != 0 || cardRenderPending) return;
        var width = Math.Max(400, contentHost.ActualWidth - 24);
        var next = width >= 1650 ? 4 : width >= 1180 ? 3 : width >= 700 ? 2 : 1;
        next = Math.Min(next, visible.Count);
        if (next == renderedAutoColumns) return;
        cardRenderPending = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            cardRenderPending = false;
            if (cards && dedicated is null && columns == 0) ApplyLayout();
        });
    }

    private void BuildTabBar(PanelMode[] active)
    {
        tabBar.Children.Clear();
        foreach (var mode in active)
        {
            var current = mode;
            var activeTab = current == selectedTab;
            var button = new Button
            {
                Content = $"{Ui.Icon(current)}  {current.ToString().ToUpperInvariant()}",
                Height = 29,
                Padding = new Thickness(10, 2, 10, 2),
                Background = activeTab ? Ui.Brush("#A4251117") : new SolidColorBrush(Colors.Transparent),
                BorderBrush = activeTab ? Ui.Accent(current) : Ui.HairlineBrush,
                BorderThickness = new Thickness(activeTab ? 1 : 0),
                CornerRadius = new CornerRadius(7),
                Foreground = activeTab ? Ui.Accent(current) : Ui.MutedBrush,
                FontFamily = new FontFamily("Cascadia Mono"), FontSize = 10
            };
            button.Click += (_, _) => { if (selectedTab != current) { selectedTab = current; ApplyLayout(); } };
            tabBar.Children.Add(button);
        }
    }

    private Border Card(PanelMode mode, FrameworkElement panel)
    {
        var accent = Ui.Accent(mode);
        var header = new Grid { Height = 33, Padding = new Thickness(9, 0, 6, 0), Background = Ui.Brush("#92101116"), CanDrag = true };
        header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new FontIcon { Glyph = Ui.Icon(mode), FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 11, Foreground = accent }); title.Children.Add(Ui.SmallLabel(mode.ToString().ToUpperInvariant(), accent)); header.Children.Add(title);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        controls.Children.Add(Ui.IconButton("", $"Open dedicated {mode} window", () => new MainWindow(mode).Activate(), accent));
        controls.Children.Add(new FontIcon { Glyph = "", FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 11, Foreground = accent, Margin = new Thickness(7, 0, 5, 0) }); Grid.SetColumn(controls, 1); header.Children.Add(controls);
        header.DragStarting += (_, e) => { e.Data.SetText(mode.ToString()); e.Data.RequestedOperation = DataPackageOperation.Move; };
        var body = new Grid(); body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); body.RowDefinitions.Add(new RowDefinition()); body.Children.Add(header); Grid.SetRow(panel, 1); body.Children.Add(panel);
        var border = new Border { Background = Ui.PanelBrush, BorderBrush = new SolidColorBrush(Color.FromArgb(150, accent.Color.R, accent.Color.G, accent.Color.B)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), Child = body, AllowDrop = true, MinHeight = 190 };
        border.DragOver += (_, e) => { e.AcceptedOperation = DataPackageOperation.Move; };
        border.Drop += async (_, e) =>
        {
            var raw = await e.DataView.GetTextAsync();
            if (Enum.TryParse<PanelMode>(raw, out var source) && source != mode) { var from = order.IndexOf(source); var to = order.IndexOf(mode); order.RemoveAt(from); order.Insert(to, source); ApplyLayout(); }
        };
        return border;
    }

    private FlyoutBase CardsFlyout()
    {
        var flyout = new MenuFlyout();
        foreach (var mode in Enum.GetValues<PanelMode>())
        {
            var item = new MenuFlyoutItem { Text = $"{(visible.Contains(mode) ? "✓" : "  ")}  {mode.ToString().ToUpperInvariant()}", Tag = mode };
            item.Click += (_, _) => { var value = (PanelMode)item.Tag; if (!visible.Add(value)) visible.Remove(value); ApplyLayout(); layoutButton.Flyout = CardsFlyout(); };
            flyout.Items.Add(item);
        }
        flyout.Items.Add(new MenuFlyoutSeparator());
        var showAll = new MenuFlyoutItem { Text = "Show all cards" }; showAll.Click += (_, _) => { foreach (var mode in Enum.GetValues<PanelMode>()) visible.Add(mode); ApplyLayout(); layoutButton.Flyout = CardsFlyout(); }; flyout.Items.Add(showAll);
        return flyout;
    }

    private void ShowNewWindowMenu()
    {
        var flyout = new MenuFlyout();
        foreach (var mode in Enum.GetValues<PanelMode>()) { var item = new MenuFlyoutItem { Text = $"New {mode} window", Tag = mode }; item.Click += (_, _) => new MainWindow((PanelMode)item.Tag).Activate(); flyout.Items.Add(item); }
        flyout.ShowAt(root);
    }

    private void ShowLayoutMenu()
    {
        CreateLayoutFlyout().ShowAt(root);
    }

    private MenuFlyout CreateLayoutFlyout()
    {
        var flyout = new MenuFlyout();
        var fitted = new MenuFlyoutItem { Text = $"{(fit ? "✓" : "  ")}  Fit cards to window" }; fitted.Click += (_, _) => { fit = !fit; if (cards) ApplyLayout(); }; flyout.Items.Add(fitted);
        flyout.Items.Add(new MenuFlyoutSeparator());
        foreach (var value in new[] { 0, 1, 2, 3, 4 })
        {
            var label = value == 0 ? "AUTO" : value.ToString();
            var item = new MenuFlyoutItem { Text = $"{(columns == value ? "✓" : "  ")}  Columns: {label}", Tag = value };
            item.Click += (_, _) => { columns = (int)item.Tag; if (cards) ApplyLayout(); };
            flyout.Items.Add(item);
        }
        return flyout;
    }

    private void ToggleTopmost()
    {
        topmost = !topmost; if (appWindow.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = topmost; UpdateTopmostButton();
    }

    private void UpdateTopmostButton()
    {
        if (topmostButton is null) return;
        topmostButton.Foreground = topmost ? Ui.RedBrush : Ui.MutedBrush;
        topmostButton.Background = topmost ? Ui.Brush("#552A0B11") : new SolidColorBrush(Colors.Transparent);
        topmostButton.BorderBrush = topmost ? Ui.RedBrush : Ui.HairlineBrush;
        topmostButton.BorderThickness = new Thickness(topmost ? 1 : 0);
    }

    private void ShowAppearanceMenu()
    {
        var flyout = new MenuFlyout();
        var text = new MenuFlyoutItem { Text = "Text color…" }; text.Click += (_, _) => PickColor(Ui.TextBrush); flyout.Items.Add(text);
        var accent = new MenuFlyoutItem { Text = "Accent color…" }; accent.Click += (_, _) => PickColor(Ui.RedBrush); flyout.Items.Add(accent);
        flyout.Items.Add(new MenuFlyoutSeparator());
        foreach (var amount in new[] { 0.70, 0.85, 0.95, 1.0 })
        {
            var value = amount;
            var item = new MenuFlyoutItem { Text = $"{(Math.Abs(Preferences.Opacity - value) < .01 ? "✓" : "  ")}  Opacity: {value:P0}" };
            item.Click += (_, _) => { Preferences.Opacity = value; ApplyWindowOpacity(value); Preferences.Save(); };
            flyout.Items.Add(item);
        }
        flyout.ShowAt(root);
    }

    private void PickColor(SolidColorBrush target)
    {
        var memory = Marshal.AllocCoTaskMem(16 * sizeof(uint));
        try
        {
            for (var i = 0; i < 16; i++) Marshal.WriteInt32(memory, i * sizeof(uint), 0);
            var color = target.Color;
            var data = new ChooseColorData { Size = Marshal.SizeOf<ChooseColorData>(), Owner = hwnd, CustomColors = memory, Flags = 0x00000103, Result = (uint)(color.R | color.G << 8 | color.B << 16) };
            if (ChooseColor(ref data)) { target.Color = Color.FromArgb(255, (byte)data.Result, (byte)(data.Result >> 8), (byte)(data.Result >> 16)); Preferences.Save(); }
        }
        finally { Marshal.FreeCoTaskMem(memory); }
    }

    private void ApplyWindowOpacity(double value)
    {
        const int exStyle = -20; const long layered = 0x00080000; const uint alphaFlag = 0x2;
        var style = GetWindowLongPtr(hwnd, exStyle).ToInt64();
        SetWindowLongPtr(hwnd, exStyle, new IntPtr(style | layered));
        SetLayeredWindowAttributes(hwnd, 0, (byte)Math.Clamp((int)Math.Round(value * 255), 1, 255), alphaFlag);
    }

    internal void RunSmokeTest()
    {
        if (dedicated is not null) return;
        App.Trace("Switching Cards to Tabs"); if (cards) ToggleLayout();
        App.Trace("Selecting Runner tab"); selectedTab = PanelMode.Run; ApplyLayout();
        App.Trace("Selecting BTOP tab"); selectedTab = PanelMode.Btop; ApplyLayout();
        App.Trace("Switching Tabs to Cards"); ToggleLayout();
        App.Trace("Opening Layout menu"); var layoutFlyout = CreateLayoutFlyout(); layoutFlyout.ShowAt(root); layoutFlyout.Hide();
        App.Trace("Toggling always on top"); ToggleTopmost(); ToggleTopmost();
        App.Trace("Applying transparency"); ApplyWindowOpacity(0.85);
        App.Trace("Testing BTOP layout"); if (panels[PanelMode.Btop] is BtopPanel btop) btop.RunSmokeTest();
        App.Trace("Layout switching completed");
    }

    private static FrameworkElement CreatePanel(PanelMode mode) => mode switch { PanelMode.Watch => new WatchPanel(), PanelMode.Run => new RunnerPanel(), PanelMode.Codex => new CodexPanel(), _ => new BtopPanel() };

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!closeForReal && dedicated is null && !App.IsExiting) { args.Cancel = true; appWindow.Hide(); return; }
        toolbarTimer.Stop(); foreach (var panel in panels.Values.OfType<IDisposable>()) panel.Dispose();
    }

    public void ToggleVisibility() { if (IsWindowVisible(hwnd)) appWindow.Hide(); else ShowAndActivate(); }
    public void ShowAndActivate() { appWindow.Show(); Activate(); }
    public void CloseForReal() { closeForReal = true; Close(); }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ChooseColorData
    {
        public int Size; public IntPtr Owner, Instance; public uint Result; public IntPtr CustomColors; public uint Flags;
        public IntPtr CustomData, Hook, TemplateName;
    }
}
