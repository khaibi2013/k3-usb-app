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

## Features

- Password login using the existing `.vault_config.json` PBKDF2 format.
- Initial password setup when no config exists.
- Real and decoy vault support.
- Encrypt files into `.vault` as `.k3enc`.
- Decrypt selected `.k3enc` files to an output folder.
- Hide K3 support folders/files with `chflags hidden`.
- Eject the current USB volume with `diskutil eject`.
- Basic USB malware heuristics plus optional ClamAV if `clamscan` exists.

## Notes

- Windows-only features such as Registry history cleanup, CHKDSK, DiskPart read-only mode, and `autorun.inf` auto-run do not exist on macOS.
- macOS does not allow USB autorun for security reasons.
- To use ClamAV on macOS, install it with Homebrew or place `clamscan` on `PATH`.
