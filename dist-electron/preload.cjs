// electron/preload.ts
var import_electron = require("electron");
import_electron.contextBridge.exposeInMainWorld("electronAPI", {
  checkSetup: () => import_electron.ipcRenderer.invoke("check-setup"),
  getConfig: () => import_electron.ipcRenderer.invoke("get-config"),
  updateConfig: (newSettings) => import_electron.ipcRenderer.invoke("update-config", newSettings),
  setupPassword: (password) => import_electron.ipcRenderer.invoke("setup-password", password),
  changePassword: (oldP, newP) => import_electron.ipcRenderer.invoke("change-password", oldP, newP),
  setupDecoy: (password) => import_electron.ipcRenderer.invoke("setup-decoy", password),
  login: (password) => import_electron.ipcRenderer.invoke("login", password),
  logout: () => import_electron.ipcRenderer.invoke("logout"),
  getDashboardStats: () => import_electron.ipcRenderer.invoke("get-dashboard-stats"),
  listFiles: () => import_electron.ipcRenderer.invoke("list-files"),
  scanFile: (filePath) => import_electron.ipcRenderer.invoke("scan-file", filePath),
  encryptFile: (sourcePath, secureDelete, vaultPath, enableCompression = true, forceRaw = false) => import_electron.ipcRenderer.invoke("encrypt-file", sourcePath, secureDelete, vaultPath, enableCompression, forceRaw),
  decryptFile: (fileName, targetDir, relativeStructure) => import_electron.ipcRenderer.invoke("decrypt-file", fileName, targetDir, relativeStructure),
  readVaultText: (fileName) => import_electron.ipcRenderer.invoke("read-vault-text", fileName),
  writeVaultText: (fileName, content, originalNameFullPath, enableCompression = true) => import_electron.ipcRenderer.invoke("write-vault-text", fileName, content, originalNameFullPath, enableCompression),
  deleteFile: (fileName) => import_electron.ipcRenderer.invoke("delete-file", fileName),
  selectFiles: () => import_electron.ipcRenderer.invoke("select-files"),
  getDrives: () => import_electron.ipcRenderer.invoke("get-drives"),
  listLocalDir: (dirPath) => import_electron.ipcRenderer.invoke("list-local-dir", dirPath),
  getPathForFile: (file) => import_electron.webUtils.getPathForFile(file),
  copyLocalFile: (source, target) => import_electron.ipcRenderer.invoke("copy-local-file", source, target),
  moveLocalFile: (source, target) => import_electron.ipcRenderer.invoke("move-local-file", source, target),
  deleteLocalFile: (filePath, secure = false) => import_electron.ipcRenderer.invoke("delete-local-file", filePath, secure),
  renameLocal: (oldPath, newPath) => import_electron.ipcRenderer.invoke("rename-local", oldPath, newPath),
  createLocalFolder: (dirPath) => import_electron.ipcRenderer.invoke("create-local-folder", dirPath),
  getUsbHistory: () => import_electron.ipcRenderer.invoke("get-usb-history"),
  clearUsbHistory: () => import_electron.ipcRenderer.invoke("clear-usb-history"),
  deleteUsbHistoryItems: (deviceIds) => import_electron.ipcRenderer.invoke("delete-usb-history-items", deviceIds),
  enableHwidLock: () => import_electron.ipcRenderer.invoke("enable-hwid-lock"),
  createVaultFolder: (folderName, vaultPath) => import_electron.ipcRenderer.invoke("create-vault-folder", folderName, vaultPath),
  onUsbHistoryUpdated: (callback) => {
    import_electron.ipcRenderer.removeAllListeners("usb-history-updated");
    import_electron.ipcRenderer.on("usb-history-updated", callback);
  },
  showOpenDialog: (options) => import_electron.ipcRenderer.invoke("show-open-dialog", options),
  scannerStart: (paths) => import_electron.ipcRenderer.invoke("scanner-start", paths),
  scannerStop: () => import_electron.ipcRenderer.invoke("scanner-stop"),
  quarantineAction: (action, id) => import_electron.ipcRenderer.invoke("quarantine-action", action, id),
  getQuarantineList: () => import_electron.ipcRenderer.invoke("get-quarantine-list"),
  getScannerLogs: () => import_electron.ipcRenderer.invoke("get-scanner-logs"),
  scannerAction: (action, paths) => import_electron.ipcRenderer.invoke("scanner-action", action, paths),
  onScannerProgress: (callback) => {
    import_electron.ipcRenderer.on("scanner-progress", (_, data) => callback(data));
  },
  onScannerThreatFound: (callback) => {
    import_electron.ipcRenderer.on("scanner-threat-found", (_, data) => callback(data));
  },
  onScannerDone: (callback) => {
    import_electron.ipcRenderer.on("scanner-done", () => callback());
  },
  onEncryptionProgress: (callback) => {
    import_electron.ipcRenderer.removeAllListeners("encryption-progress");
    import_electron.ipcRenderer.on("encryption-progress", (_, data) => callback(data));
  },
  onAutoEncryptDone: (callback) => {
    import_electron.ipcRenderer.removeAllListeners("auto-encrypt-done");
    import_electron.ipcRenderer.on("auto-encrypt-done", () => callback());
  },
  exportZip: (files, passwordToSet, savePath) => import_electron.ipcRenderer.invoke("export-zip", files, passwordToSet, savePath),
  showSaveDialog: (options) => import_electron.ipcRenderer.invoke("show-save-dialog", options),
  checkReadOnly: () => import_electron.ipcRenderer.invoke("check-readonly"),
  toggleReadOnly: (enable) => import_electron.ipcRenderer.invoke("toggle-readonly", enable),
  checkVaultIntegrity: () => import_electron.ipcRenderer.invoke("check-vault-integrity"),
  searchEverything: (query) => import_electron.ipcRenderer.invoke("search-everything", query),
  formatVault: () => import_electron.ipcRenderer.invoke("format-vault"),
  copyRawFile: (fileName, targetDir) => import_electron.ipcRenderer.invoke("copy-raw-file", fileName, targetDir)
});
