# USB An Toan K3 - BA Requirements

## 1. Muc tieu san pham

USB An Toan K3 la bo cong cu bao mat portable chay truc tiep tren USB, ho tro Windows va macOS, tap trung vao:

- Bao ve du lieu bang ket ma hoa tuong thich Windows/macOS.
- Phat hien rui ro USB malware va virus bang K3 rules + ClamAV portable.
- Ho tro che do mat khau gia, quarantine, trusted files, history, lock/eject.
- Van hanh offline va khong yeu cau cai dat tren may khach cho cac chuc nang cot loi.

## 2. Persona

- Nguoi dung ca nhan: can USB bao mat de luu tai lieu rieng.
- Ky thuat vien: can quet USB/may khach nhanh, portable.
- Khach hang doanh nghiep nho: can USB co branding va chinh sach bao mat ro rang.
- Nguoi ban/phat hanh: can dong goi USB hang loat, QA duoc, cap nhat duoc.

## 3. Functional Requirements

### Data Security

| ID | Requirement | Priority | Acceptance Criteria |
| --- | --- | --- | --- |
| FR-DS-001 | Tao recovery key khi setup ket moi | High | App sinh recovery key mot lan, hien thi ro rang, yeu cau nguoi dung luu offline. |
| FR-DS-002 | Recovery key khong duoc hien lai sau setup | High | Sau khi dong man hinh setup, app chi hien trang thai "Da tao recovery key", khong hien plain key. |
| FR-DS-003 | Recovery key phai khoi phuc duoc quyen truy cap | High | Nguoi dung quen mat khau co the dung recovery key de dat lai mat khau ma khong mat file. |
| FR-DS-004 | Tuy chon tu huy du lieu | High | Settings co toggle bat/tat tu huy sau nhieu lan sai. |
| FR-DS-005 | Chon hanh dong khi sai 10 lan | High | Cho chon: khoa vinh vien, xoa ket that, xoa ket that + ket gia + BaoMat + config. |
| FR-DS-006 | Canh bao truoc khi bat tu huy | High | UI hien canh bao va yeu cau xac nhan truoc khi luu. |
| FR-DS-007 | Ma hoa thu muc giu cau truc | High | Dua folder vao ket, giai ma ra dung cay thu muc ban dau. |
| FR-DS-008 | Tuong thich crypto Windows/macOS | Critical | File `.k3enc` tao tren mot nen tang phai doc duoc tren nen tang con lai. |
| FR-DS-009 | Chu ky toan ven app/tools/rules | High | App canh bao neu `AnToanUSB.exe`, `K3 Mac.app`, `tools/rules/k3-rules.json`, ClamAV portable bi sua. |
| FR-DS-010 | Chong clone USB | Medium | App gan license theo serial USB; neu clone sang USB khac thi canh bao hoac khoa. |

### Antivirus And USB Safety

| ID | Requirement | Priority | Acceptance Criteria |
| --- | --- | --- | --- |
| FR-AV-001 | Auto scan khi mo app | High | Sau login, app tu quet root USB neu setting bat. |
| FR-AV-002 | Canh bao USB malware | High | File `.lnk`, `.vbs`, `.js`, `.jpg.exe`, autorun dang nghi phai bi danh dau. |
| FR-AV-003 | USB vaccine | Medium | App tao/bao ve `autorun.inf`, don shortcut virus, an lai file he thong K3. |
| FR-AV-004 | Quarantine details | Medium | UI xem duoc path goc, signature, thoi gian cach ly, hash. |
| FR-AV-005 | Restore quarantine | Medium | Co the khoi phuc file bi cach ly ve vi tri cu hoac thu muc do nguoi dung chon. |
| FR-AV-006 | Trusted from quarantine | Medium | Co the danh dau trusted neu false positive. |
| FR-AV-007 | K3 rule updater | Medium | App cap nhat `k3-rules.json` tu URL cau hinh hoac file offline. |
| FR-AV-008 | Scan report HTML | High | Sau moi lan quet co the xuat HTML gom tong file, nguy hiem, cach ly, thoi gian, may quet. |

### User Experience

| ID | Requirement | Priority | Acceptance Criteria |
| --- | --- | --- | --- |
| FR-UX-001 | Wizard setup lan dau | High | Co 5 buoc: mat khau that, mat khau gia, chinh sach tu huy, ClamAV, hoan tat. |
| FR-UX-002 | Dashboard tong quan | High | Hien ket, ClamAV, lan quet gan nhat, dung luong USB, so file trong ket. |
| FR-UX-003 | Thong bao ro rang | Medium | Moi thao tac quan trong co status de hieu: vao ket, xoa file goc, eject an toan. |
| FR-UX-004 | Drag and drop | Medium | Keo file/folder vao app de ma hoa; keo file ket ra de giai ma. |
| FR-UX-005 | Da ngon ngu | Medium | Ho tro `vi` va `en`; co cau truc them ngon ngu moi. |

## 4. Non-Functional Requirements

| ID | Requirement | Priority | Acceptance Criteria |
| --- | --- | --- | --- |
| NFR-001 | Portable | Critical | Chay tu USB, khong can cai dat cho chuc nang ket va K3 rules. |
| NFR-002 | Offline first | High | Ma hoa/giai ma/quy tac K3/quarantine chay khi khong co internet. |
| NFR-003 | Data safety | Critical | Update app/tools khong duoc xoa `.vault`, `.vault_decoy`, `.vault_config.json`. |
| NFR-004 | Auditability | High | History ghi lai su kien bao mat quan trong, khong ghi mat khau/plain secret. |
| NFR-005 | Cross-platform compatibility | Critical | Windows/macOS dung chung `.vault_config.json`, `.vault`, `.vault_decoy`, `.k3enc`. |
| NFR-006 | Performance | Medium | Scan 10,000 file nho khong lam UI treo qua lau; co status tien trinh o phase sau. |
| NFR-007 | Legal disclosure | High | Tu huy du lieu va antivirus false positive phai co canh bao ro trong UI/tai lieu. |

## 5. Out Of Scope For V1

- Dong bo cloud.
- Quan tri tap trung enterprise.
- Auto-run tren macOS khi cam USB.
- Bao dam phat hien 100% malware.
- Khoi phuc du lieu neu nguoi dung da xoa key/recovery va quen mat khau.

## 6. Open Questions

- Recovery key se dung kien truc master key moi hay chi la emergency reset cho ban moi?
- Tu huy mac dinh nen tat hay bat khi ban ra?
- License USB gan theo serial vat ly nao tren Windows/macOS de on dinh nhat?
- Co chap nhan can internet de update K3 rules khong?
- Ban Intel Mac co nam trong thi truong muc tieu khong?
