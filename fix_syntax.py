with open('electron/main.ts', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# delete lines 1348 to 1359 (0-indexed 1347 to 1358)
del lines[1347:1359]

with open('electron/main.ts', 'w', encoding='utf-8') as f:
    f.writelines(lines)
