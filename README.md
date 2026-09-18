# RedTrace for Windows

> A local-first command activity viewer, interactive terminal workspace, and live system monitor for Windows.

RedTrace brings your shell sessions, ChatGPT/Codex command activity, and system telemetry into one native Windows desktop app. Nothing is sent to a RedTrace service: its logs, hook events, and terminal activity remain on your PC.

## Highlights

- **Interactive terminals** — persistent PowerShell, Command Prompt, WSL, and custom shell sessions powered by ConPTY
- **Live Watch** — view activity from RedTrace-launched shells, or filter by shell type
- **ChatGPT activity** — readable local hook events for commands, inputs, results, exit status, and errors
- **System dashboard** — CPU, best-effort GPU, memory, disk, network, and process telemetry
- **Flexible workspace** — use focused tabs or a responsive card dashboard; pop out dedicated Watch, Run, ChatGPT, and BTOP windows
- **Made for the desktop** — dark red-and-black theme, rounded surfaces, Acrylic where Windows supports it, tray mode, opacity, fonts/colors, and an always-on-top option
- **Command help** — shell-specific Common Commands menus insert a command for review; choosing one never runs it

## Install

### Download a release

Choose the latest **Windows x64** build from [Releases](../../releases), extract it, and run `install.ps1` from PowerShell:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\install.ps1
```

The installer puts RedTrace in `%LOCALAPPDATA%\RedTrace` and creates a Start-menu entry.

### Build from source

Requirements:

- Windows 11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows App SDK dependencies restored by the project

```powershell
dotnet restore
dotnet build -c Release
.\install.ps1
```

GitHub Actions also builds the project. Download the `RedTrace-Windows-x64` artifact from a successful **Build RedTrace Windows** run.

## Using RedTrace

1. Open **RUN**, select PowerShell, CMD, WSL, or a custom shell, then start a session.
2. Use **WATCH** to follow all RedTrace shell sessions or a single shell source.
3. Switch between **Tabs** for a focused workspace and **Cards** for a live dashboard.
4. Open the **ChatGPT** tab to view hook-delivered activity.
5. Use the tray icon to keep RedTrace available after closing ordinary windows.

## ChatGPT/Codex activity

Open `/hooks` in a local Codex session, approve the RedTrace hook entries, then begin a new local session. RedTrace records supported tool activity locally and distinguishes command lifecycle events, target/input details, output, exit status, and errors when supplied.

RedTrace cannot passively inspect an unrelated cloud conversation. It can show the events delivered to its local hooks.

## Privacy and limitations

- RedTrace monitors the sessions it launches; it intentionally does **not** attach to arbitrary existing Windows Terminal windows.
- Full-screen or graphical terminal programs may require terminal emulation beyond the current ConPTY renderer. Standard interactive prompts and normal command output are supported.
- GPU readings are best-effort because performance counter availability depends on the driver.
- Review commands before running them. Shell sessions run with your Windows account permissions.

## Uninstall

Remove the installed folder and Start-menu entry, then remove the RedTrace hook entries through `/hooks` if you no longer want ChatGPT activity capture. Local logs can be removed separately if desired.

## Release notes

See [CHANGELOG.md](CHANGELOG.md) for the complete release history.
