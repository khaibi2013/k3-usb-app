using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;

namespace AnToanUSB
{
    public static class IconExtractor
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_SMALLICON = 0x000000001;
        public const uint SHGFI_LARGEICON = 0x000000000;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        public const uint SHGFI_TYPENAME = 0x000000400;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern uint ExtractIconEx(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        private static Dictionary<string, Image> iconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        public static Image GetFileIcon(string path, bool isSmall = true, bool useFileAttributes = false)
        {
            string ext = "";
            if (path == "folder") ext = "folder";
            else if (path == "drive") ext = "drive";
            else if (path == "file") ext = "file";
            else if (Directory.Exists(path) && Path.GetPathRoot(path) == path) ext = "drive:" + path.ToLower();
            else ext = Path.HasExtension(path) ? Path.GetExtension(path).ToLower() : "file";

            if (iconCache.ContainsKey(ext))
            {
                return iconCache[ext];
            }

            SHFILEINFO shinfo = new SHFILEINFO();
            uint flags = SHGFI_ICON | (isSmall ? SHGFI_SMALLICON : SHGFI_LARGEICON);
            bool isExtensionOnly = path.StartsWith(".") && path.IndexOfAny(new[] { '\\', '/' }) < 0;
            if (useFileAttributes || isExtensionOnly || ext == "file") flags |= SHGFI_USEFILEATTRIBUTES;

            uint attributes = 0x80; // FILE_ATTRIBUTE_NORMAL
            if (useFileAttributes || isExtensionOnly || ext == "file")
            {
                if (ext == "folder") attributes = 0x10; // FILE_ATTRIBUTE_DIRECTORY
            }

            // If path is exactly "folder" or "drive" and we use attributes, we can pass a dummy path.
            string queryPath = path;
            if (ext == "folder" && useFileAttributes) queryPath = "dummy";
            else if (ext == "drive" && useFileAttributes) queryPath = "C:\\";
            else if (ext == "file") queryPath = "dummy";
            else if (isExtensionOnly) queryPath = "dummy" + ext;

            IntPtr res = SHGetFileInfo(queryPath, attributes, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
            {
                Icon myIcon = Icon.FromHandle(shinfo.hIcon);
                Image img = myIcon.ToBitmap();
                DestroyIcon(shinfo.hIcon);
                
                if (ext != "drive" && ext != "folder" && !string.IsNullOrEmpty(ext))
                {
                    iconCache[ext] = img;
                }
                return img;
            }
            return null;
        }

        public static Image GetNiceDriveIcon(DriveInfo drive, bool isSmall = true)
        {
            Image shellIcon = null;
            try
            {
                if (drive != null && !string.IsNullOrEmpty(drive.Name))
                    shellIcon = GetFileIcon(drive.Name, isSmall, false);
            }
            catch { }
            if (shellIcon != null) return shellIcon;

            if (drive != null && drive.DriveType == DriveType.Removable)
                return GetSystemIcon("imageres.dll", 27, isSmall) ?? GetSystemIcon("shell32.dll", 7, isSmall);
            if (drive != null && drive.DriveType == DriveType.CDRom)
                return GetSystemIcon("imageres.dll", 25, isSmall) ?? GetSystemIcon("shell32.dll", 11, isSmall);
            if (drive != null && drive.DriveType == DriveType.Network)
                return GetSystemIcon("imageres.dll", 28, isSmall) ?? GetSystemIcon("shell32.dll", 9, isSmall);

            return GetSystemIcon("imageres.dll", 32, isSmall) ?? GetSystemIcon("shell32.dll", 8, isSmall);
        }

        public static Image GetSystemIcon(string dllPath, int iconIndex, bool isSmall = false)
        {
            IntPtr largeIcon;
            IntPtr smallIcon;
            ExtractIconEx(dllPath, iconIndex, out largeIcon, out smallIcon, 1);

            IntPtr targetIcon = isSmall ? smallIcon : largeIcon;
            if (targetIcon != IntPtr.Zero)
            {
                Icon myIcon = Icon.FromHandle(targetIcon);
                Image img = myIcon.ToBitmap();
                DestroyIcon(largeIcon);
                DestroyIcon(smallIcon);
                return img;
            }
            return null;
        }
    }
}
