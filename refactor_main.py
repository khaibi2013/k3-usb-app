import re

with open('electron/main.ts', 'r') as f:
    content = f.read()

# Add zlib import if not exists
if 'import * as zlib from \'zlib\';' not in content:
    content = content.replace("import * as crypto from 'crypto';", "import * as crypto from 'crypto';\nimport * as zlib from 'zlib';")

# 1. Refactor processEncryption
process_encryption_regex = r"async function processEncryption\(event: any, sourcePath: string, secureDelete: boolean, vaultPath: string\) \{[\s\S]*?async \(_\, deviceIds"
# Actually, let's just replace the blocks directly to avoid massive regex failures.

process_encryption_old = """async function processEncryption(event: any, sourcePath: string, secureDelete: boolean, vaultPath: string) {
    const cleanVaultPath = vaultPath.replace(/^\\/+|^\\/+$/g, '');
    const baseName = path.basename(sourcePath);
    const virtualBasePath = cleanVaultPath ? `${cleanVaultPath}/${baseName}` : baseName;

    const stat = await fs.stat(sourcePath);
    if (stat.isDirectory()) {
        // Create an explicit virtual folder entry just to ensure empty folders exist
        const iv = crypto.randomBytes(16);
        const cipher = crypto.createCipheriv(ALGORITHM, currentKey!, iv);
        const encrypted = Buffer.concat([cipher.update(Buffer.from('')), cipher.final()]);
        const authTag = cipher.getAuthTag();
        
        const nameBuffer = Buffer.from(`${virtualBasePath}/.k3empty`, 'utf8');
        const nameLen = Buffer.alloc(4);
        nameLen.writeUInt32LE(nameBuffer.length, 0);
        
        const finalData = Buffer.concat([iv, authTag, nameLen, nameBuffer, encrypted]);
        await fs.writeFile(path.join(getVaultPath(), `${crypto.randomBytes(8).toString('hex')}.k3enc`), finalData);
        
        const items = await fs.readdir(sourcePath);
        for (const item of items) {
            await processEncryption(event, path.join(sourcePath, item), false, virtualBasePath);
        }
        if (secureDelete) await shredFolder(sourcePath);
    } else {
        event.sender.send('encryption-progress', { file: virtualBasePath, size: stat.size, status: 'encrypting' });
        const fileData = await fs.readFile(sourcePath);
        const iv = crypto.randomBytes(16);
        const cipher = crypto.createCipheriv(ALGORITHM, currentKey!, iv);
        const encrypted = Buffer.concat([cipher.update(fileData), cipher.final()]);
        const authTag = cipher.getAuthTag();
        
        const nameBuffer = Buffer.from(virtualBasePath, 'utf8');
        const nameLen = Buffer.alloc(4);
        nameLen.writeUInt32LE(nameBuffer.length, 0);
        
        const finalData = Buffer.concat([iv, authTag, nameLen, nameBuffer, encrypted]);
        await fs.writeFile(path.join(getVaultPath(), `${crypto.randomBytes(8).toString('hex')}.k3enc`), finalData);
        if (secureDelete) await shredFile(sourcePath);
    }
}"""

process_encryption_new = """async function processEncryption(event: any, sourcePath: string, secureDelete: boolean, vaultPath: string) {
    const cleanVaultPath = vaultPath.replace(/^\\/+|^\\/+$/g, '');
    const baseName = path.basename(sourcePath);
    const virtualBasePath = cleanVaultPath ? `${cleanVaultPath}/${baseName}` : baseName;

    const stat = await fs.stat(sourcePath);
    if (stat.isDirectory()) {
        const iv = crypto.randomBytes(16);
        const cipher = crypto.createCipheriv(ALGORITHM, currentKey!, iv);
        const encrypted = Buffer.concat([cipher.update(Buffer.from('')), cipher.final()]);
        const authTag = cipher.getAuthTag();
        
        const nameBuffer = Buffer.from(`${virtualBasePath}/.k3empty`, 'utf8');
        const nameLen = Buffer.alloc(4);
        nameLen.writeUInt32LE(nameBuffer.length, 0);
        
        const isCompressed = Buffer.alloc(1);
        isCompressed.writeUInt8(0, 0); // 0 = false

        const finalData = Buffer.concat([iv, authTag, isCompressed, nameLen, nameBuffer, encrypted]);
        await fs.writeFile(path.join(getVaultPath(), `${crypto.randomBytes(8).toString('hex')}.k3enc`), finalData);
        
        const items = await fs.readdir(sourcePath);
        for (const item of items) {
            await processEncryption(event, path.join(sourcePath, item), false, virtualBasePath);
        }
        if (secureDelete) await shredFolder(sourcePath);
    } else {
        event.sender.send('encryption-progress', { file: virtualBasePath, size: stat.size, status: 'encrypting' });
        const fileData = await fs.readFile(sourcePath);
        
        // Smart compression for eligible files to save USB space
        let dataToEncrypt = fileData;
        let compressedFlag = 0;
        
        // Exclude formats that are already compressed (jpg, zip, mp4, etc.)
        const ext = path.extname(sourcePath).toLowerCase();
        const noCompressExts = ['.zip', '.rar', '.7z', '.mp4', '.mkv', '.jpg', '.jpeg', '.png', '.mp3'];
        if (!noCompressExts.includes(ext) && fileData.length > 1024) { // Only compress > 1KB
            dataToEncrypt = zlib.gzipSync(fileData);
            compressedFlag = 1;
        }

        const iv = crypto.randomBytes(16);
        const cipher = crypto.createCipheriv(ALGORITHM, currentKey!, iv);
        const encrypted = Buffer.concat([cipher.update(dataToEncrypt), cipher.final()]);
        const authTag = cipher.getAuthTag();
        
        const nameBuffer = Buffer.from(virtualBasePath, 'utf8');
        const nameLen = Buffer.alloc(4);
        nameLen.writeUInt32LE(nameBuffer.length, 0);
        
        const isCompressed = Buffer.alloc(1);
        isCompressed.writeUInt8(compressedFlag, 0);

        const finalData = Buffer.concat([iv, authTag, isCompressed, nameLen, nameBuffer, encrypted]);
        await fs.writeFile(path.join(getVaultPath(), `${crypto.randomBytes(8).toString('hex')}.k3enc`), finalData);
        if (secureDelete) await shredFile(sourcePath);
    }
}"""

content = content.replace(process_encryption_old, process_encryption_new)

# 2. Refactor decrypt-file
decrypt_old = """        const data = await fs.readFile(path.join(getVaultPath(), fileName));
        const iv = data.subarray(0, 16);
        const authTag = data.subarray(16, 32);
        const nameLen = data.readUInt32LE(32);
        const originalNameFull = data.subarray(36, 36 + nameLen).toString('utf8');
        const originalName = originalNameFull.split('/').pop() || originalNameFull;
        event.sender.send('encryption-progress', { file: originalNameFull, status: 'decrypting' });
        const encryptedData = data.subarray(36 + nameLen);
        const decipher = crypto.createDecipheriv(ALGORITHM, currentKey, iv);
        decipher.setAuthTag(authTag);
        const decrypted = Buffer.concat([decipher.update(encryptedData), decipher.final()]);"""

decrypt_new = """        const data = await fs.readFile(path.join(getVaultPath(), fileName));
        const iv = data.subarray(0, 16);
        const authTag = data.subarray(16, 32);
        
        // Support backward compatibility
        let isCompressed = 0;
        let nameLenOffset = 32;
        let dataOffset = 36;
        
        // If byte at 32 is 0 or 1, and nameLen makes sense, it might be the compressed flag
        // However, nameLen in old format is readUInt32LE(32). If it's a very long string, byte 35 could be > 0.
        // Actually, let's just check if the new file format applies. 
        // We added 1 byte, so the nameLen offset moved to 33.
        const possibleFlag = data.readUInt8(32);
        let nameLen = 0;
        
        if (possibleFlag === 0 || possibleFlag === 1) {
            // Check if nameLen at 33 makes sense (should be less than 2000 chars usually)
            const possibleNameLen = data.readUInt32LE(33);
            if (possibleNameLen > 0 && possibleNameLen < 5000 && 37 + possibleNameLen <= data.length) {
                // It is the new format
                isCompressed = possibleFlag;
                nameLenOffset = 33;
                dataOffset = 37;
                nameLen = possibleNameLen;
            } else {
                // Old format
                nameLen = data.readUInt32LE(32);
            }
        } else {
            // Old format
            nameLen = data.readUInt32LE(32);
        }

        const originalNameFull = data.subarray(dataOffset, dataOffset + nameLen).toString('utf8');
        const originalName = originalNameFull.split('/').pop() || originalNameFull;
        event.sender.send('encryption-progress', { file: originalNameFull, status: 'decrypting' });
        const encryptedData = data.subarray(dataOffset + nameLen);
        
        const decipher = crypto.createDecipheriv(ALGORITHM, currentKey, iv);
        decipher.setAuthTag(authTag);
        let decrypted = Buffer.concat([decipher.update(encryptedData), decipher.final()]);
        
        if (isCompressed === 1) {
            try { decrypted = zlib.gunzipSync(decrypted); } catch (e) { console.error('Gunzip failed', e); }
        }"""

content = content.replace(decrypt_old, decrypt_new)


# Also fix list-vault-files metadata parsing
list_vault_old = """                const data = await fs.readFile(p);
                const nameLen = data.readUInt32LE(32);
                const originalNameFull = data.subarray(36, 36 + nameLen).toString('utf8');"""

list_vault_new = """                const data = await fs.readFile(p);
                const possibleFlag = data.readUInt8(32);
                let nameLen = 0;
                let dataOffset = 36;
                if (possibleFlag === 0 || possibleFlag === 1) {
                    const possibleNameLen = data.readUInt32LE(33);
                    if (possibleNameLen > 0 && possibleNameLen < 5000 && 37 + possibleNameLen <= data.length) {
                        nameLen = possibleNameLen;
                        dataOffset = 37;
                    } else { nameLen = data.readUInt32LE(32); }
                } else { nameLen = data.readUInt32LE(32); }
                
                const originalNameFull = data.subarray(dataOffset, dataOffset + nameLen).toString('utf8');"""

content = content.replace(list_vault_old, list_vault_new)

# 3. Add IPC Check Vault Integrity
integrity_ipc = """ipcMain.handle('check-vault-integrity', async (event) => {
    if (!currentKey) return { success: false, error: 'Not authenticated' };
    try {
        const vaultDir = getVaultPath();
        const files = await fs.readdir(vaultDir);
        let corruptedFiles = [];
        let totalFiles = 0;
        
        for (const f of files) {
            if (!f.endsWith('.k3enc')) continue;
            totalFiles++;
            const p = path.join(vaultDir, f);
            const data = await fs.readFile(p);
            
            try {
                const iv = data.subarray(0, 16);
                const authTag = data.subarray(16, 32);
                
                const possibleFlag = data.readUInt8(32);
                let nameLen = 0;
                let dataOffset = 36;
                if (possibleFlag === 0 || possibleFlag === 1) {
                    const possibleNameLen = data.readUInt32LE(33);
                    if (possibleNameLen > 0 && possibleNameLen < 5000 && 37 + possibleNameLen <= data.length) {
                        nameLen = possibleNameLen;
                        dataOffset = 37;
                    } else { nameLen = data.readUInt32LE(32); }
                } else { nameLen = data.readUInt32LE(32); }
                
                const originalNameFull = data.subarray(dataOffset, dataOffset + nameLen).toString('utf8');
                const encryptedData = data.subarray(dataOffset + nameLen);
                
                const decipher = crypto.createDecipheriv(ALGORITHM, currentKey, iv);
                decipher.setAuthTag(authTag);
                decipher.update(encryptedData);
                decipher.final(); // This will throw if integrity fails
            } catch (err) {
                // If anything throws, file is corrupted
                corruptedFiles.push(f);
            }
        }
        
        return { success: true, corruptedFiles, totalFiles };
    } catch (e: any) {
        return { success: false, error: e.message };
    }
});
"""

# Append integrity_ipc before module.exports or end
content = content + "\n" + integrity_ipc

with open('electron/main.ts', 'w') as f:
    f.write(content)
