import { contextBridge, ipcRenderer, webUtils } from 'electron';

contextBridge.exposeInMainWorld('electronAPI', {
    checkSetup: () => ipcRenderer.invoke('check-setup'),
    getConfig: () => ipcRenderer.invoke('get-config'),
    updateConfig: (newSettings: any) => ipcRenderer.invoke('update-config', newSettings),
    setupPassword: (password: string) => ipcRenderer.invoke('setup-password', password),
    changePassword: (oldP: string, newP: string) => ipcRenderer.invoke('change-password', oldP, newP),
    setupDecoy: (password: string) => ipcRenderer.invoke('setup-decoy', password),
    getDecoyInfo: () => ipcRenderer.invoke('get-decoy-info'),
    wipeDecoy: () => ipcRenderer.invoke('wipe-decoy'),
    login: (password: string) => ipcRenderer.invoke('login', password),
    logout: () => ipcRenderer.invoke('logout'),
    getDashboardStats: () => ipcRenderer.invoke('get-dashboard-stats'),
    listFiles: () => ipcRenderer.invoke('list-files'),
    scanFile: (filePath: string) => ipcRenderer.invoke('scan-file', filePath),
    encryptFile: (sourcePath: string, secureDelete: boolean, vaultPath: string, enableCompression: boolean = true, forceRaw: boolean = false) => ipcRenderer.invoke('encrypt-file', sourcePath, secureDelete, vaultPath, enableCompression, forceRaw),
    decryptFile: (fileName: string, targetDir?: string, relativeStructure?: string) => ipcRenderer.invoke('decrypt-file', fileName, targetDir, relativeStructure),
    readVaultText: (fileName: string) => ipcRenderer.invoke('read-vault-text', fileName),
    writeVaultText: (fileName: string | null, content: string, originalNameFullPath: string, enableCompression: boolean = true) => ipcRenderer.invoke('write-vault-text', fileName, content, originalNameFullPath, enableCompression),
    deleteFile: (fileName: string) => ipcRenderer.invoke('delete-file', fileName),
    selectFiles: () => ipcRenderer.invoke('select-files'),
    getDrives: () => ipcRenderer.invoke('get-drives'),
    listLocalDir: (dirPath: string) => ipcRenderer.invoke('list-local-dir', dirPath),
    getPathForFile: (file: File) => webUtils.getPathForFile(file),
    copyLocalFile: (source: string, target: string) => ipcRenderer.invoke('copy-local-file', source, target),
    moveLocalFile: (source: string, target: string) => ipcRenderer.invoke('move-local-file', source, target),
    deleteLocalFile: (filePath: string, secure: boolean = false) => ipcRenderer.invoke('delete-local-file', filePath, secure),
    renameLocal: (oldPath: string, newPath: string) => ipcRenderer.invoke('rename-local', oldPath, newPath),
    createLocalFolder: (dirPath: string) => ipcRenderer.invoke('create-local-folder', dirPath),
    getUsbHistory: () => ipcRenderer.invoke('get-usb-history'),
    clearUsbHistory: () => ipcRenderer.invoke('clear-usb-history'),
    deleteUsbHistoryItems: (deviceIds: string[]) => ipcRenderer.invoke('delete-usb-history-items', deviceIds),
    enableHwidLock: () => ipcRenderer.invoke('enable-hwid-lock'),
    createVaultFolder: (folderName: string, vaultPath: string) => ipcRenderer.invoke('create-vault-folder', folderName, vaultPath),
    onUsbHistoryUpdated: (callback: () => void) => {
        ipcRenderer.removeAllListeners('usb-history-updated');
        ipcRenderer.on('usb-history-updated', callback);
    },
    showOpenDialog: (options: any) => ipcRenderer.invoke('show-open-dialog', options),
    scannerStart: (paths: string[]) => ipcRenderer.invoke('scanner-start', paths),
    scannerStop: () => ipcRenderer.invoke('scanner-stop'),
    quarantineAction: (action: string, id: string) => ipcRenderer.invoke('quarantine-action', action, id),
    getQuarantineList: () => ipcRenderer.invoke('get-quarantine-list'),
    getScannerLogs: () => ipcRenderer.invoke('get-scanner-logs'),
    scannerAction: (action: string, paths: string[]) => ipcRenderer.invoke('scanner-action', action, paths),
    onScannerProgress: (callback: (data: any) => void) => {
        ipcRenderer.on('scanner-progress', (_, data) => callback(data));
    },
    onScannerThreatFound: (callback: (data: any) => void) => {
        ipcRenderer.on('scanner-threat-found', (_, data) => callback(data));
    },
    onScannerDone: (callback: () => void) => {
        ipcRenderer.on('scanner-done', () => callback());
    },
    onEncryptionProgress: (callback: (data: any) => void) => {
        ipcRenderer.removeAllListeners('encryption-progress');
        ipcRenderer.on('encryption-progress', (_, data) => callback(data));
    },
    onAutoEncryptDone: (callback: () => void) => {
        ipcRenderer.removeAllListeners('auto-encrypt-done');
        ipcRenderer.on('auto-encrypt-done', () => callback());
    },
    exportZip: (files: any[], passwordToSet: string, savePath: string) => ipcRenderer.invoke('export-zip', files, passwordToSet, savePath),
    showSaveDialog: (options: any) => ipcRenderer.invoke('show-save-dialog', options),
    checkReadOnly: () => ipcRenderer.invoke('check-readonly'),
    toggleReadOnly: (enable: boolean) => ipcRenderer.invoke('toggle-readonly', enable),
    checkVaultIntegrity: () => ipcRenderer.invoke('check-vault-integrity'),
    searchEverything: (query: string) => ipcRenderer.invoke('search-everything', query),
    formatVault: () => ipcRenderer.invoke('format-vault'),
    copyRawFile: (fileName: string, targetDir: string) => ipcRenderer.invoke('copy-raw-file', fileName, targetDir)
});
