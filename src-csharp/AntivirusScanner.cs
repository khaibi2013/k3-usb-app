using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;

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
            string fileName = Path.GetFileName(filePath).ToLowerInvariant();
            if (fileName.EndsWith(".exe") && Path.GetFileNameWithoutExtension(fileName).Contains(".")) return false;

            string ext = Path.GetExtension(fileName);
            if (riskyExtensions.Contains(ext)) return false;

            try
            {
                if (new FileInfo(filePath).Length < 512)
                {
                    string content = File.ReadAllText(filePath);
                    if (content.Contains(@"X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*")) return false;
                }
            }
            catch { }

            return true;
        }

        public static ScanResult ScanFileReal(string filePath)
        {
            try
            {
                string yaraThreat = UsbYaraRuleScanner.ScanFile(filePath);
                if (!string.IsNullOrEmpty(yaraThreat))
                    return Infected(filePath, yaraThreat);

                if (!IsSafeFile(filePath))
                    return Infected(filePath, "Suspicious.Extension.Gen");

                using (var sha256 = SHA256.Create())
                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = sha256.ComputeHash(stream);
                    string sha256String = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    if (sha256Signatures.Contains(sha256String))
                        return Infected(filePath, "Trojan.Generic.Hash");

                    stream.Position = 0;
                    byte[] md5Hash = md5.ComputeHash(stream);
                    string md5String = BitConverter.ToString(md5Hash).Replace("-", "").ToLowerInvariant();
                    if (md5Signatures.Contains(md5String))
                        return Infected(filePath, "Trojan.Generic.Hash");
                }

                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".exe" || ext == ".dll")
                {
                    if (ext == ".exe" && LooksLikeFolderIconExecutable(filePath))
                        return Infected(filePath, "Heur.FolderIcon.Executable");

                    if (new FileInfo(filePath).Length <= 64L * 1024L * 1024L)
                    {
                        double entropy = CalculateEntropy(filePath);
                        if (entropy > 7.2)
                            return Infected(filePath, "Heur.Packed.Entropy");
                    }
                }

                string clamThreat = ScanWithClamAv(filePath);
                if (!string.IsNullOrEmpty(clamThreat))
                    return Infected(filePath, clamThreat);
            }
            catch
            {
                return new ScanResult { FilePath = filePath, Status = "Lỗi", VirusName = "Không thể đọc file" };
            }

            return new ScanResult { FilePath = filePath, Status = "Sạch", VirusName = "" };
        }

        public static bool IsEngineAvailable()
        {
            return true;
        }

        public static string GetEngineInfo()
        {
            return "K3 heuristic + " + UsbYaraRuleScanner.GetEngineInfo() + " + " + ClamAvManager.GetStatusText() + ". Windows Defender: bỏ qua.";
        }

        private static ScanResult Infected(string filePath, string virusName)
        {
            return new ScanResult { FilePath = filePath, Status = "Nhiễm", VirusName = virusName };
        }

        private static double CalculateEntropy(string filePath)
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            if (fileBytes.Length == 0) return 0.0;

            int[] counts = new int[256];
            foreach (byte b in fileBytes) counts[b]++;

            double entropy = 0.0;
            double length = fileBytes.Length;
            foreach (int count in counts)
            {
                if (count == 0) continue;
                double p = count / length;
                entropy -= p * Math.Log(p, 2);
            }
            return entropy;
        }

        private static string ScanWithClamAv(string filePath)
        {
            try
            {
                string clamPath = ClamAvManager.FindClamScanPath();
                if (string.IsNullOrEmpty(clamPath)) return "";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = clamPath,
                    Arguments = ClamAvManager.BuildClamScanArguments(filePath),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process proc = Process.Start(psi))
                {
                    if (!proc.WaitForExit(15000))
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
    }
}
