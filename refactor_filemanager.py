import re

with open('src/components/FileManager.vue', 'r') as f:
    content = f.read()

# Replace states
content = content.replace('const selectedLocalFile = ref<any>(null);', 'const selectedLocalFiles = ref<Set<any>>(new Set());')
content = content.replace('const selectedVaultFile = ref<any>(null);', 'const selectedVaultFiles = ref<Set<any>>(new Set());')

# Replace navigation clears
content = content.replace('selectedVaultFile.value = null;', 'selectedVaultFiles.value.clear();')
content = content.replace('selectedLocalFile.value = null;', 'selectedLocalFiles.value.clear();')

# Replace context menu
content = content.replace('''  if (isLocal) selectedLocalFile.value = file;
  else selectedVaultFile.value = file;''', '''  if (isLocal) {
    if (!selectedLocalFiles.value.has(file)) {
      selectedLocalFiles.value.clear();
      selectedLocalFiles.value.add(file);
    }
  } else {
    if (!selectedVaultFiles.value.has(file)) {
      selectedVaultFiles.value.clear();
      selectedVaultFiles.value.add(file);
    }
  }''')

with open('src/components/FileManager.vue', 'w') as f:
    f.write(content)

