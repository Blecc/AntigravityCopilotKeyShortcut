# Antigravity Copilot Key Shortcut

A lightweight, zero-dependency Windows 11 system tray utility written in C# (.NET Framework 4.x) that remaps the physical **Copilot key** on your keyboard into a dedicated toggle button for **Antigravity** (**GUI** `Antigravity.exe`, **CLI** `agy.exe`, or a quick **Ask Every Time** popup).

| Current State of Target (GUI or CLI) | Action When Pressing the Copilot Key |
| :--- | :--- |
| **Not running** | Auto-detects and launches `Antigravity.exe` (GUI) or `agy.exe` (CLI) |
| **Running in background, minimized, or hidden to tray** | Restores/unhides and brings the target window to the foreground (preserving maximized/normal state) |
| **Already open and focused** | **Minimize to Tray (default):** Hides the window completely from the taskbar so it stays alive behind the hidden system tray icon<br>**Close App (optional):** Cleanly closes the window (`WM_CLOSE`) |

Switch between **Target: Antigravity GUI**, **Target: Antigravity CLI (agy)**, and **Target: Ask Every Time (Popup)** — as well as **Minimize to Tray (Hide)** and **Close App** — anytime from the system tray right-click menu; your choices are saved automatically in `config.ini`.

Both Windows Copilot and the Windows Start Menu are suppressed when you press the key.

---

## Requirements

- **OS:** Windows 11 (64-bit) with a hardware Copilot key (or any keyboard emitting `Win + Shift + F23`)
- **Runtime / Compiler:** .NET Framework 4.x (included with Windows; the setup script checks for `csc.exe` and offers to install the .NET Framework 4.8 Developer Pack if missing)
- **App:** Antigravity desktop app (`Antigravity.exe`) and/or Antigravity CLI (`agy.exe`) — both auto-detected at runtime with no manual path configuration required

---

## Quick Start (Download & Install)

### Option 1: Double-Click `install.bat` (Easiest — no terminal, no Git required)
1. Download [`install.bat`](https://raw.githubusercontent.com/Blecc/AntigravityCopilotKeyShortcut/main/install.bat) (right-click → *Save link as...*).
2. Double-click **`install.bat`**.

It automatically downloads the latest version into `%LOCALAPPDATA%\AntigravityCopilotKeyShortcut`, compiles `CopilotToAntigravity.exe`, adds it to Windows Startup, and launches it.

### Option 2: One-Line PowerShell Install (No download needed)
Open PowerShell and run:
```powershell
irm https://raw.githubusercontent.com/Blecc/AntigravityCopilotKeyShortcut/main/install.ps1 | iex
```

> **Custom install folder:**
> ```powershell
> $env:ANTIGRAVITY_COPILOT_INSTALL_DIR = "C:\Utility\Programmi\AntigravityCopilotKeyShortcut"
> irm https://raw.githubusercontent.com/Blecc/AntigravityCopilotKeyShortcut/main/install.ps1 | iex
> ```

### Option 3: Clone with Git
```powershell
git clone https://github.com/Blecc/AntigravityCopilotKeyShortcut.git
cd AntigravityCopilotKeyShortcut
.\Build-And-Install.bat
```

### Option 4: Direct ZIP / GitHub Releases
1. Download the [latest source ZIP](https://github.com/Blecc/AntigravityCopilotKeyShortcut/archive/refs/heads/main.zip) (or a pre-built release from [GitHub Releases](https://github.com/Blecc/AntigravityCopilotKeyShortcut/releases/latest)) and extract it to any permanent folder.
2. Double-click **`Build-And-Install.bat`** (or run `CopilotToAntigravity.exe` directly if using a pre-built release).

### What `Build-And-Install` Does
- Detects your Antigravity installation and the .NET Framework C# compiler (`csc.exe`) — prompting to install .NET Framework 4.8 Developer Pack if missing
- Stops any running instance of the helper
- Compiles `CopilotToAntigravity.exe`
- Adds a shortcut (`CopilotToAntigravity.lnk`) to your Windows Startup folder (`shell:startup`)
- Launches the utility in the system tray

### Updating Later
Re-run the one-line installer above (your `config.ini` settings are preserved), or run `git pull` followed by `.\Build-And-Install.bat`.

### Optional Setup Flags

```powershell
# Compile and launch without creating a Windows Startup shortcut
powershell -ExecutionPolicy Bypass -File .\Build-And-Install.ps1 -NoStartup

# Compile and configure Startup without launching immediately
powershell -ExecutionPolicy Bypass -File .\Build-And-Install.ps1 -NoLaunch
```

---

## Repository Contents

| File | Purpose |
| :--- | :--- |
| `install.bat` | Standalone double-click installer (can be downloaded by itself; runs local `install.ps1` or fetches it from GitHub) |
| `install.ps1` | One-line remote installer: downloads the repo via Git (or ZIP fallback), preserves `config.ini`, and runs `Build-And-Install.ps1` |
| `CopilotToAntigravity.cs` | C# source code for the keyboard hook, tray icon, toggle/hide/close logic, and Antigravity path auto-detection |
| `Build-And-Install.ps1` | PowerShell setup script: checks Antigravity & C# compiler state (prompts to install compiler if missing), builds `CopilotToAntigravity.exe`, updates the Startup shortcut, and launches it |
| `Build-And-Install.bat` | Double-click launcher for `Build-And-Install.ps1` (bypasses PowerShell execution policy) |
| `.github/workflows/release.yml` | GitHub Actions workflow that compiles `CopilotToAntigravity.exe` and attaches a ready-to-run release ZIP when pushing a `v*` tag |
| `README.md` | Project documentation |

### Generated / System Files
- **`CopilotToAntigravity.exe`** — Native Windows executable compiled by `Build-And-Install.ps1` inside the project folder.
- **`config.ini`** — Auto-created settings file storing your chosen target (`TargetMode=GUI`, `TargetMode=CLI`, or `TargetMode=Ask`) and focused action (`FocusedAction=MinimizeToTray` or `FocusedAction=Close`).
- **Windows Startup Shortcut** — `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\CopilotToAntigravity.lnk` (created automatically unless `-NoStartup` is passed).

> **Tip:** Press `Win + R`, type `shell:startup`, and press Enter to open your Windows Startup folder.

---

## Usage

### Start Manually
If you skipped auto-start or previously exited the helper, double-click `CopilotToAntigravity.exe` or run it from PowerShell inside the project folder:
```powershell
Start-Process .\CopilotToAntigravity.exe
```

To check whether it is currently running:
```powershell
Get-Process -Name CopilotToAntigravity -ErrorAction SilentlyContinue
```

### Choose Target (GUI vs. CLI vs. Ask Every Time)
1. Click the **^** arrow near the Windows clock (bottom-right taskbar).
2. Right-click the **Antigravity** tray icon (`Copilot -> Antigravity`).
3. Choose one of:
   - **Target: Antigravity GUI** *(default)* — Launches, focuses, hides, or closes `Antigravity.exe`.
   - **Target: Antigravity CLI (agy)** — Launches, focuses, hides, or closes `agy.exe`.
   - **Target: Ask Every Time (Popup)** — Opens a compact centered picker popup each time you press the Copilot key:
     - Press **`1`** or **`G`** (or click) for **Antigravity GUI**
     - Press **`2`** or **`C`** (or click) for **Antigravity CLI (agy)**
     - Press **`Esc`** (or click outside, or press the Copilot key again) to cancel
     - Optionally check **Remember choice** in the popup to save your selection as the default.

You can also launch or focus either surface directly anytime from the tray menu (**Open / Show Antigravity GUI** / **Open / Show Antigravity CLI (agy)**).

### Choose Focused Action (Minimize to Tray vs. Close App)
1. Click the **^** arrow near the Windows clock (bottom-right taskbar).
2. Right-click the **Antigravity** tray icon (`Copilot -> Antigravity`).
3. Choose either:
   - **When Focused: Minimize to Tray (Hide)** *(default)* — Hides the active Antigravity GUI or CLI window from the taskbar and keeps it running silently behind the tray icon until you press the Copilot key again (or open it from the tray).
   - **When Focused: Close App** — Sends a clean close (`WM_CLOSE`) to the focused window.

Your selections are saved automatically in `config.ini` next to `CopilotToAntigravity.exe`.

### Temporarily Pause Remapping
1. Click the **^** arrow near the Windows clock (bottom-right taskbar).
2. Right-click the **Antigravity** tray icon.
3. Uncheck **Enabled** (tooltip changes to `Copilot -> Antigravity (Paused)`). Check it again to resume.

> Double-clicking the tray icon opens/focuses the selected target (or opens the quick picker popup when **Ask Every Time** is active).

### Stop Completely (Until Next Sign-In)
- **System Tray (recommended):** Right-click the tray icon → click **Exit**.
- **Task Manager:** `Ctrl + Shift + Esc` → **CopilotToAntigravity** → right-click → **End task**.
- **PowerShell:**
  ```powershell
  Stop-Process -Name CopilotToAntigravity -Force
  ```

### Disable Auto-Start on Login
- **Task Manager** (`Ctrl + Shift + Esc`) → **Startup apps** → right-click **CopilotToAntigravity** → **Disable**, **or**
- Delete `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\CopilotToAntigravity.lnk`.

---

## Uninstall

1. Stop the running process and remove the Startup shortcut in PowerShell:
   ```powershell
   Stop-Process -Name CopilotToAntigravity -Force -ErrorAction SilentlyContinue
   Remove-Item "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\CopilotToAntigravity.lnk" -Force -ErrorAction SilentlyContinue
   ```
2. Delete the cloned/downloaded project folder.

---

## How It Works (Technical Details)

### 1. Hardware Copilot Key Chord
On Windows 11 PCs, the dedicated Copilot key emits a 3-key shortcut chord:

```text
Left Win (VK_LWIN = 0x5B) + Left Shift (VK_LSHIFT = 0xA0) + F23 (VK_F23 = 0x86)
```

Windows' built-in *Settings → Personalization → Text input → Customize Copilot key* only allows Microsoft Store / MSIX apps registered as Copilot key providers, so standard `.exe` apps like Antigravity cannot be selected natively.

### 2. Low-Level Keyboard Hook (`WH_KEYBOARD_LL`)
1. `CopilotToAntigravity.exe` installs a global user-mode low-level keyboard hook via `SetWindowsHookEx(WH_KEYBOARD_LL, ...)` in `user32.dll`.
2. The hook ignores synthetic/injected events (`LLKHF_INJECTED`) and tracks `LWin`/`RWin` and `LShift`/`RShift` states (with `GetAsyncKeyState` fallback).
3. When `VK_F23` is pressed while `Win` and `Shift` are held:
   - **Suppresses Start Menu:** Injects an unassigned mask virtual key (`0xE8`) down + up via `SendInput` so Windows marks the Win-key chord as consumed and will not open the Start Menu when `LWin` is released.
   - **Suppresses Windows Copilot:** Returns `(IntPtr)1` on both `F23` key-down and key-up so Windows Shell never receives `Win + Shift + F23`.
   - **Debounces & Dispatches Async:** Enforces a 300 ms cooldown and posts `ToggleAntigravity()` to the UI thread via `Control.BeginInvoke` so the keyboard hook never blocks.

### 3. Antigravity GUI & CLI Path Auto-Detection
Both `CopilotToAntigravity.exe` (at runtime) and `Build-And-Install.ps1` (at setup time) automatically locate `Antigravity.exe` (GUI) and `agy.exe` (CLI) without hardcoded user paths:
- **GUI (`Antigravity.exe`):** Checks standard user/system install folders (`%LOCALAPPDATA%\Programs\antigravity`, `%ProgramFiles%\antigravity`, `%ProgramFiles(x86)%\antigravity`), running `Antigravity` processes, `App Paths\Antigravity.exe`, `Uninstall` registry entries, and `PATH`.
- **CLI (`agy.exe`):** Checks standard CLI install folders (`%LOCALAPPDATA%\agy\bin`, `%LOCALAPPDATA%\Programs\agy\bin`, `%USERPROFILE%\.agy\bin`, etc.), running `agy` processes, `App Paths\agy.exe`, and `PATH`. When launched, `agy.exe` starts in your `%USERPROFILE%` directory.

If either executable is moved or installed later, the helper re-runs auto-detection automatically the next time you press the Copilot key. If it still cannot be found, it shows a tray balloon notification instead of failing silently.

### 4. Window Toggle, Hide-to-Tray & Close Logic
GUI and CLI windows are tracked independently so both can run and be hidden/restored without interfering:
1. **Target is focused:** Resolves the foreground root window (`GetAncestor(..., GA_ROOT)`):
   - **Minimize to Tray mode (`FocusedAction=MinimizeToTray`, default):** Records the window handle in the corresponding hidden list and hides it via `ShowWindow(rootHwnd, SW_HIDE)`, removing it from the Windows taskbar and Alt+Tab while keeping it running in the background.
   - **Close App mode (`FocusedAction=Close`):** Sends `WM_CLOSE` via `PostMessage(rootHwnd, WM_CLOSE, ...)` so the window closes cleanly.
2. **Target is hidden, minimized, or in the background:** Checks the hidden window list first (then last toggled window, then `EnumWindows`), unhides via `ShowWindow(hWnd, SW_SHOW)` (and `SW_RESTORE` if minimized), and brings it to the foreground (`AllowSetForegroundWindow`, `AttachThreadInput`, `BringWindowToTop`, `SetForegroundWindow`).
3. **Target is not running:** Launches the auto-detected `Antigravity.exe` or `agy.exe`.
4. **Safety on Exit:** Clicking **Exit** in the tray menu automatically unhides any hidden GUI or CLI windows (`SW_SHOW`) before exiting so no window is ever left stranded invisible.

### 5. C# Compiler Detection (`Build-And-Install.ps1`)
When you run `Build-And-Install.bat` (or `Build-And-Install.ps1`), it checks for the built-in .NET Framework 4.x C# compiler (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, 32-bit fallback, or `csc.exe` in `PATH`):
- **If found:** Stops any running helper, compiles `CopilotToAntigravity.cs`, updates the Startup shortcut, and launches the new binary.
- **If missing:** Prompts you (`Y/N`) to install **Microsoft .NET Framework 4.8 Developer Pack** automatically via `winget` (or opens Microsoft's official download page if `winget` is unavailable).

### 6. Single Instance & System Tray
- Enforces a single instance via `Local\CopilotToAntigravity_SingleInstance_Mutex`.
- Displays the Antigravity icon in the system tray (falling back to the default system icon if Antigravity is not yet installed).

---

## Known Limitations & Notes

- **Elevated (Administrator) Windows:** Because the helper runs as a standard user process, Windows UIPI blocks non-admin keyboard hooks while an elevated window (such as Task Manager or an Administrator terminal) has focus.
- **Windows Defender:** Locally compiled unsigned executables with keyboard hooks can occasionally trigger antivirus heuristics. If Windows Security flags `CopilotToAntigravity.exe`, you can add an exclusion for the folder containing it, or use a signed alternative (such as AutoHotkey v2 or PowerToys).
