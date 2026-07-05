import re

with open('electron/main.ts', 'r') as f:
    content = f.read()

# 1. Update export-zip
export_zip_old = """                    const data = await fs.readFile(path.join(getVaultPath(), fileObj.fileName));
                    const iv = data.subarray(0, 16);
                    const authTag = data.subarray(16, 32);
                    const nameLen = data.readUInt32LE(32);
                    const originalNameFull = data.subarray(36, 36 + nameLen).toString('utf8');
                    const originalName = originalNameFull.split('/').pop() || originalNameFull;
                    
                    const encryptedData = data.subarray(36 + nameLen);
                    const decipher = crypto.createDecipheriv(ALGORITHM, currentKey, iv);
                    decipher.setAuthTag(authTag);
                    const decrypted = Buffer.concat([decipher.update(encryptedData), decipher.final()]);"""

export_zip_new = """                    const data = await fs.readFile(path.join(getVaultPath(), fileObj.fileName));
                    const iv = data.subarray(0, 16);
                    const authTag = data.subarray(16, 32);
                    
                    let isCompressed = 0;
                    let nameLenOffset = 32;
                    let dataOffset = 36;
                    
                    const possibleFlag = data.readUInt8(32);
                    let nameLen = 0;
                    
                    if (possibleFlag === 0 || possibleFlag === 1) {
                        const possibleNameLen = data.readUInt32LE(33);
                        if (possibleNameLen > 0 && possibleNameLen < 5000 && 37 + possibleNameLen <= data.length) {
                            isCompressed = possibleFlag;
                            nameLenOffset = 33;
                            dataOffset = 37;
                            nameLen = possibleNameLen;
                        } else {
                            nameLen = data.readUInt32LE(32);
                        }
                    } else {
                        nameLen = data.readUInt32LE(32);
                    }
                    
                    const originalNameFull = data.subarray(dataOffset, dataOffset + nameLen).toString('utf8');
                    const originalName = originalNameFull.split('/').pop() || originalNameFull;
                    
                    const encryptedData = data.subarray(dataOffset + nameLen);
                    const decipher = crypto.createDecipheriv(ALGORITHM, currentKey, iv);
                    decipher.setAuthTag(authTag);
                    let decrypted = Buffer.concat([decipher.update(encryptedData), decipher.final()]);
                    if (isCompressed === 1) {
                        try { decrypted = zlib.gunzipSync(decrypted); } catch(e) {}
                    }"""
content = content.replace(export_zip_old, export_zip_new)

# 2. Update create-vault-folder
create_vault_folder_old = """        const iv = crypto.randomBytes(16);
        const cipher = crypto.createCipheriv(ALGORITHM, currentKey, iv);
        const encrypted = Buffer.concat([cipher.update(Buffer.from('')), cipher.final()]);
        const authTag = cipher.getAuthTag();
        
        const nameBuffer = Buffer.from(virtualPath, 'utf8');
        const nameLen = Buffer.alloc(4);
        nameLen.writeUInt32LE(nameBuffer.length, 0);
        
        const finalData = Buffer.concat([iv, authTag, nameLen, nameBuffer, encrypted]);"""

create_vault_folder_new = """        const iv = crypto.randomBytes(16);
        const cipher = crypto.createCipheriv(ALGORITHM, currentKey, iv);
        const encrypted = Buffer.concat([cipher.update(Buffer.from('')), cipher.final()]);
        const authTag = cipher.getAuthTag();
        
        const nameBuffer = Buffer.from(virtualPath, 'utf8');
        const nameLen = Buffer.alloc(4);
        nameLen.writeUInt32LE(nameBuffer.length, 0);
        
        const isCompressed = Buffer.alloc(1);
        isCompressed.writeUInt8(0, 0);
        
        const finalData = Buffer.concat([iv, authTag, isCompressed, nameLen, nameBuffer, encrypted]);"""

content = content.replace(create_vault_folder_old, create_vault_folder_new)

with open('electron/main.ts', 'w') as f:
    f.write(content)
