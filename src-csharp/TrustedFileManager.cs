using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace AnToanUSB
{
    public static class TrustedFileManager
    {
        private static readonly HashSet<string> trustedHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<TrustedFileEntry> trustedEntries = new List<TrustedFileEntry>();
        private static bool loaded;

        private static string TrustFilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".k3_trusted_hashes.txt"); }
        }

        public static bool IsTrusted(string filePath)
        {
            Load();
            string hash = ComputeSha256(filePath);
            return !string.IsNullOrEmpty(hash) && trustedHashes.Contains(hash);
        }

        public static bool TrustFile(string filePath)
        {
            Load();
            string hash = ComputeSha256(filePath);
            if (string.IsNullOrEmpty(hash)) return false;
            if (trustedHashes.Add(hash))
            {
                trustedEntries.Add(new TrustedFileEntry
                {
                    Hash = hash,
                    Name = Path.GetFileName(filePath),
                    AddedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
                Save();
            }
            return true;
        }

        public static List<TrustedFileEntry> GetEntries()
        {
            Load();
            return trustedEntries
                .OrderBy(e => e.Name ?? "")
                .ThenBy(e => e.Hash ?? "")
                .ToList();
        }

        public static bool RemoveHash(string hash)
        {
            Load();
            if (string.IsNullOrWhiteSpace(hash)) return false;
            bool removed = trustedHashes.Remove(hash.Trim());
            trustedEntries.RemoveAll(e => string.Equals(e.Hash, hash.Trim(), StringComparison.OrdinalIgnoreCase));
            if (removed) Save();
            return removed;
        }

        public static string ComputeSha256(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return "";
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(filePath))
                {
                    byte[] hash = sha.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch
            {
                return "";
            }
        }

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            trustedHashes.Clear();
            trustedEntries.Clear();
            try
            {
                if (!File.Exists(TrustFilePath)) return;
                foreach (string line in File.ReadAllLines(TrustFilePath))
                {
                    TrustedFileEntry entry = ParseEntry(line);
                    if (entry != null && entry.Hash.Length == 64)
                    {
                        trustedHashes.Add(entry.Hash);
                        trustedEntries.Add(entry);
                    }
                }
            }
            catch { }
        }

        private static void Save()
        {
            try
            {
                ConfigManager.PrepareManagedFileForWrite(TrustFilePath);
                File.WriteAllLines(TrustFilePath, trustedEntries.Select(e =>
                    e.Hash + "|" + Escape(e.Name) + "|" + Escape(e.AddedAt)));
                ConfigManager.HideManagedPath(TrustFilePath);
            }
            catch { }
        }

        private static TrustedFileEntry ParseEntry(string line)
        {
            string value = (line ?? "").Trim();
            if (value.Length == 0) return null;

            string[] parts = value.Split('|');
            if (parts.Length == 1)
                return new TrustedFileEntry { Hash = value, Name = "(không rõ)", AddedAt = "" };

            return new TrustedFileEntry
            {
                Hash = parts[0].Trim(),
                Name = Unescape(parts.Length > 1 ? parts[1] : ""),
                AddedAt = Unescape(parts.Length > 2 ? parts[2] : "")
            };
        }

        private static string Escape(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("|", "\\p");
        }

        private static string Unescape(string value)
        {
            return (value ?? "").Replace("\\p", "|").Replace("\\\\", "\\");
        }
    }

    public class TrustedFileEntry
    {
        public string Hash { get; set; }
        public string Name { get; set; }
        public string AddedAt { get; set; }
    }
}
