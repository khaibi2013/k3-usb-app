using System;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AnToanUSB
{
    public class UsbRescueForm : Form
    {
        private ListView lvReport;
        private Label lblSummary;
        private string usbRoot;

        public UsbRescueForm()
        {
            usbRoot = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Cứu hộ USB";
            Size = new Size(940, 620);
            MinimumSize = new Size(860, 560);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Mist;
            Font = Theme.FontBody;
            try { Icon = Icon.FromHandle((IconExtractor.GetSystemIcon("imageres.dll", 78, false) as Bitmap).GetHicon()); } catch { }

            Panel header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Theme.Ink };
            Label title = new Label { Text = "Cứu hộ USB", Location = new Point(24, 18), AutoSize = true, Font = new Font("Segoe UI Semibold", 18, FontStyle.Bold), ForeColor = Theme.TextInv };
            Label sub = new Label { Text = "Phục hồi thư mục bị ẩn, xóa shortcut giả, khóa Autorun và quét nhanh USB.", Location = new Point(26, 54), AutoSize = true, ForeColor = Theme.TextInvMute, Font = Theme.FontBody };
            header.Controls.AddRange(new Control[] { title, sub });

            Panel actions = new Panel { Dock = DockStyle.Top, Height = 86, Padding = new Padding(18), BackColor = Theme.White };
            Button btnRecover = MakeButton("Phục hồi file ẩn", 18, 20, Theme.Teal);
            Button btnAutorun = MakeButton("Khóa Autorun", 170, 20, Theme.Slate2);
            Button btnScan = MakeButton("Quét nhanh USB", 322, 20, Color.FromArgb(16, 124, 16));
            Button btnAll = MakeButton("Chạy tất cả", 474, 20, Color.FromArgb(185, 91, 0));
            lblSummary = new Label { Text = "USB: " + usbRoot + " | Engine: " + AntivirusScanner.GetEngineInfo(), Location = new Point(18, 58), AutoSize = true, ForeColor = Theme.TextMute, Font = Theme.FontSmall };
            actions.Controls.AddRange(new Control[] { btnRecover, btnAutorun, btnScan, btnAll, lblSummary });

            lvReport = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, BorderStyle = BorderStyle.None, BackColor = Theme.White };
            lvReport.Columns.Add("Thời gian", 145);
            lvReport.Columns.Add("Tác vụ", 180);
            lvReport.Columns.Add("Trạng thái", 110);
            lvReport.Columns.Add("Chi tiết", 460);

            btnRecover.Click += (s, e) => RecoverHiddenAndShortcuts();
            btnAutorun.Click += (s, e) => LockAutorun();
            btnScan.Click += (s, e) => QuickScanUsb();
            btnAll.Click += (s, e) => {
                RecoverHiddenAndShortcuts();
                LockAutorun();
                QuickScanUsb();
            };

            Controls.Add(lvReport);
            Controls.Add(actions);
            Controls.Add(header);
        }

        private Button MakeButton(string text, int x, int y, Color color)
        {
            Button btn = new Button { Text = text, Location = new Point(x, y), Width = 140, Height = 34, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void RecoverHiddenAndShortcuts()
        {
            if (string.IsNullOrEmpty(usbRoot) || !Directory.Exists(usbRoot))
            {
                AddReport("Phục hồi file ẩn", "Lỗi", "Không xác định được USB root.");
                return;
            }

            int restored = 0;
            int deletedShortcuts = 0;
            foreach (string dir in EnumerateDirectoriesSafe(usbRoot))
            {
                try
                {
                    FileAttributes attr = File.GetAttributes(dir);
                    if ((attr & FileAttributes.Hidden) == FileAttributes.Hidden || (attr & FileAttributes.System) == FileAttributes.System)
                    {
                        File.SetAttributes(dir, attr & ~FileAttributes.Hidden & ~FileAttributes.System);
                        restored++;
                    }
                }
                catch { }
            }

            foreach (string file in EnumerateFilesSafe(usbRoot, "*"))
            {
                try
                {
                    FileAttributes attr = File.GetAttributes(file);
                    if ((attr & FileAttributes.Hidden) == FileAttributes.Hidden || (attr & FileAttributes.System) == FileAttributes.System)
                    {
                        File.SetAttributes(file, attr & ~FileAttributes.Hidden & ~FileAttributes.System);
                        restored++;
                    }
                }
                catch { }
            }

            foreach (string shortcut in EnumerateFilesSafe(usbRoot, "*.lnk"))
            {
                try
                {
                    string folderName = Path.GetFileNameWithoutExtension(shortcut);
                    string folderPath = Path.Combine(Path.GetDirectoryName(shortcut), folderName);
                    if (Directory.Exists(folderPath))
                    {
                        File.Delete(shortcut);
                        deletedShortcuts++;
                    }
                }
                catch { }
            }

            AddReport("Phục hồi file ẩn", "OK", string.Format("Đã bỏ Hidden/System cho {0} mục; xóa {1} shortcut giả.", restored, deletedShortcuts));
        }

        private void LockAutorun()
        {
            if (!IsAdministrator())
            {
                AddReport("Khóa Autorun", "Bỏ qua", "Cần chạy ứng dụng bằng quyền Administrator để ghi HKLM.");
                return;
            }

            try
            {
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                {
                    key.SetValue("NoDriveTypeAutoRun", 0xFF, RegistryValueKind.DWord);
                    key.SetValue("NoAutorun", 1, RegistryValueKind.DWord);
                }

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                {
                    key.SetValue("NoDriveTypeAutoRun", 0xFF, RegistryValueKind.DWord);
                    key.SetValue("NoAutorun", 1, RegistryValueKind.DWord);
                }

                AddReport("Khóa Autorun", "OK", "Đã vô hiệu hóa Autorun/AutoPlay bằng Registry policy.");
            }
            catch (Exception ex)
            {
                AddReport("Khóa Autorun", "Lỗi", ex.Message);
            }
        }

        private void QuickScanUsb()
        {
            if (string.IsNullOrEmpty(usbRoot) || !Directory.Exists(usbRoot))
            {
                AddReport("Quét nhanh USB", "Lỗi", "Không xác định được USB root.");
                return;
            }

            int clean = 0;
            int infected = 0;
            int skipped = 0;
            foreach (string file in EnumerateFilesSafe(usbRoot, "*"))
            {
                if (ShouldSkip(file)) continue;
                ScanResult result = AntivirusScanner.ScanFileReal(file);
                if (result.Status == "Sạch") clean++;
                else if (result.Status == "Lỗi") skipped++;
                else
                {
                    infected++;
                    AddReport("Phát hiện mã độc", "Cảnh báo", result.VirusName + " | " + file);
                }
            }

            AddReport("Quét nhanh USB", infected == 0 ? "OK" : "Cảnh báo", string.Format("Sạch={0}, Nhiễm={1}, Bỏ qua={2}", clean, infected, skipped));
        }

        private bool ShouldSkip(string file)
        {
            string name = Path.GetFileName(file);
            if (name.Equals("AnToanUSB.exe", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.Equals(".vault_config.json", StringComparison.OrdinalIgnoreCase)) return true;
            string full = Path.GetFullPath(file);
            string baseDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            if (full.StartsWith(Path.Combine(baseDir, ".vault"), StringComparison.OrdinalIgnoreCase)) return true;
            if (full.StartsWith(Path.Combine(baseDir, ".vault_decoy"), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private string[] EnumerateFilesSafe(string root, string pattern)
        {
            try { return Directory.GetFiles(root, pattern, SearchOption.AllDirectories); }
            catch { return new string[0]; }
        }

        private string[] EnumerateDirectoriesSafe(string root)
        {
            try { return Directory.GetDirectories(root, "*", SearchOption.AllDirectories); }
            catch { return new string[0]; }
        }

        private void AddReport(string task, string status, string detail)
        {
            ListViewItem item = new ListViewItem(new[] { DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), task, status, detail });
            if (status == "OK") item.ForeColor = Color.FromArgb(16, 124, 16);
            else if (status == "Bỏ qua") item.ForeColor = Color.Gray;
            else if (status == "Cảnh báo") item.ForeColor = Color.DarkOrange;
            else item.ForeColor = Color.DarkRed;
            lvReport.Items.Add(item);
            item.EnsureVisible();
        }

        private bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
