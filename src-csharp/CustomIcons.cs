using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace AnToanUSB
{
    public static class CustomIcons
    {
        public static Bitmap GetAddFolderIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(Brushes.Gold, GetRoundedRect(new Rectangle(2, 10, 24, 18), 2));
                g.FillPath(Brushes.DarkGoldenrod, GetRoundedRect(new Rectangle(2, 6, 10, 8), 2));
                g.FillEllipse(Brushes.DodgerBlue, 14, 14, 16, 16);
                using (Pen p = new Pen(Color.White, 3))
                {
                    g.DrawLine(p, 22, 17, 22, 27);
                    g.DrawLine(p, 17, 22, 27, 22);
                }
            }
            return bmp;
        }

        public static Bitmap GetViewIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(Brushes.Khaki, GetRoundedRect(new Rectangle(4, 2, 20, 26), 2));
                using (Pen p = new Pen(Color.CornflowerBlue, 2))
                {
                    g.DrawLine(p, 8, 8, 20, 8);
                    g.DrawLine(p, 8, 12, 20, 12);
                    g.DrawLine(p, 8, 16, 20, 16);
                }
                g.FillEllipse(Brushes.LightCyan, 12, 18, 18, 10);
                g.DrawEllipse(Pens.DodgerBlue, 12, 18, 18, 10);
                g.FillEllipse(Brushes.DodgerBlue, 18, 20, 6, 6);
            }
            return bmp;
        }

        public static Bitmap GetDriveKeyIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(Brushes.SlateGray, GetRoundedRect(new Rectangle(4, 6, 22, 22), 3));
                g.FillEllipse(Brushes.Silver, 8, 10, 14, 14);
                using (Pen p = new Pen(Color.Gold, 3))
                {
                    g.DrawEllipse(p, 18, 14, 8, 8);
                    g.DrawLine(p, 18, 20, 10, 28);
                    g.DrawLine(p, 14, 24, 16, 28);
                }
            }
            return bmp;
        }

        public static Bitmap GetSettingsGearIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Brush gear = new SolidBrush(Color.SlateGray))
                using (Brush center = new SolidBrush(Color.White))
                using (Pen pen = new Pen(Color.FromArgb(45, 55, 72), 2))
                {
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        int x = 16 + (int)(Math.Cos(a) * 10) - 3;
                        int y = 16 + (int)(Math.Sin(a) * 10) - 3;
                        g.FillRectangle(gear, x, y, 6, 6);
                    }
                    g.FillEllipse(gear, 7, 7, 18, 18);
                    g.DrawEllipse(pen, 7, 7, 18, 18);
                    g.FillEllipse(center, 12, 12, 8, 8);
                }
            }
            return bmp;
        }

        public static Bitmap GetToolboxIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Brush box = new SolidBrush(Color.FromArgb(232, 163, 61)))
                using (Brush dark = new SolidBrush(Color.FromArgb(122, 78, 16)))
                using (Pen pen = new Pen(Color.FromArgb(80, 54, 20), 2))
                {
                    g.FillPath(box, GetRoundedRect(new Rectangle(4, 12, 24, 14), 3));
                    g.DrawPath(pen, GetRoundedRect(new Rectangle(4, 12, 24, 14), 3));
                    g.FillRectangle(dark, 13, 8, 6, 5);
                    g.DrawRectangle(pen, 11, 7, 10, 6);
                    g.DrawLine(new Pen(Color.White, 2), 7, 17, 25, 17);
                }
            }
            return bmp;
        }

        public static Bitmap GetCleanupIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen handle = new Pen(Color.SaddleBrown, 4))
                using (Brush broom = new SolidBrush(Color.FromArgb(232, 163, 61)))
                using (Brush dust = new SolidBrush(Color.SlateGray))
                {
                    g.DrawLine(handle, 23, 4, 12, 21);
                    g.FillPolygon(broom, new Point[] { new Point(8, 20), new Point(18, 25), new Point(6, 29) });
                    g.FillEllipse(dust, 20, 23, 4, 4);
                    g.FillEllipse(dust, 25, 25, 3, 3);
                }
            }
            return bmp;
        }

        public static Bitmap GetSafeUsbIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(Brushes.MediumSeaGreen, GetRoundedRect(new Rectangle(10, 10, 12, 20), 4));
                g.FillRectangle(Brushes.Silver, 12, 2, 8, 8);
                using (Pen p = new Pen(Color.White, 3))
                {
                    g.DrawLine(p, 13, 20, 16, 24);
                    g.DrawLine(p, 16, 24, 22, 14);
                }
            }
            return bmp;
        }

        public static Bitmap GetUsbLockIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Brush usb = new SolidBrush(Color.MediumSeaGreen))
                using (Brush metal = new SolidBrush(Color.Silver))
                using (Pen lockPen = new Pen(Color.White, 2))
                {
                    g.FillPath(usb, GetRoundedRect(new Rectangle(5, 11, 13, 18), 4));
                    g.FillRectangle(metal, 7, 3, 9, 8);
                    g.DrawArc(lockPen, 18, 12, 9, 9, 180, 180);
                    g.DrawRectangle(lockPen, 17, 17, 12, 10);
                    g.DrawLine(lockPen, 23, 21, 23, 25);
                }
            }
            return bmp;
        }

        public static Bitmap GetShieldCheckIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Brush shield = new SolidBrush(Color.MediumSeaGreen))
                using (Pen outline = new Pen(Color.SeaGreen, 2))
                using (Pen check = new Pen(Color.White, 3))
                {
                    Point[] pts = { new Point(16, 2), new Point(28, 8), new Point(26, 21), new Point(16, 30), new Point(6, 21), new Point(4, 8) };
                    g.FillPolygon(shield, pts);
                    g.DrawPolygon(outline, pts);
                    g.DrawLine(check, 10, 17, 15, 22);
                    g.DrawLine(check, 15, 22, 23, 11);
                }
            }
            return bmp;
        }

        public static Bitmap GetLockToggleIcon(bool locked)
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Color body = locked ? Color.FromArgb(232, 163, 61) : Color.SlateGray;
                using (Brush b = new SolidBrush(body))
                using (Pen p = new Pen(Color.White, 3))
                using (Pen dark = new Pen(Color.FromArgb(60, 70, 84), 2))
                {
                    g.FillPath(b, GetRoundedRect(new Rectangle(7, 14, 18, 14), 3));
                    g.DrawPath(dark, GetRoundedRect(new Rectangle(7, 14, 18, 14), 3));
                    g.DrawArc(dark, locked ? new Rectangle(10, 5, 12, 14) : new Rectangle(10, 5, 14, 14), 180, locked ? 180 : 140);
                    if (locked)
                    {
                        g.DrawLine(p, 16, 18, 16, 24);
                    }
                    else
                    {
                        g.DrawLine(p, 12, 21, 20, 21);
                    }
                }
            }
            return bmp;
        }

        public static Bitmap GetShredderIcon()
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(Brushes.DodgerBlue, 4, 16, 16, 8);
                g.FillEllipse(Brushes.DodgerBlue, 4, 10, 16, 8);
                g.FillEllipse(Brushes.DodgerBlue, 4, 4, 16, 8);
                g.DrawEllipse(Pens.Navy, 4, 16, 16, 8);
                g.DrawEllipse(Pens.Navy, 4, 10, 16, 8);
                g.DrawEllipse(Pens.Navy, 4, 4, 16, 8);
                using (Pen p = new Pen(Color.SaddleBrown, 4)) g.DrawLine(p, 28, 4, 20, 16);
                g.FillPolygon(Brushes.BurlyWood, new Point[] { new Point(20,16), new Point(14,28), new Point(24,30) });
            }
            return bmp;
        }

        private static GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            GraphicsPath path = new GraphicsPath();
            if (radius == 0) { path.AddRectangle(bounds); return path; }
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
