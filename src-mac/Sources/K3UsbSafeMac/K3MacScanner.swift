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
    private struct PortableRuleSet: Codable {
        var version: Int
        var nameRules: [NameRule]
        var contentRules: [ContentRule]
    }

    private struct NameRule: Codable {
        var id: String
        var extensions: [String]?
        var exactNames: [String]?
        var suffixes: [String]?
        var contains: [String]?
    }

    private struct ContentRule: Codable {
        var id: String
        var extensions: [String]?
        var maxBytes: Int?
        var all: [String]?
        var any: [String]?
    }

    private static let clamscanCandidates = ["/opt/homebrew/bin/clamscan", "/usr/local/bin/clamscan", "/usr/bin/clamscan"]
    private static let freshclamCandidates = ["/opt/homebrew/bin/freshclam", "/usr/local/bin/freshclam", "/usr/bin/freshclam"]

    static func engineInfo(root: URL? = nil) -> K3AntivirusEngineInfo {
        K3AntivirusEngineInfo(
            clamScanPath: clamScanPath(root: root),
            freshClamPath: freshClamPath(root: root)
        )
    }

    static func ensurePortableLayout(at root: URL) {
        let folders = [
            root.appendingPathComponent("tools/rules", isDirectory: true),
            root.appendingPathComponent("tools/mac-arm64/clamav/database", isDirectory: true),
            root.appendingPathComponent("tools/mac-x64/clamav/database", isDirectory: true),
            root.appendingPathComponent("tools/windows/clamav/database", isDirectory: true)
        ]
        for folder in folders {
            try? FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        }
        let ruleURL = portableRuleURL(root: root)
        if !FileManager.default.fileExists(atPath: ruleURL.path) {
            try? defaultRuleData.write(to: ruleURL, options: .atomic)
        }
        writeFreshClamConfigIfNeeded(root: root)
        try? MacSystemTools.hide(root.appendingPathComponent("tools", isDirectory: true))
    }

    static func scan(_ file: URL, root: URL? = nil) -> K3ScanResult {
        if let rule = matchPortableRules(file, root: root) {
            return K3ScanResult(isThreat: true, name: rule)
        }
        if let rule = matchUsbRules(file) {
            return K3ScanResult(isThreat: true, name: rule)
        }
        if let yara = matchContentRules(file) {
            return K3ScanResult(isThreat: true, name: yara)
        }
        if let clam = scanWithClamAV(file, root: root) {
            return K3ScanResult(isThreat: true, name: clam)
        }
        return K3ScanResult(isThreat: false, name: "")
    }

    static func updateClamAVDatabase(root: URL? = nil) throws -> String {
        if let root {
            ensurePortableLayout(at: root)
        }
        guard let freshclam = engineInfo(root: root).freshClamPath else {
            throw K3Error.userFacing("Chua tim thay freshclam. Hay dat ban ClamAV portable vao tools/mac-arm64/clamav hoac tools/mac-x64/clamav.")
        }
        let process = Process()
        process.executableURL = URL(fileURLWithPath: freshclam)
        if let root {
            process.currentDirectoryURL = portableClamRoot(root: root)
            process.arguments = ["--config-file=\(portableFreshClamConfigURL(root: root).path)", "--stdout"]
        } else {
            process.arguments = []
        }
        let pipe = Pipe()
        process.standardOutput = pipe
        process.standardError = pipe
        try process.run()
        process.waitUntilExit()
        let output = String(data: pipe.fileHandleForReading.readDataToEndOfFile(), encoding: .utf8) ?? ""
        guard process.terminationStatus == 0 else {
            throw K3Error.userFacing(output.isEmpty ? "freshclam that bai." : output)
        }
        return output.isEmpty ? "Da cap nhat CSDL ClamAV." : output
    }

    static func updatePortableRules(from urlString: String, root: URL) throws -> String {
        let trimmed = urlString.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty, let url = URL(string: trimmed) else {
            throw K3Error.userFacing("URL cap nhat K3 rules khong hop le.")
        }
        let data = try Data(contentsOf: url)
        try installPortableRules(data: data, root: root)
        return portableRulesStatus(root: root)
    }

    static func importPortableRules(from file: URL, root: URL) throws -> String {
        let didAccess = file.startAccessingSecurityScopedResource()
        defer {
            if didAccess {
                file.stopAccessingSecurityScopedResource()
            }
        }
        let data = try Data(contentsOf: file)
        try installPortableRules(data: data, root: root)
        return portableRulesStatus(root: root)
    }

    static func installPortableRules(data: Data, root: URL) throws {
        guard data.count <= 2 * 1024 * 1024 else {
            throw K3Error.userFacing("File K3 rules qua lon.")
        }
        let rules = try JSONDecoder().decode(PortableRuleSet.self, from: data)
        guard rules.version > 0 else {
            throw K3Error.userFacing("K3 rules thieu version hop le.")
        }
        guard !rules.nameRules.isEmpty || !rules.contentRules.isEmpty else {
            throw K3Error.userFacing("K3 rules khong co rule nao.")
        }
        ensurePortableLayout(at: root)
        let url = portableRuleURL(root: root)
        try data.write(to: url, options: .atomic)
        try? MacSystemTools.hide(root.appendingPathComponent("tools", isDirectory: true))
    }

    static func portableRulesStatus(root: URL) -> String {
        ensurePortableLayout(at: root)
        guard let rules = loadPortableRules(root: root) else { return "Chua co rule portable" }
        return "\(rules.nameRules.count) rule ten, \(rules.contentRules.count) rule noi dung"
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
            ("createremotethread", "K3.Win.ApiInjection"),
            ("virtualallocex", "K3.Win.ApiInjection"),
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
        if matches.filter({ $0 == "K3.Win.ApiInjection" }).count >= 2 {
            return "K3.Win.ApiInjection"
        }
        if matches.contains("K3.Script.Downloader"), text.contains("| sh") || text.contains("| bash") {
            return "K3.Script.DownloadAndExecute"
        }
        if matches.count >= 2 {
            return matches.first ?? "K3.Script.Suspicious"
        }
        return nil
    }

    private static func scanWithClamAV(_ file: URL, root: URL?) -> String? {
        guard let clamscan = engineInfo(root: root).clamScanPath else { return nil }
        let process = Process()
        process.executableURL = URL(fileURLWithPath: clamscan)
        var arguments = ["--no-summary", "--infected"]
        if let root {
            ensurePortableLayout(at: root)
            let database = portableDatabaseURL(root: root)
            if ((try? FileManager.default.contentsOfDirectory(at: database, includingPropertiesForKeys: nil)) ?? []).isEmpty == false {
                arguments.append("--database=\(database.path)")
            }
        }
        arguments.append(file.path)
        process.arguments = arguments
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

    private static func matchPortableRules(_ file: URL, root: URL?) -> String? {
        guard let root, let rules = loadPortableRules(root: root) else { return nil }
        let name = file.lastPathComponent.lowercased()
        let ext = file.pathExtension.lowercased()

        for rule in rules.nameRules {
            if let extensions = rule.extensions, !extensions.map({ $0.lowercased() }).contains(ext) { continue }
            if rule.exactNames?.contains(where: { name == $0.lowercased() }) == true { return rule.id }
            if rule.suffixes?.contains(where: { name.hasSuffix($0.lowercased()) }) == true { return rule.id }
            if rule.contains?.contains(where: { name.contains($0.lowercased()) }) == true { return rule.id }
        }

        let fileSize = (try? file.resourceValues(forKeys: [.fileSizeKey]).fileSize) ?? 0
        let maxReadable = rules.contentRules.compactMap(\.maxBytes).max() ?? (2 * 1024 * 1024)
        guard fileSize <= maxReadable, let data = try? Data(contentsOf: file) else { return nil }
        let text = (String(data: data, encoding: .utf8) ?? String(data: data, encoding: .ascii) ?? "")
            .lowercased()
            .replacingOccurrences(of: "\0", with: "")
        guard !text.isEmpty else { return nil }

        for rule in rules.contentRules {
            if let extensions = rule.extensions, !extensions.map({ $0.lowercased() }).contains(ext) { continue }
            if let maxBytes = rule.maxBytes, data.count > maxBytes { continue }
            let allOK = rule.all?.allSatisfy { text.contains($0.lowercased()) } ?? true
            let anyOK = rule.any?.isEmpty != false || (rule.any?.contains(where: { text.contains($0.lowercased()) }) ?? false)
            if allOK && anyOK { return rule.id }
        }
        return nil
    }

    private static func loadPortableRules(root: URL) -> PortableRuleSet? {
        ensurePortableLayout(at: root)
        guard let data = try? Data(contentsOf: portableRuleURL(root: root)) else { return nil }
        return try? JSONDecoder().decode(PortableRuleSet.self, from: data)
    }

    private static func clamScanPath(root: URL?) -> String? {
        let portable = root.map { portableClamRoot(root: $0).appendingPathComponent("clamscan").path }
        return ([portable].compactMap { $0 } + clamscanCandidates)
            .first(where: { FileManager.default.isExecutableFile(atPath: $0) })
    }

    private static func freshClamPath(root: URL?) -> String? {
        let portable = root.map { portableClamRoot(root: $0).appendingPathComponent("freshclam").path }
        return ([portable].compactMap { $0 } + freshclamCandidates)
            .first(where: { FileManager.default.isExecutableFile(atPath: $0) })
    }

    private static func portableClamRoot(root: URL) -> URL {
        #if arch(arm64)
        return root.appendingPathComponent("tools/mac-arm64/clamav", isDirectory: true)
        #else
        return root.appendingPathComponent("tools/mac-x64/clamav", isDirectory: true)
        #endif
    }

    private static func portableDatabaseURL(root: URL) -> URL {
        portableClamRoot(root: root).appendingPathComponent("database", isDirectory: true)
    }

    private static func portableFreshClamConfigURL(root: URL) -> URL {
        portableClamRoot(root: root).appendingPathComponent("freshclam.conf")
    }

    private static func portableRuleURL(root: URL) -> URL {
        root.appendingPathComponent("tools/rules/k3-rules.json")
    }

    private static func writeFreshClamConfigIfNeeded(root: URL) {
        let configURL = portableFreshClamConfigURL(root: root)
        guard !FileManager.default.fileExists(atPath: configURL.path) else { return }
        let config = """
        DatabaseDirectory database
        DatabaseMirror database.clamav.net
        UpdateLogFile freshclam.log
        LogTime yes

        """
        try? config.write(to: configURL, atomically: true, encoding: .utf8)
    }

    private static var defaultRuleData: Data {
        let json = """
        {
          "version": 1,
          "nameRules": [
            {
              "id": "K3.USB.FolderImpersonator",
              "extensions": ["exe", "scr", "com"],
              "exactNames": ["folder.exe", "new folder.exe", "my documents.exe", "documents.exe", "pictures.exe"],
              "suffixes": [".pdf.exe", ".doc.exe", ".docx.exe", ".xls.exe", ".xlsx.exe", ".jpg.exe", ".png.exe", ".txt.exe", ".zip.exe"]
            },
            {
              "id": "K3.USB.RiskyScriptOrShortcut",
              "extensions": ["bat", "cmd", "vbs", "vbe", "js", "jse", "wsf", "wsh", "hta", "ps1", "lnk", "reg", "scf", "url", "command"]
            }
          ],
          "contentRules": [
            {
              "id": "K3.Test.EICAR",
              "maxBytes": 2097152,
              "any": ["EICAR-STANDARD-ANTIVIRUS-TEST-FILE"]
            },
            {
              "id": "K3.Win.ApiInjection",
              "maxBytes": 2097152,
              "all": ["CreateRemoteThread", "VirtualAllocEx"]
            },
            {
              "id": "K3.Script.EncodedPowerShell",
              "extensions": ["bat", "cmd", "ps1", "vbs", "js", "hta", "txt"],
              "maxBytes": 2097152,
              "any": ["powershell -enc", "powershell.exe -enc", "-encodedcommand", "frombase64string"]
            },
            {
              "id": "K3.Script.DownloadAndExecute",
              "extensions": ["sh", "command", "bat", "cmd", "ps1", "js", "vbs", "hta", "txt"],
              "maxBytes": 2097152,
              "all": ["|"],
              "any": ["curl ", "wget ", "invoke-webrequest", "downloadstring("]
            },
            {
              "id": "K3.Mac.PersistenceLaunchAgent",
              "extensions": ["sh", "command", "plist", "txt"],
              "maxBytes": 2097152,
              "any": ["launchctl load", "/library/launchagents", "~/library/launchagents"]
            },
            {
              "id": "K3.USB.HideAndShortcut",
              "extensions": ["bat", "cmd", "vbs", "js", "ps1", "txt"],
              "maxBytes": 2097152,
              "all": ["attrib +h"],
              "any": [".lnk", "shortcut", "system volume information"]
            }
          ]
        }
        """
        return Data(json.utf8)
    }
}
