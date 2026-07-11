using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

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

                string portableRule = MatchPortableRules(filePath, lowerName, ext);
                if (!string.IsNullOrEmpty(portableRule)) return portableRule;

                if (LooksLikeUsbFolderImpersonator(lowerName, ext))
                    return "YARA.USB.FolderImpersonator";

                string binaryText = Normalize(ReadBinaryTextSample(filePath));
                if (ContainsAny(binaryText, "createremotethread") &&
                    ContainsAny(binaryText, "virtualallocex"))
                    return "YARA.Win.ApiInjection";

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
            string path = PortableRulePath;
            if (File.Exists(path))
                return "USB YARA rules: portable tools/rules/k3-rules.json + internal rules";
            return "USB YARA rules: internal portable rule set";
        }

        private static string PortableRulePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "rules", "k3-rules.json"); }
        }

        private static string MatchPortableRules(string filePath, string lowerName, string extWithDot)
        {
            string jsonPath = PortableRulePath;
            if (!File.Exists(jsonPath)) return "";

            string json = File.ReadAllText(jsonPath);
            string ext = extWithDot.TrimStart('.').ToLowerInvariant();

            foreach (string rule in ExtractRuleObjects(json, "nameRules"))
            {
                string id = ExtractString(rule, "id");
                if (string.IsNullOrEmpty(id)) id = "K3.Portable.NameRule";
                if (!ExtensionAllowed(rule, ext)) continue;

                if (ArrayContains(rule, "exactNames", lowerName) ||
                    ArrayHasSuffix(rule, "suffixes", lowerName) ||
                    ArrayContainsSubstring(rule, "contains", lowerName))
                    return id;

                if (!HasArray(rule, "exactNames") && !HasArray(rule, "suffixes") && !HasArray(rule, "contains"))
                    return id;
            }

            string text = null;
            foreach (string rule in ExtractRuleObjects(json, "contentRules"))
            {
                string id = ExtractString(rule, "id");
                if (string.IsNullOrEmpty(id)) id = "K3.Portable.ContentRule";
                if (!ExtensionAllowed(rule, ext)) continue;
                if (text == null) text = Normalize(ReadTextSample(filePath));

                string[] all = ExtractArray(rule, "all");
                string[] any = ExtractArray(rule, "any");
                bool allOk = all.Length == 0 || ContainsAll(text, all);
                bool anyOk = any.Length == 0 || ContainsAny(text, any);
                if (allOk && anyOk) return id;
            }

            return "";
        }

        private static IEnumerable<string> ExtractRuleObjects(string json, string arrayName)
        {
            int nameIndex = json.IndexOf("\"" + arrayName + "\"", StringComparison.OrdinalIgnoreCase);
            if (nameIndex < 0) yield break;
            int arrayStart = json.IndexOf('[', nameIndex);
            if (arrayStart < 0) yield break;

            int depth = 0;
            bool inString = false;
            int objectStart = -1;
            for (int i = arrayStart + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\')) inString = !inString;
                if (inString) continue;

                if (c == '{')
                {
                    if (depth == 0) objectStart = i;
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0 && objectStart >= 0)
                    {
                        yield return json.Substring(objectStart, i - objectStart + 1);
                        objectStart = -1;
                    }
                }
                else if (c == ']' && depth == 0)
                {
                    yield break;
                }
            }
        }

        private static string ExtractString(string json, string key)
        {
            Match match = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["value"].Value : "";
        }

        private static string[] ExtractArray(string json, string key)
        {
            Match match = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\\[(?<body>.*?)\\]", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!match.Success) return new string[0];

            List<string> values = new List<string>();
            foreach (Match item in Regex.Matches(match.Groups["body"].Value, "\"(?<value>[^\"]*)\""))
                values.Add(item.Groups["value"].Value.ToLowerInvariant());
            return values.ToArray();
        }

        private static bool HasArray(string json, string key)
        {
            return Regex.IsMatch(json, "\"" + Regex.Escape(key) + "\"\\s*:", RegexOptions.IgnoreCase);
        }

        private static bool ExtensionAllowed(string rule, string ext)
        {
            string[] extensions = ExtractArray(rule, "extensions");
            if (extensions.Length == 0) return true;
            foreach (string allowed in extensions)
                if (string.Equals(allowed.TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool ArrayContains(string json, string key, string value)
        {
            foreach (string item in ExtractArray(json, key))
                if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool ArrayHasSuffix(string json, string key, string value)
        {
            foreach (string item in ExtractArray(json, key))
                if (value.EndsWith(item, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool ArrayContainsSubstring(string json, string key, string value)
        {
            foreach (string item in ExtractArray(json, key))
                if (value.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private static bool ContainsAll(string text, string[] needles)
        {
            foreach (string needle in needles)
                if (text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            return true;
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
