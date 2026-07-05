import re

with open('electron/preload.ts', 'r') as f:
    preload = f.read()

with open('electron/main.ts', 'r') as f:
    main_ts = f.read()

preload_invokes = re.findall(r"ipcRenderer\.invoke\('([^']+)'", preload)
main_handles = re.findall(r"ipcMain\.handle\('([^']+)'", main_ts)

missing_in_main = set(preload_invokes) - set(main_handles)
missing_in_preload = set(main_handles) - set(preload_invokes)

print("Invoked in preload but MISSING in main:", missing_in_main)
print("Handled in main but MISSING in preload:", missing_in_preload)
