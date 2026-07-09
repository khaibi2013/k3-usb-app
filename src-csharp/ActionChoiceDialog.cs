using System;
using System.Drawing;
using System.Windows.Forms;

namespace AnToanUSB
{
    public class ActionChoice
    {
        public string Id;
        public string Title;
        public string Description;
        public Image Icon;
        public Color Accent;

        public ActionChoice(string id, string title, string description, Image icon, Color accent)
        {
            Id = id;
            Title = title;
            Description = description;
            Icon = icon;
            Accent = accent;
        }
    }

    public class ActionChoiceDialog : Form
    {
        private string selectedId = "";

        public string SelectedId { get { return selectedId; } }

        public ActionChoiceDialog(string title, string subtitle, params ActionChoice[] choices)
        {
            Text = title;
            Size = new Size(620, 180 + choices.Length * 82);
            MinimumSize = new Size(560, 300);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Theme.Mist;
            Font = Theme.FontBody;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = Theme.Ink };
            Label lblTitle = new Label { Text = title, Location = new Point(24, 18), AutoSize = true, Font = new Font("Segoe UI Semibold", 15, FontStyle.Bold), ForeColor = Theme.TextInv };
            Label lblSub = new Label { Text = subtitle, Location = new Point(26, 50), Width = 550, Height = 26, Font = Theme.FontBody, ForeColor = Theme.TextInvMute };
            header.Controls.AddRange(new Control[] { lblTitle, lblSub });

            FlowLayoutPanel body = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Theme.Mist };
            foreach (ActionChoice choice in choices)
                body.Controls.Add(CreateChoiceButton(choice));

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = Theme.White };
            Button cancel = new Button { Text = "Hủy", Width = 110, Height = 34, Location = new Point(Width - 150, 12), Anchor = AnchorStyles.Top | AnchorStyles.Right, FlatStyle = FlatStyle.Flat, BackColor = Theme.Mist, ForeColor = Theme.TextDark, Font = Theme.FontHeading };
            cancel.FlatAppearance.BorderColor = Theme.MistLine;
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            footer.Controls.Add(cancel);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);
        }

        private Control CreateChoiceButton(ActionChoice choice)
        {
            Panel card = new Panel { Width = 552, Height = 70, BackColor = Theme.White, Margin = new Padding(0, 0, 0, 10), Cursor = Cursors.Hand };
            card.Paint += (s, e) => {
                using (Pen pen = new Pen(Theme.MistLine))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                using (Brush brush = new SolidBrush(choice.Accent))
                    e.Graphics.FillRectangle(brush, 0, 0, 5, card.Height);
            };

            PictureBox icon = new PictureBox { Image = choice.Icon, SizeMode = PictureBoxSizeMode.Zoom, Location = new Point(18, 17), Size = new Size(36, 36), BackColor = Color.Transparent };
            Label title = new Label { Text = choice.Title, Location = new Point(68, 13), Width = 430, Height = 24, Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold), ForeColor = Theme.TextDark, BackColor = Color.Transparent };
            Label desc = new Label { Text = choice.Description, Location = new Point(69, 38), Width = 440, Height = 22, Font = Theme.FontSmall, ForeColor = Theme.TextMute, BackColor = Color.Transparent };

            EventHandler choose = (s, e) => {
                selectedId = choice.Id;
                DialogResult = DialogResult.OK;
                Close();
            };
            card.Click += choose;
            icon.Click += choose;
            title.Click += choose;
            desc.Click += choose;

            card.Controls.AddRange(new Control[] { icon, title, desc });
            return card;
        }
    }
}
