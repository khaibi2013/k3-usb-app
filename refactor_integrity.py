import re

with open('src/components/FileManager.vue', 'r') as f:
    content = f.read()

# Add button
button_old = """            <button class="icon-btn-small" @click="menuAction('new-vault-folder')" title="Tạo thư mục mới"><Folder :size="16" /></button>
            <button class="icon-btn-small text-danger" @click="handleDeleteVault()" :disabled="selectedVaultFiles.size === 0"><Trash2 :size="16" /></button>
            <button class="icon-btn-small" @click="loadVaultFiles"><RefreshCw :size="16" :class="{ 'spin': loadingVault }" /></button>"""

button_new = """            <button class="icon-btn-small" @click="checkVaultIntegrity" title="Quét Toàn Vẹn Két Sắt"><ShieldCheck :size="16" color="var(--accent)" /></button>
            <button class="icon-btn-small" @click="menuAction('new-vault-folder')" title="Tạo thư mục mới"><Folder :size="16" /></button>
            <button class="icon-btn-small text-danger" @click="handleDeleteVault()" :disabled="selectedVaultFiles.size === 0"><Trash2 :size="16" /></button>
            <button class="icon-btn-small" @click="loadVaultFiles"><RefreshCw :size="16" :class="{ 'spin': loadingVault }" /></button>"""
content = content.replace(button_old, button_new)


# Add function checkVaultIntegrity
func_code = """const checkVaultIntegrity = async () => {
  processingStatus.value = `Đang quét toàn vẹn Két sắt...`;
  scanStatus.value = 'scanning';
  const res = await api.checkVaultIntegrity();
  processingStatus.value = '';
  
  if (!res.success) {
    scanStatus.value = 'danger';
    await openConfirm('Lỗi khi quét: ' + res.error);
    scanStatus.value = '';
    return;
  }
  
  if (res.corruptedFiles && res.corruptedFiles.length > 0) {
    scanStatus.value = 'danger';
    await openConfirm(`CẢNH BÁO: Phát hiện ${res.corruptedFiles.length} tệp bị hỏng hoặc bị sửa đổi trái phép trong tổng số ${res.totalFiles} tệp! Vui lòng kiểm tra lại ổ đĩa USB.`);
  } else {
    scanStatus.value = 'safe';
    await openConfirm(`Hoàn tất: Két sắt an toàn tuyệt đối. Đã quét ${res.totalFiles} tệp và không phát hiện bất thường nào.`);
  }
  scanStatus.value = '';
};"""

script_end = "</script>"
content = content.replace(script_end, func_code + "\n</script>")

with open('src/components/FileManager.vue', 'w') as f:
    f.write(content)
