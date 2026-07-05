import re

with open('src/components/FileManager.vue', 'r') as f:
    content = f.read()

old_delete = """const handleDeleteVault = async (file = selectedVaultFile.value) => {
  if (!file) return;
  const confirmed = await openConfirm('Xóa vĩnh viễn tệp/thư mục này khỏi Két sắt?', true);
  if (confirmed) {
    if (file.isDirectory) {
      // Find all files that start with this folder's prefix and delete them
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
    await loadVaultFiles();
  }
};"""

new_delete = """const handleDeleteVault = async () => {
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
};"""
content = content.replace(old_delete, new_delete)

with open('src/components/FileManager.vue', 'w') as f:
    f.write(content)
