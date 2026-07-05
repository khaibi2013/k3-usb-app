import re

with open('src/components/FileManager.vue', 'r') as f:
    content = f.read()

# Replace handleCopyToVault
old_copy_to = """const handleCopyToVault = async (fileToEncrypt = selectedLocalFile.value) => {
  if (!fileToEncrypt) return;
  
  if (enableVirusScan.value) {
    scanStatus.value = 'scanning';
    processingStatus.value = `Đang quét virus: ${fileToEncrypt.name}...`;
    const scanResult = await api.scanFile(fileToEncrypt.path);
    
    if (!scanResult.safe) {
      scanStatus.value = 'danger';
      processingStatus.value = `PHÁT HIỆN MÃ ĐỘC: ${scanResult.threat}! Hủy bỏ sao chép.`;
      setTimeout(() => { processingStatus.value = ''; scanStatus.value = null; }, 4000);
      return;
    }
  }
  
  scanStatus.value = 'safe';
  processingStatus.value = deleteAfterEncrypt.value ? `Đang mã hóa & Xóa tệp gốc...` : `Đang mã hóa dữ liệu...`;
  await api.encryptFile(fileToEncrypt.path, deleteAfterEncrypt.value, currentVaultDir.value); 
  processingStatus.value = '';
  progressData.value = null;
  scanStatus.value = null;
  await loadVaultFiles();
  await openLocalDir(currentLocalDir.value);
};"""

new_copy_to = """const handleCopyToVault = async () => {
  const filesToEncrypt = Array.from(selectedLocalFiles.value);
  if (filesToEncrypt.length === 0) return;
  
  let successCount = 0;
  for (let i = 0; i < filesToEncrypt.length; i++) {
    const file = filesToEncrypt[i];
    
    if (enableVirusScan.value) {
      scanStatus.value = 'scanning';
      processingStatus.value = `Đang quét virus [${i+1}/${filesToEncrypt.length}]: ${file.name}...`;
      const scanResult = await api.scanFile(file.path);
      
      if (!scanResult.safe) {
        scanStatus.value = 'danger';
        processingStatus.value = `PHÁT HIỆN MÃ ĐỘC: ${scanResult.threat}! Hủy bỏ.`;
        setTimeout(() => { processingStatus.value = ''; scanStatus.value = null; }, 4000);
        return;
      }
    }
    
    scanStatus.value = 'safe';
    processingStatus.value = deleteAfterEncrypt.value ? `Đang mã hóa & Xóa tệp gốc [${i+1}/${filesToEncrypt.length}]: ${file.name}` : `Đang mã hóa [${i+1}/${filesToEncrypt.length}]: ${file.name}`;
    const res = await api.encryptFile(file.path, deleteAfterEncrypt.value, currentVaultDir.value); 
    if (!res || !res.success) {
       if (res && res.error) (window as any).showToast(`Lỗi mã hoá ${file.name}: ${res.error}`, 'error');
    } else {
       successCount++;
    }
  }
  
  processingStatus.value = '';
  progressData.value = null;
  scanStatus.value = null;
  (window as any).showToast(`Đã mã hoá thành công ${successCount}/${filesToEncrypt.length} tệp.`, 'success');
  await loadVaultFiles();
  await openLocalDir(currentLocalDir.value);
};"""
content = content.replace(old_copy_to, new_copy_to)

with open('src/components/FileManager.vue', 'w') as f:
    f.write(content)
