# Prompt tiếp tục trên máy Mac

Copy toàn bộ nội dung dưới đây và đưa cho Codex trên máy Mac.

```text
Tôi đang phát triển app USB An Toàn K3. Repo đã có bản Windows ở src-csharp và bản macOS mới tạo ở src-mac.

Mục tiêu: build/test/hoàn thiện bản macOS.

Hãy làm các bước sau:

1. Mở và kiểm tra project macOS:
   - Thư mục: src-mac
   - Package: src-mac/Package.swift
   - App: K3UsbSafeMac
   - Công nghệ: SwiftUI / Swift Package

2. Chạy build trên Mac:
   - cd src-mac
   - swift build -c release
   Nếu lỗi compile thì sửa trực tiếp trong src-mac.

3. Kiểm tra tính tương thích với bản Windows:
   - Config file: .vault_config.json
   - Vault chính: .vault
   - Vault giả: .vault_decoy
   - File mã hóa: .k3enc
   - Crypto format phải tương thích với src-csharp/CryptoEngine.cs:
     [IV 16 bytes][HMAC 32 bytes][isCompressed 1 byte][nameLen 4 bytes little-endian][name UTF-8][AES-CBC encrypted data]
   - Key derivation: PBKDF2 100000 vòng, derive 64 bytes, 32 bytes AES key + 32 bytes HMAC key.
   - HMAC-SHA256 trên IV + payload.
   - AES-CBC PKCS7.

4. Test luồng chính:
   - Chạy app.
   - Nếu chưa có config thì tạo mật khẩu mới.
   - App phải tạo .vault, .vault_decoy, BaoMat, .vault_config.json.
   - Login bằng mật khẩu thật.
   - Encrypt một file text vào .vault.
   - Decrypt file đó ra thư mục khác.
   - So sánh nội dung file gốc và file giải mã.
   - Test decoy password nếu có.
   - Test ẩn file bằng chflags hidden.
   - Test eject USB bằng diskutil eject nếu đang chạy từ USB.

5. Kiểm tra UI:
   - LoginView
   - MainView
   - Chọn file để encrypt
   - Chọn file trong vault để decrypt
   - Refresh vault
   - Logout
   - Lock & Eject

6. Nếu SwiftUI fileImporter không cho đọc file do sandbox/security-scoped bookmark, hãy sửa bằng startAccessingSecurityScopedResource() khi xử lý URL chọn từ file picker.

7. Nếu Package.swift không build được do CommonCrypto import/linking, hãy sửa cấu hình Swift Package cho macOS phù hợp.

8. Nếu gzip/gunzip qua /usr/bin/gzip không tương thích hoặc gây lỗi, hãy sửa sao cho file .k3enc vẫn tương thích với Windows CryptoEngine.cs. Có thể tạm thời bỏ compression cho file mới trên Mac, nhưng phải decrypt được file Windows đã compressed.

9. Sau khi sửa xong:
   - Commit thay đổi.
   - Push lên main.
   - Ghi rõ cách build app Mac và file output ở đâu.

Lưu ý:
- Không sửa/xóa các thay đổi không liên quan.
- Không đụng vào eicar_test_virus.txt nếu nó đang bị thay đổi/deleted ngoài luồng.
- Bản Windows đã build/package ổn, tập trung vào src-mac.
```
