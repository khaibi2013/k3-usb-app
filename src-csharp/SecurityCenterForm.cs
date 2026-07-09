using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AnToanUSB
{
    public class SecurityCenterForm : Form
    {
        private readonly string vaultPath;
        private Label lblSummary;
        private ListView lvStatus;
        private Button btnVerify;
        private ProgressBar progress;

        public SecurityCenterForm(string vaultPath)
        {
            this.vaultPath = vaultPath;
            InitializeComponent();
            LoadStatus();
        }

        private void InitializeComponent()
        {
            Text = "Trung tâm bảo mật";
            Size = new Size(780, 560);
            MinimumSize = new Size(760, 520);
            StartPosition = FormStartPosition.CenterParent;
            Theme.Apply(this);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Theme.Ink };
            PictureBox icon = new PictureBox { Image = CustomIcons.GetShieldCheckIcon(), SizeMode = PictureBoxSizeMode.Zoom, Location = new Point(24, 24), Size = new Size(48, 48) };
            Label title = new Label { Text = "Trung tâm bảo mật K3", Location = new Point(86, 20), AutoSize = true, Font = new Font("Segoe UI Semibold", 16, FontStyle.Bold), ForeColor = Theme.TextInv };
            lblSummary = new Label { Text = "", Location = new Point(88, 54), AutoSize = true, Font = Theme.FontBody, ForeColor = Theme.TextInvMute };
            header.Controls.AddRange(new Control[] { icon, title, lblSummary });

            lvStatus = new ListView
            {
                Dock = DockStyle.Top,
                Height = 300,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BorderStyle = BorderStyle.None,
                Font = Theme.FontBody,
                BackColor = Theme.White
            };
            lvStatus.Columns.Add("Hạng mục", 220);
            lvStatus.Columns.Add("Trạng thái", 180);
            lvStatus.Columns.Add("Gợi ý", 330);

            Panel bottom = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Mist };
            btnVerify = new Button { Text = "Kiểm tra toàn vẹn Két sắt", Location = new Point(24, 26), Width = 220, Height = 42, BackColor = Theme.Teal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            btnVerify.FlatAppearance.BorderSize = 0;
            btnVerify.Click += (s, e) => VerifyVaultIntegrity();
            Button btnReload = new Button { Text = "Nạp lại trạng thái", Location = new Point(260, 26), Width = 160, Height = 42, BackColor = Theme.Slate2, ForeColor = Theme.TextInv, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            btnReload.FlatAppearance.BorderSize = 0;
            btnReload.Click += (s, e) => LoadStatus();
            Button btnTrusted = new Button { Text = "Phần mềm tin cậy", Location = new Point(436, 26), Width = 160, Height = 42, BackColor = Theme.Amber, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            btnTrusted.FlatAppearance.BorderSize = 0;
            btnTrusted.Click += (s, e) => new TrustedFilesForm().ShowDialog(this);
            progress = new ProgressBar { Location = new Point(24, 88), Width = 710, Height = 14, Style = ProgressBarStyle.Continuous };
            Label note = new Label
            {
                Text = "Kiểm tra toàn vẹn sẽ xác thực MAC và thử giải mã trong bộ nhớ, không xuất dữ liệu ra máy tính.",
                Location = new Point(24, 116),
                AutoSize = true,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMute
            };
            bottom.Controls.AddRange(new Control[] { btnVerify, btnReload, btnTrusted, progress, note });

            Controls.Add(bottom);
            Controls.Add(lvStatus);
            Controls.Add(header);
        }

        private void LoadStatus()
        {
            lvStatus.Items.Clear();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string config = Path.Combine(baseDir, ".vault_config.json");
            string realVault = Path.Combine(baseDir, ".vault");
            string decoyVault = Path.Combine(baseDir, ".vault_decoy");
            int encryptedCount = Directory.Exists(vaultPath) ? Directory.GetFiles(vaultPath, "*.k3enc", SearchOption.AllDirectories).Length : 0;

            AddStatus("Két sắt chính", Directory.Exists(realVault), Directory.Exists(realVault) ? "Sẵn sàng" : "Thiếu thư mục .vault", "Tạo lại bằng Format Két sắt nếu bị thiếu.");
            AddStatus("Két sắt giả", Directory.Exists(decoyVault), Directory.Exists(decoyVault) ? "Sẵn sàng" : "Chưa bật decoy", "Nên đặt mật khẩu giả nếu bán cho nhóm khách cần chống cưỡng ép.");
            AddStatus("Cấu hình bảo mật", File.Exists(config), File.Exists(config) ? "Có cấu hình" : "Thiếu cấu hình", "Thiết lập mật khẩu lần đầu để tạo cấu hình.");
            AddStatus("HWID Lock", !string.IsNullOrEmpty(ConfigManager.HwidLock), string.IsNullOrEmpty(ConfigManager.HwidLock) ? "Chưa khóa máy" : "Đã khóa thiết bị", "Nên bật khi giao sản phẩm cho khách.");
            AddStatus("Chặn ghi USB", UsbHelper.IsReadOnlyEnabled(), UsbHelper.IsReadOnlyEnabled() ? "Đang bật" : "Đang tắt", "Bật khi cần chống ghi ngoài phần mềm.");
            AddStatus("Tệp mã hóa", encryptedCount > 0, encryptedCount + " tệp .k3enc", encryptedCount > 0 ? "Có dữ liệu trong két." : "Kéo tệp vào BaoMat hoặc dùng Mã hóa USB.");
            AddStatus("Chế độ truy cập", !ConfigManager.IsDecoyMode, ConfigManager.IsDecoyMode ? "Decoy vault" : "Két thật", ConfigManager.IsDecoyMode ? "Bạn đang ở két giả." : "Đang quản lý dữ liệu thật.");

            lblSummary.Text = string.Format("{0} tệp mã hóa | HWID: {1} | Read-only: {2}",
                encryptedCount,
                string.IsNullOrEmpty(ConfigManager.HwidLock) ? "Tắt" : "Bật",
                UsbHelper.IsReadOnlyEnabled() ? "Bật" : "Tắt");
        }

        private void AddStatus(string name, bool ok, string status, string suggestion)
        {
            var item = new ListViewItem(new[] { name, status, suggestion });
            item.ForeColor = ok ? Theme.TealDark : Theme.AmberDark;
            lvStatus.Items.Add(item);
        }

        private void VerifyVaultIntegrity()
        {
            if (!Directory.Exists(vaultPath))
            {
                MessageBox.Show("Không tìm thấy thư mục két sắt.", "Kiểm tra toàn vẹn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string[] files = Directory.GetFiles(vaultPath, "*.k3enc", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                MessageBox.Show("Két sắt chưa có tệp mã hóa để kiểm tra.", "Kiểm tra toàn vẹn", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnVerify.Enabled = false;
            progress.Maximum = files.Length;
            progress.Value = 0;

            int ok = 0;
            int bad = 0;
            string firstError = "";

            foreach (string file in files)
            {
                try
                {
                    CryptoEngine.VerifyEncryptedFile(file);
                    ok++;
                }
                catch (Exception ex)
                {
                    bad++;
                    if (string.IsNullOrEmpty(firstError)) firstError = Path.GetFileName(file) + ": " + ex.Message;
                }

                if (progress.Value < progress.Maximum) progress.Value++;
                Application.DoEvents();
            }

            btnVerify.Enabled = true;
            LoadStatus();

            if (bad == 0)
            {
                MessageBox.Show(string.Format("Đã kiểm tra {0} tệp. Toàn bộ hợp lệ.", ok), "Kiểm tra toàn vẹn", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(string.Format("Hợp lệ: {0}\nLỗi: {1}\n\nLỗi đầu tiên: {2}", ok, bad, firstError), "Cảnh báo toàn vẹn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
