using System.Text;
using System.Text.Json.Nodes;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RedTrace.Windows.Controls;
using Windows.ApplicationModel.DataTransfer;

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
        var flyout = Ui.Menu();
        flyout.Items.Add(new MenuFlyoutItem { Text = "SHELL", IsEnabled = false });
        flyout.Items.Add(new MenuFlyoutSeparator());
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
        var flyout = Ui.Menu();
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
    private readonly Dictionary<string, Sparkline> charts = [];
    private readonly Dictionary<string, MetricCard> tiles = [];
    private readonly List<string> order = ["CPU", "MEMORY", "GPU", "DISK", "NETWORK", "PROCESSES"];
    private readonly Grid metrics = new() { ColumnSpacing = 8, RowSpacing = 8, Padding = new Thickness(10, 5, 10, 5) };
    private readonly Grid metricsBody = new();
    private readonly ScrollViewer metricsScroll = new()
    {
        VerticalScrollMode = ScrollMode.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollMode = ScrollMode.Disabled,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        HorizontalContentAlignment = HorizontalAlignment.Stretch
    };
    private readonly Button columnsButton;
    private readonly Grid cpuThreadGrid = new() { Visibility = Visibility.Collapsed, ColumnSpacing = 6, RowSpacing = 2 };
    private readonly List<Sparkline> cpuThreadCharts = [];
    private readonly List<TextBlock> cpuThreadValues = [];
    private Button cpuModeButton = null!;
    private bool cpuThreads;
    private bool metricLayoutPending;
    private double cpuTotalHeight = 92;
    private int columns;
    private int renderedMetricColumns;

    private const double MinimumAutoMetricWidth = 220;
    private const int MaximumAutoMetricColumns = 3;

    public BtopPanel()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); RowDefinitions.Add(new RowDefinition());
        var toolbar = new Grid { Padding = new Thickness(10, 6, 10, 0) }; toolbar.ColumnDefinitions.Add(new ColumnDefinition()); toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.Children.Add(Ui.SmallLabel("SYSTEM MONITOR", Ui.Brush("#FF27DDE5")));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        columnsButton = new Button { Content = "AUTO  ▾", Height = 27, Padding = new Thickness(9, 2, 9, 2), Background = Ui.RaisedBrush, BorderBrush = Ui.HairlineBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Foreground = Ui.WhiteBrush, FontFamily = new FontFamily("Cascadia Mono"), FontSize = 9 };
        columnsButton.Flyout = ColumnsFlyout(); actions.Children.Add(columnsButton);
        actions.Children.Add(Ui.IconButton("", "Reset monitor layout", ResetLayout)); Grid.SetColumn(actions, 1); toolbar.Children.Add(actions); Children.Add(toolbar);
        var definitions = new[] { ("CPU", Ui.RedBrush), ("MEMORY", Ui.Brush("#FFFFC928")), ("GPU", Ui.Brush("#FFB97AFF")), ("DISK", Ui.Brush("#FFFF3B91")), ("NETWORK", Ui.Brush("#FF36DDE8")), ("PROCESSES", Ui.Brush("#FF6EDF45")) };
        foreach (var definition in definitions) { var tile = Metric(definition.Item1, definition.Item2); tiles[definition.Item1] = tile; metrics.Children.Add(tile); }
        metricsBody.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        metricsBody.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        metricsBody.Children.Add(metrics);
        var processBorder = new Border { Background = Ui.Brush("#5A050609"), BorderBrush = Ui.HairlineBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(10), Margin = new Thickness(10, 5, 10, 10), Child = processes };
        Grid.SetRow(processBorder, 1); metricsBody.Children.Add(processBorder);
        metricsScroll.Content = metricsBody; Grid.SetRow(metricsScroll, 1); Children.Add(metricsScroll);
        SizeChanged += (_, _) => ScheduleMetricLayout(); LayoutMetrics();
        timer.Tick += (_, _) => Refresh(); timer.Start(); Refresh();
    }

    private MetricCard Metric(string title, SolidColorBrush accent)
    {
        var tile = new MetricCard { Label = title, AccentBrush = accent, Height = 92, MinHeight = 74, CanDrag = true, AllowDrop = true };
        if (title == "CPU")
        {
            cpuModeButton = new Button { Content = "TOTAL", Height = 22, Padding = new Thickness(7, 1, 7, 1), Margin = new Thickness(5, 0, 7, 0), Background = Ui.Brush("#40271118"), BorderBrush = accent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Foreground = accent, FontFamily = new FontFamily("Cascadia Mono"), FontSize = 8 };
            cpuModeButton.Click += (_, _) => ToggleCpuMode();
            tile.HeaderContent = cpuModeButton;
        }
        var chartHost = new Grid();
        var chart = new Sparkline(accent); charts[title] = chart; chartHost.Children.Add(chart);
        if (title == "CPU") chartHost.Children.Add(cpuThreadGrid);
        tile.TrendContent = chartHost;
        var handle = tile.ResizeHandle;
        tile.DragStarting += (_, e) => { e.Data.SetText(title); e.Data.RequestedOperation = DataPackageOperation.Move; };
        tile.DragOver += (_, e) => e.AcceptedOperation = DataPackageOperation.Move;
        tile.Drop += async (_, e) => { var source = await e.DataView.GetTextAsync(); Move(source, title); };
        var resizing = false; double startY = 0, startHeight = 0;
        handle.PointerPressed += (_, e) => { resizing = true; startY = e.GetCurrentPoint(this).Position.Y; startHeight = tile.ActualHeight; handle.CapturePointer(e.Pointer); e.Handled = true; };
        handle.PointerMoved += (_, e) => { if (!resizing) return; tile.Height = Math.Max(74, startHeight + e.GetCurrentPoint(this).Position.Y - startY); e.Handled = true; };
        handle.PointerReleased += (_, e) => { resizing = false; handle.ReleasePointerCapture(e.Pointer); e.Handled = true; };
        return tile;
    }

    private void ToggleCpuMode()
    {
        cpuThreads = !cpuThreads; cpuModeButton.Content = cpuThreads ? "THREADS" : "TOTAL";
        charts["CPU"].Visibility = cpuThreads ? Visibility.Collapsed : Visibility.Visible;
        cpuThreadGrid.Visibility = cpuThreads ? Visibility.Visible : Visibility.Collapsed;
        if (cpuThreads)
        {
            cpuTotalHeight = tiles["CPU"].ActualHeight > 0 ? tiles["CPU"].ActualHeight : tiles["CPU"].Height;
            BuildCpuThreadCharts(sampler.CpuThreads.Count);
            var count = Math.Max(1, sampler.CpuThreads.Count); var cols = count > 24 ? 4 : count > 12 ? 3 : count > 6 ? 2 : 1;
            tiles["CPU"].Height = Math.Max(96, Math.Ceiling(count / (double)cols) * 18 + 47);
        }
        else tiles["CPU"].Height = Math.Max(74, cpuTotalHeight);
    }

    private void BuildCpuThreadCharts(int count)
    {
        if (count == cpuThreadCharts.Count) return;
        cpuThreadGrid.Children.Clear(); cpuThreadGrid.ColumnDefinitions.Clear(); cpuThreadGrid.RowDefinitions.Clear(); cpuThreadCharts.Clear(); cpuThreadValues.Clear();
        count = Math.Max(1, count); var columnCount = count > 24 ? 4 : count > 12 ? 3 : count > 6 ? 2 : 1;
        for (var i = 0; i < columnCount; i++) cpuThreadGrid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < (int)Math.Ceiling(count / (double)columnCount); i++) cpuThreadGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        var colors = new[] { "#FFFF5362", "#FFFFC928", "#FFB97AFF", "#FF36DDE8", "#FF6EDF45", "#FFFF3B91" };
        for (var i = 0; i < count; i++)
        {
            var row = new Grid { ColumnSpacing = 4 }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(27) });
            var accent = Ui.Brush(colors[i % colors.Length]); row.Children.Add(new TextBlock { Text = $"T{i}", FontFamily = new FontFamily("Cascadia Mono"), FontSize = 7, Foreground = accent, VerticalAlignment = VerticalAlignment.Center });
            var spark = new Sparkline(accent) { Height = 12 }; cpuThreadCharts.Add(spark); Grid.SetColumn(spark, 1); row.Children.Add(spark);
            var value = new TextBlock { Text = "0%", FontFamily = new FontFamily("Cascadia Mono"), FontSize = 7, Foreground = Ui.MutedBrush, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; cpuThreadValues.Add(value); Grid.SetColumn(value, 2); row.Children.Add(value);
            Grid.SetColumn(row, i % columnCount); Grid.SetRow(row, i / columnCount); cpuThreadGrid.Children.Add(row);
        }
    }

    private MenuFlyout ColumnsFlyout()
    {
        var flyout = Ui.Menu();
        flyout.Items.Add(new MenuFlyoutItem { Text = "METRIC COLUMNS", IsEnabled = false });
        flyout.Items.Add(new MenuFlyoutSeparator());
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
        var count = columns == 0 ? CalculateAutoColumns(AvailableMetricWidth()) : columns;
        count = Math.Clamp(count, 1, 4); metrics.ColumnDefinitions.Clear(); metrics.RowDefinitions.Clear();
        for (var i = 0; i < count; i++) metrics.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < (int)Math.Ceiling(order.Count / (double)count); i++) metrics.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < order.Count; i++) { var tile = tiles[order[i]]; Grid.SetColumn(tile, i % count); Grid.SetRow(tile, i / count); }
        renderedMetricColumns = count;
    }

    private void ScheduleMetricLayout()
    {
        if (columns != 0 || metricLayoutPending) return;
        var requestedColumns = CalculateAutoColumns(AvailableMetricWidth());
        if (requestedColumns == renderedMetricColumns) return;
        metricLayoutPending = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            metricLayoutPending = false;
            if (columns == 0 && CalculateAutoColumns(AvailableMetricWidth()) != renderedMetricColumns) LayoutMetrics();
        });
    }

    private double AvailableMetricWidth()
    {
        var width = metricsScroll.ActualWidth > 0 ? metricsScroll.ActualWidth : ActualWidth;
        return Math.Max(0, width - metrics.Padding.Left - metrics.Padding.Right);
    }

    private static int CalculateAutoColumns(double availableWidth)
    {
        var count = (int)Math.Floor((availableWidth + 8) / (MinimumAutoMetricWidth + 8));
        return Math.Clamp(count, 1, MaximumAutoMetricColumns);
    }

    private void Move(string source, string target)
    {
        if (source == target || !order.Contains(source) || !order.Contains(target)) return;
        order.Remove(source); order.Insert(order.IndexOf(target), source); LayoutMetrics();
    }

    private void ResetLayout()
    {
        order.Clear(); order.AddRange(["CPU", "MEMORY", "GPU", "DISK", "NETWORK", "PROCESSES"]); columns = 0; columnsButton.Content = "AUTO  ▾"; columnsButton.Flyout = ColumnsFlyout();
        foreach (var tile in tiles.Values) tile.Height = 92; LayoutMetrics();
    }

    internal void RunSmokeTest()
    {
        App.Trace("Validating metric cards");
        if (tiles.Count != order.Count || tiles.Any(pair => pair.Value.Label != pair.Key)) throw new InvalidOperationException("Metric cards were not initialized correctly.");
        if (tiles.Values.Any(tile => tile.Progress is < 0 or > 100)) throw new InvalidOperationException("Metric progress is outside its valid range.");
        App.Trace("Testing responsive metric breakpoints");
        if (CalculateAutoColumns(300) != 1 || CalculateAutoColumns(520) != 2 || CalculateAutoColumns(900) != 3) throw new InvalidOperationException("Metric auto-column breakpoints are invalid.");
        if (metricsScroll.VerticalScrollBarVisibility != ScrollBarVisibility.Auto || metricsScroll.HorizontalScrollMode != ScrollMode.Disabled) throw new InvalidOperationException("Metric scrolling is not configured correctly.");
        App.Trace("Testing metric layout"); columns = 2; Move("GPU", "CPU"); tiles["CPU"].Height = 96; LayoutMetrics();
        columns = 4; LayoutMetrics(); if (metrics.ColumnDefinitions.Count != 4) throw new InvalidOperationException("Manual metric columns were not preserved.");
        App.Trace("Testing CPU thread mode"); ToggleCpuMode(); ToggleCpuMode();
        App.Trace("Resetting metric layout"); ResetLayout();
    }
    private void Refresh()
    {
        sampler.Sample();
        var gpu = Percent(sampler.Gpu); var disk = Percent(sampler.Disk); var network = Math.Min(100, Math.Log10(1 + sampler.NetworkBytesPerSecond) / 7 * 100); var processLoad = Math.Min(100, sampler.ProcessCount / 5.0);
        tiles["CPU"].DisplayValue = $"{sampler.Cpu:0}%"; tiles["MEMORY"].DisplayValue = $"{sampler.Memory:0}%"; tiles["GPU"].DisplayValue = sampler.Gpu; tiles["DISK"].DisplayValue = sampler.Disk; tiles["NETWORK"].DisplayValue = sampler.Network; tiles["PROCESSES"].DisplayValue = sampler.ProcessCount.ToString();
        tiles["CPU"].Progress = sampler.Cpu; tiles["MEMORY"].Progress = sampler.Memory; tiles["GPU"].Progress = gpu; tiles["DISK"].Progress = disk; tiles["NETWORK"].Progress = network; tiles["PROCESSES"].Progress = processLoad;
        charts["CPU"].Add(sampler.Cpu); charts["MEMORY"].Add(sampler.Memory); charts["GPU"].Add(gpu); charts["DISK"].Add(disk); charts["NETWORK"].Add(network); charts["PROCESSES"].Add(processLoad);
        if (cpuThreads)
        {
            BuildCpuThreadCharts(sampler.CpuThreads.Count);
            for (var i = 0; i < Math.Min(cpuThreadCharts.Count, sampler.CpuThreads.Count); i++) { cpuThreadCharts[i].Add(sampler.CpuThreads[i]); cpuThreadValues[i].Text = $"{sampler.CpuThreads[i]:0}%"; }
        }
        processes.Text = " PID    CPU   PROCESS\n" + string.Join("\n", sampler.Processes.Select(p => $"{p.Pid,6} {p.Cpu,6:0.0}%  {p.Name}"));
    }
    private static double Percent(string value) => double.TryParse(value.Trim().TrimEnd('%'), out var number) ? Math.Clamp(number, 0, 100) : 0;
    public void Dispose() => timer.Stop();
}
