# USB An Toan K3 - QA Checklist

## Test Matrix

| Platform | Required | Notes |
| --- | --- | --- |
| Windows 10 | Yes | Build va chay `AnToanUSB.exe`. |
| Windows 11 | Yes | Test ClamAV Windows portable. |
| macOS Apple Silicon | Yes | Test `K3 Mac.app`, ClamAV mac-arm64 portable. |
| macOS Intel | Optional for V1 | Can `tools/mac-x64/clamav`. |
| USB exFAT | Yes | Cau hinh ban hang hien tai. |
| USB gan day dung luong | Yes | Test loi ghi file/report/quarantine. |

## Setup And Login

- [ ] USB moi khong co config tao duoc `.vault_config.json`, `.vault`, `.vault_decoy`, `BaoMat`.
- [ ] Tao mat khau that thanh cong.
- [ ] Tao mat khau gia thanh cong.
- [ ] Login bang mat khau that vao ket that.
- [ ] Login bang mat khau gia vao ket gia.
- [ ] Sai mat khau 1-4 lan hien canh bao.
- [ ] Sai lan thu 5 khoa 5 phut.
- [ ] Het 5 phut co the nhap lai.
- [ ] Sai lan thu 10 hanh dong theo chinh sach tu huy.
- [ ] Khong ghi plain password/recovery key vao history/log.

## Vault

- [ ] Ma hoa file text nho.
- [ ] Ma hoa file ten tieng Viet.
- [ ] Ma hoa file lon hon 1GB.
- [ ] Ma hoa folder giu dung cau truc khi giai ma.
- [ ] Giai ma tren Windows file tao tu Mac.
- [ ] Giai ma tren Mac file tao tu Windows.
- [ ] HMAC sai thi bi chan.
- [ ] File goc duoc xoa an toan neu chon dua vao ket.

## Antivirus

- [ ] ClamAV portable Mac hien "ClamAV san sang".
- [ ] ClamAV portable Windows hien san sang.
- [ ] K3 rules bat `.jpg.exe`.
- [ ] K3 rules bat `.vbs`.
- [ ] K3 rules bat EICAR chuan.
- [ ] File sach khong bi false positive trong bo test.
- [ ] Quarantine file nguy hiem.
- [ ] Restore quarantine.
- [ ] Xoa vinh vien quarantine.
- [ ] Trusted file bo qua canh bao.
- [ ] Xuat scan report HTML.

## USB Safety

- [ ] USB vaccine tao/bao ve `autorun.inf`.
- [ ] Don shortcut virus `.lnk` dang nghi.
- [ ] Khong xoa file user khi vaccine.
- [ ] Lock & Eject khoa session, thoat app, eject volume.
- [ ] Neu eject that bai co thong bao ro.

## Release

- [ ] `K3 Mac.app` mo bang double click.
- [ ] `AnToanUSB.exe` mo tren Windows.
- [ ] `tools/rules/k3-rules.json` co version dung.
- [ ] `.vault`, `.vault_decoy`, `.vault_config.json` duoc an.
- [ ] Copy update app khong lam mat du lieu ket.
- [ ] README co canh bao tu huy du lieu.
