using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace RedTrace.Windows;

public enum RedMode { Watch, Run, Codex, Btop }
public enum ShellKind { PowerShell, CommandPrompt, Wsl }

public sealed class App : Application
{
    private Forms.NotifyIcon? tray;
    private MainWindow? main;
    private bool exiting;

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--install-hooks")) { HookInstaller.Install(); return; }
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Theme.Load();
        Theme.ApplyResources();
        HookInstaller.Install(silent: true);
        main = new MainWindow();
        main.Closing += (_, ev) => { if (!exiting) { ev.Cancel = true; main.Hide(); } };
        main.Show();

        tray = new Forms.NotifyIcon { Text = "RedTrace", Visible = true };
        var iconPath = Path.Combine(AppContext.BaseDirectory, "RedTrace.ico");
        tray.Icon = File.Exists(iconPath) ? new System.Drawing.Icon(iconPath) : System.Drawing.SystemIcons.Application;
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ToggleMain(); };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show RedTrace", null, (_, _) => ShowMain());
        menu.Items.Add("New Runner", null, (_, _) => Current.Dispatcher.Invoke(() => new MainWindow(RedMode.Run).Show()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Current.Dispatcher.Invoke(Quit));
        tray.ContextMenuStrip = menu;
    }

    private void ToggleMain() { Dispatcher.Invoke(() => { if (main?.IsVisible == true) main.Hide(); else ShowMain(); }); }
    private void ShowMain() { main ??= new MainWindow(); main.Show(); main.Activate(); }
    private void Quit() { exiting = true; tray!.Visible = false; main?.CloseForReal(); Shutdown(); }
}

public static class Theme
{
    private sealed record SavedTheme(double Opacity, double FontSize, string FontName, string Background, string Text);
    private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RedTrace", "settings.json");
    public static readonly Color Red = Color.FromRgb(255, 59, 77);
    public static readonly Brush RedBrush = new SolidColorBrush(Red);
    public static Brush Background = new SolidColorBrush(Color.FromRgb(5, 5, 7));
    public static readonly Brush Panel = new SolidColorBrush(Color.FromRgb(10, 11, 15));
    public static readonly Brush Raised = new SolidColorBrush(Color.FromRgb(17, 18, 24));
    public static readonly Brush Hairline = new SolidColorBrush(Color.FromRgb(46, 48, 58));
    public static Brush Text = new SolidColorBrush(Color.FromRgb(255, 77, 90));
    public static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(150, 150, 158));
    public static string FontName = "Cascadia Mono";
    public static double FontSize = 12;
    public static double Opacity = .85;

    public static void ApplyResources()
    {
        var dictionary = (ResourceDictionary)XamlReader.Parse("""
            <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <SolidColorBrush x:Key="ControlSurface" Color="#111218"/>
              <SolidColorBrush x:Key="ControlHover" Color="#252732"/>
              <SolidColorBrush x:Key="ControlStroke" Color="#353744"/>
              <SolidColorBrush x:Key="RedTraceRed" Color="#FF3B4D"/>
              <Style TargetType="{x:Type Button}">
                <Setter Property="Foreground" Value="#E8E8ED"/>
                <Setter Property="Background" Value="Transparent"/>
                <Setter Property="BorderBrush" Value="#353744"/>
                <Setter Property="BorderThickness" Value="1"/>
                <Setter Property="Padding" Value="8,4"/>
                <Setter Property="Cursor" Value="Hand"/>
                <Setter Property="Template">
                  <Setter.Value>
                    <ControlTemplate TargetType="{x:Type Button}">
                      <Border x:Name="Chrome" Background="{TemplateBinding Background}"
                              BorderBrush="{TemplateBinding BorderBrush}"
                              BorderThickness="{TemplateBinding BorderThickness}"
                              CornerRadius="6" SnapsToDevicePixels="True">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"
                                          Margin="{TemplateBinding Padding}"/>
                      </Border>
                      <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                          <Setter TargetName="Chrome" Property="Background" Value="#252732"/>
                          <Setter TargetName="Chrome" Property="BorderBrush" Value="#80585D6C"/>
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                          <Setter TargetName="Chrome" Property="Background" Value="#30323E"/>
                        </Trigger>
                      </ControlTemplate.Triggers>
                    </ControlTemplate>
                  </Setter.Value>
                </Setter>
              </Style>
              <Style TargetType="{x:Type ComboBox}">
                <Setter Property="Foreground" Value="#E8E8ED"/>
                <Setter Property="Background" Value="#111218"/>
                <Setter Property="BorderBrush" Value="#353744"/>
                <Setter Property="Padding" Value="7,3"/>
                <Setter Property="FontSize" Value="11"/>
              </Style>
              <Style TargetType="{x:Type ContextMenu}">
                <Setter Property="Background" Value="#15161C"/>
                <Setter Property="Foreground" Value="#E8E8ED"/>
                <Setter Property="BorderBrush" Value="#3A3C48"/>
              </Style>
              <Style TargetType="{x:Type MenuItem}">
                <Setter Property="Background" Value="#15161C"/>
                <Setter Property="Foreground" Value="#E8E8ED"/>
                <Setter Property="Padding" Value="8,5"/>
              </Style>
              <Style TargetType="{x:Type TabControl}">
                <Setter Property="Background" Value="Transparent"/>
                <Setter Property="BorderThickness" Value="0"/>
              </Style>
              <Style TargetType="{x:Type TabItem}">
                <Setter Property="Foreground" Value="#94949E"/>
                <Setter Property="Background" Value="Transparent"/>
                <Setter Property="BorderBrush" Value="Transparent"/>
                <Setter Property="Padding" Value="13,5"/>
                <Setter Property="FontSize" Value="10"/>
                <Setter Property="FontWeight" Value="SemiBold"/>
                <Style.Triggers>
                  <Trigger Property="IsSelected" Value="True">
                    <Setter Property="Foreground" Value="#FF4D5A"/>
                    <Setter Property="Background" Value="#201419"/>
                    <Setter Property="BorderBrush" Value="#663B45"/>
                  </Trigger>
                </Style.Triggers>
              </Style>
              <Style TargetType="{x:Type ToolTip}">
                <Setter Property="Background" Value="#202129"/>
                <Setter Property="Foreground" Value="#F2F2F4"/>
                <Setter Property="BorderBrush" Value="#3A3C48"/>
              </Style>
              <Style TargetType="{x:Type ScrollBar}">
                <Setter Property="Opacity" Value="0.32"/>
                <Setter Property="Background" Value="Transparent"/>
                <Style.Triggers>
                  <Trigger Property="Orientation" Value="Vertical"><Setter Property="Width" Value="7"/></Trigger>
                  <Trigger Property="Orientation" Value="Horizontal"><Setter Property="Height" Value="7"/></Trigger>
                  <Trigger Property="IsMouseOver" Value="True"><Setter Property="Opacity" Value="0.7"/></Trigger>
                </Style.Triggers>
              </Style>
            </ResourceDictionary>
            """);
        Application.Current.Resources.MergedDictionaries.Add(dictionary);
        Application.Current.Resources[SystemColors.WindowBrushKey] = Raised;
        Application.Current.Resources[SystemColors.WindowTextBrushKey] = Brushes.White;
        Application.Current.Resources[SystemColors.HighlightBrushKey] = new SolidColorBrush(Color.FromRgb(70, 26, 34));
        Application.Current.Resources[SystemColors.HighlightTextBrushKey] = Brushes.White;
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var saved = JsonSerializer.Deserialize<SavedTheme>(File.ReadAllText(SettingsPath));
            if (saved is null) return;
            Opacity = Math.Clamp(saved.Opacity, .25, 1);
            FontSize = Math.Clamp(saved.FontSize, 9, 24);
            FontName = saved.FontName;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(saved.Background));
            Text = new SolidColorBrush((Color)ColorConverter.ConvertFromString(saved.Text));
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var saved = new SavedTheme(Opacity, FontSize, FontName, Background.ToString(), Text.ToString());
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(saved, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

public static class ActivityHub
{
    public static event Action<string, string>? Line;
    public static void Publish(string source, string value) => Line?.Invoke(source, $"[{DateTime.Now:HH:mm:ss}] [{source}] {value}");
}

public sealed class ShellSession : IDisposable
{
    private Process? process;
    private CancellationTokenSource? cancellation;
    public ShellKind Kind { get; private set; }
    public event Action<string>? Output;

    public ShellSession(ShellKind kind) { Kind = kind; Start(); }

    public void Start()
    {
        Stop();
        var (file, args) = Kind switch
        {
            ShellKind.CommandPrompt => ("cmd.exe", "/Q /K"),
            ShellKind.Wsl => ("wsl.exe", ""),
            _ => ("powershell.exe", "-NoLogo -NoProfile -NoExit")
        };
        process = new Process
        {
            StartInfo = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };
        try
        {
            process.Start();
            cancellation = new CancellationTokenSource();
            _ = Pump(process.StandardOutput, cancellation.Token);
            _ = Pump(process.StandardError, cancellation.Token);
            Emit($"— {Kind} session started —\n");
        }
        catch (Exception ex) { Emit($"Could not start {Kind}: {ex.Message}\n"); }
    }

    private async Task Pump(StreamReader reader, CancellationToken token)
    {
        var buffer = new char[2048];
        try
        {
            while (!token.IsCancellationRequested)
            {
                var count = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                if (count == 0) break;
                Emit(new string(buffer, 0, count));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Emit($"\n{ex.Message}\n"); }
    }

    private void Emit(string value)
    {
        Output?.Invoke(value);
        foreach (var line in value.Split('\n', StringSplitOptions.RemoveEmptyEntries)) ActivityHub.Publish(Kind.ToString(), line);
    }

    public void Send(string command)
    {
        if (process?.HasExited != false) Start();
        try { process?.StandardInput.WriteLine(command); process?.StandardInput.Flush(); ActivityHub.Publish(Kind.ToString(), $"> {command}"); }
        catch (Exception ex) { Emit($"\nCould not send command: {ex.Message}\n"); }
    }

    public void ChangeShell(ShellKind kind) { Kind = kind; Start(); }
    public void Stop()
    {
        cancellation?.Cancel();
        if (process is { HasExited: false }) { try { process.Kill(true); } catch { } }
        process?.Dispose(); process = null; cancellation?.Dispose(); cancellation = null;
    }
    public void Dispose() => Stop();
}

public sealed class RunnerPanel : Grid, IDisposable
{
    private readonly TextBox output = Ui.OutputBox();
    private readonly TextBox input = Ui.InputBox();
    private readonly Button shellButton;
    private ShellKind selectedShell = ShellKind.PowerShell;
    private readonly List<string> history = [];
    private int historyIndex;
    private readonly ShellSession session;

    public RunnerPanel()
    {
        shellButton = Ui.SelectorButton("PowerShell", "Choose shell", ChooseShell);
        shellButton.Width = 118; shellButton.Margin = new Thickness(0, 0, 7, 0);
        RowDefinitions.Add(new RowDefinition());
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Children.Add(output);
        output.Text = "Runner ready. Type a command below and press Enter.\n";
        var bar = new Grid { Margin = new Thickness(8, 5, 8, 8) };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition());
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(bar, 1);
        input.MinWidth = 120; input.HorizontalAlignment = HorizontalAlignment.Stretch; input.ToolTip = "Run a command…";
        Grid.SetColumn(shellButton, 0); bar.Children.Add(shellButton);
        Grid.SetColumn(input, 1); bar.Children.Add(input);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0) };
        actions.Children.Add(Ui.IconButton("\uE712", "Common commands", CommonCommands));
        actions.Children.Add(Ui.IconButton("\uE74A", "Previous command", () => History(-1)));
        actions.Children.Add(Ui.IconButton("\uE74B", "Next command", () => History(1)));
        actions.Children.Add(Ui.IconButton("\uE74D", "Clear output", output.Clear));
        actions.Children.Add(Ui.IconButton("\uE71A", "Stop and restart", () => session.Start()));
        actions.Children.Add(Ui.IconButton("\uE72A", "Run", Run, Theme.RedBrush));
        Grid.SetColumn(actions, 2); bar.Children.Add(actions);
        Children.Add(bar);
        session = new ShellSession(ShellKind.PowerShell);
        session.Output += Append;
        input.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Run(); e.Handled = true; } };
    }

    private void ChooseShell()
    {
        var menu = new ContextMenu();
        foreach (var kind in Enum.GetValues<ShellKind>())
        {
            var item = new MenuItem { Header = Ui.ShellName(kind), IsCheckable = true, IsChecked = selectedShell == kind, Tag = kind };
            item.Click += (_, _) => { selectedShell = (ShellKind)item.Tag; shellButton.Content = Ui.ShellName(selectedShell) + "  ▾"; session.ChangeShell(selectedShell); };
            menu.Items.Add(item);
        }
        menu.PlacementTarget = shellButton; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true;
    }

    private void Run()
    {
        var command = input.Text.Trim(); if (command.Length == 0) return;
        history.Remove(command); history.Add(command); historyIndex = history.Count; input.Clear(); session.Send(command);
    }
    private void History(int delta) { if (history.Count == 0) return; historyIndex = Math.Clamp(historyIndex + delta, 0, history.Count); input.Text = historyIndex == history.Count ? "" : history[historyIndex]; input.CaretIndex = input.Text.Length; }
    private void Append(string value) => Dispatcher.Invoke(() => { output.AppendText(value); output.ScrollToEnd(); });

    private void CommonCommands()
    {
        var menu = new ContextMenu();
        foreach (var group in CommandCatalog.For(selectedShell))
        {
            var parent = new MenuItem { Header = group.Key };
            foreach (var item in group.Value)
            {
                var child = new MenuItem { Header = item.Label, Tag = item.Command };
                child.Click += (_, _) => { input.Text = (string)child.Tag; input.CaretIndex = input.Text.Length; input.Focus(); };
                parent.Items.Add(child);
            }
            menu.Items.Add(parent);
        }
        menu.IsOpen = true;
    }
    public void Dispose() => session.Dispose();
}

public static class CommandCatalog
{
    public sealed record Entry(string Label, string Command);
    public static Dictionary<string, Entry[]> For(ShellKind shell) => shell switch
    {
        ShellKind.CommandPrompt => new()
        {
            ["Navigation"] = [new("Current folder", "cd"), new("List files", "dir"), new("Go up", "cd .."), new("Open Explorer", "explorer .")],
            ["System"] = [new("IP configuration", "ipconfig /all"), new("Processes", "tasklist"), new("Disk space", "wmic logicaldisk get caption,freespace,size")],
            ["Development"] = [new("Git status", "git status"), new("Python version", "python --version"), new("Node version", "node --version")]
        },
        ShellKind.Wsl => new()
        {
            ["Navigation"] = [new("Current folder", "pwd"), new("List files", "ls -la"), new("Go up", "cd ..")],
            ["System"] = [new("Disk space", "df -h"), new("Memory", "free -h"), new("Top processes", "ps aux --sort=-%cpu | head -15")],
            ["Development"] = [new("Git status", "git status"), new("Recent commits", "git log --oneline -10"), new("Kernel", "uname -a")]
        },
        _ => new()
        {
            ["Navigation"] = [new("Current folder", "Get-Location"), new("List files", "Get-ChildItem -Force"), new("Go up", "Set-Location .."), new("Open Explorer", "explorer .")],
            ["System"] = [new("Processes", "Get-Process | Sort-Object CPU -Descending | Select-Object -First 15"), new("Disk space", "Get-Volume"), new("Network", "Get-NetIPConfiguration")],
            ["Development"] = [new("Git status", "git status"), new("Recent commits", "git log --oneline -10"), new("PowerShell version", "$PSVersionTable")]
        }
    };
}

public sealed class WatchPanel : Grid, IDisposable
{
    private readonly TextBox output = Ui.OutputBox();
    private readonly Button sourceButton;
    private string selectedSource = "All RedTrace shells";
    public WatchPanel()
    {
        sourceButton = Ui.SelectorButton(selectedSource, "Choose activity source", ChooseSource);
        sourceButton.Width = 176; sourceButton.Margin = new Thickness(10, 8, 10, 3); sourceButton.HorizontalAlignment = HorizontalAlignment.Left;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); RowDefinitions.Add(new RowDefinition());
        Children.Add(sourceButton); Grid.SetRow(output, 1); Children.Add(output);
        output.Text = "Activity from RedTrace-launched shells appears here.\n";
        ActivityHub.Line += Append;
    }
    private void ChooseSource()
    {
        var menu = new ContextMenu();
        foreach (var source in new[] { "All RedTrace shells", "PowerShell", "CommandPrompt", "Wsl" })
        {
            var item = new MenuItem { Header = source, IsCheckable = true, IsChecked = selectedSource == source, Tag = source };
            item.Click += (_, _) => { selectedSource = (string)item.Tag; sourceButton.Content = selectedSource + "  ▾"; };
            menu.Items.Add(item);
        }
        menu.PlacementTarget = sourceButton; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true;
    }
    private void Append(string origin, string line) => Dispatcher.Invoke(() =>
    {
        if (selectedSource != "All RedTrace shells" && !string.Equals(selectedSource, origin, StringComparison.OrdinalIgnoreCase)) return;
        output.AppendText(line + "\n"); output.ScrollToEnd();
    });
    public void Dispose() => ActivityHub.Line -= Append;
}

public sealed class CodexPanel : Grid, IDisposable
{
    private readonly TextBox output = Ui.OutputBox();
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
                if (node["phase"]?.ToString() == "start") output.AppendText($"\n[{origin}] {node["cwd"]}\n❯ {node["command"]}\n");
                else { var body = node["output"]?.ToString(); if (!string.IsNullOrWhiteSpace(body)) output.AppendText(body + "\n"); output.AppendText($"[{origin}] ✓ finished\n"); }
            }
            output.ScrollToEnd();
        }
        catch { }
    }
    public void Dispose() => timer.Stop();
}

public sealed class SystemSampler
{
    [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint Low, High; public ulong Value => ((ulong)High << 32) | Low; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] private struct MemoryStatus { public uint Length; public uint Load; public ulong Total, Available, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, AvailableExtended; }
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    private ulong oldIdle, oldKernel, oldUser, oldNetwork;
    private DateTime oldTime = DateTime.UtcNow;
    private readonly ConcurrentDictionary<int, TimeSpan> processTimes = new();
    public double Cpu { get; private set; }
    public double Memory { get; private set; }
    public double NetworkBytesPerSecond { get; private set; }
    public string Network { get; private set; } = "—";
    public string Disk { get; private set; } = "—";
    public string Gpu { get; private set; } = "—";
    public int ProcessCount { get; private set; }
    public IReadOnlyList<(int Pid, string Name, double Cpu)> Processes { get; private set; } = [];
    private int gpuTick;

    public void Sample()
    {
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            var idleDelta = idle.Value - oldIdle; var total = kernel.Value - oldKernel + user.Value - oldUser;
            if (oldKernel != 0 && total > 0) Cpu = Math.Clamp(100.0 * (total - idleDelta) / total, 0, 100);
            oldIdle = idle.Value; oldKernel = kernel.Value; oldUser = user.Value;
        }
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (GlobalMemoryStatusEx(ref memory)) Memory = memory.Load;
        var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name.StartsWith(Path.GetPathRoot(Environment.SystemDirectory)!));
        if (drive != null) Disk = $"{100 * (drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize}%";
        var now = DateTime.UtcNow; var bytes = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up).Sum(n => (long)(n.GetIPv4Statistics().BytesReceived + n.GetIPv4Statistics().BytesSent));
        var seconds = Math.Max(.1, (now - oldTime).TotalSeconds);
        if (oldNetwork != 0) { NetworkBytesPerSecond = Math.Max(0, (bytes - (long)oldNetwork) / seconds); Network = Ui.Rate(NetworkBytesPerSecond); }
        oldNetwork = (ulong)Math.Max(0, bytes); oldTime = now;
        var samples = new List<(int, string, double)>();
        foreach (var process in Process.GetProcesses())
        {
            try { var current = process.TotalProcessorTime; processTimes.TryGetValue(process.Id, out var previous); processTimes[process.Id] = current; var percent = previous == default ? 0 : Math.Max(0, (current - previous).TotalSeconds / seconds / Environment.ProcessorCount * 100); samples.Add((process.Id, process.ProcessName, percent)); }
            catch { }
            finally { process.Dispose(); }
        }
        ProcessCount = samples.Count;
        Processes = samples.OrderByDescending(x => x.Item3).Take(12).ToArray();
        if (++gpuTick % 5 == 1) _ = SampleGpu();
    }

    private async Task SampleGpu()
    {
        try
        {
            var command = @"(Get-Counter '\GPU Engine(*)\Utilization Percentage' -ErrorAction SilentlyContinue).CounterSamples.CookedValue | Measure-Object -Sum | % Sum";
            var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -Command \"{command}\"") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            using var p = Process.Start(psi)!; var value = await p.StandardOutput.ReadToEndAsync(); await p.WaitForExitAsync();
            if (double.TryParse(value.Trim(), out var gpu)) Gpu = $"{Math.Clamp(gpu, 0, 100):0}%";
        }
        catch { Gpu = "—"; }
    }
}

public sealed class Sparkline : FrameworkElement
{
    private readonly Queue<double> samples = new();
    private readonly Brush stroke;
    public Sparkline(Brush stroke) { this.stroke = stroke; Height = 17; MinWidth = 40; SnapsToDevicePixels = true; IsHitTestVisible = false; }
    public void Add(double value) { samples.Enqueue(Math.Clamp(value, 0, 100)); while (samples.Count > 60) samples.Dequeue(); InvalidateVisual(); }
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var width = ActualWidth; var height = ActualHeight; if (width <= 1 || height <= 1) return;
        drawingContext.DrawLine(new Pen(Ui.WithAlpha(stroke, 36), 1), new Point(0, height - .5), new Point(width, height - .5));
        var values = samples.ToArray(); if (values.Length < 2) return;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(0, height - values[0] / 100 * (height - 2) - 1), false, false);
            for (var i = 1; i < values.Length; i++) context.LineTo(new Point(i * width / (values.Length - 1), height - values[i] / 100 * (height - 2) - 1), true, false);
        }
        geometry.Freeze(); drawingContext.DrawGeometry(null, new Pen(stroke, 1.5), geometry);
    }
}

public sealed class BtopPanel : Grid, IDisposable
{
    private readonly SystemSampler sampler = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TextBlock cpu = Ui.Metric(), gpu = Ui.Metric(), ram = Ui.Metric(), disk = Ui.Metric(), network = Ui.Metric(), processCount = Ui.Metric();
    private readonly Sparkline cpuChart = new(Theme.RedBrush), memoryChart = new(Brushes.Gold), gpuChart = new(Brushes.MediumPurple), diskChart = new(Brushes.DeepPink), networkChart = new(Brushes.Cyan), processChart = new(Brushes.LawnGreen);
    private readonly TextBox processes = Ui.OutputBox();
    public BtopPanel()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); RowDefinitions.Add(new RowDefinition());
        var metrics = new UniformGrid { Columns = 3, Margin = new Thickness(7, 7, 7, 3) };
        metrics.Children.Add(Ui.MetricCard("CPU", cpu, cpuChart, Theme.RedBrush));
        metrics.Children.Add(Ui.MetricCard("MEMORY", ram, memoryChart, Brushes.Gold));
        metrics.Children.Add(Ui.MetricCard("GPU", gpu, gpuChart, Brushes.MediumPurple));
        metrics.Children.Add(Ui.MetricCard("DISK", disk, diskChart, Brushes.DeepPink));
        metrics.Children.Add(Ui.MetricCard("NETWORK", network, networkChart, Brushes.Cyan));
        metrics.Children.Add(Ui.MetricCard("PROCESSES", processCount, processChart, Brushes.LawnGreen));
        Children.Add(metrics); Grid.SetRow(processes, 1); Children.Add(processes);
        timer.Tick += (_, _) => Refresh(); timer.Start(); Refresh();
    }
    private void Refresh()
    {
        sampler.Sample(); cpu.Text = $"{sampler.Cpu:0}%"; gpu.Text = sampler.Gpu; ram.Text = $"{sampler.Memory:0}%"; disk.Text = sampler.Disk; network.Text = sampler.Network; processCount.Text = sampler.ProcessCount.ToString();
        cpuChart.Add(sampler.Cpu); memoryChart.Add(sampler.Memory); gpuChart.Add(Ui.Percent(sampler.Gpu)); diskChart.Add(Ui.Percent(sampler.Disk));
        networkChart.Add(Math.Min(100, Math.Log10(1 + sampler.NetworkBytesPerSecond) / 7 * 100)); processChart.Add(Math.Min(100, sampler.ProcessCount / 5.0));
        processes.Text = " PID    CPU   PROCESS\n" + string.Join("\n", sampler.Processes.Select(p => $"{p.Pid,6} {p.Cpu,6:0.0}%  {p.Name}"));
    }
    public void Dispose() => timer.Stop();
}

public sealed class MainWindow : Window
{
    private readonly RedMode? dedicated;
    private readonly DockPanel root = new();
    private readonly Border content = new();
    private readonly Dictionary<RedMode, FrameworkElement> panels = [];
    private readonly List<RedMode> order = [RedMode.Watch, RedMode.Run, RedMode.Codex, RedMode.Btop];
    private readonly HashSet<RedMode> visibleModes = [RedMode.Watch, RedMode.Run, RedMode.Codex, RedMode.Btop];
    private readonly Dictionary<RedMode, double> manualHeights = Enum.GetValues<RedMode>().ToDictionary(x => x, _ => 260.0);
    private readonly SystemSampler toolbarSampler = new();
    private readonly DispatcherTimer toolbarTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly TextBlock toolbarStats = new();
    private Button? modePill;
    private bool cards = true;
    private int columns;
    private bool fit = true;
    private bool closeForReal;

    public MainWindow(RedMode? dedicated = null)
    {
        this.dedicated = dedicated;
        Title = dedicated is null ? "RedTrace" : $"RedTrace {dedicated}";
        Width = dedicated is null ? 1120 : dedicated == RedMode.Btop ? 820 : 720; Height = dedicated is null ? 700 : dedicated == RedMode.Btop ? 560 : 460;
        MinWidth = 620; MinHeight = 380; Background = Theme.Background; Foreground = Theme.Text; Opacity = Theme.Opacity; Topmost = true;
        WindowStyle = WindowStyle.None; AllowsTransparency = false; ResizeMode = ResizeMode.CanResize;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, CornerRadius = new CornerRadius(11), GlassFrameThickness = new Thickness(0), ResizeBorderThickness = new Thickness(6), UseAeroCaptionButtons = false });
        SourceInitialized += (_, _) => NativeWindowStyle.Apply(this);
        root.LastChildFill = true; root.Background = Theme.Background; Content = root; BuildToolbar();
        content.Background = Theme.Background; content.BorderBrush = new SolidColorBrush(Color.FromArgb(88, 255, 59, 77)); content.BorderThickness = new Thickness(1); content.CornerRadius = new CornerRadius(11); content.Margin = new Thickness(8, 0, 8, 8); content.ClipToBounds = true; root.Children.Add(content);
        if (dedicated is RedMode only) panels[only] = CreatePanel(only);
        else foreach (var mode in Enum.GetValues<RedMode>()) panels[mode] = CreatePanel(mode);
        UpdateModePill(); ShowLayout(); SizeChanged += (_, _) => { if (dedicated is null && cards) ShowCards(); };
        toolbarTimer.Tick += (_, _) => RefreshToolbarStats(); toolbarTimer.Start(); RefreshToolbarStats();
        Closing += (_, e) => { if (!closeForReal && dedicated is null) { e.Cancel = true; Hide(); } else DisposePanels(); };
    }

    private void BuildToolbar()
    {
        var bar = new Grid { Height = 38, Margin = new Thickness(10, 4, 8, 3), Background = Brushes.Transparent };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition());
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        DockPanel.SetDock(bar, Dock.Top); root.Children.Add(bar);
        bar.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(left, 0); bar.Children.Add(left);
        if (dedicated is RedMode only)
        {
            left.Children.Add(Ui.ModePill(Ui.ModeIcon(only), only.ToString().ToUpperInvariant(), Ui.Accent(only)));
        }
        else
        {
            modePill = Ui.PillButton("\uECA5  TABS", "Switch visible cards", CardVisibilityMenu);
            left.Children.Add(modePill);
        }

        toolbarStats.FontFamily = new FontFamily(Theme.FontName); toolbarStats.FontSize = 9;
        toolbarStats.Foreground = Theme.Muted; toolbarStats.VerticalAlignment = VerticalAlignment.Center;
        toolbarStats.HorizontalAlignment = HorizontalAlignment.Right; toolbarStats.Margin = new Thickness(10, 0, 8, 0);
        Grid.SetColumn(toolbarStats, 1); bar.Children.Add(toolbarStats);

        var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(right, 2); bar.Children.Add(right);
        if (dedicated is null) right.Children.Add(Ui.IconButton("\uE8A9", "Switch Tabs/Cards", () => { cards = !cards; UpdateModePill(); ShowLayout(); }));
        right.Children.Add(Ui.IconButton("\uE710", "New window", NewWindowMenu));
        right.Children.Add(Ui.IconButton("\uE8A9", "Card layout", LayoutMenu));
        right.Children.Add(Ui.IconButton("\uE790", "Appearance", Appearance));
        right.Children.Add(Ui.IconButton("\uE718", "Always on top", () => Topmost = !Topmost));
        right.Children.Add(Ui.IconButton("\uE921", "Minimize", () => WindowState = WindowState.Minimized));
        right.Children.Add(Ui.IconButton("\uE8BB", "Close", Close));
    }

    private void RefreshToolbarStats()
    {
        toolbarSampler.Sample();
        toolbarStats.Text = $"CPU {toolbarSampler.Cpu:0}%   GPU {toolbarSampler.Gpu}   RAM {toolbarSampler.Memory:0}%";
    }

    private void UpdateModePill()
    {
        if (modePill is null) return;
        modePill.Content = cards ? "\uECA5  CARDS  ▾" : "\uE8A9  TABS";
        modePill.ToolTip = cards ? "Choose visible cards" : "Switch to Cards to choose visible cards";
    }

    private void ShowLayout()
    {
        if (dedicated is RedMode mode) { Detach(panels[mode]); content.Child = panels[mode]; return; }
        if (cards) ShowCards(); else ShowTabs();
    }

    private void ShowTabs()
    {
        content.Child = null;
        var tabs = new TabControl { Background = Theme.Background, BorderThickness = new Thickness(0), Foreground = Theme.Text, Padding = new Thickness(5) };
        foreach (var mode in order) { Detach(panels[mode]); tabs.Items.Add(new TabItem { Header = $"{Ui.ModeIcon(mode)}  {mode.ToString().ToUpperInvariant()}", Content = panels[mode], Foreground = Theme.Text, Background = Theme.Panel }); }
        content.Child = tabs;
    }

    private void ShowCards()
    {
        content.Child = null;
        var activeModes = order.Where(visibleModes.Contains).ToArray();
        if (activeModes.Length == 0)
        {
            content.Child = new TextBlock { Text = "No cards selected\nClick CARDS to choose what to show.", TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Theme.Muted, FontFamily = new FontFamily(Theme.FontName), LineHeight = 24 };
            return;
        }
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = Theme.Background };
        var wrap = new WrapPanel { Margin = new Thickness(7), Background = Theme.Background }; scroll.Content = wrap; content.Child = scroll;
        var width = Math.Max(400, content.ActualWidth - 20); var height = Math.Max(220, content.ActualHeight - 20);
        var count = columns == 0 ? width >= 1650 ? 4 : width >= 1200 ? 3 : width >= 720 ? 2 : 1 : columns;
        count = Math.Min(count, activeModes.Length);
        var rows = (int)Math.Ceiling(activeModes.Length / (double)count); var cardWidth = width / count - 12; var cardHeight = fit ? Math.Max(180, height / rows - 12) : 260;
        foreach (var mode in activeModes) { Detach(panels[mode]); wrap.Children.Add(Card(mode, panels[mode], cardWidth, fit ? cardHeight : manualHeights[mode])); }
    }

    private Border Card(RedMode mode, FrameworkElement panel, double width, double height)
    {
        var border = new Border { Width = width, Height = height, Margin = new Thickness(6), BorderThickness = new Thickness(1), BorderBrush = Ui.WithAlpha(Ui.Accent(mode), 145), CornerRadius = new CornerRadius(10), Background = Theme.Panel, AllowDrop = true, ClipToBounds = true };
        var grid = new Grid(); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition()); border.Child = grid;
        var header = new DockPanel { Height = 32, Background = new SolidColorBrush(Color.FromRgb(12, 13, 18)), LastChildFill = true };
        var drag = Ui.Glyph("\uE700", Ui.Accent(mode)); drag.Margin = new Thickness(7, 0, 7, 0); drag.Cursor = Cursors.SizeAll; DockPanel.SetDock(drag, Dock.Right); header.Children.Add(drag);
        var detach = Ui.IconButton("\uE8A7", $"Open dedicated {mode} window", () => new MainWindow(mode).Show(), Ui.Accent(mode)); detach.Margin = new Thickness(2, 3, 0, 3); DockPanel.SetDock(detach, Dock.Right); header.Children.Add(detach);
        var title = Ui.ModePill(Ui.ModeIcon(mode), mode.ToString().ToUpperInvariant(), Ui.Accent(mode), compact: true); title.Margin = new Thickness(7, 0, 0, 0); header.Children.Add(title);
        drag.MouseMove += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragDrop.DoDragDrop(drag, mode, DragDropEffects.Move); };
        border.Drop += (_, e) => { if (e.Data.GetData(typeof(RedMode)) is RedMode source && source != mode) { var a = order.IndexOf(source); var b = order.IndexOf(mode); order.RemoveAt(a); order.Insert(b, source); ShowCards(); } };
        grid.Children.Add(header); Grid.SetRow(panel, 1); grid.Children.Add(panel);
        var thumb = Ui.ResizeThumb();
        thumb.DragDelta += (_, e) => { fit = false; border.Height = Math.Max(180, border.Height + e.VerticalChange); manualHeights[mode] = border.Height; };
        Grid.SetRowSpan(thumb, 2); grid.Children.Add(thumb); return border;
    }

    private static void Detach(FrameworkElement element)
    {
        if (element.Parent is ContentControl cc) cc.Content = null;
        else if (element.Parent is Panel panel) panel.Children.Remove(element);
        else if (element.Parent is Decorator decorator) decorator.Child = null;
    }

    private static FrameworkElement CreatePanel(RedMode mode) => mode switch { RedMode.Watch => new WatchPanel(), RedMode.Run => new RunnerPanel(), RedMode.Codex => new CodexPanel(), _ => new BtopPanel() };

    private void NewWindowMenu()
    {
        var menu = new ContextMenu();
        foreach (var mode in Enum.GetValues<RedMode>()) { var item = new MenuItem { Header = $"New {mode} window", Tag = mode }; item.Click += (_, _) => new MainWindow((RedMode)item.Tag).Show(); menu.Items.Add(item); }
        menu.IsOpen = true;
    }

    private void LayoutMenu()
    {
        var menu = new ContextMenu();
        var fitItem = new MenuItem { Header = "Fit cards to window", IsCheckable = true, IsChecked = fit }; fitItem.Click += (_, _) => { fit = fitItem.IsChecked; if (cards) ShowCards(); }; menu.Items.Add(fitItem);
        foreach (var value in new[] { 0, 1, 2, 3, 4 }) { var item = new MenuItem { Header = value == 0 ? "Columns: AUTO" : $"Columns: {value}", IsCheckable = true, IsChecked = columns == value, Tag = value }; item.Click += (_, _) => { columns = (int)item.Tag; if (cards) ShowCards(); }; menu.Items.Add(item); }
        menu.IsOpen = true;
    }

    private void CardVisibilityMenu()
    {
        if (!cards) { cards = true; UpdateModePill(); ShowCards(); }
        var menu = new ContextMenu();
        foreach (var mode in Enum.GetValues<RedMode>())
        {
            var item = new MenuItem { Header = mode.ToString().ToUpperInvariant(), IsCheckable = true, IsChecked = visibleModes.Contains(mode), Tag = mode };
            item.Click += (_, _) => { var value = (RedMode)item.Tag; if (item.IsChecked) visibleModes.Add(value); else visibleModes.Remove(value); ShowCards(); };
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var all = new MenuItem { Header = "Show all cards" }; all.Click += (_, _) => { visibleModes.Clear(); foreach (var mode in Enum.GetValues<RedMode>()) visibleModes.Add(mode); ShowCards(); }; menu.Items.Add(all);
        menu.IsOpen = true;
    }

    private void Appearance()
    {
        var dialog = new Window { Title = "Appearance", Width = 340, Height = 330, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Theme.Panel, Foreground = Brushes.White, ResizeMode = ResizeMode.NoResize };
        var stack = new StackPanel { Margin = new Thickness(18) };
        stack.Children.Add(new TextBlock { Text = "Window opacity", Margin = new Thickness(0, 0, 0, 5) });
        var opacity = new Slider { Minimum = .25, Maximum = 1, Value = Opacity, TickFrequency = .05, IsSnapToTickEnabled = true }; opacity.ValueChanged += (_, _) => Opacity = opacity.Value; stack.Children.Add(opacity);
        stack.Children.Add(new TextBlock { Text = "Font size", Margin = new Thickness(0, 14, 0, 5) });
        var font = new Slider { Minimum = 9, Maximum = 24, Value = Theme.FontSize, TickFrequency = 1, IsSnapToTickEnabled = true }; font.ValueChanged += (_, _) => { Theme.FontSize = font.Value; Ui.ApplyTypography(this); }; stack.Children.Add(font);
        stack.Children.Add(new TextBlock { Text = "Font", Margin = new Thickness(0, 14, 0, 5) });
        var family = new ComboBox { ItemsSource = new[] { "Cascadia Mono", "Consolas", "Courier New", "Lucida Console" }, SelectedItem = Theme.FontName };
        family.SelectionChanged += (_, _) => { if (family.SelectedItem is string name) { Theme.FontName = name; Ui.ApplyTypography(this); } }; stack.Children.Add(family);
        var colors = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        colors.Children.Add(Ui.Button("Text color", "Choose terminal text color", () => ChooseColor(false)));
        colors.Children.Add(Ui.Button("Background", "Choose window background color", () => ChooseColor(true)));
        stack.Children.Add(colors);
        var close = Ui.Button("Done", "Close", dialog.Close); close.Margin = new Thickness(0, 18, 0, 0); stack.Children.Add(close); dialog.Content = stack; dialog.ShowDialog();
        Theme.Opacity = Opacity; Theme.Save();
    }

    private void ChooseColor(bool background)
    {
        using var picker = new Forms.ColorDialog { FullOpen = true };
        if (picker.ShowDialog() != Forms.DialogResult.OK) return;
        var color = Color.FromRgb(picker.Color.R, picker.Color.G, picker.Color.B);
        if (background) { Theme.Background = new SolidColorBrush(color); Background = Theme.Background; root.Background = Theme.Background; content.Background = Theme.Background; }
        else { Theme.Text = new SolidColorBrush(color); Ui.ApplyTerminalColors(this); }
        Theme.Save();
    }

    private void DisposePanels() { toolbarTimer.Stop(); foreach (var panel in panels.Values.OfType<IDisposable>()) panel.Dispose(); }
    public void CloseForReal() { closeForReal = true; Close(); }
}

public static class NativeWindowStyle
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    public static void Apply(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            var enabled = 1; DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
            var rounded = 2; DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int));
            var backdrop = 2; DwmSetWindowAttribute(handle, 38, ref backdrop, sizeof(int));
        }
        catch { }
    }
}

public static class Ui
{
    public static TextBox OutputBox() => new() { IsReadOnly = true, AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.Transparent, Foreground = Theme.Text, SelectionBrush = new SolidColorBrush(Color.FromRgb(76, 28, 36)), BorderThickness = new Thickness(0), FontFamily = new FontFamily(Theme.FontName), FontSize = Theme.FontSize, Padding = new Thickness(12, 10, 12, 10) };
    public static TextBox InputBox() => new() { Height = 27, Background = Theme.Raised, Foreground = Theme.Text, CaretBrush = Theme.RedBrush, BorderBrush = Theme.Hairline, BorderThickness = new Thickness(1), FontFamily = new FontFamily(Theme.FontName), FontSize = Theme.FontSize, Padding = new Thickness(8, 4, 8, 4), VerticalContentAlignment = VerticalAlignment.Center };
    public static Button Button(string text, string tip, Action action) { var b = new Button { Content = text, ToolTip = tip, Margin = new Thickness(2, 0, 2, 0), Padding = new Thickness(8, 4, 8, 4), Background = Brushes.Transparent, Foreground = Theme.RedBrush, BorderBrush = Theme.Hairline }; b.Click += (_, _) => action(); return b; }
    public static Button IconButton(string glyph, string tip, Action action, Brush? accent = null)
    {
        var b = Button(glyph, tip, action); b.Tag = "RedTraceIcon"; b.Width = 28; b.Height = 27; b.Padding = new Thickness(0); b.Margin = new Thickness(2, 0, 2, 0); b.BorderBrush = Brushes.Transparent; b.Foreground = accent ?? new SolidColorBrush(Color.FromRgb(188, 189, 198)); b.FontFamily = new FontFamily("Segoe Fluent Icons"); b.FontSize = 12; return b;
    }
    public static Button PillButton(string text, string tip, Action action)
    {
        var b = Button(text, tip, action); b.Height = 26; b.Padding = new Thickness(10, 3, 10, 3); b.Margin = new Thickness(0, 0, 4, 0); b.Foreground = Theme.RedBrush; b.Background = new SolidColorBrush(Color.FromRgb(31, 18, 22)); b.BorderBrush = new SolidColorBrush(Color.FromRgb(91, 35, 44)); b.FontFamily = new FontFamily(Theme.FontName); b.FontWeight = FontWeights.SemiBold; b.FontSize = 10; return b;
    }
    public static Button SelectorButton(string text, string tip, Action action)
    {
        var button = Button(text + "  ▾", tip, action); button.Height = 27; button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Background = Theme.Raised; button.BorderBrush = Theme.Hairline; button.Foreground = new SolidColorBrush(Color.FromRgb(222, 223, 229));
        button.FontFamily = new FontFamily(Theme.FontName); button.FontSize = 10; button.Padding = new Thickness(9, 3, 8, 3); return button;
    }
    public static TextBlock Glyph(string value, Brush? color = null) => new() { Text = value, FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 11, Foreground = color ?? Theme.RedBrush, VerticalAlignment = VerticalAlignment.Center };
    public static Border ModePill(string glyph, string text, Brush accent, bool compact = false)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(Glyph(glyph, accent));
        row.Children.Add(new TextBlock { Text = text, FontFamily = new FontFamily(Theme.FontName), FontSize = compact ? 9 : 10, FontWeight = FontWeights.SemiBold, Foreground = accent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) });
        return new Border { Child = row, Background = Brushes.Transparent, Padding = compact ? new Thickness(0) : new Thickness(8, 4, 8, 4), CornerRadius = new CornerRadius(7), VerticalAlignment = VerticalAlignment.Center };
    }
    public static string ModeIcon(RedMode mode) => mode switch { RedMode.Watch => "\uE890", RedMode.Run => "\uE756", RedMode.Codex => "\uE943", _ => "\uE9D2" };
    public static TextBlock Label(string value) => new() { Text = value, Foreground = Theme.RedBrush, FontFamily = new FontFamily(Theme.FontName), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    public static TextBlock Metric() => new() { Foreground = Brushes.White, FontSize = 17, FontFamily = new FontFamily(Theme.FontName), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 2, 0, 0) };
    public static Border MetricCard(string title, TextBlock value, Sparkline chart, Brush accent)
    {
        var grid = new Grid(); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var top = new Grid(); top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new TextBlock { Text = title, Foreground = accent, FontFamily = new FontFamily(Theme.FontName), FontWeight = FontWeights.SemiBold, FontSize = 8, VerticalAlignment = VerticalAlignment.Center };
        top.Children.Add(label); Grid.SetColumn(value, 1); value.Margin = new Thickness(8, 0, 0, 0); top.Children.Add(value); grid.Children.Add(top);
        chart.Margin = new Thickness(0, 4, 0, 0); Grid.SetRow(chart, 1); grid.Children.Add(chart);
        return new Border { Child = grid, BorderBrush = WithAlpha(accent, 92), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Margin = new Thickness(4), Padding = new Thickness(9, 6, 9, 6), Background = Theme.Raised };
    }
    public static Thumb ResizeThumb()
    {
        var visual = new FrameworkElementFactory(typeof(Border)); visual.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        return new Thumb { Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Cursor = Cursors.SizeNWSE, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Template = new ControlTemplate(typeof(Thumb)) { VisualTree = visual } };
    }
    public static Brush Accent(RedMode mode) => mode switch { RedMode.Run => Brushes.Orange, RedMode.Codex => Brushes.DeepPink, RedMode.Btop => Brushes.Cyan, _ => Theme.RedBrush };
    public static Brush WithAlpha(Brush brush, byte alpha)
    {
        var color = brush is SolidColorBrush solid ? solid.Color : Colors.White;
        return new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
    }
    public static double Percent(string value) => double.TryParse(value.Trim().TrimEnd('%'), out var number) ? Math.Clamp(number, 0, 100) : 0;
    public static string ShellName(ShellKind kind) => kind switch { ShellKind.CommandPrompt => "Command Prompt", ShellKind.Wsl => "WSL", _ => "PowerShell" };
    public static string Rate(double bytes) => bytes > 1024 * 1024 ? $"{bytes / 1024 / 1024:0.0} MB/s" : bytes > 1024 ? $"{bytes / 1024:0} KB/s" : $"{bytes:0} B/s";
    public static void ApplyTypography(DependencyObject root) { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is Control c && !Equals(c.Tag, "RedTraceIcon")) { c.FontFamily = new FontFamily(Theme.FontName); c.FontSize = Theme.FontSize; } ApplyTypography(child); } }
    public static void ApplyTerminalColors(DependencyObject root) { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is TextBox box) box.Foreground = Theme.Text; ApplyTerminalColors(child); } }
}

public static class HookInstaller
{
    private const string Marker = "codex-hook.ps1";
    public static void Install(bool silent = false)
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var support = Path.Combine(home, ".redtrace"); Directory.CreateDirectory(support);
            var source = Path.Combine(AppContext.BaseDirectory, "codex-hook.ps1"); var hook = Path.Combine(support, "codex-hook.ps1");
            if (File.Exists(source)) File.Copy(source, hook, true);
            var codex = Path.Combine(home, ".codex"); Directory.CreateDirectory(codex); var configPath = Path.Combine(codex, "hooks.json");
            JsonObject config;
            try { config = File.Exists(configPath) ? JsonNode.Parse(File.ReadAllText(configPath))?.AsObject() ?? new JsonObject() : new JsonObject(); } catch { config = new JsonObject(); }
            var hooks = config["hooks"] as JsonObject;
            if (hooks is null) { hooks = new JsonObject(); config["hooks"] = hooks; }
            var hookCommand = $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{hook}\"";
            foreach (var eventName in new[] { "PreToolUse", "PostToolUse" })
            {
                var groups = hooks[eventName] as JsonArray;
                if (groups is null) { groups = new JsonArray(); hooks[eventName] = groups; }
                for (var i = groups.Count - 1; i >= 0; i--) if (groups[i]?.ToJsonString().Contains(Marker, StringComparison.OrdinalIgnoreCase) == true) groups.RemoveAt(i);
                groups.Add(new JsonObject
                {
                    ["matcher"] = ".*",
                    ["hooks"] = new JsonArray(new JsonObject
                    {
                        ["type"] = "command",
                        ["command"] = hookCommand,
                        ["timeout"] = 3,
                        ["statusMessage"] = "Streaming command to RedTrace"
                    })
                });
            }
            File.WriteAllText(configPath, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            if (!silent) System.Windows.MessageBox.Show("Local Codex hooks installed. Review /hooks and begin a new Codex session.", "RedTrace");
        }
        catch (Exception ex) { if (!silent) System.Windows.MessageBox.Show(ex.Message, "RedTrace hook installation failed"); }
    }
}
