using System;
using System.Diagnostics;
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
    }
}
