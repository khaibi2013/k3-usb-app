# K3 Portable Tools

Dat tool portable dung chung tren USB theo cau truc nay:

```text
tools/
├─ rules/
│  └─ k3-rules.json
├─ windows/
│  └─ clamav/
│     ├─ clamscan.exe
│     ├─ freshclam.exe
│     └─ database/
├─ mac-arm64/
│  └─ clamav/
│     ├─ clamscan
│     ├─ freshclam
│     └─ database/
└─ mac-x64/
   └─ clamav/
      ├─ clamscan
      ├─ freshclam
      └─ database/
```

- `rules/k3-rules.json` la rule du lieu portable. Ban macOS doc file nay moi lan quet.
- File database ClamAV co the dung theo tung nen tang trong `database/`.
- Binary ClamAV phai dung dung he dieu hanh/CPU. Windows `.exe` khong chay tren macOS.
- Neu thieu tool portable, ban macOS se fallback sang ClamAV cai tren may o `/opt/homebrew`, `/usr/local`, hoac `/usr/bin`.
- De dong goi ClamAV Mac vao USB, tren may Mac build chay:

```bash
brew install clamav
scripts/package-mac-clamav-portable.sh /Volumes/USB-DATA
```

Script se tao wrapper `clamscan` va `freshclam` trong `tools/mac-arm64/clamav` hoac `tools/mac-x64/clamav` de may khac chay duoc ma khong can cai Homebrew.
