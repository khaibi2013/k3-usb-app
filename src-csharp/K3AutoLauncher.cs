using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AnToanUSB
{
    internal static class K3AutoLauncherProgram
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length > 0 && string.Equals(args[0], "/install", StringComparison.OrdinalIgnoreCase))
            {
                AutoLauncherInstaller.Install();
                MessageBox.Show("Đã cài K3 AutoLauncher. Từ lần sau khi cắm USB K3, phần mềm sẽ tự mở màn hình đăng nhập.", "K3 AutoLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (args.Length > 0 && string.Equals(args[0], "/uninstall", StringComparison.OrdinalIgnoreCase))
            {
                AutoLauncherInstaller.Uninstall();
                MessageBox.Show("Đã gỡ K3 AutoLauncher khỏi khởi động cùng Windows.", "K3 AutoLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (AutoLauncherContext context = new AutoLauncherContext())
                Application.Run(context);
        }
    }

    internal sealed class AutoLauncherContext : ApplicationContext
    {
        private readonly AutoLauncherWindow window;
        private readonly NotifyIcon tray;

        public AutoLauncherContext()
        {
            window = new AutoLauncherWindow();
            tray = new NotifyIcon
            {
                Icon = SystemIcons.Shield,
                Text = "K3 AutoLauncher",
                Visible = true,
                ContextMenuStrip = BuildMenu()
            };

            window.UsbArrived += (s, e) => ScanAndLaunch();
            ScanAndLaunch();
        }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem scan = new ToolStripMenuItem("Tìm USB K3 ngay");
            ToolStripMenuItem install = new ToolStripMenuItem("Cài tự khởi động");
            ToolStripMenuItem uninstall = new ToolStripMenuItem("Gỡ tự khởi động");
            ToolStripMenuItem exit = new ToolStripMenuItem("Thoát");

            scan.Click += (s, e) => ScanAndLaunch();
            install.Click += (s, e) => AutoLauncherInstaller.Install();
            uninstall.Click += (s, e) => AutoLauncherInstaller.Uninstall();
            exit.Click += (s, e) => ExitThread();

            menu.Items.Add(scan);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(install);
            menu.Items.Add(uninstall);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);
            return menu;
        }

        private void ScanAndLaunch()
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Removable) continue;

                    string exePath = FindK3App(drive.RootDirectory.FullName);
                    if (string.IsNullOrEmpty(exePath)) continue;
                    if (IsAlreadyRunningFromPath(exePath)) continue;

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(exePath)
                    });
                    return;
                }
                catch { }
            }
        }

        private string FindK3App(string root)
        {
            string[] candidates = new[]
            {
                Path.Combine(root, "AnToanUSB.exe"),
                Path.Combine(root, "USB-An-Toan-K3-portable", "AnToanUSB.exe")
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }
            return "";
        }

        private bool IsAlreadyRunningFromPath(string exePath)
        {
            string target = Path.GetFullPath(exePath);
            foreach (Process process in Process.GetProcessesByName("AnToanUSB"))
            {
                try
                {
                    string running = process.MainModule.FileName;
                    if (string.Equals(Path.GetFullPath(running), target, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch { }
            }
            return false;
        }

        protected override void ExitThreadCore()
        {
            tray.Visible = false;
            tray.Dispose();
            window.Dispose();
            base.ExitThreadCore();
        }
    }

    internal sealed class AutoLauncherWindow : Form
    {
        public event EventHandler UsbArrived;

        public AutoLauncherWindow()
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            Opacity = 0;
            Width = 1;
            Height = 1;
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_DEVICECHANGE = 0x0219;
            const int DBT_DEVICEARRIVAL = 0x8000;

            if (m.Msg == WM_DEVICECHANGE && m.WParam.ToInt32() == DBT_DEVICEARRIVAL)
            {
                Timer timer = new Timer { Interval = 1200 };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    EventHandler handler = UsbArrived;
                    if (handler != null) handler(this, EventArgs.Empty);
                };
                timer.Start();
            }

            base.WndProc(ref m);
        }
    }

    internal static class AutoLauncherInstaller
    {
        private const string RunName = "K3AutoLauncher";

        public static void Install()
        {
            string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "K3AutoLauncher");
            Directory.CreateDirectory(targetDir);

            string currentExe = Application.ExecutablePath;
            string targetExe = Path.Combine(targetDir, "K3AutoLauncher.exe");
            if (!string.Equals(currentExe, targetExe, StringComparison.OrdinalIgnoreCase))
                File.Copy(currentExe, targetExe, true);

            using (RegistryKey run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (run != null)
                    run.SetValue(RunName, "\"" + targetExe + "\"", RegistryValueKind.String);
            }
        }

        public static void Uninstall()
        {
            using (RegistryKey run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (run != null)
                    run.DeleteValue(RunName, false);
            }
        }
    }
}
