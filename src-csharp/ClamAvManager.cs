using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace AnToanUSB
{
    public class ClamAvUpdateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Output { get; set; }
    }

    public static class ClamAvManager
    {
        public static string ClamRoot
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "clamav"); }
        }

        public static string DatabaseDir
        {
            get { return Path.Combine(ClamRoot, "database"); }
        }

        public static string FreshClamConfigPath
        {
            get { return Path.Combine(ClamRoot, "freshclam.conf"); }
        }

        public static string FindClamScanPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = {
                Path.Combine(baseDir, @"tools\windows\clamav\clamscan.exe"),
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

        public static string FindFreshClamPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = {
                Path.Combine(baseDir, @"tools\windows\clamav\freshclam.exe"),
                Path.Combine(baseDir, @"tools\clamav\freshclam.exe"),
                Path.Combine(baseDir, @"clamav\freshclam.exe"),
                @"C:\Program Files\ClamAV\freshclam.exe",
                @"C:\Program Files (x86)\ClamAV\freshclam.exe"
            };

            foreach (string candidate in candidates)
                if (File.Exists(candidate)) return candidate;

            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    string candidate = Path.Combine(dir.Trim(), "freshclam.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
            return "";
        }

        public static string BuildClamScanArguments(string filePath)
        {
            EnsurePortableLayout();
            if (Directory.Exists(DatabaseDir) && Directory.GetFiles(DatabaseDir, "*.*").Length > 0)
                return string.Format("--no-summary --infected --database=\"{0}\" \"{1}\"", DatabaseDir, filePath);
            return string.Format("--no-summary --infected \"{0}\"", filePath);
        }

        public static string GetStatusText()
        {
            string clamscan = FindClamScanPath();
            string freshclam = FindFreshClamPath();
            int dbCount = Directory.Exists(DatabaseDir) ? Directory.GetFiles(DatabaseDir, "*.*").Length : 0;
            DateTime newest = DateTime.MinValue;
            if (Directory.Exists(DatabaseDir))
            {
                foreach (string file in Directory.GetFiles(DatabaseDir, "*.*"))
                {
                    DateTime t = File.GetLastWriteTime(file);
                    if (t > newest) newest = t;
                }
            }

            string dbText = dbCount == 0 ? "database chưa có" : string.Format("{0} file database, mới nhất {1}", dbCount, newest.ToString("dd/MM/yyyy HH:mm"));
            return string.Format("ClamAV portable: clamscan={0}; freshclam={1}; {2}",
                string.IsNullOrEmpty(clamscan) ? "chưa tìm thấy" : clamscan,
                string.IsNullOrEmpty(freshclam) ? "chưa tìm thấy" : freshclam,
                dbText);
        }

        public static ClamAvUpdateResult UpdateDatabase(int timeoutMs)
        {
            EnsurePortableLayout();
            string freshclam = FindFreshClamPath();
            if (string.IsNullOrEmpty(freshclam))
            {
                return new ClamAvUpdateResult
                {
                    Success = false,
                    Message = "Chưa tìm thấy freshclam.exe. Hãy chạy tools\\clamav\\Install-ClamAV-Portable.ps1 khi có mạng.",
                    Output = ""
                };
            }

            WriteFreshClamConfig();

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = freshclam,
                Arguments = string.Format("--config-file=\"{0}\" --stdout", FreshClamConfigPath),
                WorkingDirectory = ClamRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            try
            {
                using (Process proc = Process.Start(psi))
                {
                    if (!proc.WaitForExit(timeoutMs))
                    {
                        try { proc.Kill(); } catch { }
                        return new ClamAvUpdateResult { Success = false, Message = "freshclam quá thời gian chờ.", Output = "" };
                    }

                    string output = "";
                    try { output = (proc.StandardOutput.ReadToEnd() + "\r\n" + proc.StandardError.ReadToEnd()).Trim(); } catch { }
                    bool ok = proc.ExitCode == 0 || output.IndexOf("up-to-date", StringComparison.OrdinalIgnoreCase) >= 0;
                    return new ClamAvUpdateResult
                    {
                        Success = ok,
                        Message = ok ? "Đã cập nhật database ClamAV." : "Cập nhật ClamAV chưa thành công. Kiểm tra mạng hoặc cấu hình freshclam.",
                        Output = output
                    };
                }
            }
            catch (Exception ex)
            {
                return new ClamAvUpdateResult { Success = false, Message = "Không chạy được freshclam: " + ex.Message, Output = "" };
            }
        }

        public static void EnsurePortableLayout()
        {
            if (!Directory.Exists(ClamRoot)) Directory.CreateDirectory(ClamRoot);
            if (!Directory.Exists(DatabaseDir)) Directory.CreateDirectory(DatabaseDir);
            if (!File.Exists(FreshClamConfigPath)) WriteFreshClamConfig();
            ConfigManager.HidePortableSupportFiles();
        }

        private static void WriteFreshClamConfig()
        {
            if (!Directory.Exists(ClamRoot)) Directory.CreateDirectory(ClamRoot);
            if (!Directory.Exists(DatabaseDir)) Directory.CreateDirectory(DatabaseDir);

            string config =
                "DatabaseDirectory database\r\n" +
                "DatabaseMirror database.clamav.net\r\n" +
                "UpdateLogFile freshclam.log\r\n" +
                "LogTime yes\r\n";
            File.WriteAllText(FreshClamConfigPath, config, new UTF8Encoding(false));
        }
    }
}
