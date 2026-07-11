# Prompt tiep tuc tren macOS: dong bo K3 Folder Package voi Windows

Toi dang phat trien app USB An Toan K3. Windows vua doi luong ma hoa folder sang format package `.k3folder` de ban ra thi truong on dinh hon tren USB that.

Hay pull `main` moi nhat va sua ban macOS trong `src-mac` de tuong thich 2 chieu voi Windows.

## Muc tieu chinh

1. File le van dung format `.k3enc` V1 hien co.
2. Folder khi dua vao ket phai tao 1 file duy nhat:
   ```text
   TenFolder.k3folder
   ```
3. `.k3folder` thuc chat la:
   - Zip folder goc thanh `TenFolder.zip` trong temp local.
   - Ma hoa zip bang cung format K3 V1.
   - Metadata `storedName` trong K3 V1 phai la `TenFolder.zip`.
   - Doi duoi output thanh `.k3folder`.
4. Khi giai ma `.k3folder`:
   - Giai ma ra temp local de thu duoc `TenFolder.zip`.
   - Giai nen zip ra thu muc dich thanh `TenFolder/`.
   - Neu trung ten thi tao ten khong trung, vi du `TenFolder (1)`.
   - Xoa temp sau khi xong.

## Format K3 V1 bat buoc giu nguyen

Khong sua format file `.k3enc` cu.

```text
[IV 16 bytes][HMAC 32 bytes][payload]

payload =
[isCompressed 1 byte]
[nameLen 4 bytes little-endian]
[name UTF-8]
[AES-CBC-PKCS7 encrypted bytes]
```

Crypto:

- PBKDF2 100000 vong.
- Salt doc tu `.vault_config.json` field `crypto_salt`.
- Derive 64 bytes theo thu tu:
  - 32 bytes AES key.
  - 32 bytes HMAC key.
- AES-CBC PKCS7.
- HMAC-SHA256 tinh tren:
  ```text
  IV + payload
  ```
- Path trong metadata name phai chan:
  - absolute path
  - `..`
  - path traversal

## Yeu cau import folder tren Mac

Khi user chon/kéo folder vao ket:

1. Preflight folder truoc:
   - Liet ke tat ca file/folder.
   - Thu mo moi file de doc.
   - Chay scan/rule an toan neu app Mac dang co.
2. Neu co file loi:
   - Khong tao package nua chung.
   - Bao ro file nao loi.
   - Khong de lai package hong trong ket.
3. Neu OK:
   - Tao zip tam o local temp, khong tao truc tiep tren USB.
   - Ma hoa zip thanh temp `.k3folder` o local temp.
   - Verify HMAC/decrypt stream cua package.
   - Copy 1 file `.k3folder` sang `.vault` hoac `.vault_decoy`.
   - Verify size/hash sau copy.
   - Neu copy sang USB loi: xoa file `.k3folder` dang copy do.

## Yeu cau UI tren Mac

Trong danh sach ket:

- `.k3enc`: hien nhu file ma hoa.
- `.k3folder`: hien nhu folder/package, ten bo duoi `.k3folder`.
- Giai ma `.k3folder`: bung lai folder goc ra may.
- Neu co nut scan/toan ven/security center thi dem ca `.k3enc` va `.k3folder`.

## Virtual Folder Viewer cho `.k3folder`

Can lam de user co the vao ket va xem folder package nhu folder binh thuong:

1. Double-click `TenFolder.k3folder`.
2. App tao temp local rieng, vi du:
   ```text
   /tmp/K3FolderView_<uuid>/
   ```
3. Giai ma `.k3folder` vao temp de thu `TenFolder.zip`.
4. Giai nen zip vao temp.
5. Hien thi cay thu muc/file ben trong trong UI cua ket.
6. Khi user bam `..` o root package thi quay lai ket that.
7. Khi user copy/giai ma file/folder tu viewer ra may:
   - Copy tu temp ra thu muc dich.
   - Khong sua truc tiep noi dung trong `.k3folder`.
8. Khi app lock, logout, eject, close, crash-safe best effort:
   - Xoa temp `K3FolderView_<uuid>`.

Phase nay chua can sua file ben trong `.k3folder`. Neu user muon cap nhat package thi co the giai ma ra ngoai, sua, roi dua folder vao ket lai de tao package moi.

## Test bat buoc

Sau khi sua Mac, test cac ca sau:

1. Mac tao file le `.k3enc`, Windows giai ma duoc.
2. Windows tao file le `.k3enc`, Mac giai ma duoc.
3. Mac tao folder `.k3folder`, Windows giai ma bung dung cay folder.
4. Windows tao folder `.k3folder`, Mac giai ma bung dung cay folder.
5. Windows/Mac double-click `.k3folder` trong ket va xem duoc danh sach file/folder ben trong.
6. Copy 1 file va 1 subfolder tu viewer `.k3folder` ra may, noi dung phai dung.
7. Lock/close app sau khi xem `.k3folder`, temp viewer phai duoc xoa.
8. Folder co:
   - file rong
   - file ten tieng Viet
   - subfolder nhieu cap
   - file lon tren 1GB neu co the
   - nhieu file nho
9. Rut/copy loi gia lap:
   - Khong de lai package hong.
   - Khong xoa source goc neu import chua xong.

## File Windows da sua lien quan

Tham khao cac thay doi tren Windows:

- `src-csharp/MainForm.cs`
  - `CreateEncryptedFolderPackage`
  - `DecryptFolderPackage`
  - `.k3folder` display/decrypt handling
  - `OpenK3FolderPackageView`
  - `CleanupK3FolderView`
  - virtual viewer navigation for `.k3folder`
- `src-csharp/CryptoEngine.cs`
  - streaming encrypt/decrypt/verify
- `src-csharp/SecurityCenterForm.cs`
  - dem `.k3enc/.k3folder`
- `src-csharp/build.bat`
  - them reference zip library

## Ket qua mong muon

Sau khi xong, Mac va Windows phai dung chung chuan:

- File le: `.k3enc`
- Folder: `.k3folder`
- Khong con ma hoa folder thanh hang nghin file roi khi dua vao USB.
- Khong sot file am tham.
- Neu loi thi fail ca folder va bao report/alert.
