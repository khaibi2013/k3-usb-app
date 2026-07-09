using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AnToanUSB
{
    public static class AppBrand
    {
        private static Icon cachedIcon;
        private static readonly Dictionary<int, Image> cachedLogos = new Dictionary<int, Image>();

        public static string FindLogoPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string cwd = Environment.CurrentDirectory;
            string[] candidates = new string[]
            {
                Path.Combine(baseDir, "icon.png"),
                Path.Combine(baseDir, "public", "icon.png"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "public", "icon.png")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "public", "icon.png")),
                Path.Combine(cwd, "public", "icon.png")
            };

            foreach (string path in candidates)
            {
                if (File.Exists(path)) return path;
            }
            return null;
        }

        public static Image LoadLogoImage(int maxSize)
        {
            if (cachedLogos.ContainsKey(maxSize)) return (Image)cachedLogos[maxSize].Clone();

            string path = FindLogoPath();
            if (string.IsNullOrEmpty(path)) return null;

            using (Image source = Image.FromFile(path))
            {
                int width = source.Width;
                int height = source.Height;
                float scale = Math.Min((float)maxSize / width, (float)maxSize / height);
                if (scale > 1f) scale = 1f;

                int targetWidth = Math.Max(1, (int)(width * scale));
                int targetHeight = Math.Max(1, (int)(height * scale));
                Bitmap bitmap = new Bitmap(targetWidth, targetHeight);
                bitmap.SetResolution(source.HorizontalResolution, source.VerticalResolution);

                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(source, new Rectangle(0, 0, targetWidth, targetHeight));
                }
                cachedLogos[maxSize] = bitmap;
                return (Image)bitmap.Clone();
            }
        }

        public static Icon LoadWindowIcon()
        {
            if (cachedIcon != null) return cachedIcon;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string icoPath = Path.Combine(baseDir, "app.ico");
            if (File.Exists(icoPath))
            {
                cachedIcon = new Icon(icoPath);
                return cachedIcon;
            }

            using (Image img = LoadLogoImage(256))
            {
                if (img == null) return null;
                using (Bitmap bitmap = new Bitmap(img))
                {
                    IntPtr handle = bitmap.GetHicon();
                    try
                    {
                        using (Icon icon = Icon.FromHandle(handle))
                        {
                            cachedIcon = (Icon)icon.Clone();
                        }
                    }
                    finally
                    {
                        DestroyIcon(handle);
                    }
                }
            }

            return cachedIcon;
        }

        public static void ApplyWindowIcon(Form form)
        {
            try
            {
                Icon icon = LoadWindowIcon();
                if (icon != null) form.Icon = icon;
            }
            catch
            {
                // Keep the default Windows icon if the logo cannot be loaded.
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
