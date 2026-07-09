using System;
using System.Collections.Generic;

namespace AnToanUSB
{
    public static class LanguageManager
    {
        public static string CurrentLanguage = "vi"; // "vi" or "en"
        public static event Action LanguageChanged;

        private static Dictionary<string, string> vi = new Dictionary<string, string>
        {
            { "AppTitle", "USB An Toàn K3 (Safe Mode)" },
            { "LoginTitle", "Login" },
            { "LoginHelp", "Trợ giúp HELP!" },
            { "LoginBtn", "Đăng nhập" },
            { "LoginStatus", "Đang giải mã thiết lập/1..." },
            { "Menu_Selection", "Lựa chọn" },
            { "Menu_Function", "Chức năng" },
            { "Menu_Help", "Trợ giúp" },
            { "Search_Local", "Tìm trên Máy tính" },
            { "Search_USB", "Tìm kiếm" },
            { "Btn_EncryptUSB", "Đưa vào két" },
            { "Ctx_Copy", "Copy\tCtrl+C" },
            { "Ctx_Cut", "Cut\tCtrl+X" },
            { "Ctx_Paste", "Paste\tCtrl+V" },
            { "Ctx_Encrypt", "Mã hóa" },
            { "Ctx_Encrypt_Paste", "Dán và Mã hóa\tCtrl+M" },
            { "Ctx_Encrypt_File", "Mã hóa tệp" },
            { "Ctx_Encrypt_Custom", "Mã hóa với khóa tùy chọn" },
            { "Ctx_Decrypt", "Giải mã" },
            { "Ctx_Delete", "Delete\tDel" },
            { "Ctx_SecureDelete", "Secure Delete" },
            { "Ctx_Rename", "Rename\tF2" },
            { "Ctx_NewFolder", "New Folder" },
            { "Ctx_Refresh", "Refresh" },
            { "Ctx_OpenPath", "Mở đường dẫn" },
            { "Overlay_DragDrop", "Hỗ trợ kéo thả qua lại để sao chép" },
            { "SettingsTitle", "Cài đặt Két Sắt" },
            { "Settings_Password", "Đổi mật khẩu" },
            { "Settings_HWID", "Bật khóa cứng (HWID Lock)" },
            { "Settings_ReadOnly", "Khóa ghi USB (Chống Virus)" },
            { "Settings_Save", "Lưu cài đặt" }
        };

        private static Dictionary<string, string> en = new Dictionary<string, string>
        {
            { "AppTitle", "K3 Secure USB (Safe Mode)" },
            { "LoginTitle", "Login" },
            { "LoginHelp", "Help!" },
            { "LoginBtn", "Login" },
            { "LoginStatus", "Decrypting settings/1..." },
            { "Menu_Selection", "Selection" },
            { "Menu_Function", "Function" },
            { "Menu_Help", "Help" },
            { "Search_Local", "Search Local PC" },
            { "Search_USB", "Search USB" },
            { "Btn_EncryptUSB", "Send to vault" },
            { "Ctx_Copy", "Copy\tCtrl+C" },
            { "Ctx_Cut", "Cut\tCtrl+X" },
            { "Ctx_Paste", "Paste\tCtrl+V" },
            { "Ctx_Encrypt", "Encrypt" },
            { "Ctx_Encrypt_Paste", "Paste & Encrypt\tCtrl+M" },
            { "Ctx_Encrypt_File", "Encrypt File" },
            { "Ctx_Encrypt_Custom", "Encrypt with Custom Key" },
            { "Ctx_Decrypt", "Decrypt" },
            { "Ctx_Delete", "Delete\tDel" },
            { "Ctx_SecureDelete", "Secure Delete" },
            { "Ctx_Rename", "Rename\tF2" },
            { "Ctx_NewFolder", "New Folder" },
            { "Ctx_Refresh", "Refresh" },
            { "Ctx_OpenPath", "Open Path" },
            { "Overlay_DragDrop", "Supports drag and drop to copy" },
            { "SettingsTitle", "Vault Settings" },
            { "Settings_Password", "Change Password" },
            { "Settings_HWID", "Enable HWID Lock" },
            { "Settings_ReadOnly", "USB Read-Only Lock" },
            { "Settings_Save", "Save Settings" }
        };

        public static string GetString(string key)
        {
            var dict = CurrentLanguage == "en" ? en : vi;
            if (dict.ContainsKey(key))
                return dict[key];
            return key;
        }

        public static void SwitchLanguage(string langCode)
        {
            CurrentLanguage = langCode;
            if (LanguageChanged != null) LanguageChanged.Invoke();
        }
    }
}
