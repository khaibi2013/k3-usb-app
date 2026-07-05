import re

with open('electron/main.ts', 'r', encoding='utf-8') as f:
    content = f.read()

pattern = r"ipcMain\.handle\('decrypt-file', async \(event, fileName: string, targetDir\?: string, relativeStructure\?: string\) => \{.*?\n\}\);\n"

new_code = """ipcMain.handle('decrypt-file', async (event, fileName: string, targetDir?: string, relativeStructure?: string) => {
    if (!currentKey) throw new Error('Not authenticated');
    try {
        const filePath = path.join(getVaultPath(), fileName);
        const fh = await fs.open(filePath, 'r');
        const headerBuf = Buffer.alloc(4096);
        const { bytesRead } = await fh.read(headerBuf, 0, 4096, 0);
        await fh.close();
        
        if (bytesRead < 36) throw new Error('Invalid file format');
        
        const iv = headerBuf.subarray(0, 16);
        const authTag = headerBuf.subarray(16, 32);
        
        let isCompressed = 0;
        let dataOffset = 36;
        
        const possibleFlag = headerBuf.readUInt8(32);
        let nameLen = 0;
        
        if (possibleFlag === 0 || possibleFlag === 1 || possibleFlag === 2) {
            const possibleNameLen = headerBuf.readUInt32LE(33);
            if (possibleNameLen > 0 && possibleNameLen < 5000) {
                isCompressed = possibleFlag;
                dataOffset = 37;
                nameLen = possibleNameLen;
            } else {
                nameLen = headerBuf.readUInt32LE(32);
            }
        } else {
            nameLen = headerBuf.readUInt32LE(32);
        }

        const originalNameFull = headerBuf.subarray(dataOffset, dataOffset + nameLen).toString('utf8');
        const originalName = originalNameFull.split('/').pop() || originalNameFull;
        event.sender.send('encryption-progress', { file: originalNameFull, status: isCompressed === 2 ? 'copying raw' : 'decrypting' });
        
        const outputName = relativeStructure ? relativeStructure : originalName;
        
        if (originalNameFull.endsWith('.k3empty')) {
            const folderName = relativeStructure ? relativeStructure.replace(/\.k3empty$/, '') : (originalNameFull.split('/').slice(-2, -1)[0] || 'New Folder');
            const outPath = targetDir ? path.join(targetDir, folderName) : path.join(path.dirname(getVaultPath()), 'Decrypted', folderName);
            await fs.mkdir(outPath, { recursive: true });
            return { success: true };
        }
        
        let outPath = targetDir ? path.join(targetDir, outputName) : path.join(path.dirname(getVaultPath()), 'Decrypted', outputName);
        await fs.mkdir(path.dirname(outPath), { recursive: true });
        
        const payloadOffset = dataOffset + nameLen;
        
        if (isCompressed === 2) {
            // RAW STREAM MODE
            await new Promise<void>((resolve, reject) => {
                // need standard fs module for createReadStream, we imported fs from 'fs/promises'
                // let's just use require('fs') to avoid import conflicts
                const fsSync = require('fs');
                const readStream = fsSync.createReadStream(filePath, { start: payloadOffset });
                const writeStream = fsSync.createWriteStream(outPath);
                readStream.pipe(writeStream);
                readStream.on('error', reject);
                writeStream.on('error', reject);
                writeStream.on('finish', resolve);
            });
            return { success: true };
        }
        
        // NORMAL DECRYPTION MODE
        const data = await fs.readFile(filePath);
        const encryptedData = data.subarray(payloadOffset);
        
        const decipher = crypto.createDecipheriv(ALGORITHM, currentKey, iv);
        decipher.setAuthTag(authTag);
        let decrypted = Buffer.concat([decipher.update(encryptedData), decipher.final()]);
        
        if (isCompressed === 1) {
            try { decrypted = zlib.gunzipSync(decrypted); } catch (e) { console.error('Gunzip failed', e); }
        }
        
        if (outputName.endsWith('.k3dir')) {
            outPath = outPath.replace('.k3dir', '');
            await fs.mkdir(outPath, { recursive: true });
            const tempZip = path.join(os.tmpdir(), `k3_${Date.now()}.zip`);
            await fs.writeFile(tempZip, decrypted);
            const zip = new AdmZip(tempZip);
            await new Promise<void>((resolve, reject) => zip.extractAllToAsync(outPath, true, false, (err) => err ? reject(err) : resolve()));
            await fs.unlink(tempZip);
        } else {
            await fs.writeFile(outPath, decrypted);
        }
        
        return { success: true };
    } catch (e: any) { return { success: false, error: e.message }; }
});\n"""

content = re.sub(pattern, new_code, content, flags=re.DOTALL)
with open('electron/main.ts', 'w', encoding='utf-8') as f:
    f.write(content)
