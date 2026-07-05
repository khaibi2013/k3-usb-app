import { app, BrowserWindow, ipcMain, dialog } from 'electron';
import * as path from 'path';
import * as fs from 'fs/promises';
import * as chokidar from 'chokidar';
import * as crypto from 'crypto';
import * as zlib from 'zlib';
import * as os from 'os';
import * as https from 'https';
import { exec } from 'child_process';
import AdmZip from 'adm-zip';
import si from 'systeminformation';
const archiver = require('archiver');
const archiverZipEncrypted = require('archiver-zip-encrypted');
import { createWriteStream } from 'fs';
const yara = require('@virustotal/yara-x');

let yaraScanner: any = null;

async function initYaraScanner() {
    try {
        const appPath = app.getAppPath();
        const wasmPath = path.join(appPath, 'node_modules/@virustotal/yara-x/pkg/yara_x_js_bg.wasm');
        const fsSync = require('fs');
        const wasmBuffer = fsSync.readFileSync(wasmPath);
        yara.initSync({ module: wasmBuffer });
        
        const userDataPath = app.getPath('userData');
        const userRulesPath = path.join(userDataPath, 'k3_rules.yar');
        let rulesPath = path.join(appPath, 'electron', 'scanner', 'k3_rules.yar');
        
        try {
            await fs.access(userRulesPath);
            rulesPath = userRulesPath;
        } catch {}
        
        const rulesStr = await fs.readFile(rulesPath, 'utf8');
        
        const compiler = new yara.Compiler();
        compiler.addSource(rulesStr);
        const rules = compiler.build();
        yaraScanner = new yara.Scanner(rules);
        console.log('YARA Scanner initialized successfully.');
    } catch (e) {
        console.error('Failed to init YARA scanner:', e);
    }
}

async function autoUpdateDatabase() {
    try {
        const url = 'https://raw.githubusercontent.com/khaibi2013/k3-usb-app/main/electron/scanner/k3_rules.yar';
        https.get(url, (res) => {
            if (res.statusCode === 200) {
                let data = '';
                res.on('data', chunk => data += chunk);
                res.on('end', async () => {
                    if (data.includes('rule ') && data.includes('condition:')) {
                        const userRulesPath = path.join(app.getPath('userData'), 'k3_rules.yar');
                        await fs.writeFile(userRulesPath, data);
                        await initYaraScanner();
                    }
                });
            }
        }).on('error', () => {});
    } catch (e) {}
}

async function scanYaraOffline(filePath: string): Promise<string | null> {
    if (!yaraScanner) return null;
    try {
        const buffer = await fs.readFile(filePath);
        const results = yaraScanner.scan(buffer);
        if (results && results.matches && results.matches.length > 0) {
            return results.matches.map((m: any) => m.identifier).join(', ');
        }
    } catch (e) {}
    return null;
}

archiver.registerFormat('zip-encrypted', archiverZipEncrypted);

const isDev = process.env.NODE_ENV === 'development';
const ALGORITHM = 'aes-256-gcm';
const MAX_ATTEMPTS = 10;

let currentKey: Buffer | null = null; 
let isDecoyMode = false;
let failedAttempts = 0;
let lockoutUntil = 0;

const getUsbRoot = () => {
    if (isDev) return path.join(__dirname, '../../');
    if (process.platform === 'win32') {
        // Windows Portable build sets this env variable to the USB drive path
        return process.env.PORTABLE_EXECUTABLE_DIR || path.dirname(app.getPath('exe'));
    } else if (process.platform === 'darwin') {
        // macOS App path: /Volumes/USBName/IronVault.app/Contents/MacOS/IronVault
        // Go up 3 levels to reach the USB root (/Volumes/USBName/)
        return path.resolve(app.getPath('exe'), '../../..');
    }
    return path.dirname(app.getPath('exe'));
};

const getVaultPath = () => {
    const folder = isDecoyMode ? '.vault_decoy' : '.vault';
    return path.join(getUsbRoot(), folder);
};

const getConfigPath = () => {
    return path.join(getUsbRoot(), '.vault_config.json');
};

const initVault = async () => {
    try { await fs.mkdir(path.join(getUsbRoot(), '.vault'), { recursive: true }); } catch (e) {}
    try { await fs.mkdir(path.join(getUsbRoot(), '.vault_decoy'), { recursive: true }); } catch (e) {}
};

const deriveKey = (password: string, salt: Buffer): Buffer => {
    return crypto.scryptSync(password, salt, 32);
};

const hashPassword = (password: string) => {
    const salt = crypto.randomBytes(16).toString('hex');
    const hash = crypto.scryptSync(password, salt, 64).toString('hex');
    return `${salt}:${hash}`;
};

const verifyPassword = (password: string, storedHash: string) => {
    const [salt, hash] = storedHash.split(':');
    const derivedHash = crypto.scryptSync(password, salt, 64).toString('hex');
    return derivedHash === hash;
};

const readConfig = async () => {
    try {
        const configStr = await fs.readFile(getConfigPath(), 'utf8');
        return JSON.parse(configStr);
    } catch {
        return null;
    }
};

const writeConfig = async (config: any) => {
    try { await fs.writeFile(getConfigPath(), JSON.stringify(config, null, 2)); } catch (e) {}
};

const HISTORY_FILE = path.join(app.getPath('userData'), 'history.json');

const readHistory = async () => {
    try {
        const data = await fs.readFile(HISTORY_FILE, 'utf8');
        return JSON.parse(data);
    } catch (e) { return []; }
};

const writeHistory = async (history: any[]) => {
    try { await fs.writeFile(HISTORY_FILE, JSON.stringify(history)); } catch (e) {}
};

// --- USB Polling Service ---
const pollUSBDevices = () => {
    return new Promise<any[]>((resolve) => {
        if (process.platform === 'win32') {
            exec('wmic diskdrive where interfacetype="USB" get caption, pnpdeviceid, serialnumber /value', (err, stdout) => {
                if (err) return resolve([]);
                const devices = [];
                let currentDevice: any = {};
                for (const line of stdout.split('\n')) {
                    const t = line.trim();
                    if (!t) {
                        if (currentDevice.Caption) devices.push(currentDevice);
                        currentDevice = {};
                        continue;
                    }
                    const [key, ...vals] = t.split('=');
                    if (key && vals.length) currentDevice[key.trim()] = vals.join('=').trim();
                }
                if (currentDevice.Caption) devices.push(currentDevice);
                
                resolve(devices.map(d => ({
                    name: d.Caption || 'USB Device',
                    type: 'USB Storage',
                    serial: d.SerialNumber || 'Unknown',
                    deviceId: d.PNPDeviceID || 'Unknown'
                })));
            });
        } else if (process.platform === 'darwin') {
            exec('system_profiler SPUSBDataType -json', (err, stdout) => {
                if (err) return resolve([]);
                try {
                    const data = JSON.parse(stdout);
                    const devices: any[] = [];
                    const findDevices = (items: any[]) => {
                        for (const item of items) {
                            if (item.serial_num || (item.name && item.name.toLowerCase().includes('usb'))) {
                                if (item.serial_num) {
                                    devices.push({
                                        name: item._name || item.name || 'USB Device',
                                        type: 'USB Storage',
                                        serial: item.serial_num,
                                        deviceId: item.device_id || item.serial_num
                                    });
                                }
                            }
                            if (item._items) findDevices(item._items);
                        }
                    };
                    if (data.SPUSBDataType) findDevices(data.SPUSBDataType);
                    resolve(devices);
                } catch { resolve([]); }
            });
        } else {
            resolve([]);
        }
    });
};


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

const startUSBPoll = async () => {
    setInterval(async () => {
        const history = await readHistory();
        const devices = await pollUSBDevices();
        
        const newIds = new Set<string>();
        let updated = false;
        
        for (const d of devices) {
            newIds.add(d.deviceId);
            const existing = history.find((h: any) => h.deviceId === d.deviceId);
            if (!existing) {
                history.unshift({
                    time: new Date().toLocaleString('vi-VN'),
                    name: d.name,
                    type: d.type,
                    status: 'Đang kết nối',
                    serial: d.serial,
                    deviceId: d.deviceId
                });
                updated = true;
            } else if (existing.status !== 'Đang kết nối') {
                existing.status = 'Đang kết nối';
                existing.time = new Date().toLocaleString('vi-VN');
                updated = true;
            }
        }
        
        for (const h of history) {
            if (h.status === 'Đang kết nối' && !newIds.has(h.deviceId)) {
                h.status = 'Đã ngắt';
                updated = true;
            }
        }
        
        if (updated) {
            await writeHistory(history);
            BrowserWindow.getAllWindows().forEach(w => w.webContents.send('usb-history-updated'));
        }
    }, 5000);
};

const createWindow = () => {
    const win = new BrowserWindow({
        width: 1100,
        height: 750,
        titleBarStyle: 'hidden',
        webPreferences: {
            preload: path.join(__dirname, 'preload.cjs'),
            nodeIntegration: false,
            contextIsolation: true
        }
    });

    if (isDev) {
        win.loadURL('http://localhost:5173');
        win.webContents.openDevTools();
    } else {
        win.loadFile(path.join(__dirname, '../dist/index.html'));
    }
};

// Secure File Shredder
const shredFile = async (filePath: string) => {
    try {
        const stat = await fs.stat(filePath);
        if (stat.isDirectory()) return; 
        const fh = await fs.open(filePath, 'r+');
        const chunkSize = 1024 * 1024;
        const passes = 3;
        for (let p = 0; p < passes; p++) {
            let written = 0;
            const buffer = crypto.randomBytes(chunkSize);
            while (written < stat.size) {
                const toWrite = Math.min(chunkSize, stat.size - written);
                await fh.write(buffer, 0, toWrite, written);
                written += toWrite;
            }
        }
        await fh.close();
        await fs.unlink(filePath);
    } catch (e) {
        console.error('Shred error', e);
        try { await fs.unlink(filePath); } catch {}
    }
};

const shredFolder = async (dirPath: string) => {
    try {
        const files = await fs.readdir(dirPath, { withFileTypes: true });
        for (const f of files) {
            const p = path.join(dirPath, f.name);
            if (f.isDirectory()) await shredFolder(p);
            else await shredFile(p);
        }
        await fs.rmdir(dirPath);
    } catch (e) { console.error('Shred folder error', e); }
};

// IPC Handlers
ipcMain.handle('check-setup', async () => {
    const config = await readConfig();
    return !!(config && config.passwordHash);
});

ipcMain.handle('get-config', async () => await readConfig() || {});

ipcMain.handle('update-config', async (_, newSettings: any) => {
    const config = await readConfig() || {};
    Object.assign(config, newSettings);
    await writeConfig(config);
    setupVaultWatcher(config);
    return true;
});

ipcMain.handle('setup-password', async (_, password: string) => {
    const config = { passwordHash: hashPassword(password), failedAttempts: 0, locked: false };
    await writeConfig(config);
    return true;
});

ipcMain.handle('setup-decoy', async (_, password: string) => {
    const config = await readConfig();
    if (!config) return false;
    
    // Wipe old decoy vault if exists to free up space
    try {
        const decoyPath = path.join(getUsbRoot(), '.vault_decoy');
        await fs.rm(decoyPath, { recursive: true, force: true });
    } catch (e) {}

    config.decoyHash = hashPassword(password);
    await writeConfig(config);
    return true;
});

const getFolderSize = async (dirPath: string): Promise<number> => {
    let total = 0;
    try {
        const items = await fs.readdir(dirPath, { withFileTypes: true });
        for (const item of items) {
            const itemPath = path.join(dirPath, item.name);
            if (item.isDirectory()) {
                total += await getFolderSize(itemPath);
            } else {
                const stat = await fs.stat(itemPath);
                total += stat.size;
            }
        }
    } catch (e) {}
    return total;
};

ipcMain.handle('get-decoy-info', async () => {
    const config = await readConfig();
    const isSetup = !!(config && config.decoyHash);
    const decoyPath = path.join(getUsbRoot(), '.vault_decoy');
    const sizeBytes = await getFolderSize(decoyPath);
    return { isSetup, sizeMB: sizeBytes / (1024 * 1024) };
});

ipcMain.handle('wipe-decoy', async () => {
    const config = await readConfig();
    if (!config) return false;
    
    try {
        const decoyPath = path.join(getUsbRoot(), '.vault_decoy');
        await fs.rm(decoyPath, { recursive: true, force: true });
    } catch (e) {}
    
    delete config.decoyHash;
    await writeConfig(config);
    return true;
});

ipcMain.handle('login', async (_, password: string) => {
    if (Date.now() < lockoutUntil) {
        const remain = Math.ceil((lockoutUntil - Date.now())/1000);
        return { success: false, error: `Bị khóa do nhập sai nhiều lần. Thử lại sau ${remain}s.` };
    }

    const config = await readConfig();
    if (!config) return { success: false, error: 'Not setup' };
    
    if (config.locked || config.failedAttempts >= MAX_ATTEMPTS) {
        config.locked = true;
        // SELF DESTRUCT: Wipe the entire vault to prevent brute-force extraction
        try {
            await fs.rm(path.join(getUsbRoot(), '.vault'), { recursive: true, force: true });
            await fs.rm(path.join(getUsbRoot(), '.decoy_vault'), { recursive: true, force: true });
            
            // Delete the password hashes to brick the drive permanently
            delete config.passwordHash;
            delete config.decoyHash;
        } catch (e) {}
        
        await writeConfig(config);
        return { success: false, error: 'Thiết bị đã TỰ HỦY DỮ LIỆU do nhập sai quá nhiều lần để bảo vệ an toàn!', locked: true };
    }
    
    if (config.hwidEnabled && config.expectedHwid) {
        const devices = await si.blockDevices();
        let currentHwid = '';
        let maxLen = -1;
        for (const dev of devices) {
            if (dev.mount && USB_DIR.startsWith(dev.mount)) {
                if (dev.mount.length > maxLen) {
                    maxLen = dev.mount.length;
                    currentHwid = (dev.serial || '') + (dev.uuid || '');
                }
            }
        }
        if (currentHwid !== config.expectedHwid) {
            return { success: false, error: 'Lỗi bảo mật: Dữ liệu bị copy trái phép sang USB khác!' };
        }
    }
    
    if (verifyPassword(password, config.passwordHash)) {
        config.failedAttempts = 0;
        await writeConfig(config);
        failedAttempts = 0;
        currentKey = deriveKey(password, Buffer.from(config.passwordHash.split(':')[0], 'hex'));
        isDecoyMode = false;
        setupVaultWatcher(config);
        return { success: true, isDecoy: false };
    }
    
    if (config.decoyHash && verifyPassword(password, config.decoyHash)) {
        config.failedAttempts = 0;
        await writeConfig(config);
        failedAttempts = 0;
        currentKey = deriveKey(password, Buffer.from(config.decoyHash.split(':')[0], 'hex'));
        isDecoyMode = true;
        return { success: true, isDecoy: true };
    }

    config.failedAttempts = (config.failedAttempts || 0) + 1;
    await writeConfig(config);
    
    failedAttempts++;
    if (failedAttempts >= 5) {
        lockoutUntil = Date.now() + 5 * 60 * 1000;
        failedAttempts = 0;
        return { success: false, error: 'Nhập sai 5 lần. Két sắt tự động khóa 5 phút!' };
    }

    return { success: false, error: `Sai mật khẩu (${failedAttempts}/5 lần). Còn ${MAX_ATTEMPTS - config.failedAttempts} lần thử hệ thống.` };
});


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

ipcMain.handle('logout', async () => {
    currentKey = null;
    isDecoyMode = false;
    if (vaultWatcher) { vaultWatcher.close(); vaultWatcher = null; }
    
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
    return true;
});

ipcMain.handle('get-dashboard-stats', async () => {
    if (!currentKey) return null;
    const vPath = getVaultPath();
    const files = await fs.readdir(vPath);
    let totalSize = 0;
    let encryptedCount = 0;
    
    for (const f of files) {
        if (f.endsWith('.k3enc')) {
            const stat = await fs.stat(path.join(vPath, f));
            totalSize += stat.size;
            encryptedCount++;
        }
    }
    
    // Calculate real security score
    const config = await readConfig() || {};
    let score = 30; // Điểm cơ bản cho việc sử dụng mã hóa AES-256-GCM chuẩn quân đội
    
    if (config.decoyHash) score += 30; // 30 điểm cho Két giả (Plausible Deniability)
    if (config.autoClearHistory) score += 20; // 20 điểm cho tính năng tự động xóa dấu vết
    if (config.cleanSystemJunk !== false) score += 10; // 10 điểm cho tính năng dọn rác hệ thống
    if (!config.showHidden) score += 10; // 10 điểm cho việc ẩn file hệ thống
    
    if (score > 100) score = 100;
    
    // Get disk usage
    let usbTotalSize = 0;
    let usbUsedSize = 0;
    try {
        const root = getUsbRoot();
        const drives = await si.fsSize();
        let bestDrive: any = null;
        let maxLen = -1;
        for (const d of drives) {
            if (root.startsWith(d.mount) && d.mount.length > maxLen) {
                maxLen = d.mount.length;
                bestDrive = d;
            }
        }
        if (bestDrive) {
            usbTotalSize = bestDrive.size;
            usbUsedSize = bestDrive.used;
        }
    } catch (e) {}

    return {
        totalSize,
        encryptedCount,
        isDecoyMode,
        securityScore: isDecoyMode ? 35 : score,
        usbTotalSize,
        usbUsedSize
    };
});

ipcMain.handle('list-files', async () => {
    if (!currentKey) throw new Error('Not authenticated');
    const vPath = getVaultPath();
    const files = await fs.readdir(vPath);
    const result = [];
    for (const f of files) {
        if (!f.endsWith('.k3enc')) continue;
        const stat = await fs.stat(path.join(vPath, f));
        
        let originalName = f;
        try {
            const fh = await fs.open(path.join(vPath, f), 'r');
            const header = Buffer.alloc(37);
            await fh.read(header, 0, 37, 0);
            
            const possibleFlag = header.readUInt8(32);
            let nameLen = 0;
            let dataOffset = 36;
            
            if (possibleFlag === 0 || possibleFlag === 1) {
                const possibleNameLen = header.readUInt32LE(33);
                if (possibleNameLen > 0 && possibleNameLen < 5000) {
                    nameLen = possibleNameLen;
                    dataOffset = 37;
                } else {
                    nameLen = header.readUInt32LE(32);
                }
            } else {
                nameLen = header.readUInt32LE(32);
            }
            
            if (nameLen > 0 && nameLen < 5000) {
                const nameBuf = Buffer.alloc(nameLen);
                await fh.read(nameBuf, 0, nameLen, dataOffset);
                originalName = nameBuf.toString('utf8');
            }
            await fh.close();
        } catch (e) {}

        result.push({
            name: f,
            originalName: originalName,
            isDirectory: originalName.endsWith('.k3dir'),
            size: stat.size,
            isEncrypted: true
        });
    }
    return result;
});

ipcMain.handle('scan-file', async (_, filePath: string) => {
    try {
        const stat = await fs.stat(filePath);
        if (stat.isDirectory()) return { safe: true, info: 'Thư mục' };
        
        // 1. Lớp 1: Quét Heuristics (kiểm tra nhanh hành vi/đuôi)
        const fileName = path.basename(filePath);
        const offlineThreat = await scanSingleFileOffline(filePath, fileName);
        if (offlineThreat) {
            return { safe: false, threat: `Heuristics: ${offlineThreat}` };
        }
        
        // 2. Lớp 2: Quét sâu bằng YARA Engine (100% Offline)
        const yaraThreat = await scanYaraOffline(filePath);
        if (yaraThreat) {
            return { safe: false, threat: `YARA Engine: Phát hiện mẫu ${yaraThreat}` };
        }
        
        return { safe: true, info: 'An toàn (Đã vượt qua Heuristics & YARA)' };
    } catch (e: any) {
        return { safe: false, threat: 'Lỗi đọc tệp: ' + e.message };
    }
});

ipcMain.handle('get-usb-history', async () => await readHistory());

ipcMain.handle('clear-usb-history', async () => {
    await writeHistory([]);
    return true;
});

ipcMain.handle('delete-usb-history-items', async (_, deviceIds: string[]) => {
    let history = await readHistory();
    history = history.filter((h: any) => !deviceIds.includes(h.deviceId));
    await writeHistory(history);
    return true;
});

async function processEncryption(event: any, sourcePath: string, secureDelete: boolean, vaultPath: string, enableCompression: boolean, forceRaw: boolean = false): Promise<number> {
    const cleanVaultPath = vaultPath.replace(/^\/+|\/+$/g, '');
    const baseName = path.basename(sourcePath);
    const virtualBasePath = cleanVaultPath ? `${cleanVaultPath}/${baseName}` : baseName;

    const stat = await fs.stat(sourcePath);
    if (stat.isDirectory()) {
        const iv = crypto.randomBytes(16);
        const cipher = crypto.createCipheriv(ALGORITHM, currentKey!, iv);
        const encrypted = Buffer.concat([cipher.update(Buffer.from('')), cipher.final()]);
        const authTag = cipher.getAuthTag();
        
        const nameBuffer = Buffer.from(`${virtualBasePath}/.k3empty`, 'utf8');
        const nameLen = Buffer.alloc(4);
        nameLen.writeUInt32LE(nameBuffer.length, 0);
        
        const isCompressed = Buffer.alloc(1);
        isCompressed.writeUInt8(0, 0);

        const finalData = Buffer.concat([iv, authTag, isCompressed, nameLen, nameBuffer, encrypted]);
        await fs.writeFile(path.join(getVaultPath(), `${crypto.randomBytes(8).toString('hex')}.k3enc`), finalData);
        
        let blockedCount = 0;
        const items = await fs.readdir(sourcePath);
        for (const item of items) {
            blockedCount += await processEncryption(event, path.join(sourcePath, item), false, virtualBasePath, enableCompression, forceRaw);
        }
        if (secureDelete) await shredFolder(sourcePath);
        return blockedCount;
    } else {
        // --- Quét đệ quy Offline Heuristics cho từng file (Bắt virus ẩn trong thư mục) ---
        const offlineThreat = await scanSingleFileOffline(sourcePath, baseName);
        if (offlineThreat) {
            event.sender.send('encryption-progress', { file: virtualBasePath, size: stat.size, status: `❌ Đã chặn (Mã độc: ${offlineThreat})` });
            return 1; // Bỏ qua file nhiễm độc, không mã hóa!
        }
        
        // --- Lớp 2: Quét YARA Offline ---
        const yaraThreat = await scanYaraOffline(sourcePath);
        if (yaraThreat) {
            event.sender.send('encryption-progress', { file: virtualBasePath, size: stat.size, status: `❌ Đã chặn (YARA: ${yaraThreat})` });
            return 1;
        }

        event.sender.send('encryption-progress', { file: virtualBasePath, size: stat.size, status: forceRaw ? 'copying raw' : 'encrypting' });
        
        const nameBuffer = Buffer.from(virtualBasePath, 'utf8');
        const nameLen = Buffer.alloc(4);
        nameLen.writeUInt32LE(nameBuffer.length, 0);
        
        const destFile = path.join(getVaultPath(), `${crypto.randomBytes(8).toString('hex')}.k3enc`);

        if (forceRaw) {
            const iv = crypto.randomBytes(16);
            const authTag = crypto.randomBytes(16);
            const isCompressed = Buffer.alloc(1);
            isCompressed.writeUInt8(2, 0); // 2 means RAW
            const headerData = Buffer.concat([iv, authTag, isCompressed, nameLen, nameBuffer]);

            await new Promise<void>((resolve, reject) => {
                const fsSync = require('fs');
                const readStream = fsSync.createReadStream(sourcePath);
                const writeStream = fsSync.createWriteStream(destFile);
                writeStream.write(headerData);
                readStream.pipe(writeStream);
                readStream.on('error', reject);
                writeStream.on('error', reject);
                writeStream.on('finish', resolve);
            });
            if (secureDelete) await shredFile(sourcePath);
        } else {
            const fileData = await fs.readFile(sourcePath);
            
            let dataToEncrypt = fileData;
            let compressedFlag = 0;
            
            if (enableCompression) {
                const ext = path.extname(sourcePath).toLowerCase();
                const noCompressExts = ['.zip', '.rar', '.7z', '.mp4', '.mkv', '.jpg', '.jpeg', '.png', '.mp3'];
                if (!noCompressExts.includes(ext) && fileData.length > 1024) {
                    dataToEncrypt = zlib.gzipSync(fileData);
                    compressedFlag = 1;
                }
            }

            const iv = crypto.randomBytes(16);
            const cipher = crypto.createCipheriv(ALGORITHM, currentKey!, iv);
            const encrypted = Buffer.concat([cipher.update(dataToEncrypt), cipher.final()]);
            const authTag = cipher.getAuthTag();
            
            const isCompressed = Buffer.alloc(1);
            isCompressed.writeUInt8(compressedFlag, 0);

            const finalData = Buffer.concat([iv, authTag, isCompressed, nameLen, nameBuffer, encrypted]);
            await fs.writeFile(destFile, finalData);
            if (secureDelete) await shredFile(sourcePath);
        }
        return 0;
    }
}

ipcMain.handle('encrypt-file', async (event, sourcePath: string, secureDelete: boolean = true, vaultPath: string = '/', enableCompression: boolean = true, forceRaw: boolean = false) => {
    if (!currentKey) throw new Error('Not authenticated');
    try {
        const blockedCount = await processEncryption(event, sourcePath, secureDelete, vaultPath, enableCompression, forceRaw);
        event.sender.send('encryption-progress', { file: 'Done', status: 'done' });
        return { success: true, blocked: blockedCount };
    } catch (e: any) { return { success: false, error: e.message }; }
});

ipcMain.handle('read-vault-text', async (event, fileName: string) => {
    if (!currentKey) throw new Error('Not authenticated');
    try {
        const data = await fs.readFile(path.join(getVaultPath(), fileName));
        const iv = data.subarray(0, 16);
        const authTag = data.subarray(16, 32);
        
        let isCompressed = 0;
        let dataOffset = 36;
        
        const possibleFlag = data.readUInt8(32);
        if (possibleFlag === 0 || possibleFlag === 1 || possibleFlag === 2) {
            const possibleNameLen = data.readUInt32LE(33);
            if (possibleNameLen > 0 && possibleNameLen < 5000 && 37 + possibleNameLen <= data.length) {
                isCompressed = possibleFlag;
                dataOffset = 37 + possibleNameLen;
            } else {
                const nameLen = data.readUInt32LE(32);
                dataOffset = 36 + nameLen;
            }
        } else {
            const nameLen = data.readUInt32LE(32);
            dataOffset = 36 + nameLen;
        }

        const payloadData = data.subarray(dataOffset);
        let decrypted = payloadData;

        if (isCompressed !== 2) {
            const decipher = crypto.createDecipheriv(ALGORITHM, currentKey, iv);
            decipher.setAuthTag(authTag);
            decrypted = Buffer.concat([decipher.update(payloadData), decipher.final()]);
        }
        
        if (isCompressed === 1) {
            try { decrypted = zlib.gunzipSync(decrypted); } catch(e) {}
        }
        
        return { success: true, text: decrypted.toString('utf8') };
    } catch (e: any) { return { success: false, error: e.message }; }
});

ipcMain.handle('write-vault-text', async (event, fileName: string | null, content: string, originalNameFullPath: string, enableCompression: boolean = true) => {
    if (!currentKey) throw new Error('Not authenticated');
    try {
        const fileData = Buffer.from(content, 'utf8');
        let dataToEncrypt = fileData;
        let compressedFlag = 0;
        
        if (enableCompression && fileData.length > 1024) {
            dataToEncrypt = zlib.gzipSync(fileData);
            compressedFlag = 1;
        }

        const iv = crypto.randomBytes(16);
        const cipher = crypto.createCipheriv(ALGORITHM, currentKey, iv);
        const encrypted = Buffer.concat([cipher.update(dataToEncrypt), cipher.final()]);
        const authTag = cipher.getAuthTag();
        
        const nameBuffer = Buffer.from(originalNameFullPath, 'utf8');
        const nameLen = Buffer.alloc(4);
        nameLen.writeUInt32LE(nameBuffer.length, 0);
        
        const isCompressedBuf = Buffer.alloc(1);
        isCompressedBuf.writeUInt8(compressedFlag, 0);

        const finalData = Buffer.concat([iv, authTag, isCompressedBuf, nameLen, nameBuffer, encrypted]);
        
        const targetHexName = fileName || `${crypto.randomBytes(8).toString('hex')}.k3enc`;
        await fs.writeFile(path.join(getVaultPath(), targetHexName), finalData);
        
        return { success: true };
    } catch (e: any) { return { success: false, error: e.message }; }
});

ipcMain.handle('decrypt-file', async (event, fileName: string, targetDir?: string, relativeStructure?: string) => {
    if (!currentKey) throw new Error('Not authenticated');
    try {
        const filePath = path.join(getVaultPath(), fileName);
        const fh = await fs.open(filePath, 'r');
        const headerBuf = Buffer.alloc(4096);
        const { bytesRead } = await fh.read(headerBuf, 0, 4096, 0);
        await fh.close();
        
        if (bytesRead < 36) throw new Error('Invalid file format');
        
        const iv = headerBuf.subarray(0, 16);
        const authTag = headerBuf.subarray(16, 32);
        
        let isCompressed = 0;
        let dataOffset = 36;
        
        const possibleFlag = headerBuf.readUInt8(32);
        let nameLen = 0;
        
        if (possibleFlag === 0 || possibleFlag === 1 || possibleFlag === 2) {
            const possibleNameLen = headerBuf.readUInt32LE(33);
            if (possibleNameLen > 0 && possibleNameLen < 5000) {
                isCompressed = possibleFlag;
                dataOffset = 37;
                nameLen = possibleNameLen;
            } else {
                nameLen = headerBuf.readUInt32LE(32);
            }
        } else {
            nameLen = headerBuf.readUInt32LE(32);
        }

        const originalNameFull = headerBuf.subarray(dataOffset, dataOffset + nameLen).toString('utf8');
        const originalName = originalNameFull.split('/').pop() || originalNameFull;
        event.sender.send('encryption-progress', { file: originalNameFull, status: isCompressed === 2 ? 'copying raw' : 'decrypting' });
        
        const outputName = relativeStructure ? relativeStructure : originalName;
        
        if (originalNameFull.endsWith('.k3empty')) {
            const folderName = relativeStructure ? relativeStructure.replace(/\.k3empty$/, '') : (originalNameFull.split('/').slice(-2, -1)[0] || 'New Folder');
            const outPath = targetDir ? path.join(targetDir, folderName) : path.join(path.dirname(getVaultPath()), 'Decrypted', folderName);
            await fs.mkdir(outPath, { recursive: true });
            return { success: true };
        }
        
        let outPath = targetDir ? path.join(targetDir, outputName) : path.join(path.dirname(getVaultPath()), 'Decrypted', outputName);
        await fs.mkdir(path.dirname(outPath), { recursive: true });
        
        const payloadOffset = dataOffset + nameLen;
        
        if (isCompressed === 2) {
            // RAW STREAM MODE
            await new Promise<void>((resolve, reject) => {
                // need standard fs module for createReadStream, we imported fs from 'fs/promises'
                // let's just use require('fs') to avoid import conflicts
                const fsSync = require('fs');
                const readStream = fsSync.createReadStream(filePath, { start: payloadOffset });
                const writeStream = fsSync.createWriteStream(outPath);
                readStream.pipe(writeStream);
                readStream.on('error', reject);
                writeStream.on('error', reject);
                writeStream.on('finish', resolve);
            });
            return { success: true };
        }
        
        // NORMAL DECRYPTION MODE
        const data = await fs.readFile(filePath);
        const encryptedData = data.subarray(payloadOffset);
        
        const decipher = crypto.createDecipheriv(ALGORITHM, currentKey, iv);
        decipher.setAuthTag(authTag);
        let decrypted = Buffer.concat([decipher.update(encryptedData), decipher.final()]);
        
        if (isCompressed === 1) {
            try { decrypted = zlib.gunzipSync(decrypted); } catch (e) { console.error('Gunzip failed', e); }
        }
        
        if (outputName.endsWith('.k3dir')) {
            outPath = outPath.replace('.k3dir', '');
            await fs.mkdir(outPath, { recursive: true });
            const tempZip = path.join(os.tmpdir(), `k3_${Date.now()}.zip`);
            await fs.writeFile(tempZip, decrypted);
            const zip = new AdmZip(tempZip);
            await new Promise<void>((resolve, reject) => zip.extractAllToAsync(outPath, true, false, (err) => err ? reject(err) : resolve()));
            await fs.unlink(tempZip);
        } else {
            await fs.writeFile(outPath, decrypted);
        }
        
        return { success: true };
    } catch (e: any) { return { success: false, error: e.message }; }
});

ipcMain.handle('export-zip', async (event, files: {fileName: string, relativeStructure: string}[], passwordToSet: string, savePath: string) => {
    if (!currentKey) throw new Error('Not authenticated');
    try {
        return new Promise((resolve) => {
            const output = createWriteStream(savePath);
            const archive = archiver('zip-encrypted', {
                zlib: { level: 8 },
                encryptionMethod: 'aes256',
                password: passwordToSet
            });
            output.on('close', () => resolve({ success: true }));
            archive.on('error', (err: any) => resolve({ success: false, error: err.message }));
            archive.pipe(output);
            
            (async () => {
                for (const fileObj of files) {
                    const data = await fs.readFile(path.join(getVaultPath(), fileObj.fileName));
                    const iv = data.subarray(0, 16);
                    const authTag = data.subarray(16, 32);
                    
                    let isCompressed = 0;
                    let nameLenOffset = 32;
                    let dataOffset = 36;
                    
                    const possibleFlag = data.readUInt8(32);
                    let nameLen = 0;
                    
                    if (possibleFlag === 0 || possibleFlag === 1) {
                        const possibleNameLen = data.readUInt32LE(33);
                        if (possibleNameLen > 0 && possibleNameLen < 5000 && 37 + possibleNameLen <= data.length) {
                            isCompressed = possibleFlag;
                            nameLenOffset = 33;
                            dataOffset = 37;
                            nameLen = possibleNameLen;
                        } else {
                            nameLen = data.readUInt32LE(32);
                        }
                    } else {
                        nameLen = data.readUInt32LE(32);
                    }
                    
                    const originalNameFull = data.subarray(dataOffset, dataOffset + nameLen).toString('utf8');
                    const originalName = originalNameFull.split('/').pop() || originalNameFull;
                    
                    const encryptedData = data.subarray(dataOffset + nameLen);
                    const decipher = crypto.createDecipheriv(ALGORITHM, currentKey, iv);
                    decipher.setAuthTag(authTag);
                    let decrypted = Buffer.concat([decipher.update(encryptedData), decipher.final()]);
                    if (isCompressed === 1) {
                        try { decrypted = zlib.gunzipSync(decrypted); } catch(e) {}
                    }
                    
                    const archiveName = fileObj.relativeStructure || originalName;
                    
                    if (originalNameFull.endsWith('.k3empty')) {
                        archive.append('', { name: archiveName.replace('.k3empty', '/') });
                    } else if (archiveName.endsWith('.k3dir')) {
                        archive.append(decrypted, { name: archiveName.replace('.k3dir', '.zip') });
                    } else {
                        archive.append(decrypted, { name: archiveName });
                    }
                }
                archive.finalize();
            })().catch(e => {
                resolve({ success: false, error: e.message });
            });
        });
    } catch (e: any) { return { success: false, error: e.message }; }
});

ipcMain.handle('delete-file', async (_, fileName: string) => {
    if (!currentKey) throw new Error('Not authenticated');
    const filePath = path.join(getVaultPath(), fileName);
    await fs.rm(filePath);
    return true;
});

ipcMain.handle('create-vault-folder', async (_, folderName: string, vaultPath: string = '/') => {
    if (!currentKey) throw new Error('Not authenticated');
    try {
        const cleanVaultPath = vaultPath.replace(/^\/+|\/+$/g, '');
        const virtualPath = cleanVaultPath ? `${cleanVaultPath}/${folderName}/.k3empty` : `${folderName}/.k3empty`;
        
        const iv = crypto.randomBytes(16);
        const cipher = crypto.createCipheriv(ALGORITHM, currentKey, iv);
        const encrypted = Buffer.concat([cipher.update(Buffer.from('')), cipher.final()]);
        const authTag = cipher.getAuthTag();
        
        const nameBuffer = Buffer.from(virtualPath, 'utf8');
        const nameLen = Buffer.alloc(4);
        nameLen.writeUInt32LE(nameBuffer.length, 0);
        
        const isCompressed = Buffer.alloc(1);
        isCompressed.writeUInt8(0, 0);
        
        const finalData = Buffer.concat([iv, authTag, isCompressed, nameLen, nameBuffer, encrypted]);
        await fs.writeFile(path.join(getVaultPath(), `${crypto.randomBytes(8).toString('hex')}.k3enc`), finalData);
        return { success: true, vaultDir: getVaultPath() };
    } catch (e: any) { return { success: false, error: e.message }; }
});

ipcMain.handle('enable-hwid-lock', async () => {
    const config = await readConfig();
    if (!config) return { success: false, error: 'Not setup' };
    
    try {
        const devices = await si.blockDevices();
        let currentHwid = '';
        let maxLen = -1;
        for (const dev of devices) {
            if (dev.mount && USB_DIR.startsWith(dev.mount)) {
                if (dev.mount.length > maxLen) {
                    maxLen = dev.mount.length;
                    currentHwid = (dev.serial || '') + (dev.uuid || '');
                }
            }
        }
        
        if (!currentHwid) return { success: false, error: 'Không thể đọc được ID phần cứng của USB này' };
        
        config.hwidEnabled = true;
        config.expectedHwid = currentHwid;
        await writeConfig(config);
        return { success: true };
    } catch (e: any) {
        return { success: false, error: e.message };
    }
});

ipcMain.handle('check-readonly', async () => {
    try {
        const testFile = path.join(getUsbRoot(), '.k3_readonly_test');
        await fs.writeFile(testFile, 'test');
        await fs.unlink(testFile);
        return false;
    } catch (e: any) {
        if (e.code === 'EPERM' || e.code === 'EROFS' || e.message.toLowerCase().includes('read-only') || e.message.toLowerCase().includes('write')) {
            return true;
        }
        return false; 
    }
});

ipcMain.handle('toggle-readonly', async (_, enable: boolean) => {
    if (process.platform !== 'win32') {
        return { success: false, error: 'Chỉ hỗ trợ trên hệ điều hành Windows.' };
    }
    try {
        const driveLetter = getUsbRoot().substring(0, 1);
        const scriptContent = `select volume ${driveLetter}\nattributes volume ${enable ? 'set' : 'clear'} readonly\n`;
        const scriptPath = path.join(os.tmpdir(), `k3_dp_${Date.now()}.txt`);
        await fs.writeFile(scriptPath, scriptContent);
        
        return new Promise((resolve) => {
            const cmd = `powershell -Command "Start-Process diskpart -ArgumentList '/s ${scriptPath}' -Verb RunAs -WindowStyle Hidden"`;
            exec(cmd, () => {
                setTimeout(async () => {
                    try { await fs.unlink(scriptPath); } catch {}
                }, 5000);
                resolve({ success: true });
            });
        });
    } catch (e: any) {
        return { success: false, error: e.message };
    }
});

ipcMain.handle('select-files', async () => {
    const { canceled, filePaths } = await dialog.showOpenDialog({ properties: ['openFile', 'multiSelections'] });
    if (canceled) return [];
    return filePaths;
});

ipcMain.handle('get-drives', async () => {
    if (process.platform === 'win32') {
        return [
            { name: 'Win (C:)', path: 'C:\\' },
            { name: 'DATA (D:)', path: 'D:\\' },
            { name: 'Desktop', path: path.join(os.homedir(), 'Desktop') }
        ];
    } else {
        return [
            { name: 'Root (/)', path: '/' },
            { name: 'Home', path: os.homedir() },
            { name: 'Desktop', path: path.join(os.homedir(), 'Desktop') }
        ];
    }
});

ipcMain.handle('list-local-dir', async (_, dirPath: string) => {
    try {
        const config = await readConfig() || {};
        const cleanSystem = config.cleanSystemJunk !== false; 
        const showHidden = config.showHidden === true;
        
        const files = await fs.readdir(dirPath, { withFileTypes: true });
        const result = [];
        for (const f of files) {
            if (!showHidden && f.name.startsWith('.')) continue;
            if (cleanSystem) {
                const lowerName = f.name.toLowerCase();
                if (f.name.startsWith('._') || f.name === '.DS_Store' || lowerName === 'thumbs.db' || lowerName === 'desktop.ini') {
                    fs.rm(path.join(dirPath, f.name), { force: true, recursive: true }).catch(()=>{});
                    continue;
                }
                if (lowerName === '$recycle.bin' || lowerName === 'system volume information') continue;
            }
            
            let size = 0;
            if (!f.isDirectory()) {
                try { const stat = await fs.stat(path.join(dirPath, f.name)); size = stat.size; } catch(e) {}
            }
            result.push({
                name: f.name,
                isDirectory: f.isDirectory(),
                path: path.join(dirPath, f.name),
                size
            });
        }
        return result.sort((a, b) => {
            if (a.isDirectory === b.isDirectory) return a.name.localeCompare(b.name);
            return a.isDirectory ? -1 : 1;
        });
    } catch (e) {
        return [];
    }
});

ipcMain.handle('copy-local-file', async (_, source: string, target: string) => {
    try {
        const stat = await fs.stat(source);
        if (stat.isDirectory()) await fs.cp(source, target, { recursive: true });
        else await fs.copyFile(source, target);
        return { success: true };
    } catch (e: any) { return { success: false, error: e.message }; }
});

ipcMain.handle('move-local-file', async (_, source: string, target: string) => {
    try {
        await fs.rename(source, target);
        return { success: true };
    } catch (e: any) { return { success: false, error: e.message }; }
});

ipcMain.handle('delete-local-file', async (_, filePath: string, secure: boolean = false) => {
    try {
        if (secure) {
            const stat = await fs.stat(filePath);
            if (stat.isDirectory()) await shredFolder(filePath);
            else await shredFile(filePath);
        } else await fs.rm(filePath, { recursive: true, force: true });
        return { success: true };
    } catch (e: any) { return { success: false, error: e.message }; }
});

ipcMain.handle('rename-local', async (_, oldPath: string, newPath: string) => {
    try {
        await fs.rename(oldPath, newPath);
        return { success: true };
    } catch (e: any) { return { success: false, error: e.message }; }
});

ipcMain.handle('create-local-folder', async (_, dirPath: string) => {
    try {
        await fs.mkdir(dirPath, { recursive: true });
        return { success: true };
    } catch (e: any) { return { success: false, error: e.message }; }
});

// --- DEDICATED SCANNER ENGINE ---
let isScanning = false;

async function scanSingleFileOffline(fullPath: string, fileName: string): Promise<string> {
    let threat = '';
    const lowerName = fileName.toLowerCase();
    
    // Heuristics: Các loại đuôi file script thường dùng phát tán qua USB
    if (lowerName.endsWith('.bat') || lowerName.endsWith('.vbs') || lowerName.endsWith('.ps1')) {
        threat = 'Heuristics.Script.Suspicious';
    } 
    // Heuristics: File 2 đuôi (Double-Extension) để lừa người dùng
    else if (lowerName.match(/\.[a-z0-9]+\.(exe|vbs|bat|cmd|pif|scr)$/)) {
        threat = 'Heuristics.DoubleExtension.Trojan';
    }
    
    // Kiểm tra EICAR theo tên file
    if (!threat && (lowerName.includes('eicar') || lowerName.includes('virus'))) {
        threat = 'EICAR-Test-Signature';
    }
    
    // Kiểm tra mẫu chữ ký virus (Signature-based) đối với các file dung lượng nhỏ
    if (!threat && lowerName.endsWith('.txt')) {
        try {
            const stat = await fs.stat(fullPath);
            if (stat.size < 1000) {
                const data = await fs.readFile(fullPath, 'utf8');
                if (data.includes('X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR')) {
                    threat = 'EICAR-Test-Signature';
                }
            }
        } catch {}
    }
    return threat;
}

const scanDirectory = async (dirPath: string, win: BrowserWindow, config: any) => {
    if (!isScanning) return;
    try {
        const files = await fs.readdir(dirPath, { withFileTypes: true });
        for (const f of files) {
            if (!isScanning) break;
            const fullPath = path.join(dirPath, f.name);
            if (f.isDirectory()) {
                await scanDirectory(fullPath, win, config);
            } else {
                win.webContents.send('scanner-progress', { path: fullPath, name: f.name });
                
                let threat = await scanSingleFileOffline(fullPath, f.name);
                if (!threat) {
                    const yaraThreat = await scanYaraOffline(fullPath);
                    if (yaraThreat) threat = `YARA: ${yaraThreat}`;
                }
                
                if (threat) {
                    win.webContents.send('scanner-threat-found', { path: fullPath, name: f.name, threat });
                }
                // Throttling for UI responsiveness
                await new Promise(r => setTimeout(r, 10));
            }
        }
    } catch (e) {}
};

ipcMain.handle('scanner-start', async (event, targetPaths: string[]) => {
    if (isScanning) return { success: false, error: 'Đang có tiến trình quét' };
    isScanning = true;
    const config = await readConfig();
    const win = BrowserWindow.fromWebContents(event.sender);
    
    (async () => {
        try {
            for (const p of targetPaths) {
                if (!isScanning) break;
                const stat = await fs.stat(p).catch(() => null);
                if (!stat) continue;
                
                if (stat.isDirectory()) {
                    await scanDirectory(p, win!, config);
                } else {
                    win!.webContents.send('scanner-progress', { path: p, name: path.basename(p) });
                    const fName = path.basename(p);
                    let threat = await scanSingleFileOffline(p, fName);
                    if (!threat) {
                        const yaraThreat = await scanYaraOffline(p);
                        if (yaraThreat) threat = `YARA: ${yaraThreat}`;
                    }
                    
                    if (threat) {
                        win!.webContents.send('scanner-threat-found', { path: p, name: fName, threat });
                    }
                    await new Promise(r => setTimeout(r, 50));
                }
            }
        } catch (e) {}
        isScanning = false;
        win?.webContents.send('scanner-done');
    })();
    return { success: true };
});

ipcMain.handle('scanner-stop', () => {
    isScanning = false;
    return true;
});

ipcMain.handle('show-open-dialog', async (event, options) => {
    const win = BrowserWindow.fromWebContents(event.sender);
    if (!win) return [];
    const { canceled, filePaths } = await dialog.showOpenDialog(win, options);
    if (!canceled) {
        return filePaths;
    }
    return [];
});

ipcMain.handle('show-save-dialog', async (event, options) => {
    const win = BrowserWindow.fromWebContents(event.sender);
    if (!win) return null;
    const { canceled, filePath } = await dialog.showSaveDialog(win, options);
    if (!canceled) {
        return filePath;
    }
    return null;
});



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

app.whenReady().then(async () => {
    await initVault();
    await initYaraScanner();
    autoUpdateDatabase(); // Khởi chạy auto update ngầm
    const config = await readConfig();
    if (config && config.autoClearHistory) await writeHistory([]);
    createWindow();
    startUSBPoll();
});

app.on('window-all-closed', async () => {
    const config = await readConfig();
    if (config && config.autoClearHistory) await writeHistory([]);
    if (process.platform !== 'darwin') app.quit();
});

ipcMain.handle('check-vault-integrity', async (event) => {
    if (!currentKey) return { success: false, error: 'Not authenticated' };
    try {
        const vaultDir = getVaultPath();
        const files = await fs.readdir(vaultDir);
        let corruptedFiles = [];
        let totalFiles = 0;
        
        for (const f of files) {
            if (!f.endsWith('.k3enc')) continue;
            totalFiles++;
            const p = path.join(vaultDir, f);
            const data = await fs.readFile(p);
            
            try {
                const iv = data.subarray(0, 16);
                const authTag = data.subarray(16, 32);
                
                const possibleFlag = data.readUInt8(32);
                let nameLen = 0;
                let dataOffset = 36;
                if (possibleFlag === 0 || possibleFlag === 1) {
                    const possibleNameLen = data.readUInt32LE(33);
                    if (possibleNameLen > 0 && possibleNameLen < 5000 && 37 + possibleNameLen <= data.length) {
                        nameLen = possibleNameLen;
                        dataOffset = 37;
                    } else { nameLen = data.readUInt32LE(32); }
                } else { nameLen = data.readUInt32LE(32); }
                
                const originalNameFull = data.subarray(dataOffset, dataOffset + nameLen).toString('utf8');
                const encryptedData = data.subarray(dataOffset + nameLen);
                
                const decipher = crypto.createDecipheriv(ALGORITHM, currentKey, iv);
                decipher.setAuthTag(authTag);
                decipher.update(encryptedData);
                decipher.final(); // This will throw if integrity fails
            } catch (err) {
                // If anything throws, file is corrupted
                corruptedFiles.push(f);
            }
        }
        
        return { success: true, corruptedFiles, totalFiles };
    } catch (e: any) {
        return { success: false, error: e.message };
    }
});

ipcMain.handle('search-everything', async (event, query) => {
    if (!query || query.trim() === '') return { success: true, results: [] };
    
    return new Promise((resolve) => {
        try {
            if (process.platform === 'win32') {
                const esPath = isDev 
                    ? path.join(__dirname, '../../electron/bin/es.exe')
                    : path.join(process.resourcesPath, 'bin/es.exe');
                    
                exec(`"${esPath}" -csv -n 50 "${query}"`, { timeout: 5000 }, (err, stdout) => {
                    // Ignore errors if we still got stdout
                    if (err && !stdout) return resolve({ success: false, error: err.message });
                    const lines = stdout.split('\n').filter(l => l.trim() !== '');
                    if (lines.length > 0) lines.shift();
                    const results = lines.map(l => {
                        const parts = l.split(',');
                        if (parts.length >= 2) {
                            const name = parts[0].replace(/^"|"$/g, '');
                            const p = parts[1].replace(/^"|"$/g, '');
                            return { name, path: path.join(p, name), isDirectory: false };
                        }
                        return null;
                    }).filter(Boolean);
                    resolve({ success: true, results });
                });
            } else if (process.platform === 'darwin') {
                exec(`mdfind -name "${query}" | head -n 50`, { timeout: 5000 }, (err, stdout) => {
                    // Ignore SIGPIPE errors if we have stdout
                    if (err && !stdout) return resolve({ success: false, error: err.message });
                    const lines = stdout.split('\n').filter(l => l.trim() !== '');
                    const results = lines.map(p => {
                        return { name: path.basename(p), path: p, isDirectory: false };
                    });
                    resolve({ success: true, results });
                });
            } else {
                resolve({ success: false, error: 'OS not supported for global search' });
            }
        } catch (e: any) {
            resolve({ success: false, error: e.message });
        }
    });
});

ipcMain.handle('format-vault', async () => {
    try {
        const vaultDir = getVaultPath();
        await fs.rm(vaultDir, { recursive: true, force: true });
        await initVault(); // recreate empty vault
        return { success: true };
    } catch (e: any) {
        return { success: false, error: e.message };
    }
});
