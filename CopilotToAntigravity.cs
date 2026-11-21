using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CopilotToAntigravity
{
    internal enum FocusedActionMode
    {
        MinimizeToTray = 0,
        CloseApp = 1
    }

    internal enum LaunchTargetMode
    {
        Gui = 0,
        Cli = 1,
        AskEveryTime = 2
    }

    internal enum SelectedTarget
    {
        None = 0,
        Gui = 1,
        Cli = 2
    }

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

    internal sealed class TargetChoiceForm : Form
    {
        private readonly Button _guiButton;
        private readonly Button _cliButton;
        private readonly CheckBox _rememberCheckBox;
        private readonly Action<SelectedTarget, bool> _onCompleted;
        private bool _completed;

        public TargetChoiceForm(string guiStatus, string cliStatus, Icon appIcon, Action<SelectedTarget, bool> onCompleted)
        {
            _onCompleted = onCompleted;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            ClientSize = new Size(420, 214);
            BackColor = Color.FromArgb(24, 24, 28);
            ForeColor = Color.FromArgb(240, 240, 245);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            if (appIcon != null)
            {
                Icon = appIcon;
            }

            Panel borderPanel = new Panel();
            borderPanel.Dock = DockStyle.Fill;
            borderPanel.BackColor = Color.FromArgb(24, 24, 28);
            borderPanel.Padding = new Padding(18, 16, 18, 14);
            borderPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Color.FromArgb(68, 70, 82), 1f))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, borderPanel.Width - 1, borderPanel.Height - 1);
                }
            };
            Controls.Add(borderPanel);

            Label titleLabel = new Label();
            titleLabel.Text = "Antigravity - Choose Target";
            titleLabel.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            titleLabel.ForeColor = Color.White;
            titleLabel.AutoSize = false;
            titleLabel.Location = new Point(18, 14);
            titleLabel.Size = new Size(384, 26);
            borderPanel.Controls.Add(titleLabel);

            Label subtitleLabel = new Label();
            subtitleLabel.Text = "Press 1 / G for GUI, 2 / C for CLI, Esc to cancel";
            subtitleLabel.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            subtitleLabel.ForeColor = Color.FromArgb(165, 170, 185);
            subtitleLabel.AutoSize = false;
            subtitleLabel.Location = new Point(18, 42);
            subtitleLabel.Size = new Size(384, 20);
            borderPanel.Controls.Add(subtitleLabel);

            _guiButton = CreateTargetButton(
                "[1] Antigravity GUI",
                guiStatus,
                new Point(18, 72),
                new Size(184, 86));
            _guiButton.Click += delegate(object sender, EventArgs e)
            {
                CompleteSelection(SelectedTarget.Gui);
            };
            borderPanel.Controls.Add(_guiButton);

            _cliButton = CreateTargetButton(
                "[2] Antigravity CLI (agy)",
                cliStatus,
                new Point(218, 72),
                new Size(184, 86));
            _cliButton.Click += delegate(object sender, EventArgs e)
            {
                CompleteSelection(SelectedTarget.Cli);
            };
            borderPanel.Controls.Add(_cliButton);

            _rememberCheckBox = new CheckBox();
            _rememberCheckBox.Text = "Remember choice (change anytime from tray icon)";
            _rememberCheckBox.AutoSize = true;
            _rememberCheckBox.Location = new Point(18, 174);
            _rememberCheckBox.ForeColor = Color.FromArgb(200, 204, 216);
            _rememberCheckBox.BackColor = Color.FromArgb(24, 24, 28);
            borderPanel.Controls.Add(_rememberCheckBox);

            Shown += delegate(object sender, EventArgs e)
            {
                _guiButton.Focus();
            };

            Deactivate += delegate(object sender, EventArgs e)
            {
                CompleteSelection(SelectedTarget.None);
            };
        }

        private static Button CreateTargetButton(string title, string status, Point location, Size size)
        {
            Button btn = new Button();
            btn.Location = location;
            btn.Size = size;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(72, 76, 92);
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(46, 50, 64);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(56, 62, 80);
            btn.BackColor = Color.FromArgb(34, 36, 44);
            btn.ForeColor = Color.White;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btn.Text = title + Environment.NewLine + Environment.NewLine + status;
            btn.Cursor = Cursors.Hand;
            return btn;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1 || e.KeyCode == Keys.G)
            {
                e.Handled = true;
                CompleteSelection(SelectedTarget.Gui);
                return;
            }

            if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2 || e.KeyCode == Keys.C)
            {
                e.Handled = true;
                CompleteSelection(SelectedTarget.Cli);
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                CompleteSelection(SelectedTarget.None);
                return;
            }

            base.OnKeyDown(e);
        }

        public void DismissPopup()
        {
            CompleteSelection(SelectedTarget.None);
        }

        private void CompleteSelection(SelectedTarget selection)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            bool remember = _rememberCheckBox.Checked;
            Close();

            if (_onCompleted != null)
            {
                _onCompleted(selection, remember);
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
        private const uint WM_CLOSE = 0x0010;

        private const uint LLKHF_INJECTED = 0x10;

        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_F23 = 0x86;
        private const ushort VK_MASK = 0xE8; // Unassigned virtual key to suppress Start Menu on LWin release

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;
        private const uint GA_ROOT = 2;

        private readonly string _configPath;
        private string _antigravityGuiPath;
        private string _antigravityCliPath;
        private readonly Control _invoker;
        private readonly NotifyIcon _trayIcon;
        private readonly ToolStripMenuItem _enabledMenuItem;
        private readonly ToolStripMenuItem _targetGuiMenuItem;
        private readonly ToolStripMenuItem _targetCliMenuItem;
        private readonly ToolStripMenuItem _targetAskMenuItem;
        private readonly ToolStripMenuItem _modeMinimizeMenuItem;
        private readonly ToolStripMenuItem _modeCloseMenuItem;
        private readonly LowLevelKeyboardProc _hookProc;
        private readonly Stopwatch _triggerTimer;
        private readonly List<IntPtr> _hiddenGuiWindows = new List<IntPtr>();
        private readonly List<IntPtr> _hiddenCliWindows = new List<IntPtr>();
        private readonly System.Windows.Forms.Timer _captureCliTimer;

        private IntPtr _hookId = IntPtr.Zero;
        private IntPtr _lastToggledGuiHwnd = IntPtr.Zero;
        private IntPtr _lastToggledCliHwnd = IntPtr.Zero;
        private TargetChoiceForm _pickerForm;
        private int _captureCliTicksRemaining;
        private bool _enabled = true;
        private LaunchTargetMode _targetMode = LaunchTargetMode.Gui;
        private FocusedActionMode _focusedAction = FocusedActionMode.MinimizeToTray;
        private bool _lWinDown;
        private bool _rWinDown;
        private bool _lShiftDown;
        private bool _rShiftDown;
        private bool _f23Down;
        private long _lastTriggerMs = -1000;

        public CopilotTrayContext()
        {
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            LoadConfig(_configPath, out _targetMode, out _focusedAction);
            _antigravityGuiPath = ResolveAntigravityGuiPath();
            _antigravityCliPath = ResolveAntigravityCliPath();
            _triggerTimer = Stopwatch.StartNew();

            _invoker = new Control();
            IntPtr forceHandle = _invoker.Handle;
            GC.KeepAlive(forceHandle);

            _captureCliTimer = new System.Windows.Forms.Timer();
            _captureCliTimer.Interval = 100;
            _captureCliTimer.Tick += OnCaptureCliTimerTick;

            _enabledMenuItem = new ToolStripMenuItem("Enabled");
            _enabledMenuItem.Checked = true;
            _enabledMenuItem.CheckOnClick = true;
            _enabledMenuItem.CheckedChanged += OnEnabledChanged;

            _targetGuiMenuItem = new ToolStripMenuItem("Target: Antigravity GUI");
            _targetGuiMenuItem.Click += OnSelectTargetGui;

            _targetCliMenuItem = new ToolStripMenuItem("Target: Antigravity CLI (agy)");
            _targetCliMenuItem.Click += OnSelectTargetCli;

            _targetAskMenuItem = new ToolStripMenuItem("Target: Ask Every Time (Popup)");
            _targetAskMenuItem.Click += OnSelectTargetAsk;

            _modeMinimizeMenuItem = new ToolStripMenuItem("When Focused: Minimize to Tray (Hide)");
            _modeMinimizeMenuItem.Click += OnSelectMinimizeMode;

            _modeCloseMenuItem = new ToolStripMenuItem("When Focused: Close App");
            _modeCloseMenuItem.Click += OnSelectCloseMode;

            UpdateMenuChecks();

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open / Show Antigravity GUI", null, OnOpenGuiClicked);
            menu.Items.Add("Open / Show Antigravity CLI (agy)", null, OnOpenCliClicked);
            menu.Items.Add(_enabledMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_targetGuiMenuItem);
            menu.Items.Add(_targetCliMenuItem);
            menu.Items.Add(_targetAskMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_modeMinimizeMenuItem);
            menu.Items.Add(_modeCloseMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, OnExitClicked);

            _trayIcon = new NotifyIcon();
            _trayIcon.Icon = LoadTrayIcon(_antigravityGuiPath, _antigravityCliPath);
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += OnTrayDoubleClick;
            UpdateTrayTooltip();
            _trayIcon.Visible = true;

            _hookProc = HookCallback;
            _hookId = InstallHook(_hookProc);
        }

        private static void LoadConfig(string configPath, out LaunchTargetMode targetMode, out FocusedActionMode focusedAction)
        {
            targetMode = LaunchTargetMode.Gui;
            focusedAction = FocusedActionMode.MinimizeToTray;

            try
            {
                if (File.Exists(configPath))
                {
                    string[] lines = File.ReadAllLines(configPath);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                        {
                            continue;
                        }

                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;

                        string key = line.Substring(0, eq).Trim();
                        string val = line.Substring(eq + 1).Trim();

                        if (key.Equals("TargetMode", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("Target", StringComparison.OrdinalIgnoreCase))
                        {
                            if (val.Equals("CLI", StringComparison.OrdinalIgnoreCase) ||
                                val.Equals("Agy", StringComparison.OrdinalIgnoreCase))
                            {
                                targetMode = LaunchTargetMode.Cli;
                            }
                            else if (val.Equals("Ask", StringComparison.OrdinalIgnoreCase) ||
                                     val.Equals("AskEveryTime", StringComparison.OrdinalIgnoreCase) ||
                                     val.Equals("Popup", StringComparison.OrdinalIgnoreCase))
                            {
                                targetMode = LaunchTargetMode.AskEveryTime;
                            }
                            else
                            {
                                targetMode = LaunchTargetMode.Gui;
                            }
                        }
                        else if (key.Equals("FocusedAction", StringComparison.OrdinalIgnoreCase))
                        {
                            if (val.Equals("Close", StringComparison.OrdinalIgnoreCase) ||
                                val.Equals("CloseApp", StringComparison.OrdinalIgnoreCase))
                            {
                                focusedAction = FocusedActionMode.CloseApp;
                            }
                            else
                            {
                                focusedAction = FocusedActionMode.MinimizeToTray;
                            }
                        }
                    }
                }
                else
                {
                    SaveConfig(configPath, targetMode, focusedAction);
                }
            }
            catch
            {
                // Fall back to defaults
            }
        }

        private static void SaveConfig(string configPath, LaunchTargetMode targetMode, FocusedActionMode focusedAction)
        {
            try
            {
                string targetValue = "GUI";
                if (targetMode == LaunchTargetMode.Cli)
                {
                    targetValue = "CLI";
                }
                else if (targetMode == LaunchTargetMode.AskEveryTime)
                {
                    targetValue = "Ask";
                }

                string actionValue = (focusedAction == FocusedActionMode.CloseApp) ? "Close" : "MinimizeToTray";
                string content =
                    "# What to open/toggle when pressing the Copilot key" + Environment.NewLine +
                    "# Options: GUI | CLI | Ask" + Environment.NewLine +
                    "TargetMode=" + targetValue + Environment.NewLine +
                    Environment.NewLine +
                    "# Action when pressing the Copilot key while Antigravity is already focused" + Environment.NewLine +
                    "# Options: MinimizeToTray | Close" + Environment.NewLine +
                    "FocusedAction=" + actionValue + Environment.NewLine;
                File.WriteAllText(configPath, content);
            }
            catch
            {
                // Ignore config write failures (e.g. read-only directory)
            }
        }

        private void UpdateMenuChecks()
        {
            _targetGuiMenuItem.Checked = (_targetMode == LaunchTargetMode.Gui);
            _targetCliMenuItem.Checked = (_targetMode == LaunchTargetMode.Cli);
            _targetAskMenuItem.Checked = (_targetMode == LaunchTargetMode.AskEveryTime);
            _modeMinimizeMenuItem.Checked = (_focusedAction == FocusedActionMode.MinimizeToTray);
            _modeCloseMenuItem.Checked = (_focusedAction == FocusedActionMode.CloseApp);
        }

        private void UpdateTrayTooltip()
        {
            if (!_enabled)
            {
                _trayIcon.Text = "Copilot -> Antigravity (Paused)";
                return;
            }

            string targetLabel = "GUI";
            if (_targetMode == LaunchTargetMode.Cli)
            {
                targetLabel = "CLI";
            }
            else if (_targetMode == LaunchTargetMode.AskEveryTime)
            {
                targetLabel = "Ask";
            }

            string actionLabel = (_focusedAction == FocusedActionMode.CloseApp) ? "Close" : "Hide";
            _trayIcon.Text = "Copilot -> Antigravity [" + targetLabel + " | " + actionLabel + "]";
        }

        private static string ResolveAntigravityGuiPath()
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
            string runningPath = FindRunningProcessPath("Antigravity");
            if (!string.IsNullOrEmpty(runningPath)) return runningPath;

            // 3. Check Windows App Paths registry keys
            string appPath = FindInAppPaths(Registry.CurrentUser, "Antigravity.exe");
            if (!string.IsNullOrEmpty(appPath)) return appPath;

            appPath = FindInAppPaths(Registry.LocalMachine, "Antigravity.exe");
            if (!string.IsNullOrEmpty(appPath)) return appPath;

            // 4. Check Windows Uninstall registry keys
            string uninstallPath = FindInUninstallRegistry(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (!string.IsNullOrEmpty(uninstallPath)) return uninstallPath;

            uninstallPath = FindInUninstallRegistry(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (!string.IsNullOrEmpty(uninstallPath)) return uninstallPath;

            uninstallPath = FindInUninstallRegistry(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
            if (!string.IsNullOrEmpty(uninstallPath)) return uninstallPath;

            // 5. Check directories listed in PATH
            return FindInPathEnv("Antigravity.exe");
        }

        private static string ResolveAntigravityCliPath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            string[] candidates = new string[]
            {
                Path.Combine(localAppData, "agy", "bin", "agy.exe"),
                Path.Combine(localAppData, "Programs", "agy", "bin", "agy.exe"),
                Path.Combine(localAppData, "Programs", "agy", "agy.exe"),
                Path.Combine(localAppData, "Programs", "antigravity", "bin", "agy.exe"),
                Path.Combine(localAppData, "Programs", "Antigravity", "bin", "agy.exe"),
                Path.Combine(userProfile, ".agy", "bin", "agy.exe"),
                Path.Combine(programFiles, "agy", "bin", "agy.exe"),
                Path.Combine(programFiles, "agy", "agy.exe"),
                Path.Combine(programFilesX86, "agy", "bin", "agy.exe")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (!string.IsNullOrEmpty(candidates[i]) && File.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            string runningPath = FindRunningProcessPath("agy");
            if (!string.IsNullOrEmpty(runningPath)) return runningPath;

            string appPath = FindInAppPaths(Registry.CurrentUser, "agy.exe");
            if (!string.IsNullOrEmpty(appPath)) return appPath;

            appPath = FindInAppPaths(Registry.LocalMachine, "agy.exe");
            if (!string.IsNullOrEmpty(appPath)) return appPath;

            return FindInPathEnv("agy.exe");
        }

        private static string FindRunningProcessPath(string processName)
        {
            Process[] running = Process.GetProcessesByName(processName);
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

            return null;
        }

        private static string FindInPathEnv(string exeName)
        {
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
                        string exe = Path.Combine(dir, exeName);
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

        private static string FindInAppPaths(RegistryKey root, string exeName)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\" + exeName))
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

        private static Icon LoadTrayIcon(string guiPath, string cliPath)
        {
            string[] candidates = new string[] { guiPath, cliPath };
            for (int i = 0; i < candidates.Length; i++)
            {
                try
                {
                    if (!string.IsNullOrEmpty(candidates[i]) && File.Exists(candidates[i]))
                    {
                        Icon extracted = Icon.ExtractAssociatedIcon(candidates[i]);
                        if (extracted != null)
                        {
                            return extracted;
                        }
                    }
                }
                catch
                {
                    // Try next candidate
                }
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
                                    _invoker.BeginInvoke(new MethodInvoker(HandleCopilotTrigger));
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

        private void HandleCopilotTrigger()
        {
            try
            {
                if (_pickerForm != null && !_pickerForm.IsDisposed && _pickerForm.Visible)
                {
                    _pickerForm.DismissPopup();
                    return;
                }

                if (_targetMode == LaunchTargetMode.AskEveryTime)
                {
                    ShowTargetPickerPopup();
                    return;
                }

                IntPtr fgRoot = GetForegroundRootWindow();
                if (_targetMode == LaunchTargetMode.Cli)
                {
                    ToggleCli(fgRoot);
                }
                else
                {
                    ToggleGui(fgRoot);
                }
            }
            catch
            {
                // Ignore errors to keep the background hook alive
            }
        }

        private void ShowTargetPickerPopup()
        {
            if (_pickerForm != null && !_pickerForm.IsDisposed && _pickerForm.Visible)
            {
                _pickerForm.DismissPopup();
                return;
            }

            IntPtr previousFgRoot = GetForegroundRootWindow();
            string guiStatus = GetGuiStatusText(previousFgRoot);
            string cliStatus = GetCliStatusText(previousFgRoot);

            _pickerForm = new TargetChoiceForm(
                guiStatus,
                cliStatus,
                _trayIcon.Icon,
                delegate(SelectedTarget selection, bool remember)
                {
                    _pickerForm = null;

                    if (selection == SelectedTarget.None)
                    {
                        return;
                    }

                    if (remember)
                    {
                        _targetMode = (selection == SelectedTarget.Cli) ? LaunchTargetMode.Cli : LaunchTargetMode.Gui;
                        SaveConfig(_configPath, _targetMode, _focusedAction);
                        UpdateMenuChecks();
                        UpdateTrayTooltip();
                    }

                    if (selection == SelectedTarget.Cli)
                    {
                        ToggleCli(previousFgRoot);
                    }
                    else
                    {
                        ToggleGui(previousFgRoot);
                    }
                });

            _pickerForm.Show();
            FocusWindow(_pickerForm.Handle);
        }

        private string GetGuiStatusText(IntPtr fgRoot)
        {
            HashSet<uint> guiPids = GetGuiPids();
            PruneHiddenGuiWindows(guiPids);

            if (guiPids.Count == 0)
            {
                return "Not running - Launch";
            }

            if (fgRoot != IntPtr.Zero && IsWindow(fgRoot))
            {
                uint fgPid;
                GetWindowThreadProcessId(fgRoot, out fgPid);
                if (guiPids.Contains(fgPid) && IsWindowVisible(fgRoot) && !IsIconic(fgRoot) && !_hiddenGuiWindows.Contains(fgRoot))
                {
                    return (_focusedAction == FocusedActionMode.CloseApp) ? "Active - Close" : "Active - Hide to Tray";
                }
            }

            return "Running - Show / Focus";
        }

        private string GetCliStatusText(IntPtr fgRoot)
        {
            HashSet<uint> cliPids = GetCliPids();
            HashSet<uint> guiPids = GetGuiPids();
            PruneHiddenCliWindows(cliPids);

            if (cliPids.Count == 0)
            {
                return "Not running - Launch";
            }

            if (fgRoot != IntPtr.Zero && IsCliWindow(fgRoot, cliPids, guiPids))
            {
                if (IsWindowVisible(fgRoot) && !IsIconic(fgRoot) && !_hiddenCliWindows.Contains(fgRoot))
                {
                    return (_focusedAction == FocusedActionMode.CloseApp) ? "Active - Close" : "Active - Hide to Tray";
                }
            }

            return "Running - Show / Focus";
        }

        private static IntPtr GetForegroundRootWindow()
        {
            IntPtr fgHwnd = GetForegroundWindow();
            if (fgHwnd == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            IntPtr rootHwnd = GetAncestor(fgHwnd, GA_ROOT);
            return (rootHwnd != IntPtr.Zero) ? rootHwnd : fgHwnd;
        }

        private void ToggleGui(IntPtr fgRoot)
        {
            try
            {
                HashSet<uint> targetPids = GetGuiPids();
                PruneHiddenGuiWindows(targetPids);

                if (targetPids.Count > 0)
                {
                    if (fgRoot != IntPtr.Zero && IsWindow(fgRoot))
                    {
                        uint fgPid;
                        GetWindowThreadProcessId(fgRoot, out fgPid);
                        if (targetPids.Contains(fgPid))
                        {
                            if (IsWindowVisible(fgRoot) && !IsIconic(fgRoot) && !_hiddenGuiWindows.Contains(fgRoot))
                            {
                                if (_focusedAction == FocusedActionMode.CloseApp)
                                {
                                    _hiddenGuiWindows.Remove(fgRoot);
                                    if (_lastToggledGuiHwnd == fgRoot)
                                    {
                                        _lastToggledGuiHwnd = IntPtr.Zero;
                                    }
                                    PostMessage(fgRoot, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                                }
                                else
                                {
                                    HideGuiWindowToTray(fgRoot);
                                }
                                return;
                            }
                        }
                    }

                    IntPtr existingHwnd = FindGuiWindow(targetPids);
                    if (existingHwnd != IntPtr.Zero)
                    {
                        _lastToggledGuiHwnd = existingHwnd;
                        _hiddenGuiWindows.Remove(existingHwnd);
                        if (FocusWindow(existingHwnd))
                        {
                            return;
                        }
                    }
                }

                LaunchAntigravityGui();
            }
            catch
            {
                // Ignore errors to keep the background hook alive
            }
        }

        private void OpenOrFocusGui()
        {
            try
            {
                HashSet<uint> targetPids = GetGuiPids();
                PruneHiddenGuiWindows(targetPids);

                IntPtr existingHwnd = FindGuiWindow(targetPids);
                if (existingHwnd != IntPtr.Zero)
                {
                    _lastToggledGuiHwnd = existingHwnd;
                    _hiddenGuiWindows.Remove(existingHwnd);
                    if (FocusWindow(existingHwnd))
                    {
                        return;
                    }
                }

                LaunchAntigravityGui();
            }
            catch
            {
                // Ignore launch errors to keep the background hook alive
            }
        }

        private void ToggleCli(IntPtr fgRoot)
        {
            try
            {
                HashSet<uint> cliPids = GetCliPids();
                HashSet<uint> guiPids = GetGuiPids();
                PruneHiddenCliWindows(cliPids);

                if (cliPids.Count > 0)
                {
                    if (fgRoot != IntPtr.Zero && IsCliWindow(fgRoot, cliPids, guiPids))
                    {
                        if (IsWindowVisible(fgRoot) && !IsIconic(fgRoot) && !_hiddenCliWindows.Contains(fgRoot))
                        {
                            if (_focusedAction == FocusedActionMode.CloseApp)
                            {
                                _hiddenCliWindows.Remove(fgRoot);
                                if (_lastToggledCliHwnd == fgRoot)
                                {
                                    _lastToggledCliHwnd = IntPtr.Zero;
                                }
                                PostMessage(fgRoot, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            }
                            else
                            {
                                HideCliWindowToTray(fgRoot);
                            }
                            return;
                        }
                    }

                    IntPtr existingHwnd = FindCliWindow(cliPids, guiPids);
                    if (existingHwnd != IntPtr.Zero)
                    {
                        _lastToggledCliHwnd = existingHwnd;
                        _hiddenCliWindows.Remove(existingHwnd);
                        if (FocusWindow(existingHwnd))
                        {
                            return;
                        }
                    }
                }

                LaunchAntigravityCli();
            }
            catch
            {
                // Ignore errors to keep the background hook alive
            }
        }

        private void OpenOrFocusCli()
        {
            try
            {
                HashSet<uint> cliPids = GetCliPids();
                HashSet<uint> guiPids = GetGuiPids();
                PruneHiddenCliWindows(cliPids);

                if (cliPids.Count > 0)
                {
                    IntPtr existingHwnd = FindCliWindow(cliPids, guiPids);
                    if (existingHwnd != IntPtr.Zero)
                    {
                        _lastToggledCliHwnd = existingHwnd;
                        _hiddenCliWindows.Remove(existingHwnd);
                        if (FocusWindow(existingHwnd))
                        {
                            return;
                        }
                    }
                }

                LaunchAntigravityCli();
            }
            catch
            {
                // Ignore launch errors to keep the background hook alive
            }
        }

        private void HideGuiWindowToTray(IntPtr rootHwnd)
        {
            _lastToggledGuiHwnd = rootHwnd;
            if (!_hiddenGuiWindows.Contains(rootHwnd))
            {
                _hiddenGuiWindows.Add(rootHwnd);
            }
            ShowWindow(rootHwnd, SW_HIDE);
        }

        private void HideCliWindowToTray(IntPtr rootHwnd)
        {
            _lastToggledCliHwnd = rootHwnd;
            if (!_hiddenCliWindows.Contains(rootHwnd))
            {
                _hiddenCliWindows.Add(rootHwnd);
            }
            ShowWindow(rootHwnd, SW_HIDE);
        }

        private void PruneHiddenGuiWindows(HashSet<uint> activePids)
        {
            for (int i = _hiddenGuiWindows.Count - 1; i >= 0; i--)
            {
                IntPtr h = _hiddenGuiWindows[i];
                if (h == IntPtr.Zero || !IsWindow(h))
                {
                    _hiddenGuiWindows.RemoveAt(i);
                    continue;
                }

                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (!activePids.Contains(pid))
                {
                    _hiddenGuiWindows.RemoveAt(i);
                }
            }
        }

        private void PruneHiddenCliWindows(HashSet<uint> cliPids)
        {
            if (cliPids.Count == 0)
            {
                // Restore any hidden terminal windows before clearing so they aren't stranded invisible if agy exited
                for (int i = 0; i < _hiddenCliWindows.Count; i++)
                {
                    IntPtr h = _hiddenCliWindows[i];
                    if (h != IntPtr.Zero && IsWindow(h))
                    {
                        ShowWindow(h, SW_SHOW);
                    }
                }
                _hiddenCliWindows.Clear();
                _lastToggledCliHwnd = IntPtr.Zero;
                return;
            }

            for (int i = _hiddenCliWindows.Count - 1; i >= 0; i--)
            {
                IntPtr h = _hiddenCliWindows[i];
                if (h == IntPtr.Zero || !IsWindow(h))
                {
                    _hiddenCliWindows.RemoveAt(i);
                }
            }

            if (_lastToggledCliHwnd != IntPtr.Zero && !IsWindow(_lastToggledCliHwnd))
            {
                _lastToggledCliHwnd = IntPtr.Zero;
            }
        }

        private void RestoreAllHiddenWindows()
        {
            for (int i = 0; i < _hiddenGuiWindows.Count; i++)
            {
                IntPtr h = _hiddenGuiWindows[i];
                if (h != IntPtr.Zero && IsWindow(h))
                {
                    ShowWindow(h, SW_SHOW);
                }
            }
            _hiddenGuiWindows.Clear();

            for (int i = 0; i < _hiddenCliWindows.Count; i++)
            {
                IntPtr h = _hiddenCliWindows[i];
                if (h != IntPtr.Zero && IsWindow(h))
                {
                    ShowWindow(h, SW_SHOW);
                }
            }
            _hiddenCliWindows.Clear();
        }

        private void LaunchAntigravityGui()
        {
            if (string.IsNullOrEmpty(_antigravityGuiPath) || !File.Exists(_antigravityGuiPath))
            {
                _antigravityGuiPath = ResolveAntigravityGuiPath();
                if (!string.IsNullOrEmpty(_antigravityGuiPath) && File.Exists(_antigravityGuiPath))
                {
                    _trayIcon.Icon = LoadTrayIcon(_antigravityGuiPath, _antigravityCliPath);
                }
            }

            if (!string.IsNullOrEmpty(_antigravityGuiPath) && File.Exists(_antigravityGuiPath))
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = _antigravityGuiPath;
                psi.WorkingDirectory = Path.GetDirectoryName(_antigravityGuiPath);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            else
            {
                _trayIcon.ShowBalloonTip(
                    4000,
                    "Antigravity GUI Not Found",
                    "Could not auto-detect Antigravity.exe. Please make sure Antigravity is installed.",
                    ToolTipIcon.Warning);
            }
        }

        private void LaunchAntigravityCli()
        {
            if (string.IsNullOrEmpty(_antigravityCliPath) || !File.Exists(_antigravityCliPath))
            {
                _antigravityCliPath = ResolveAntigravityCliPath();
                if (!string.IsNullOrEmpty(_antigravityCliPath) && File.Exists(_antigravityCliPath))
                {
                    _trayIcon.Icon = LoadTrayIcon(_antigravityGuiPath, _antigravityCliPath);
                }
            }

            if (!string.IsNullOrEmpty(_antigravityCliPath) && File.Exists(_antigravityCliPath))
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string workingDir = (!string.IsNullOrEmpty(userProfile) && Directory.Exists(userProfile))
                    ? userProfile
                    : Path.GetDirectoryName(_antigravityCliPath);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = _antigravityCliPath;
                psi.WorkingDirectory = workingDir;
                psi.UseShellExecute = true;
                Process.Start(psi);

                _captureCliTicksRemaining = 25;
                _captureCliTimer.Start();
            }
            else
            {
                _trayIcon.ShowBalloonTip(
                    4000,
                    "Antigravity CLI Not Found",
                    "Could not auto-detect agy.exe. Please make sure Antigravity CLI is installed.",
                    ToolTipIcon.Warning);
            }
        }

        private void OnCaptureCliTimerTick(object sender, EventArgs e)
        {
            _captureCliTicksRemaining--;
            if (_captureCliTicksRemaining <= 0)
            {
                _captureCliTimer.Stop();
                return;
            }

            HashSet<uint> cliPids = GetCliPids();
            if (cliPids.Count == 0)
            {
                return;
            }

            IntPtr fgRoot = GetForegroundRootWindow();
            if (fgRoot == IntPtr.Zero || !IsWindow(fgRoot))
            {
                return;
            }

            uint pid;
            GetWindowThreadProcessId(fgRoot, out pid);
            if (cliPids.Contains(pid) || IsTerminalHostProcess(pid))
            {
                _lastToggledCliHwnd = fgRoot;
                _captureCliTimer.Stop();
            }
        }

        private static HashSet<uint> GetGuiPids()
        {
            return GetPidsByProcessName("Antigravity");
        }

        private static HashSet<uint> GetCliPids()
        {
            return GetPidsByProcessName("agy");
        }

        private static HashSet<uint> GetPidsByProcessName(string processName)
        {
            HashSet<uint> targetPids = new HashSet<uint>();
            Process[] procs = Process.GetProcessesByName(processName);
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

        private static string GetProcessNameSafe(uint pid)
        {
            if (pid == 0)
            {
                return string.Empty;
            }

            try
            {
                using (Process p = Process.GetProcessById((int)pid))
                {
                    return p.ProcessName ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsTerminalHostProcess(uint pid)
        {
            string name = GetProcessNameSafe(pid);
            return name.Equals("WindowsTerminal", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("OpenConsole", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("conhost", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAnyTerminalProcess(uint pid)
        {
            string name = GetProcessNameSafe(pid);
            return name.Equals("WindowsTerminal", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("OpenConsole", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("conhost", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("cmd", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("powershell", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("pwsh", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetWindowTitleText(IntPtr hWnd)
        {
            int len = GetWindowTextLength(hWnd);
            if (len <= 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private bool IsCliWindow(IntPtr hWnd, HashSet<uint> cliPids, HashSet<uint> guiPids)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
            {
                return false;
            }

            if (_hiddenCliWindows.Contains(hWnd) ||
                (_lastToggledCliHwnd != IntPtr.Zero && hWnd == _lastToggledCliHwnd))
            {
                return true;
            }

            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (cliPids.Contains(pid))
            {
                return true;
            }

            if (guiPids.Contains(pid))
            {
                return false;
            }

            string title = GetWindowTitleText(hWnd);
            if (!string.IsNullOrEmpty(title) &&
                (title.IndexOf("agy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 title.IndexOf("Antigravity CLI", StringComparison.OrdinalIgnoreCase) >= 0) &&
                IsAnyTerminalProcess(pid))
            {
                return true;
            }

            return false;
        }

        private IntPtr FindGuiWindow(HashSet<uint> targetPids)
        {
            if (targetPids.Count == 0)
            {
                return IntPtr.Zero;
            }

            // 1. Prefer the most recently hidden window (if any)
            for (int i = _hiddenGuiWindows.Count - 1; i >= 0; i--)
            {
                IntPtr h = _hiddenGuiWindows[i];
                if (h != IntPtr.Zero && IsWindow(h))
                {
                    uint pid;
                    GetWindowThreadProcessId(h, out pid);
                    if (targetPids.Contains(pid))
                    {
                        return h;
                    }
                }
            }

            // 2. Prefer the last toggled visible window
            if (_lastToggledGuiHwnd != IntPtr.Zero && IsWindow(_lastToggledGuiHwnd) && IsWindowVisible(_lastToggledGuiHwnd))
            {
                uint lastPid;
                GetWindowThreadProcessId(_lastToggledGuiHwnd, out lastPid);
                if (targetPids.Contains(lastPid) && GetWindowTextLength(_lastToggledGuiHwnd) > 0)
                {
                    return _lastToggledGuiHwnd;
                }
            }

            // 3. Otherwise find any visible Antigravity top-level window
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

        private IntPtr FindCliWindow(HashSet<uint> cliPids, HashSet<uint> guiPids)
        {
            if (cliPids.Count == 0)
            {
                return IntPtr.Zero;
            }

            // 1. Prefer the most recently hidden CLI window (if any)
            for (int i = _hiddenCliWindows.Count - 1; i >= 0; i--)
            {
                IntPtr h = _hiddenCliWindows[i];
                if (h != IntPtr.Zero && IsWindow(h))
                {
                    return h;
                }
            }

            // 2. Prefer the last toggled visible CLI window
            if (_lastToggledCliHwnd != IntPtr.Zero && IsWindow(_lastToggledCliHwnd) && IsWindowVisible(_lastToggledCliHwnd))
            {
                return _lastToggledCliHwnd;
            }

            // 3. Otherwise search visible top-level windows matching agy / terminal host
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (!IsWindowVisible(hWnd) || GetWindowTextLength(hWnd) <= 0)
                {
                    return true;
                }

                if (IsCliWindow(hWnd, cliPids, guiPids))
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
            // SW_SHOW unhides a hidden window while preserving maximized/normal state
            ShowWindow(targetHwnd, SW_SHOW);
            if (IsIconic(targetHwnd))
            {
                ShowWindow(targetHwnd, SW_RESTORE);
            }

            IntPtr fgHwnd = GetForegroundWindow();
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
                SetActiveWindow(targetHwnd);
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

            return GetForegroundWindow() == targetHwnd || IsWindowVisible(targetHwnd);
        }

        private void OnEnabledChanged(object sender, EventArgs e)
        {
            _enabled = _enabledMenuItem.Checked;
            _f23Down = false;
            UpdateTrayTooltip();
        }

        private void OnSelectTargetGui(object sender, EventArgs e)
        {
            _targetMode = LaunchTargetMode.Gui;
            SaveConfig(_configPath, _targetMode, _focusedAction);
            UpdateMenuChecks();
            UpdateTrayTooltip();
        }

        private void OnSelectTargetCli(object sender, EventArgs e)
        {
            _targetMode = LaunchTargetMode.Cli;
            SaveConfig(_configPath, _targetMode, _focusedAction);
            UpdateMenuChecks();
            UpdateTrayTooltip();
        }

        private void OnSelectTargetAsk(object sender, EventArgs e)
        {
            _targetMode = LaunchTargetMode.AskEveryTime;
            SaveConfig(_configPath, _targetMode, _focusedAction);
            UpdateMenuChecks();
            UpdateTrayTooltip();
        }

        private void OnSelectMinimizeMode(object sender, EventArgs e)
        {
            _focusedAction = FocusedActionMode.MinimizeToTray;
            SaveConfig(_configPath, _targetMode, _focusedAction);
            UpdateMenuChecks();
            UpdateTrayTooltip();
        }

        private void OnSelectCloseMode(object sender, EventArgs e)
        {
            _focusedAction = FocusedActionMode.CloseApp;
            SaveConfig(_configPath, _targetMode, _focusedAction);
            UpdateMenuChecks();
            UpdateTrayTooltip();
        }

        private void OnOpenGuiClicked(object sender, EventArgs e)
        {
            OpenOrFocusGui();
        }

        private void OnOpenCliClicked(object sender, EventArgs e)
        {
            OpenOrFocusCli();
        }

        private void OnTrayDoubleClick(object sender, EventArgs e)
        {
            if (_targetMode == LaunchTargetMode.AskEveryTime)
            {
                ShowTargetPickerPopup();
            }
            else if (_targetMode == LaunchTargetMode.Cli)
            {
                OpenOrFocusCli();
            }
            else
            {
                OpenOrFocusGui();
            }
        }

        private void OnExitClicked(object sender, EventArgs e)
        {
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            // Safety: restore any Antigravity window we hid so it is never stranded invisible
            RestoreAllHiddenWindows();

            if (_captureCliTimer != null)
            {
                _captureCliTimer.Stop();
                _captureCliTimer.Dispose();
            }

            if (_pickerForm != null && !_pickerForm.IsDisposed)
            {
                _pickerForm.Close();
                _pickerForm.Dispose();
                _pickerForm = null;
            }

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

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetActiveWindow(IntPtr hWnd);

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
