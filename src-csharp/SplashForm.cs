using System.Drawing;
using System.Windows.Forms;

namespace AnToanUSB
{
    public class SplashForm : Form
    {
        public SplashForm()
        {
            Text = "USB An Toan K3";
            Size = new Size(420, 300);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.White;
            ShowInTaskbar = false;
            AppBrand.ApplyWindowIcon(this);

            Panel frame = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(28)
            };

            PictureBox logo = new PictureBox
            {
                Size = new Size(128, 128),
                Location = new Point((Width - 128) / 2, 42),
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = AppBrand.LoadLogoImage(128)
            };

            Label title = new Label
            {
                Text = "USB An Toan K3",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
                ForeColor = Theme.TextDark,
                Location = new Point(24, 178),
                Size = new Size(372, 36)
            };

            Label subtitle = new Label
            {
                Text = "Dang khoi dong he thong bao mat...",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.FontBody,
                ForeColor = Theme.TextMute,
                Location = new Point(24, 218),
                Size = new Size(372, 24)
            };

            Panel line = new Panel
            {
                BackColor = Theme.Teal,
                Location = new Point(130, 254),
                Size = new Size(160, 4)
            };

            frame.Controls.Add(logo);
            frame.Controls.Add(title);
            frame.Controls.Add(subtitle);
            frame.Controls.Add(line);
            Controls.Add(frame);
        }
    }
}
