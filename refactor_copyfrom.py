import re

with open('src/components/FileManager.vue', 'r') as f:
    content = f.read()

# Fix context menu parsing error
content = content.replace("selectedLocalFiles.size === 0s.value.has(file)", "!selectedLocalFiles.value.has(file)")
content = content.replace("selectedVaultFiles.size === 0s.value.has(file)", "!selectedVaultFiles.value.has(file)")

# Replace handleCopyFromVault
old_copy_from = """const handleCopyFromVault = async (fileToDecrypt = selectedVaultFile.value) => {
  if (!fileToDecrypt || !currentLocalDir.value) return;
  
  if (fileToDecrypt.isDirectory) {
    processingStatus.value = `Đang giải mã thư mục ảo: ${fileToDecrypt.name}...`;
    const prefix = currentVaultDir.value === '/' ? fileToDecrypt.name + '/' : currentVaultDir.value + '/' + fileToDecrypt.name + '/';
    const exactEmpty = currentVaultDir.value === '/' ? fileToDecrypt.name + '/.k3empty' : currentVaultDir.value + '/' + fileToDecrypt.name + '/.k3empty';
    let hasError = false;
    
    for (const f of allVaultFiles.value) {
      const orig = f.originalName || f.name;
      if (orig.startsWith(prefix) || orig === exactEmpty) {
        let relativeStructure = orig;
        if (currentVaultDir.value !== '/') {
          relativeStructure = orig.substring(currentVaultDir.value.length + 1);
        }
        const res = await api.decryptFile(f.name, currentLocalDir.value, relativeStructure);
        if (!res.success) {
          hasError = true;
          console.error("Decrypt error on file " + f.name + ":", res.error);
          (window as any).showToast('Lỗi giải mã file ' + f.name + ': ' + res.error, 'error');
        }
      }
    }
    processingStatus.value = '';
    if (!hasError) await openLocalDir(currentLocalDir.value);
    return;
  }
  
  processingStatus.value = `Đang giải mã sang máy tính: ${fileToDecrypt.name}...`;
  const res = await api.decryptFile(fileToDecrypt.name, currentLocalDir.value);
  processingStatus.value = '';
  progressData.value = null;
  if (res.success) {
    await openLocalDir(currentLocalDir.value);
  } else {
    (window as any).showToast('Lỗi giải mã: ' + res.error, 'error');
  }
};"""

new_copy_from = """const handleCopyFromVault = async () => {
  const filesToDecrypt = Array.from(selectedVaultFiles.value);
  if (filesToDecrypt.length === 0 || !currentLocalDir.value) return;
  
  let successCount = 0;
  for (let i = 0; i < filesToDecrypt.length; i++) {
    const file = filesToDecrypt[i];
    processingStatus.value = `Đang giải mã [${i+1}/${filesToDecrypt.length}]: ${file.name || file.displayName}`;
    
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
          const res = await api.decryptFile(f.name, currentLocalDir.value, relativeStructure);
          if (!res.success) {
            hasError = true;
            (window as any).showToast('Lỗi giải mã file ' + f.name + ': ' + res.error, 'error');
          }
        }
      }
      if (!hasError) successCount++;
    } else {
      const res = await api.decryptFile(file.name, currentLocalDir.value);
      if (res.success) successCount++;
      else (window as any).showToast('Lỗi giải mã: ' + res.error, 'error');
    }
  }
  
  processingStatus.value = '';
  progressData.value = null;
  (window as any).showToast(`Đã giải mã thành công ${successCount}/${filesToDecrypt.length} tệp/thư mục.`, 'success');
  await openLocalDir(currentLocalDir.value);
};"""
content = content.replace(old_copy_from, new_copy_from)

with open('src/components/FileManager.vue', 'w') as f:
    f.write(content)
