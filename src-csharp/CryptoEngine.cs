using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace AnToanUSB
{
    public static class CryptoEngine
    {
        private const int KeySize = 32; // 256 bit
        private const int MacSize = 32; // 256 bit HMAC
        private const int IvSize = 16;  // 128 bit
        private const int SaltSize = 16; // 128 bit
        private const int Iterations = 100000;
        private const int CopyBufferSize = 1024 * 1024;
        private static readonly byte[] CustomMagic = Encoding.ASCII.GetBytes("K3C1");

        private static byte[] currentKey;
        private static byte[] currentMacKey;

        public static void Authenticate(string password)
        {
            byte[] salt = ConfigManager.GetCryptoSaltBytes();

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                currentKey = pbkdf2.GetBytes(KeySize);
                currentMacKey = pbkdf2.GetBytes(MacSize);
            }
        }

        public static bool IsAuthenticated { get { return currentKey != null && currentMacKey != null; } }

        public static void Logout()
        {
            if (currentKey != null) Array.Clear(currentKey, 0, currentKey.Length);
            if (currentMacKey != null) Array.Clear(currentMacKey, 0, currentMacKey.Length);
            currentKey = null;
            currentMacKey = null;
        }

        public static void EncryptFile(string sourceFile, string destFile)
        {
            EncryptFile(sourceFile, destFile, Path.GetFileName(sourceFile));
        }

        public static void EncryptFile(string sourceFile, string destFile, string storedName)
        {
            if (!IsAuthenticated) throw new UnauthorizedAccessException("Not authenticated.");

            byte[] iv = new byte[IvSize];
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(iv);

            byte isCompressed = 0;
            string baseName = string.IsNullOrWhiteSpace(storedName) ? Path.GetFileName(sourceFile) : storedName;
            byte[] nameBytes = Encoding.UTF8.GetBytes(baseName);
            byte[] nameLenBytes = BitConverter.GetBytes(nameBytes.Length);
            bool shouldCompress = !IsLikelyCompressedMedia(sourceFile) && new FileInfo(sourceFile).Length > 1024;
            isCompressed = shouldCompress ? (byte)1 : (byte)0;

            string destDir = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
            string tempPayload = destFile + ".payload." + Guid.NewGuid().ToString("N") + ".tmp";
            string tempFinal = destFile + ".tmp." + Guid.NewGuid().ToString("N");

            try
            {
                using (var payload = new FileStream(tempPayload, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferSize))
                {
                    payload.WriteByte(isCompressed);
                    payload.Write(nameLenBytes, 0, nameLenBytes.Length);
                    payload.Write(nameBytes, 0, nameBytes.Length);

                    using (var aes = new AesManaged { Key = currentKey, IV = iv, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 })
                    using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                    using (var crypto = new CryptoStream(payload, encryptor, CryptoStreamMode.Write))
                    using (var source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize))
                    {
                        if (shouldCompress)
                        {
                            using (var gzip = new GZipStream(crypto, CompressionMode.Compress, true))
                                CopyStream(source, gzip);
                        }
                        else
                        {
                            CopyStream(source, crypto);
                        }

                        crypto.FlushFinalBlock();
                    }
                }

                byte[] mac = ComputeMac(iv, tempPayload);

                using (var output = new FileStream(tempFinal, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferSize))
                {
                    output.Write(iv, 0, iv.Length);
                    output.Write(mac, 0, mac.Length);
                    using (var payload = new FileStream(tempPayload, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize))
                        CopyStream(payload, output);
                }

                if (File.Exists(destFile)) File.Delete(destFile);
                File.Move(tempFinal, destFile);
            }
            finally
            {
                TryDelete(tempPayload);
                TryDelete(tempFinal);
            }
        }

        public static void EncryptFileWithPassword(string sourceFile, string destFile, string password)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("Password is required.");

            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] encKey;
            byte[] macKey;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                encKey = pbkdf2.GetBytes(KeySize);
                macKey = pbkdf2.GetBytes(MacSize);
            }

            byte[] fileData = File.ReadAllBytes(sourceFile);
            byte isCompressed = 0;
            string ext = Path.GetExtension(sourceFile).ToLower();
            bool isMedia = ext == ".mp4" || ext == ".zip" || ext == ".jpg" || ext == ".png" || ext == ".mp3" || ext == ".rar";

            if (!isMedia && fileData.Length > 1024)
            {
                using (var ms = new MemoryStream())
                {
                    using (var gz = new GZipStream(ms, CompressionMode.Compress, true))
                        gz.Write(fileData, 0, fileData.Length);
                    fileData = ms.ToArray();
                    isCompressed = 1;
                }
            }

            byte[] encryptedData;
            using (var aes = new AesManaged { Key = encKey, IV = iv, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 })
            using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    cs.Write(fileData, 0, fileData.Length);
                encryptedData = ms.ToArray();
            }

            string baseName = Path.GetFileName(sourceFile);
            byte[] nameBytes = Encoding.UTF8.GetBytes(baseName);
            byte[] nameLenBytes = BitConverter.GetBytes(nameBytes.Length);

            using (var payloadMs = new MemoryStream())
            {
                payloadMs.WriteByte(isCompressed);
                payloadMs.Write(nameLenBytes, 0, nameLenBytes.Length);
                payloadMs.Write(nameBytes, 0, nameBytes.Length);
                payloadMs.Write(encryptedData, 0, encryptedData.Length);
                byte[] payload = payloadMs.ToArray();

                byte[] mac;
                using (var hmac = new HMACSHA256(macKey))
                using (var macMs = new MemoryStream())
                {
                    macMs.Write(CustomMagic, 0, CustomMagic.Length);
                    macMs.Write(salt, 0, salt.Length);
                    macMs.Write(iv, 0, iv.Length);
                    macMs.Write(payload, 0, payload.Length);
                    mac = hmac.ComputeHash(macMs.ToArray());
                }

                using (var fs = new FileStream(destFile, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(CustomMagic, 0, CustomMagic.Length);
                    fs.Write(salt, 0, salt.Length);
                    fs.Write(iv, 0, iv.Length);
                    fs.Write(mac, 0, mac.Length);
                    fs.Write(payload, 0, payload.Length);
                }
            }
        }

        public static bool IsCustomEncryptedFile(string sourceFile)
        {
            if (!File.Exists(sourceFile) || new FileInfo(sourceFile).Length < CustomMagic.Length) return false;
            byte[] magic = new byte[CustomMagic.Length];
            using (var fs = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                fs.Read(magic, 0, magic.Length);
            for (int i = 0; i < CustomMagic.Length; i++)
                if (magic[i] != CustomMagic[i]) return false;
            return true;
        }

        public static void DecryptFileWithPassword(string sourceFile, string destDir, string password)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("Password is required.");

            byte[] allData = File.ReadAllBytes(sourceFile);
            int minSize = CustomMagic.Length + SaltSize + IvSize + MacSize + 1 + 4;
            if (allData.Length < minSize) throw new Exception("Invalid custom encrypted file format.");

            int offset = 0;
            for (int i = 0; i < CustomMagic.Length; i++)
                if (allData[i] != CustomMagic[i]) throw new Exception("Not a custom encrypted K3 file.");
            offset += CustomMagic.Length;

            byte[] salt = new byte[SaltSize];
            Array.Copy(allData, offset, salt, 0, SaltSize);
            offset += SaltSize;

            byte[] iv = new byte[IvSize];
            Array.Copy(allData, offset, iv, 0, IvSize);
            offset += IvSize;

            byte[] mac = new byte[MacSize];
            Array.Copy(allData, offset, mac, 0, MacSize);
            offset += MacSize;

            byte[] payload = new byte[allData.Length - offset];
            Array.Copy(allData, offset, payload, 0, payload.Length);

            byte[] encKey;
            byte[] macKey;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                encKey = pbkdf2.GetBytes(KeySize);
                macKey = pbkdf2.GetBytes(MacSize);
            }

            using (var hmac = new HMACSHA256(macKey))
            using (var macMs = new MemoryStream())
            {
                macMs.Write(CustomMagic, 0, CustomMagic.Length);
                macMs.Write(salt, 0, salt.Length);
                macMs.Write(iv, 0, iv.Length);
                macMs.Write(payload, 0, payload.Length);
                byte[] computed = hmac.ComputeHash(macMs.ToArray());
                int diff = 0;
                for (int i = 0; i < MacSize; i++) diff |= mac[i] ^ computed[i];
                if (diff != 0) throw new Exception("Sai mật khẩu riêng hoặc file đã bị thay đổi.");
            }

            offset = 0;
            byte isCompressed = payload[offset++];
            int nameLen = BitConverter.ToInt32(payload, offset);
            offset += 4;
            if (nameLen < 0 || offset + nameLen > payload.Length) throw new Exception("Invalid metadata.");
            string fileName = Encoding.UTF8.GetString(payload, offset, nameLen);
            offset += nameLen;

            byte[] encData = new byte[payload.Length - offset];
            Array.Copy(payload, offset, encData, 0, encData.Length);

            byte[] decryptedData;
            using (var aes = new AesManaged { Key = encKey, IV = iv, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 })
            using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
            using (var ms = new MemoryStream(encData))
            using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            using (var outMs = new MemoryStream())
            {
                cs.CopyTo(outMs);
                decryptedData = outMs.ToArray();
            }

            if (isCompressed == 1)
            {
                using (var ms = new MemoryStream(decryptedData))
                using (var gz = new GZipStream(ms, CompressionMode.Decompress))
                using (var outMs = new MemoryStream())
                {
                    gz.CopyTo(outMs);
                    decryptedData = outMs.ToArray();
                }
            }

            string customDestFile = SafeDestinationPath(destDir, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(customDestFile));
            File.WriteAllBytes(customDestFile, decryptedData);
        }

        public static void DecryptFile(string sourceFile, string destDir)
        {
            if (!IsAuthenticated) throw new UnauthorizedAccessException("Not authenticated.");

            using (var fs = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize))
            {
                if (fs.Length < IvSize + MacSize + 1 + 4) throw new Exception("Invalid file format.");

                byte[] iv = ReadExact(fs, IvSize);
                byte[] mac = ReadExact(fs, MacSize);

                using (var hmac = new HMACSHA256(currentMacKey))
                {
                    hmac.TransformBlock(iv, 0, iv.Length, null, 0);
                    byte[] buffer = new byte[CopyBufferSize];
                    int read;
                    while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                        hmac.TransformBlock(buffer, 0, read, null, 0);
                    hmac.TransformFinalBlock(new byte[0], 0, 0);

                    int diff = 0;
                    byte[] computedMac = hmac.Hash;
                    for (int i = 0; i < MacSize; i++) diff |= mac[i] ^ computedMac[i];
                    if (diff != 0) throw new Exception("File integrity check failed (MAC mismatch).");
                }

                fs.Position = IvSize + MacSize;
                byte isCompressed = (byte)fs.ReadByte();
                byte[] nameLenBytes = ReadExact(fs, 4);
                int nameLen = BitConverter.ToInt32(nameLenBytes, 0);
                if (nameLen < 0 || nameLen > 1024 * 1024) throw new Exception("Invalid metadata.");
                string fileName = Encoding.UTF8.GetString(ReadExact(fs, nameLen));
                string destFile = SafeDestinationPath(destDir, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                string tempDest = destFile + ".tmp." + Guid.NewGuid().ToString("N");

                try
                {
                    using (var aes = new AesManaged { Key = currentKey, IV = iv, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 })
                    using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                    using (var crypto = new CryptoStream(fs, decryptor, CryptoStreamMode.Read))
                    using (var output = new FileStream(tempDest, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferSize))
                    {
                        if (isCompressed == 1)
                        {
                            using (var gzip = new GZipStream(crypto, CompressionMode.Decompress))
                                CopyStream(gzip, output);
                        }
                        else
                        {
                            CopyStream(crypto, output);
                        }
                    }

                    if (File.Exists(destFile)) File.Delete(destFile);
                    File.Move(tempDest, destFile);
                }
                finally
                {
                    TryDelete(tempDest);
                }
            }
        }

        private static string SafeDestinationPath(string destDir, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) throw new Exception("Invalid file name.");
            string normalizedName = fileName.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalizedName)) throw new Exception("Invalid file name.");

            string[] parts = normalizedName.Split(Path.DirectorySeparatorChar);
            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part) || part == "." || part == "..")
                    throw new Exception("Invalid file name.");
            }

            string basePath = Path.GetFullPath(destDir);
            string destPath = Path.GetFullPath(Path.Combine(basePath, normalizedName));
            string prefix = basePath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? basePath : basePath + Path.DirectorySeparatorChar;
            if (!destPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !string.Equals(destPath, basePath, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Encrypted file tried to write outside output folder.");
            return destPath;
        }

        public static void VerifyEncryptedFile(string sourceFile)
        {
            if (!IsAuthenticated) throw new UnauthorizedAccessException("Not authenticated.");

            using (var fs = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize))
            {
                if (fs.Length < IvSize + MacSize + 1 + 4) throw new Exception("Invalid file format.");

                byte[] iv = ReadExact(fs, IvSize);
                byte[] mac = ReadExact(fs, MacSize);

                using (var hmac = new HMACSHA256(currentMacKey))
                {
                    hmac.TransformBlock(iv, 0, iv.Length, null, 0);
                    byte[] buffer = new byte[CopyBufferSize];
                    int read;
                    while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                        hmac.TransformBlock(buffer, 0, read, null, 0);
                    hmac.TransformFinalBlock(new byte[0], 0, 0);

                    int diff = 0;
                    byte[] computedMac = hmac.Hash;
                    for (int i = 0; i < MacSize; i++) diff |= mac[i] ^ computedMac[i];
                    if (diff != 0) throw new Exception("File integrity check failed (MAC mismatch).");
                }

                fs.Position = IvSize + MacSize;
                byte isCompressed = (byte)fs.ReadByte();
                byte[] nameLenBytes = ReadExact(fs, 4);
                int nameLen = BitConverter.ToInt32(nameLenBytes, 0);
                if (nameLen < 0 || nameLen > 1024 * 1024) throw new Exception("Invalid metadata.");
                ReadExact(fs, nameLen);

                using (var aes = new AesManaged { Key = currentKey, IV = iv, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 })
                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var crypto = new CryptoStream(fs, decryptor, CryptoStreamMode.Read))
                {
                    if (isCompressed == 1)
                    {
                        using (var gzip = new GZipStream(crypto, CompressionMode.Decompress))
                            CopyStream(gzip, Stream.Null);
                    }
                    else
                    {
                        CopyStream(crypto, Stream.Null);
                    }
                }
            }
        }

        private static bool IsLikelyCompressedMedia(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".mp4" || ext == ".m4v" || ext == ".mov" || ext == ".avi" ||
                   ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".gz" ||
                   ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" ||
                   ext == ".mp3" || ext == ".aac" || ext == ".flac" || ext == ".wav" ||
                   ext == ".pdf";
        }

        private static byte[] ComputeMac(byte[] iv, string payloadPath)
        {
            using (var hmac = new HMACSHA256(currentMacKey))
            {
                hmac.TransformBlock(iv, 0, iv.Length, null, 0);
                using (var payload = new FileStream(payloadPath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize))
                {
                    byte[] buffer = new byte[CopyBufferSize];
                    int read;
                    while ((read = payload.Read(buffer, 0, buffer.Length)) > 0)
                        hmac.TransformBlock(buffer, 0, read, null, 0);
                }
                hmac.TransformFinalBlock(new byte[0], 0, 0);
                return hmac.Hash;
            }
        }

        private static void CopyStream(Stream source, Stream destination)
        {
            byte[] buffer = new byte[CopyBufferSize];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                destination.Write(buffer, 0, read);
        }

        private static byte[] ReadExact(Stream source, int count)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = source.Read(buffer, offset, count - offset);
                if (read <= 0) throw new EndOfStreamException("Invalid file format.");
                offset += read;
            }
            return buffer;
        }

        private static void TryDelete(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); } catch { }
        }

        public static void SecureShredFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    long length = new FileInfo(filePath).Length;
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        byte[] zeroData = new byte[4096]; // Pass 1: 0x00
                        byte[] onesData = new byte[4096]; // Pass 2: 0xFF
                        for(int i = 0; i < onesData.Length; i++) onesData[i] = 0xFF;
                        byte[] randomData = new byte[4096]; // Pass 3: Random

                        // Pass 1
                        fs.Position = 0;
                        long remaining = length;
                        while (remaining > 0)
                        {
                            int toWrite = (int)Math.Min(zeroData.Length, remaining);
                            fs.Write(zeroData, 0, toWrite);
                            remaining -= toWrite;
                        }
                        fs.Flush();

                        // Pass 2
                        fs.Position = 0;
                        remaining = length;
                        while (remaining > 0)
                        {
                            int toWrite = (int)Math.Min(onesData.Length, remaining);
                            fs.Write(onesData, 0, toWrite);
                            remaining -= toWrite;
                        }
                        fs.Flush();

                        // Pass 3
                        fs.Position = 0;
                        remaining = length;
                        using (var rng = new RNGCryptoServiceProvider())
                        {
                            while (remaining > 0)
                            {
                                rng.GetBytes(randomData);
                                int toWrite = (int)Math.Min(randomData.Length, remaining);
                                fs.Write(randomData, 0, toWrite);
                                remaining -= toWrite;
                            }
                        }
                        fs.Flush();
                    }
                    
                    // Rename file before deleting to obscure original name
                    string dir = Path.GetDirectoryName(filePath);
                    string newName = Path.Combine(dir, Guid.NewGuid().ToString() + ".tmp");
                    File.Move(filePath, newName);
                    File.Delete(newName);
                }
                catch { } // Ignore errors if locked
            }
        }
    }
}
