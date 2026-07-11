using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AnToanUSB
{
    public class K3IntegrityResult
    {
        public bool ManifestExists { get; set; }
        public int Checked { get; set; }
        public List<string> Failed { get; private set; }
        public List<string> Missing { get; private set; }

        public K3IntegrityResult()
        {
            Failed = new List<string>();
            Missing = new List<string>();
        }

        public bool HasWarning
        {
            get { return !ManifestExists || Failed.Count > 0 || Missing.Count > 0; }
        }
    }

    public static class K3IntegrityManager
    {
        private static readonly string[] TrackedPaths = new string[]
        {
            "AnToanUSB.exe",
            "K3 Mac.app/Contents/MacOS/K3UsbSafeMac",
            "tools/rules/k3-rules.json"
        };

        public static string ManifestPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".k3_integrity_manifest.json"); }
        }

        public static void WriteManifest()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"version\": 1,");
            sb.AppendLine("  \"generated_at\": \"" + Escape(DateTime.UtcNow.ToString("o")) + "\",");
            sb.AppendLine("  \"files\": [");

            bool first = true;
            foreach (string rel in TrackedPaths)
            {
                string full = Path.Combine(baseDir, rel);
                if (!File.Exists(full)) continue;
                FileInfo info = new FileInfo(full);
                if (!first) sb.AppendLine(",");
                first = false;
                sb.Append("    { \"path\": \"" + Escape(rel.Replace('\\', '/')) + "\", \"sha256\": \"" + Sha256(full) + "\", \"size\": " + info.Length + " }");
            }

            sb.AppendLine();
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            ConfigManager.PrepareManagedFileForWrite(ManifestPath);
            File.WriteAllText(ManifestPath, sb.ToString(), new UTF8Encoding(false));
            ConfigManager.HideManagedPath(ManifestPath);
        }

        public static K3IntegrityResult VerifyManifest()
        {
            K3IntegrityResult result = new K3IntegrityResult();
            if (!File.Exists(ManifestPath)) return result;
            result.ManifestExists = true;

            string json = File.ReadAllText(ManifestPath);
            MatchCollection matches = Regex.Matches(json, "\\{\\s*\"path\"\\s*:\\s*\"(?<path>[^\"]+)\"\\s*,\\s*\"sha256\"\\s*:\\s*\"(?<sha>[a-fA-F0-9]{64})\"\\s*,\\s*\"size\"\\s*:\\s*(?<size>\\d+)\\s*\\}");
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            foreach (Match match in matches)
            {
                string rel = Unescape(match.Groups["path"].Value).Replace('/', Path.DirectorySeparatorChar);
                string full = Path.Combine(baseDir, rel);
                long expectedSize;
                long.TryParse(match.Groups["size"].Value, out expectedSize);
                if (!File.Exists(full))
                {
                    result.Missing.Add(rel);
                    continue;
                }
                FileInfo info = new FileInfo(full);
                string actual = Sha256(full);
                if (!string.Equals(actual, match.Groups["sha"].Value, StringComparison.OrdinalIgnoreCase) || info.Length != expectedSize)
                    result.Failed.Add(rel);
                else
                    result.Checked++;
            }
            return result;
        }

        public static string Sha256(string filePath)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static string Escape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string Unescape(string value)
        {
            return (value ?? "").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }
}
