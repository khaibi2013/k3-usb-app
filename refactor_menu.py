import re

with open('src/components/FileManager.vue', 'r') as f:
    content = f.read()

# Instead of relying on a fragile regex, let's just find the `const menuAction` start and `closeContextMenu();\n};` end and replace the entire function.

match = re.search(r"const menuAction = async \(action: string\) => \{[\s\S]*?closeContextMenu\(\);\n\};", content)
if match:
    new_menu = """const menuAction = async (action: string) => {
  const { isLocal } = contextMenu.value;
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
            defaultPath: defaultPathStr.replace(/\\\\/g, '/'),
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
        clipboard.value = Array.from(selectedLocalFiles.value).map(f => ({ path: f.path, name: f.name, action: 'copy' }));
        (window as any).showToast(`Đã chép ${selectedLocalFiles.value.size} tệp.`, 'success');
      }
      break;
    case 'cut':
      if (isLocal && selectedLocalFiles.value.size > 0) {
        clipboard.value = Array.from(selectedLocalFiles.value).map(f => ({ path: f.path, name: f.name, action: 'cut' }));
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
        }
      }
      break;
    case 'rename':
      if (isLocal) {
        const files = Array.from(selectedLocalFiles.value);
        if (files.length === 1) {
          const file = files[0];
          const newName = await openPrompt('Nhập tên mới:', file.name);
          if (newName && newName !== file.name) {
            const newPath = file.path.replace(file.name, newName);
            await api.renameLocalFile(file.path, newPath);
            await openLocalDir(currentLocalDir.value);
          }
        } else {
          (window as any).showToast('Chỉ có thể đổi tên 1 file tại một thời điểm.', 'error');
        }
      } else {
        const files = Array.from(selectedVaultFiles.value);
        if (files.length === 1) {
          const file = files[0];
          const newName = await openPrompt('Nhập tên mới:', file.displayName ? file.displayName.replace('.k3enc', '').replace('.k3dir', '') : file.name);
          if (newName) {
            if (file.isDirectory) {
              const oldPrefix = currentVaultDir.value === '/' ? file.name : currentVaultDir.value + '/' + file.name;
              const newPrefix = currentVaultDir.value === '/' ? newName : currentVaultDir.value + '/' + newName;
              await api.renameVaultFolder(oldPrefix, newPrefix);
            } else {
              const ext = file.name.endsWith('.k3enc') ? '.k3enc' : '';
              await api.renameVaultFile(file.name, newName + ext);
            }
            await loadVaultFiles();
          }
        } else {
          (window as any).showToast('Chỉ có thể đổi tên 1 file tại một thời điểm.', 'error');
        }
      }
      break;
    case 'new-folder':
      if (isLocal) {
        const folderName = await openPrompt('Nhập tên thư mục mới:');
        if (folderName) {
          await api.createLocalFolder(currentLocalDir.value + '/' + folderName);
          await openLocalDir(currentLocalDir.value);
        }
      }
      break;
    case 'new-vault-folder':
      const folderName = await openPrompt('Nhập tên thư mục mới:');
      if (folderName) {
        await api.createVaultFolder(currentVaultDir.value, folderName);
        await loadVaultFiles();
      }
      break;
  }
  closeContextMenu();
};"""
    content = content[:match.start()] + new_menu + content[match.end():]
    with open('src/components/FileManager.vue', 'w') as f:
        f.write(content)
else:
    print("Failed to find menuAction")
