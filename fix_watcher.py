import re

with open('electron/main.ts', 'r', encoding='utf-8') as f:
    content = f.read()

watcher_code = """
let vaultWatcher: chokidar.FSWatcher | null = null;

async function setupVaultWatcher(config: any) {
    if (vaultWatcher) {
        await vaultWatcher.close();
        vaultWatcher = null;
    }
    
    if (!currentKey || isDecoyMode) return;
    
    const folderName = config.autoEncryptFolder || 'BaoMat';
    if (!folderName.trim()) return;
    
    const watchDir = path.join(getUsbRoot(), folderName);
    try {
        await fs.mkdir(watchDir, { recursive: true });
    } catch(e) {}
    
    vaultWatcher = chokidar.watch(watchDir, {
        persistent: true,
        ignoreInitial: true,
        awaitWriteFinish: {
            stabilityThreshold: 1000,
            pollInterval: 100
        }
    });
    
    vaultWatcher.on('add', async (filePath) => {
        try {
            const stat = await fs.stat(filePath);
            let forceRaw = false;
            const sizeMB = stat.size / (1024 * 1024);
            if (config.maxEncryptSize > 0 && sizeMB > config.maxEncryptSize) {
                forceRaw = true;
            }
            
            const fakeEvent = {
                sender: {
                    send: (channel: string, data: any) => {
                        BrowserWindow.getAllWindows().forEach(w => w.webContents.send(channel, data));
                    }
                }
            };
            
            BrowserWindow.getAllWindows().forEach(w => w.webContents.send('encryption-progress', { file: path.basename(filePath), status: forceRaw ? 'copying raw (background)' : 'encrypting (background)' }));
            
            await processEncryption(fakeEvent, filePath, true, '/', true, forceRaw);
            
            BrowserWindow.getAllWindows().forEach(w => {
                w.webContents.send('encryption-progress', { file: 'Done', status: 'done' });
                w.webContents.send('auto-encrypt-done');
            });
        } catch (e: any) {
            console.error('Auto encrypt error:', e);
        }
    });
}
"""

if "let vaultWatcher: chokidar.FSWatcher | null = null;" not in content:
    content = content.replace("const startUSBPoll = async () => {", watcher_code + "\nconst startUSBPoll = async () => {")

with open('electron/main.ts', 'w', encoding='utf-8') as f:
    f.write(content)
