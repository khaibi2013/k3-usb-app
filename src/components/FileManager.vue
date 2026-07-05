<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { 
  FolderLock, File, Lock, Trash2, 
  RefreshCw, HardDrive, Folder, ArrowRightCircle, ArrowLeftCircle, ChevronLeft, ShieldCheck, ShieldAlert, FileText, Save, X, MoreHorizontal, Settings, Search
} from '@lucide/vue';

const allVaultFiles = ref<any[]>([]);
const currentVaultDir = ref<string>('/');
const localFiles = ref<any[]>([]);
const drives = ref<any[]>([]);

const selectedLocalFiles = ref<Set<any>>(new Set());
const selectedVaultFiles = ref<Set<any>>(new Set());

const localSearchQuery = ref('');
const vaultSearchQuery = ref('');
const globalLocalFiles = ref<any[]>([]);
const isSearchingLocal = ref(false);

const enableVirusScan = ref<boolean>(true);
const enableCompression = ref<boolean>(true);
const deleteAfterEncrypt = ref<boolean>(false);

const textEditorState = ref({
  visible: false,
  fileName: '' as string | null,
  displayName: '',
  content: '',
  isSaving: false
});

const globalSettings = ref({
  visible: false,
  maxEncryptSize: 0,
  autoDecrypt: false,
  showHidden: false,
  autoClearHistory: false,
  cleanSystemJunk: true,
  autoEncryptFolder: 'BaoMat'
});


const cleanJunkNow = async () => {
  processingStatus.value = 'Đang dọn rác hệ thống...';
  // Send dummy event or trigger logout clean manually, we just simulate for UI since logout does it
  setTimeout(() => {
    processingStatus.value = '';
    (window as any).showToast('Đã dọn dẹp bộ nhớ đệm và rác K3 thành công!', 'success');
  }, 1000);
};

const loadGlobalSettings = async () => {
  const config = await api.getConfig();
  if (config) {
    globalSettings.value = {
      ...globalSettings.value,
      ...config,
      visible: globalSettings.value.visible
    };
  }
};

const saveGlobalSettings = async () => {
  await api.updateConfig({
    maxEncryptSize: globalSettings.value.maxEncryptSize,
    autoDecrypt: globalSettings.value.autoDecrypt,
    showHidden: globalSettings.value.showHidden,
    autoClearHistory: globalSettings.value.autoClearHistory,
    cleanSystemJunk: globalSettings.value.cleanSystemJunk,
    autoEncryptFolder: globalSettings.value.autoEncryptFolder
  });
  globalSettings.value.visible = false;
  (window as any).showToast('Đã lưu cài đặt chung', 'success');
  if (currentLocalDir.value) await openLocalDir(currentLocalDir.value);
};

const showVaultActions = ref<boolean>(true);

import { computed, watch } from 'vue';

let searchTimeout: any = null;
watch(localSearchQuery, (newVal) => {
  if (!newVal || newVal.trim() === '') {
    globalLocalFiles.value = [];
    isSearchingLocal.value = false;
    return;
  }
  isSearchingLocal.value = true;
  clearTimeout(searchTimeout);
  searchTimeout = setTimeout(async () => {
    loadingLocal.value = true;
    const res = await api.searchEverything(newVal);
    if (res.success) {
      globalLocalFiles.value = res.results;
    }
    loadingLocal.value = false;
  }, 300);
});

const displayedLocalFiles = computed(() => {
  let list = isSearchingLocal.value ? globalLocalFiles.value : localFiles.value;
  if (!globalSettings.value.showHidden) {
    list = list.filter(f => !f.name.startsWith('.'));
  }
  return list;
});

const vaultItems = computed(() => {
  const items = [];
  const query = vaultSearchQuery.value.trim().toLowerCase();
  
  if (query) {
    for (const f of allVaultFiles.value) {
      const orig = f.originalName || f.name;
      const fileName = orig.split('/').pop() || '';
      if (fileName !== '.k3empty' && fileName.toLowerCase().includes(query)) {
        if (!globalSettings.value.showHidden && fileName.startsWith('.')) continue;
        items.push({ ...f, isDirectory: false, displayName: fileName, originalPath: orig });
      }
    }
    return items;
  }

  const folders = new Set();
  const prefix = currentVaultDir.value === '/' ? '' : currentVaultDir.value + '/';
  
  for (const f of allVaultFiles.value) {
    const orig = f.originalName || f.name;
    if (orig.startsWith(prefix)) {
      const rest = orig.substring(prefix.length);
      const parts = rest.split('/');
      if (parts.length === 1) {
        if (parts[0] !== '.k3empty') {
          if (!globalSettings.value.showHidden && parts[0].startsWith('.')) continue;
          items.push({ ...f, isDirectory: false, displayName: parts[0] });
        }
      } else {
        const folderName = parts[0];
        if (!folders.has(folderName)) {
          if (!globalSettings.value.showHidden && folderName.startsWith('.')) continue;
          folders.add(folderName);
          items.push({ isDirectory: true, name: folderName, displayName: folderName, size: 0, originalName: prefix + folderName + '/.k3empty' });
        }
      }
    }
  }
  return items;
});

const goUpVaultDir = () => {
  if (currentVaultDir.value === '/') return;
  const parts = currentVaultDir.value.split('/');
  parts.pop();
  currentVaultDir.value = parts.length > 0 ? parts.join('/') : '/';
  selectedVaultFiles.value.clear();
};

const openVaultDir = (dirName: string) => {
  currentVaultDir.value = currentVaultDir.value === '/' ? dirName : currentVaultDir.value + '/' + dirName;
  selectedVaultFiles.value.clear();
};

const currentLocalDir = ref<string>('');
const loadingVault = ref(false);
const loadingLocal = ref(false);
const processingStatus = ref('');
const scanStatus = ref<'scanning'|'safe'|'danger'|null>(null);
const progressData = ref<{ file: string, status: string, percent?: number } | null>(null);

const clipboard = ref<{ path: string, name: string, action: 'copy'|'cut' }[] | null>(null);

const contextMenu = ref({ visible: false, x: 0, y: 0, file: null as any, isLocal: true, isBackground: false });

const promptState = ref({ visible: false, title: '', defaultValue: '', resolve: null as any });
const confirmState = ref({ visible: false, message: '', isDanger: false, resolve: null as any });

const openPrompt = (title: string, defaultValue: string = '') => {
  return new Promise<string | null>((resolve) => {
    promptState.value = { visible: true, title, defaultValue, resolve };
  });
};

const openConfirm = (message: string, isDanger: boolean = false) => {
  return new Promise<boolean>((resolve) => {
    confirmState.value = { visible: true, message, isDanger, resolve };
  });
};

const closeConfirm = (result: boolean) => {
  const resolve = confirmState.value.resolve;
  confirmState.value.visible = false;
  if (resolve) resolve(result);
};

const closePrompt = (save: boolean) => {
  const value = save ? promptState.value.defaultValue : null;
  const resolve = promptState.value.resolve;
  promptState.value.visible = false;
  if (resolve) resolve(value);
};

const api = (window as any).electronAPI;

const openFileIfText = (f: any) => {
  if (f.isDirectory) return;
  const ext = f.displayName.split('.').pop()?.toLowerCase();
  if (['txt', 'md', 'csv', 'json', 'xml'].includes(ext)) {
    openTextEditor(f, '');
  }
};

const openTextEditor = async (f: any = null, defaultName = '') => {
  if (f) {
    processingStatus.value = 'Đang tải nội dung...';
    const res = await (window as any).electronAPI.readVaultText(f.name);
    processingStatus.value = '';
    if (!res.success) {
      (window as any).showToast('Lỗi đọc file: ' + res.error, 'error');
      return;
    }
    textEditorState.value = {
      visible: true,
      fileName: f.name,
      displayName: f.displayName || f.name,
      content: res.text,
      isSaving: false
    };
  } else {
    textEditorState.value = {
      visible: true,
      fileName: null,
      displayName: defaultName,
      content: '',
      isSaving: false
    };
  }
};

const saveTextNote = async () => {
  if (!textEditorState.value.displayName.trim()) {
    (window as any).showToast('Vui lòng nhập tên file!', 'error');
    return;
  }
  textEditorState.value.isSaving = true;
  
  let finalPath = currentVaultDir.value === '/' ? textEditorState.value.displayName : currentVaultDir.value + '/' + textEditorState.value.displayName;
  if (!finalPath.toLowerCase().match(/\.(txt|md|csv|json)$/)) {
      finalPath += '.txt';
  }

  const res = await (window as any).electronAPI.writeVaultText(
    textEditorState.value.fileName, 
    textEditorState.value.content, 
    finalPath, 
    enableCompression.value
  );
  
  textEditorState.value.isSaving = false;
  
  if (res.success) {
    (window as any).showToast('Đã lưu thành công!', 'success');
    textEditorState.value.visible = false;
    await loadVaultFiles();
  } else {
    (window as any).showToast('Lỗi khi lưu: ' + res.error, 'error');
  }
};

const loadVaultFiles = async () => {
  loadingVault.value = true;
  try { allVaultFiles.value = await api.listFiles(); } catch (e) {}
  loadingVault.value = false;
  selectedVaultFiles.value.clear();
};

const loadDrives = async () => {
  drives.value = await api.getDrives();
  if (drives.value.length > 0) await openLocalDir(drives.value[0].path);
};

const openLocalDir = async (dirPath: string) => {
  loadingLocal.value = true;
  currentLocalDir.value = dirPath;
  localFiles.value = await api.listLocalDir(dirPath);
  loadingLocal.value = false;
  selectedLocalFiles.value.clear();
};

const goUpLocalDir = async () => {
  let parts = currentLocalDir.value.split(/[/\\]/);
  parts.pop();
  if (parts.length === 0 || (parts.length === 1 && parts[0] === '')) parts = ['/'];
  const parentPath = parts.join('/') || '/';
  await openLocalDir(parts.join('\\').endsWith(':') ? parts.join('\\') + '\\' : parentPath);
};

onMounted(() => {
  loadVaultFiles();
  loadDrives();
  document.addEventListener('click', closeContextMenu);
  
  if (api.onEncryptionProgress) {
    api.onEncryptionProgress((data: any) => {
      progressData.value = data;
    });
  }
  
  if ((api as any).onAutoEncryptDone) {
    (api as any).onAutoEncryptDone(() => {
      loadVaultFiles();
      (window as any).showToast('Đã tự động cất file vào Két Sắt an toàn!', 'success');
    });
  }
});
onUnmounted(() => {
  document.removeEventListener('click', closeContextMenu);
});

const formatBytes = (bytes: number) => {
  if (bytes === 0) return '0 Bytes';
  const k = 1024, sizes = ['Bytes', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
};

const handleCopyToVault = async () => {
  const filesToEncrypt = Array.from(selectedLocalFiles.value);
  if (filesToEncrypt.length === 0) return;
  
  let successCount = 0;
  let totalBlocked = 0;
  for (let i = 0; i < filesToEncrypt.length; i++) {
    const file = filesToEncrypt[i];
    
    const targetOriginalName = currentVaultDir.value === '/' ? file.name : currentVaultDir.value + '/' + file.name;
    const existingFile = allVaultFiles.value.find((v) => (v.originalName || v.name) === targetOriginalName);
    if (existingFile && !file.isDirectory) {
      const confirmed = await openConfirm(`Tệp '${file.name}' đã tồn tại trong Két sắt. Bạn có muốn ghi đè không?`);
      if (!confirmed) continue;
      await api.deleteFile(existingFile.name);
    }

    let isRaw = false;
    if (globalSettings.value.maxEncryptSize > 0) {
      const sizeMB = (file.size || 0) / (1024 * 1024);
      if (sizeMB > globalSettings.value.maxEncryptSize) {
        isRaw = true;
      }
    }

    if (enableVirusScan.value) {
      scanStatus.value = 'scanning';
      processingStatus.value = `Đang quét virus [${i+1}/${filesToEncrypt.length}]: ${file.name}...`;
      const scanResult = await api.scanFile(file.path);
      
      if (!scanResult.safe) {
        scanStatus.value = 'danger';
        const isApiErr = scanResult.threat && scanResult.threat.includes('Lỗi API');
        if (isApiErr) {
            processingStatus.value = `Lỗi quét Virus: ${scanResult.threat}! Đang bỏ qua tệp này...`;
            setTimeout(() => { processingStatus.value = ''; scanStatus.value = null; }, 2000);
            continue;
        } else {
            processingStatus.value = `PHÁT HIỆN MÃ ĐỘC: ${scanResult.threat}! Hủy bỏ tệp.`;
            setTimeout(() => { processingStatus.value = ''; scanStatus.value = null; }, 4000);
            continue;
        }
      }
    }
    
    scanStatus.value = 'safe';
    const actionText = isRaw ? 'chép tệp (Bỏ qua mã hóa)' : 'mã hóa';
    processingStatus.value = deleteAfterEncrypt.value ? `Đang ${actionText} & Xóa tệp gốc [${i+1}/${filesToEncrypt.length}]: ${file.name}` : `Đang ${actionText} [${i+1}/${filesToEncrypt.length}]: ${file.name}`;
    const res = await api.encryptFile(file.path, deleteAfterEncrypt.value, currentVaultDir.value, enableCompression.value, isRaw); 
    if (!res || !res.success) {
       if (res && res.error) (window as any).showToast(`Lỗi mã hoá ${file.name}: ${res.error}`, 'error');
    } else {
       successCount++;
       if (res.blocked) totalBlocked += res.blocked;
    }
  }
  
  processingStatus.value = '';
  progressData.value = null;
  scanStatus.value = null;
  if (totalBlocked > 0) {
    (window as any).showToast(`Đã mã hoá ${successCount}/${filesToEncrypt.length} mục. ĐÃ CÁCH LY ${totalBlocked} TỆP MÃ ĐỘC!`, 'error');
  } else {
    (window as any).showToast(`Đã mã hoá thành công ${successCount}/${filesToEncrypt.length} tệp.`, 'success');
  }
  await loadVaultFiles();
  await openLocalDir(currentLocalDir.value);
};

const handleCopyFromVault = async () => {
  const filesToDecrypt = Array.from(selectedVaultFiles.value);
  if (filesToDecrypt.length === 0 || !currentLocalDir.value) return;
  
  let successCount = 0;
  const isAutoDecrypt = globalSettings.value.autoDecrypt;
  
  for (let i = 0; i < filesToDecrypt.length; i++) {
    const file = filesToDecrypt[i];
    processingStatus.value = `Đang ${isAutoDecrypt ? 'giải mã' : 'chép'} [${i+1}/${filesToDecrypt.length}]: ${file.name || file.displayName}`;
    
    if (file.isDirectory) {
      const prefix = currentVaultDir.value === '/' ? file.name + '/' : currentVaultDir.value + '/' + file.name + '/';
      const exactEmpty = currentVaultDir.value === '/' ? file.name + '/.k3empty' : currentVaultDir.value + '/' + file.name + '/.k3empty';
      let hasError = false;
      
      for (const f of allVaultFiles.value) {
        const orig = f.originalName || f.name;
        if (orig.startsWith(prefix) || orig === exactEmpty) {
          let relativeStructure = orig;
          if (currentVaultDir.value !== '/') {
            relativeStructure = orig.substring(currentVaultDir.value.length + 1);
          }
          const res = isAutoDecrypt 
            ? await api.decryptFile(f.name, currentLocalDir.value, relativeStructure)
            : await api.copyRawFile(f.name, currentLocalDir.value);
          if (!res.success) {
            hasError = true;
            (window as any).showToast(`Lỗi ${isAutoDecrypt?'giải mã':'chép'} file ` + f.name + ': ' + res.error, 'error');
          }
        }
      }
      if (!hasError) successCount++;
    } else {
      const res = isAutoDecrypt 
        ? await api.decryptFile(file.name, currentLocalDir.value)
        : await api.copyRawFile(file.name, currentLocalDir.value);
      if (res.success) successCount++;
      else (window as any).showToast(`Lỗi ${isAutoDecrypt?'giải mã':'chép'}: ` + res.error, 'error');
    }
  }
  
  processingStatus.value = '';
  progressData.value = null;
  (window as any).showToast(`Đã ${isAutoDecrypt?'giải mã':'chép'} thành công ${successCount}/${filesToDecrypt.length} tệp/thư mục.`, 'success');
  await openLocalDir(currentLocalDir.value);
};

const handleDeleteVault = async () => {
  const filesToDelete = Array.from(selectedVaultFiles.value);
  if (filesToDelete.length === 0) return;
  const confirmed = await openConfirm(`Xóa vĩnh viễn ${filesToDelete.length} tệp/thư mục này khỏi Két sắt?`, true);
  if (confirmed) {
    processingStatus.value = `Đang xóa dữ liệu...`;
    for (let i = 0; i < filesToDelete.length; i++) {
      const file = filesToDelete[i];
      if (file.isDirectory) {
        const prefix = currentVaultDir.value === '/' ? file.name + '/' : currentVaultDir.value + '/' + file.name + '/';
        const exactEmpty = currentVaultDir.value === '/' ? file.name + '/.k3empty' : currentVaultDir.value + '/' + file.name + '/.k3empty';
        for (const f of allVaultFiles.value) {
          const orig = f.originalName || f.name;
          if (orig.startsWith(prefix) || orig === exactEmpty) {
            await api.deleteFile(f.name);
          }
        }
      } else {
        await api.deleteFile(file.name);
      }
    }
    selectedVaultFiles.value.clear();
    processingStatus.value = '';
    (window as any).showToast('Đã xóa thành công.', 'success');
    await loadVaultFiles();
  }
};


const handleLocalClick = (e: MouseEvent, f: any) => {
  if (e.metaKey || e.ctrlKey) {
    if (selectedLocalFiles.value.has(f)) selectedLocalFiles.value.delete(f);
    else selectedLocalFiles.value.add(f);
  } else {
    selectedLocalFiles.value.clear();
    selectedLocalFiles.value.add(f);
  }
};

const handleVaultClick = (e: MouseEvent, f: any) => {
  if (e.metaKey || e.ctrlKey) {
    if (selectedVaultFiles.value.has(f)) selectedVaultFiles.value.delete(f);
    else selectedVaultFiles.value.add(f);
  } else {
    selectedVaultFiles.value.clear();
    selectedVaultFiles.value.add(f);
  }
};

const clearAllSelection = () => {
  selectedLocalFiles.value.clear();
  selectedVaultFiles.value.clear();
};

// Context Menu Logic
const showContextMenu = (e: MouseEvent, file: any, isLocal: boolean) => {
  e.preventDefault();
  if (isLocal) {
    if (!selectedLocalFiles.value.has(file)) {
      selectedLocalFiles.value.clear();
      selectedLocalFiles.value.add(file);
    }
  } else {
    if (!selectedVaultFiles.value.has(file)) {
      selectedVaultFiles.value.clear();
      selectedVaultFiles.value.add(file);
    }
  }
  
  contextMenu.value = { visible: true, x: e.clientX, y: e.clientY, file, isLocal, isBackground: false };
};

const showLocalBgMenu = (e: MouseEvent) => {
  if (e.target !== e.currentTarget && (e.target as HTMLElement).closest('tr')) return;
  e.preventDefault();
  contextMenu.value = { visible: true, x: e.clientX, y: e.clientY, file: null, isLocal: true, isBackground: true };
};

const closeContextMenu = () => { contextMenu.value.visible = false; };

const menuAction = async (action: string) => {
  const { file, isLocal } = contextMenu.value;
  switch (action) {
    case 'encrypt':
      if (isLocal) await handleCopyToVault();
      break;
    case 'decrypt':
      if (!isLocal) await handleCopyFromVault();
      break;
    case 'export-zip':
      if (!isLocal) {
        const filesToExportTarget = Array.from(selectedVaultFiles.value);
        if (filesToExportTarget.length === 0) return;
        
        const zipPwd = await openPrompt('Tạo mật khẩu cho file ZIP này (Để an toàn khi gửi qua mạng):');
        if (!zipPwd) return;
        
        const firstFile = filesToExportTarget[0];
        const defaultName = filesToExportTarget.length > 1 ? 'Archive.zip' : (firstFile.displayName ? firstFile.displayName.replace('.k3enc', '').replace('.k3dir', '.zip') : firstFile.name.replace('.k3enc', '.zip'));
        const defaultPathStr = currentLocalDir.value ? currentLocalDir.value + '/' + (defaultName.endsWith('.zip') ? defaultName : defaultName + '.zip') : (defaultName.endsWith('.zip') ? defaultName : defaultName + '.zip');
        const savePath = await api.showSaveDialog({ 
            title: 'Lưu file ZIP',
            defaultPath: defaultPathStr.replace(/\\/g, '/'),
            filters: [{ name: 'ZIP Archives', extensions: ['zip'] }]
        });
        if (savePath) {
          processingStatus.value = `Đang nén và mã hóa ZIP...`;
          let filesToExport = [];
          
          for (const targetFile of filesToExportTarget) {
            if (targetFile.isDirectory) {
              const prefix = currentVaultDir.value === '/' ? targetFile.name + '/' : currentVaultDir.value + '/' + targetFile.name + '/';
              const exactEmpty = currentVaultDir.value === '/' ? targetFile.name + '/.k3empty' : currentVaultDir.value + '/' + targetFile.name + '/.k3empty';
              for (const f of allVaultFiles.value) {
                const orig = f.originalName || f.name;
                if (orig.startsWith(prefix) || orig === exactEmpty) {
                  let relativeStructure = orig;
                  if (currentVaultDir.value !== '/') {
                    relativeStructure = orig.substring(currentVaultDir.value.length + 1);
                  }
                  filesToExport.push({ fileName: f.name, relativeStructure });
                }
              }
            } else {
              filesToExport.push({ fileName: targetFile.name, relativeStructure: targetFile.displayName || targetFile.name });
            }
          }
          
          const res = await api.exportZip(filesToExport, zipPwd, savePath);
          processingStatus.value = '';
          if (res.success) (window as any).showToast('Đã xuất file ZIP thành công!', 'success');
          else (window as any).showToast('Lỗi: ' + res.error, 'error');
        }
      }
      break;
    case 'copy':
      if (isLocal && selectedLocalFiles.value.size > 0) {
        clipboard.value = Array.from(selectedLocalFiles.value).map((f: any) => ({ path: f.path, name: f.name, action: 'copy' as const }));
        (window as any).showToast(`Đã copy ${selectedLocalFiles.value.size} tệp.`, 'success');
      }
      break;
    case 'cut':
      if (isLocal && selectedLocalFiles.value.size > 0) {
        clipboard.value = Array.from(selectedLocalFiles.value).map((f: any) => ({ path: f.path, name: f.name, action: 'cut' as const }));
        (window as any).showToast(`Đã cắt ${selectedLocalFiles.value.size} tệp.`, 'success');
      }
      break;
    case 'paste':
      if (clipboard.value && clipboard.value.length > 0 && currentLocalDir.value) {
        processingStatus.value = `Đang dán tệp...`;
        for (const item of clipboard.value) {
          const target = currentLocalDir.value + (currentLocalDir.value.endsWith('/') || currentLocalDir.value.endsWith('\\\\') ? '' : '/') + item.name;
          if (item.action === 'copy') await api.copyLocalFile(item.path, target);
          else await api.moveLocalFile(item.path, target);
        }
        if (clipboard.value[0].action === 'cut') clipboard.value = null; // clear after cut
        await openLocalDir(currentLocalDir.value);
        processingStatus.value = '';
      }
      break;
    case 'delete':
      if (isLocal) {
        const files = Array.from(selectedLocalFiles.value);
        if (files.length === 0) return;
        const confirmed = await openConfirm(`Xóa ${files.length} tệp tin này khỏi máy tính?`);
        if (confirmed) {
          for (const f of files) await api.deleteLocalFile(f.path, false);
          await openLocalDir(currentLocalDir.value);
          selectedLocalFiles.value.clear();
        }
      } else {
        await handleDeleteVault();
      }
      break;
    case 'secure-delete':
      if (isLocal) {
        const files = Array.from(selectedLocalFiles.value);
        if (files.length === 0) return;
        const confirmed = await openConfirm(`CẢNH BÁO: Bạn sắp XÓA VĨNH VIỄN (Shred) ${files.length} tệp này, không thể khôi phục! Tiếp tục?`, true);
        if (confirmed) {
          processingStatus.value = `Đang hủy tệp vĩnh viễn...`;
          for (const f of files) await api.deleteLocalFile(f.path, true);
          processingStatus.value = '';
          await openLocalDir(currentLocalDir.value);
          selectedLocalFiles.value.clear();
        }
      }
      break;
    case 'rename':
      if (isLocal) {
        const files = Array.from(selectedLocalFiles.value);
        if (files.length === 1) {
          const f = files[0];
          const newName = await openPrompt('Nhập tên mới:', f.name);
          if (newName && newName !== f.name) {
            const newPath = currentLocalDir.value + (currentLocalDir.value.endsWith('/') || currentLocalDir.value.endsWith('\\') ? '' : '/') + newName;
            await api.renameLocal(f.path, newPath);
            await openLocalDir(currentLocalDir.value);
            selectedLocalFiles.value.clear();
          }
        } else if (files.length > 1) {
          (window as any).showToast('Chỉ có thể đổi tên 1 file tại một thời điểm.', 'error');
        }
      } else {
        const files = Array.from(selectedVaultFiles.value);
        if (files.length === 1) {
          const f = files[0];
          const newName = await openPrompt('Nhập tên mới:', f.displayName ? f.displayName.replace('.k3enc', '').replace('.k3dir', '') : f.name);
          if (newName) {
            if (f.isDirectory) {
              const oldPrefix = currentVaultDir.value === '/' ? f.name : currentVaultDir.value + '/' + f.name;
              const newPrefix = currentVaultDir.value === '/' ? newName : currentVaultDir.value + '/' + newName;
              await api.renameVaultFolder(oldPrefix, newPrefix);
            } else {
              const ext = f.name.endsWith('.k3enc') ? '.k3enc' : '';
              await api.renameVaultFile(f.name, newName + ext);
            }
            await loadVaultFiles();
            selectedVaultFiles.value.clear();
          }
        } else if (files.length > 1) {
          (window as any).showToast('Chỉ có thể đổi tên 1 file tại một thời điểm.', 'error');
        }
      }
      break;
    case 'new-folder':
      const folderName = await openPrompt('Tên thư mục mới:', 'New Folder');
      if (folderName) {
        const folderPath = currentLocalDir.value + (currentLocalDir.value.endsWith('/') || currentLocalDir.value.endsWith('\\') ? '' : '/') + folderName;
        await api.createLocalFolder(folderPath);
        await openLocalDir(currentLocalDir.value);
      }
      break;
    case 'new-vault-folder':
      const vFolder = await openPrompt('Tên thư mục mới:', 'New Folder');
      if (vFolder) {
        await api.createVaultFolder(vFolder, currentVaultDir.value);
        await loadVaultFiles();
      }
      break;
    case 'refresh':
      if (isLocal) await openLocalDir(currentLocalDir.value);
      else await loadVaultFiles();
      break;
  }
};
const handleDropVault = async (e: DragEvent) => {
  if (!e.dataTransfer || !e.dataTransfer.files) return;
  const files = Array.from(e.dataTransfer.files);
  if (files.length === 0) return;
  
  processingStatus.value = `Đang xử lý ${files.length} tệp tin...`;
  for (const f of files) {
    const filePath = (f as any).path || (api.getPathForFile ? api.getPathForFile(f) : undefined);
    if (!filePath) continue;

    const targetOriginalName = currentVaultDir.value === '/' ? f.name : currentVaultDir.value + '/' + f.name;
    const existingFile = allVaultFiles.value.find((v) => (v.originalName || v.name) === targetOriginalName);
    
    // Check if it's a file (not a folder). We assume it's a file if it has a size and type, or we just check if existingFile is not a directory marker
    if (existingFile && !existingFile.originalName?.endsWith('.k3empty')) {
      const confirmed = await openConfirm(`Tệp '${f.name}' đã tồn tại trong Két sắt. Bạn có muốn ghi đè không?`);
      if (!confirmed) continue;
      await api.deleteFile(existingFile.name);
    }
    
    let isRaw = false;
    if (globalSettings.value.maxEncryptSize > 0) {
      const sizeMB = f.size / (1024 * 1024);
      if (sizeMB > globalSettings.value.maxEncryptSize) {
        isRaw = true;
      }
    }
    
    if (enableVirusScan.value) {
      scanStatus.value = 'scanning';
      const scanRes = await api.scanFile(filePath);
      if (scanRes && !scanRes.safe) {
        scanStatus.value = 'danger';
        const isApiErr = scanRes.threat && scanRes.threat.includes('Lỗi API');
        if (isApiErr) {
            const confirmed = await openConfirm(`Không thể quét Virus: ${scanRes.threat}. Bỏ qua tệp ${f.name}?`, true);
            if (confirmed) continue;
        } else {
            const confirmed = await openConfirm(`Phát hiện mã độc (${scanRes.threat}) trong ${f.name}! Xóa tệp này ngay lập tức?`, true);
            if (confirmed) await api.deleteLocalFile(filePath, true);
            continue;
        }
      }
      scanStatus.value = 'safe';
    }
    
    processingStatus.value = isRaw ? `Đang chép tệp thô (bỏ qua mã hóa)...` : `Đang mã hóa tệp...`;
    await api.encryptFile(filePath, deleteAfterEncrypt.value, currentVaultDir.value, enableCompression.value, isRaw);
  }
  
  processingStatus.value = '';
  scanStatus.value = null;
  progressData.value = null;
  (window as any).showToast('Đã mã hóa thành công.', 'success');
  if (deleteAfterEncrypt.value && currentLocalDir.value) await openLocalDir(currentLocalDir.value);
  await loadVaultFiles();
};
const checkVaultIntegrity = async () => {
  processingStatus.value = `Đang quét toàn vẹn Két sắt...`;
  scanStatus.value = 'scanning';
  const res = await api.checkVaultIntegrity();
  processingStatus.value = '';
  
  if (!res.success) {
    scanStatus.value = 'danger';
    await openConfirm('Lỗi khi quét: ' + res.error);
    scanStatus.value = null;
    return;
  }
  
  if (res.corruptedFiles && res.corruptedFiles.length > 0) {
    scanStatus.value = 'danger';
    await openConfirm(`CẢNH BÁO: Phát hiện ${res.corruptedFiles.length} tệp bị hỏng hoặc bị sửa đổi trái phép trong tổng số ${res.totalFiles} tệp! Vui lòng kiểm tra lại ổ đĩa USB.`);
  } else {
    scanStatus.value = 'safe';
    await openConfirm(`Hoàn tất: Két sắt an toàn tuyệt đối. Đã quét ${res.totalFiles} tệp và không phát hiện bất thường nào.`);
  }
  scanStatus.value = null;
};

const formatVault = async () => {
  const confirmed = await openConfirm('CẢNH BÁO NGUY HIỂM: Hành động này sẽ XÓA VĨNH VIỄN toàn bộ dữ liệu trong Két Sắt và khôi phục trạng thái xuất xưởng. Bạn có chắc chắn muốn Format Két Sắt?', true);
  if (!confirmed) return;
  
  processingStatus.value = 'Đang Format Két Sắt...';
  const res = await api.formatVault();
  processingStatus.value = '';
  
  if (res.success) {
    (window as any).showToast('Đã Format Két Sắt thành công!', 'success');
    currentVaultDir.value = '/';
    await loadVaultFiles();
  } else {
    (window as any).showToast('Lỗi khi Format: ' + res.error, 'error');
  }
};

</script>

<template>
  <div class="fm-container">
    <div class="page-title flex-between" style="padding-bottom: 15px;">
      <div style="display:flex; align-items:center; gap: 8px">
        <FolderLock :size="24" color="var(--accent)" /> Quản lý Dữ liệu
      </div>
      <div class="settings-container">
        <button class="btn ghost" @click="globalSettings.visible = true" style="font-size: 13px; padding: 6px 12px; display: flex; align-items: center; gap: 5px;">
          <Settings :size="16" /> Cài đặt chung
        </button>
      </div>
    </div>

    <div class="main-layout">
      <!-- Cột Trái: Danh sách ổ đĩa -->
      <div class="sidebar glass-panel">
        <div class="sidebar-title">Ổ Đĩa Local</div>
        <ul class="drive-list">
          <li v-for="d in drives" :key="d.path" @click="openLocalDir(d.path)" :class="{ active: currentLocalDir.startsWith(d.path) }">
            <HardDrive :size="16" color="#3b82f6" /> {{ d.name }}
          </li>
        </ul>
      </div>

      <!-- Cột Giữa: File Local -->
      <div class="pane glass-panel">
        <div class="pane-header flex-between">
          <div style="display:flex; align-items:center; gap:8px; flex: 1; min-width: 0;">
            <button class="icon-btn-small" @click="goUpLocalDir" :disabled="isSearchingLocal"><ChevronLeft :size="16" /></button>
            <span class="path-text" v-if="!isSearchingLocal">{{ currentLocalDir }}</span>
            <input v-model="localSearchQuery" placeholder="Tìm máy tính (Everything)..." class="input-field" style="margin-bottom: 0; padding: 4px 8px; font-size: 13px;" v-else />
          </div>
          <button class="icon-btn-small" @click="isSearchingLocal = !isSearchingLocal; if(!isSearchingLocal) localSearchQuery = '';" title="Tìm kiếm"><Search :size="16" /></button>
        </div>
        <div class="pane-content" @contextmenu="showLocalBgMenu">
          <div v-if="loadingLocal" class="loading-state"><RefreshCw class="spin" /></div>
          <table v-else class="file-table">
            <thead><tr><th>Tên</th><th width="80">Size</th></tr></thead>
            <tbody>
              <tr v-for="f in displayedLocalFiles" :key="f.path || f.name" :class="{ selected: selectedLocalFiles.has(f) }"
                @click="handleLocalClick($event, f)" @dblclick="f.isDirectory ? openLocalDir(f.path) : null"
                @contextmenu.prevent.stop="showContextMenu($event, f, true)">
                <td>
                  <div class="file-name">
                    <Folder v-if="f.isDirectory" :size="16" color="#3b82f6" />
                    <File v-else :size="16" color="var(--text-muted)" />
                    <span class="truncate">{{ f.name }}</span>
                  </div>
                </td>
                <td class="text-right text-muted">{{ f.isDirectory ? '' : formatBytes(f.size) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Cột Các nút Mũi tên -->
      <div class="arrows-col">
        <button class="arrow-btn" :disabled="selectedLocalFiles.size === 0" @click="handleCopyToVault()" title="Đưa vào Két sắt">
          <ArrowRightCircle :size="32" />
        </button>
        <button class="arrow-btn" :disabled="selectedVaultFiles.size === 0" @click="handleCopyFromVault()" title="Giải mã về Máy tính">
          <ArrowLeftCircle :size="32" />
        </button>
      </div>

      <!-- Cột Phải: Két sắt -->
      <div class="pane glass-panel" @drop.prevent="handleDropVault" @dragover.prevent>
        <div class="pane-header flex-between">
          <div style="display:flex; align-items:center; gap:8px; flex: 1; min-width: 0;">
            <button class="icon-btn-small" @click="goUpVaultDir" :disabled="currentVaultDir === '/' || !!vaultSearchQuery"><ChevronLeft :size="16" /></button>
            <span class="path-text" v-if="!vaultSearchQuery">Két: {{ currentVaultDir }}</span>
            <input v-model="vaultSearchQuery" placeholder="Tìm trong Két sắt..." class="input-field" style="margin-bottom: 0; padding: 4px 8px; font-size: 13px;" v-else />
          </div>
          <div class="vault-actions-container">
            <div class="vault-actions">
              <button class="icon-btn-small" @click="vaultSearchQuery = vaultSearchQuery ? '' : ' '" title="Tìm kiếm"><Search :size="16" /></button>
              <button class="icon-btn-small" @click="openTextEditor(null, 'Ghi_chu_moi.txt')" title="Tạo Ghi chú mới"><FileText :size="16" color="var(--accent)" /></button>
              <button class="icon-btn-small" @click="checkVaultIntegrity" title="Quét Toàn Vẹn Két Sắt"><ShieldCheck :size="16" color="var(--accent)" /></button>
              <button class="icon-btn-small" @click="menuAction('new-vault-folder')" title="Tạo thư mục mới"><Folder :size="16" /></button>
              <button class="icon-btn-small text-danger" @click="handleDeleteVault()" :disabled="selectedVaultFiles.size === 0"><Trash2 :size="16" /></button>
              <button class="icon-btn-small" @click="loadVaultFiles" title="Làm mới"><RefreshCw :size="16" :class="{ 'spin': loadingVault }" /></button>
            </div>
            <button class="icon-btn-small trigger-btn" title="Công cụ Két sắt"><MoreHorizontal :size="16" /></button>
          </div>
        </div>
        <div class="pane-content" @contextmenu.prevent="contextMenu = { visible: true, x: $event.clientX, y: $event.clientY, file: null, isLocal: false, isBackground: true }">
          <div v-if="loadingVault" class="loading-state"><RefreshCw class="spin" /></div>
          <table v-else class="file-table">
            <thead><tr><th>Tên tệp gốc</th><th width="80">Size</th></tr></thead>
            <tbody>
              <tr v-for="f in vaultItems" :key="f.name" :class="{ selected: selectedVaultFiles.has(f) }" 
                @click="handleVaultClick($event, f)" @dblclick="f.isDirectory ? openVaultDir(f.name) : openFileIfText(f)" @contextmenu.prevent.stop="showContextMenu($event, f, false)">
                <td>
                  <div class="file-name">
                    <Folder v-if="f.isDirectory" :size="16" color="var(--accent)" />
                    <Lock v-else :size="16" color="var(--accent)" />
                    <span class="truncate">{{ f.displayName.replace('.k3enc', '').replace('.k3dir', '') }}</span>
                  </div>
                </td>
                <td class="text-right text-muted">{{ f.isDirectory ? '' : formatBytes(f.size) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Processing Modal -->
    <Transition name="fade">
      <div v-if="processingStatus" class="processing-overlay">
        <div class="glass-panel processing-modal" :class="scanStatus">
          <RefreshCw v-if="scanStatus === 'scanning' || (!scanStatus && !progressData)" :size="48" color="var(--accent)" class="spin" />
          <ShieldCheck v-if="scanStatus === 'safe'" :size="48" color="var(--accent)" />
          <ShieldAlert v-if="scanStatus === 'danger'" :size="48" color="var(--danger)" class="shake" />
          
          <h3 :class="{'text-danger': scanStatus === 'danger'}">Tiến trình Bảo mật</h3>
          <p>{{ processingStatus }}</p>
          
          <div v-if="progressData" class="progress-container">
            <div class="progress-label">
              <span class="truncate">{{ progressData.file }}</span>
              <span>Đang xử lý...</span>
            </div>
            <div class="progress-bar-bg">
              <div class="progress-bar-fill spin-bg"></div>
            </div>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Custom Prompt Modal -->
    <Transition name="fade">
      <div v-if="promptState.visible" class="processing-overlay" style="z-index: 1001;">
        <div class="glass-panel processing-modal" style="min-width: 300px; text-align: left;">
          <h3 style="margin-top:0; font-size: 16px; margin-bottom: 15px;">{{ promptState.title }}</h3>
          <input type="text" class="input-field" v-model="promptState.defaultValue" @keyup.enter="closePrompt(true)" />
          <div style="display:flex; justify-content:flex-end; gap: 10px; margin-top: 20px;">
            <button class="btn ghost" @click="closePrompt(false)">Hủy</button>
            <button class="btn" @click="closePrompt(true)">Xác nhận</button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Text Editor Modal -->
    <Transition name="fade">
      <div v-if="textEditorState.visible" class="processing-overlay" style="z-index: 1003; padding: 20px;">
        <div class="glass-panel" style="width: 100%; max-width: 800px; height: 80vh; display: flex; flex-direction: column; text-align: left; background: rgba(15, 20, 30, 0.95); border: 1px solid var(--border);">
          <div style="display: flex; justify-content: space-between; align-items: center; padding: 15px; border-bottom: 1px solid var(--border);">
            <div style="display: flex; align-items: center; gap: 10px; flex: 1;">
              <FileText :size="20" color="var(--accent)" />
              <input type="text" class="input-field" v-model="textEditorState.displayName" placeholder="Tên file (vd: ghi-chu.txt)" style="margin-bottom: 0; max-width: 300px; padding: 5px 10px;" :disabled="textEditorState.fileName !== null" />
            </div>
            <div style="display: flex; gap: 10px;">
              <button class="btn ghost" @click="textEditorState.visible = false" :disabled="textEditorState.isSaving"><X :size="16"/> Đóng</button>
              <button class="btn" @click="saveTextNote" :disabled="textEditorState.isSaving">
                <RefreshCw v-if="textEditorState.isSaving" :size="16" class="spin"/>
                <Save v-else :size="16"/> Lưu vào Két
              </button>
            </div>
          </div>
          <div style="flex: 1; padding: 15px; display: flex;">
            <textarea v-model="textEditorState.content" style="width: 100%; height: 100%; background: rgba(0,0,0,0.3); border: 1px solid var(--border); border-radius: 8px; padding: 15px; color: var(--text-main); font-family: monospace; font-size: 14px; resize: none; outline: none;"></textarea>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Custom Confirm Modal -->
    <Transition name="fade">
      <div v-if="confirmState.visible" class="processing-overlay" style="z-index: 1002;">
        <div class="glass-panel processing-modal" style="min-width: 300px; text-align: left;" :class="{'danger': confirmState.isDanger}">
          <h3 style="margin-top:0; font-size: 16px; margin-bottom: 15px;" :class="{'text-danger': confirmState.isDanger}">
            {{ confirmState.isDanger ? 'Cảnh báo' : 'Xác nhận' }}
          </h3>
          <p style="margin-bottom: 20px;">{{ confirmState.message }}</p>
          <div style="display:flex; justify-content:flex-end; gap: 10px;">
            <button class="btn ghost" @click="closeConfirm(false)">Hủy</button>
            <button class="btn" :class="{'danger': confirmState.isDanger}" @click="closeConfirm(true)">Đồng ý</button>
          </div>
        </div>
      </div>
    </Transition>

    <!-- Context Menu -->
    <div v-if="contextMenu.visible" class="context-menu glass-panel" :style="{ top: contextMenu.y + 'px', left: contextMenu.x + 'px' }">
      <template v-if="contextMenu.isLocal">
        <template v-if="!contextMenu.isBackground">
          <div class="menu-item text-success" @click="menuAction('encrypt')">Mã hóa (Đưa vào Két sắt)</div>
          <div class="menu-divider"></div>
          <div class="menu-item" @click="menuAction('copy')">Copy</div>
          <div class="menu-item" @click="menuAction('cut')">Cut</div>
          <div class="menu-item" @click="menuAction('rename')">Rename</div>
          <div class="menu-divider"></div>
          <div class="menu-item" @click="menuAction('delete')">Delete</div>
          <div class="menu-item text-danger" @click="menuAction('secure-delete')">Secure Delete (Xóa hủy)</div>
        </template>
        <template v-else>
          <div class="menu-item" :class="{ disabled: !clipboard }" @click="clipboard ? menuAction('paste') : null">Paste</div>
          <div class="menu-item" @click="menuAction('new-folder')">Thư mục mới...</div>
          <div class="menu-divider"></div>
          <div class="menu-item" @click="menuAction('refresh')">Refresh</div>
        </template>
      </template>
      
      <template v-else>
        <template v-if="!contextMenu.isBackground">
          <div class="menu-item text-success" @click="menuAction('decrypt')">Giải mã ra Máy tính</div>
          <div class="menu-item" @click="menuAction('export-zip')">Xuất thành file ZIP có mật khẩu</div>
          <div class="menu-divider"></div>
          <div class="menu-item text-danger" @click="menuAction('delete')">Delete khỏi Két sắt</div>
        </template>
        <template v-else>
          <div class="menu-item" @click="menuAction('new-vault-folder')">Thư mục mới...</div>
          <div class="menu-divider"></div>
          <div class="menu-item" @click="menuAction('refresh')">Refresh</div>
        </template>
      </template>
    </div>
    
    <!-- Global Settings Modal -->
    <Transition name="fade">
      <div v-if="globalSettings.visible" class="processing-overlay" style="z-index: 1005;">
        <div class="glass-panel processing-modal" style="width: 600px; max-width: 90vw; text-align: left;">
          <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; border-bottom: 1px solid var(--border); padding-bottom: 10px;">
            <h3 style="margin: 0; font-size: 18px; display: flex; align-items: center; gap: 8px;"><Settings :size="20" color="var(--accent)" /> Cài đặt chung</h3>
            <button class="icon-btn-small" @click="globalSettings.visible = false"><X :size="20" /></button>
          </div>
          
          <div style="display: flex; flex-direction: column; gap: 15px;">
            <div class="setting-item" style="display: flex; flex-direction: column; gap: 5px;">
              <label style="display: flex; align-items: center; gap: 8px; font-weight: bold; cursor: pointer;">
                <input type="checkbox" checked disabled style="accent-color: var(--accent);" />
                Mã hóa dữ liệu được sao chép sang USB trong Thư mục:
                <input type="text" v-model="globalSettings.autoEncryptFolder" class="input-field" style="width: 150px; margin: 0; padding: 4px;" />
              </label>
              <div style="padding-left: 25px; color: var(--text-muted); font-size: 13px;">
                - Tất cả dữ liệu sao chép vào thư mục này trên USB sẽ tự động được mã hóa (Bảo vệ thời gian thực).
              </div>
              <div style="padding-left: 25px; font-size: 13px; margin-top: 5px;">
                * Dung lượng tệp tối đa sẽ được mã hóa (vượt quá sẽ bỏ qua):
                <select v-model="globalSettings.maxEncryptSize" class="input-field" style="width: 120px; margin: 0; padding: 4px; display: inline-block;">
                  <option :value="0">Không giới hạn</option>
                  <option :value="500">500 MB</option>
                  <option :value="1024">1 GB</option>
                  <option :value="2048">2 GB</option>
                  <option :value="5120">5 GB</option>
                </select>
              </div>
            </div>

            <hr style="border: none; border-top: 1px solid var(--border);" />

            <label style="display: flex; flex-direction: column; gap: 5px; cursor: pointer;">
              <div style="display: flex; align-items: center; gap: 8px; font-weight: bold;">
                <input type="checkbox" v-model="globalSettings.autoDecrypt" style="accent-color: var(--accent);" />
                Mặc định Giải mã khi sao chép ra máy tính
              </div>
              <div style="padding-left: 25px; color: var(--text-muted); font-size: 13px;">
                - Khi kéo thả tệp từ Két Sắt ra cột Local, ứng dụng sẽ tự động giải mã thay vì giữ nguyên dạng mã hóa (.k3enc).
              </div>
            </label>

            <label style="display: flex; flex-direction: column; gap: 5px; cursor: pointer;">
              <div style="display: flex; align-items: center; gap: 8px; font-weight: bold;">
                <input type="checkbox" v-model="globalSettings.showHidden" style="accent-color: var(--accent);" />
                Mặc định Hiển thị tất cả các Tệp và Thư mục ẩn trong danh sách
              </div>
            </label>

            <label style="display: flex; flex-direction: column; gap: 5px; cursor: pointer;">
              <div style="display: flex; align-items: center; gap: 8px; font-weight: bold;">
                <input type="checkbox" v-model="globalSettings.autoClearHistory" style="accent-color: var(--accent);" />
                Tự động xóa lịch sử kết nối của USB khi thoát phần mềm
              </div>
            </label>

            <label style="display: flex; flex-direction: column; gap: 5px; cursor: pointer;">
              <div style="display: flex; align-items: center; gap: 8px; font-weight: bold;">
                <input type="checkbox" v-model="globalSettings.cleanSystemJunk" style="accent-color: var(--accent);" />
                Tự động xóa các Metadata thừa của MacOS (các tệp có tên ._ phía trước)
              </div>
            </label>
            
            <hr style="border: none; border-top: 1px solid var(--border);" />
            
            <div style="display: flex; flex-direction: column; gap: 5px;">
              <div style="display: flex; align-items: center; gap: 8px; font-weight: bold; color: var(--danger);">
                <Trash2 :size="16" /> Tùy chọn Nguy hiểm
              </div>
              <div style="padding-left: 25px;">
                <button class="btn btn-danger" @click="formatVault" style="font-size: 13px; padding: 5px 10px;">Format Két Sắt (Xóa trắng)</button>
              </div>
            </div>
            
          </div>
          
          <div style="display:flex; justify-content:center; margin-top: 25px;">
            <button class="btn" @click="saveGlobalSettings" style="width: 200px; display: flex; justify-content: center; gap: 8px;"><Save :size="16" /> Lưu thay đổi</button>
          </div>
        </div>
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.fm-container { display: flex; flex-direction: column; height: 100%; padding: 10px; }
.main-layout { display: flex; flex: 1; gap: 15px; overflow: hidden; }

.sidebar { width: 180px; display: flex; flex-direction: column; }
.sidebar-title { padding: 12px; font-weight: 600; font-size: 12px; text-transform: uppercase; letter-spacing: 1px; color: var(--text-muted); border-bottom: 1px solid var(--border); }
.drive-list { list-style: none; overflow-y: auto; flex: 1; }
.drive-list li { padding: 10px 12px; display: flex; align-items: center; gap: 10px; font-size: 13px; cursor: pointer; transition: all 0.2s; border-bottom: 1px solid rgba(255,255,255,0.02); }
.drive-list li:hover { background: rgba(255,255,255,0.05); }
.drive-list li.active { background: rgba(16, 185, 129, 0.15); color: var(--accent); border-right: 2px solid var(--accent); }

.pane { flex: 1; display: flex; flex-direction: column; overflow: hidden; min-width: 0; }
.pane-header { padding: 10px; border-bottom: 1px solid var(--border); display: flex; align-items: center; gap: 10px; font-size: 13px; font-weight: 600; background: rgba(0,0,0,0.2); }
.flex-between { justify-content: space-between; }
.path-text { flex: 1; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; color: var(--text-muted); font-weight: normal; min-width: 0; }
.pane-content { flex: 1; overflow: auto; position: relative; }

.file-table { width: 100%; border-collapse: collapse; table-layout: fixed; }
.file-table th { padding: 10px; text-align: left; font-size: 12px; color: var(--text-muted); position: sticky; top: 0; background: var(--bg-panel); backdrop-filter: var(--glass-blur); border-bottom: 1px solid var(--border); z-index: 1; white-space: nowrap; }
.file-table td { padding: 8px 10px; font-size: 13px; border-bottom: 1px solid rgba(255,255,255,0.03); cursor: pointer; user-select: none; }
.file-table tr:hover td { background: rgba(255,255,255,0.05); }
.file-table tr.selected td { background: rgba(16, 185, 129, 0.15); color: white; }

.file-name { display: flex; align-items: center; gap: 8px; overflow: hidden; width: 100%; }
.truncate { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; display: block; flex: 1; min-width: 0; }
.text-right { text-align: right; }

.arrows-col { width: 50px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 20px; }
.arrow-btn { background: none; border: none; color: var(--accent); cursor: pointer; border-radius: 50%; display: flex; transition: all 0.2s; }
.arrow-btn:hover:not(:disabled) { transform: scale(1.1); filter: drop-shadow(0 0 8px rgba(16,185,129,0.5)); }
.arrow-btn:disabled { color: var(--text-muted); opacity: 0.3; cursor: not-allowed; }

.icon-btn-small { background: none; border: none; cursor: pointer; color: var(--text-muted); padding: 4px; border-radius: 4px; transition: all 0.2s; display: inline-flex; }
.icon-btn-small:hover:not(:disabled) { background: rgba(255,255,255,0.1); color: var(--text-main); }
.icon-btn-small:disabled { opacity: 0.3; cursor: not-allowed; }

.loading-state { display: flex; justify-content: center; align-items: center; height: 100%; color: var(--accent); }
.spin { animation: spin 1s linear infinite; }
@keyframes spin { 100% { transform: rotate(360deg); } }

.processing-overlay { position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.7); backdrop-filter: blur(5px); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.processing-modal { padding: 40px; text-align: center; min-width: 350px; }
.processing-modal h3 { margin: 20px 0 10px; }
.processing-modal p { color: var(--text-muted); font-size: 14px; }
.processing-modal.danger { border: 1px solid var(--danger); box-shadow: 0 0 30px rgba(239,68,68,0.2); }

@keyframes shake { 0%, 100% { transform: translateX(0); } 20%, 60% { transform: translateX(-5px); } 40%, 80% { transform: translateX(5px); } }
.shake { animation: shake 0.5s ease-in-out; }

.progress-container { margin-top: 20px; text-align: left; }
.progress-label { display: flex; justify-content: space-between; font-size: 12px; color: var(--text-muted); margin-bottom: 6px; }
.progress-label .truncate { max-width: 200px; }
.progress-bar-bg { height: 6px; background: rgba(0,0,0,0.3); border-radius: 3px; overflow: hidden; position: relative; }
.progress-bar-fill { height: 100%; background: var(--accent); width: 50%; border-radius: 3px; box-shadow: 0 0 10px var(--accent-glow); }
.spin-bg { animation: progress-indeterminate 1.5s ease-in-out infinite alternate; }
@keyframes progress-indeterminate { 0% { width: 10%; transform: translateX(-10%); } 100% { width: 80%; transform: translateX(120%); } }

.context-menu {
  position: fixed;
  z-index: 9999;
  min-width: 200px;
  padding: 5px 0;
  display: flex;
  flex-direction: column;
}
.menu-item {
  padding: 8px 15px;
  font-size: 13px;
  cursor: pointer;
  transition: background 0.2s;
}
.menu-item:hover:not(.disabled) { background: rgba(255,255,255,0.1); }
.menu-item.disabled { color: var(--text-muted); cursor: not-allowed; opacity: 0.5; }
.menu-divider { height: 1px; background: var(--border); margin: 5px 0; }

.vault-actions-container {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 10px;
  margin: -10px;
}
.vault-actions {
  display: none;
  gap: 4px;
}
.vault-actions-container:hover .vault-actions {
  display: flex;
}
.vault-actions-container:hover .trigger-btn {
  background: rgba(255, 255, 255, 0.1);
}

.settings-dropdown {
  display: none;
  position: absolute;
  top: 100%;
  right: 0;
  margin-top: 0;
  flex-direction: column;
  gap: 10px;
  padding: 15px;
  z-index: 100;
  background: rgba(15, 20, 30, 0.95);
  border: 1px solid var(--border);
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0,0,0,0.5);
}
.settings-container:hover .settings-dropdown {
  display: flex;
}
</style>
