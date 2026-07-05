import re

with open('electron/main.ts', 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Add import
if "import * as chokidar from 'chokidar';" not in content:
    content = content.replace("import * as fs from 'fs/promises';", "import * as fs from 'fs/promises';\nimport * as chokidar from 'chokidar';")

# 2. Add vaultWatcher variables and function
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
                        if (mainWindow && !mainWindow.isDestroyed()) {
                            mainWindow.webContents.send(channel, data);
                        }
                    }
                }
            };
            
            if (mainWindow && !mainWindow.isDestroyed()) {
                mainWindow.webContents.send('encryption-progress', { file: path.basename(filePath), status: forceRaw ? 'copying raw (background)' : 'encrypting (background)' });
            }
            
            await processEncryption(fakeEvent, filePath, true, '/', true, forceRaw);
            
            if (mainWindow && !mainWindow.isDestroyed()) {
                mainWindow.webContents.send('encryption-progress', { file: 'Done', status: 'done' });
                mainWindow.webContents.send('auto-encrypt-done');
            }
        } catch (e: any) {
            console.error('Auto encrypt error:', e);
        }
    });
}
"""

if "let vaultWatcher" not in content:
    # Insert after mainWindow = new BrowserWindow
    pattern = r"let mainWindow: BrowserWindow \| null = null;\n"
    content = re.sub(pattern, "let mainWindow: BrowserWindow | null = null;\n" + watcher_code, content)

# 3. Call setupVaultWatcher in login (normal login)
login_pattern = r"(currentKey = deriveKey\(password, Buffer\.from\(config\.passwordHash\.split\(':'\)\[0\], 'hex'\)\);\n\s*isDecoyMode = false;\n\s*)return \{ success: true, isDecoy: false \};"
if "setupVaultWatcher" not in re.search(login_pattern, content).group(1):
    content = re.sub(login_pattern, r"\1setupVaultWatcher(config);\n        return { success: true, isDecoy: false };", content)

# 4. Update config call
update_pattern = r"(ipcMain\.handle\('update-config', async \(_, newSettings: any\) => \{\n\s*const config = await readConfig\(\) \|\| \{\};\n\s*Object\.assign\(config, newSettings\);\n\s*await writeConfig\(config\);\n\s*)return true;"
if "setupVaultWatcher" not in re.search(update_pattern, content).group(1):
    content = re.sub(update_pattern, r"\1setupVaultWatcher(config);\n    return true;", content)

# 5. Stop watcher on logout
logout_pattern = r"(ipcMain\.handle\('logout', \(\) => \{\n\s*currentKey = null;\n\s*isDecoyMode = false;\n\s*)return true;"
if "vaultWatcher.close()" not in re.search(logout_pattern, content).group(1):
    content = re.sub(logout_pattern, r"\1if (vaultWatcher) { vaultWatcher.close(); vaultWatcher = null; }\n    return true;", content)

with open('electron/main.ts', 'w', encoding='utf-8') as f:
    f.write(content)
