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
    private readonly Dictionary<PanelMode, FrameworkElement> panels = [];
    private readonly List<PanelMode> order = [PanelMode.Watch, PanelMode.Run, PanelMode.Codex, PanelMode.Btop];
    private readonly HashSet<PanelMode> visible = [PanelMode.Watch, PanelMode.Run, PanelMode.Codex, PanelMode.Btop];
    private readonly SystemSampler toolbarSampler = new();
    private readonly DispatcherTimer toolbarTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly TextBlock stats = new();
    private readonly Button layoutButton;
    private bool cards = true;
    private int columns;
    private bool fit = true;
    private bool closeForReal;
    private bool topmost = true;
    private bool cardRenderPending;
    private int renderedAutoColumns = -1;
    private PanelMode selectedTab = PanelMode.Watch;

    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);

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

        if (dedicated is PanelMode only) panels[only] = CreatePanel(only);
        else foreach (var mode in Enum.GetValues<PanelMode>()) panels[mode] = CreatePanel(mode);
        Render();
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
        if (dedicated is null) actions.Children.Add(Ui.IconButton("", "Switch Tabs / Cards", ToggleLayout));
        actions.Children.Add(Ui.IconButton("", "New window", ShowNewWindowMenu));
        actions.Children.Add(Ui.IconButton("", "Layout", ShowLayoutMenu));
        actions.Children.Add(Ui.IconButton("", "Always on top", ToggleTopmost));
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
        cards = !cards; layoutButton.Content = cards ? "▦  CARDS  ▾" : "▣  TABS"; layoutButton.Flyout = cards ? CardsFlyout() : null; Render();
    }

    private void Render()
    {
        DetachAll();
        if (dedicated is PanelMode only) { contentHost.Child = panels[only]; return; }
        if (cards) RenderCards(); else RenderTabs();
    }

    private void RenderCards()
    {
        DetachAll();
        var active = order.Where(visible.Contains).ToArray();
        if (active.Length == 0) { contentHost.Child = new TextBlock { Text = "No cards selected\nUse CARDS to restore a panel.", Foreground = Ui.MutedBrush, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; return; }
        var width = Math.Max(400, contentHost.ActualWidth - 24);
        var count = columns == 0 ? width >= 1650 ? 4 : width >= 1180 ? 3 : width >= 700 ? 2 : 1 : columns;
        count = Math.Min(count, active.Length);
        if (columns == 0) renderedAutoColumns = count;
        var rows = (int)Math.Ceiling(active.Length / (double)count);
        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 10, Padding = new Thickness(10) };
        for (var i = 0; i < count; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < rows; i++) grid.RowDefinitions.Add(new RowDefinition { Height = fit ? new GridLength(1, GridUnitType.Star) : new GridLength(280) });
        for (var i = 0; i < active.Length; i++)
        {
            var card = Card(active[i], panels[active[i]]); Grid.SetColumn(card, i % count); Grid.SetRow(card, i / count); grid.Children.Add(card);
        }
        contentHost.Child = grid;
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
            if (cards && dedicated is null && columns == 0) RenderCards();
        });
    }

    private void RenderTabs()
    {
        DetachAll();
        var active = order.Where(visible.Contains).ToArray();
        if (active.Length == 0)
        {
            contentHost.Child = new TextBlock { Text = "No tabs selected", Foreground = Ui.MutedBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            return;
        }
        if (!visible.Contains(selectedTab)) selectedTab = active[0];

        var host = new Grid();
        host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        host.RowDefinitions.Add(new RowDefinition());
        var tabBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Padding = new Thickness(9, 7, 9, 6) };
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
            button.Click += (_, _) => { if (selectedTab != current) { selectedTab = current; RenderTabs(); } };
            tabBar.Children.Add(button);
        }
        host.Children.Add(tabBar);
        var panel = panels[selectedTab]; Grid.SetRow(panel, 1); host.Children.Add(panel);
        contentHost.Child = host;
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
            if (Enum.TryParse<PanelMode>(raw, out var source) && source != mode) { var from = order.IndexOf(source); var to = order.IndexOf(mode); order.RemoveAt(from); order.Insert(to, source); RenderCards(); }
        };
        return border;
    }

    private FlyoutBase CardsFlyout()
    {
        var flyout = new MenuFlyout();
        foreach (var mode in Enum.GetValues<PanelMode>())
        {
            var item = new ToggleMenuFlyoutItem { Text = mode.ToString().ToUpperInvariant(), IsChecked = visible.Contains(mode), Tag = mode };
            item.Click += (_, _) => { var value = (PanelMode)item.Tag; if (item.IsChecked) visible.Add(value); else visible.Remove(value); RenderCards(); layoutButton.Flyout = CardsFlyout(); };
            flyout.Items.Add(item);
        }
        flyout.Items.Add(new MenuFlyoutSeparator());
        var showAll = new MenuFlyoutItem { Text = "Show all cards" }; showAll.Click += (_, _) => { foreach (var mode in Enum.GetValues<PanelMode>()) visible.Add(mode); RenderCards(); layoutButton.Flyout = CardsFlyout(); }; flyout.Items.Add(showAll);
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
        var flyout = new MenuFlyout();
        var fitted = new ToggleMenuFlyoutItem { Text = "Fit cards to window", IsChecked = fit }; fitted.Click += (_, _) => { fit = fitted.IsChecked; if (cards) RenderCards(); }; flyout.Items.Add(fitted);
        foreach (var value in new[] { 0, 1, 2, 3, 4 }) { var item = new RadioMenuFlyoutItem { Text = value == 0 ? "Columns: AUTO" : $"Columns: {value}", GroupName = "columns", IsChecked = columns == value, Tag = value }; item.Click += (_, _) => { columns = (int)item.Tag; if (cards) RenderCards(); }; flyout.Items.Add(item); }
        flyout.ShowAt(root);
    }

    private void ToggleTopmost()
    {
        topmost = !topmost; if (appWindow.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = topmost;
    }

    private void DetachAll()
    {
        contentHost.Child = null;
        foreach (var panel in panels.Values) Detach(panel);
    }

    internal void RunSmokeTest()
    {
        if (dedicated is not null) return;
        if (cards) ToggleLayout();
        selectedTab = PanelMode.Run; RenderTabs();
        selectedTab = PanelMode.Btop; RenderTabs();
        ToggleLayout();
    }

    private static void Detach(FrameworkElement element)
    {
        if (element.Parent is Border border) border.Child = null;
        else if (element.Parent is ContentControl content) content.Content = null;
        else if (element.Parent is Panel parent) parent.Children.Remove(element);
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
}
