using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CopilotToAntigravity
{
    internal static class Program
    {
        private const string MutexName = "Local\\CopilotToAntigravity_SingleInstance_Mutex";

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new CopilotTrayContext());
            }
        }
    }

    internal sealed class CopilotTrayContext : ApplicationContext
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const uint LLKHF_INJECTED = 0x10;

        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_F23 = 0x86;
        private const ushort VK_MASK = 0xE8; // Unassigned virtual key to suppress Start Menu on LWin release

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int SW_MINIMIZE = 6;
        private const uint GA_ROOT = 2;

        private string _antigravityPath;
        private readonly Control _invoker;
        private readonly NotifyIcon _trayIcon;
        private readonly ToolStripMenuItem _enabledMenuItem;
        private readonly LowLevelKeyboardProc _hookProc;
        private readonly Stopwatch _triggerTimer;

        private IntPtr _hookId = IntPtr.Zero;
        private IntPtr _lastToggledHwnd = IntPtr.Zero;
        private bool _enabled = true;
        private bool _lWinDown;
        private bool _rWinDown;
        private bool _lShiftDown;
        private bool _rShiftDown;
        private bool _f23Down;
        private long _lastTriggerMs = -1000;

        public CopilotTrayContext()
        {
            _antigravityPath = ResolveAntigravityPath();
            _triggerTimer = Stopwatch.StartNew();

            _invoker = new Control();
            IntPtr forceHandle = _invoker.Handle;
            GC.KeepAlive(forceHandle);

            _enabledMenuItem = new ToolStripMenuItem("Enabled");
            _enabledMenuItem.Checked = true;
            _enabledMenuItem.CheckOnClick = true;
            _enabledMenuItem.CheckedChanged += OnEnabledChanged;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open Antigravity", null, OnOpenClicked);
            menu.Items.Add(_enabledMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, OnExitClicked);

            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "Copilot Key -> Antigravity";
            _trayIcon.Icon = LoadTrayIcon(_antigravityPath);
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += OnOpenClicked;
            _trayIcon.Visible = true;

            _hookProc = HookCallback;
            _hookId = InstallHook(_hookProc);
        }

        private static string ResolveAntigravityPath()
        {
            // 1. Check standard installation paths
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            string[] candidates = new string[]
            {
                Path.Combine(localAppData, "Programs", "antigravity", "Antigravity.exe"),
                Path.Combine(localAppData, "Programs", "Antigravity", "Antigravity.exe"),
                Path.Combine(programFiles, "antigravity", "Antigravity.exe"),
                Path.Combine(programFiles, "Antigravity", "Antigravity.exe"),
                Path.Combine(programFilesX86, "antigravity", "Antigravity.exe"),
                Path.Combine(programFilesX86, "Antigravity", "Antigravity.exe")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (!string.IsNullOrEmpty(candidates[i]) && File.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            // 2. Check if Antigravity is currently running
            Process[] running = Process.GetProcessesByName("Antigravity");
            try
            {
                for (int i = 0; i < running.Length; i++)
                {
                    try
                    {
                        if (running[i].MainModule != null && File.Exists(running[i].MainModule.FileName))
                        {
                            return running[i].MainModule.FileName;
                        }
                    }
                    catch
                    {
                        // Ignore inaccessible processes
                    }
                }
            }
            finally
            {
                for (int i = 0; i < running.Length; i++)
                {
                    running[i].Dispose();
                }
            }

            // 3. Check Windows App Paths registry keys
            string appPath = FindInAppPaths(Registry.CurrentUser);
            if (!string.IsNullOrEmpty(appPath)) return appPath;

            appPath = FindInAppPaths(Registry.LocalMachine);
            if (!string.IsNullOrEmpty(appPath)) return appPath;

            // 4. Check Windows Uninstall registry keys
            string uninstallPath = FindInUninstallRegistry(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (!string.IsNullOrEmpty(uninstallPath)) return uninstallPath;

            uninstallPath = FindInUninstallRegistry(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (!string.IsNullOrEmpty(uninstallPath)) return uninstallPath;

            uninstallPath = FindInUninstallRegistry(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
            if (!string.IsNullOrEmpty(uninstallPath)) return uninstallPath;

            // 5. Check directories listed in PATH
            string envPath = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(envPath))
            {
                string[] dirs = envPath.Split(';');
                for (int i = 0; i < dirs.Length; i++)
                {
                    string dir = dirs[i].Trim().Trim('"');
                    if (string.IsNullOrEmpty(dir)) continue;

                    try
                    {
                        string exe = Path.Combine(dir, "Antigravity.exe");
                        if (File.Exists(exe))
                        {
                            return exe;
                        }
                    }
                    catch
                    {
                        // Ignore malformed PATH entries
                    }
                }
            }

            return null;
        }

        private static string FindInAppPaths(RegistryKey root)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\Antigravity.exe"))
                {
                    if (key != null)
                    {
                        string val = key.GetValue(null) as string;
                        string cleaned = CleanRegistryExePath(val);
                        if (!string.IsNullOrEmpty(cleaned) && File.Exists(cleaned))
                        {
                            return cleaned;
                        }
                    }
                }
            }
            catch
            {
                // Ignore registry read failures
            }

            return null;
        }

        private static string FindInUninstallRegistry(RegistryKey root, string subKeyPath)
        {
            try
            {
                using (RegistryKey uninstallKey = root.OpenSubKey(subKeyPath))
                {
                    if (uninstallKey == null) return null;

                    string[] subKeys = uninstallKey.GetSubKeyNames();
                    for (int i = 0; i < subKeys.Length; i++)
                    {
                        try
                        {
                            using (RegistryKey appKey = uninstallKey.OpenSubKey(subKeys[i]))
                            {
                                if (appKey == null) continue;

                                string displayName = appKey.GetValue("DisplayName") as string;
                                if (string.IsNullOrEmpty(displayName) ||
                                    displayName.IndexOf("Antigravity", StringComparison.OrdinalIgnoreCase) < 0)
                                {
                                    continue;
                                }

                                string installLocation = appKey.GetValue("InstallLocation") as string;
                                if (!string.IsNullOrEmpty(installLocation))
                                {
                                    string exe = Path.Combine(installLocation.Trim().Trim('"'), "Antigravity.exe");
                                    if (File.Exists(exe)) return exe;
                                }

                                string displayIcon = CleanRegistryExePath(appKey.GetValue("DisplayIcon") as string);
                                if (!string.IsNullOrEmpty(displayIcon) && File.Exists(displayIcon))
                                {
                                    if (Path.GetFileName(displayIcon).Equals("Antigravity.exe", StringComparison.OrdinalIgnoreCase))
                                    {
                                        return displayIcon;
                                    }
                                    string sibling = Path.Combine(Path.GetDirectoryName(displayIcon), "Antigravity.exe");
                                    if (File.Exists(sibling)) return sibling;
                                }

                                string uninstallStr = CleanRegistryExePath(appKey.GetValue("UninstallString") as string);
                                if (!string.IsNullOrEmpty(uninstallStr))
                                {
                                    string dir = Path.GetDirectoryName(uninstallStr);
                                    if (!string.IsNullOrEmpty(dir))
                                    {
                                        string exe = Path.Combine(dir, "Antigravity.exe");
                                        if (File.Exists(exe)) return exe;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Ignore individual key read errors
                        }
                    }
                }
            }
            catch
            {
                // Ignore registry read failures
            }

            return null;
        }

        private static string CleanRegistryExePath(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;

            string s = raw.Trim();
            if (s.StartsWith("\""))
            {
                int endQuote = s.IndexOf('"', 1);
                if (endQuote > 1)
                {
                    s = s.Substring(1, endQuote - 1);
                }
            }
            else
            {
                int commaIdx = s.IndexOf(',');
                if (commaIdx > 0)
                {
                    s = s.Substring(0, commaIdx);
                }
            }

            return s.Trim();
        }

        private static Icon LoadTrayIcon(string exePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    Icon extracted = Icon.ExtractAssociatedIcon(exePath);
                    if (extracted != null)
                    {
                        return extracted;
                    }
                }
            }
            catch
            {
                // Fall back to default system icon
            }

            return SystemIcons.Application;
        }

        private static IntPtr InstallHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                IntPtr moduleHandle = curModule != null ? GetModuleHandle(curModule.ModuleName) : IntPtr.Zero;
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, moduleHandle, 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                KBDLLHOOKSTRUCT kbd = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

                // Ignore synthetic keystrokes (such as our own mask key)
                if ((kbd.flags & LLKHF_INJECTED) == 0)
                {
                    int msg = wParam.ToInt32();
                    bool isDown = (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN);
                    bool isUp = (msg == WM_KEYUP || msg == WM_SYSKEYUP);
                    int vk = (int)kbd.vkCode;

                    if (vk == VK_LWIN)
                    {
                        if (isDown) _lWinDown = true;
                        else if (isUp) _lWinDown = false;
                    }
                    else if (vk == VK_RWIN)
                    {
                        if (isDown) _rWinDown = true;
                        else if (isUp) _rWinDown = false;
                    }
                    else if (vk == VK_LSHIFT)
                    {
                        if (isDown) _lShiftDown = true;
                        else if (isUp) _lShiftDown = false;
                    }
                    else if (vk == VK_RSHIFT)
                    {
                        if (isDown) _rShiftDown = true;
                        else if (isUp) _rShiftDown = false;
                    }
                    else if (vk == VK_F23 && _enabled)
                    {
                        bool winDown = _lWinDown || _rWinDown || IsKeyDown(VK_LWIN) || IsKeyDown(VK_RWIN);
                        bool shiftDown = _lShiftDown || _rShiftDown || IsKeyDown(VK_LSHIFT) || IsKeyDown(VK_RSHIFT);

                        if (isDown && winDown && shiftDown)
                        {
                            if (!_f23Down)
                            {
                                _f23Down = true;
                                SendMenuMaskKey();

                                long now = _triggerTimer.ElapsedMilliseconds;
                                if (now - _lastTriggerMs > 300)
                                {
                                    _lastTriggerMs = now;
                                    _invoker.BeginInvoke(new MethodInvoker(ToggleAntigravity));
                                }
                            }
                            return (IntPtr)1;
                        }

                        if (isUp && (_f23Down || (winDown && shiftDown)))
                        {
                            _f23Down = false;
                            return (IntPtr)1;
                        }
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private static bool IsKeyDown(int vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        private static void SendMenuMaskKey()
        {
            INPUT[] inputs = new INPUT[2];

            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = VK_MASK;
            inputs[0].U.ki.dwFlags = 0;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = VK_MASK;
            inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private void ToggleAntigravity()
        {
            try
            {
                HashSet<uint> targetPids = GetAntigravityPids();
                if (targetPids.Count > 0)
                {
                    IntPtr fgHwnd = GetForegroundWindow();
                    if (fgHwnd != IntPtr.Zero)
                    {
                        uint fgPid;
                        GetWindowThreadProcessId(fgHwnd, out fgPid);
                        if (targetPids.Contains(fgPid))
                        {
                            IntPtr rootHwnd = GetAncestor(fgHwnd, GA_ROOT);
                            if (rootHwnd == IntPtr.Zero)
                            {
                                rootHwnd = fgHwnd;
                            }

                            if (!IsIconic(rootHwnd))
                            {
                                _lastToggledHwnd = rootHwnd;
                                ShowWindow(rootHwnd, SW_MINIMIZE);
                                return;
                            }
                        }
                    }

                    IntPtr existingHwnd = FindAntigravityWindow(targetPids);
                    if (existingHwnd != IntPtr.Zero)
                    {
                        _lastToggledHwnd = existingHwnd;
                        if (FocusWindow(existingHwnd))
                        {
                            return;
                        }
                    }
                }

                LaunchAntigravity();
            }
            catch
            {
                // Ignore errors to keep the background hook alive
            }
        }

        private void OpenOrFocusAntigravity()
        {
            try
            {
                HashSet<uint> targetPids = GetAntigravityPids();
                IntPtr existingHwnd = FindAntigravityWindow(targetPids);
                if (existingHwnd != IntPtr.Zero)
                {
                    _lastToggledHwnd = existingHwnd;
                    if (FocusWindow(existingHwnd))
                    {
                        return;
                    }
                }

                LaunchAntigravity();
            }
            catch
            {
                // Ignore launch errors to keep the background hook alive
            }
        }

        private void LaunchAntigravity()
        {
            if (string.IsNullOrEmpty(_antigravityPath) || !File.Exists(_antigravityPath))
            {
                _antigravityPath = ResolveAntigravityPath();
                if (!string.IsNullOrEmpty(_antigravityPath) && File.Exists(_antigravityPath))
                {
                    _trayIcon.Icon = LoadTrayIcon(_antigravityPath);
                }
            }

            if (!string.IsNullOrEmpty(_antigravityPath) && File.Exists(_antigravityPath))
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = _antigravityPath;
                psi.WorkingDirectory = Path.GetDirectoryName(_antigravityPath);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            else
            {
                _trayIcon.ShowBalloonTip(
                    4000,
                    "Antigravity Not Found",
                    "Could not auto-detect Antigravity.exe. Please make sure Antigravity is installed.",
                    ToolTipIcon.Warning);
            }
        }

        private static HashSet<uint> GetAntigravityPids()
        {
            HashSet<uint> targetPids = new HashSet<uint>();
            Process[] procs = Process.GetProcessesByName("Antigravity");
            try
            {
                for (int i = 0; i < procs.Length; i++)
                {
                    targetPids.Add((uint)procs[i].Id);
                }
            }
            finally
            {
                for (int i = 0; i < procs.Length; i++)
                {
                    procs[i].Dispose();
                }
            }

            return targetPids;
        }

        private IntPtr FindAntigravityWindow(HashSet<uint> targetPids)
        {
            if (targetPids.Count == 0)
            {
                return IntPtr.Zero;
            }

            if (_lastToggledHwnd != IntPtr.Zero && IsWindow(_lastToggledHwnd) && IsWindowVisible(_lastToggledHwnd))
            {
                uint lastPid;
                GetWindowThreadProcessId(_lastToggledHwnd, out lastPid);
                if (targetPids.Contains(lastPid) && GetWindowTextLength(_lastToggledHwnd) > 0)
                {
                    return _lastToggledHwnd;
                }
            }

            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (!IsWindowVisible(hWnd))
                {
                    return true;
                }

                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (targetPids.Contains(pid) && GetWindowTextLength(hWnd) > 0)
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        private static bool FocusWindow(IntPtr targetHwnd)
        {
            if (IsIconic(targetHwnd))
            {
                ShowWindow(targetHwnd, SW_RESTORE);
            }
            else
            {
                ShowWindow(targetHwnd, SW_SHOW);
            }

            IntPtr fgHwnd = GetForegroundWindow();
            if (fgHwnd == targetHwnd)
            {
                return true;
            }

            uint fgPid;
            uint targetPid;
            uint fgThread = fgHwnd != IntPtr.Zero ? GetWindowThreadProcessId(fgHwnd, out fgPid) : 0;
            uint targetThread = GetWindowThreadProcessId(targetHwnd, out targetPid);
            uint currentThread = GetCurrentThreadId();

            AllowSetForegroundWindow(targetPid);

            bool attachedFg = false;
            bool attachedTarget = false;
            try
            {
                if (fgThread != 0 && fgThread != currentThread)
                {
                    attachedFg = AttachThreadInput(currentThread, fgThread, true);
                }
                if (targetThread != 0 && targetThread != currentThread && targetThread != fgThread)
                {
                    attachedTarget = AttachThreadInput(currentThread, targetThread, true);
                }

                BringWindowToTop(targetHwnd);
                SetForegroundWindow(targetHwnd);
            }
            finally
            {
                if (attachedTarget)
                {
                    AttachThreadInput(currentThread, targetThread, false);
                }
                if (attachedFg)
                {
                    AttachThreadInput(currentThread, fgThread, false);
                }
            }

            return GetForegroundWindow() == targetHwnd;
        }

        private void OnEnabledChanged(object sender, EventArgs e)
        {
            _enabled = _enabledMenuItem.Checked;
            _f23Down = false;
            _trayIcon.Text = _enabled
                ? "Copilot Key -> Antigravity"
                : "Copilot Key -> Antigravity (Paused)";
        }

        private void OnOpenClicked(object sender, EventArgs e)
        {
            OpenOrFocusAntigravity();
        }

        private void OnExitClicked(object sender, EventArgs e)
        {
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            if (_invoker != null)
            {
                _invoker.Dispose();
            }

            base.ExitThreadCore();
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllowSetForegroundWindow(uint dwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
    }
}
