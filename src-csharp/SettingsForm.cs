using System;
using System.Drawing;
using System.Windows.Forms;

namespace AnToanUSB
{
    public class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Cài đặt chung";
            this.Size = new Size(800, 550);
            this.MinimumSize = new Size(800, 550);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            Theme.Apply(this);

            TabControl tabControl = new TabControl { Dock = DockStyle.Fill, ItemSize = new Size(150, 30), Font = Theme.FontBody };
            
            TabPage tabLoginPwd = new TabPage("Mật khẩu đăng nhập");
            TabPage tabEncPwd = new TabPage("Mật khẩu mã hóa");
            TabPage tabLoginCustom = new TabPage("Tùy chỉnh đăng nhập");
            TabPage tabGeneral = new TabPage("Cài đặt chung");

            // --- Setup tabLoginPwd (Old Settings) ---
            Label lblReal = new Label { Text = "Mật khẩu CHÍNH (Real Password):", Location = new Point(30, 20), AutoSize = true };
            TextBox txtRealPassword = new TextBox { Location = new Point(30, 40), Width = 320, PasswordChar = '●' };
            Label lblDecoy = new Label { Text = "Mật khẩu GIẢ (Decoy Password):", Location = new Point(30, 80), AutoSize = true };
            TextBox txtDecoyPassword = new TextBox { Location = new Point(30, 100), Width = 320, PasswordChar = '●' };
            Button btnSavePassword = new Button { Text = "Lưu Mật khẩu", Location = new Point(30, 140), Width = 320, Height = 40, BackColor = Theme.Teal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            btnSavePassword.Click += (s, e) => {
                string rp = txtRealPassword.Text;
                string dp = txtDecoyPassword.Text;
                if (string.IsNullOrEmpty(rp)) { MessageBox.Show("Bạn chưa nhập mật khẩu chính mới. Mật khẩu hiện tại được giữ nguyên."); return; }
                if (rp.Length < 6) { MessageBox.Show("Mật khẩu chính cần ít nhất 6 ký tự."); return; }
                if (rp == dp) { MessageBox.Show("Mật khẩu giả không được trùng với mật khẩu chính!"); return; }
                ConfigManager.SavePasswords(rp, dp);
                txtRealPassword.Text = "";
                txtDecoyPassword.Text = "";
                MessageBox.Show("Đã cập nhật mật khẩu thành công!", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            Button btnHwidLock = new Button { Text = "Khóa thiết bị (HWID Binding)", Location = new Point(30, 200), Width = 320, Height = 40 };
            btnHwidLock.Click += (s, e) => {
                string serial = UsbHelper.GetUsbSerialNumber();
                var confirm = MessageBox.Show("Khóa USB này với thiết bị hiện tại?\n\nHWID: " + serial + "\n\nSau khi khóa, phần mềm sẽ từ chối đăng nhập trên thiết bị khác.", "HWID Lock", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes)
                {
                    ConfigManager.BindToCurrentDevice();
                    MessageBox.Show("Đã khóa USB với thiết bị hiện tại.", "HWID Lock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            Button btnHwidUnlock = new Button { Text = "Gỡ khóa thiết bị (HWID)", Location = new Point(370, 200), Width = 320, Height = 40 };
            btnHwidUnlock.Click += (s, e) => {
                if (MessageBox.Show("Gỡ khóa HWID để USB đăng nhập được trên máy khác?", "HWID Lock", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    ConfigManager.ClearHwidBinding();
                    MessageBox.Show("Đã gỡ khóa HWID.", "HWID Lock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            Button btnReadOnlyLock = new Button { Text = UsbHelper.IsReadOnlyEnabled() ? "Tắt chế độ Chỉ Đọc" : "Bật chế độ Chỉ Đọc", Location = new Point(30, 250), Width = 320, Height = 40 };
            btnReadOnlyLock.Click += (s, e) => {
                try
                {
                    bool next = !UsbHelper.IsReadOnlyEnabled();
                    UsbHelper.SetReadOnly(next);
                    btnReadOnlyLock.Text = next ? "Tắt chế độ Chỉ Đọc" : "Bật chế độ Chỉ Đọc";
                    bool canWrite = UsbHelper.CanWriteToCurrentAppDrive();
                    bool verified = next ? !canWrite : canWrite;
                    string message = next
                        ? (canWrite ? "Đã bật chặn ghi nhưng Windows vẫn ghi thử được. Hãy rút/cắm lại USB." : "Đã bật chặn ghi và kiểm tra thực tế: USB không ghi được.")
                        : (canWrite ? "Đã tắt chặn ghi và kiểm tra thực tế: USB đã ghi được." : "Đã tắt chặn ghi nhưng Windows vẫn chưa cho ghi. Hãy rút/cắm lại USB.");
                    MessageBox.Show(message, "Security", MessageBoxButtons.OK, verified ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Read-Only Lock", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            Label lblPwdNote = new Label { Text = "Để trống nếu không muốn đổi mật khẩu. Mật khẩu hiện tại không được hiển thị vì lý do bảo mật.", Location = new Point(30, 180), Width = 680, Height = 18, ForeColor = Theme.TextMute };
            tabLoginPwd.Controls.AddRange(new Control[] { lblReal, txtRealPassword, lblDecoy, txtDecoyPassword, btnSavePassword, lblPwdNote, btnHwidLock, btnHwidUnlock, btnReadOnlyLock });
            ConfigManager.LoadConfig();
            txtRealPassword.Text = "";
            txtDecoyPassword.Text = "";

            Label lblEncInfo = new Label {
                Text = "Mật khẩu mã hóa riêng được tạo ngay khi dùng menu chuột phải: Mã hóa > Mã hóa với khóa tùy chọn.\n\nMỗi file dùng salt riêng và không lưu mật khẩu trong cấu hình. Khi giải mã, phần mềm sẽ tự nhận dạng file dùng khóa riêng và hỏi mật khẩu riêng.\n\nKhuyến nghị: dùng mật khẩu riêng dài tối thiểu 8 ký tự, khác mật khẩu đăng nhập két.",
                Location = new Point(30, 25),
                Width = 700,
                Height = 120,
                Font = Theme.FontBody,
                ForeColor = Theme.TextDark
            };
            Button btnOpenCustomEncryptHelp = new Button { Text = "Mở thử thư mục Két sắt", Location = new Point(30, 160), Width = 220, Height = 38, BackColor = Theme.Teal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnOpenCustomEncryptHelp.Click += (s, e) => {
                string vault = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigManager.IsDecoyMode ? ".vault_decoy" : ".vault");
                if (!System.IO.Directory.Exists(vault)) System.IO.Directory.CreateDirectory(vault);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = vault, UseShellExecute = true });
            };
            tabEncPwd.Controls.AddRange(new Control[] { lblEncInfo, btnOpenCustomEncryptHelp });

            Label lblCustomTitle = new Label { Text = "Tiêu đề màn hình đăng nhập:", Location = new Point(30, 30), AutoSize = true };
            TextBox txtLoginTitle = new TextBox { Location = new Point(30, 52), Width = 360 };
            Label lblCustomHelp = new Label { Text = "Nhãn trợ giúp/ngôn ngữ:", Location = new Point(30, 90), AutoSize = true };
            TextBox txtLoginHelp = new TextBox { Location = new Point(30, 112), Width = 360 };
            CheckBox chkHideHelp = new CheckBox { Text = "Ẩn nút trợ giúp trên màn hình đăng nhập", Location = new Point(30, 150), AutoSize = true };
            Button btnSaveLoginCustom = new Button { Text = "Lưu tùy chỉnh đăng nhập", Location = new Point(30, 190), Width = 220, Height = 40, BackColor = Theme.Teal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            txtLoginTitle.Text = ConfigManager.LoginTitle;
            txtLoginHelp.Text = ConfigManager.LoginHelpText;
            chkHideHelp.Checked = ConfigManager.HideLoginHelp;
            btnSaveLoginCustom.Click += (s, e) => {
                ConfigManager.LoginTitle = string.IsNullOrWhiteSpace(txtLoginTitle.Text) ? "USB An Toàn K3" : txtLoginTitle.Text.Trim();
                ConfigManager.LoginHelpText = string.IsNullOrWhiteSpace(txtLoginHelp.Text) ? "Trợ giúp HELP!" : txtLoginHelp.Text.Trim();
                ConfigManager.HideLoginHelp = chkHideHelp.Checked;
                ConfigManager.SaveAllConfig();
                MessageBox.Show("Đã lưu tùy chỉnh đăng nhập. Mở lại màn hình đăng nhập để xem thay đổi.", "Tùy chỉnh đăng nhập", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            tabLoginCustom.Controls.AddRange(new Control[] { lblCustomTitle, txtLoginTitle, lblCustomHelp, txtLoginHelp, chkHideHelp, btnSaveLoginCustom });

            // --- Setup tabGeneral ---
            tabGeneral.BackColor = Theme.White;
            int y = 20;

            CheckBox chkAutoEnc = new CheckBox { Text = "Mã hóa dữ liệu được sao chép sang USB trong Thư mục:", Location = new Point(30, y), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            ComboBox cmbFolder = new ComboBox { Location = new Point(370, y - 2), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFolder.Items.AddRange(new string[] { "BaoMat", "Toàn bộ USB" }); cmbFolder.SelectedIndex = 0;
            y += 25;
            Label lblAutoEncDesc = new Label { Text = "- Chọn một thư mục, nếu chọn 'Toàn bộ USB', tất cả dữ liệu sao chép sang USB sẽ Mặc định được mã hóa.\n  Các tệp sau khi Mã hóa sẽ có phần mở rộng là .k3enc", Location = new Point(45, y), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Italic) };
            y += 40;
            Label lblMaxFileSize = new Label { Text = "* Dung lượng tệp tối đa sẽ được mã hóa (vượt quá sẽ không mã hóa):", Location = new Point(30, y), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            ComboBox cmbMaxSize = new ComboBox { Location = new Point(430, y - 2), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMaxSize.Items.AddRange(new string[] { "-- Không giới hạn --", "-- 1 GB --", "-- 2 GB --", "-- 4 GB --" }); cmbMaxSize.SelectedIndex = 2;
            
            y += 40;
            CheckBox chkAutoDec = new CheckBox { Text = "Mặc định Giải mã khi sao chép ra máy tính", Location = new Point(30, y), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            y += 25;
            Label lblAutoDecDesc = new Label { Text = "- Nếu chọn tính năng này, tất cả các tệp nếu đang được Mã hóa trên USB (có đuôi tệp là .k3enc) sẽ được\n  Giải mã khi sao chép ra máy tính.", Location = new Point(45, y), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Italic) };

            y += 45;
            CheckBox chkShowHidden = new CheckBox { Text = "Mặc định Hiển thị tất cả các Tệp và Thư mục ẩn trong danh sách", Location = new Point(30, y), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Regular) };
            y += 25;
            Label lblShowHiddenDesc = new Label { Text = "- Nếu bỏ chọn, các Tệp và Thư mục ẩn của hệ thống sẽ không được hiển thị.", Location = new Point(45, y), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Italic) };

            y += 35;
            CheckBox chkWipeHistory = new CheckBox { Text = "Tự động xóa lịch sử kết nối của USB khi thoát phần mềm", Location = new Point(30, y), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Regular) };

            y += 30;
            CheckBox chkWipeMacOs = new CheckBox { Text = "Tự động xóa các Metadata thừa của MacOS (các tệp có tên ._ phía trước)", Location = new Point(30, y), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.DodgerBlue };

            y += 40;
            Button btnSaveGeneral = new Button { Text = "Lưu thay đổi", Location = new Point(300, y), Width = 150, Height = 40, BackColor = Theme.Teal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            try { btnSaveGeneral.Image = IconExtractor.GetSystemIcon("shell32.dll", 6, true); btnSaveGeneral.TextImageRelation = TextImageRelation.ImageBeforeText; } catch { }
            ConfigManager.LoadConfig();

            chkAutoEnc.Checked = ConfigManager.AutoEncryptFolder != "";
            if (ConfigManager.AutoEncryptFolder == "Toàn bộ USB") cmbFolder.SelectedIndex = 1; else cmbFolder.SelectedIndex = 0;
            
            if (ConfigManager.MaxFileSizeBytes == -1) cmbMaxSize.SelectedIndex = 0;
            else if (ConfigManager.MaxFileSizeBytes == 1073741824) cmbMaxSize.SelectedIndex = 1;
            else if (ConfigManager.MaxFileSizeBytes == 2147483648) cmbMaxSize.SelectedIndex = 2;
            else cmbMaxSize.SelectedIndex = 3;

            chkAutoDec.Checked = ConfigManager.AutoDecrypt;
            chkShowHidden.Checked = ConfigManager.ShowHidden;
            chkWipeHistory.Checked = ConfigManager.WipeHistory;
            chkWipeMacOs.Checked = ConfigManager.WipeMacOs;

            btnSaveGeneral.Click += (s, e) => {
                ConfigManager.AutoEncryptFolder = chkAutoEnc.Checked ? cmbFolder.SelectedItem.ToString() : "";
                
                if (cmbMaxSize.SelectedIndex == 0) ConfigManager.MaxFileSizeBytes = -1;
                else if (cmbMaxSize.SelectedIndex == 1) ConfigManager.MaxFileSizeBytes = 1073741824;
                else if (cmbMaxSize.SelectedIndex == 2) ConfigManager.MaxFileSizeBytes = 2147483648;
                else ConfigManager.MaxFileSizeBytes = 4294967296;

                ConfigManager.AutoDecrypt = chkAutoDec.Checked;
                ConfigManager.ShowHidden = chkShowHidden.Checked;
                ConfigManager.WipeHistory = chkWipeHistory.Checked;
                ConfigManager.WipeMacOs = chkWipeMacOs.Checked;

                ConfigManager.SaveAllConfig();
                MessageBox.Show("Đã lưu các tùy chỉnh Cài đặt chung!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            };

            tabGeneral.Controls.AddRange(new Control[] {
                chkAutoEnc, cmbFolder, lblAutoEncDesc, lblMaxFileSize, cmbMaxSize,
                chkAutoDec, lblAutoDecDesc, chkShowHidden, lblShowHiddenDesc,
                chkWipeHistory, chkWipeMacOs, btnSaveGeneral
            });

            tabControl.TabPages.Add(tabLoginPwd);
            tabControl.TabPages.Add(tabEncPwd);
            tabControl.TabPages.Add(tabLoginCustom);
            tabControl.TabPages.Add(tabGeneral);
            tabControl.SelectedIndex = 3; // Focus General tab by default

            this.Controls.Add(tabControl);
            
            Label lblReady = new Label { Text = "Sẵn sàng!", Location = new Point(10, 480), AutoSize = true };
            this.Controls.Add(lblReady);
        }
    }
}
