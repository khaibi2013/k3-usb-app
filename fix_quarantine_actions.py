import re

# 1. Update main.ts
with open('electron/main.ts', 'r', encoding='utf-8') as f:
    main_ts = f.read()

new_handlers = """
ipcMain.handle('quarantine-action', async (_, action: string, id: string) => {
    try {
        const qPath = path.join(app.getPath('userData'), 'quarantine');
        const targetFile = path.join(qPath, id);
        
        let logs: any[] = [];
        try { logs = JSON.parse(await fs.readFile(getLogsPath(), 'utf8')); } catch(e){}
        
        const originalName = id.split('_').slice(1).join('_').replace('.k3q', '');
        
        if (action === 'delete') {
            await fs.unlink(targetFile);
            logs.unshift({ id: Date.now()+Math.random().toString(), time: new Date().toLocaleString('vi-VN'), event: 'Xóa vĩnh viễn tệp cách ly', details: originalName });
        } else if (action === 'restore') {
            const desktopPath = app.getPath('desktop');
            const dest = path.join(desktopPath, originalName);
            await fs.rename(targetFile, dest);
            logs.unshift({ id: Date.now()+Math.random().toString(), time: new Date().toLocaleString('vi-VN'), event: 'Khôi phục tệp ra Desktop', details: originalName });
        }
        await fs.writeFile(getLogsPath(), JSON.stringify(logs.slice(0, 100), null, 2));
        return { success: true };
    } catch (e: any) {
        return { success: false, error: e.message };
    }
});
"""

if "quarantine-action" not in main_ts:
    main_ts = main_ts.replace("ipcMain.handle('get-quarantine-list'", new_handlers + "\nipcMain.handle('get-quarantine-list'")
    with open('electron/main.ts', 'w', encoding='utf-8') as f:
        f.write(main_ts)

# 2. Update preload.ts
with open('electron/preload.ts', 'r', encoding='utf-8') as f:
    preload = f.read()

if "quarantineAction" not in preload:
    preload = preload.replace("getQuarantineList:", "quarantineAction: (action: string, id: string) => ipcRenderer.invoke('quarantine-action', action, id),\n    getQuarantineList:")
    with open('electron/preload.ts', 'w', encoding='utf-8') as f:
        f.write(preload)

# 3. Update Scanner.vue
with open('src/components/Scanner.vue', 'r', encoding='utf-8') as f:
    vue = f.read()

vue_script_add = """
const handleQuarantineAction = async (action: string, id: string) => {
  const msg = action === 'delete' ? 'Bạn có chắc chắn muốn xóa vĩnh viễn tệp này khỏi khu cách ly?' : 'Khôi phục tệp này ra màn hình Desktop?';
  if (!confirm(msg)) return;
  const res = await api.quarantineAction(action, id);
  if (res.success) {
    alert(action === 'delete' ? 'Đã xóa vĩnh viễn!' : 'Đã khôi phục thành công ra Desktop!');
    await loadData();
  } else {
    alert('Lỗi: ' + res.error);
  }
};
"""
if "handleQuarantineAction" not in vue:
    vue = vue.replace("const handleAction = async (action: string) => {", vue_script_add.strip() + "\n\nconst handleAction = async (action: string) => {")

vue_html_add = """
              <td>{{ q.date }}</td>
              <td style="text-align: right">
                <button class="btn btn-outline btn-sm" @click="handleQuarantineAction('restore', q.id)" style="margin-right: 5px; color: #10b981; border-color: rgba(16, 185, 129, 0.3)">Khôi phục</button>
                <button class="btn btn-outline btn-sm" @click="handleQuarantineAction('delete', q.id)" style="color: #ef4444; border-color: rgba(239, 68, 68, 0.3)">Xóa hẳn</button>
              </td>
            </tr>
"""
if "handleQuarantineAction" not in vue.split("</template>")[0]: # crude check
    vue = vue.replace("<td>{{ q.date }}</td>\n            </tr>", vue_html_add.strip() + "\n            </tr>")
    
    # Also add the empty TH to the table header
    vue = vue.replace("<th>Ngày cách ly</th>\n            </tr>", "<th>Ngày cách ly</th>\n              <th style=\"width: 150px\"></th>\n            </tr>")

with open('src/components/Scanner.vue', 'w', encoding='utf-8') as f:
    f.write(vue)

