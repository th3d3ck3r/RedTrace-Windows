# RedTrace for Windows

Native Windows 11 x64 companion to RedTrace for macOS. All activity remains local.

## Visual parity update

The Windows shell now closely follows the macOS Cards layout: a compact CARDS pill, 2×2 responsive dashboard, inset near-black panels, thin per-card accent colors, Fluent icon controls, integrated dark command inputs, six system-monitor tiles, and CPU/GPU/RAM status in the title bar. Windows-specific PowerShell, Command Prompt, and WSL behavior is unchanged.

## Included

- Tabs and responsive movable/resizable Cards layouts for WATCH, RUN, CODEX, and BTOP
- Selectable WATCH source: all RedTrace shells, PowerShell, Command Prompt, or WSL
- Independent PowerShell, Command Prompt, and WSL Runner sessions
- Shell-specific Common Commands menus that insert commands for review
- Dedicated windows and multiple simultaneous Runner sessions
- Local Codex hook event view
- CPU, best-effort GPU, RAM, disk, network, process, and system-tray monitoring
- Background operation, tray popup, always-on-top, persistent opacity, fonts, and colors

External Windows terminal monitoring is intentionally excluded. WATCH displays sessions launched by RedTrace.

## Build and install

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), open PowerShell in this folder, and run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\install.ps1
```

The self-contained x64 app is installed under `%LOCALAPPDATA%\RedTrace` and added to the Start menu. For Codex events, review the two RedTrace entries in `/hooks`, approve them, and begin a new local Codex session.

Alternatively, put these files in a GitHub repository and run **Build RedTrace Windows** under Actions. Download the `RedTrace-Windows-x64` artifact when it finishes.

## Notes

- WSL requires the optional Windows Subsystem for Linux feature.
- GPU usage is best-effort because counter availability varies by driver.
- Some full-screen console programs require deeper ConPTY emulation; ordinary commands, prompts, stdout, and stderr are captured.
