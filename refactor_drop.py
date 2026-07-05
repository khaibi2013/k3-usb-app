import re

with open('src/components/FileManager.vue', 'r') as f:
    content = f.read()

# Add drop handler to vault pane
vault_pane = """<div class="pane glass-panel">
        <div class="pane-header flex-between">"""

new_vault_pane = """<div class="pane glass-panel" @drop.prevent="handleDropVault" @dragover.prevent>
        <div class="pane-header flex-between">"""

content = content.replace(vault_pane, new_vault_pane)

# Add handler function
handler_code = """const handleDropVault = async (e: DragEvent) => {
  if (!e.dataTransfer || !e.dataTransfer.files) return;
  const files = Array.from(e.dataTransfer.files);
  if (files.length === 0) return;
  
  processingStatus.value = `Đang xử lý ${files.length} tệp tin...`;
  for (const f of files) {
    if (enableVirusScan.value) {
      scanStatus.value = 'scanning';
      const isSafe = await api.scanVirus(f.path);
      if (!isSafe) {
        scanStatus.value = 'danger';
        const confirmed = await openConfirm(`Phát hiện mã độc trong ${f.name}! Xóa tệp này ngay lập tức?`, true);
        if (confirmed) await api.deleteLocalFile(f.path, true);
        continue;
      }
      scanStatus.value = 'safe';
    }
    
    await api.encryptFile(f.path, deleteAfterEncrypt.value, currentVaultDir.value);
  }
  
  processingStatus.value = '';
  scanStatus.value = '';
  progressData.value = null;
  (window as any).showToast('Đã mã hóa thành công.', 'success');
  if (deleteAfterEncrypt.value && currentLocalDir.value) await openLocalDir(currentLocalDir.value);
  await loadVaultFiles();
};"""

# Insert the handler function before the end of the script
script_end = "</script>"
content = content.replace(script_end, handler_code + "\n</script>")

with open('src/components/FileManager.vue', 'w') as f:
    f.write(content)
