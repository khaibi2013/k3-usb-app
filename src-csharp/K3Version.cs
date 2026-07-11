using System;

namespace AnToanUSB
{
    public static class K3Version
    {
        public const string AppVersion = "0.4.1-win";
        public const string BuildDate = "2026-07-11";
        public const string Channel = "portable";

        public static string Display
        {
            get { return AppVersion + " (" + Channel + ", " + BuildDate + ")"; }
        }

        public static readonly string[] Changelog = new string[]
        {
            "Windows first-run wizard and login lockout policy",
            "Folder encryption with structure preservation",
            "USB integrity manifest",
            "K3 rule updater from URL/file",
            "HTML scan report export",
            "Quarantine trust and restore",
            "Auto scan on login"
        };
    }
}
