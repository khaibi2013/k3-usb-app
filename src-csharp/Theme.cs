using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;

namespace AnToanUSB
{
    /// <summary>
    /// Bang mau va font dung chung cho toan app, khop voi ban thiet ke.
    /// Chi can Theme.Apply(this) trong constructor cua moi Form.
    /// </summary>
    public static class Theme
    {
        // ---- Mau nen ----
        public static readonly Color Ink      = ColorTranslator.FromHtml("#12161D"); // titlebar
        public static readonly Color Slate    = ColorTranslator.FromHtml("#1C2431"); // toolbar/sidebar
        public static readonly Color Slate2   = ColorTranslator.FromHtml("#252F3F"); // nut, o nhap trong sidebar
        public static readonly Color RailLine = ColorTranslator.FromHtml("#2E3948");
        public static readonly Color Mist     = ColorTranslator.FromHtml("#F4F6F8"); // vung danh sach file
        public static readonly Color MistLine = ColorTranslator.FromHtml("#E2E6EA");
        public static readonly Color White    = Color.White;

        // ---- Mau chu ----
        public static readonly Color TextDark    = ColorTranslator.FromHtml("#1B2430");
        public static readonly Color TextMute    = ColorTranslator.FromHtml("#6C7686");
        public static readonly Color TextInv     = ColorTranslator.FromHtml("#E7EBF0"); // chu tren nen toi
        public static readonly Color TextInvMute = ColorTranslator.FromHtml("#8B96A6");

        // ---- Mau trang thai ----
        public static readonly Color Teal      = ColorTranslator.FromHtml("#15B891"); // da ma hoa / an toan
        public static readonly Color TealDark  = ColorTranslator.FromHtml("#0B6E56");
        public static readonly Color TealFill  = ColorTranslator.FromHtml("#E1F5EE");
        public static readonly Color Amber     = ColorTranslator.FromHtml("#E8A33D"); // canh bao / chua ma hoa
        public static readonly Color AmberDark = ColorTranslator.FromHtml("#7A4E10");
        public static readonly Color AmberFill = ColorTranslator.FromHtml("#FBEEDA");
        public static readonly Color Danger    = ColorTranslator.FromHtml("#DC3545");
        public static readonly Color DangerDark= ColorTranslator.FromHtml("#A71D2A");

        // ---- Font ----
        public static readonly Font FontHeading = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        public static readonly Font FontBody    = new Font("Segoe UI", 9f);
        public static readonly Font FontSmall   = new Font("Segoe UI", 8f);
        public static readonly Font FontMono    = new Font("Consolas", 8.5f);
        public static readonly Font FontTitle   = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);

        public const int Radius = 9;

        /// <summary>Ap mau nen/chu mac dinh cho ca Form (goi trong constructor).</summary>
        public static void Apply(Form form)
        {
            form.BackColor = Mist;
            form.Font = FontBody;
            AppBrand.ApplyWindowIcon(form);
        }

        /// <summary>Bo goc mem cho mot Panel/Button bat ky (dung trong Resize hoac Load).</summary>
        public static void RoundCorners(Control control, int radius)
        {
            if (control.Width < radius * 2 || control.Height < radius * 2) return;
            GraphicsPath path = new GraphicsPath();
            Rectangle rect = control.ClientRectangle;
            path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
            path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
            path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
            path.CloseFigure();
            control.Region = new Region(path);
        }

        /// <summary>Style a ToolStrip to match the dark theme.</summary>
        public static void StyleToolStrip(ToolStrip strip)
        {
            strip.BackColor = Slate;
            strip.ForeColor = TextInv;
            strip.GripStyle = ToolStripGripStyle.Hidden;
            strip.Renderer = new DarkToolStripRenderer();
        }

        /// <summary>Style a MenuStrip to match the dark theme.</summary>
        public static void StyleMenuStrip(MenuStrip strip)
        {
            strip.BackColor = Ink;
            strip.ForeColor = TextInv;
            strip.Renderer = new DarkToolStripRenderer();
        }
    }

    /// <summary>Custom renderer for dark ToolStrip/MenuStrip.</summary>
    public class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                using (SolidBrush brush = new SolidBrush(Theme.Slate2))
                {
                    e.Graphics.FillRectangle(brush, e.Item.ContentRectangle);
                }
            }
            else
            {
                base.OnRenderMenuItemBackground(e);
            }
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush(e.ToolStrip is MenuStrip ? Theme.Ink : Theme.Slate))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected || e.Item.Pressed)
            {
                using (SolidBrush brush = new SolidBrush(Theme.Slate2))
                {
                    e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Theme.TextInv;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (Pen pen = new Pen(Theme.RailLine))
            {
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 0, y, e.Item.Width, y);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // No border
        }
    }

    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Theme.Slate2; } }
        public override Color MenuItemBorder { get { return Theme.RailLine; } }
        public override Color MenuBorder { get { return Theme.RailLine; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Slate2; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Slate2; } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.Slate; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.Slate; } }
        public override Color MenuStripGradientBegin { get { return Theme.Ink; } }
        public override Color MenuStripGradientEnd { get { return Theme.Ink; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Slate; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Slate; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Slate; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Slate; } }
        public override Color SeparatorDark { get { return Theme.RailLine; } }
        public override Color SeparatorLight { get { return Theme.RailLine; } }
    }

    /// <summary>
    /// Nut flat, bo tron, dung cho toolbar va CTA "Ma hoa USB".
    /// </summary>
    public enum ButtonVariant { Toolbar, Primary, Warning, Danger }

    public class ThemedButton : Button
    {
        private ButtonVariant _variant = ButtonVariant.Toolbar;
        public ButtonVariant Variant
        {
            get { return _variant; }
            set { _variant = value; ApplyVariant(); }
        }

        public ThemedButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 1;
            Font = Theme.FontHeading;
            Cursor = Cursors.Hand;
            Height = 38;
            Padding = new Padding(12, 0, 12, 0);
            Resize += delegate { if (Width > 18 && Height > 18) Theme.RoundCorners(this, Theme.Radius); };
            ApplyVariant();
        }

        private void ApplyVariant()
        {
            switch (_variant)
            {
                case ButtonVariant.Warning:
                    BackColor = Theme.Amber;
                    ForeColor = ColorTranslator.FromHtml("#3E2405");
                    FlatAppearance.BorderColor = ColorTranslator.FromHtml("#A46A1E");
                    break;
                case ButtonVariant.Primary:
                    BackColor = Theme.Teal;
                    ForeColor = Color.White;
                    FlatAppearance.BorderColor = Theme.TealDark;
                    break;
                case ButtonVariant.Danger:
                    BackColor = Theme.Danger;
                    ForeColor = Color.White;
                    FlatAppearance.BorderColor = Theme.DangerDark;
                    break;
                default:
                    BackColor = Theme.Slate2;
                    ForeColor = Theme.TextInvMute;
                    FlatAppearance.BorderColor = Theme.RailLine;
                    break;
            }
            FlatAppearance.MouseOverBackColor = ControlPaint.Light(BackColor, 0.15f);
        }
    }

    /// <summary>
    /// ListView co ve them huy hieu o khoa cho cac dong la file/thu muc da ma hoa.
    /// </summary>
    public class VaultListView : ListView
    {
        public HashSet<string> EncryptedNames { get; set; }

        public VaultListView()
        {
            EncryptedNames = new HashSet<string>();
            OwnerDraw = true;
            View = View.Details;
            FullRowSelect = true;
            GridLines = false;
            BackColor = Theme.Mist;
            ForeColor = Theme.TextDark;
            Font = Theme.FontBody;
            BorderStyle = BorderStyle.None;

            DrawColumnHeader += OnDrawColumnHeader;
            DrawItem += OnDrawItem;
            DrawSubItem += OnDrawSubItem;
        }

        private void OnDrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (SolidBrush bg = new SolidBrush(Theme.White))
            {
                e.Graphics.FillRectangle(bg, e.Bounds);
            }
            using (Pen borderPen = new Pen(Theme.MistLine))
            {
                e.Graphics.DrawLine(borderPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
            TextRenderer.DrawText(e.Graphics, e.Header.Text, Theme.FontHeading, e.Bounds,
                Theme.TextMute, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        private void OnDrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;
        }

        private void OnDrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            bool selected = e.Item.Selected;
            using (SolidBrush bg = new SolidBrush(selected ? Theme.TealFill : (e.ItemIndex % 2 == 0 ? Theme.White : Theme.Mist)))
            {
                e.Graphics.FillRectangle(bg, e.Bounds);
            }

            // Draw bottom border
            using (Pen borderPen = new Pen(Theme.MistLine))
            {
                e.Graphics.DrawLine(borderPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            Rectangle textBounds = e.Bounds;
            textBounds.X += 4;
            textBounds.Width -= 8;
            bool isNameColumn = e.ColumnIndex == 0;

            // Draw lock icon for encrypted files
            if (isNameColumn && e.Item.Text != null && EncryptedNames.Contains(e.Item.Text))
            {
                int lockSize = 14;
                int lockX = e.Bounds.Right - lockSize - 8;
                int lockY = e.Bounds.Top + (e.Bounds.Height - lockSize) / 2;
                using (Pen pen = new Pen(Theme.TealDark, 1.6f))
                {
                    // Lock body
                    e.Graphics.DrawRectangle(pen, lockX, lockY + 5, lockSize, lockSize - 5);
                    // Lock shackle
                    e.Graphics.DrawArc(pen, lockX + 2, lockY, lockSize - 4, 10, 180, 180);
                }
                // Fill lock body
                using (SolidBrush tealBrush = new SolidBrush(Theme.TealFill))
                {
                    e.Graphics.FillRectangle(tealBrush, lockX + 1, lockY + 6, lockSize - 1, lockSize - 6);
                }
                textBounds.Width -= lockSize + 12;
            }

            Color textColor = isNameColumn ? Theme.TextDark : Theme.TextMute;
            if (selected) textColor = Theme.TealDark;

            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Theme.FontBody, textBounds,
                textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>
    /// Thanh tieu de tuy chinh (WinForms khong cho doi mau titlebar he thong).
    /// Dat FormBorderStyle = None tren Form, roi them ThemedTitleBar lam control dau tien, Dock = Top.
    /// </summary>
    public class ThemedTitleBar : Panel
    {
        private Label _title;
        private Panel _btnClose;
        private Panel _btnMax;
        private Panel _btnMin;

        public ThemedTitleBar(string title)
        {
            Height = 44;
            Dock = DockStyle.Top;
            BackColor = Theme.Ink;
            DoubleBuffered = true;

            _title = new Label
            {
                Text = title,
                ForeColor = Theme.TextInv,
                Font = Theme.FontHeading,
                AutoSize = true,
                Location = new Point(14, 13),
                BackColor = Color.Transparent
            };
            Controls.Add(_title);

            _btnClose = MakeDot(ColorTranslator.FromHtml("#E34D4D"));
            _btnMax   = MakeDot(ColorTranslator.FromHtml("#3A4454"));
            _btnMin   = MakeDot(ColorTranslator.FromHtml("#3A4454"));
            Controls.AddRange(new Control[] { _btnClose, _btnMax, _btnMin });

            Resize += delegate { LayoutDots(); };
            LayoutDots();

            // Drag form by title bar
            MouseDown += delegate { DragForm(); };
            _title.MouseDown += delegate { DragForm(); };

            _btnClose.Click += delegate
            {
                Form f = FindForm();
                if (f != null) f.Close();
            };
            _btnMax.Click += delegate
            {
                Form f = FindForm();
                if (f != null) f.WindowState = f.WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal : FormWindowState.Maximized;
            };
            _btnMin.Click += delegate
            {
                Form f = FindForm();
                if (f != null) f.WindowState = FormWindowState.Minimized;
            };

            // Hover effects
            _btnClose.MouseEnter += delegate { _btnClose.BackColor = ColorTranslator.FromHtml("#FF6B6B"); };
            _btnClose.MouseLeave += delegate { _btnClose.BackColor = ColorTranslator.FromHtml("#E34D4D"); };
        }

        private Panel MakeDot(Color c)
        {
            Panel p = new Panel { Width = 13, Height = 13, BackColor = c, Cursor = Cursors.Hand };
            p.Resize += delegate { Theme.RoundCorners(p, 7); };
            return p;
        }

        private void LayoutDots()
        {
            int y = (Height - 13) / 2;
            _btnClose.Location = new Point(Width - 13 - 14, y);
            _btnMax.Location   = new Point(Width - 13 - 14 - 22, y);
            _btnMin.Location   = new Point(Width - 13 - 14 - 44, y);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private void DragForm()
        {
            Form f = FindForm();
            if (f == null) return;
            ReleaseCapture();
            SendMessage(f.Handle, 0xA1, 2, 0);
        }
    }

    /// <summary>
    /// Panel with rounded corners and shadow effect.
    /// </summary>
    public class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; }
        public Color BorderColor { get; set; }

        public RoundedPanel()
        {
            CornerRadius = Theme.Radius;
            BorderColor = Theme.MistLine;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            GraphicsPath path = new GraphicsPath();
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int r = CornerRadius;
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
            path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            using (SolidBrush brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillPath(brush, path);
            }
            using (Pen pen = new Pen(BorderColor))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }
    }
}
