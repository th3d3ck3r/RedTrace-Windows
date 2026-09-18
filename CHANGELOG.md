# Changelog

All notable changes to RedTrace for Windows are documented here.

## 3.0.0 — 2026-09-18

### Added

- Native ConPTY-backed interactive Runner sessions for PowerShell, Command Prompt, WSL, and custom shells.
- Terminal view support for live input, paste, resizing, ANSI styling, alternate-screen behavior, cursor visibility, bracketed paste, and basic terminal query responses.
- ChatGPT activity view with structured local hook records for tool start/finish events, command inputs, output, exit status, and errors.
- ChatGPT terminology throughout the interface, replacing the older Codex-facing label.
- Responsive red-and-black WinUI 3 workspace with tabs, cards, dedicated windows, rounded surfaces, Acrylic support, tray operation, and persistent appearance controls.
- Watch source selection, common command insertion menus, system-monitor cards, CPU/GPU/RAM title-bar readouts, and per-thread CPU graphs.

### Changed

- Runner sessions now use a real terminal rather than redirected standard streams. Terminal-aware programs receive a proper console environment.
- Hooks preserve non-command tool events and carry structured target/input/exit/error fields where available.
- Main README rewritten to match the macOS project, with clearer install, privacy, and capability guidance.

### Fixed

- Resolved terminal and activity-view build issues.
- Made Windows CI safe for headless GitHub Actions runners.
- Stabilized terminal colors and terminal-view resource references.

## Earlier releases

Earlier Windows development was delivered through the initial WinUI 3 rewrite. It introduced the card dashboard, selectable Watch sources, shell-specific runners, local ChatGPT/Codex hooks, background tray mode, appearance settings, movable/resizable monitor cards, and rounded popup menus.
