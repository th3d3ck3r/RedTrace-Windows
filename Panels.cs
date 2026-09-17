using System.Text;
using System.Text.Json.Nodes;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace RedTrace.Windows;

public sealed class WatchPanel : Grid, IDisposable
{
    private readonly TextBox output = Ui.Terminal();
    private readonly ComboBox source = new() { Width = 190, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(12, 8, 12, 4) };
    public WatchPanel()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); RowDefinitions.Add(new RowDefinition());
        source.ItemsSource = new[] { "All RedTrace shells", "PowerShell", "CommandPrompt", "Wsl" }; source.SelectedIndex = 0;
        Children.Add(source); Grid.SetRow(output, 1); Children.Add(output);
        output.Text = "Activity from RedTrace-launched shells appears here.\n";
        ActivityHub.Line += Append;
    }
    private void Append(string origin, string line) => DispatcherQueue.TryEnqueue(() =>
    {
        if (source.SelectedIndex > 0 && !string.Equals(source.SelectedItem?.ToString(), origin, StringComparison.OrdinalIgnoreCase)) return;
        Ui.Append(output, line + "\n");
    });
    public void Dispose() => ActivityHub.Line -= Append;
}

public sealed class RunnerPanel : Grid, IDisposable
{
    private readonly TextBox output = Ui.Terminal();
    private readonly TextBox input = Ui.Terminal(false);
    private readonly Button shellButton;
    private readonly ShellSession session;
    private readonly List<string> history = [];
    private ShellKind shell = ShellKind.PowerShell;
    private int historyIndex;

    public RunnerPanel()
    {
        session = new ShellSession(shell); session.Output += Append;
        RowDefinitions.Add(new RowDefinition()); RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        output.Text = "Runner ready. Type a command below and press Enter.\n"; Children.Add(output);
        var bar = new Grid { ColumnSpacing = 7, Padding = new Thickness(9, 6, 9, 9) };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); bar.ColumnDefinitions.Add(new ColumnDefinition()); bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        shellButton = new Button { Content = "PowerShell  ▾", MinWidth = 120, Height = 30, Background = Ui.RaisedBrush, BorderBrush = Ui.HairlineBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Foreground = Ui.WhiteBrush, FontSize = 11 };
        shellButton.Flyout = ShellFlyout(); bar.Children.Add(shellButton);
        input.Height = 30; input.Padding = new Thickness(9, 4, 9, 4); input.Background = Ui.RaisedBrush; input.BorderBrush = Ui.HairlineBrush; input.BorderThickness = new Thickness(1); input.CornerRadius = new CornerRadius(7); input.PlaceholderText = "Run a command…"; input.KeyDown += InputKeyDown;
        Grid.SetColumn(input, 1); bar.Children.Add(input);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var common = Ui.IconButton("", "Common commands", () => { }); common.Flyout = CommandsFlyout(); actions.Children.Add(common);
        actions.Children.Add(Ui.IconButton("", "Previous command", () => History(-1)));
        actions.Children.Add(Ui.IconButton("", "Next command", () => History(1)));
        actions.Children.Add(Ui.IconButton("", "Clear output", () => output.Text = ""));
        actions.Children.Add(Ui.IconButton("", "Stop and restart", session.Start));
        actions.Children.Add(Ui.IconButton("", "Run", Run, Ui.RedBrush));
        Grid.SetColumn(actions, 2); bar.Children.Add(actions); Grid.SetRow(bar, 1); Children.Add(bar);
    }

    private MenuFlyout ShellFlyout()
    {
        var flyout = new MenuFlyout();
        foreach (var kind in Enum.GetValues<ShellKind>())
        {
            var item = new MenuFlyoutItem { Text = $"{(shell == kind ? "✓" : "  ")}  {ShellSession.DisplayName(kind)}", Tag = kind };
            item.Click += (_, _) => { shell = (ShellKind)item.Tag; shellButton.Content = ShellSession.DisplayName(shell) + "  ▾"; session.ChangeShell(shell); shellButton.Flyout = ShellFlyout(); };
            flyout.Items.Add(item);
        }
        return flyout;
    }

    private MenuFlyout CommandsFlyout()
    {
        var flyout = new MenuFlyout();
        foreach (var group in CommandCatalog.For(shell))
        {
            var parent = new MenuFlyoutSubItem { Text = group.Key };
            foreach (var entry in group.Value)
            {
                var item = new MenuFlyoutItem { Text = entry.Label, Tag = entry.Command };
                item.Click += (_, _) => { input.Text = (string)item.Tag; input.Focus(FocusState.Programmatic); input.Select(input.Text.Length, 0); };
                parent.Items.Add(item);
            }
            flyout.Items.Add(parent);
        }
        return flyout;
    }

    private void InputKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) { if (e.Key == global::Windows.System.VirtualKey.Enter) { Run(); e.Handled = true; } }
    private void Run() { var command = input.Text.Trim(); if (command.Length == 0) return; history.Remove(command); history.Add(command); historyIndex = history.Count; input.Text = ""; session.Send(command); }
    private void History(int delta) { if (history.Count == 0) return; historyIndex = Math.Clamp(historyIndex + delta, 0, history.Count); input.Text = historyIndex == history.Count ? "" : history[historyIndex]; input.Select(input.Text.Length, 0); }
    private void Append(string value) => DispatcherQueue.TryEnqueue(() => Ui.Append(output, value));
    public void Dispose() => session.Dispose();
}

public sealed class CodexPanel : Grid, IDisposable
{
    private readonly TextBox output = Ui.Terminal();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".redtrace", "codex-events.jsonl");
    private long offset;
    public CodexPanel()
    {
        Children.Add(output); output.Text = "Waiting for local Codex command events…\n";
        timer.Tick += (_, _) => Read(); timer.Start();
    }
    private void Read()
    {
        try
        {
            if (!File.Exists(path)) return;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length < offset) offset = 0; if (fs.Length == offset) return; fs.Position = offset;
            using var reader = new StreamReader(fs, Encoding.UTF8); var raw = reader.ReadToEnd(); offset = fs.Position;
            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var node = JsonNode.Parse(line)?.AsObject(); if (node is null) continue;
                var origin = node["origin"]?.ToString().ToUpperInvariant() ?? "LOCAL";
                if (node["phase"]?.ToString() == "start") Ui.Append(output, $"\n[{origin}] {node["cwd"]}\n❯ {node["command"]}\n");
                else { var body = node["output"]?.ToString(); if (!string.IsNullOrWhiteSpace(body)) Ui.Append(output, body + "\n"); Ui.Append(output, $"[{origin}] ✓ finished\n"); }
            }
        }
        catch { }
    }
    public void Dispose() => timer.Stop();
}

public sealed class BtopPanel : Grid, IDisposable
{
    private readonly SystemSampler sampler = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TextBlock processes = new() { FontFamily = new FontFamily("Cascadia Mono"), FontSize = 10, Foreground = Ui.TextBrush };
    private readonly Dictionary<string, TextBlock> values = [];
    private readonly Dictionary<string, Sparkline> charts = [];
    private readonly Dictionary<string, Border> tiles = [];
    private readonly List<string> order = ["CPU", "MEMORY", "GPU", "DISK", "NETWORK", "PROCESSES"];
    private readonly Grid metrics = new() { ColumnSpacing = 8, RowSpacing = 8, Padding = new Thickness(10, 5, 10, 5) };
    private readonly Button columnsButton;
    private int columns;

    public BtopPanel()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); RowDefinitions.Add(new RowDefinition());
        var toolbar = new Grid { Padding = new Thickness(10, 6, 10, 0) }; toolbar.ColumnDefinitions.Add(new ColumnDefinition()); toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.Children.Add(Ui.SmallLabel("SYSTEM MONITOR", Ui.Brush("#FF27DDE5")));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        columnsButton = new Button { Content = "AUTO  ▾", Height = 27, Padding = new Thickness(9, 2, 9, 2), Background = Ui.RaisedBrush, BorderBrush = Ui.HairlineBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Foreground = Ui.WhiteBrush, FontFamily = new FontFamily("Cascadia Mono"), FontSize = 9 };
        columnsButton.Flyout = ColumnsFlyout(); actions.Children.Add(columnsButton);
        actions.Children.Add(Ui.IconButton("", "Reset monitor layout", ResetLayout)); Grid.SetColumn(actions, 1); toolbar.Children.Add(actions); Children.Add(toolbar);
        var definitions = new[] { ("CPU", Ui.RedBrush), ("MEMORY", Ui.Brush("#FFFFC928")), ("GPU", Ui.Brush("#FFB97AFF")), ("DISK", Ui.Brush("#FFFF3B91")), ("NETWORK", Ui.Brush("#FF36DDE8")), ("PROCESSES", Ui.Brush("#FF6EDF45")) };
        foreach (var definition in definitions) { var tile = Metric(definition.Item1, definition.Item2); tiles[definition.Item1] = tile; metrics.Children.Add(tile); }
        Grid.SetRow(metrics, 1); Children.Add(metrics);
        var processBorder = new Border { Background = Ui.Brush("#5A050609"), BorderBrush = Ui.HairlineBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(10), Margin = new Thickness(10, 5, 10, 10), Child = processes };
        Grid.SetRow(processBorder, 2); Children.Add(processBorder);
        SizeChanged += (_, _) => { if (columns == 0) LayoutMetrics(); }; LayoutMetrics();
        timer.Tick += (_, _) => Refresh(); timer.Start(); Refresh();
    }

    private Border Metric(string title, SolidColorBrush accent)
    {
        var grid = new Grid(); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition());
        var top = new Grid(); top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(Ui.SmallLabel(title, accent));
        var value = new TextBlock { Text = "—", Foreground = Ui.WhiteBrush, FontFamily = new FontFamily("Cascadia Mono"), FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }; values[title] = value; Grid.SetColumn(value, 1); top.Children.Add(value); grid.Children.Add(top);
        var chart = new Sparkline(accent) { Margin = new Thickness(0, 5, 0, 0) }; charts[title] = chart; Grid.SetRow(chart, 1); grid.Children.Add(chart);
        var handle = new Border { Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Background = Ui.Brush("#30FFFFFF"), CornerRadius = new CornerRadius(5), Child = new TextBlock { Text = "◢", FontSize = 9, Foreground = accent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        grid.Children.Add(handle); Grid.SetRow(handle, 1);
        var tile = new Border { Height = 82, MinHeight = 64, Background = Ui.RaisedBrush, BorderBrush = new SolidColorBrush(Color.FromArgb(100, accent.Color.R, accent.Color.G, accent.Color.B)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 7, 6, 5), Child = grid, CanDrag = true, AllowDrop = true };
        tile.DragStarting += (_, e) => { e.Data.SetText(title); e.Data.RequestedOperation = DataPackageOperation.Move; };
        tile.DragOver += (_, e) => e.AcceptedOperation = DataPackageOperation.Move;
        tile.Drop += async (_, e) => { var source = await e.DataView.GetTextAsync(); Move(source, title); };
        var resizing = false; double startY = 0, startHeight = 0;
        handle.PointerPressed += (_, e) => { resizing = true; startY = e.GetCurrentPoint(this).Position.Y; startHeight = tile.ActualHeight; handle.CapturePointer(e.Pointer); e.Handled = true; };
        handle.PointerMoved += (_, e) => { if (!resizing) return; tile.Height = Math.Max(64, startHeight + e.GetCurrentPoint(this).Position.Y - startY); e.Handled = true; };
        handle.PointerReleased += (_, e) => { resizing = false; handle.ReleasePointerCapture(e.Pointer); e.Handled = true; };
        return tile;
    }

    private MenuFlyout ColumnsFlyout()
    {
        var flyout = new MenuFlyout();
        foreach (var value in new[] { 0, 1, 2, 3, 4 })
        {
            var item = new MenuFlyoutItem { Text = $"{(columns == value ? "✓" : "  ")}  {(value == 0 ? "AUTO" : value.ToString())}", Tag = value };
            item.Click += (_, _) => { columns = (int)item.Tag; columnsButton.Content = columns == 0 ? "AUTO  ▾" : $"{columns} COL  ▾"; columnsButton.Flyout = ColumnsFlyout(); LayoutMetrics(); };
            flyout.Items.Add(item);
        }
        return flyout;
    }

    private void LayoutMetrics()
    {
        var count = columns == 0 ? ActualWidth >= 760 ? 3 : ActualWidth >= 480 ? 2 : 1 : columns;
        count = Math.Clamp(count, 1, 4); metrics.ColumnDefinitions.Clear(); metrics.RowDefinitions.Clear();
        for (var i = 0; i < count; i++) metrics.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < (int)Math.Ceiling(order.Count / (double)count); i++) metrics.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < order.Count; i++) { var tile = tiles[order[i]]; Grid.SetColumn(tile, i % count); Grid.SetRow(tile, i / count); }
    }

    private void Move(string source, string target)
    {
        if (source == target || !order.Contains(source) || !order.Contains(target)) return;
        order.Remove(source); order.Insert(order.IndexOf(target), source); LayoutMetrics();
    }

    private void ResetLayout()
    {
        order.Clear(); order.AddRange(["CPU", "MEMORY", "GPU", "DISK", "NETWORK", "PROCESSES"]); columns = 0; columnsButton.Content = "AUTO  ▾"; columnsButton.Flyout = ColumnsFlyout();
        foreach (var tile in tiles.Values) tile.Height = 82; LayoutMetrics();
    }

    internal void RunSmokeTest() { columns = 2; Move("GPU", "CPU"); tiles["CPU"].Height = 96; LayoutMetrics(); ResetLayout(); }
    private void Refresh()
    {
        sampler.Sample();
        values["CPU"].Text = $"{sampler.Cpu:0}%"; values["MEMORY"].Text = $"{sampler.Memory:0}%"; values["GPU"].Text = sampler.Gpu; values["DISK"].Text = sampler.Disk; values["NETWORK"].Text = sampler.Network; values["PROCESSES"].Text = sampler.ProcessCount.ToString();
        charts["CPU"].Add(sampler.Cpu); charts["MEMORY"].Add(sampler.Memory); charts["GPU"].Add(Percent(sampler.Gpu)); charts["DISK"].Add(Percent(sampler.Disk)); charts["NETWORK"].Add(Math.Min(100, Math.Log10(1 + sampler.NetworkBytesPerSecond) / 7 * 100)); charts["PROCESSES"].Add(Math.Min(100, sampler.ProcessCount / 5.0));
        processes.Text = " PID    CPU   PROCESS\n" + string.Join("\n", sampler.Processes.Select(p => $"{p.Pid,6} {p.Cpu,6:0.0}%  {p.Name}"));
    }
    private static double Percent(string value) => double.TryParse(value.Trim().TrimEnd('%'), out var number) ? Math.Clamp(number, 0, 100) : 0;
    public void Dispose() => timer.Stop();
}
