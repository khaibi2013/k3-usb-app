using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Linq;
using System.Drawing;

namespace AnToanUSB
{
    public class ScanResult
    {
        public string FilePath { get; set; }
        public string Status { get; set; }
        public string VirusName { get; set; }
    }

    public static class AntivirusScanner
    {
        // Local offline signatures used before handing a file to the vault.
        private static readonly HashSet<string> sha256Signatures = new HashSet<string> {
            "275a021bbfb6489e54d471899f7db9d1663fc695ec2fe2a2c4538aabf651fd0f" // EICAR SHA256
        };

        private static readonly HashSet<string> md5Signatures = new HashSet<string> {
            "44d88612fea8a8f36de82e1278abb02f" // EICAR MD5
        };

        private static readonly HashSet<string> riskyExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".bat", ".cmd", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".hta", ".scr", ".pif", ".com", ".ps1", ".psm1", ".lnk"
        };

        public static bool IsSafeFile(string filePath)
        {
            string fileName = Path.GetFileName(filePath).ToLower();
            if (fileName.EndsWith(".exe") && Path.GetFileNameWithoutExtension(fileName).Contains(".")) return false;
            string ext = Path.GetExtension(fileName);
            if (riskyExtensions.Contains(ext)) return false;

            try {
                if (new FileInfo(filePath).Length < 512) {
                    string content = File.ReadAllText(filePath);
                    if (content.Contains(@"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*")) return false;
                }
            } catch { }

            return true;
        }

        public static ScanResult ScanFileReal(string filePath)
        {
            if (!IsSafeFile(filePath)) {
                return new ScanResult { FilePath = filePath, Status = "Nhiễm", VirusName = "Suspicious.Extension.Gen" };
            }

            try {
                // Layer 2: Hash Check
                using (var sha256 = SHA256.Create())
                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(filePath)) {
                    byte[] hash = sha256.ComputeHash(stream);
                    string sha256String = System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    if (sha256Signatures.Contains(sha256String)) {
                        return new ScanResult { FilePath = filePath, Status = "Nhiễm", VirusName = "Trojan.Generic.Hash" };
                    }

                    stream.Position = 0;
                    byte[] md5Hash = md5.ComputeHash(stream);
                    string md5String = System.BitConverter.ToString(md5Hash).Replace("-", "").ToLowerInvariant();
                    if (md5Signatures.Contains(md5String)) {
                        return new ScanResult { FilePath = filePath, Status = "Nhiễm", VirusName = "Trojan.Generic.Hash" };
                    }
                }

                // Layer 3: Entropy Check for executables
                string ext = Path.GetExtension(filePath).ToLower();
                if (ext == ".exe" || ext == ".dll") {
                    if (ext == ".exe" && LooksLikeFolderIconExecutable(filePath)) {
                        return new ScanResult { FilePath = filePath, Status = "Nhiễm", VirusName = "Heur.FolderIcon.Executable" };
                    }

                    double entropy = CalculateEntropy(filePath);
                    if (entropy > 7.2) { // Highly packed/encrypted
                        return new ScanResult { FilePath = filePath, Status = "Nhiễm", VirusName = "Heur.Packed.Entropy" };
                    }
                }

                // Layer 4: Windows Defender Core integration, if available.
                string defenderThreat = ScanWithDefender(filePath);
                if (!string.IsNullOrEmpty(defenderThreat)) {
                    return new ScanResult { FilePath = filePath, Status = "Nhiễm", VirusName = defenderThreat };
                }

                // Layer 5: Optional ClamAV clamscan.exe integration.
                string clamThreat = ScanWithClamAv(filePath);
                if (!string.IsNullOrEmpty(clamThreat)) {
                    return new ScanResult { FilePath = filePath, Status = "Nhiễm", VirusName = clamThreat };
                }

            } catch {
                return new ScanResult { FilePath = filePath, Status = "Lỗi", VirusName = "Không thể đọc file" };
            }
            return new ScanResult { FilePath = filePath, Status = "Sạch", VirusName = "" };
        }

        public static bool IsEngineAvailable()
        {
            return !string.IsNullOrEmpty(FindDefenderPath());
        }

        public static string GetEngineInfo()
        {
            string defenderPath = FindDefenderPath();
            string clamPath = FindClamScanPath();
            return string.IsNullOrEmpty(defenderPath)
                ? "K3-AV Offline + Heuristic" + (string.IsNullOrEmpty(clamPath) ? ". Defender/ClamAV: không tìm thấy." : " + ClamAV: " + clamPath)
                : "K3-AV Offline + Heuristic + Windows Defender CLI: " + defenderPath + (string.IsNullOrEmpty(clamPath) ? "" : " + ClamAV: " + clamPath);
        }

        private static double CalculateEntropy(string filePath)
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            if (fileBytes.Length == 0) return 0.0;

            int[] counts = new int[256];
            foreach (byte b in fileBytes) counts[b]++;

            double entropy = 0.0;
            double length = fileBytes.Length;
            foreach (int count in counts) {
                if (count == 0) continue;
                double p = count / length;
                entropy -= p * Math.Log(p, 2);
            }
            return entropy;
        }

        private static string ScanWithDefender(string filePath)
        {
            try {
                string defenderPath = FindDefenderPath();
                if (string.IsNullOrEmpty(defenderPath)) return "";

                ProcessStartInfo psi = new ProcessStartInfo {
                    FileName = defenderPath,
                    Arguments = string.Format("-Scan -ScanType 3 -File \"{0}\" -DisableRemediation", filePath),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process proc = Process.Start(psi)) {
                    if (!proc.WaitForExit(60000)) {
                        try { proc.Kill(); } catch { }
                        return "";
                    }

                    string output = "";
                    try { output = (proc.StandardOutput.ReadToEnd() + " " + proc.StandardError.ReadToEnd()).Trim(); } catch { }
                    if (proc.ExitCode == 2 || output.IndexOf("found threats", StringComparison.OrdinalIgnoreCase) >= 0 || output.IndexOf("Threat", StringComparison.OrdinalIgnoreCase) >= 0) {
                        return "Win32.Defender.Detected";
                    }
                    return "";
                }
            } catch { return ""; }
        }

        private static string ScanWithClamAv(string filePath)
        {
            try
            {
                string clamPath = FindClamScanPath();
                if (string.IsNullOrEmpty(clamPath)) return "";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = clamPath,
                    Arguments = string.Format("--no-summary --infected \"{0}\"", filePath),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process proc = Process.Start(psi))
                {
                    if (!proc.WaitForExit(60000))
                    {
                        try { proc.Kill(); } catch { }
                        return "";
                    }

                    string output = "";
                    try { output = (proc.StandardOutput.ReadToEnd() + " " + proc.StandardError.ReadToEnd()).Trim(); } catch { }
                    if (proc.ExitCode == 1) return ExtractClamThreatName(output);
                    return "";
                }
            }
            catch { return ""; }
        }

        private static string ExtractClamThreatName(string output)
        {
            if (string.IsNullOrWhiteSpace(output)) return "ClamAV.Detected";
            int idx = output.IndexOf(" FOUND", StringComparison.OrdinalIgnoreCase);
            if (idx <= 0) return "ClamAV.Detected";
            int colon = output.LastIndexOf(':', idx);
            if (colon >= 0 && colon < idx - 1)
            {
                string name = output.Substring(colon + 1, idx - colon - 1).Trim();
                if (!string.IsNullOrEmpty(name)) return "ClamAV." + name;
            }
            return "ClamAV.Detected";
        }

        private static string FindClamScanPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = {
                Path.Combine(baseDir, @"tools\clamav\clamscan.exe"),
                Path.Combine(baseDir, @"clamav\clamscan.exe"),
                @"C:\Program Files\ClamAV\clamscan.exe",
                @"C:\Program Files (x86)\ClamAV\clamscan.exe"
            };
            foreach (string candidate in candidates)
                if (File.Exists(candidate)) return candidate;

            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    string candidate = Path.Combine(dir.Trim(), "clamscan.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
            return "";
        }

        private static bool LooksLikeFolderIconExecutable(string filePath)
        {
            try
            {
                Image fileIcon = IconExtractor.GetFileIcon(filePath, true, false);
                Image folderIcon = IconExtractor.GetFileIcon("folder", true, true);
                if (fileIcon == null || folderIcon == null) return false;
                return CompareIconSimilarity(fileIcon, folderIcon) > 0.90;
            }
            catch { return false; }
        }

        private static double CompareIconSimilarity(Image a, Image b)
        {
            using (Bitmap ba = new Bitmap(a, new Size(16, 16)))
            using (Bitmap bb = new Bitmap(b, new Size(16, 16)))
            {
                int similar = 0;
                int total = 16 * 16;
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        Color ca = ba.GetPixel(x, y);
                        Color cb = bb.GetPixel(x, y);
                        int diff = Math.Abs(ca.R - cb.R) + Math.Abs(ca.G - cb.G) + Math.Abs(ca.B - cb.B) + Math.Abs(ca.A - cb.A);
                        if (diff < 45) similar++;
                    }
                }
                return similar / (double)total;
            }
        }

        private static string FindDefenderPath()
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string platformRoot = Path.Combine(programData, @"Microsoft\Windows Defender\Platform");
            try
            {
                if (Directory.Exists(platformRoot))
                {
                    foreach (string dir in Directory.GetDirectories(platformRoot).OrderByDescending(d => d))
                    {
                        string candidate = Path.Combine(dir, "MpCmdRun.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            catch { }

            string[] candidates = {
                @"C:\Program Files\Windows Defender\MpCmdRun.exe",
                @"C:\Program Files\Microsoft Defender\MpCmdRun.exe"
            };
            foreach (string candidate in candidates)
                if (File.Exists(candidate)) return candidate;
            return "";
        }
    }
}
