import Foundation

struct K3ScanResult {
    let isThreat: Bool
    let name: String
}

enum K3MacScanner {
    static func scan(_ file: URL) -> K3ScanResult {
        if let rule = matchUsbRules(file) {
            return K3ScanResult(isThreat: true, name: rule)
        }
        if let clam = scanWithClamAV(file) {
            return K3ScanResult(isThreat: true, name: clam)
        }
        return K3ScanResult(isThreat: false, name: "")
    }

    private static func matchUsbRules(_ file: URL) -> String? {
        let name = file.lastPathComponent.lowercased()
        let ext = file.pathExtension.lowercased()
        let risky = ["bat", "cmd", "vbs", "vbe", "js", "jse", "wsf", "wsh", "hta", "scr", "pif", "com", "ps1", "lnk"]
        if risky.contains(ext) { return "K3.USB.RiskyScriptOrShortcut" }
        if name.hasSuffix(".pdf.exe") || name.hasSuffix(".doc.exe") || name.hasSuffix(".jpg.exe") || name == "folder.exe" {
            return "K3.USB.FolderImpersonator"
        }
        if name == "autorun.inf" {
            let text = (try? String(contentsOf: file, encoding: .utf8).lowercased()) ?? ""
            if text.contains("open=") || text.contains("shellexecute=") { return "K3.USB.SuspiciousAutorun" }
        }
        return nil
    }

    private static func scanWithClamAV(_ file: URL) -> String? {
        let candidates = ["/opt/homebrew/bin/clamscan", "/usr/local/bin/clamscan", "/usr/bin/clamscan"]
        guard let clamscan = candidates.first(where: { FileManager.default.isExecutableFile(atPath: $0) }) else { return nil }
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
