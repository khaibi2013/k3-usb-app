using System;
using System.IO;
using System.Text;

namespace AnToanUSB
{
    public static class UsbYaraRuleScanner
    {
        private const int MaxBytesToRead = 1024 * 1024;

        public static string ScanFile(string filePath)
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                string lowerName = fileName.ToLowerInvariant();
                string ext = Path.GetExtension(filePath).ToLowerInvariant();

                if (LooksLikeUsbFolderImpersonator(lowerName, ext))
                    return "YARA.USB.FolderImpersonator";

                if (ext == ".inf" && lowerName == "autorun.inf")
                {
                    string text = ReadTextSample(filePath);
                    if (IsSuspiciousAutorun(text))
                        return "YARA.USB.SuspiciousAutorun";
                }

                if (IsScriptLike(ext))
                {
                    string text = ReadTextSample(filePath);
                    string rule = MatchSuspiciousScript(text);
                    if (!string.IsNullOrEmpty(rule)) return rule;
                }

                if (ext == ".lnk")
                {
                    string text = ReadBinaryTextSample(filePath);
                    string rule = MatchSuspiciousShortcut(text);
                    if (!string.IsNullOrEmpty(rule)) return rule;
                }
            }
            catch { }

            return "";
        }

        public static string GetEngineInfo()
        {
            return "USB YARA rules: internal portable rule set";
        }

        private static bool LooksLikeUsbFolderImpersonator(string lowerName, string ext)
        {
            if (ext != ".exe" && ext != ".scr" && ext != ".com") return false;
            if (lowerName.EndsWith(".pdf.exe") || lowerName.EndsWith(".doc.exe") || lowerName.EndsWith(".docx.exe") ||
                lowerName.EndsWith(".xls.exe") || lowerName.EndsWith(".xlsx.exe") || lowerName.EndsWith(".jpg.exe") ||
                lowerName.EndsWith(".png.exe") || lowerName.EndsWith(".txt.exe"))
                return true;

            return lowerName == "folder.exe" || lowerName == "new folder.exe" || lowerName == "my documents.exe" ||
                   lowerName == "documents.exe" || lowerName == "pictures.exe";
        }

        private static bool IsSuspiciousAutorun(string text)
        {
            string lower = Normalize(text);
            if (lower.IndexOf("antoanusb.exe", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            return ContainsAny(lower,
                "open=", "shellexecute=", "shell\\open\\command=") &&
                ContainsAny(lower,
                    ".vbs", ".js", ".jse", ".hta", ".bat", ".cmd", ".scr", "powershell", "wscript", "cscript", "mshta", "rundll32", "regsvr32");
        }

        private static bool IsScriptLike(string ext)
        {
            return ext == ".bat" || ext == ".cmd" || ext == ".vbs" || ext == ".vbe" || ext == ".js" ||
                   ext == ".jse" || ext == ".wsf" || ext == ".wsh" || ext == ".hta" || ext == ".ps1" ||
                   ext == ".psm1";
        }

        private static string MatchSuspiciousScript(string text)
        {
            string lower = Normalize(text);
            if (ContainsAny(lower, "powershell -enc", "powershell.exe -enc", "-encodedcommand", "frombase64string"))
                return "YARA.Script.EncodedPowerShell";

            if (ContainsAny(lower, "certutil -urlcache", "bitsadmin /transfer", "invoke-webrequest", "downloadstring(", "xmlhttp"))
                return "YARA.Script.Downloader";

            if (ContainsAny(lower, "wscript.shell", "shell.application") &&
                ContainsAny(lower, "run\\", "startup", "regwrite", "createobject"))
                return "YARA.Script.Persistence";

            if (ContainsAny(lower, "attrib +h +s", "attrib +s +h", "system volume information") &&
                ContainsAny(lower, "copy ", "xcopy ", "robocopy ", "shortcut", ".lnk"))
                return "YARA.USB.HideAndShortcut";

            if (ContainsAny(lower, "regsvr32 /s", "rundll32 ", "mshta ", "schtasks /create"))
                return "YARA.Script.LOLBinExecution";

            return "";
        }

        private static string MatchSuspiciousShortcut(string text)
        {
            string lower = Normalize(text);
            if (ContainsAny(lower, "powershell", "wscript", "cscript", "mshta", "cmd.exe", "rundll32", "regsvr32") &&
                ContainsAny(lower, "-enc", "system volume information", ".vbs", ".js", ".hta", ".bat", ".cmd", ".scr", "hidden"))
                return "YARA.LNK.UsbShortcutPayload";

            return "";
        }

        private static string ReadTextSample(string filePath)
        {
            byte[] bytes = ReadBytes(filePath);
            string utf8 = Encoding.UTF8.GetString(bytes);
            string unicode = Encoding.Unicode.GetString(bytes);
            return utf8 + "\n" + unicode;
        }

        private static string ReadBinaryTextSample(string filePath)
        {
            byte[] bytes = ReadBytes(filePath);
            string ascii = Encoding.ASCII.GetString(bytes);
            string unicode = Encoding.Unicode.GetString(bytes);
            return ascii + "\n" + unicode;
        }

        private static byte[] ReadBytes(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            {
                int count = (int)Math.Min(stream.Length, MaxBytesToRead);
                byte[] bytes = new byte[count];
                int offset = 0;
                while (offset < count)
                {
                    int read = stream.Read(bytes, offset, count - offset);
                    if (read <= 0) break;
                    offset += read;
                }
                if (offset == count) return bytes;

                byte[] trimmed = new byte[offset];
                Buffer.BlockCopy(bytes, 0, trimmed, 0, offset);
                return trimmed;
            }
        }

        private static string Normalize(string text)
        {
            return (text ?? "").Replace("\0", "").ToLowerInvariant();
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            foreach (string needle in needles)
                if (text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }
    }
}
