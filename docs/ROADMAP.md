# USB An Toan K3 - Product Roadmap

## Phase 1 - Hardening V1

Muc tieu: on dinh ban ban dau, khong lam mat du lieu.

- [x] macOS app SwiftUI portable.
- [x] Crypto tuong thich Windows.
- [x] ClamAV Mac portable arm64 tren USB.
- [x] Login lockout 5 phut va chinh sach tu huy sau 10 lan sai.
- [x] Lock & Eject: khoa session, thoat app, eject USB.
- [x] Scan report HTML.
- [x] Dashboard tong quan dung luong/lan quet/file ket.
- [x] USB vaccine co nut rieng.
- [x] Release builder script.

## Phase 2 - Data Security

- [x] Self-destruct policy UI: off / lock only / wipe real vault / wipe all sensitive data.
- [x] Folder encryption with structure preservation.
- [x] Integrity manifest for app/tools/rules.
- [ ] Recovery key architecture design.
- [ ] Recovery key implementation for new vault format or compatible bridge.

## Phase 3 - Antivirus And Safety

- [x] Auto scan on login/start.
- [x] Strong quarantine detail view.
- [x] Restore quarantine to original path.
- [x] Trust from quarantine.
- [x] K3 rule updater from URL/file.
- [ ] Scan report export HTML/PDF.

## Phase 4 - Commercialization

- [ ] License bound to USB serial.
- [ ] Clone detection warning.
- [ ] Branding package per customer.
- [ ] Version/changelog screen.
- [ ] Windows and macOS release builder.
- [ ] QA release report.

## Phase 5 - UX And Localization

- [x] First-run wizard.
- [ ] Drag and drop encrypt/decrypt.
- [ ] Vietnamese and English localization.
- [ ] Friendlier empty states and warnings.
- [ ] Better progress indicator for long scan/encrypt operations.

## Important Architecture Notes

Recovery key is not a small UI-only feature. Current crypto derives the file encryption key from the login password and shared salt. A real recovery key requires one of these designs:

1. New master-key format: data encrypted by random master key, password and recovery key only wrap/unwrap that master key.
2. Compatibility bridge: keep `.k3enc` format, but add encrypted key material in config and migrate only new files.
3. Emergency reset only: recovery key can reset login but cannot decrypt old data if password is forgotten. This is not true recovery.

The recommended product path is design 1 for a future vault version, with a migration wizard and explicit compatibility testing.
