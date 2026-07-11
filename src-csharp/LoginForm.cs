using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace AnToanUSB
{
    public class LoginForm : Form
    {
        private TextBox txtPassword;
        private Button btnLogin;
        private Label lblTitle;
        private Label lblHelp;
        private Label lblPassword;
        private Label lblStatus;
        private Panel pnlConnectionDot;
        private bool isConnected;
        private PictureBox picShield;
        private bool setupPromptShown;

        public LoginForm()
        {
            ConfigManager.LoadConfig();
            InitializeComponent();
            ApplyLanguage();
            LanguageManager.LanguageChanged += ApplyLanguage;
            this.Shown += (s, e) => PromptInitialSetupIfNeeded();
        }

        private void InitializeComponent()
        {
            this.Text = "Login";
            this.ClientSize = new Size(440, 330);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            Theme.Apply(this);

            lblTitle = new Label { Text = "USB An Toàn K3", Location = new Point(24, 22), AutoSize = true, Font = new Font("Segoe UI Semibold", 14, FontStyle.Bold), ForeColor = Theme.TextDark };
            
            lblHelp = new Label { Text = "Trợ giúp HELP!", Location = new Point(300, 25), AutoSize = true, ForeColor = Theme.TealDark, Cursor = Cursors.Hand, Font = Theme.FontHeading };
            lblHelp.Click += (s, e) => {
                LanguageManager.SwitchLanguage(LanguageManager.CurrentLanguage == "en" ? "vi" : "en");
            };

            picShield = new PictureBox { Size = new Size(76, 76), Location = new Point(182, 62), SizeMode = PictureBoxSizeMode.Zoom };
            Image logoImage = AppBrand.LoadLogoImage(76);
            if (logoImage != null)
            {
                picShield.Image = logoImage;
            }
            else
            {
                Bitmap bmp = new Bitmap(76, 76);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (Brush shield = new SolidBrush(Theme.Teal))
                    using (Brush dark = new SolidBrush(Theme.TealDark))
                    using (Pen whitePen = new Pen(Color.White, 4))
                    {
                        Point[] pts = { new Point(38, 6), new Point(62, 16), new Point(58, 48), new Point(38, 68), new Point(18, 48), new Point(14, 16) };
                        g.FillPolygon(shield, pts);
                        g.DrawPolygon(new Pen(dark, 2), pts);
                        g.DrawArc(whitePen, 26, 24, 24, 20, 180, 180);
                        g.DrawRectangle(whitePen, 24, 34, 28, 22);
                    }
                }
                picShield.Image = bmp;
            }

            lblPassword = new Label { Text = "Password", Location = new Point(78, 150), AutoSize = true, Font = Theme.FontHeading, ForeColor = Theme.TextMute };
            
            txtPassword = new TextBox { Location = new Point(78, 174), Width = 284, PasswordChar = '●', Font = new Font("Segoe UI", 12), BorderStyle = BorderStyle.FixedSingle };
            txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) PerformLogin(); };
            
            btnLogin = new Button { Text = "Đăng nhập", Location = new Point(78, 216), Width = 284, Height = 42, BackColor = Theme.Teal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.Click += (s, e) => PerformLogin();
            pnlConnectionDot = new Panel { Size = new Size(14, 14), Location = new Point(this.ClientSize.Width - 26, this.ClientSize.Height - 24), Anchor = AnchorStyles.Right | AnchorStyles.Bottom, Cursor = Cursors.Hand };
            pnlConnectionDot.Paint += DrawConnectionDot;

            lblStatus = new Label { Text = "Đang giải mã thiết lập/1...", Location = new Point(24, 270), AutoSize = true, ForeColor = Theme.TextMute, Font = Theme.FontSmall };

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblHelp);
            this.Controls.Add(picShield);
            this.Controls.Add(lblPassword);
            this.Controls.Add(txtPassword);
            this.Controls.Add(btnLogin);
            this.Controls.Add(lblStatus);
            this.Controls.Add(pnlConnectionDot);
            SetConnectionStatus(false);
        }

        private void DrawConnectionDot(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(this.BackColor);

            Color fill = isConnected ? Theme.Teal : Color.FromArgb(239, 112, 125);
            Color border = isConnected ? Theme.TealDark : Color.FromArgb(213, 63, 78);
            Rectangle rect = new Rectangle(2, 2, pnlConnectionDot.Width - 5, pnlConnectionDot.Height - 5);

            using (SolidBrush glow = new SolidBrush(Color.FromArgb(45, fill)))
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(border, 1.2f))
            {
                e.Graphics.FillEllipse(glow, 0, 0, pnlConnectionDot.Width - 1, pnlConnectionDot.Height - 1);
                e.Graphics.FillEllipse(brush, rect);
                e.Graphics.DrawEllipse(pen, rect);
            }
        }

        private void SetConnectionStatus(bool connected)
        {
            isConnected = connected;
            if (lblStatus != null)
                lblStatus.Text = connected ? "Đã kết nối an toàn." : LanguageManager.GetString("LoginStatus");
            if (pnlConnectionDot != null)
                pnlConnectionDot.Invalidate();
        }

        private void CompleteSuccessfulLogin()
        {
            SetConnectionStatus(true);
            Application.DoEvents();
            Thread.Sleep(180);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void PromptInitialSetupIfNeeded()
        {
            if (setupPromptShown) return;
            ConfigManager.LoadConfig();
            if (!ConfigManager.NeedsInitialSetup) return;

            setupPromptShown = true;
            if (ShowInitialSetup())
            {
                CryptoEngine.Authenticate(txtPassword.Text);
                CompleteSuccessfulLogin();
            }
        }

        private void ApplyLanguage()
        {
            this.Text = LanguageManager.GetString("LoginTitle");
            lblTitle.Text = string.IsNullOrEmpty(ConfigManager.LoginTitle) ? LanguageManager.GetString("AppTitle") : ConfigManager.LoginTitle;
            lblHelp.Text = string.IsNullOrEmpty(ConfigManager.LoginHelpText) ? LanguageManager.GetString("LoginHelp") : ConfigManager.LoginHelpText;
            lblHelp.Visible = !ConfigManager.HideLoginHelp;
            btnLogin.Text = LanguageManager.GetString("LoginBtn");
            SetConnectionStatus(isConnected);
        }

        private int failedAttempts = 0;

        private void PerformLogin()
        {
            string pwd = txtPassword.Text;
            if (string.IsNullOrEmpty(pwd)) return;

            try
            {
                ConfigManager.LoadConfig();
                failedAttempts = ConfigManager.FailedLoginCount;
                if (ConfigManager.LockedUntilUtc.HasValue && ConfigManager.LockedUntilUtc.Value > DateTime.UtcNow)
                {
                    TimeSpan remaining = ConfigManager.LockedUntilUtc.Value - DateTime.UtcNow;
                    MessageBox.Show(string.Format("Dang bi khoa dang nhap. Vui long thu lai sau {0} phut {1} giay.", (int)remaining.TotalMinutes, remaining.Seconds), "Bao mat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (ConfigManager.NeedsInitialSetup)
                {
                    if (ShowInitialSetup())
                    {
                        CryptoEngine.Authenticate(txtPassword.Text);
                        CompleteSuccessfulLogin();
                    }
                    return;
                }

                if (!ConfigManager.IsHwidAllowed())
                {
                    MessageBox.Show("USB này đã được khóa với thiết bị khác. Không thể truy cập trên máy hiện tại.", "HWID Lock", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (ConfigManager.VerifyPassword(pwd))
                {
                    CryptoEngine.Authenticate(pwd);
                    failedAttempts = 0;
                    ConfigManager.SaveLoginFailureState(0, null);
                    CompleteSuccessfulLogin();
                }
                else
                {
                    SetConnectionStatus(false);
                    HandleFailedLogin();
                    return;
#if false
                    failedAttempts++;
                    if (failedAttempts >= 10)
                    {
                        MessageBox.Show("CẢNH BÁO: Bạn đã nhập sai 10 lần! Hệ thống đang tự hủy toàn bộ dữ liệu an toàn...", "Bảo mật", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        SelfDestructVault("wipe_all");
                        Application.Exit();
                    }
                    else if (failedAttempts >= 5)
                    {
                        btnLogin.Enabled = false;
                        txtPassword.Enabled = false;
                        MessageBox.Show(string.Format("Bạn đã nhập sai {0} lần. Đăng nhập bị khóa 5 phút. Còn {1} lần sai trước khi xóa dữ liệu.", failedAttempts, 10 - failedAttempts), "Bảo mật", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        System.Windows.Forms.Timer unlockTimer = new System.Windows.Forms.Timer();
                        unlockTimer.Interval = 5 * 60 * 1000;
                        unlockTimer.Tick += (s, e) =>
                        {
                            unlockTimer.Stop();
                            unlockTimer.Dispose();
                            btnLogin.Enabled = true;
                            txtPassword.Enabled = true;
                            txtPassword.Focus();
                        };
                        unlockTimer.Start();
                    }
                    else
                    {
                        MessageBox.Show(string.Format("Mật khẩu không đúng! Còn {0} lần sai trước khi khóa 5 phút.", 5 - failedAttempts), "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
#endif
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void HandleFailedLogin()
        {
            failedAttempts++;

            if (failedAttempts >= 10)
            {
                string mode = string.IsNullOrEmpty(ConfigManager.SelfDestructMode) ? "off" : ConfigManager.SelfDestructMode;
                if (mode == "lock")
                {
                    ConfigManager.SaveLoginFailureState(failedAttempts, DateTime.MaxValue.ToUniversalTime());
                    MessageBox.Show("Nhap sai 10 lan. USB da bi khoa dang nhap theo chinh sach.", "Bao mat", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    return;
                }
                if (mode == "wipe_real" || mode == "wipe_all")
                {
                    MessageBox.Show("Nhap sai 10 lan. Dang thuc thi chinh sach tu huy: " + mode, "Bao mat", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    SelfDestructVault(mode);
                    Application.Exit();
                    return;
                }

                LockForFiveMinutes(failedAttempts);
                MessageBox.Show("Nhap sai 10 lan. Chinh sach tu huy dang tat, dang khoa dang nhap 5 phut.", "Bao mat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (failedAttempts >= 5)
            {
                LockForFiveMinutes(failedAttempts);
                MessageBox.Show(string.Format("Nhap sai {0} lan. Dang nhap bi khoa 5 phut.", failedAttempts), "Bao mat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ConfigManager.SaveLoginFailureState(failedAttempts, null);
            MessageBox.Show(string.Format("Mat khau khong dung. Con {0} lan sai truoc khi khoa 5 phut.", 5 - failedAttempts), "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void LockForFiveMinutes(int attempts)
        {
            DateTime until = DateTime.UtcNow.AddMinutes(5);
            ConfigManager.SaveLoginFailureState(attempts, until);
            btnLogin.Enabled = false;
            txtPassword.Enabled = false;
            System.Windows.Forms.Timer unlockTimer = new System.Windows.Forms.Timer();
            unlockTimer.Interval = 5 * 60 * 1000;
            unlockTimer.Tick += (s, e) =>
            {
                unlockTimer.Stop();
                unlockTimer.Dispose();
                ConfigManager.SaveLoginFailureState(attempts, null);
                btnLogin.Enabled = true;
                txtPassword.Enabled = true;
                txtPassword.Focus();
            };
            unlockTimer.Start();
        }

        private bool ShowInitialSetup()
        {
            using (Form dialog = new Form())
            using (Label title = new Label())
            using (Label lblReal = new Label())
            using (TextBox real = new TextBox())
            using (Label lblConfirm = new Label())
            using (TextBox confirm = new TextBox())
            using (Label lblDecoy = new Label())
            using (TextBox decoy = new TextBox())
            using (Label lblPolicy = new Label())
            using (ComboBox cmbPolicy = new ComboBox())
            using (CheckBox chkAutoScan = new CheckBox())
            using (Label lblDone = new Label())
            using (Button ok = new Button())
            using (Button cancel = new Button())
            {
                dialog.Text = "Thiết lập lần đầu";
                dialog.Size = new Size(500, 430);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                Theme.Apply(dialog);

                title.Text = "Tạo mật khẩu Két sắt";
                title.Font = Theme.FontTitle;
                title.ForeColor = Theme.TextDark;
                title.Location = new Point(24, 18);
                title.AutoSize = true;

                lblReal.Text = "Mật khẩu chính";
                lblReal.Location = new Point(24, 58);
                lblReal.AutoSize = true;
                real.Location = new Point(24, 78);
                real.Width = 360;
                real.PasswordChar = '●';

                lblConfirm.Text = "Nhập lại mật khẩu chính";
                lblConfirm.Location = new Point(24, 108);
                lblConfirm.AutoSize = true;
                confirm.Location = new Point(24, 128);
                confirm.Width = 360;
                confirm.PasswordChar = '●';

                lblDecoy.Text = "Mật khẩu giả (tùy chọn)";
                lblDecoy.Location = new Point(24, 158);
                lblDecoy.AutoSize = true;
                decoy.Location = new Point(24, 178);
                decoy.Width = 360;
                decoy.PasswordChar = '●';

                ok.Text = "Tạo két sắt";
                lblPolicy.Text = "Buoc 3: Chinh sach khi sai 10 lan";
                lblPolicy.Location = new Point(24, 208);
                lblPolicy.AutoSize = true;
                cmbPolicy.Location = new Point(24, 230);
                cmbPolicy.Width = 360;
                cmbPolicy.DropDownStyle = ComboBoxStyle.DropDownList;
                cmbPolicy.Items.AddRange(new string[] { "off", "lock", "wipe_real", "wipe_all" });
                cmbPolicy.SelectedIndex = 0;

                chkAutoScan.Text = "Buoc 4: Tu dong quet USB sau khi dang nhap";
                chkAutoScan.Location = new Point(24, 264);
                chkAutoScan.AutoSize = true;

                lblDone.Text = "Buoc 5: Kiem tra lai thong tin roi bam Tao ket sat.";
                lblDone.Location = new Point(24, 296);
                lblDone.Width = 430;
                lblDone.Height = 32;
                lblDone.ForeColor = Theme.TextMute;

                ok.Location = new Point(268, 340);
                ok.Width = 96;
                ok.DialogResult = DialogResult.OK;
                cancel.Text = "Hủy";
                cancel.Location = new Point(372, 340);
                cancel.Width = 92;
                cancel.DialogResult = DialogResult.Cancel;
                dialog.Controls.AddRange(new Control[] { title, lblReal, real, lblConfirm, confirm, lblDecoy, decoy, lblPolicy, cmbPolicy, chkAutoScan, lblDone, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;

                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                if (string.IsNullOrWhiteSpace(real.Text) || real.Text.Length < 6)
                {
                    MessageBox.Show("Mật khẩu chính cần ít nhất 6 ký tự.", "Thiết lập", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                if (real.Text != confirm.Text)
                {
                    MessageBox.Show("Hai lần nhập mật khẩu chính không khớp.", "Thiết lập", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                if (!string.IsNullOrEmpty(decoy.Text) && decoy.Text == real.Text)
                {
                    MessageBox.Show("Mật khẩu giả không được trùng mật khẩu chính.", "Thiết lập", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                string selectedPolicy = cmbPolicy.SelectedItem != null ? cmbPolicy.SelectedItem.ToString() : "off";
                if (selectedPolicy == "wipe_real" || selectedPolicy == "wipe_all")
                {
                    if (MessageBox.Show("Chinh sach nay se xoa du lieu khi sai 10 lan: " + selectedPolicy + "\nBan chac chan muon bat?", "Xac nhan chinh sach tu huy", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                        return false;
                }

                ConfigManager.SelfDestructMode = selectedPolicy;
                ConfigManager.AutoScanOnLogin = chkAutoScan.Checked;
                ConfigManager.SavePasswords(real.Text, decoy.Text);
                txtPassword.Text = real.Text;
                return true;
            }
        }

        private void SelfDestructVault(string mode)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] targets = mode == "wipe_real"
                ? new string[] { ".vault" }
                : new string[] {
                    ".vault",
                    ".vault_decoy",
                    "BaoMat",
                    ".k3_quarantine",
                    ".k3_recovery_snapshots",
                    ".k3_trusted_hashes.txt",
                    ".k3_history.log",
                    ".vault_config.json"
                };

            foreach (string target in targets)
                SecureDeletePath(System.IO.Path.Combine(baseDir, target));
        }

        private void SecureDeletePath(string path)
        {
            if (System.IO.File.Exists(path))
            {
                try { CryptoEngine.SecureShredFile(path); }
                catch { try { System.IO.File.Delete(path); } catch { } }
                return;
            }

            if (!System.IO.Directory.Exists(path)) return;
            foreach (var file in System.IO.Directory.GetFiles(path, "*", System.IO.SearchOption.AllDirectories))
            {
                try { CryptoEngine.SecureShredFile(file); }
                catch { try { System.IO.File.Delete(file); } catch { } }
            }
            try { System.IO.Directory.Delete(path, true); } catch { }
        }
    }
}
