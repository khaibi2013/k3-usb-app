import Foundation

struct K3ScanResult {
    let isThreat: Bool
    let name: String
}

struct K3AntivirusEngineInfo {
    let clamScanPath: String?
    let freshClamPath: String?

    var clamAvailable: Bool { clamScanPath != nil }
    var freshClamAvailable: Bool { freshClamPath != nil }
}

enum K3MacScanner {
    private static let clamscanCandidates = ["/opt/homebrew/bin/clamscan", "/usr/local/bin/clamscan", "/usr/bin/clamscan"]
    private static let freshclamCandidates = ["/opt/homebrew/bin/freshclam", "/usr/local/bin/freshclam", "/usr/bin/freshclam"]

    static func engineInfo() -> K3AntivirusEngineInfo {
        K3AntivirusEngineInfo(
            clamScanPath: clamscanCandidates.first(where: { FileManager.default.isExecutableFile(atPath: $0) }),
            freshClamPath: freshclamCandidates.first(where: { FileManager.default.isExecutableFile(atPath: $0) })
        )
    }

    static func scan(_ file: URL) -> K3ScanResult {
        if let rule = matchUsbRules(file) {
            return K3ScanResult(isThreat: true, name: rule)
        }
        if let yara = matchContentRules(file) {
            return K3ScanResult(isThreat: true, name: yara)
        }
        if let clam = scanWithClamAV(file) {
            return K3ScanResult(isThreat: true, name: clam)
        }
        return K3ScanResult(isThreat: false, name: "")
    }

    static func updateClamAVDatabase() throws -> String {
        guard let freshclam = engineInfo().freshClamPath else {
            throw K3Error.userFacing("freshclam was not found. Install ClamAV with Homebrew or place freshclam on PATH.")
        }
        let process = Process()
        process.executableURL = URL(fileURLWithPath: freshclam)
        process.arguments = []
        let pipe = Pipe()
        process.standardOutput = pipe
        process.standardError = pipe
        try process.run()
        process.waitUntilExit()
        let output = String(data: pipe.fileHandleForReading.readDataToEndOfFile(), encoding: .utf8) ?? ""
        guard process.terminationStatus == 0 else {
            throw K3Error.userFacing(output.isEmpty ? "freshclam failed." : output)
        }
        return output.isEmpty ? "ClamAV database update completed." : output
    }

    private static func matchUsbRules(_ file: URL) -> String? {
        let name = file.lastPathComponent.lowercased()
        let ext = file.pathExtension.lowercased()
        let risky = ["bat", "cmd", "vbs", "vbe", "js", "jse", "wsf", "wsh", "hta", "scr", "pif", "com", "ps1", "lnk", "reg", "inf", "scf", "url", "command", "tool"]
        if risky.contains(ext) { return "K3.USB.RiskyScriptOrShortcut" }
        let decoys = [".pdf.exe", ".doc.exe", ".docx.exe", ".xls.exe", ".xlsx.exe", ".jpg.exe", ".png.exe", ".mp4.exe", ".zip.exe"]
        if decoys.contains(where: { name.hasSuffix($0) }) || name == "folder.exe" || name == "new folder.exe" {
            return "K3.USB.FolderImpersonator"
        }
        if name == "autorun.inf" {
            let text = (try? String(contentsOf: file, encoding: .utf8).lowercased()) ?? ""
            if text.contains("open=") || text.contains("shellexecute=") { return "K3.USB.SuspiciousAutorun" }
        }
        if name.hasPrefix("._") || name == ".ds_store" { return nil }
        return nil
    }

    private static func matchContentRules(_ file: URL) -> String? {
        guard let values = try? file.resourceValues(forKeys: [.fileSizeKey]),
              (values.fileSize ?? 0) <= 2 * 1024 * 1024,
              let data = try? Data(contentsOf: file) else { return nil }

        if String(data: data, encoding: .ascii)?.contains("EICAR-STANDARD-ANTIVIRUS-TEST-FILE") == true {
            return "K3.Test.EICAR"
        }

        let text = String(data: data, encoding: .utf8)?.lowercased()
            ?? String(data: data, encoding: .ascii)?.lowercased()
            ?? ""
        guard !text.isEmpty else { return nil }

        let scriptRules: [(String, String)] = [
            ("powershell", "K3.Script.PowerShell"),
            ("encodedcommand", "K3.Script.EncodedPowerShell"),
            ("frombase64string", "K3.Script.Base64Payload"),
            ("wscript.shell", "K3.Script.WScriptShell"),
            ("createobject(", "K3.Script.ActiveXCreateObject"),
            ("shellexecute", "K3.Script.ShellExecute"),
            ("do shell script", "K3.Mac.OSAScriptShell"),
            ("launchctl load", "K3.Mac.PersistenceLaunchctl"),
            ("/library/launchagents", "K3.Mac.PersistenceLaunchAgent"),
            ("curl ", "K3.Script.Downloader"),
            ("wget ", "K3.Script.Downloader"),
            ("chmod +x", "K3.Script.Dropper"),
            ("rm -rf /", "K3.Script.DestructiveCommand")
        ]

        let matches = scriptRules.filter { text.contains($0.0) }.map(\.1)
        if matches.contains("K3.Script.Downloader"), text.contains("| sh") || text.contains("| bash") {
            return "K3.Script.DownloadAndExecute"
        }
        if matches.count >= 2 {
            return matches.first ?? "K3.Script.Suspicious"
        }
        return nil
    }

    private static func scanWithClamAV(_ file: URL) -> String? {
        guard let clamscan = engineInfo().clamScanPath else { return nil }
        let process = Process()
        process.executableURL = URL(fileURLWithPath: clamscan)
        process.arguments = ["--no-summary", "--infected", file.path]
        let pipe = Pipe()
        process.standardOutput = pipe
        process.standardError = pipe
        do {
            try process.run()
            process.waitUntilExit()
            let output = String(data: pipe.fileHandleForReading.readDataToEndOfFile(), encoding: .utf8) ?? ""
            if process.terminationStatus == 1 {
                return extractClamName(output)
            }
        } catch {
            return nil
        }
        return nil
    }

    private static func extractClamName(_ output: String) -> String {
        guard let foundRange = output.range(of: " FOUND", options: .caseInsensitive) else { return "ClamAV.Detected" }
        let prefix = output[..<foundRange.lowerBound]
        guard let colon = prefix.lastIndex(of: ":") else { return "ClamAV.Detected" }
        let name = prefix[prefix.index(after: colon)...].trimmingCharacters(in: .whitespacesAndNewlines)
        return name.isEmpty ? "ClamAV.Detected" : "ClamAV.\(name)"
    }
}
