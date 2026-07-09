using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AnToanUSB
{
    public enum ThreatAlertAction
    {
        Close,
        Quarantine,
        OpenFolder,
        TrustFile
    }

    public class ThreatAlertDialog : Form
    {
        private readonly string filePath;
        private readonly string reason;
        public ThreatAlertAction SelectedAction { get; private set; }

        public ThreatAlertDialog(string filePath, string reason, bool allowQuarantine)
        {
            this.filePath = filePath;
            this.reason = reason;
            SelectedAction = ThreatAlertAction.Close;

            Text = "K3-AV cảnh báo";
            Size = new Size(700, 350);
            MinimumSize = new Size(640, 320);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Theme.Apply(this);

            Panel accent = new Panel { Dock = DockStyle.Left, Width = 8, BackColor = Theme.Danger };
            Label icon = new Label
            {
                Text = "!",
                Location = new Point(34, 34),
                Size = new Size(58, 58),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Theme.Danger
            };

            Label title = new Label
            {
                Text = "Đã chặn tệp trước khi đưa vào USB/Két sắt",
                Location = new Point(112, 30),
                Size = new Size(540, 30),
                Font = Theme.FontTitle,
                ForeColor = Theme.TextDark
            };

            Label body = new Label
            {
                Text = "K3-AV phát hiện dấu hiệu nguy hiểm nên tệp này chưa được sao chép hoặc mã hóa vào USB.",
                Location = new Point(114, 64),
                Size = new Size(530, 46),
                Font = Theme.FontBody,
                ForeColor = Theme.TextMute
            };

            TextBox details = new TextBox
            {
                Location = new Point(34, 122),
                Size = new Size(618, 86),
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Font = Theme.FontMono,
                Text = "Tệp: " + filePath + "\r\nLý do: " + (string.IsNullOrWhiteSpace(reason) ? "Không rõ" : reason)
            };

            Button btnQuarantine = MakeButton("Đưa vào khu cách ly", Theme.Amber, Color.White, Theme.AmberDark);
            btnQuarantine.Location = new Point(80, 240);
            btnQuarantine.Width = 170;
            btnQuarantine.Enabled = allowQuarantine && File.Exists(filePath);
            btnQuarantine.Click += (s, e) => { SelectedAction = ThreatAlertAction.Quarantine; DialogResult = DialogResult.OK; Close(); };

            Button btnTrust = MakeButton("Tin cậy file này", Theme.Teal, Color.White, Theme.TealDark);
            btnTrust.Location = new Point(270, 240);
            btnTrust.Width = 140;
            btnTrust.Enabled = File.Exists(filePath);
            btnTrust.Click += (s, e) => { SelectedAction = ThreatAlertAction.TrustFile; DialogResult = DialogResult.OK; Close(); };

            Button btnFolder = MakeButton("Mở thư mục", Color.White, Theme.TextDark, Theme.MistLine);
            btnFolder.Location = new Point(424, 240);
            btnFolder.Click += (s, e) => { SelectedAction = ThreatAlertAction.OpenFolder; DialogResult = DialogResult.OK; Close(); };

            Button btnClose = MakeButton("Đóng", Color.White, Theme.TextDark, Theme.MistLine);
            btnClose.Location = new Point(550, 240);
            btnClose.Click += (s, e) => { SelectedAction = ThreatAlertAction.Close; DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(accent);
            Controls.Add(icon);
            Controls.Add(title);
            Controls.Add(body);
            Controls.Add(details);
            Controls.Add(btnQuarantine);
            Controls.Add(btnTrust);
            Controls.Add(btnFolder);
            Controls.Add(btnClose);
            AcceptButton = btnClose;
            CancelButton = btnClose;
        }

        private Button MakeButton(string text, Color backColor, Color foreColor, Color borderColor)
        {
            Button button = new Button
            {
                Text = text,
                Width = 112,
                Height = 38,
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = foreColor,
                Font = Theme.FontHeading
            };
            button.FlatAppearance.BorderColor = borderColor;
            button.FlatAppearance.BorderSize = 1;
            return button;
        }

        public void ExecuteSelectedAction()
        {
            if (SelectedAction == ThreatAlertAction.OpenFolder)
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            }
            else if (SelectedAction == ThreatAlertAction.Quarantine)
            {
                QuarantineManager.QuarantineFile(filePath, reason);
            }
            else if (SelectedAction == ThreatAlertAction.TrustFile)
            {
                TrustedFileManager.TrustFile(filePath);
            }
        }
    }
}
