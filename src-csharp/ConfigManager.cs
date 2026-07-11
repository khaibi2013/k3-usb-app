using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AnToanUSB
{
    public static class ConfigManager
    {
        private const int PasswordHashIterations = 100000;
        private const int SaltSize = 16;
        private const int PasswordHashSize = 32;

        public static string HwidLock { get; private set; }
        public static string DecoyPasswordHash { get; private set; }
        public static string RealPasswordHash { get; private set; }
        public static string CryptoSalt { get; private set; }
        public static bool IsDecoyMode { get; private set; }
        public static bool NeedsInitialSetup { get; private set; }
        
        // General Settings
        public static string AutoEncryptFolder { get; set; }
        public static long MaxFileSizeBytes { get; set; }
        public static bool AutoDecrypt { get; set; }
        public static bool ShowHidden { get; set; }
        public static bool WipeHistory { get; set; }
        public static bool WipeMacOs { get; set; }
        public static string LoginTitle { get; set; }
        public static string LoginHelpText { get; set; }
        public static bool HideLoginHelp { get; set; }
        public static bool AutoScanOnLogin { get; set; }
        public static string SelfDestructMode { get; set; }
        public static string K3RuleUpdateUrl { get; set; }
        public static int FailedLoginCount { get; set; }
        public static DateTime? LockedUntilUtc { get; set; }

        public static void LoadConfig()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".vault_config.json");
            if (File.Exists(configPath))
            {
                NeedsInitialSetup = false;
                string json = File.ReadAllText(configPath);
                HwidLock = ExtractJsonValue(json, "hwid");
                DecoyPasswordHash = ExtractJsonValue(json, "decoy_hash");
                RealPasswordHash = ExtractJsonValue(json, "real_hash");
                CryptoSalt = ExtractJsonValue(json, "crypto_salt");
                AutoEncryptFolder = ExtractJsonValue(json, "auto_enc_folder");
                
                string maxBytesStr = ExtractJsonValue(json, "max_size_bytes");
                long mb; if (long.TryParse(maxBytesStr, out mb)) MaxFileSizeBytes = mb; else MaxFileSizeBytes = 2L * 1024 * 1024 * 1024; // 2GB
                
                AutoDecrypt = ExtractJsonValue(json, "auto_dec") == "true";
                ShowHidden = ExtractJsonValue(json, "show_hidden") == "true";
                WipeHistory = ExtractJsonValue(json, "wipe_history") == "true";
                WipeMacOs = ExtractJsonValue(json, "wipe_macos") == "true";
                LoginTitle = ExtractJsonValue(json, "login_title");
                LoginHelpText = ExtractJsonValue(json, "login_help");
                HideLoginHelp = ExtractJsonValue(json, "hide_login_help") == "true";
                AutoScanOnLogin = ExtractJsonBool(json, "auto_scan_on_login");
                SelfDestructMode = ExtractJsonValue(json, "self_destruct_mode");
                K3RuleUpdateUrl = ExtractJsonValue(json, "k3_rule_update_url");
                int failed;
                FailedLoginCount = int.TryParse(ExtractJsonValue(json, "failed_login_count"), out failed) ? failed : 0;
                LockedUntilUtc = ParseUtc(ExtractJsonValue(json, "locked_until_utc"));
                if (string.IsNullOrEmpty(LoginTitle)) LoginTitle = "USB An Toàn K3";
                if (string.IsNullOrEmpty(LoginHelpText)) LoginHelpText = "Trợ giúp HELP!";

                if (string.IsNullOrEmpty(CryptoSalt))
                {
                    CryptoSalt = CreateSalt();
                    SaveAllConfig();
                }
                if (string.IsNullOrEmpty(SelfDestructMode)) SelfDestructMode = "off";
            }
            else
            {
                NeedsInitialSetup = true;
                HwidLock = "";
                DecoyPasswordHash = "";
                RealPasswordHash = "";
                CryptoSalt = CreateSalt();
                AutoEncryptFolder = "BaoMat";
                MaxFileSizeBytes = 2L * 1024 * 1024 * 1024; // 2GB
                AutoDecrypt = false;
                ShowHidden = false;
                WipeHistory = false;
                WipeMacOs = true;
                LoginTitle = "USB An Toàn K3";
                LoginHelpText = "Trợ giúp HELP!";
                HideLoginHelp = false;
                AutoScanOnLogin = false;
                SelfDestructMode = "off";
                K3RuleUpdateUrl = "";
                FailedLoginCount = 0;
                LockedUntilUtc = null;
            }

            EnsureRuntimeStorage();
            EnsurePortableMetadata();
        }

        public static bool IsHwidAllowed()
        {
            if (string.IsNullOrEmpty(HwidLock)) return true;
            string currentHwid = UsbHelper.GetUsbSerialNumber();
            return string.Equals(HwidLock, currentHwid, StringComparison.OrdinalIgnoreCase);
        }

        public static void BindToCurrentDevice()
        {
            HwidLock = UsbHelper.GetUsbSerialNumber();
            SaveAllConfig();
        }

        public static void ClearHwidBinding()
        {
            HwidLock = "";
            SaveAllConfig();
        }

        public static bool VerifyPassword(string inputPassword)
        {
            if (VerifyStoredPassword(inputPassword, DecoyPasswordHash))
            {
                IsDecoyMode = true;
                return true;
            }
            if (VerifyStoredPassword(inputPassword, RealPasswordHash))
            {
                IsDecoyMode = false;
                return true;
            }
            return false;
        }

        public static void SavePasswords(string realPass, string decoyPass)
        {
            RealPasswordHash = HashPassword(realPass);
            DecoyPasswordHash = string.IsNullOrEmpty(decoyPass) ? "" : HashPassword(decoyPass);
            NeedsInitialSetup = false;
            FailedLoginCount = 0;
            LockedUntilUtc = null;
            SaveAllConfig();
            EnsureRuntimeStorage();
        }

        public static void SaveLoginFailureState(int failedCount, DateTime? lockedUntilUtc)
        {
            FailedLoginCount = failedCount;
            LockedUntilUtc = lockedUntilUtc;
            SaveAllConfig();
        }

        public static void SaveAllConfig()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".vault_config.json");
            if (string.IsNullOrEmpty(CryptoSalt)) CryptoSalt = CreateSalt();

            string lockedUntil = LockedUntilUtc.HasValue ? LockedUntilUtc.Value.ToUniversalTime().ToString("o") : "";
            string mode = string.IsNullOrEmpty(SelfDestructMode) ? "off" : SelfDestructMode;
            string json = string.Format("{{\"hwid\":\"{0}\",\"real_hash\":\"{1}\",\"decoy_hash\":\"{2}\",\"crypto_salt\":\"{3}\",\"auto_enc_folder\":\"{4}\",\"max_size_bytes\":\"{5}\",\"auto_dec\":\"{6}\",\"show_hidden\":\"{7}\",\"wipe_history\":\"{8}\",\"wipe_macos\":\"{9}\",\"login_title\":\"{10}\",\"login_help\":\"{11}\",\"hide_login_help\":\"{12}\",\"auto_scan_on_login\":\"{13}\",\"self_destruct_mode\":\"{14}\",\"k3_rule_update_url\":\"{15}\",\"failed_login_count\":\"{16}\",\"locked_until_utc\":\"{17}\"}}",
                EscapeJson(HwidLock), EscapeJson(RealPasswordHash), EscapeJson(DecoyPasswordHash), EscapeJson(CryptoSalt), EscapeJson(AutoEncryptFolder), MaxFileSizeBytes.ToString(), 
                AutoDecrypt ? "true" : "false", ShowHidden ? "true" : "false", WipeHistory ? "true" : "false", WipeMacOs ? "true" : "false",
                EscapeJson(LoginTitle), EscapeJson(LoginHelpText), HideLoginHelp ? "true" : "false",
                AutoScanOnLogin ? "true" : "false", EscapeJson(mode), EscapeJson(K3RuleUpdateUrl), FailedLoginCount, EscapeJson(lockedUntil));
            PrepareManagedFileForWrite(configPath);
            File.WriteAllText(configPath, json);
            HideRuntimePath(configPath);
            EnsureRuntimeStorage();
        }

        public static void EnsureRuntimeStorage()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            EnsureDirectory(Path.Combine(baseDir, ".vault"));
            EnsureDirectory(Path.Combine(baseDir, ".vault_decoy"));
            EnsureDirectory(Path.Combine(baseDir, "BaoMat"));
            HideRuntimePath(Path.Combine(baseDir, ".vault_config.json"));
        }

        public static void EnsurePortableMetadata()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string autorunPath = Path.Combine(baseDir, "autorun.inf");
            if (!File.Exists(autorunPath))
            {
                string content =
                    "[AutoRun]\r\n" +
                    "label=USB An Toan K3\r\n" +
                    "icon=AnToanUSB.exe\r\n" +
                    "action=Mo USB An Toan K3\r\n" +
                    "open=AnToanUSB.exe\r\n" +
                    "shellexecute=AnToanUSB.exe\r\n" +
                    "shell\\open=Mo USB An Toan K3\r\n" +
                    "shell\\open\\command=AnToanUSB.exe\r\n" +
                    "shell\\explore=Mo thu muc USB\r\n" +
                    "shell\\explore\\command=explorer.exe .\r\n";
                File.WriteAllText(autorunPath, content);
            }
            HideRuntimePath(autorunPath);
            HidePortableSupportFiles();
        }

        public static void HidePortableSupportFiles()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] paths = {
                Path.Combine(baseDir, "autorun.inf"),
                Path.Combine(baseDir, "icon.png"),
                Path.Combine(baseDir, ".k3_trusted_hashes.txt"),
                Path.Combine(baseDir, "AutoLauncher"),
                Path.Combine(baseDir, "tools"),
                Path.Combine(baseDir, "clamav"),
                Path.Combine(baseDir, "public")
            };

            foreach (string path in paths)
                HideRuntimePath(path);
        }

        public static void PrepareManagedFileForWrite(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                FileAttributes attributes = File.GetAttributes(path);
                attributes &= ~FileAttributes.Hidden;
                attributes &= ~FileAttributes.System;
                attributes &= ~FileAttributes.ReadOnly;
                File.SetAttributes(path, attributes);
            }
            catch { }
        }

        public static void HideManagedPath(string path)
        {
            HideRuntimePath(path);
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            HideRuntimePath(path);
        }

        private static void HideRuntimePath(string path)
        {
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path)) return;
                FileAttributes attributes = File.GetAttributes(path);
                attributes |= FileAttributes.Hidden | FileAttributes.System;
                File.SetAttributes(path, attributes);
            }
            catch { }
        }

        public static byte[] GetCryptoSaltBytes()
        {
            if (string.IsNullOrEmpty(CryptoSalt)) CryptoSalt = CreateSalt();
            return Convert.FromBase64String(CryptoSalt);
        }

        private static string HashPassword(string password)
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(salt);

            byte[] hash;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, PasswordHashIterations))
                hash = pbkdf2.GetBytes(PasswordHashSize);

            return string.Format("pbkdf2${0}${1}${2}", PasswordHashIterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
        }

        private static bool VerifyStoredPassword(string inputPassword, string storedPassword)
        {
            if (string.IsNullOrEmpty(storedPassword)) return false;

            if (!storedPassword.StartsWith("pbkdf2$", StringComparison.Ordinal))
                return inputPassword == storedPassword;

            try
            {
                string[] parts = storedPassword.Split('$');
                if (parts.Length != 4) return false;

                int iterations;
                if (!int.TryParse(parts[1], out iterations)) return false;

                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expected = Convert.FromBase64String(parts[3]);
                byte[] actual;
                using (var pbkdf2 = new Rfc2898DeriveBytes(inputPassword, salt, iterations))
                    actual = pbkdf2.GetBytes(expected.Length);

                return FixedTimeEquals(actual, expected);
            }
            catch
            {
                return false;
            }
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static string CreateSalt()
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(salt);
            return Convert.ToBase64String(salt);
        }

        private static string EscapeJson(string value)
        {
            if (value == null) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string ExtractJsonValue(string json, string key)
        {
            string searchKey = "\"" + key + "\":";
            int idx = json.IndexOf(searchKey);
            if (idx == -1) return "";
            
            int start = json.IndexOf("\"", idx + searchKey.Length);
            if (start == -1) return "";

            StringBuilder value = new StringBuilder();
            bool escaped = false;
            for (int i = start + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (escaped)
                {
                    switch (c)
                    {
                        case '"': value.Append('"'); break;
                        case '\\': value.Append('\\'); break;
                        case '/': value.Append('/'); break;
                        case 'b': value.Append('\b'); break;
                        case 'f': value.Append('\f'); break;
                        case 'n': value.Append('\n'); break;
                        case 'r': value.Append('\r'); break;
                        case 't': value.Append('\t'); break;
                        case 'u':
                            if (i + 4 < json.Length)
                            {
                                string hex = json.Substring(i + 1, 4);
                                int code;
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out code))
                                {
                                    value.Append((char)code);
                                    i += 4;
                                }
                                else
                                {
                                    value.Append(c);
                                }
                            }
                            else
                            {
                                value.Append(c);
                            }
                            break;
                        default:
                            value.Append(c);
                            break;
                    }
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"') return value.ToString();
                value.Append(c);
            }

            return "";
        }

        private static bool ExtractJsonBool(string json, string key)
        {
            string value = ExtractJsonValue(json, key);
            if (!string.IsNullOrEmpty(value)) return value == "true";

            string searchKey = "\"" + key + "\":";
            int idx = json.IndexOf(searchKey);
            if (idx == -1) return false;
            int start = idx + searchKey.Length;
            while (start < json.Length && char.IsWhiteSpace(json[start])) start++;
            return json.Substring(start).StartsWith("true", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime? ParseUtc(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out parsed))
                return parsed.ToUniversalTime();
            return null;
        }
    }
}
