using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace AnToanUSB
{
    public class TextNoteEditorForm : Form
    {
        private TextBox editor;
        private Label status;
        private bool saved;

        public string EditedText { get { return editor.Text; } }
        public bool Saved { get { return saved; } }

        public TextNoteEditorForm(string title, string initialText)
        {
            Text = title;
            Size = new Size(900, 650);
            MinimumSize = new Size(720, 480);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Mist;
            Font = Theme.FontBody;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Theme.Ink };
            Label lblTitle = new Label { Text = title, Location = new Point(18, 10), Width = 680, Height = 24, Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold), ForeColor = Theme.TextInv };
            Label lblHint = new Label { Text = "Ctrl+S để lưu, Esc để đóng.", Location = new Point(19, 33), Width = 680, Height = 18, Font = Theme.FontSmall, ForeColor = Theme.TextInvMute };
            header.Controls.AddRange(new Control[] { lblTitle, lblHint });

            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Theme.White };
            Button btnSave = new Button { Text = "Lưu", Width = 110, Height = 34, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(660, 10), BackColor = Theme.Teal, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            Button btnCancel = new Button { Text = "Đóng", Width = 110, Height = 34, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(778, 10), BackColor = Theme.Mist, ForeColor = Theme.TextDark, FlatStyle = FlatStyle.Flat, Font = Theme.FontHeading };
            status = new Label { Text = "Sẵn sàng", Location = new Point(18, 18), Width = 620, Height = 20, Font = Theme.FontSmall, ForeColor = Theme.TextMute };
            btnSave.FlatAppearance.BorderSize = 0;
            btnCancel.FlatAppearance.BorderColor = Theme.MistLine;
            btnSave.Click += (s, e) => SaveAndClose();
            btnCancel.Click += (s, e) => Close();
            footer.Resize += (s, e) => {
                btnCancel.Left = footer.ClientSize.Width - btnCancel.Width - 18;
                btnSave.Left = btnCancel.Left - btnSave.Width - 10;
                status.Width = Math.Max(200, btnSave.Left - 30);
            };
            footer.Controls.AddRange(new Control[] { status, btnSave, btnCancel });

            editor = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                AcceptsReturn = true,
                AcceptsTab = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 10),
                Text = initialText ?? "",
                BorderStyle = BorderStyle.None
            };

            editor.TextChanged += (s, e) => {
                saved = false;
                status.Text = string.Format("{0} ký tự | Chưa lưu", editor.TextLength);
            };
            editor.KeyDown += (s, e) => {
                if (e.Control && e.KeyCode == Keys.S)
                {
                    SaveAndClose();
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    Close();
                }
            };

            Controls.Add(editor);
            Controls.Add(footer);
            Controls.Add(header);
        }

        private void SaveAndClose()
        {
            saved = true;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
