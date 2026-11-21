# Antigravity Copilot Key Shortcut

A lightweight, zero-dependency Windows 11 system tray utility written in C# (.NET Framework 4.x) that remaps the physical **Copilot key** on your keyboard into a dedicated toggle button for **Antigravity**.

| Current State of Antigravity | Action When Pressing the Copilot Key |
| :--- | :--- |
| **Not running** | Auto-detects and launches `Antigravity.exe` |
| **Running in background or minimized** | Restores (if minimized) and brings Antigravity to the foreground |
| **Already open and focused** | Minimizes Antigravity and returns focus to the previous window |

Both Windows Copilot and the Windows Start Menu are suppressed when you press the key.

---

## Requirements

- **OS:** Windows 11 (64-bit) with a hardware Copilot key (or any keyboard emitting `Win + Shift + F23`)
- **Runtime / Compiler:** .NET Framework 4.x (included with Windows; the setup script checks for `csc.exe` and offers to install the .NET Framework 4.8 Developer Pack if missing)
- **App:** Antigravity desktop app (auto-detected at runtime — no manual path configuration required)

---

## Quick Start (Build & Install)

1. Clone or download this repository into any permanent folder where you want the utility to stay (the Windows Startup shortcut will point to the compiled executable in that folder).
2. Double-click **`Build-And-Install.bat`**, or run the PowerShell script from the repository folder:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\Build-And-Install.ps1
   ```

The setup script will:
- Detect your Antigravity installation and the .NET Framework C# compiler (`csc.exe`)
- Stop any running instance of the helper
- Compile `CopilotToAntigravity.exe`
- Add a shortcut (`CopilotToAntigravity.lnk`) to your Windows Startup folder (`shell:startup`)
- Launch the utility in the system tray

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
| `CopilotToAntigravity.cs` | C# source code for the keyboard hook, tray icon, toggle logic, and Antigravity path auto-detection |
| `Build-And-Install.ps1` | PowerShell setup script: checks Antigravity & C# compiler state (prompts to install compiler if missing), builds `CopilotToAntigravity.exe`, updates the Startup shortcut, and launches it |
| `Build-And-Install.bat` | Double-click launcher for `Build-And-Install.ps1` (bypasses PowerShell execution policy) |
| `README.md` | Project documentation |

### Generated / System Files
- **`CopilotToAntigravity.exe`** — Native Windows executable (~15 KB) compiled by `Build-And-Install.ps1` inside the project folder.
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

### Temporarily Pause Remapping
1. Click the **^** arrow near the Windows clock (bottom-right taskbar).
2. Right-click the **Antigravity** tray icon (`Copilot Key -> Antigravity`).
3. Uncheck **Enabled** (tooltip changes to `Copilot Key -> Antigravity (Paused)`). Check it again to resume.

> Double-clicking the tray icon opens or focuses Antigravity directly.

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

### 3. Antigravity Path Auto-Detection
Both `CopilotToAntigravity.exe` (at runtime) and `Build-And-Install.ps1` (at setup time) automatically locate `Antigravity.exe` without hardcoded user paths, checking in order:
1. Standard user and system install folders (`%LOCALAPPDATA%\Programs\antigravity`, `%ProgramFiles%\antigravity`, `%ProgramFiles(x86)%\antigravity`)
2. Currently running `Antigravity` process path (`MainModule.FileName`)
3. Windows Registry `App Paths\Antigravity.exe` (`HKCU` and `HKLM`)
4. Windows Registry `Uninstall` entries (`HKCU`, `HKLM`, `WOW6432Node`) matching `*Antigravity*` (`InstallLocation`, `DisplayIcon`, `UninstallString`)
5. Directories in the `PATH` environment variable

If Antigravity is moved or installed later, the helper re-runs auto-detection automatically the next time you press the Copilot key. If it still cannot be found, it shows a tray balloon notification instead of failing silently.

### 4. Window Toggle & Focus Logic
1. **Antigravity is focused:** Resolves the foreground root window (`GetAncestor(..., GA_ROOT)`), remembers it in `_lastToggledHwnd`, and minimizes it via `ShowWindow(rootHwnd, SW_MINIMIZE)`.
2. **Antigravity is minimized or in the background:** Prefers `_lastToggledHwnd` if still open, or locates the visible Antigravity window via `EnumWindows`, restores it (`SW_RESTORE`), and brings it to the foreground (`AllowSetForegroundWindow`, `AttachThreadInput`, `BringWindowToTop`, `SetForegroundWindow`).
3. **Antigravity is not running:** Launches the auto-detected `Antigravity.exe`.

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
