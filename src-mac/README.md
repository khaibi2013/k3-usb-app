# USB An Toan K3 for macOS

This is the macOS companion app for the K3 USB vault. It is written with SwiftUI and keeps the same portable storage layout as the Windows build:

- `.vault`
- `.vault_decoy`
- `.vault_config.json`
- `BaoMat`
- `.k3enc` encrypted files

## Requirements

- macOS 13 or newer
- Xcode 15 or newer

## Open And Run

1. Copy `src-mac` to a Mac, or open the repository on a Mac.
2. Open `src-mac/Package.swift` with Xcode.
3. Select the `K3UsbSafeMac` scheme.
4. Run.

## Build From Terminal

```bash
cd src-mac
swift build -c release
```

The release executable is written to:

```text
src-mac/.build/release/K3UsbSafeMac
```

## Features

- Password login using the existing `.vault_config.json` PBKDF2 format.
- Initial password setup when no config exists.
- Real and decoy vault support.
- Encrypt files into `.vault` as `.k3enc`.
- Decrypt selected `.k3enc` files to an output folder.
- Security Center with portable layout status and vault integrity verification.
- Antivirus scan UI using K3 USB heuristics plus optional ClamAV if `clamscan` exists.
- YARA-like built-in script/content rules for USB malware patterns, EICAR test detection, downloader scripts, macOS persistence hints, and Office/media decoy names.
- ClamAV database update button when `freshclam` exists.
- Quarantine manager with restore and permanent delete.
- Secure shred for quarantined deletes and auto-encrypted source cleanup.
- Recovery snapshots before antivirus scans in `.k3_recovery_snapshots`.
- Trusted files UI backed by `.k3_trusted_hashes.txt` SHA-256 entries.
- History log for K3 app actions on macOS.
- Settings UI for passwords, login text, and portable behavior flags.
- Auto-encrypt watcher for `BaoMat` or the whole USB root while logged in.
- macOS maintenance tools for metadata cleanup, layout repair, and `diskutil verifyVolume`.
- Hide K3 support folders/files with `chflags hidden`.
- Eject the current USB volume with `diskutil eject`.
- Basic USB malware heuristics plus optional ClamAV if `clamscan` exists.

## Notes

- Windows-only features such as Registry history cleanup, CHKDSK, DiskPart read-only mode, and `autorun.inf` auto-run do not exist on macOS.
- macOS does not allow USB autorun for security reasons.
- The macOS build provides equivalent maintenance actions where possible: portable layout repair, `.DS_Store`/`._*` cleanup, and `diskutil verifyVolume`.
- To use ClamAV on macOS, install it with Homebrew or place `clamscan` on `PATH`.
