using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RedTrace.Windows;

public enum PanelMode { Watch, Run, Codex, Btop }
public enum ShellKind { PowerShell, CommandPrompt, Wsl }

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
            }
        };
        try
        {
            process.Start();
            cancellation = new CancellationTokenSource();
            _ = Pump(process.StandardOutput, cancellation.Token);
            _ = Pump(process.StandardError, cancellation.Token);
            Emit($"— {DisplayName(Kind)} session started —\n");
        }
        catch (Exception ex) { Emit($"Could not start {DisplayName(Kind)}: {ex.Message}\n"); }
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
    public static string DisplayName(ShellKind kind) => kind switch { ShellKind.CommandPrompt => "Command Prompt", ShellKind.Wsl => "WSL", _ => "PowerShell" };
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

public sealed class SystemSampler
{
    [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint Low, High; public ulong Value => ((ulong)High << 32) | Low; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] private struct MemoryStatus { public uint Length; public uint Load; public ulong Total, Available, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, AvailableExtended; }
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    private ulong oldIdle, oldKernel, oldUser, oldNetwork;
    private DateTime oldTime = DateTime.UtcNow;
    private readonly ConcurrentDictionary<int, TimeSpan> processTimes = new();
    private int gpuTick;
    public double Cpu { get; private set; }
    public double Memory { get; private set; }
    public double NetworkBytesPerSecond { get; private set; }
    public string Network { get; private set; } = "—";
    public string Disk { get; private set; } = "—";
    public string Gpu { get; private set; } = "—";
    public int ProcessCount { get; private set; }
    public IReadOnlyList<(int Pid, string Name, double Cpu)> Processes { get; private set; } = [];

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
        var now = DateTime.UtcNow;
        var bytes = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up).Sum(n => (long)(n.GetIPv4Statistics().BytesReceived + n.GetIPv4Statistics().BytesSent));
        var seconds = Math.Max(.1, (now - oldTime).TotalSeconds);
        if (oldNetwork != 0) { NetworkBytesPerSecond = Math.Max(0, (bytes - (long)oldNetwork) / seconds); Network = Rate(NetworkBytesPerSecond); }
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
            const string command = @"(Get-Counter '\GPU Engine(*)\Utilization Percentage' -ErrorAction SilentlyContinue).CounterSamples.CookedValue | Measure-Object -Sum | % Sum";
            var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -Command \"{command}\"") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            using var p = Process.Start(psi)!; var value = await p.StandardOutput.ReadToEndAsync(); await p.WaitForExitAsync();
            if (double.TryParse(value.Trim(), out var gpu)) Gpu = $"{Math.Clamp(gpu, 0, 100):0}%";
        }
        catch { Gpu = "—"; }
    }

    private static string Rate(double bytes) => bytes > 1024 * 1024 ? $"{bytes / 1024 / 1024:0.0} MB/s" : bytes > 1024 ? $"{bytes / 1024:0} KB/s" : $"{bytes:0} B/s";
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
            var source = Path.Combine(AppContext.BaseDirectory, Marker); var hook = Path.Combine(support, Marker);
            if (File.Exists(source)) File.Copy(source, hook, true);
            var codex = Path.Combine(home, ".codex"); Directory.CreateDirectory(codex); var configPath = Path.Combine(codex, "hooks.json");
            JsonObject config;
            try { config = File.Exists(configPath) ? JsonNode.Parse(File.ReadAllText(configPath))?.AsObject() ?? new JsonObject() : new JsonObject(); } catch { config = new JsonObject(); }
            var hooks = config["hooks"] as JsonObject ?? new JsonObject(); config["hooks"] = hooks;
            var hookCommand = $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{hook}\"";
            foreach (var eventName in new[] { "PreToolUse", "PostToolUse" })
            {
                var groups = hooks[eventName] as JsonArray ?? new JsonArray(); hooks[eventName] = groups;
                for (var i = groups.Count - 1; i >= 0; i--) if (groups[i]?.ToJsonString().Contains(Marker, StringComparison.OrdinalIgnoreCase) == true) groups.RemoveAt(i);
                groups.Add(new JsonObject { ["matcher"] = ".*", ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = hookCommand, ["timeout"] = 3, ["statusMessage"] = "Streaming command to RedTrace" }) });
            }
            File.WriteAllText(configPath, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch when (silent) { }
    }
}
