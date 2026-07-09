using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AnToanUSB
{
    public class PrivacyCleanupForm : Form
    {
        private CheckedListBox taskList;
        private ListView reportList;
        private Label summaryLabel;
        private readonly List<CleanupResult> results = new List<CleanupResult>();

        public PrivacyCleanupForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Dọn riêng tư an toàn";
            Size = new Size(960, 620);
            MinimumSize = new Size(880, 560);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Mist;
            Font = Theme.FontBody;
            try { Icon = Icon.FromHandle((IconExtractor.GetSystemIcon("imageres.dll", 78, false) as Bitmap).GetHicon()); } catch { }

            Panel header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Theme.Slate };
            Label title = new Label { Text = "Dọn riêng tư an toàn", Location = new Point(24, 18), AutoSize = true, Font = new Font("Segoe UI Semibold", 18, FontStyle.Bold), ForeColor = Theme.TextInv };
            Label subtitle = new Label
            {
                Text = "Dọn dữ liệu tạm, metadata rác và lịch sử local được phép xóa trên máy hiện tại.",
                Location = new Point(26, 54),
                AutoSize = true,
                ForeColor = Theme.TextInvMute,
                Font = Theme.FontBody
            };
            header.Controls.AddRange(new Control[] { title, subtitle });

            Panel left = new Panel { Dock = DockStyle.Left, Width = 330, Padding = new Padding(18), BackColor = Theme.White };
            Label taskTitle = new Label { Text = "Tác vụ", Dock = DockStyle.Top, Height = 28, Font = Theme.FontHeading, ForeColor = Theme.TextDark };
            taskList = new CheckedListBox { Dock = DockStyle.Top, Height = 260, CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle };
            taskList.Items.Add("Dọn file tạm của ứng dụng", true);
            taskList.Items.Add("Dọn metadata rác trên USB", true);
            taskList.Items.Add("Dọn shortcut Recent local", true);
            taskList.Items.Add("Dọn thư mục BaoMat sau khi đã đưa vào két", false);
            taskList.Items.Add("Dọn lịch sử USB Registry local (Admin)", false);
            taskList.Items.Add("Dọn NetworkProfile Event Log local (Admin)", false);

            Button btnRun = new Button { Text = "Chạy dọn dẹp", Dock = DockStyle.Top, Height = 42, BackColor = Theme.Teal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            btnRun.FlatAppearance.BorderSize = 0;
            btnRun.Click += (s, e) => RunCleanup();

            Button btnExport = new Button { Text = "Xuất báo cáo", Dock = DockStyle.Top, Height = 38, BackColor = Theme.Slate2, ForeColor = Theme.TextInv, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.Click += (s, e) => ExportReport();

            Label note = new Label
            {
                Text = "Ghi chú: tính năng này chỉ dọn dữ liệu local mà Windows cho phép. Báo cáo sẽ ghi rõ mục thành công, bỏ qua hoặc lỗi.",
                Dock = DockStyle.Fill,
                ForeColor = Theme.TextMute,
                Font = Theme.FontSmall
            };

            left.Controls.Add(note);
            left.Controls.Add(btnExport);
            left.Controls.Add(new Panel { Height = 10, Dock = DockStyle.Top });
            left.Controls.Add(btnRun);
            left.Controls.Add(new Panel { Height = 12, Dock = DockStyle.Top });
            left.Controls.Add(taskList);
            left.Controls.Add(taskTitle);

            Panel main = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = Theme.Mist };
            summaryLabel = new Label { Text = "Chưa chạy dọn dẹp.", Dock = DockStyle.Top, Height = 30, Font = Theme.FontHeading, ForeColor = Theme.TextDark };
            reportList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, BorderStyle = BorderStyle.None };
            reportList.Columns.Add("Thời gian", 145);
            reportList.Columns.Add("Tác vụ", 260);
            reportList.Columns.Add("Trạng thái", 110);
            reportList.Columns.Add("Chi tiết", 360);
            main.Controls.Add(reportList);
            main.Controls.Add(summaryLabel);

            Controls.Add(main);
            Controls.Add(left);
            Controls.Add(header);
        }

        private void RunCleanup()
        {
            results.Clear();
            reportList.Items.Clear();

            RunSelected(0, "Dọn file tạm của ứng dụng", CleanupAppTemp);
            RunSelected(1, "Dọn metadata rác trên USB", CleanupUsbMetadata);
            RunSelected(2, "Dọn shortcut Recent local", CleanupRecentShortcuts);
            RunSelected(3, "Dọn thư mục BaoMat sau khi đã đưa vào két", CleanupBaoMat);
            RunSelected(4, "Dọn lịch sử USB Registry local", CleanupUsbRegistry);
            RunSelected(5, "Dọn NetworkProfile Event Log local", CleanupNetworkProfileLog);

            int ok = 0, skipped = 0, error = 0;
            foreach (var item in results)
            {
                if (item.Status == "OK") ok++;
                else if (item.Status == "Bỏ qua") skipped++;
                else error++;
            }
            summaryLabel.Text = string.Format("Hoàn tất: {0} OK, {1} bỏ qua, {2} lỗi.", ok, skipped, error);
        }

        private void RunSelected(int index, string name, Func<CleanupResult> action)
        {
            if (!taskList.GetItemChecked(index))
            {
                AddResult(new CleanupResult(name, "Bỏ qua", "Người dùng không chọn tác vụ."));
                return;
            }

            try
            {
                AddResult(action());
            }
            catch (Exception ex)
            {
                AddResult(new CleanupResult(name, "Lỗi", ex.Message));
            }
        }

        private void AddResult(CleanupResult result)
        {
            results.Add(result);
            var item = new ListViewItem(new[] { DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), result.Task, result.Status, result.Detail });
            if (result.Status == "OK") item.ForeColor = Color.FromArgb(16, 124, 16);
            else if (result.Status == "Bỏ qua") item.ForeColor = Color.Gray;
            else item.ForeColor = Color.DarkRed;
            reportList.Items.Add(item);
        }

        private CleanupResult CleanupAppTemp()
        {
            int count = 0;
            string[] dirs =
            {
                Path.Combine(Path.GetTempPath(), "K3"),
                Path.Combine(Path.GetTempPath(), "K3USB"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp")
            };

            foreach (string dir in dirs)
                count += DeleteDirectoryContents(dir);

            return new CleanupResult("Dọn file tạm của ứng dụng", "OK", "Đã xử lý " + count + " mục tạm.");
        }

        private CleanupResult CleanupUsbMetadata()
        {
            int count = 0;
            string root = AppDomain.CurrentDomain.BaseDirectory;
            count += DeleteFiles(root, "._*", true, false);
            count += DeleteFiles(root, ".DS_Store", true, false);
            count += DeleteFiles(root, "Thumbs.db", true, false);
            count += DeleteFiles(root, "desktop.ini", true, false);
            return new CleanupResult("Dọn metadata rác trên USB", "OK", "Đã xóa " + count + " file metadata rác.");
        }

        private CleanupResult CleanupRecentShortcuts()
        {
            string recent = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            int count = DeleteFiles(recent, "*", false, false);
            return new CleanupResult("Dọn shortcut Recent local", "OK", "Đã xóa " + count + " shortcut trong Recent của người dùng hiện tại.");
        }

        private CleanupResult CleanupBaoMat()
        {
            string baoMat = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BaoMat");
            if (!Directory.Exists(baoMat)) return new CleanupResult("Dọn thư mục BaoMat sau khi đã đưa vào két", "Bỏ qua", "Không tìm thấy BaoMat.");

            int count = 0;
            foreach (string file in Directory.GetFiles(baoMat, "*", SearchOption.AllDirectories))
            {
                try
                {
                    CryptoEngine.SecureShredFile(file);
                    count++;
                }
                catch { }
            }
            return new CleanupResult("Dọn thư mục BaoMat sau khi đã đưa vào két", "OK", "Đã xóa an toàn " + count + " file còn lại trong BaoMat.");
        }

        private CleanupResult CleanupUsbRegistry()
        {
            if (!IsAdministrator()) return new CleanupResult("Dọn lịch sử USB Registry local", "Bỏ qua", "Cần chạy ứng dụng bằng quyền Administrator.");

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum", true))
            {
                if (key == null) return new CleanupResult("Dọn lịch sử USB Registry local", "Bỏ qua", "Không mở được HKLM Enum.");
                TryDeleteSubKeyTree(key, "USBSTOR");
                TryDeleteSubKeyTree(key, "USB");
            }
            return new CleanupResult("Dọn lịch sử USB Registry local", "OK", "Đã gửi lệnh xóa USBSTOR và USB trong Registry local.");
        }

        private CleanupResult CleanupNetworkProfileLog()
        {
            if (!IsAdministrator()) return new CleanupResult("Dọn NetworkProfile Event Log local", "Bỏ qua", "Cần chạy ứng dụng bằng quyền Administrator.");

            EventLogSession.GlobalSession.ClearLog("Microsoft-Windows-NetworkProfile/Operational");
            return new CleanupResult("Dọn NetworkProfile Event Log local", "OK", "Đã xóa NetworkProfile/Operational trên máy hiện tại.");
        }

        private int DeleteDirectoryContents(string dir)
        {
            if (!Directory.Exists(dir)) return 0;
            int count = 0;

            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(file); count++; } catch { }
            }

            foreach (string subDir in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories))
            {
                try { Directory.Delete(subDir, true); count++; } catch { }
            }

            return count;
        }

        private int DeleteFiles(string dir, string pattern, bool recursive, bool secure)
        {
            if (!Directory.Exists(dir)) return 0;
            int count = 0;
            SearchOption option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (string file in Directory.GetFiles(dir, pattern, option))
            {
                try
                {
                    if (secure) CryptoEngine.SecureShredFile(file);
                    else File.Delete(file);
                    count++;
                }
                catch { }
            }
            return count;
        }

        private void TryDeleteSubKeyTree(RegistryKey parent, string name)
        {
            try
            {
                if (parent != null && Array.IndexOf(parent.GetSubKeyNames(), name) >= 0)
                    parent.DeleteSubKeyTree(name, false);
            }
            catch { }
        }

        private bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void ExportReport()
        {
            if (results.Count == 0)
            {
                MessageBox.Show("Chưa có báo cáo để xuất.", "Xuất báo cáo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog
            {
                FileName = "K3_PrivacyCleanup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv",
                Filter = "CSV UTF-8 (*.csv)|*.csv|Text file (*.txt)|*.txt",
                Title = "Xuất báo cáo dọn riêng tư"
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Thời gian,Tác vụ,Trạng thái,Chi tiết");
                foreach (CleanupResult result in results)
                {
                    sb.AppendLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\"",
                        EscapeCsv(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")),
                        EscapeCsv(result.Task),
                        EscapeCsv(result.Status),
                        EscapeCsv(result.Detail)));
                }
                File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
                MessageBox.Show("Đã xuất báo cáo.", "Xuất báo cáo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private string EscapeCsv(string value)
        {
            return (value ?? "").Replace("\"", "\"\"");
        }

        private class CleanupResult
        {
            public string Task;
            public string Status;
            public string Detail;

            public CleanupResult(string task, string status, string detail)
            {
                Task = task;
                Status = status;
                Detail = detail;
            }
        }
    }
}
