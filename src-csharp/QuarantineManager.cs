using System;
using System.IO;
using System.Text;

namespace AnToanUSB
{
    public static class QuarantineManager
    {
        public static string QuarantineDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "K3_Quarantine"); }
        }

        public static bool QuarantineFile(string sourceFile, string virusName)
        {
            if (!File.Exists(sourceFile)) return false;
            if (!Directory.Exists(QuarantineDir)) Directory.CreateDirectory(QuarantineDir);

            string id = Guid.NewGuid().ToString();
            string dest = Path.Combine(QuarantineDir, id + ".k3q");
            File.Move(sourceFile, dest);

            string meta = Path.Combine(QuarantineDir, id + ".meta");
            string[] lines = new string[] {
                "OriginalPath=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(sourceFile)),
                "OriginalName=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(Path.GetFileName(sourceFile))),
                "VirusName=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(virusName ?? "")),
                "QuarantineDate=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "Size=" + new FileInfo(dest).Length.ToString()
            };
            File.WriteAllLines(meta, lines);
            return true;
        }
    }
}
