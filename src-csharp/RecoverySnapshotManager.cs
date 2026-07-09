using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace AnToanUSB
{
    public class RecoverySnapshotResult
    {
        public string SnapshotDir { get; set; }
        public string ManifestPath { get; set; }
        public int ItemCount { get; set; }
        public int BackedUpFileCount { get; set; }
        public long BackedUpBytes { get; set; }
    }

    public static class RecoverySnapshotManager
    {
        private const long MaxSingleBackupBytes = 10L * 1024 * 1024;
        private const long MaxTotalBackupBytes = 250L * 1024 * 1024;
        private const long MaxHashBytes = 50L * 1024 * 1024;

        private static readonly HashSet<string> dataExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".md", ".csv", ".json", ".xml", ".ini", ".log",
            ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
            ".zip", ".rar", ".7z"
        };

        private static readonly HashSet<string> executableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".msi", ".bat", ".cmd", ".vbs", ".vbe", ".js", ".jse",
            ".wsf", ".wsh", ".hta", ".scr", ".pif", ".com", ".ps1", ".psm1", ".lnk"
        };

        public static string SnapshotRoot
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "K3_RecoverySnapshots"); }
        }

        public static RecoverySnapshotResult CreateSnapshot(string targetPath)
        {
            RecoverySnapshotResult result = new RecoverySnapshotResult();
            if (string.IsNullOrEmpty(targetPath) || (!File.Exists(targetPath) && !Directory.Exists(targetPath)))
                return result;

            Directory.CreateDirectory(SnapshotRoot);
            string snapshotName = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + SanitizeName(Path.GetFileName(targetPath.TrimEnd('\\', '/')));
            if (string.IsNullOrEmpty(snapshotName)) snapshotName = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_scan";

            string snapshotDir = Path.Combine(SnapshotRoot, snapshotName);
            string backupDir = Path.Combine(snapshotDir, "files");
            Directory.CreateDirectory(snapshotDir);
            Directory.CreateDirectory(backupDir);

            result.SnapshotDir = snapshotDir;
            result.ManifestPath = Path.Combine(snapshotDir, "manifest.csv");

            string root = File.Exists(targetPath) ? Path.GetDirectoryName(targetPath) : targetPath;
            if (string.IsNullOrEmpty(root)) root = Path.GetPathRoot(targetPath);

            using (StreamWriter writer = new StreamWriter(result.ManifestPath, false, new System.Text.UTF8Encoding(true)))
            {
                writer.WriteLine("Type,RelativePath,Size,LastWriteTimeUtc,Attributes,SHA256,BackedUp");

                if (File.Exists(targetPath))
                    AddFile(writer, targetPath, root, backupDir, result);
                else
                {
                    AddDirectory(writer, targetPath, root, result);
                    foreach (string dir in SafeGetDirectories(targetPath))
                        AddDirectory(writer, dir, root, result);
                    foreach (string file in SafeGetFiles(targetPath))
                        AddFile(writer, file, root, backupDir, result);
                }
            }

            return result;
        }

        private static void AddDirectory(StreamWriter writer, string dir, string root, RecoverySnapshotResult result)
        {
            if (ShouldSkipPath(dir)) return;
            try
            {
                DirectoryInfo info = new DirectoryInfo(dir);
                writer.WriteLine(string.Join(",", new string[]
                {
                    "DIR",
                    Csv(RelativePath(root, dir)),
                    "0",
                    Csv(info.LastWriteTimeUtc.ToString("o")),
                    Csv(info.Attributes.ToString()),
                    "",
                    "false"
                }));
                result.ItemCount++;
            }
            catch { }
        }

        private static void AddFile(StreamWriter writer, string file, string root, string backupDir, RecoverySnapshotResult result)
        {
            if (ShouldSkipPath(file)) return;
            try
            {
                FileInfo info = new FileInfo(file);
                string hash = info.Length <= MaxHashBytes ? HashFile(file) : "";
                bool backedUp = TryBackupFile(file, root, backupDir, info, result);

                writer.WriteLine(string.Join(",", new string[]
                {
                    "FILE",
                    Csv(RelativePath(root, file)),
                    info.Length.ToString(),
                    Csv(info.LastWriteTimeUtc.ToString("o")),
                    Csv(info.Attributes.ToString()),
                    Csv(hash),
                    backedUp ? "true" : "false"
                }));
                result.ItemCount++;
            }
            catch { }
        }

        private static bool TryBackupFile(string file, string root, string backupDir, FileInfo info, RecoverySnapshotResult result)
        {
            if (!dataExtensions.Contains(info.Extension)) return false;
            if (executableExtensions.Contains(info.Extension)) return false;
            if (info.Length > MaxSingleBackupBytes) return false;
            if (result.BackedUpBytes + info.Length > MaxTotalBackupBytes) return false;

            try
            {
                string rel = RelativePath(root, file);
                string dest = Path.Combine(backupDir, rel);
                string destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                File.Copy(file, dest, true);
                result.BackedUpFileCount++;
                result.BackedUpBytes += info.Length;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<string> SafeGetFiles(string root)
        {
            Stack<string> dirs = new Stack<string>();
            dirs.Push(root);
            while (dirs.Count > 0)
            {
                string dir = dirs.Pop();
                string[] files = new string[0];
                string[] children = new string[0];
                try { files = Directory.GetFiles(dir); } catch { }
                try { children = Directory.GetDirectories(dir); } catch { }
                foreach (string file in files) yield return file;
                foreach (string child in children) if (!ShouldSkipPath(child)) dirs.Push(child);
            }
        }

        private static IEnumerable<string> SafeGetDirectories(string root)
        {
            Stack<string> dirs = new Stack<string>();
            dirs.Push(root);
            while (dirs.Count > 0)
            {
                string dir = dirs.Pop();
                string[] children = new string[0];
                try { children = Directory.GetDirectories(dir); } catch { }
                foreach (string child in children)
                {
                    if (ShouldSkipPath(child)) continue;
                    yield return child;
                    dirs.Push(child);
                }
            }
        }

        private static bool ShouldSkipPath(string path)
        {
            try
            {
                string name = Path.GetFileName(path);
                if (string.Equals(name, ".vault", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(name, ".vault_decoy", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(name, "K3_RecoverySnapshots", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(name, ".k3_trusted_hashes.txt", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }
            return false;
        }

        private static string HashFile(string file)
        {
            try
            {
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(file))
                {
                    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                }
            }
            catch { return ""; }
        }

        private static string RelativePath(string root, string path)
        {
            try
            {
                string fullRoot = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                string fullPath = Path.GetFullPath(path);
                if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                    return fullPath.Substring(fullRoot.Length);
            }
            catch { }
            return Path.GetFileName(path);
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "scan";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
