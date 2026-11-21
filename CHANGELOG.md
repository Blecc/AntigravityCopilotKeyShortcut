# Changelog

All notable changes to this project will be documented in this file.

## [v1.1.0] - 2026-11-21

### Added
- **Choose between Antigravity GUI and CLI:** You can now choose whether pressing the Copilot key targets the **Antigravity GUI** (`Antigravity.exe`), the **Antigravity CLI** (`agy.exe`), or opens an **Ask Every Time** quick picker popup (`1`/`G` for GUI, `2`/`C` for CLI, `Esc` to cancel, with a *Remember choice* option).
- **Tray menu controls:** Switch target mode (**Target: Antigravity GUI**, **Target: Antigravity CLI (agy)**, **Target: Ask Every Time (Popup)**) or directly launch/focus either version from the system tray menu.
- **Automatic `agy.exe` detection:** Auto-detects the Antigravity CLI binary at setup time and runtime across standard install folders, running processes, registry `App Paths`, and `PATH`.
- **Independent window tracking & persistence:** GUI and CLI windows are tracked independently for hide-to-tray, focus, and close actions, and your target selection (`TargetMode`) is saved automatically in `config.ini`.

## [v1.0.0] - 2026-11-21

### Added
- Initial release remapping the Windows 11 hardware Copilot key (`Win + Shift + F23`) to toggle Antigravity while suppressing Windows Copilot and the Start Menu.
- System tray utility with **Minimize to Tray (Hide)** and **Close App** focused-window actions persisted in `config.ini`.
- Auto-detection of `Antigravity.exe` and the .NET Framework C# compiler (`csc.exe`).
- One-line PowerShell installer (`install.ps1`), double-click installer (`install.bat`), build script (`Build-And-Install.ps1`), and automated GitHub Releases workflow.
