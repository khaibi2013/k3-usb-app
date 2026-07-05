# Kế hoạch phát triển tính năng Ghi chú trực tiếp trong Két sắt

## Goal Description
Bổ sung tính năng cho phép người dùng tạo, đọc và chỉnh sửa trực tiếp các tệp văn bản (như `.txt`, `.md`) ngay bên trong Két sắt mà không cần phải giải mã ra ổ cứng ngoài rồi mã hóa lại. Điều này giúp tăng cường trải nghiệm và tránh rò rỉ dữ liệu tạm thời ra bộ nhớ vật lý.

## User Review Required
Tính năng này sẽ thêm một Modal (Cửa sổ nổi) soạn thảo văn bản đơn giản. Khi người dùng bấm đúp vào file `.txt` trong két, cửa sổ này sẽ hiện lên với nội dung đã giải mã (lưu tạm trong RAM). Khi nhấn "Lưu", nội dung sẽ được mã hóa và ghi đè trực tiếp vào file trong két.

## Proposed Changes

### Backend (Electron)
#### [MODIFY] electron/main.ts
- Thêm IPC `read-vault-text`: Đọc file `.k3enc` từ USB, giải mã và trả về chuỗi text trực tiếp (không ghi ra đĩa).
- Thêm IPC `write-vault-text`: Nhận chuỗi text từ giao diện, mã hóa và ghi đè thẳng vào file `.k3enc` trong USB.

#### [MODIFY] electron/preload.ts
- Expose `readVaultText(fileName)` và `writeVaultText(fileName, content, vaultPath)` để frontend gọi.

### Frontend (Vue)
#### [MODIFY] src/components/FileManager.vue
- Giao diện:
  - Thêm nút "Tạo Ghi chú mới" (New Note) trên thanh công cụ Két sắt.
  - Thêm Modal `TextEditor`: Gồm tiêu đề tệp, ô textarea soạn thảo nội dung, và nút Lưu/Hủy.
- Logic:
  - Nhận diện khi người dùng bấm đúp vào tệp `.txt` hoặc tệp văn bản -> Gọi `readVaultText`, gán vào biến và mở Modal.
  - Khi lưu, gọi `writeVaultText` để ghi vào USB và tải lại danh sách.

## Verification Plan
### Manual Verification
- Người dùng có thể nhấn nút "Tạo Ghi chú" -> Nhập "Hello World" -> Lưu.
- Bấm đúp vào tệp vừa tạo -> Kiểm tra xem nội dung "Hello World" có hiện ra đúng không.
- Chỉnh sửa nội dung thành "Hello K3" -> Lưu lại -> Bấm đúp kiểm tra.
- Mọi thao tác này sẽ không tạo ra bất kỳ file `.txt` tạm nào ngoài ổ cứng gốc.
