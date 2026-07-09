using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using Microsoft.Win32;
using System.Security.Principal;

namespace AnToanUSB
{
    public static class UsbHelper
    {
        public static string GetUsbSerialNumber()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE InterfaceType='USB'"))
                {
                    foreach (ManagementObject queryObj in searcher.Get())
                    {
                        var serialObj = queryObj["SerialNumber"];
                        var serial = serialObj != null ? serialObj.ToString() : null;
                        if (!string.IsNullOrEmpty(serial))
                            return serial.Trim();
                    }
                }
            }
            catch { }
            return "UNKNOWN_HWID";
        }

        public static void SetReadOnly(bool isReadOnly)
        {
            if (!IsAdministrator())
                throw new UnauthorizedAccessException("Cần chạy bằng quyền Administrator để bật/tắt chế độ chỉ đọc USB.");

            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\StorageDevicePolicies"))
            {
                if (key == null) throw new InvalidOperationException("Không thể mở Registry StorageDevicePolicies.");
                key.SetValue("WriteProtect", isReadOnly ? 1 : 0, RegistryValueKind.DWord);
            }
            SetCurrentDiskReadOnly(isReadOnly);
        }

        public static bool IsReadOnlyEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\StorageDevicePolicies"))
                {
                    if (key == null) return false;
                    object value = key.GetValue("WriteProtect");
                    return value != null && Convert.ToInt32(value) == 1;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void SetCurrentDiskReadOnly(bool isReadOnly)
        {
            int diskIndex = GetCurrentAppDiskIndex();
            if (diskIndex < 0)
                throw new InvalidOperationException("Không xác định được số disk của USB đang chạy ứng dụng.");

            string scriptPath = Path.Combine(Path.GetTempPath(), "k3_diskpart_" + Guid.NewGuid().ToString("N") + ".txt");
            string action = isReadOnly ? "set readonly" : "clear readonly";
            File.WriteAllText(scriptPath,
                "select disk " + diskIndex + Environment.NewLine +
                "attributes disk " + action + Environment.NewLine);

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "diskpart.exe",
                    Arguments = "/s \"" + scriptPath + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process proc = Process.Start(psi))
                {
                    if (!proc.WaitForExit(30000))
                    {
                        try { proc.Kill(); } catch { }
                        throw new TimeoutException("DiskPart quá thời gian chờ khi đổi trạng thái chỉ đọc.");
                    }

                    string output = "";
                    try { output = proc.StandardOutput.ReadToEnd(); } catch { }
                    string error = "";
                    try { error = proc.StandardError.ReadToEnd(); } catch { }

                    if (proc.ExitCode != 0)
                        throw new InvalidOperationException("DiskPart không đổi được trạng thái chỉ đọc.\n" + output + "\n" + error);
                }
            }
            finally
            {
                try { File.Delete(scriptPath); } catch { }
            }
        }

        private static int GetCurrentAppDiskIndex()
        {
            string root = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
            if (string.IsNullOrEmpty(root)) return -1;
            string driveLetter = root.TrimEnd('\\').ToUpperInvariant();

            string logicalQuery = "ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" + driveLetter + "'} WHERE AssocClass=Win32_LogicalDiskToPartition";
            using (ManagementObjectSearcher partitionSearcher = new ManagementObjectSearcher(logicalQuery))
            using (ManagementObjectCollection partitions = partitionSearcher.Get())
            {
                foreach (ManagementObject partition in partitions)
                {
                    string partitionId = partition["DeviceID"] == null ? "" : partition["DeviceID"].ToString();
                    if (string.IsNullOrEmpty(partitionId)) continue;

                    string escapedPartitionId = partitionId.Replace("\\", "\\\\").Replace("'", "\\'");
                    string diskQuery = "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + escapedPartitionId + "'} WHERE AssocClass=Win32_DiskDriveToDiskPartition";
                    using (ManagementObjectSearcher diskSearcher = new ManagementObjectSearcher(diskQuery))
                    using (ManagementObjectCollection disks = diskSearcher.Get())
                    {
                        foreach (ManagementObject disk in disks)
                        {
                            object index = disk["Index"];
                            if (index == null) continue;
                            return Convert.ToInt32(index);
                        }
                    }
                }
            }

            return -1;
        }
    }
}
