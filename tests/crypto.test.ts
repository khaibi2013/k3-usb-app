import { describe, it, expect } from 'vitest';
import crypto from 'crypto';

const ALGORITHM = 'aes-256-gcm';

// Giả lập logic mã hóa trong main.ts
function encryptBuffer(data: Buffer, key: Buffer, originalNameFull: string) {
  const iv = crypto.randomBytes(16);
  const cipher = crypto.createCipheriv(ALGORITHM, key, iv);
  const encrypted = Buffer.concat([cipher.update(data), cipher.final()]);
  const authTag = cipher.getAuthTag();
  
  const nameBuffer = Buffer.from(originalNameFull, 'utf8');
  const nameLen = Buffer.alloc(4);
  nameLen.writeUInt32LE(nameBuffer.length, 0);
  
  return Buffer.concat([iv, authTag, nameLen, nameBuffer, encrypted]);
}

function decryptBuffer(encryptedData: Buffer, key: Buffer) {
  const iv = encryptedData.subarray(0, 16);
  const authTag = encryptedData.subarray(16, 32);
  const nameLen = encryptedData.readUInt32LE(32);
  const originalNameFull = encryptedData.subarray(36, 36 + nameLen).toString('utf8');
  
  const payload = encryptedData.subarray(36 + nameLen);
  const decipher = crypto.createDecipheriv(ALGORITHM, key, iv);
  decipher.setAuthTag(authTag);
  
  const decrypted = Buffer.concat([decipher.update(payload), decipher.final()]);
  return { decrypted, originalNameFull };
}

describe('Thuật toán Mã hóa & Giải mã IronVault', () => {
  it('Phải mã hóa và giải mã dữ liệu chính xác 100%', () => {
    const key = crypto.randomBytes(32);
    const originalData = Buffer.from('Dữ liệu siêu bí mật của IronVault 2026!');
    const originalName = 'bí-mật/tài-liệu.txt';
    
    // Mã hóa
    const encrypted = encryptBuffer(originalData, key, originalName);
    
    // Đảm bảo dữ liệu đã bị biến đổi
    expect(encrypted.toString()).not.toContain('Dữ liệu siêu bí mật');
    
    // Giải mã
    const result = decryptBuffer(encrypted, key);
    
    // Đảm bảo dữ liệu nguyên vẹn
    expect(result.decrypted.toString()).toBe('Dữ liệu siêu bí mật của IronVault 2026!');
    expect(result.originalNameFull).toBe('bí-mật/tài-liệu.txt');
  });

  it('Phải từ chối giải mã nếu sai mật khẩu (Sai Key)', () => {
    const key = crypto.randomBytes(32);
    const wrongKey = crypto.randomBytes(32);
    const originalData = Buffer.from('Dữ liệu siêu bí mật');
    
    const encrypted = encryptBuffer(originalData, key, 'test.txt');
    
    // Cố gắng giải mã bằng wrongKey sẽ văng lỗi auth tag hoặc decipher
    expect(() => {
      decryptBuffer(encrypted, wrongKey);
    }).toThrow();
  });
});
