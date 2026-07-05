import re

with open('src/components/FileManager.vue', 'r') as f:
    vue = f.read()

print("showHidden usage:", "globalSettings.value.showHidden" in vue or "showHidden" in vue)
print("autoClearHistory usage:", "autoClearHistory" in vue)
print("cleanSystemJunk usage:", "cleanSystemJunk" in vue)
print("scanFile usage:", "api.scanFile" in vue)
print("createFolder usage:", "api.createFolder" in vue)
print("delete usage:", "api.delete" in vue)
print("rename usage:", "api.rename" in vue)
