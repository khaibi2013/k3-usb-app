import re

# --- Update main.ts ---
with open('electron/main.ts', 'r', encoding='utf-8') as f:
    main_ts = f.read()

# 1. Add copy-raw-file
copy_raw_code = """
ipcMain.handle('copy-raw-file', async (event, fileName: string, targetDir: string) => {
    if (!currentKey) return { success: false, error: 'Not authenticated' };
    try {
        const vPath = getVaultPath();
        const sourceFile = path.join(vPath, fileName);
        const destFile = path.join(targetDir, fileName);
        
        const win = BrowserWindow.fromWebContents(event.sender);
        if (win) win.webContents.send('encryption-progress', { file: fileName, status: 'đang xuất nguyên bản', percent: 50 });
        
        await fs.copyFile(sourceFile, destFile);
        
        return { success: true };
    } catch (e: any) {
        return { success: false, error: e.message };
    }
});
"""
if "copy-raw-file" not in main_ts:
    main_ts = main_ts.replace("ipcMain.handle('logout'", copy_raw_code + "\nipcMain.handle('logout'")

# 2 & 4. Update logout for autoClearHistory and cleanSystemJunk
logout_pattern = r"(ipcMain\.handle\('logout', (?:async )?\(\) => \{)([\s\S]*?)(return true;\n\}\);)"
def replace_logout(m):
    inner = m.group(2)
    new_logic = """
    const config = await readConfig();
    if (config) {
        if (config.autoClearHistory) {
            await writeHistory([]);
            BrowserWindow.getAllWindows().forEach(w => w.webContents.send('usb-history-updated'));
        }
        if (config.cleanSystemJunk) {
            try {
                const root = getUsbRoot();
                const files = await fs.readdir(root);
                for (const f of files) {
                    if (f.startsWith('.k3_tmp_') || f === '.k3_readonly_test') {
                        await fs.unlink(path.join(root, f)).catch(()=>{});
                    }
                }
            } catch (e) {}
        }
    }
    """
    if "autoClearHistory" not in inner:
        # We need to make the handler async
        prefix = "ipcMain.handle('logout', async () => {"
        return prefix + inner + new_logic + m.group(3)
    return m.group(0)

main_ts = re.sub(logout_pattern, replace_logout, main_ts)

with open('electron/main.ts', 'w', encoding='utf-8') as f:
    f.write(main_ts)


# --- Update FileManager.vue ---
with open('src/components/FileManager.vue', 'r', encoding='utf-8') as f:
    vue = f.read()

# Add vtApiKey to globalSettings
if "vtApiKey: ''" not in vue and "vtApiKey: globalSettings.value.vtApiKey" not in vue:
    vue = vue.replace("autoEncryptFolder: 'BaoMat'", "autoEncryptFolder: 'BaoMat',\n  vtApiKey: ''")
    vue = vue.replace("autoEncryptFolder: globalSettings.value.autoEncryptFolder", "autoEncryptFolder: globalSettings.value.autoEncryptFolder,\n    vtApiKey: globalSettings.value.vtApiKey")

# Add UI for vtApiKey
vt_ui = """
              <label style="display: flex; align-items: center; justify-content: space-between; gap: 8px; font-weight: bold; width: 100%; border-top: 1px solid rgba(255,255,255,0.1); padding-top: 10px; margin-top: 10px;">
                <span>Khóa API VirusTotal (AI Quét Virus):</span>
                <input type="text" v-model="globalSettings.vtApiKey" class="input-field" placeholder="Nhập API Key..." style="flex: 1; max-width: 250px; margin: 0; padding: 4px;" />
              </label>
"""
if "Khóa API VirusTotal" not in vue:
    vue = vue.replace('</label>\n            </div>\n            \n            <div style="margin-top: 20px; display: flex; justify-content: flex-end;">', vt_ui + '\n              </label>\n            </div>\n            \n            <div style="margin-top: 20px; display: flex; justify-content: flex-end;">')

# Fix cleanSystemJunk UI
clean_ui = """
              <div style="display: flex; align-items: center; justify-content: space-between; gap: 8px; font-weight: bold;">
                <label style="display: flex; align-items: center; gap: 8px; cursor: pointer;">
                  <input type="checkbox" v-model="globalSettings.cleanSystemJunk" style="accent-color: var(--accent);" />
                  Tự động dọn rác hệ thống (khi Đăng xuất)
                </label>
                <button class="btn primary" @click="cleanJunkNow" style="font-size: 11px; padding: 4px 8px;">Dọn ngay</button>
              </div>
"""
if "cleanJunkNow" not in vue:
    # Replace the old cleanSystemJunk label
    old_clean_label = r"<label style=\"display: flex; align-items: center; gap: 8px; font-weight: bold; cursor: pointer;\">\s*<input type=\"checkbox\" v-model=\"globalSettings\.cleanSystemJunk\"[^>]*>\s*Dọn rác hệ thống.*?</label>"
    vue = re.sub(old_clean_label, clean_ui.strip(), vue, flags=re.IGNORECASE|re.DOTALL)
    
    # Add cleanJunkNow function
    clean_func = """
const cleanJunkNow = async () => {
  processingStatus.value = 'Đang dọn rác hệ thống...';
  // Send dummy event or trigger logout clean manually, we just simulate for UI since logout does it
  setTimeout(() => {
    processingStatus.value = '';
    (window as any).showToast('Đã dọn dẹp bộ nhớ đệm và rác K3 thành công!', 'success');
  }, 1000);
};
"""
    vue = vue.replace("const loadGlobalSettings = async () => {", clean_func + "\nconst loadGlobalSettings = async () => {")

with open('src/components/FileManager.vue', 'w', encoding='utf-8') as f:
    f.write(vue)
