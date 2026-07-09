using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AnToanUSB
{
    public class TrustedFilesForm : Form
    {
        private ListView list;

        public TrustedFilesForm()
        {
            InitializeComponent();
            LoadTrustedFiles();
        }

        private void InitializeComponent()
        {
            Text = "Phần mềm tin cậy";
            Size = new Size(820, 520);
            MinimumSize = new Size(760, 460);
            StartPosition = FormStartPosition.CenterParent;
            Theme.Apply(this);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = Theme.Ink };
            Label title = new Label
            {
                Text = "Phần mềm tin cậy",
                Location = new Point(22, 16),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 16, FontStyle.Bold),
                ForeColor = Theme.TextInv
            };
            Label sub = new Label
            {
                Text = "Cho phép các file cài đặt/phần mềm hợp lệ bị antivirus báo nhầm, dựa trên SHA-256.",
                Location = new Point(24, 50),
                AutoSize = true,
                Font = Theme.FontBody,
                ForeColor = Theme.TextInvMute
            };
            header.Controls.AddRange(new Control[] { title, sub });

            Panel actions = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Theme.White };
            Button btnAdd = MakeButton("Thêm file", 18, Theme.Teal, Color.White);
            Button btnRemove = MakeButton("Xóa khỏi tin cậy", 132, Theme.Danger, Color.White);
            btnRemove.Width = 140;
            Button btnReload = MakeButton("Nạp lại", 286, Theme.Slate2, Theme.TextInv);
            btnAdd.Click += (s, e) => AddTrustedFile();
            btnRemove.Click += (s, e) => RemoveSelected();
            btnReload.Click += (s, e) => LoadTrustedFiles();
            actions.Controls.AddRange(new Control[] { btnAdd, btnRemove, btnReload });

            list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BorderStyle = BorderStyle.None,
                Font = Theme.FontBody,
                BackColor = Theme.White
            };
            list.Columns.Add("Tên file", 220);
            list.Columns.Add("SHA-256", 430);
            list.Columns.Add("Ngày thêm", 140);

            Controls.Add(list);
            Controls.Add(actions);
            Controls.Add(header);
        }

        private Button MakeButton(string text, int x, Color back, Color fore)
        {
            Button button = new Button
            {
                Text = text,
                Location = new Point(x, 12),
                Size = new Size(100, 34),
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontHeading
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void LoadTrustedFiles()
        {
            list.Items.Clear();
            foreach (TrustedFileEntry entry in TrustedFileManager.GetEntries())
            {
                ListViewItem item = new ListViewItem(new[] { entry.Name, entry.Hash, entry.AddedAt });
                item.Tag = entry.Hash;
                list.Items.Add(item);
            }
        }

        private void AddTrustedFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Chọn phần mềm tin cậy";
                dialog.Filter = "Ứng dụng và bộ cài|*.exe;*.msi;*.bat;*.cmd;*.vbs;*.ps1|Tất cả file|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                string hash = TrustedFileManager.ComputeSha256(dialog.FileName);
                if (string.IsNullOrEmpty(hash))
                {
                    MessageBox.Show("Không thể đọc file để tính SHA-256.", "Phần mềm tin cậy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string message = "Chỉ thêm file này nếu bạn chắc chắn nguồn tải là hợp lệ.\n\n"
                    + Path.GetFileName(dialog.FileName) + "\nSHA-256: " + hash + "\n\nThêm vào danh sách tin cậy?";
                if (MessageBox.Show(message, "Xác nhận tin cậy", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                TrustedFileManager.TrustFile(dialog.FileName);
                LoadTrustedFiles();
            }
        }

        private void RemoveSelected()
        {
            if (list.SelectedItems.Count == 0)
            {
                MessageBox.Show("Chọn một mục cần xóa khỏi danh sách tin cậy.", "Phần mềm tin cậy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (ListViewItem item in list.SelectedItems)
            {
                string hash = item.Tag != null ? item.Tag.ToString() : "";
                TrustedFileManager.RemoveHash(hash);
            }
            LoadTrustedFiles();
        }
    }
}
