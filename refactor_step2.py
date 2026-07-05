import re

with open('src/components/FileManager.vue', 'r') as f:
    content = f.read()

# Replace template handlers
content = content.replace('@click="selectedLocalFile = f"', '@click="handleLocalClick($event, f)"')
content = content.replace('@click="selectedVaultFile = f"', '@click="handleVaultClick($event, f)"')

content = content.replace('!selectedLocalFile', 'selectedLocalFiles.size === 0')
content = content.replace('!selectedVaultFile', 'selectedVaultFiles.size === 0')

content = content.replace('selectedLocalFile?.path === f.path', 'selectedLocalFiles.has(f)')
content = content.replace('selectedVaultFile?.name === f.name', 'selectedVaultFiles.has(f)')

# Add handleLocalClick and handleVaultClick
click_handlers = """
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
"""

content = content.replace('// Context Menu Logic', click_handlers + '\n// Context Menu Logic')

with open('src/components/FileManager.vue', 'w') as f:
    f.write(content)
