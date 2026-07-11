using System;
using System.Drawing;
using System.Windows.Forms;

namespace AnToanUSB
{
    public class VersionForm : Form
    {
        public VersionForm()
        {
            Text = "Phien ban K3";
            Size = new Size(560, 420);
            MinimumSize = new Size(520, 360);
            StartPosition = FormStartPosition.CenterParent;
            Theme.Apply(this);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Theme.Ink };
            PictureBox icon = new PictureBox { Image = CustomIcons.GetShieldCheckIcon(), SizeMode = PictureBoxSizeMode.Zoom, Location = new Point(24, 20), Size = new Size(52, 52) };
            Label title = new Label { Text = "USB An Toan K3", Location = new Point(92, 18), AutoSize = true, Font = new Font("Segoe UI Semibold", 17, FontStyle.Bold), ForeColor = Theme.TextInv };
            Label version = new Label { Text = K3Version.Display, Location = new Point(94, 56), AutoSize = true, Font = Theme.FontBody, ForeColor = Theme.TextInvMute };
            header.Controls.AddRange(new Control[] { icon, title, version });

            ListView list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BorderStyle = BorderStyle.None,
                Font = Theme.FontBody
            };
            list.Columns.Add("Changelog", 500);
            foreach (string item in K3Version.Changelog)
                list.Items.Add(new ListViewItem(item));

            Label note = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                Text = "Recovery key implementation is documented for a future crypto version; V1 keeps the existing .k3enc format.",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(18, 0, 18, 0),
                ForeColor = Theme.TextMute,
                Font = Theme.FontSmall
            };

            Controls.Add(list);
            Controls.Add(note);
            Controls.Add(header);
        }
    }
}
