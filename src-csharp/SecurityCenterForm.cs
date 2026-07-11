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
            Text = "Trung tam bao mat";
            Size = new Size(860, 620);
            MinimumSize = new Size(820, 560);
            StartPosition = FormStartPosition.CenterParent;
            Theme.Apply(this);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Theme.Ink };
            PictureBox icon = new PictureBox { Image = CustomIcons.GetShieldCheckIcon(), SizeMode = PictureBoxSizeMode.Zoom, Location = new Point(24, 24), Size = new Size(52, 52) };
            Label title = new Label { Text = "Trung tam bao mat K3", Location = new Point(92, 20), AutoSize = true, Font = new Font("Segoe UI Semibold", 17, FontStyle.Bold), ForeColor = Theme.TextInv };
            lblSummary = new Label { Text = "", Location = new Point(94, 58), AutoSize = true, Font = Theme.FontBody, ForeColor = Theme.TextInvMute };
            header.Controls.AddRange(new Control[] { icon, title, lblSummary });

            lvStatus = new ListView
            {
                Dock = DockStyle.Top,
                Height = 352,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BorderStyle = BorderStyle.None,
                Font = Theme.FontBody,
                BackColor = Theme.White
            };
            lvStatus.Columns.Add("Hang muc", 230);
            lvStatus.Columns.Add("Trang thai", 190);
            lvStatus.Columns.Add("Goi y", 400);

            Panel bottom = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Mist };
            btnVerify = new Button { Text = "Kiem tra toan ven Ket sat", Location = new Point(24, 26), Width = 220, Height = 42, BackColor = Theme.Teal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            btnVerify.FlatAppearance.BorderSize = 0;
            btnVerify.Click += (s, e) => VerifyVaultIntegrity();

            Button btnReload = new Button { Text = "Nap lai trang thai", Location = new Point(260, 26), Width = 160, Height = 42, BackColor = Theme.Slate2, ForeColor = Theme.TextInv, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            btnReload.FlatAppearance.BorderSize = 0;
            btnReload.Click += (s, e) => LoadStatus();

            Button btnTrusted = new Button { Text = "Phan mem tin cay", Location = new Point(436, 26), Width = 170, Height = 42, BackColor = Theme.Amber, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            btnTrusted.FlatAppearance.BorderSize = 0;
            btnTrusted.Click += (s, e) => new TrustedFilesForm().ShowDialog(this);

            Button btnRepair = new Button { Text = "Tu sua cau truc USB", Location = new Point(622, 26), Width = 180, Height = 42, BackColor = Theme.TealDark, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            btnRepair.FlatAppearance.BorderSize = 0;
            btnRepair.Click += (s, e) => { ConfigManager.EnsureRuntimeStorage(); ConfigManager.EnsurePortableMetadata(); LoadStatus(); };
            Button btnManifest = new Button { Text = "Tao manifest", Location = new Point(622, 76), Width = 180, Height = 36, BackColor = Theme.Amber, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            btnManifest.FlatAppearance.BorderSize = 0;
            btnManifest.Click += (s, e) => { K3IntegrityManager.WriteManifest(); LoadStatus(); MessageBox.Show("Da tao .k3_integrity_manifest.json.", "Integrity", MessageBoxButtons.OK, MessageBoxIcon.Information); };

            progress = new ProgressBar { Location = new Point(24, 88), Width = 780, Height = 14, Style = ProgressBarStyle.Continuous };
            Label note = new Label
            {
                Text = "Kiem tra toan ven xac thuc MAC va thu giai ma trong bo nho, khong xuat du lieu ra may tinh.",
                Location = new Point(24, 116),
                AutoSize = true,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMute
            };
            bottom.Controls.AddRange(new Control[] { btnVerify, btnReload, btnTrusted, btnRepair, btnManifest, progress, note });

            Controls.Add(bottom);
            Controls.Add(lvStatus);
            Controls.Add(header);
        }

        private void LoadStatus()
        {
            lvStatus.Items.Clear();
            ConfigManager.EnsureRuntimeStorage();
            ConfigManager.EnsurePortableMetadata();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string config = Path.Combine(baseDir, ".vault_config.json");
            string realVault = Path.Combine(baseDir, ".vault");
            string decoyVault = Path.Combine(baseDir, ".vault_decoy");
            string baoMat = Path.Combine(baseDir, "BaoMat");
            string autorun = Path.Combine(baseDir, "autorun.inf");
            string icon = Path.Combine(baseDir, "icon.png");
            string launcher = Path.Combine(baseDir, "AutoLauncher", "K3AutoLauncher.exe");
            string installLauncher = Path.Combine(baseDir, "AutoLauncher", "CaiDat-AutoLauncher.bat");

            int encryptedCount = Directory.Exists(vaultPath) ? Directory.GetFiles(vaultPath, "*.k3enc", SearchOption.AllDirectories).Length : 0;
            bool readOnlyEnabled = UsbHelper.IsReadOnlyEnabled();
            bool canWrite = UsbHelper.CanWriteToCurrentAppDrive();
            K3IntegrityResult integrity = K3IntegrityManager.VerifyManifest();

            AddStatus("Ket sat chinh", Directory.Exists(realVault), Directory.Exists(realVault) ? "San sang" : "Thieu .vault", "App co the tu tao lai cau truc rong.");
            AddStatus("Ket sat gia", Directory.Exists(decoyVault), Directory.Exists(decoyVault) ? "San sang" : "Thieu .vault_decoy", "Nen dung khi can mat khau gia.");
            AddStatus("Thu muc BaoMat", Directory.Exists(baoMat), Directory.Exists(baoMat) ? "San sang" : "Thieu BaoMat", "Noi tu dong dua file vao ket.");
            AddStatus("Cau hinh bao mat", File.Exists(config), File.Exists(config) ? "Da co" : "Chua tao", "Lan dau nhap mat khau se tao file cau hinh.");
            AddStatus("HWID Lock", !string.IsNullOrEmpty(ConfigManager.HwidLock), string.IsNullOrEmpty(ConfigManager.HwidLock) ? "Dang tat" : "Dang bat", "Nen bat khi giao san pham cho khach.");
            AddStatus("Chan ghi USB", readOnlyEnabled, readOnlyEnabled ? "Policy dang bat" : "Policy dang tat", "Can quyen Admin va co the can rut/cam lai USB.");
            AddStatus("Kiem tra ghi thuc te", readOnlyEnabled ? !canWrite : canWrite, canWrite ? "Ghi duoc" : "Dang bi chan ghi", readOnlyEnabled && canWrite ? "Rut/cam lai USB de Windows ap dung readonly." : "Trang thai phu hop.");
            AddStatus("Autorun USB", File.Exists(autorun), File.Exists(autorun) ? "Da co" : "Thieu", "Dung de hien ten/icon/menu mo app.");
            AddStatus("Logo USB", File.Exists(icon), File.Exists(icon) ? "Da co" : "Thieu icon.png", "Can de man hinh splash/login dep hon.");
            AddStatus("K3 AutoLauncher", File.Exists(launcher) && File.Exists(installLauncher), File.Exists(launcher) ? "Da dong goi" : "Chua co", "Cai tren may can tu mo khi cam USB.");
            AddStatus("Toan ven app/rules", !integrity.HasWarning, IntegrityStatusText(integrity), IntegritySuggestionText(integrity));
            AddStatus("Tep ma hoa", encryptedCount > 0, encryptedCount + " tep .k3enc", encryptedCount > 0 ? "Co du lieu trong ket." : "Hay dua file vao BaoMat hoac Ket sat.");
            AddStatus("Che do truy cap", !ConfigManager.IsDecoyMode, ConfigManager.IsDecoyMode ? "Ket gia" : "Ket that", ConfigManager.IsDecoyMode ? "Dang quan ly ket gia." : "Dang quan ly ket that.");

            lblSummary.Text = string.Format("{0} file ma hoa | HWID: {1} | Read-only: {2} | Ghi thu: {3}",
                encryptedCount,
                string.IsNullOrEmpty(ConfigManager.HwidLock) ? "Tat" : "Bat",
                readOnlyEnabled ? "Bat" : "Tat",
                canWrite ? "OK" : "Blocked");
        }

        private void AddStatus(string name, bool ok, string status, string suggestion)
        {
            var item = new ListViewItem(new[] { name, status, suggestion });
            item.ForeColor = ok ? Theme.TealDark : Theme.AmberDark;
            lvStatus.Items.Add(item);
        }

        private string IntegrityStatusText(K3IntegrityResult result)
        {
            if (!result.ManifestExists) return "Chua co manifest";
            if (!result.HasWarning) return "Hop le";
            return "Canh bao";
        }

        private string IntegritySuggestionText(K3IntegrityResult result)
        {
            if (!result.ManifestExists) return ".k3_integrity_manifest.json";
            if (!result.HasWarning) return result.Checked + " file dung hash";
            if (result.Failed.Count > 0) return "Sai hash: " + string.Join(", ", result.Failed.ToArray());
            return "Thieu: " + string.Join(", ", result.Missing.ToArray());
        }

        private void VerifyVaultIntegrity()
        {
            if (!Directory.Exists(vaultPath))
            {
                MessageBox.Show("Khong tim thay thu muc ket sat.", "Kiem tra toan ven", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string[] files = Directory.GetFiles(vaultPath, "*.k3enc", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                MessageBox.Show("Ket sat chua co tep ma hoa de kiem tra.", "Kiem tra toan ven", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show(string.Format("Da kiem tra {0} tep. Toan bo hop le.", ok), "Kiem tra toan ven", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show(string.Format("Hop le: {0}\nLoi: {1}\n\nLoi dau tien: {2}", ok, bad, firstError), "Canh bao toan ven", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
