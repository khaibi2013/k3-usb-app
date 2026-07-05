var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));

// electron/main.ts
var import_electron = require("electron");
var path = __toESM(require("path"), 1);
var fs = __toESM(require("fs/promises"), 1);
var chokidar = __toESM(require("chokidar"), 1);
var crypto = __toESM(require("crypto"), 1);
var zlib = __toESM(require("zlib"), 1);
var os = __toESM(require("os"), 1);
var https = __toESM(require("https"), 1);
var import_child_process = require("child_process");
var import_adm_zip = __toESM(require("adm-zip"), 1);
var import_systeminformation = __toESM(require("systeminformation"), 1);
var import_fs = require("fs");
var archiver = require("archiver");
var archiverZipEncrypted = require("archiver-zip-encrypted");
var yara = require("@virustotal/yara-x");
var yaraScanner = null;
async function initYaraScanner() {
  try {
    const appPath = import_electron.app.getAppPath();
    const wasmPath = path.join(appPath, "node_modules/@virustotal/yara-x/pkg/yara_x_js_bg.wasm");
    const fsSync = require("fs");
    const wasmBuffer = fsSync.readFileSync(wasmPath);
    yara.initSync({ module: wasmBuffer });
    const rulesPath = path.join(appPath, "electron", "scanner", "k3_rules.yar");
    const rulesStr = await fs.readFile(rulesPath, "utf8");
    const compiler = new yara.Compiler();
    compiler.addSource(rulesStr);
    const rules = compiler.build();
    yaraScanner = new yara.Scanner(rules);
    console.log("YARA Scanner initialized successfully.");
  } catch (e) {
    console.error("Failed to init YARA scanner:", e);
  }
}
async function autoUpdateDatabase() {
  try {
    const url = "https://raw.githubusercontent.com/khaibi2013/k3-usb-app/main/electron/scanner/k3_rules.yar";
    https.get(url, (res) => {
      if (res.statusCode === 200) {
        let data = "";
        res.on("data", (chunk) => data += chunk);
        res.on("end", async () => {
          if (data.includes("rule ") && data.includes("condition:")) {
            const rulesPath = path.join(import_electron.app.getAppPath(), "electron", "scanner", "k3_rules.yar");
            await fs.writeFile(rulesPath, data);
            await initYaraScanner();
          }
        });
      }
    }).on("error", () => {
    });
  } catch (e) {
  }
}
async function scanYaraOffline(filePath) {
  if (!yaraScanner) return null;
  try {
    const buffer = await fs.readFile(filePath);
    const results = yaraScanner.scan(buffer);
    if (results && results.matches && results.matches.length > 0) {
      return results.matches.map((m) => m.identifier).join(", ");
    }
  } catch (e) {
  }
  return null;
}
archiver.registerFormat("zip-encrypted", archiverZipEncrypted);
var isDev = process.env.NODE_ENV === "development";
var ALGORITHM = "aes-256-gcm";
var MAX_ATTEMPTS = 10;
var currentKey = null;
var isDecoyMode = false;
var failedAttempts = 0;
var lockoutUntil = 0;
var getUsbRoot = () => {
  if (isDev) return path.join(__dirname, "../../");
  if (process.platform === "win32") {
    return process.env.PORTABLE_EXECUTABLE_DIR || path.dirname(import_electron.app.getPath("exe"));
  } else if (process.platform === "darwin") {
    return path.resolve(import_electron.app.getPath("exe"), "../../..");
  }
  return path.dirname(import_electron.app.getPath("exe"));
};
var getVaultPath = () => {
  const folder = isDecoyMode ? ".vault_decoy" : ".vault";
  return path.join(getUsbRoot(), folder);
};
var getConfigPath = () => {
  return path.join(getUsbRoot(), ".vault_config.json");
};
var initVault = async () => {
  try {
    await fs.mkdir(path.join(getUsbRoot(), ".vault"), { recursive: true });
  } catch (e) {
  }
  try {
    await fs.mkdir(path.join(getUsbRoot(), ".vault_decoy"), { recursive: true });
  } catch (e) {
  }
};
var deriveKey = (password, salt) => {
  return crypto.scryptSync(password, salt, 32);
};
var hashPassword = (password) => {
  const salt = crypto.randomBytes(16).toString("hex");
  const hash = crypto.scryptSync(password, salt, 64).toString("hex");
  return `${salt}:${hash}`;
};
var verifyPassword = (password, storedHash) => {
  const [salt, hash] = storedHash.split(":");
  const derivedHash = crypto.scryptSync(password, salt, 64).toString("hex");
  return derivedHash === hash;
};
var readConfig = async () => {
  try {
    const configStr = await fs.readFile(getConfigPath(), "utf8");
    return JSON.parse(configStr);
  } catch {
    return null;
  }
};
var writeConfig = async (config) => {
  try {
    await fs.writeFile(getConfigPath(), JSON.stringify(config, null, 2));
  } catch (e) {
  }
};
var HISTORY_FILE = path.join(import_electron.app.getPath("userData"), "history.json");
var readHistory = async () => {
  try {
    const data = await fs.readFile(HISTORY_FILE, "utf8");
    return JSON.parse(data);
  } catch (e) {
    return [];
  }
};
var writeHistory = async (history) => {
  try {
    await fs.writeFile(HISTORY_FILE, JSON.stringify(history));
  } catch (e) {
  }
};
var pollUSBDevices = () => {
  return new Promise((resolve2) => {
    if (process.platform === "win32") {
      (0, import_child_process.exec)('wmic diskdrive where interfacetype="USB" get caption, pnpdeviceid, serialnumber /value', (err, stdout) => {
        if (err) return resolve2([]);
        const devices = [];
        let currentDevice = {};
        for (const line of stdout.split("\n")) {
          const t = line.trim();
          if (!t) {
            if (currentDevice.Caption) devices.push(currentDevice);
            currentDevice = {};
            continue;
          }
          const [key, ...vals] = t.split("=");
          if (key && vals.length) currentDevice[key.trim()] = vals.join("=").trim();
        }
        if (currentDevice.Caption) devices.push(currentDevice);
        resolve2(devices.map((d) => ({
          name: d.Caption || "USB Device",
          type: "USB Storage",
          serial: d.SerialNumber || "Unknown",
          deviceId: d.PNPDeviceID || "Unknown"
        })));
      });
    } else if (process.platform === "darwin") {
      (0, import_child_process.exec)("system_profiler SPUSBDataType -json", (err, stdout) => {
        if (err) return resolve2([]);
        try {
          const data = JSON.parse(stdout);
          const devices = [];
          const findDevices = (items) => {
            for (const item of items) {
              if (item.serial_num || item.name && item.name.toLowerCase().includes("usb")) {
                if (item.serial_num) {
                  devices.push({
                    name: item._name || item.name || "USB Device",
                    type: "USB Storage",
                    serial: item.serial_num,
                    deviceId: item.device_id || item.serial_num
                  });
                }
              }
              if (item._items) findDevices(item._items);
            }
          };
          if (data.SPUSBDataType) findDevices(data.SPUSBDataType);
          resolve2(devices);
        } catch {
          resolve2([]);
        }
      });
    } else {
      resolve2([]);
    }
  });
};
var vaultWatcher = null;
async function setupVaultWatcher(config) {
  if (vaultWatcher) {
    await vaultWatcher.close();
    vaultWatcher = null;
  }
  if (!currentKey || isDecoyMode) return;
  const folderName = config.autoEncryptFolder || "BaoMat";
  if (!folderName.trim()) return;
  const watchDir = path.join(getUsbRoot(), folderName);
  try {
    await fs.mkdir(watchDir, { recursive: true });
  } catch (e) {
  }
  vaultWatcher = chokidar.watch(watchDir, {
    persistent: true,
    ignoreInitial: true,
    awaitWriteFinish: {
      stabilityThreshold: 1e3,
      pollInterval: 100
    }
  });
  vaultWatcher.on("add", async (filePath) => {
    try {
      const stat2 = await fs.stat(filePath);
      let forceRaw = false;
      const sizeMB = stat2.size / (1024 * 1024);
      if (config.maxEncryptSize > 0 && sizeMB > config.maxEncryptSize) {
        forceRaw = true;
      }
      const fakeEvent = {
        sender: {
          send: (channel, data) => {
            import_electron.BrowserWindow.getAllWindows().forEach((w) => w.webContents.send(channel, data));
          }
        }
      };
      import_electron.BrowserWindow.getAllWindows().forEach((w) => w.webContents.send("encryption-progress", { file: path.basename(filePath), status: forceRaw ? "copying raw (background)" : "encrypting (background)" }));
      await processEncryption(fakeEvent, filePath, true, "/", true, forceRaw);
      import_electron.BrowserWindow.getAllWindows().forEach((w) => {
        w.webContents.send("encryption-progress", { file: "Done", status: "done" });
        w.webContents.send("auto-encrypt-done");
      });
    } catch (e) {
      console.error("Auto encrypt error:", e);
    }
  });
}
var startUSBPoll = async () => {
  setInterval(async () => {
    const history = await readHistory();
    const devices = await pollUSBDevices();
    const newIds = /* @__PURE__ */ new Set();
    let updated = false;
    for (const d of devices) {
      newIds.add(d.deviceId);
      const existing = history.find((h) => h.deviceId === d.deviceId);
      if (!existing) {
        history.unshift({
          time: (/* @__PURE__ */ new Date()).toLocaleString("vi-VN"),
          name: d.name,
          type: d.type,
          status: "\u0110ang k\u1EBFt n\u1ED1i",
          serial: d.serial,
          deviceId: d.deviceId
        });
        updated = true;
      } else if (existing.status !== "\u0110ang k\u1EBFt n\u1ED1i") {
        existing.status = "\u0110ang k\u1EBFt n\u1ED1i";
        existing.time = (/* @__PURE__ */ new Date()).toLocaleString("vi-VN");
        updated = true;
      }
    }
    for (const h of history) {
      if (h.status === "\u0110ang k\u1EBFt n\u1ED1i" && !newIds.has(h.deviceId)) {
        h.status = "\u0110\xE3 ng\u1EAFt";
        updated = true;
      }
    }
    if (updated) {
      await writeHistory(history);
      import_electron.BrowserWindow.getAllWindows().forEach((w) => w.webContents.send("usb-history-updated"));
    }
  }, 5e3);
};
var createWindow = () => {
  const win = new import_electron.BrowserWindow({
    width: 1100,
    height: 750,
    titleBarStyle: "hidden",
    webPreferences: {
      preload: path.join(__dirname, "preload.cjs"),
      nodeIntegration: false,
      contextIsolation: true
    }
  });
  if (isDev) {
    win.loadURL("http://localhost:5173");
    win.webContents.openDevTools();
  } else {
    win.loadFile(path.join(__dirname, "../dist/index.html"));
  }
};
var shredFile = async (filePath) => {
  try {
    const stat2 = await fs.stat(filePath);
    if (stat2.isDirectory()) return;
    const fh = await fs.open(filePath, "r+");
    const chunkSize = 1024 * 1024;
    const passes = 3;
    for (let p = 0; p < passes; p++) {
      let written = 0;
      const buffer = crypto.randomBytes(chunkSize);
      while (written < stat2.size) {
        const toWrite = Math.min(chunkSize, stat2.size - written);
        await fh.write(buffer, 0, toWrite, written);
        written += toWrite;
      }
    }
    await fh.close();
    await fs.unlink(filePath);
  } catch (e) {
    console.error("Shred error", e);
    try {
      await fs.unlink(filePath);
    } catch {
    }
  }
};
var shredFolder = async (dirPath) => {
  try {
    const files = await fs.readdir(dirPath, { withFileTypes: true });
    for (const f of files) {
      const p = path.join(dirPath, f.name);
      if (f.isDirectory()) await shredFolder(p);
      else await shredFile(p);
    }
    await fs.rmdir(dirPath);
  } catch (e) {
    console.error("Shred folder error", e);
  }
};
import_electron.ipcMain.handle("check-setup", async () => {
  const config = await readConfig();
  return !!(config && config.passwordHash);
});
import_electron.ipcMain.handle("get-config", async () => await readConfig() || {});
import_electron.ipcMain.handle("update-config", async (_, newSettings) => {
  const config = await readConfig() || {};
  Object.assign(config, newSettings);
  await writeConfig(config);
  setupVaultWatcher(config);
  return true;
});
import_electron.ipcMain.handle("setup-password", async (_, password) => {
  const config = { passwordHash: hashPassword(password), failedAttempts: 0, locked: false };
  await writeConfig(config);
  return true;
});
import_electron.ipcMain.handle("setup-decoy", async (_, password) => {
  const config = await readConfig();
  if (!config) return false;
  config.decoyHash = hashPassword(password);
  await writeConfig(config);
  return true;
});
import_electron.ipcMain.handle("login", async (_, password) => {
  if (Date.now() < lockoutUntil) {
    const remain = Math.ceil((lockoutUntil - Date.now()) / 1e3);
    return { success: false, error: `B\u1ECB kh\xF3a do nh\u1EADp sai nhi\u1EC1u l\u1EA7n. Th\u1EED l\u1EA1i sau ${remain}s.` };
  }
  const config = await readConfig();
  if (!config) return { success: false, error: "Not setup" };
  if (config.locked || config.failedAttempts >= MAX_ATTEMPTS) {
    config.locked = true;
    try {
      await fs.rm(path.join(getUsbRoot(), ".vault"), { recursive: true, force: true });
      await fs.rm(path.join(getUsbRoot(), ".decoy_vault"), { recursive: true, force: true });
      delete config.passwordHash;
      delete config.decoyHash;
    } catch (e) {
    }
    await writeConfig(config);
    return { success: false, error: "Thi\u1EBFt b\u1ECB \u0111\xE3 T\u1EF0 H\u1EE6Y D\u1EEE LI\u1EC6U do nh\u1EADp sai qu\xE1 nhi\u1EC1u l\u1EA7n \u0111\u1EC3 b\u1EA3o v\u1EC7 an to\xE0n!", locked: true };
  }
  if (config.hwidEnabled && config.expectedHwid) {
    const devices = await import_systeminformation.default.blockDevices();
    let currentHwid = "";
    let maxLen = -1;
    for (const dev of devices) {
      if (dev.mount && USB_DIR.startsWith(dev.mount)) {
        if (dev.mount.length > maxLen) {
          maxLen = dev.mount.length;
          currentHwid = (dev.serial || "") + (dev.uuid || "");
        }
      }
    }
    if (currentHwid !== config.expectedHwid) {
      return { success: false, error: "L\u1ED7i b\u1EA3o m\u1EADt: D\u1EEF li\u1EC7u b\u1ECB copy tr\xE1i ph\xE9p sang USB kh\xE1c!" };
    }
  }
  if (verifyPassword(password, config.passwordHash)) {
    config.failedAttempts = 0;
    await writeConfig(config);
    failedAttempts = 0;
    currentKey = deriveKey(password, Buffer.from(config.passwordHash.split(":")[0], "hex"));
    isDecoyMode = false;
    setupVaultWatcher(config);
    return { success: true, isDecoy: false };
  }
  if (config.decoyHash && verifyPassword(password, config.decoyHash)) {
    config.failedAttempts = 0;
    await writeConfig(config);
    failedAttempts = 0;
    currentKey = deriveKey(password, Buffer.from(config.decoyHash.split(":")[0], "hex"));
    isDecoyMode = true;
    return { success: true, isDecoy: true };
  }
  config.failedAttempts = (config.failedAttempts || 0) + 1;
  await writeConfig(config);
  failedAttempts++;
  if (failedAttempts >= 5) {
    lockoutUntil = Date.now() + 5 * 60 * 1e3;
    failedAttempts = 0;
    return { success: false, error: "Nh\u1EADp sai 5 l\u1EA7n. K\xE9t s\u1EAFt t\u1EF1 \u0111\u1ED9ng kh\xF3a 5 ph\xFAt!" };
  }
  return { success: false, error: `Sai m\u1EADt kh\u1EA9u (${failedAttempts}/5 l\u1EA7n). C\xF2n ${MAX_ATTEMPTS - config.failedAttempts} l\u1EA7n th\u1EED h\u1EC7 th\u1ED1ng.` };
});
import_electron.ipcMain.handle("copy-raw-file", async (event, fileName, targetDir) => {
  if (!currentKey) return { success: false, error: "Not authenticated" };
  try {
    const vPath = getVaultPath();
    const sourceFile = path.join(vPath, fileName);
    const destFile = path.join(targetDir, fileName);
    const win = import_electron.BrowserWindow.fromWebContents(event.sender);
    if (win) win.webContents.send("encryption-progress", { file: fileName, status: "\u0111ang xu\u1EA5t nguy\xEAn b\u1EA3n", percent: 50 });
    await fs.copyFile(sourceFile, destFile);
    return { success: true };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.ipcMain.handle("logout", async () => {
  currentKey = null;
  isDecoyMode = false;
  if (vaultWatcher) {
    vaultWatcher.close();
    vaultWatcher = null;
  }
  const config = await readConfig();
  if (config) {
    if (config.autoClearHistory) {
      await writeHistory([]);
      import_electron.BrowserWindow.getAllWindows().forEach((w) => w.webContents.send("usb-history-updated"));
    }
    if (config.cleanSystemJunk) {
      try {
        const root = getUsbRoot();
        const files = await fs.readdir(root);
        for (const f of files) {
          if (f.startsWith(".k3_tmp_") || f === ".k3_readonly_test") {
            await fs.unlink(path.join(root, f)).catch(() => {
            });
          }
        }
      } catch (e) {
      }
    }
  }
  return true;
});
import_electron.ipcMain.handle("get-dashboard-stats", async () => {
  if (!currentKey) return null;
  const vPath = getVaultPath();
  const files = await fs.readdir(vPath);
  let totalSize = 0;
  let encryptedCount = 0;
  for (const f of files) {
    if (f.endsWith(".k3enc")) {
      const stat2 = await fs.stat(path.join(vPath, f));
      totalSize += stat2.size;
      encryptedCount++;
    }
  }
  const config = await readConfig() || {};
  let score = 30;
  if (config.decoyHash) score += 30;
  if (config.autoClearHistory) score += 20;
  if (config.cleanSystemJunk !== false) score += 10;
  if (!config.showHidden) score += 10;
  if (score > 100) score = 100;
  let usbTotalSize = 0;
  let usbUsedSize = 0;
  try {
    const root = getUsbRoot();
    const drives = await import_systeminformation.default.fsSize();
    let bestDrive = null;
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
  } catch (e) {
  }
  return {
    totalSize,
    encryptedCount,
    isDecoyMode,
    securityScore: isDecoyMode ? 35 : score,
    usbTotalSize,
    usbUsedSize
  };
});
import_electron.ipcMain.handle("list-files", async () => {
  if (!currentKey) throw new Error("Not authenticated");
  const vPath = getVaultPath();
  const files = await fs.readdir(vPath);
  const result = [];
  for (const f of files) {
    if (!f.endsWith(".k3enc")) continue;
    const stat2 = await fs.stat(path.join(vPath, f));
    let originalName = f;
    try {
      const fh = await fs.open(path.join(vPath, f), "r");
      const header = Buffer.alloc(37);
      await fh.read(header, 0, 37, 0);
      const possibleFlag = header.readUInt8(32);
      let nameLen = 0;
      let dataOffset = 36;
      if (possibleFlag === 0 || possibleFlag === 1) {
        const possibleNameLen = header.readUInt32LE(33);
        if (possibleNameLen > 0 && possibleNameLen < 5e3) {
          nameLen = possibleNameLen;
          dataOffset = 37;
        } else {
          nameLen = header.readUInt32LE(32);
        }
      } else {
        nameLen = header.readUInt32LE(32);
      }
      if (nameLen > 0 && nameLen < 5e3) {
        const nameBuf = Buffer.alloc(nameLen);
        await fh.read(nameBuf, 0, nameLen, dataOffset);
        originalName = nameBuf.toString("utf8");
      }
      await fh.close();
    } catch (e) {
    }
    result.push({
      name: f,
      originalName,
      isDirectory: originalName.endsWith(".k3dir"),
      size: stat2.size,
      isEncrypted: true
    });
  }
  return result;
});
import_electron.ipcMain.handle("scan-file", async (_, filePath) => {
  try {
    const stat2 = await fs.stat(filePath);
    if (stat2.isDirectory()) return { safe: true, info: "Th\u01B0 m\u1EE5c" };
    const fileName = path.basename(filePath);
    const offlineThreat = await scanSingleFileOffline(filePath, fileName);
    if (offlineThreat) {
      return { safe: false, threat: `Heuristics: ${offlineThreat}` };
    }
    const yaraThreat = await scanYaraOffline(filePath);
    if (yaraThreat) {
      return { safe: false, threat: `YARA Engine: Ph\xE1t hi\u1EC7n m\u1EABu ${yaraThreat}` };
    }
    return { safe: true, info: "An to\xE0n (\u0110\xE3 v\u01B0\u1EE3t qua Heuristics & YARA)" };
  } catch (e) {
    return { safe: false, threat: "L\u1ED7i \u0111\u1ECDc t\u1EC7p: " + e.message };
  }
});
import_electron.ipcMain.handle("get-usb-history", async () => await readHistory());
import_electron.ipcMain.handle("clear-usb-history", async () => {
  await writeHistory([]);
  return true;
});
import_electron.ipcMain.handle("delete-usb-history-items", async (_, deviceIds) => {
  let history = await readHistory();
  history = history.filter((h) => !deviceIds.includes(h.deviceId));
  await writeHistory(history);
  return true;
});
async function processEncryption(event, sourcePath, secureDelete, vaultPath, enableCompression, forceRaw = false) {
  const cleanVaultPath = vaultPath.replace(/^\/+|\/+$/g, "");
  const baseName = path.basename(sourcePath);
  const virtualBasePath = cleanVaultPath ? `${cleanVaultPath}/${baseName}` : baseName;
  const stat2 = await fs.stat(sourcePath);
  if (stat2.isDirectory()) {
    const iv = crypto.randomBytes(16);
    const cipher = crypto.createCipheriv(ALGORITHM, currentKey, iv);
    const encrypted = Buffer.concat([cipher.update(Buffer.from("")), cipher.final()]);
    const authTag = cipher.getAuthTag();
    const nameBuffer = Buffer.from(`${virtualBasePath}/.k3empty`, "utf8");
    const nameLen = Buffer.alloc(4);
    nameLen.writeUInt32LE(nameBuffer.length, 0);
    const isCompressed = Buffer.alloc(1);
    isCompressed.writeUInt8(0, 0);
    const finalData = Buffer.concat([iv, authTag, isCompressed, nameLen, nameBuffer, encrypted]);
    await fs.writeFile(path.join(getVaultPath(), `${crypto.randomBytes(8).toString("hex")}.k3enc`), finalData);
    let blockedCount = 0;
    const items = await fs.readdir(sourcePath);
    for (const item of items) {
      blockedCount += await processEncryption(event, path.join(sourcePath, item), false, virtualBasePath, enableCompression, forceRaw);
    }
    if (secureDelete) await shredFolder(sourcePath);
    return blockedCount;
  } else {
    const offlineThreat = await scanSingleFileOffline(sourcePath, baseName);
    if (offlineThreat) {
      event.sender.send("encryption-progress", { file: virtualBasePath, size: stat2.size, status: `\u274C \u0110\xE3 ch\u1EB7n (M\xE3 \u0111\u1ED9c: ${offlineThreat})` });
      return 1;
    }
    const yaraThreat = await scanYaraOffline(sourcePath);
    if (yaraThreat) {
      event.sender.send("encryption-progress", { file: virtualBasePath, size: stat2.size, status: `\u274C \u0110\xE3 ch\u1EB7n (YARA: ${yaraThreat})` });
      return 1;
    }
    event.sender.send("encryption-progress", { file: virtualBasePath, size: stat2.size, status: forceRaw ? "copying raw" : "encrypting" });
    const nameBuffer = Buffer.from(virtualBasePath, "utf8");
    const nameLen = Buffer.alloc(4);
    nameLen.writeUInt32LE(nameBuffer.length, 0);
    const destFile = path.join(getVaultPath(), `${crypto.randomBytes(8).toString("hex")}.k3enc`);
    if (forceRaw) {
      const iv = crypto.randomBytes(16);
      const authTag = crypto.randomBytes(16);
      const isCompressed = Buffer.alloc(1);
      isCompressed.writeUInt8(2, 0);
      const headerData = Buffer.concat([iv, authTag, isCompressed, nameLen, nameBuffer]);
      await new Promise((resolve2, reject) => {
        const fsSync = require("fs");
        const readStream = fsSync.createReadStream(sourcePath);
        const writeStream = fsSync.createWriteStream(destFile);
        writeStream.write(headerData);
        readStream.pipe(writeStream);
        readStream.on("error", reject);
        writeStream.on("error", reject);
        writeStream.on("finish", resolve2);
      });
      if (secureDelete) await shredFile(sourcePath);
    } else {
      const fileData = await fs.readFile(sourcePath);
      let dataToEncrypt = fileData;
      let compressedFlag = 0;
      if (enableCompression) {
        const ext = path.extname(sourcePath).toLowerCase();
        const noCompressExts = [".zip", ".rar", ".7z", ".mp4", ".mkv", ".jpg", ".jpeg", ".png", ".mp3"];
        if (!noCompressExts.includes(ext) && fileData.length > 1024) {
          dataToEncrypt = zlib.gzipSync(fileData);
          compressedFlag = 1;
        }
      }
      const iv = crypto.randomBytes(16);
      const cipher = crypto.createCipheriv(ALGORITHM, currentKey, iv);
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
import_electron.ipcMain.handle("encrypt-file", async (event, sourcePath, secureDelete = true, vaultPath = "/", enableCompression = true, forceRaw = false) => {
  if (!currentKey) throw new Error("Not authenticated");
  try {
    const blockedCount = await processEncryption(event, sourcePath, secureDelete, vaultPath, enableCompression, forceRaw);
    event.sender.send("encryption-progress", { file: "Done", status: "done" });
    return { success: true, blocked: blockedCount };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.ipcMain.handle("read-vault-text", async (event, fileName) => {
  if (!currentKey) throw new Error("Not authenticated");
  try {
    const data = await fs.readFile(path.join(getVaultPath(), fileName));
    const iv = data.subarray(0, 16);
    const authTag = data.subarray(16, 32);
    let isCompressed = 0;
    let dataOffset = 36;
    const possibleFlag = data.readUInt8(32);
    if (possibleFlag === 0 || possibleFlag === 1 || possibleFlag === 2) {
      const possibleNameLen = data.readUInt32LE(33);
      if (possibleNameLen > 0 && possibleNameLen < 5e3 && 37 + possibleNameLen <= data.length) {
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
      try {
        decrypted = zlib.gunzipSync(decrypted);
      } catch (e) {
      }
    }
    return { success: true, text: decrypted.toString("utf8") };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.ipcMain.handle("write-vault-text", async (event, fileName, content, originalNameFullPath, enableCompression = true) => {
  if (!currentKey) throw new Error("Not authenticated");
  try {
    const fileData = Buffer.from(content, "utf8");
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
    const nameBuffer = Buffer.from(originalNameFullPath, "utf8");
    const nameLen = Buffer.alloc(4);
    nameLen.writeUInt32LE(nameBuffer.length, 0);
    const isCompressedBuf = Buffer.alloc(1);
    isCompressedBuf.writeUInt8(compressedFlag, 0);
    const finalData = Buffer.concat([iv, authTag, isCompressedBuf, nameLen, nameBuffer, encrypted]);
    const targetHexName = fileName || `${crypto.randomBytes(8).toString("hex")}.k3enc`;
    await fs.writeFile(path.join(getVaultPath(), targetHexName), finalData);
    return { success: true };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.ipcMain.handle("decrypt-file", async (event, fileName, targetDir, relativeStructure) => {
  if (!currentKey) throw new Error("Not authenticated");
  try {
    const filePath = path.join(getVaultPath(), fileName);
    const fh = await fs.open(filePath, "r");
    const headerBuf = Buffer.alloc(4096);
    const { bytesRead } = await fh.read(headerBuf, 0, 4096, 0);
    await fh.close();
    if (bytesRead < 36) throw new Error("Invalid file format");
    const iv = headerBuf.subarray(0, 16);
    const authTag = headerBuf.subarray(16, 32);
    let isCompressed = 0;
    let dataOffset = 36;
    const possibleFlag = headerBuf.readUInt8(32);
    let nameLen = 0;
    if (possibleFlag === 0 || possibleFlag === 1 || possibleFlag === 2) {
      const possibleNameLen = headerBuf.readUInt32LE(33);
      if (possibleNameLen > 0 && possibleNameLen < 5e3) {
        isCompressed = possibleFlag;
        dataOffset = 37;
        nameLen = possibleNameLen;
      } else {
        nameLen = headerBuf.readUInt32LE(32);
      }
    } else {
      nameLen = headerBuf.readUInt32LE(32);
    }
    const originalNameFull = headerBuf.subarray(dataOffset, dataOffset + nameLen).toString("utf8");
    const originalName = originalNameFull.split("/").pop() || originalNameFull;
    event.sender.send("encryption-progress", { file: originalNameFull, status: isCompressed === 2 ? "copying raw" : "decrypting" });
    const outputName = relativeStructure ? relativeStructure : originalName;
    if (originalNameFull.endsWith(".k3empty")) {
      const folderName = relativeStructure ? relativeStructure.replace(/\.k3empty$/, "") : originalNameFull.split("/").slice(-2, -1)[0] || "New Folder";
      const outPath2 = targetDir ? path.join(targetDir, folderName) : path.join(path.dirname(getVaultPath()), "Decrypted", folderName);
      await fs.mkdir(outPath2, { recursive: true });
      return { success: true };
    }
    let outPath = targetDir ? path.join(targetDir, outputName) : path.join(path.dirname(getVaultPath()), "Decrypted", outputName);
    await fs.mkdir(path.dirname(outPath), { recursive: true });
    const payloadOffset = dataOffset + nameLen;
    if (isCompressed === 2) {
      await new Promise((resolve2, reject) => {
        const fsSync = require("fs");
        const readStream = fsSync.createReadStream(filePath, { start: payloadOffset });
        const writeStream = fsSync.createWriteStream(outPath);
        readStream.pipe(writeStream);
        readStream.on("error", reject);
        writeStream.on("error", reject);
        writeStream.on("finish", resolve2);
      });
      return { success: true };
    }
    const data = await fs.readFile(filePath);
    const encryptedData = data.subarray(payloadOffset);
    const decipher = crypto.createDecipheriv(ALGORITHM, currentKey, iv);
    decipher.setAuthTag(authTag);
    let decrypted = Buffer.concat([decipher.update(encryptedData), decipher.final()]);
    if (isCompressed === 1) {
      try {
        decrypted = zlib.gunzipSync(decrypted);
      } catch (e) {
        console.error("Gunzip failed", e);
      }
    }
    if (outputName.endsWith(".k3dir")) {
      outPath = outPath.replace(".k3dir", "");
      await fs.mkdir(outPath, { recursive: true });
      const tempZip = path.join(os.tmpdir(), `k3_${Date.now()}.zip`);
      await fs.writeFile(tempZip, decrypted);
      const zip = new import_adm_zip.default(tempZip);
      await new Promise((resolve2, reject) => zip.extractAllToAsync(outPath, true, false, (err) => err ? reject(err) : resolve2()));
      await fs.unlink(tempZip);
    } else {
      await fs.writeFile(outPath, decrypted);
    }
    return { success: true };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.ipcMain.handle("export-zip", async (event, files, passwordToSet, savePath) => {
  if (!currentKey) throw new Error("Not authenticated");
  try {
    return new Promise((resolve2) => {
      const output = (0, import_fs.createWriteStream)(savePath);
      const archive = archiver("zip-encrypted", {
        zlib: { level: 8 },
        encryptionMethod: "aes256",
        password: passwordToSet
      });
      output.on("close", () => resolve2({ success: true }));
      archive.on("error", (err) => resolve2({ success: false, error: err.message }));
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
            if (possibleNameLen > 0 && possibleNameLen < 5e3 && 37 + possibleNameLen <= data.length) {
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
          const originalNameFull = data.subarray(dataOffset, dataOffset + nameLen).toString("utf8");
          const originalName = originalNameFull.split("/").pop() || originalNameFull;
          const encryptedData = data.subarray(dataOffset + nameLen);
          const decipher = crypto.createDecipheriv(ALGORITHM, currentKey, iv);
          decipher.setAuthTag(authTag);
          let decrypted = Buffer.concat([decipher.update(encryptedData), decipher.final()]);
          if (isCompressed === 1) {
            try {
              decrypted = zlib.gunzipSync(decrypted);
            } catch (e) {
            }
          }
          const archiveName = fileObj.relativeStructure || originalName;
          if (originalNameFull.endsWith(".k3empty")) {
            archive.append("", { name: archiveName.replace(".k3empty", "/") });
          } else if (archiveName.endsWith(".k3dir")) {
            archive.append(decrypted, { name: archiveName.replace(".k3dir", ".zip") });
          } else {
            archive.append(decrypted, { name: archiveName });
          }
        }
        archive.finalize();
      })().catch((e) => {
        resolve2({ success: false, error: e.message });
      });
    });
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.ipcMain.handle("delete-file", async (_, fileName) => {
  if (!currentKey) throw new Error("Not authenticated");
  const filePath = path.join(getVaultPath(), fileName);
  await fs.rm(filePath);
  return true;
});
import_electron.ipcMain.handle("create-vault-folder", async (_, folderName, vaultPath = "/") => {
  if (!currentKey) throw new Error("Not authenticated");
  try {
    const cleanVaultPath = vaultPath.replace(/^\/+|\/+$/g, "");
    const virtualPath = cleanVaultPath ? `${cleanVaultPath}/${folderName}/.k3empty` : `${folderName}/.k3empty`;
    const iv = crypto.randomBytes(16);
    const cipher = crypto.createCipheriv(ALGORITHM, currentKey, iv);
    const encrypted = Buffer.concat([cipher.update(Buffer.from("")), cipher.final()]);
    const authTag = cipher.getAuthTag();
    const nameBuffer = Buffer.from(virtualPath, "utf8");
    const nameLen = Buffer.alloc(4);
    nameLen.writeUInt32LE(nameBuffer.length, 0);
    const isCompressed = Buffer.alloc(1);
    isCompressed.writeUInt8(0, 0);
    const finalData = Buffer.concat([iv, authTag, isCompressed, nameLen, nameBuffer, encrypted]);
    await fs.writeFile(path.join(getVaultPath(), `${crypto.randomBytes(8).toString("hex")}.k3enc`), finalData);
    return { success: true, vaultDir: getVaultPath() };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.ipcMain.handle("enable-hwid-lock", async () => {
  const config = await readConfig();
  if (!config) return { success: false, error: "Not setup" };
  try {
    const devices = await import_systeminformation.default.blockDevices();
    let currentHwid = "";
    let maxLen = -1;
    for (const dev of devices) {
      if (dev.mount && USB_DIR.startsWith(dev.mount)) {
        if (dev.mount.length > maxLen) {
          maxLen = dev.mount.length;
          currentHwid = (dev.serial || "") + (dev.uuid || "");
        }
      }
    }
    if (!currentHwid) return { success: false, error: "Kh\xF4ng th\u1EC3 \u0111\u1ECDc \u0111\u01B0\u1EE3c ID ph\u1EA7n c\u1EE9ng c\u1EE7a USB n\xE0y" };
    config.hwidEnabled = true;
    config.expectedHwid = currentHwid;
    await writeConfig(config);
    return { success: true };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.ipcMain.handle("check-readonly", async () => {
  try {
    const testFile = path.join(getUsbRoot(), ".k3_readonly_test");
    await fs.writeFile(testFile, "test");
    await fs.unlink(testFile);
    return false;
  } catch (e) {
    if (e.code === "EPERM" || e.code === "EROFS" || e.message.toLowerCase().includes("read-only") || e.message.toLowerCase().includes("write")) {
      return true;
    }
    return false;
  }
});
import_electron.ipcMain.handle("toggle-readonly", async (_, enable) => {
  if (process.platform !== "win32") {
    return { success: false, error: "Ch\u1EC9 h\u1ED7 tr\u1EE3 tr\xEAn h\u1EC7 \u0111i\u1EC1u h\xE0nh Windows." };
  }
  try {
    const driveLetter = getUsbRoot().substring(0, 1);
    const scriptContent = `select volume ${driveLetter}
attributes volume ${enable ? "set" : "clear"} readonly
`;
    const scriptPath = path.join(os.tmpdir(), `k3_dp_${Date.now()}.txt`);
    await fs.writeFile(scriptPath, scriptContent);
    return new Promise((resolve2) => {
      const cmd = `powershell -Command "Start-Process diskpart -ArgumentList '/s ${scriptPath}' -Verb RunAs -WindowStyle Hidden"`;
      (0, import_child_process.exec)(cmd, () => {
        setTimeout(async () => {
          try {
            await fs.unlink(scriptPath);
          } catch {
          }
        }, 5e3);
        resolve2({ success: true });
      });
    });
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.ipcMain.handle("select-files", async () => {
  const { canceled, filePaths } = await import_electron.dialog.showOpenDialog({ properties: ["openFile", "multiSelections"] });
  if (canceled) return [];
  return filePaths;
});
import_electron.ipcMain.handle("get-drives", async () => {
  if (process.platform === "win32") {
    return [
      { name: "Win (C:)", path: "C:\\" },
      { name: "DATA (D:)", path: "D:\\" },
      { name: "Desktop", path: path.join(os.homedir(), "Desktop") }
    ];
  } else {
    return [
      { name: "Root (/)", path: "/" },
      { name: "Home", path: os.homedir() },
      { name: "Desktop", path: path.join(os.homedir(), "Desktop") }
    ];
  }
});
import_electron.ipcMain.handle("list-local-dir", async (_, dirPath) => {
  try {
    const config = await readConfig() || {};
    const cleanSystem = config.cleanSystemJunk !== false;
    const showHidden = config.showHidden === true;
    const files = await fs.readdir(dirPath, { withFileTypes: true });
    const result = [];
    for (const f of files) {
      if (!showHidden && f.name.startsWith(".")) continue;
      if (cleanSystem) {
        const lowerName = f.name.toLowerCase();
        if (f.name.startsWith("._") || f.name === ".DS_Store" || lowerName === "thumbs.db" || lowerName === "desktop.ini") {
          fs.rm(path.join(dirPath, f.name), { force: true, recursive: true }).catch(() => {
          });
          continue;
        }
        if (lowerName === "$recycle.bin" || lowerName === "system volume information") continue;
      }
      let size = 0;
      if (!f.isDirectory()) {
        try {
          const stat2 = await fs.stat(path.join(dirPath, f.name));
          size = stat2.size;
        } catch (e) {
        }
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
import_electron.ipcMain.handle("copy-local-file", async (_, source, target) => {
  try {
    const stat2 = await fs.stat(source);
    if (stat2.isDirectory()) await fs.cp(source, target, { recursive: true });
    else await fs.copyFile(source, target);
    return { success: true };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.ipcMain.handle("move-local-file", async (_, source, target) => {
  try {
    await fs.rename(source, target);
    return { success: true };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.ipcMain.handle("delete-local-file", async (_, filePath, secure = false) => {
  try {
    if (secure) {
      const stat2 = await fs.stat(filePath);
      if (stat2.isDirectory()) await shredFolder(filePath);
      else await shredFile(filePath);
    } else await fs.rm(filePath, { recursive: true, force: true });
    return { success: true };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.ipcMain.handle("rename-local", async (_, oldPath, newPath) => {
  try {
    await fs.rename(oldPath, newPath);
    return { success: true };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.ipcMain.handle("create-local-folder", async (_, dirPath) => {
  try {
    await fs.mkdir(dirPath, { recursive: true });
    return { success: true };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
var isScanning = false;
async function scanSingleFileOffline(fullPath, fileName) {
  let threat = "";
  const lowerName = fileName.toLowerCase();
  if (lowerName.endsWith(".bat") || lowerName.endsWith(".vbs") || lowerName.endsWith(".ps1")) {
    threat = "Heuristics.Script.Suspicious";
  } else if (lowerName.match(/\.[a-z0-9]+\.(exe|vbs|bat|cmd|pif|scr)$/)) {
    threat = "Heuristics.DoubleExtension.Trojan";
  }
  if (!threat && (lowerName.includes("eicar") || lowerName.includes("virus"))) {
    threat = "EICAR-Test-Signature";
  }
  if (!threat && lowerName.endsWith(".txt")) {
    try {
      const stat2 = await fs.stat(fullPath);
      if (stat2.size < 1e3) {
        const data = await fs.readFile(fullPath, "utf8");
        if (data.includes("X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR")) {
          threat = "EICAR-Test-Signature";
        }
      }
    } catch {
    }
  }
  return threat;
}
var scanDirectory = async (dirPath, win, config) => {
  if (!isScanning) return;
  try {
    const files = await fs.readdir(dirPath, { withFileTypes: true });
    for (const f of files) {
      if (!isScanning) break;
      const fullPath = path.join(dirPath, f.name);
      if (f.isDirectory()) {
        await scanDirectory(fullPath, win, config);
      } else {
        win.webContents.send("scanner-progress", { path: fullPath, name: f.name });
        let threat = await scanSingleFileOffline(fullPath, f.name);
        if (!threat) {
          const yaraThreat = await scanYaraOffline(fullPath);
          if (yaraThreat) threat = `YARA: ${yaraThreat}`;
        }
        if (threat) {
          win.webContents.send("scanner-threat-found", { path: fullPath, name: f.name, threat });
        }
        await new Promise((r) => setTimeout(r, 10));
      }
    }
  } catch (e) {
  }
};
import_electron.ipcMain.handle("scanner-start", async (event, targetPaths) => {
  if (isScanning) return { success: false, error: "\u0110ang c\xF3 ti\u1EBFn tr\xECnh qu\xE9t" };
  isScanning = true;
  const config = await readConfig();
  const win = import_electron.BrowserWindow.fromWebContents(event.sender);
  (async () => {
    try {
      for (const p of targetPaths) {
        if (!isScanning) break;
        const stat2 = await fs.stat(p).catch(() => null);
        if (!stat2) continue;
        if (stat2.isDirectory()) {
          await scanDirectory(p, win, config);
        } else {
          win.webContents.send("scanner-progress", { path: p, name: path.basename(p) });
          const fName = path.basename(p);
          let threat = await scanSingleFileOffline(p, fName);
          if (!threat) {
            const yaraThreat = await scanYaraOffline(p);
            if (yaraThreat) threat = `YARA: ${yaraThreat}`;
          }
          if (threat) {
            win.webContents.send("scanner-threat-found", { path: p, name: fName, threat });
          }
          await new Promise((r) => setTimeout(r, 50));
        }
      }
    } catch (e) {
    }
    isScanning = false;
    win == null ? void 0 : win.webContents.send("scanner-done");
  })();
  return { success: true };
});
import_electron.ipcMain.handle("scanner-stop", () => {
  isScanning = false;
  return true;
});
import_electron.ipcMain.handle("show-open-dialog", async (event, options) => {
  const win = import_electron.BrowserWindow.fromWebContents(event.sender);
  if (!win) return [];
  const { canceled, filePaths } = await import_electron.dialog.showOpenDialog(win, options);
  if (!canceled) {
    return filePaths;
  }
  return [];
});
import_electron.ipcMain.handle("show-save-dialog", async (event, options) => {
  const win = import_electron.BrowserWindow.fromWebContents(event.sender);
  if (!win) return null;
  const { canceled, filePath } = await import_electron.dialog.showSaveDialog(win, options);
  if (!canceled) {
    return filePath;
  }
  return null;
});
import_electron.ipcMain.handle("quarantine-action", async (_, action, id) => {
  try {
    const qPath = path.join(import_electron.app.getPath("userData"), "quarantine");
    const targetFile = path.join(qPath, id);
    let logs = [];
    try {
      logs = JSON.parse(await fs.readFile(getLogsPath(), "utf8"));
    } catch (e) {
    }
    const originalName = id.split("_").slice(1).join("_").replace(".k3q", "");
    if (action === "delete") {
      await fs.unlink(targetFile);
      logs.unshift({ id: Date.now() + Math.random().toString(), time: (/* @__PURE__ */ new Date()).toLocaleString("vi-VN"), event: "X\xF3a v\u0129nh vi\u1EC5n t\u1EC7p c\xE1ch ly", details: originalName });
    } else if (action === "restore") {
      const desktopPath = import_electron.app.getPath("desktop");
      const dest = path.join(desktopPath, originalName);
      await fs.rename(targetFile, dest);
      logs.unshift({ id: Date.now() + Math.random().toString(), time: (/* @__PURE__ */ new Date()).toLocaleString("vi-VN"), event: "Kh\xF4i ph\u1EE5c t\u1EC7p ra Desktop", details: originalName });
    }
    await fs.writeFile(getLogsPath(), JSON.stringify(logs.slice(0, 100), null, 2));
    return { success: true };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.ipcMain.handle("get-quarantine-list", async () => {
  try {
    const qPath = path.join(import_electron.app.getPath("userData"), "quarantine");
    await fs.mkdir(qPath, { recursive: true }).catch(() => {
    });
    const files = await fs.readdir(qPath);
    const list = [];
    for (const f of files) {
      if (f.endsWith(".k3q")) {
        const parts = f.split("_");
        const ts = parseInt(parts[0]);
        const originalName = parts.slice(1).join("_").replace(".k3q", "");
        list.push({
          id: f,
          name: originalName,
          originalPath: "C\xE1ch ly an to\xE0n (.k3q)",
          date: new Date(ts).toLocaleString("vi-VN")
        });
      }
    }
    return list.sort((a, b) => b.id.localeCompare(a.id));
  } catch (e) {
    return [];
  }
});
var getLogsPath = () => path.join(import_electron.app.getPath("userData"), "scanner-logs.json");
import_electron.ipcMain.handle("get-scanner-logs", async () => {
  try {
    const data = await fs.readFile(getLogsPath(), "utf8");
    return JSON.parse(data);
  } catch (e) {
    return [];
  }
});
import_electron.ipcMain.handle("scanner-action", async (_, action, filePaths) => {
  try {
    let logs = [];
    try {
      logs = JSON.parse(await fs.readFile(getLogsPath(), "utf8"));
    } catch (e) {
    }
    for (const p of filePaths) {
      if (action === "delete") {
        await fs.unlink(p).catch(() => {
        });
        logs.unshift({ id: Date.now() + Math.random().toString(), time: (/* @__PURE__ */ new Date()).toLocaleString("vi-VN"), event: "X\xF3a t\u1EC7p nhi\u1EC5m", details: path.basename(p) });
      } else if (action === "quarantine") {
        const qPath = path.join(import_electron.app.getPath("userData"), "quarantine");
        await fs.mkdir(qPath, { recursive: true }).catch(() => {
        });
        const dest = path.join(qPath, `${Date.now()}_${path.basename(p)}.k3q`);
        await fs.rename(p, dest).catch(() => {
        });
        logs.unshift({ id: Date.now() + Math.random().toString(), time: (/* @__PURE__ */ new Date()).toLocaleString("vi-VN"), event: "C\xE1ch ly t\u1EC7p", details: path.basename(p) });
      }
    }
    await fs.writeFile(getLogsPath(), JSON.stringify(logs.slice(0, 100), null, 2));
    return { success: true };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.app.whenReady().then(async () => {
  await initVault();
  await initYaraScanner();
  autoUpdateDatabase();
  const config = await readConfig();
  if (config && config.autoClearHistory) await writeHistory([]);
  createWindow();
  startUSBPoll();
});
import_electron.app.on("window-all-closed", async () => {
  const config = await readConfig();
  if (config && config.autoClearHistory) await writeHistory([]);
  if (process.platform !== "darwin") import_electron.app.quit();
});
import_electron.ipcMain.handle("check-vault-integrity", async (event) => {
  if (!currentKey) return { success: false, error: "Not authenticated" };
  try {
    const vaultDir = getVaultPath();
    const files = await fs.readdir(vaultDir);
    let corruptedFiles = [];
    let totalFiles = 0;
    for (const f of files) {
      if (!f.endsWith(".k3enc")) continue;
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
          if (possibleNameLen > 0 && possibleNameLen < 5e3 && 37 + possibleNameLen <= data.length) {
            nameLen = possibleNameLen;
            dataOffset = 37;
          } else {
            nameLen = data.readUInt32LE(32);
          }
        } else {
          nameLen = data.readUInt32LE(32);
        }
        const originalNameFull = data.subarray(dataOffset, dataOffset + nameLen).toString("utf8");
        const encryptedData = data.subarray(dataOffset + nameLen);
        const decipher = crypto.createDecipheriv(ALGORITHM, currentKey, iv);
        decipher.setAuthTag(authTag);
        decipher.update(encryptedData);
        decipher.final();
      } catch (err) {
        corruptedFiles.push(f);
      }
    }
    return { success: true, corruptedFiles, totalFiles };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
import_electron.ipcMain.handle("search-everything", async (event, query) => {
  if (!query || query.trim() === "") return { success: true, results: [] };
  return new Promise((resolve2) => {
    try {
      if (process.platform === "win32") {
        const esPath = isDev ? path.join(__dirname, "../../electron/bin/es.exe") : path.join(process.resourcesPath, "bin/es.exe");
        (0, import_child_process.exec)(`"${esPath}" -csv -n 50 "${query}"`, { timeout: 5e3 }, (err, stdout) => {
          if (err && !stdout) return resolve2({ success: false, error: err.message });
          const lines = stdout.split("\n").filter((l) => l.trim() !== "");
          if (lines.length > 0) lines.shift();
          const results = lines.map((l) => {
            const parts = l.split(",");
            if (parts.length >= 2) {
              const name = parts[0].replace(/^"|"$/g, "");
              const p = parts[1].replace(/^"|"$/g, "");
              return { name, path: path.join(p, name), isDirectory: false };
            }
            return null;
          }).filter(Boolean);
          resolve2({ success: true, results });
        });
      } else if (process.platform === "darwin") {
        (0, import_child_process.exec)(`mdfind -name "${query}" | head -n 50`, { timeout: 5e3 }, (err, stdout) => {
          if (err && !stdout) return resolve2({ success: false, error: err.message });
          const lines = stdout.split("\n").filter((l) => l.trim() !== "");
          const results = lines.map((p) => {
            return { name: path.basename(p), path: p, isDirectory: false };
          });
          resolve2({ success: true, results });
        });
      } else {
        resolve2({ success: false, error: "OS not supported for global search" });
      }
    } catch (e) {
      resolve2({ success: false, error: e.message });
    }
  });
});
import_electron.ipcMain.handle("format-vault", async () => {
  try {
    const vaultDir = getVaultPath();
    await fs.rm(vaultDir, { recursive: true, force: true });
    await initVault();
    return { success: true };
  } catch (e) {
    return { success: false, error: e.message };
  }
});
