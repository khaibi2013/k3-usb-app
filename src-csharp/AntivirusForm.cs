using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;

namespace AnToanUSB
{
    public class AntivirusForm : Form
    {
        private CancellationTokenSource scanCts;
        private int cleanCount, infectedCount, skippedCount, totalCount;
        private string currentScanDir;
        private DateTime startTime;
        private ListView lvQuarantine;
        private ListView lvLog;
        private Label lblInfoEngine;
        private Label lblInfoQuarantine;
        private string initialScanPath;
        private string QuarantineDir { get { return QuarantineManager.QuarantineDir; } }
        public AntivirusForm()
        {
            InitializeComponent();
        }

        public AntivirusForm(string scanPath)
        {
            initialScanPath = scanPath;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Quét mã độc";
            this.Size = new Size(1100, 700);
            this.MinimumSize = new Size(960, 620);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(243, 244, 246); // Light Gray Background
            try { this.Icon = Icon.FromHandle((IconExtractor.GetSystemIcon("imageres.dll", 109, false) as Bitmap).GetHicon()); } catch { }

            // Top Toolbar Panel
            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Color.White };
            
            FlowLayoutPanel btnPanel = new FlowLayoutPanel { Location = new Point(10, 15), Width = 800, Height = 40 };
            
            Font btnFont = new Font("Segoe UI", 9, FontStyle.Bold);
            Color primaryBlue = Color.FromArgb(0, 120, 212); // Microsoft Blue
            
            Button btnSelectFolder = new Button { Text = "Chọn thư mục...", Width = 120, Height = 35, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = primaryBlue, Font = btnFont };
            btnSelectFolder.FlatAppearance.BorderSize = 0;
            Button btnSelectFile = new Button { Text = "Chọn tệp...", Width = 100, Height = 35, FlatStyle = FlatStyle.Flat, ForeColor = Color.DarkGray, BackColor = Color.WhiteSmoke, Font = new Font("Segoe UI", 9) };
            btnSelectFile.FlatAppearance.BorderSize = 0;
            Button btnFullScan = new Button { Text = "Quét toàn diện", Width = 120, Height = 35, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(16, 124, 16), Font = btnFont };
            btnFullScan.FlatAppearance.BorderSize = 0;
            Button btnStopScan = new Button { Text = "Dừng quét", Width = 100, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.MistyRose, ForeColor = Color.DarkRed, Font = btnFont };
            btnStopScan.FlatAppearance.BorderSize = 0;
            Button btnExport = new Button { Text = "Xuất báo cáo", Width = 110, Height = 35, FlatStyle = FlatStyle.Flat, ForeColor = Color.DarkGray, BackColor = Color.WhiteSmoke, Font = new Font("Segoe UI", 9) };
            btnExport.FlatAppearance.BorderSize = 0;

            btnPanel.Controls.AddRange(new Control[] { btnSelectFolder, btnSelectFile, btnFullScan, btnStopScan, btnExport });

            CheckBox chkAutoDelete = new CheckBox { Text = "Tự động xóa tệp nhiễm", Location = new Point(850, 10), AutoSize = true };
            CheckBox chkBackup = new CheckBox { Text = "Backup vào cách ly", Location = new Point(850, 30), AutoSize = true };

            topPanel.Controls.Add(btnPanel);
            topPanel.Controls.Add(chkAutoDelete);
            topPanel.Controls.Add(chkBackup);

            // Middle Status Panel
            Panel statusPanel = new Panel { Dock = DockStyle.Top, Height = 120, BackColor = Color.White };
            
            Label lblK3AV = new Label { Text = "K3-AV Engine", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = primaryBlue };
            Label lblPath = new Label { Text = @"Chưa chọn thư mục", Location = new Point(150, 18), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Regular), ForeColor = Color.DimGray };
            if (!string.IsNullOrEmpty(initialScanPath) && (File.Exists(initialScanPath) || Directory.Exists(initialScanPath)))
            {
                currentScanDir = initialScanPath;
                lblPath.Text = initialScanPath;
            }
            
            ComboBox cmbFilter = new ComboBox { Location = new Point(850, 15), Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10) };
            cmbFilter.Items.Add("Các tệp nghi nhiễm");
            cmbFilter.SelectedIndex = 0;

            ProgressBar progressBar = new ProgressBar { Location = new Point(20, 45), Width = 1010, Height = 8, Style = ProgressBarStyle.Continuous, Value = 0 };

            Label lblStatus1 = new Label { Text = "Sẵn sàng bảo vệ. Vui lòng chọn thư mục để quét mã độc.", Location = new Point(20, 60), AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            Label lblStatus2 = new Label { Text = @"...", Location = new Point(20, 80), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Regular), ForeColor = Color.Gray };
            
            Label lblStats = new Label { Text = "Sạch: 0 | Nhiễm: 0 | Bỏ qua: 0 | Tổng: 0 | Thời gian: 00:00:00", Location = new Point(20, 100), AutoSize = true, ForeColor = Color.FromArgb(16, 124, 16), Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            statusPanel.Controls.AddRange(new Control[] { lblK3AV, lblPath, cmbFilter, progressBar, lblStatus1, lblStatus2, lblStats });

            // Bottom TabControl
            TabControl tabControl = new TabControl { Dock = DockStyle.Fill, ItemSize = new Size(120, 30), Font = new Font("Segoe UI", 9) };
            TabPage tabResult = new TabPage("Kết quả quét") { BackColor = Color.White };
            TabPage tabQuarantine = new TabPage("Khu cách ly") { BackColor = Color.White };
            TabPage tabInfo = new TabPage("Thông tin chung") { BackColor = Color.White };
            TabPage tabLog = new TabPage("Nhật ký") { BackColor = Color.White };

            Button btnSelectAll = new Button { Text = "Chọn/Bỏ chọn", Location = new Point(610, 10), Width = 120, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnSelectAll.FlatAppearance.BorderColor = Color.LightGray;
            Button btnQuarantine = new Button { Text = "Cách ly đã chọn", Location = new Point(740, 10), Width = 140, Height = 35, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.DarkOrange, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnQuarantine.FlatAppearance.BorderSize = 0;
            Button btnDelete = new Button { Text = "Xóa tệp đã chọn", Location = new Point(890, 10), Width = 140, Height = 35, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.DarkRed, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnDelete.FlatAppearance.BorderSize = 0;

            ListView lvResults = new ListView { Location = new Point(10, 50), Width = 1040, Height = 360, View = View.Details, FullRowSelect = true, GridLines = true, CheckBoxes = true, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            lvResults.Columns.Add("[x]", 40);
            lvResults.Columns.Add("Tên tệp", 200);
            lvResults.Columns.Add("Đường dẫn", 400);
            lvResults.Columns.Add("Trạng thái", 100);
            lvResults.Columns.Add("Tên virus / dấu hiệu", 250);

            tabResult.Controls.AddRange(new Control[] { btnSelectAll, btnQuarantine, btnDelete, lvResults });

            lblInfoEngine = new Label
            {
                Text = BuildAntivirusInfoText(),
                Location = new Point(20, 20),
                Width = 1000,
                Height = 220,
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(31, 41, 55)
            };
            Button btnRefreshInfo = new Button { Text = "Nạp lại thông tin", Location = new Point(20, 250), Width = 150, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = btnFont };
            btnRefreshInfo.Click += (s, e) => RefreshAntivirusInfo();
            Button btnOpenSnapshots = new Button { Text = "Mở thư mục phục hồi", Location = new Point(184, 250), Width = 170, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = btnFont };
            btnOpenSnapshots.Click += (s, e) => {
                if (!Directory.Exists(RecoverySnapshotManager.SnapshotRoot)) Directory.CreateDirectory(RecoverySnapshotManager.SnapshotRoot);
                Process.Start(new ProcessStartInfo { FileName = RecoverySnapshotManager.SnapshotRoot, UseShellExecute = true });
            };
            Button btnUpdateClam = new Button { Text = "Cập nhật ClamAV", Location = new Point(368, 250), Width = 150, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(16, 124, 16), ForeColor = Color.White, Font = btnFont };
            btnUpdateClam.Click += async (s, e) => await UpdateClamAvDatabaseAsync(btnUpdateClam);
            Button btnOpenClam = new Button { Text = "Mở thư mục ClamAV", Location = new Point(532, 250), Width = 160, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = btnFont };
            btnOpenClam.Click += (s, e) => {
                ClamAvManager.EnsurePortableLayout();
                Process.Start(new ProcessStartInfo { FileName = ClamAvManager.ClamRoot, UseShellExecute = true });
            };
            Button btnUpdateRules = new Button { Text = "Cap nhat K3 rules", Location = new Point(706, 250), Width = 150, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = btnFont };
            btnUpdateRules.Click += (s, e) => UpdateK3RulesFromUrl();
            Button btnImportRules = new Button { Text = "Nhap rules", Location = new Point(870, 250), Width = 120, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = btnFont };
            btnImportRules.Click += (s, e) => ImportK3RulesFromFile();
            lblInfoQuarantine = new Label { Text = "", Location = new Point(20, 300), Width = 1000, Height = 80, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(16, 124, 16) };
            tabInfo.Controls.AddRange(new Control[] { lblInfoEngine, btnRefreshInfo, btnOpenSnapshots, btnUpdateClam, btnOpenClam, btnUpdateRules, btnImportRules, lblInfoQuarantine });

            lvLog = new ListView { Location = new Point(10, 15), Width = 1040, Height = 395, View = View.Details, FullRowSelect = true, GridLines = true, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            lvLog.Columns.Add("Thời gian", 150);
            lvLog.Columns.Add("Mức", 100);
            lvLog.Columns.Add("Nội dung", 760);
            tabLog.Controls.Add(lvLog);

            Button btnQReload = new Button { Text = "Nạp khu cách ly", Location = new Point(10, 10), Width = 130, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = btnFont };
            Button btnQRestore = new Button { Text = "Khôi phục", Location = new Point(150, 10), Width = 110, Height = 35, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(16, 124, 16), Font = btnFont };
            Button btnQDelete = new Button { Text = "Xóa vĩnh viễn", Location = new Point(270, 10), Width = 130, Height = 35, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.DarkRed, Font = btnFont };
            Button btnQOpen = new Button { Text = "Mở thư mục", Location = new Point(410, 10), Width = 110, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = btnFont };

            lvQuarantine = new ListView { Location = new Point(10, 55), Width = 1040, Height = 355, View = View.Details, FullRowSelect = true, GridLines = true, CheckBoxes = true, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            lvQuarantine.Columns.Add("[x]", 40);
            lvQuarantine.Columns.Add("Tên gốc", 220);
            lvQuarantine.Columns.Add("Đường dẫn gốc", 360);
            lvQuarantine.Columns.Add("Ngày cách ly", 150);
            lvQuarantine.Columns.Add("Dấu hiệu", 180);
            lvQuarantine.Columns.Add("Tệp cách ly", 240);
            lvQuarantine.Columns.Add("Size", 90);
            lvQuarantine.Columns.Add("SHA-256", 220);
            Button btnQTrust = new Button { Text = "Tin cay & khoi phuc", Location = new Point(530, 10), Width = 150, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = btnFont };
            tabQuarantine.Controls.AddRange(new Control[] { btnQReload, btnQRestore, btnQDelete, btnQOpen, btnQTrust, lvQuarantine });

            btnQReload.Click += (s, e) => LoadQuarantineList();
            btnQRestore.Click += (s, e) => RestoreSelectedQuarantine();
            btnQDelete.Click += (s, e) => DeleteSelectedQuarantine();
            btnQTrust.Click += (s, e) => TrustAndRestoreSelectedQuarantine();
            btnQOpen.Click += (s, e) => {
                if (!Directory.Exists(QuarantineDir)) Directory.CreateDirectory(QuarantineDir);
                Process.Start(new ProcessStartInfo { FileName = QuarantineDir, UseShellExecute = true });
            };

            btnSelectFolder.Click += (s, e) => {
                using (var fbd = new FolderBrowserDialog { Description = "Chọn thư mục để quét mã độc" }) {
                    if (fbd.ShowDialog() == DialogResult.OK) {
                        currentScanDir = fbd.SelectedPath;
                        lblPath.Text = currentScanDir;
                        AddLog("INFO", "Chọn thư mục quét: " + currentScanDir);
                    }
                }
            };

            btnSelectFile.Click += (s, e) => {
                using (var ofd = new OpenFileDialog { Title = "Chọn tệp để quét", Multiselect = false }) {
                    if (ofd.ShowDialog() == DialogResult.OK) {
                        currentScanDir = ofd.FileName;
                        lblPath.Text = currentScanDir;
                        AddLog("INFO", "Chọn tệp quét: " + currentScanDir);
                    }
                }
            };

            btnFullScan.Click += async (s, e) => {
                if (string.IsNullOrEmpty(currentScanDir) || (!Directory.Exists(currentScanDir) && !File.Exists(currentScanDir))) {
                    MessageBox.Show("Vui lòng chọn thư mục hoặc tệp cần quét trước!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                scanCts = new CancellationTokenSource();
                cleanCount = infectedCount = skippedCount = totalCount = 0;
                lvResults.Items.Clear();
                startTime = DateTime.Now;
                btnFullScan.Enabled = false;
                AddLog("INFO", "Bắt đầu quét: " + currentScanDir);
                
                try {
                    RecoverySnapshotResult snapshot = RecoverySnapshotManager.CreateSnapshot(currentScanDir);
                    if (!string.IsNullOrEmpty(snapshot.SnapshotDir))
                        AddLog("INFO", string.Format("Đã tạo snapshot phục hồi: {0} mục, backup {1} file ({2} KB). {3}", snapshot.ItemCount, snapshot.BackedUpFileCount, snapshot.BackedUpBytes / 1024, snapshot.SnapshotDir));

                    int maxFiles = CountScanFiles(currentScanDir, scanCts.Token);
                    progressBar.Maximum = maxFiles > 0 ? maxFiles : 1;
                    progressBar.Value = 0;
                    totalCount = maxFiles;

                    await Task.Run(() => {
                        int scanned = 0;
                        DateTime lastUi = DateTime.MinValue;
                        foreach (string file in EnumerateScanFiles(currentScanDir, scanCts.Token)) {
                            if (scanCts.Token.IsCancellationRequested) break;
                            scanned++;

                            if ((DateTime.Now - lastUi).TotalMilliseconds > 150) {
                                lastUi = DateTime.Now;
                                BeginInvoke(new Action(() => {
                                    int percent = maxFiles > 0 ? (int)(Math.Min(scanned, maxFiles) * 100.0 / maxFiles) : 0;
                                    lblStatus1.Text = string.Format("Đang quét toàn diện: {0}/{1} tệp ({2}%)", scanned, maxFiles, percent);
                                    lblStatus2.Text = string.Format("Đang quét: {0}", file);
                                    progressBar.Value = Math.Min(scanned, progressBar.Maximum);
                                    lblStats.Text = BuildScanStatsText();
                                }));
                            }

                            var res = AntivirusScanner.ScanFileReal(file);
                            
                            if (res.Status == "Sạch") cleanCount++;
                            else if (res.Status == "Lỗi") skippedCount++;
                            else {
                                infectedCount++;
                                BeginInvoke(new Action(() => {
                                    var item = new ListViewItem(new[] { "", Path.GetFileName(file), file, res.Status, res.VirusName });
                                    item.ForeColor = Color.Red;
                                    lvResults.Items.Add(item);
                                    if (lvResults.Items.Count > 500)
                                        lvResults.Items.RemoveAt(0);
                                    AddLog("WARN", string.Format("Phát hiện {0}: {1}", res.VirusName, file));
                                    
                                    if (chkAutoDelete.Checked) {
                                        try {
                                            if (chkBackup.Checked && QuarantineFile(file, res.VirusName)) {
                                                item.SubItems[3].Text = "Đã cách ly";
                                                item.ForeColor = Color.Orange;
                                                LoadQuarantineList();
                                                AddLog("WARN", "Tự động cách ly: " + file);
                                            } else {
                                                CryptoEngine.SecureShredFile(file);
                                                item.SubItems[3].Text = "Đã xóa";
                                                AddLog("WARN", "Tự động xóa an toàn: " + file);
                                            }
                                        } catch {}
                                    }
                                }));
                            }
                        }
                        BeginInvoke(new Action(() => {
                            lblStatus1.Text = scanCts.Token.IsCancellationRequested ? "Đã dừng quét!" : "Đã quét xong!";
                            lblStatus2.Text = "";
                            progressBar.Value = Math.Min(progressBar.Maximum, Math.Max(progressBar.Value, Math.Min(scanned, progressBar.Maximum)));
                            lblStats.Text = BuildScanStatsText();
                        }));
                    }, scanCts.Token);
                }
                catch (UnauthorizedAccessException) {
                    MessageBox.Show("Không có quyền truy cập vào một số thư mục!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally {
                    btnFullScan.Enabled = true;
                    lblStatus1.Text = scanCts.IsCancellationRequested ? "Đã dừng quét!" : "Đã quét xong!";
                    lblStatus2.Text = "";
                    AddLog(scanCts.IsCancellationRequested ? "WARN" : "INFO", string.Format("Kết thúc quét. Sạch={0}, Nhiễm={1}, Bỏ qua={2}, Tổng={3}", cleanCount, infectedCount, skippedCount, totalCount));
                    RefreshAntivirusInfo();
                }
            };

            btnStopScan.Click += (s, e) => {
                if (scanCts != null) scanCts.Cancel();
                AddLog("WARN", "Người dùng yêu cầu dừng quét.");
            };

            btnExport.Click += (s, e) => {
                if (lvResults.Items.Count == 0) {
                    MessageBox.Show("Chưa có kết quả quét để xuất báo cáo.", "Xuất báo cáo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                ExportScanReport(lvResults);
                return;
#if false
                using (var sfd = new SaveFileDialog {
                    Title = "Xuất báo cáo quét mã độc",
                    FileName = "K3AV_Report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv",
                    Filter = "CSV UTF-8 (*.csv)|*.csv|Text file (*.txt)|*.txt"
                }) {
                    if (sfd.ShowDialog(this) != DialogResult.OK) return;

                    var sb = new StringBuilder();
                    sb.AppendLine("Tên tệp,Đường dẫn,Trạng thái,Tên virus / dấu hiệu");
                    foreach (ListViewItem item in lvResults.Items) {
                        sb.AppendLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\"",
                            EscapeCsv(item.SubItems[1].Text),
                            EscapeCsv(item.SubItems[2].Text),
                            EscapeCsv(item.SubItems[3].Text),
                            EscapeCsv(item.SubItems[4].Text)));
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
                    AddLog("INFO", "Đã xuất báo cáo: " + sfd.FileName);
                    MessageBox.Show("Đã xuất báo cáo quét mã độc.", "Xuất báo cáo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
#endif
            };

            btnSelectAll.Click += (s, e) => {
                bool check = false;
                foreach (ListViewItem item in lvResults.Items) if (!item.Checked) { check = true; break; }
                foreach (ListViewItem item in lvResults.Items) item.Checked = check;
            };

            btnQuarantine.Click += (s, e) => {
                int count = 0;
                System.Collections.IEnumerable itemsToProcess = lvResults.CheckedItems.Count > 0 ? (System.Collections.IEnumerable)lvResults.CheckedItems : (System.Collections.IEnumerable)lvResults.SelectedItems;
                foreach (ListViewItem item in itemsToProcess) {
                    if (item.SubItems[3].Text == "Đã cách ly" || item.SubItems[3].Text == "Đã xóa") continue;
                    try {
                        string file = item.SubItems[2].Text;
                        string virusName = item.SubItems[4].Text;
                        if (QuarantineFile(file, virusName)) {
                            item.SubItems[3].Text = "Đã cách ly";
                            item.ForeColor = Color.Orange;
                            AddLog("WARN", "Cách ly thủ công: " + file);
                            count++;
                        }
                    } catch { }
                }
                if (count > 0) {
                    LoadQuarantineList();
                    tabControl.SelectedTab = tabQuarantine;
                    MessageBox.Show(string.Format("Đã cách ly an toàn {0} tệp!", count), "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else MessageBox.Show("Vui lòng chọn hoặc đánh dấu tệp cần cách ly!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            btnDelete.Click += (s, e) => {
                System.Collections.IEnumerable itemsToProcess = lvResults.CheckedItems.Count > 0 ? (System.Collections.IEnumerable)lvResults.CheckedItems : (System.Collections.IEnumerable)lvResults.SelectedItems;
                int processCount = lvResults.CheckedItems.Count > 0 ? lvResults.CheckedItems.Count : lvResults.SelectedItems.Count;
                if (processCount == 0) {
                    MessageBox.Show("Vui lòng chọn hoặc đánh dấu tệp cần xóa!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (MessageBox.Show("Sử dụng thuật toán Xóa An Toàn DoD 5220.22-M để tiêu hủy vĩnh viễn các mã độc đã chọn?\nKhông thể khôi phục sau khi xóa!", "Xác nhận Diệt", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) {
                    int count = 0;
                    foreach (ListViewItem item in itemsToProcess) {
                        if (item.SubItems[3].Text == "Đã xóa") continue;
                        try {
                            string file = item.SubItems[2].Text;
                            if (File.Exists(file)) {
                                RemediateAfterThreat(file);
                                CryptoEngine.SecureShredFile(file);
                            }
                            item.SubItems[3].Text = "Đã xóa";
                            item.ForeColor = Color.Gray;
                            AddLog("WARN", "Xóa an toàn thủ công: " + file);
                            count++;
                        } catch { }
                    }
                    if (count > 0) MessageBox.Show(string.Format("Đã tiêu hủy {0} tệp mã độc!", count), "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            tabControl.TabPages.Add(tabResult);
            tabControl.TabPages.Add(tabQuarantine);
            tabControl.TabPages.Add(tabInfo);
            tabControl.TabPages.Add(tabLog);

            this.Controls.Add(tabControl);
            this.Controls.Add(statusPanel);
            this.Controls.Add(topPanel);

            LoadQuarantineList();
            AddLog("INFO", "K3-AV Engine sẵn sàng.");
            RefreshAntivirusInfo();
        }

        private string BuildAntivirusInfoText()
        {
            return
                "K3-AV Engine\n\n" +
                "- Lớp 1: Heuristic offline chặn double-extension và script nguy hiểm (.bat, .vbs, .cmd).\n" +
                "- Lớp 2: Kiểm tra EICAR và chữ ký hash cục bộ.\n" +
                "- Lớp 3: Entropy heuristic cho EXE/DLL đóng gói bất thường.\n" +
                "- Lớp 4: Tích hợp Windows Defender MpCmdRun nếu hệ thống có hỗ trợ.\n" +
                "- Lớp 5: Quét chặn trước khi file/folder được đưa vào USB/Két sắt.\n\n" +
                "Engine đang dùng: " + AntivirusScanner.GetEngineInfo() + "\n" +
                "Thư mục cách ly: " + QuarantineDir;
        }

        private void RefreshAntivirusInfo()
        {
            if (lblInfoEngine != null) lblInfoEngine.Text = BuildAntivirusInfoText();
            if (lblInfoQuarantine != null)
            {
                int qCount = Directory.Exists(QuarantineDir) ? Directory.GetFiles(QuarantineDir, "*.k3q").Length : 0;
                lblInfoQuarantine.Text = string.Format("Khu cách ly hiện có {0} tệp. Lần quét gần nhất: Sạch={1}, Nhiễm={2}, Bỏ qua={3}, Tổng={4}.", qCount, cleanCount, infectedCount, skippedCount, totalCount);
            }
        }

        private void UpdateK3RulesFromUrl()
        {
            ConfigManager.LoadConfig();
            if (string.IsNullOrWhiteSpace(ConfigManager.K3RuleUpdateUrl))
            {
                MessageBox.Show("Chua cau hinh URL k3-rules.json trong Cai dat chung.", "K3 rules", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                using (WebClient client = new WebClient())
                {
                    string json = client.DownloadString(ConfigManager.K3RuleUpdateUrl);
                    InstallK3RulesJson(json);
                }
                AddLog("INFO", "Da cap nhat K3 rules tu URL.");
                MessageBox.Show("Da cap nhat K3 rules.", "K3 rules", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AddLog("ERROR", "Cap nhat K3 rules that bai: " + ex.Message);
                MessageBox.Show(ex.Message, "K3 rules", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ImportK3RulesFromFile()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Title = "Nhap k3-rules.json", Filter = "JSON (*.json)|*.json|All files (*.*)|*.*" })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    InstallK3RulesJson(File.ReadAllText(ofd.FileName, Encoding.UTF8));
                    AddLog("INFO", "Da nhap K3 rules offline: " + ofd.FileName);
                    MessageBox.Show("Da nhap K3 rules.", "K3 rules", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "K3 rules", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void InstallK3RulesJson(string json)
        {
            if (!IsValidK3RulesJson(json)) throw new InvalidDataException("File K3 rules khong hop le.");
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "rules");
            Directory.CreateDirectory(dir);
            string target = Path.Combine(dir, "k3-rules.json");
            ConfigManager.PrepareManagedFileForWrite(target);
            File.WriteAllText(target, json, new UTF8Encoding(false));
            ConfigManager.HideManagedPath(target);
            try { K3IntegrityManager.WriteManifest(); } catch { }
        }

        private bool IsValidK3RulesJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 2 * 1024 * 1024) return false;
            string lower = json.ToLowerInvariant();
            return lower.Contains("\"version\"") &&
                (lower.Contains("\"namerules\"") || lower.Contains("\"contentrules\"")) &&
                json.TrimStart().StartsWith("{");
        }

        private async Task UpdateClamAvDatabaseAsync(Button button)
        {
            button.Enabled = false;
            string oldText = button.Text;
            button.Text = "Đang cập nhật...";
            AddLog("INFO", "Bắt đầu cập nhật database ClamAV.");
            try
            {
                ClamAvUpdateResult result = await Task.Run(() => ClamAvManager.UpdateDatabase(180000));
                AddLog(result.Success ? "INFO" : "ERROR", result.Message);
                if (!string.IsNullOrWhiteSpace(result.Output))
                {
                    string output = result.Output.Length > 1200 ? result.Output.Substring(0, 1200) + "..." : result.Output;
                    AddLog(result.Success ? "INFO" : "ERROR", output);
                }
                RefreshAntivirusInfo();
                MessageBox.Show(result.Message, "Cập nhật ClamAV", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            finally
            {
                button.Text = oldText;
                button.Enabled = true;
            }
        }

        private void AddLog(string level, string message)
        {
            if (lvLog == null) return;
            if (lvLog.InvokeRequired)
            {
                lvLog.BeginInvoke(new Action(() => AddLog(level, message)));
                return;
            }
            var item = new ListViewItem(new[] { DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), level, message });
            if (level == "WARN") item.ForeColor = Color.DarkOrange;
            else if (level == "ERROR") item.ForeColor = Color.DarkRed;
            lvLog.Items.Insert(0, item);
        }

        private void ExportScanReport(ListView results)
        {
            using (SaveFileDialog sfd = new SaveFileDialog
            {
                Title = "Xuat bao cao quet ma doc",
                FileName = "K3AV_Report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".html",
                Filter = "HTML report (*.html)|*.html|CSV UTF-8 (*.csv)|*.csv|PDF backlog (*.pdf)|*.pdf"
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                string outputPath = sfd.FileName;
                if (sfd.FilterIndex == 2)
                {
                    File.WriteAllText(outputPath, BuildCsvReport(results), new UTF8Encoding(true));
                }
                else
                {
                    if (sfd.FilterIndex == 3) outputPath = Path.ChangeExtension(outputPath, ".html");
                    File.WriteAllText(outputPath, BuildHtmlReport(results), new UTF8Encoding(false));
                }

                AddLog("INFO", "Da xuat bao cao: " + outputPath);
                string message = sfd.FilterIndex == 3
                    ? "PDF la backlog tren WinForms hien tai. Da xuat HTML thay the:\n" + outputPath
                    : "Da xuat bao cao:\n" + outputPath;
                MessageBox.Show(message, "Xuat bao cao", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private string BuildCsvReport(ListView results)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Ten tep,Duong dan,Trang thai,Dau hieu");
            foreach (ListViewItem item in results.Items)
            {
                sb.AppendLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\"",
                    EscapeCsv(item.SubItems[1].Text),
                    EscapeCsv(item.SubItems[2].Text),
                    EscapeCsv(item.SubItems[3].Text),
                    EscapeCsv(item.SubItems[4].Text)));
            }
            return sb.ToString();
        }

        private string BuildHtmlReport(ListView results)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>K3 AV Report</title>");
            sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#1f2937}table{border-collapse:collapse;width:100%}th,td{border:1px solid #d1d5db;padding:8px;text-align:left;font-size:13px}th{background:#0f766e;color:white}.summary{margin:12px 0 18px;padding:12px;background:#f3f4f6}</style>");
            sb.AppendLine("</head><body>");
            sb.AppendLine("<h1>USB An Toan K3 - Scan Report</h1>");
            sb.AppendLine("<div class=\"summary\">");
            sb.AppendLine("<div>Generated: " + EscapeHtml(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")) + "</div>");
            sb.AppendLine("<div>Target: " + EscapeHtml(currentScanDir ?? "") + "</div>");
            sb.AppendLine("<div>Engine: " + EscapeHtml(AntivirusScanner.GetEngineInfo()) + "</div>");
            sb.AppendLine("<div>Clean=" + cleanCount + " | Infected=" + infectedCount + " | Skipped=" + skippedCount + " | Total=" + totalCount + "</div>");
            sb.AppendLine("</div>");
            sb.AppendLine("<table><thead><tr><th>File</th><th>Path</th><th>Status</th><th>Signature</th></tr></thead><tbody>");
            foreach (ListViewItem item in results.Items)
            {
                sb.AppendLine("<tr><td>" + EscapeHtml(item.SubItems[1].Text) + "</td><td>" +
                    EscapeHtml(item.SubItems[2].Text) + "</td><td>" +
                    EscapeHtml(item.SubItems[3].Text) + "</td><td>" +
                    EscapeHtml(item.SubItems[4].Text) + "</td></tr>");
            }
            sb.AppendLine("</tbody></table></body></html>");
            return sb.ToString();
        }

        private string EscapeCsv(string value)
        {
            return (value ?? "").Replace("\"", "\"\"");
        }

        private string EscapeHtml(string value)
        {
            return (value ?? "")
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private string BuildScanStatsText()
        {
            return string.Format("Sạch: {0} | Nhiễm: {1} | Bỏ qua: {2} | Tổng: {3} | Thời gian: {4}",
                cleanCount, infectedCount, skippedCount, totalCount, (DateTime.Now - startTime).ToString(@"hh\:mm\:ss"));
        }

        private int CountScanFiles(string path, CancellationToken token)
        {
            int count = 0;
            foreach (string file in EnumerateScanFiles(path, token, false))
            {
                if (token.IsCancellationRequested) break;
                count++;
            }
            return count;
        }

        private IEnumerable<string> EnumerateScanFiles(string path, CancellationToken token)
        {
            return EnumerateScanFiles(path, token, true);
        }

        private IEnumerable<string> EnumerateScanFiles(string path, CancellationToken token, bool countSkipped)
        {
            if (File.Exists(path))
            {
                if (!ShouldSkipScanPath(path)) yield return path;
                yield break;
            }

            if (!Directory.Exists(path)) yield break;

            Stack<string> pending = new Stack<string>();
            pending.Push(path);

            while (pending.Count > 0)
            {
                if (token.IsCancellationRequested) yield break;

                string dir = pending.Pop();
                if (ShouldSkipScanPath(dir)) continue;

                string[] files = new string[0];
                try { files = Directory.GetFiles(dir); }
                catch { if (countSkipped) skippedCount++; }

                foreach (string file in files)
                {
                    if (token.IsCancellationRequested) yield break;
                    if (!ShouldSkipScanPath(file)) yield return file;
                }

                string[] dirs = new string[0];
                try { dirs = Directory.GetDirectories(dir); }
                catch { if (countSkipped) skippedCount++; }

                foreach (string child in dirs)
                {
                    if (token.IsCancellationRequested) yield break;
                    if (!ShouldSkipScanPath(child)) pending.Push(child);
                    else if (countSkipped) skippedCount++;
                }
            }
        }

        private bool ShouldSkipScanPath(string path)
        {
            try
            {
                string name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(name)) return false;

                if (name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)) return true;
                if (name.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase)) return true;
                if (name.Equals("Recovery", StringComparison.OrdinalIgnoreCase)) return true;
                if (name.Equals("Boot", StringComparison.OrdinalIgnoreCase)) return true;
                if (name.Equals("EFI", StringComparison.OrdinalIgnoreCase)) return true;
                if (name.Equals(".vault", StringComparison.OrdinalIgnoreCase)) return true;
                if (name.Equals(".vault_decoy", StringComparison.OrdinalIgnoreCase)) return true;
                if (name.Equals("AutoLauncher", StringComparison.OrdinalIgnoreCase)) return true;

                FileAttributes attr = File.GetAttributes(path);
                if ((attr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) return true;
                if ((attr & FileAttributes.System) == FileAttributes.System && Directory.Exists(path)) return true;
            }
            catch { return true; }

            return false;
        }

        private bool QuarantineFile(string sourceFile, string virusName)
        {
            if (!File.Exists(sourceFile)) return false;
            if (!Directory.Exists(QuarantineDir)) Directory.CreateDirectory(QuarantineDir);
            int restored = RemediateAfterThreat(sourceFile);

            string id = Guid.NewGuid().ToString();
            string dest = Path.Combine(QuarantineDir, id + ".k3q");
            File.Move(sourceFile, dest);
            string sha256 = TrustedFileManager.ComputeSha256(dest);

            string meta = Path.Combine(QuarantineDir, id + ".meta");
            string[] lines = new string[] {
                "OriginalPath=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(sourceFile)),
                "OriginalName=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Path.GetFileName(sourceFile))),
                "VirusName=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(virusName ?? "")),
                "QuarantineDate=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "Size=" + new FileInfo(dest).Length.ToString(),
                "SHA256=" + sha256
            };
            File.WriteAllLines(meta, lines);
            AddLog("WARN", "Đưa vào khu cách ly: " + sourceFile);
            if (restored > 0)
                AddLog("INFO", string.Format("Da hien lai {0} muc bi an lien quan shortcut: {1}", restored, sourceFile));
            return true;
        }

        private int RecoverFolderHiddenByShortcut(string shortcutFile)
        {
            try
            {
                if (string.IsNullOrEmpty(shortcutFile) || !shortcutFile.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    return 0;

                string dir = Path.GetDirectoryName(shortcutFile);
                string folderName = Path.GetFileNameWithoutExtension(shortcutFile);
                if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(folderName)) return 0;

                string realFolder = Path.Combine(dir, folderName);
                if (!Directory.Exists(realFolder)) return 0;

                int restored = ClearHiddenSystemAttributes(realFolder);
                foreach (string childDir in Directory.GetDirectories(realFolder, "*", SearchOption.AllDirectories))
                    restored += ClearHiddenSystemAttributes(childDir);
                foreach (string file in Directory.GetFiles(realFolder, "*", SearchOption.AllDirectories))
                    restored += ClearHiddenSystemAttributes(file);
                return restored;
            }
            catch
            {
                return 0;
            }
        }

        private int RemediateAfterThreat(string threatFile)
        {
            if (string.IsNullOrEmpty(threatFile)) return 0;

            int restored = RecoverFolderHiddenByShortcut(threatFile);
            if (!ShouldRunUsbRemediation(threatFile)) return restored;

            string dir = Path.GetDirectoryName(threatFile);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return restored;

            try
            {
                foreach (string childDir in Directory.GetDirectories(dir))
                {
                    if (ShouldSkipRemediationPath(childDir)) continue;
                    restored += ClearHiddenSystemAttributes(childDir);
                    try
                    {
                        foreach (string nestedDir in Directory.GetDirectories(childDir, "*", SearchOption.AllDirectories))
                            if (!ShouldSkipRemediationPath(nestedDir)) restored += ClearHiddenSystemAttributes(nestedDir);
                        foreach (string nestedFile in Directory.GetFiles(childDir, "*", SearchOption.AllDirectories))
                            if (!ShouldSkipRemediationPath(nestedFile)) restored += ClearHiddenSystemAttributes(nestedFile);
                    }
                    catch { }
                }

                foreach (string siblingFile in Directory.GetFiles(dir))
                {
                    if (ShouldSkipRemediationPath(siblingFile)) continue;
                    if (string.Equals(siblingFile, threatFile, StringComparison.OrdinalIgnoreCase)) continue;
                    restored += ClearHiddenSystemAttributes(siblingFile);
                }
            }
            catch { }

            return restored;
        }

        private bool ShouldRunUsbRemediation(string threatFile)
        {
            string name = Path.GetFileName(threatFile);
            string ext = Path.GetExtension(threatFile);
            if (name.Equals("autorun.inf", StringComparison.OrdinalIgnoreCase)) return true;
            return ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".vbs", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".vbe", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".bat", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".js", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jse", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".wsf", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".hta", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".scr", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".pif", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".com", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".ps1", StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldSkipRemediationPath(string path)
        {
            try
            {
                string full = Path.GetFullPath(path);
                string baseDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
                if (full.StartsWith(Path.Combine(baseDir, ".vault"), StringComparison.OrdinalIgnoreCase)) return true;
                if (full.StartsWith(Path.Combine(baseDir, ".vault_decoy"), StringComparison.OrdinalIgnoreCase)) return true;
                if (Path.GetFileName(full).Equals(".vault_config.json", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }
            return false;
        }

        private int ClearHiddenSystemAttributes(string path)
        {
            try
            {
                FileAttributes attr = File.GetAttributes(path);
                FileAttributes cleaned = attr & ~FileAttributes.Hidden & ~FileAttributes.System;
                if (cleaned == attr) return 0;
                File.SetAttributes(path, cleaned);
                return 1;
            }
            catch
            {
                return 0;
            }
        }

        private void LoadQuarantineList()
        {
            if (lvQuarantine == null) return;
            lvQuarantine.Items.Clear();
            if (!Directory.Exists(QuarantineDir)) Directory.CreateDirectory(QuarantineDir);

            foreach (string qFile in Directory.GetFiles(QuarantineDir, "*.k3q"))
            {
                string id = Path.GetFileNameWithoutExtension(qFile);
                string metaFile = Path.Combine(QuarantineDir, id + ".meta");
                string originalPath = "";
                string originalName = Path.GetFileName(qFile);
                string virusName = "Không rõ";
                string date = File.GetCreationTime(qFile).ToString("dd/MM/yyyy HH:mm:ss");

                string size = new FileInfo(qFile).Length.ToString();
                string sha256 = TrustedFileManager.ComputeSha256(qFile);

                if (File.Exists(metaFile))
                {
                    foreach (string line in File.ReadAllLines(metaFile))
                    {
                        int sep = line.IndexOf('=');
                        if (sep <= 0) continue;
                        string key = line.Substring(0, sep);
                        string value = line.Substring(sep + 1);
                        if (key == "OriginalPath") originalPath = DecodeMeta(value);
                        else if (key == "OriginalName") originalName = DecodeMeta(value);
                        else if (key == "VirusName") virusName = DecodeMeta(value);
                        else if (key == "QuarantineDate") date = value;
                        else if (key == "Size") size = value;
                        else if (key == "SHA256") sha256 = value;
                    }
                }

                var item = new ListViewItem(new[] { "", originalName, originalPath, date, virusName, qFile, size, sha256 });
                item.Tag = qFile;
                item.ForeColor = Color.DarkOrange;
                lvQuarantine.Items.Add(item);
            }
            RefreshAntivirusInfo();
        }

        private string DecodeMeta(string value)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
            catch { return value; }
        }

        private void RestoreSelectedQuarantine()
        {
            System.Collections.IEnumerable itemsToProcess = lvQuarantine.CheckedItems.Count > 0 ? (System.Collections.IEnumerable)lvQuarantine.CheckedItems : (System.Collections.IEnumerable)lvQuarantine.SelectedItems;
            int count = 0;
            foreach (ListViewItem item in itemsToProcess)
            {
                string qFile = item.Tag != null ? item.Tag.ToString() : "";
                string originalPath = item.SubItems[2].Text;
                if (string.IsNullOrEmpty(qFile) || !File.Exists(qFile)) continue;

                if (string.IsNullOrEmpty(originalPath))
                {
                    using (SaveFileDialog sfd = new SaveFileDialog { FileName = item.SubItems[1].Text, Title = "Chọn nơi khôi phục tệp cách ly" })
                    {
                        if (sfd.ShowDialog(this) != DialogResult.OK) continue;
                        originalPath = sfd.FileName;
                    }
                }

                try
                {
                    string dir = Path.GetDirectoryName(originalPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    if (File.Exists(originalPath))
                    {
                        if (MessageBox.Show("Tệp đích đã tồn tại. Ghi đè?\n" + originalPath, "Khôi phục", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                            continue;
                        File.Delete(originalPath);
                    }
                    File.Move(qFile, originalPath);
                    DeleteMetaFor(qFile);
                    AddLog("INFO", "Khôi phục từ khu cách ly: " + originalPath);
                    count++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Không thể khôi phục: " + ex.Message, "Khôi phục", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            LoadQuarantineList();
            if (count > 0) MessageBox.Show(string.Format("Đã khôi phục {0} tệp.", count), "Khôi phục", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void TrustAndRestoreSelectedQuarantine()
        {
            int selectedCount = lvQuarantine.CheckedItems.Count > 0 ? lvQuarantine.CheckedItems.Count : lvQuarantine.SelectedItems.Count;
            if (selectedCount == 0)
            {
                MessageBox.Show("Vui long chon tep trong khu cach ly.", "Khu cach ly", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show("Danh dau file da chon la tin cay va khoi phuc ve duong dan goc?", "False positive", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            System.Collections.IEnumerable itemsToProcess = lvQuarantine.CheckedItems.Count > 0 ? (System.Collections.IEnumerable)lvQuarantine.CheckedItems : (System.Collections.IEnumerable)lvQuarantine.SelectedItems;
            foreach (ListViewItem item in itemsToProcess)
            {
                string qFile = item.Tag != null ? item.Tag.ToString() : "";
                if (File.Exists(qFile)) TrustedFileManager.TrustFile(qFile);
            }
            RestoreSelectedQuarantine();
        }

        private void DeleteSelectedQuarantine()
        {
            System.Collections.IEnumerable itemsToProcess = lvQuarantine.CheckedItems.Count > 0 ? (System.Collections.IEnumerable)lvQuarantine.CheckedItems : (System.Collections.IEnumerable)lvQuarantine.SelectedItems;
            int selectedCount = lvQuarantine.CheckedItems.Count > 0 ? lvQuarantine.CheckedItems.Count : lvQuarantine.SelectedItems.Count;
            if (selectedCount == 0)
            {
                MessageBox.Show("Vui lòng chọn tệp trong khu cách ly.", "Khu cách ly", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show("Xóa vĩnh viễn các tệp đã chọn trong khu cách ly?", "Khu cách ly", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            int count = 0;
            foreach (ListViewItem item in itemsToProcess)
            {
                string qFile = item.Tag != null ? item.Tag.ToString() : "";
                try
                {
                    if (File.Exists(qFile)) CryptoEngine.SecureShredFile(qFile);
                    DeleteMetaFor(qFile);
                    AddLog("WARN", "Xóa vĩnh viễn khỏi khu cách ly: " + qFile);
                    count++;
                }
                catch { }
            }
            LoadQuarantineList();
            if (count > 0) MessageBox.Show(string.Format("Đã xóa vĩnh viễn {0} tệp.", count), "Khu cách ly", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DeleteMetaFor(string qFile)
        {
            string meta = Path.Combine(Path.GetDirectoryName(qFile), Path.GetFileNameWithoutExtension(qFile) + ".meta");
            try { if (File.Exists(meta)) File.Delete(meta); } catch { }
        }
    }
}
