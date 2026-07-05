import re

# 1. Update main.ts
with open('electron/main.ts', 'r', encoding='utf-8') as f:
    main_ts = f.read()

new_handlers = """
ipcMain.handle('get-quarantine-list', async () => {
    try {
        const qPath = path.join(app.getPath('userData'), 'quarantine');
        await fs.mkdir(qPath, { recursive: true }).catch(()=>{});
        const files = await fs.readdir(qPath);
        const list = [];
        for (const f of files) {
            if (f.endsWith('.k3q')) {
                const parts = f.split('_');
                const ts = parseInt(parts[0]);
                const originalName = parts.slice(1).join('_').replace('.k3q', '');
                list.push({
                    id: f,
                    name: originalName,
                    originalPath: 'Cách ly an toàn (.k3q)',
                    date: new Date(ts).toLocaleString('vi-VN')
                });
            }
        }
        return list.sort((a,b) => b.id.localeCompare(a.id));
    } catch (e) { return []; }
});

const getLogsPath = () => path.join(app.getPath('userData'), 'scanner-logs.json');

ipcMain.handle('get-scanner-logs', async () => {
    try {
        const data = await fs.readFile(getLogsPath(), 'utf8');
        return JSON.parse(data);
    } catch (e) { return []; }
});
"""

if "get-quarantine-list" not in main_ts:
    main_ts = main_ts.replace("ipcMain.handle('scanner-action'", new_handlers + "\nipcMain.handle('scanner-action'")

old_scanner_action = r"ipcMain\.handle\('scanner-action', async \(_, action: string, filePaths: string\[\]\) => \{[\s\S]*?\}\);"
new_scanner_action = """
ipcMain.handle('scanner-action', async (_, action: string, filePaths: string[]) => {
    try {
        let logs: any[] = [];
        try { logs = JSON.parse(await fs.readFile(getLogsPath(), 'utf8')); } catch(e){}
        for (const p of filePaths) {
            if (action === 'delete') {
                await fs.unlink(p).catch(()=>{});
                logs.unshift({ id: Date.now()+Math.random().toString(), time: new Date().toLocaleString('vi-VN'), event: 'Xóa tệp nhiễm', details: path.basename(p) });
            } else if (action === 'quarantine') {
                const qPath = path.join(app.getPath('userData'), 'quarantine');
                await fs.mkdir(qPath, { recursive: true }).catch(()=>{});
                const dest = path.join(qPath, `${Date.now()}_${path.basename(p)}.k3q`);
                await fs.rename(p, dest).catch(()=>{});
                logs.unshift({ id: Date.now()+Math.random().toString(), time: new Date().toLocaleString('vi-VN'), event: 'Cách ly tệp', details: path.basename(p) });
            }
        }
        await fs.writeFile(getLogsPath(), JSON.stringify(logs.slice(0, 100), null, 2));
        return { success: true };
    } catch (e: any) {
        return { success: false, error: e.message };
    }
});
"""
main_ts = re.sub(old_scanner_action, new_scanner_action.strip(), main_ts, count=1)

with open('electron/main.ts', 'w', encoding='utf-8') as f:
    f.write(main_ts)

# 2. Update preload.ts
with open('electron/preload.ts', 'r', encoding='utf-8') as f:
    preload = f.read()

if "getQuarantineList" not in preload:
    preload = preload.replace("scannerAction:", "getQuarantineList: () => ipcRenderer.invoke('get-quarantine-list'),\n    getScannerLogs: () => ipcRenderer.invoke('get-scanner-logs'),\n    scannerAction:")
    with open('electron/preload.ts', 'w', encoding='utf-8') as f:
        f.write(preload)

# 3. Update Scanner.vue
with open('src/components/Scanner.vue', 'r', encoding='utf-8') as f:
    vue = f.read()

watch_code = """
import { ref, onMounted, onUnmounted, watch } from 'vue';
"""
if "import { ref, onMounted, onUnmounted, watch } from 'vue';" not in vue:
    vue = vue.replace("import { ref, onMounted, onUnmounted } from 'vue';", watch_code.strip())

load_data_code = """
const loadData = async () => {
  if (activeTab.value === 'quarantine') {
    quarantineList.value = await api.getQuarantineList();
  } else if (activeTab.value === 'logs') {
    logsList.value = await api.getScannerLogs();
  }
};
watch(activeTab, loadData);
onMounted(() => {
  loadData();
  api.onScannerProgress(handleProgress);
"""
if "watch(activeTab, loadData);" not in vue:
    vue = vue.replace("onMounted(() => {\n  api.onScannerProgress(handleProgress);", load_data_code.strip())

# also update handleAction in vue to reload data
vue_action = r"(const res = await api\.scannerAction\(action, selectedPaths\);\s*if \(res\.success\) \{[\s\S]*?alert\('Đã xử lý thành công!'\);\s*threats\.value = threats\.value\.filter\(t => !t\.selected\);)"
new_vue_action = r"\1\n    await loadData();"
vue = re.sub(vue_action, new_vue_action, vue)

with open('src/components/Scanner.vue', 'w', encoding='utf-8') as f:
    f.write(vue)

