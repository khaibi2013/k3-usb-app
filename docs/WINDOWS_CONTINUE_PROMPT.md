# Prompt tiep tuc tren Windows

Toi dang phat trien app USB An Toan K3. Repo da co ban Windows trong `src-csharp` va ban macOS trong `src-mac`.

Muc tieu tren Windows: dua ban Windows len ngang cac tinh nang da lam cho macOS, giu tuong thich du lieu voi Mac va khong xoa du lieu USB hien co.

Hay lam cac viec sau tren may Windows:

1. Kiem tra nhanh repo va build Windows:
   - Thu muc: `src-csharp`
   - Build:
     ```bat
     cd src-csharp
     build.bat
     build-launcher.bat
     ```
   - Neu build loi thi sua truc tiep trong `src-csharp`.

2. Giu tuong thich du lieu voi Mac:
   - Config: `.vault_config.json`
   - Ket that: `.vault`
   - Ket gia: `.vault_decoy`
   - Folder staging: `BaoMat`
   - File ma hoa: `.k3enc`
   - Crypto V1 phai giu format:
     ```text
     [IV 16 bytes][HMAC 32 bytes][isCompressed 1 byte][nameLen 4 bytes little-endian][name UTF-8][AES-CBC encrypted data]
     ```
   - PBKDF2 100000 vong, derive 64 bytes: 32 AES + 32 HMAC.
   - HMAC-SHA256 tren IV + payload.
   - AES-CBC PKCS7.
   - Khong sua format V1 neu chua co migration V2.

3. Dong bo cac tinh nang macOS da lam:
   - First-run wizard 5 buoc:
     1. Tao mat khau that.
     2. Tao mat khau gia.
     3. Chon chinh sach tu huy.
     4. Bat/tat auto scan khi dang nhap.
     5. Hoan tat.
   - Chinh sach nhap sai:
     - Sai 5 lan: khoa 5 phut.
     - Sai 10 lan tuy theo setting:
       - `off`: khong tu huy, chi khoa 5 phut.
       - `lock`: khoa vinh vien.
       - `wipe_real`: xoa `.vault`.
       - `wipe_all`: xoa `.vault`, `.vault_decoy`, `BaoMat`, quarantine, history, trusted, config.
     - Khi bat `wipe_real` hoac `wipe_all`, UI phai hien canh bao va bat xac nhan.
   - Auto scan sau dang nhap neu `auto_scan_on_login=true`.
   - Ma hoa ca folder giu cau truc thu muc:
     - Khi dua folder vao ket, moi file con luu relative path trong metadata name, vi du `Folder/Sub/a.txt`.
     - Khi giai ma, tao lai thu muc cha va chan path traversal (`..`, absolute path).
   - Quarantine manager:
     - Xem path goc, signature, thoi gian, size, SHA-256.
     - Khoi phuc.
     - Xoa vinh vien.
     - Trusted & restore neu false positive.
   - Trusted files theo SHA-256 dung chung `.k3_trusted_hashes.txt`.
   - K3 rules updater:
     - Cap nhat `tools/rules/k3-rules.json` tu URL cau hinh.
     - Nhap rule tu file offline.
     - Validate JSON truoc khi ghi.
   - Scan report:
     - Xuat HTML.
     - Xuat PDF neu WinForms/.NET co thu vien san; neu khong, xuat HTML truoc va ghi ro PDF la backlog.
   - USB vaccine:
     - Tao/bao ve `autorun.inf`.
     - Don shortcut/script virus nghi ngo.
   - Integrity manifest:
     - File `.k3_integrity_manifest.json`.
     - Kiem tra hash cua `AnToanUSB.exe`, `K3 Mac.app/Contents/MacOS/K3UsbSafeMac`, `tools/rules/k3-rules.json`.
     - Neu mismatch thi canh bao trong Security Center.
   - Clone/license warning:
     - Windows da co `hwid`; hay kiem tra lai logic USB serial.
     - Neu clone sang USB khac, app canh bao hoac khoa theo setting.
   - Version/changelog screen:
     - Hien version Windows, build date, changelog ngan.
   - Progress indicator:
     - Khi scan/ma hoa lau, UI hien dang xu ly file nao.

4. Dong goi USB tren Windows:
   - Sau khi build, copy `AnToanUSB.exe` moi vao root USB.
   - Copy AutoLauncher neu can.
   - Giu nguyen:
     - `.vault`
     - `.vault_decoy`
     - `.vault_config.json`
     - `.k3_quarantine`
     - `.k3_recovery_snapshots`
     - `.k3_trusted_hashes.txt`
     - `BaoMat`
     - `tools/clamav/database`
   - Khong xoa du lieu ket that/gia.

5. Test bat buoc tren Windows:
   - Login mat khau that va mat khau gia.
   - Ma hoa file tao tren Windows, giai ma tren Mac.
   - Ma hoa file tao tren Mac, giai ma tren Windows.
   - Ma hoa folder co subfolder, giai ma ra dung cay thu muc tren Windows va Mac.
   - Scan folder test virus:
     - EICAR phai detect.
     - `.jpg.exe`, `.vbs`, `.lnk`, `autorun.inf` nghi ngo phai detect.
   - Quarantine, restore, trusted from quarantine.
   - Export report.
   - Lock/eject hoat dong.
   - Integrity manifest canh bao khi sua `k3-rules.json`.
   - Build lai `AnToanUSB.exe` va copy vao USB.

6. Sau khi xong:
   - Commit thay doi.
   - Push len `main`.
   - Ghi ro lenh build Windows va file output o dau.

Luu y:
- Khong sua/xoa file runtime khong lien quan.
- Khong xoa `.vault`, `.vault_decoy`, `.vault_config.json`.
- Khong dung recovery key implementation that neu chua chuyen sang crypto V2/master key. Hien tai chi co tai lieu kien truc trong `docs/RECOVERY_KEY_ARCHITECTURE.md`.
