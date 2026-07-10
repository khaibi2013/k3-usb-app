# USB An Toan K3 - Release USB Structure

## Visible Root

```text
USB-DATA/
├─ K3 Mac.app
├─ AnToanUSB.exe
├─ README-MAC-K3.txt
├─ USB-An-Toan-K3-portable/
└─ USB-An-Toan-K3-portable.zip
```

## Hidden/System Root

```text
USB-DATA/
├─ .vault/
├─ .vault_decoy/
├─ .vault_config.json
├─ .k3_history.log
├─ .k3_quarantine/
├─ .k3_recovery_snapshots/
├─ .k3_trusted_hashes.txt
├─ BaoMat/
├─ AutoLauncher/
├─ tools/
├─ autorun.inf
└─ icon.png
```

## Tools

```text
tools/
├─ rules/
│  └─ k3-rules.json
├─ mac-arm64/
│  └─ clamav/
│     ├─ clamscan
│     ├─ freshclam
│     ├─ bin/
│     ├─ lib/
│     └─ database/
├─ mac-x64/
│  └─ clamav/
└─ clamav/
   ├─ clamscan.exe
   ├─ freshclam.exe
   └─ database/
```

## Release Rules

- Khong bao gio xoa `.vault`, `.vault_decoy`, `.vault_config.json` khi update app.
- App/tools co the update de len file cu.
- ClamAV database co the update rieng.
- `K3 Mac.app` phai chay tu root USB de nhan dung `.vault_config.json`.
- Windows `.exe` can build tren Windows sau moi thay doi C#.
- Neu ban cho Mac Intel, phai bo sung `tools/mac-x64/clamav`.

## Manual Update Steps

1. Build macOS:

```bash
cd src-mac
swift build -c release
```

2. Copy Mac app binary:

```bash
cp src-mac/.build/release/K3UsbSafeMac /Volumes/USB-DATA/mac/K3UsbSafeMac
cp src-mac/.build/release/K3UsbSafeMac "/Volumes/USB-DATA/K3 Mac.app/Contents/MacOS/K3UsbSafeMac"
```

3. Copy rules:

```bash
cp tools/rules/k3-rules.json /Volumes/USB-DATA/tools/rules/k3-rules.json
```

4. Rebuild Windows tren Windows:

```bat
cd src-csharp
build.bat
build-launcher.bat
```

5. Copy Windows exe vao root USB.

## Release Acceptance

- [ ] Mac app double click duoc.
- [ ] Windows exe double click duoc.
- [ ] ClamAV san sang tren nen tang muc tieu.
- [ ] Ket that hien dung file.
- [ ] Quet bo test co ket qua dung.
- [ ] Lock & Eject thoat app va eject USB.
