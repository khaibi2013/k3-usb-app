# Bối Cảnh Lập Trình Dự Án IronVault (AI Context Master Document)

**Gửi AI (Agent) của phiên làm việc tiếp theo:**
Hãy đọc thật kỹ toàn bộ tài liệu này trước khi lập trình hoặc đề xuất bất kỳ đoạn code nào. File này chứa toàn bộ cấu trúc kiến trúc, danh sách chức năng cực kỳ chi tiết và các kỹ thuật chống lỗi đặc thù nền tảng (Cross-platform) đã được áp dụng. Việc đọc tài liệu này giúp bạn không phá hỏng những tính năng đã hoạt động vô cùng trơn tru.

## 1. Tổng Quan Kiến Trúc & Công Nghệ
- **Tên dự án:** IronVault (thư mục: `k3-usb-app`)
- **Mục tiêu:** Xây dựng phần mềm "Két sắt Bảo mật" Portable chạy trực tiếp từ USB trên cả macOS và Windows mà không cần cài đặt. Toàn bộ dữ liệu nằm gọn trên USB và được mã hóa.
- **Tech Stack:** 
  - **Frontend:** Vue 3, Vite, TypeScript, Vanilla CSS (Giao diện hai cột File Manager kiểu Commander).
  - **Backend:** Electron (Node.js), IPC Communication.
  - **Core Modules:** `crypto` (AES-256-GCM), `chokidar` (Watcher), `systeminformation` (HWID Lock), `@virustotal/yara-x` (Antivirus).
- **Lưu trữ Cấu hình:** `.vault_config.json` nằm trực tiếp ở Root của USB.
- **Lưu trữ Két Sắt:** Thư mục ẩn `.vault` (hoặc `.vault_decoy`) nằm ở Root của USB.

---

## 2. Chi Tiết Các Tính Năng Lõi (Core Features)

### A. Hệ Thống Mã Hóa & Giải Mã (Crypto Engine)
- **Thuật toán:** Sử dụng `aes-256-gcm` với `crypto.scryptSync` để tạo khóa (Key Derivation) từ mật khẩu người dùng. 
- **Cấu trúc File Két sắt (`.k3enc`):** 
  - Metadata được ghép trực tiếp vào file mã hóa theo thứ tự Byte: `IV (16 bytes)` + `Auth Tag (16 bytes)` + `isCompressed (1 byte)` + `nameLen (4 bytes, UInt32LE)` + `OriginalNameBuffer` + `Encrypted Data`.
  - Giúp bảo toàn tên file gốc và cây thư mục nguyên bản.
- **Nén Dữ Liệu (Compression):** Tự động nén bằng `zlib.gzipSync` các file văn bản/data trước khi mã hóa để tiết kiệm không gian USB. (Bỏ qua nén với `.mp4`, `.zip`, `.jpg`...).
- **Xóa Hủy An Toàn (Secure Shredding):** Tính năng `shredFile()` ghi đè chuỗi byte ngẫu nhiên lên ổ cứng vật lý trước khi xóa file gốc (`fs.unlink`) để chống lại các phần mềm phục hồi dữ liệu.

### B. Auto-Encrypt (Mã Hóa Tự Động Background)
- **Cơ chế:** Dùng `chokidar` theo dõi thư mục không ẩn `BaoMat` (Tên có thể đổi trong Settings). Bất cứ file nào copy/kéo thả vào đây sẽ bị bắt sự kiện `on('add')`.
- **Luồng xử lý:** Quét virus file -> Nén -> Mã hóa đưa vào `.vault` -> Shred (Hủy) file gốc bên ngoài `BaoMat` -> Bắn IPC cập nhật UI.
- **Bắt lỗi Windows Explorer EBUSY:** Windows thường xuyên Lock (khóa) file đang copy quá lâu. Hệ thống đã cài đặt vòng lặp Retry 10 lần (delay 1s mỗi lần) để liên tục thử mã hóa lại cho đến khi Windows nhả Lock. **Tuyệt đối không xóa vòng lặp này.**

### C. Quản Trị Hệ Thống Quét Virus (Antivirus & Quarantine)
- **Offline Heuristics:** Quét nhanh tên file chặn double-extension (vd: `image.png.exe`), chặn EICAR, và các đuôi script nguy hiểm (`.bat`, `.vbs`, `.ps1`).
- **Yara Engine:** Tích hợp bộ thư viện quét signature mã độc yara, chuyên dụng quét chặn Virus ẩn trong USB trước khi file được đưa vào Két sắt.
- **Quarantine:** Các file nghi ngờ bị cách ly, không được đưa vào thư mục `.vault` mà chuyển sang khoanh vùng xử lý riêng.

### D. Két Sắt Giả (Decoy Vault) & Khóa Thiết Bị (HWID Lock)
- **Decoy Vault:** Hỗ trợ nhập 2 mật khẩu khác nhau ở màn hình Login. Nếu nhập mật khẩu giả, phần mềm mount thư mục `.vault_decoy` thay vì `.vault`. Giao diện hiển thị y hệt để che mắt người ép buộc cung cấp mật khẩu.
- **HWID Lock:** Tính năng trói buộc USB vào 1 máy tính duy nhất thông qua mã định danh phần cứng (Hardware ID). Cắm sang máy khác phần mềm sẽ từ chối truy cập.

### E. Giao Diện Quản Lý File (Commander UI)
- Vue 3 Frontend thiết kế 2 khung hình: Cột trái (Máy tính hiện tại) - Cột phải (Két sắt trên USB). Hỗ trợ Drag & Drop hai chiều.
- **Tìm kiếm nâng cao (Search):** Thuật toán tìm kiếm trong Két sắt quét **toàn bộ cây thư mục**. (Đã fix lỗi cross-platform để hiển thị đúng dấu `/` hoặc `\` và xử lý đúng file nhận diện thư mục là `.k3empty`).
- **Real-time UX:** Sử dụng Toast Notification và Modal Tiến trình (Processing Overlay) kích hoạt thông qua event `encryption-progress` IPC để báo trạng thái ngầm liên tục cho người dùng.

---

## 3. Các Lỗi Nghiêm Trọng Đã Fix (Tuyệt Đối Không Phá Vỡ Logic)
1. **Mất Thư Mục Chứa (`.k3empty`):** `electron-builder` và cơ chế giải nén thường bỏ qua Folder rỗng. Hệ thống quản lý Két sắt lưu thư mục con dưới dạng 1 file giả mang tên `.k3empty`. Logic search, copy, delete phụ thuộc hoàn toàn vào file này. Đừng sửa đổi cơ chế tạo folder `.k3empty`.
2. **UI Bị Đóng Băng Khi Auto-Encrypt Lỗi:** Đã bắt `catch` trong event của `chokidar`, nếu `retries` thất bại hoàn toàn sẽ bắn `status: 'done'` qua IPC kèm thông báo lỗi để giải phóng UI.
3. **Cross-Platform Paths:** Bất cứ khi nào thao tác với đường dẫn, tuyệt đối dùng `path.join`, `path.basename` thay vì cắt chuỗi thủ công bằng `/`, vì Windows dùng `\`.

## 4. Chỉ Thị Dành Riêng Cho Môi Trường Windows (Ngày Mai)
- Mọi câu lệnh Node.js `fs` cần bao bọc trong `try...catch` thật kỹ càng với Error code `EPERM` hoặc `EBUSY`, vì Windows Defender và Windows Indexer rất hay chèn ngang giành quyền đọc file.
- Giao diện có thể test trực tiếp bằng `npm run dev`. File `.exe` build ra nằm ở `release/IronVault_Windows.exe`.
- Khi đóng gói cho Windows, sử dụng chuẩn `electron-builder --win --x64`. Tham chiếu cấu hình Build tại `package.json`.
