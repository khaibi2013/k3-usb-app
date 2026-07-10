import CryptoKit
import Foundation
import Security

struct TrustedFileEntry: Identifiable, Hashable {
    let id: String
    let hash: String
    let name: String
    let addedAt: String
}

enum K3TrustedFileManager {
    static func trustedFileURL(at root: URL) -> URL {
        root.appendingPathComponent(".k3_trusted_hashes.txt")
    }

    static func entries(at root: URL) -> [TrustedFileEntry] {
        let url = trustedFileURL(at: root)
        guard let text = try? String(contentsOf: url, encoding: .utf8) else { return [] }
        return text
            .split(whereSeparator: \.isNewline)
            .compactMap { parse(String($0)) }
            .sorted { $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending }
    }

    static func isTrusted(_ file: URL, root: URL) -> Bool {
        guard let hash = sha256(file) else { return false }
        return entries(at: root).contains { $0.hash.caseInsensitiveCompare(hash) == .orderedSame }
    }

    @discardableResult
    static func trust(_ file: URL, root: URL) throws -> Bool {
        guard let hash = sha256(file) else { return false }
        var values = entries(at: root)
        if values.contains(where: { $0.hash.caseInsensitiveCompare(hash) == .orderedSame }) {
            return true
        }
        values.append(TrustedFileEntry(id: hash, hash: hash, name: file.lastPathComponent, addedAt: timestamp()))
        try save(values, at: root)
        return true
    }

    static func remove(_ entry: TrustedFileEntry, root: URL) throws {
        try save(entries(at: root).filter { $0.hash != entry.hash }, at: root)
    }

    @discardableResult
    static func trustQuarantined(_ item: QuarantineItem, root: URL) throws -> Bool {
        guard let hash = sha256(item.quarantinedURL) else { return false }
        var values = entries(at: root)
        if values.contains(where: { $0.hash.caseInsensitiveCompare(hash) == .orderedSame }) {
            return true
        }
        values.append(TrustedFileEntry(id: hash, hash: hash, name: item.originalName, addedAt: timestamp()))
        try save(values, at: root)
        return true
    }

    static func sha256(_ file: URL) -> String? {
        guard let data = try? Data(contentsOf: file) else { return nil }
        return SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined()
    }

    private static func save(_ entries: [TrustedFileEntry], at root: URL) throws {
        let url = trustedFileURL(at: root)
        MacSystemTools.clearHidden(url)
        let text = entries
            .map { "\($0.hash)|\(escape($0.name))|\(escape($0.addedAt))" }
            .joined(separator: "\n")
        try text.write(to: url, atomically: true, encoding: .utf8)
        try MacSystemTools.hide(url)
    }

    private static func parse(_ line: String) -> TrustedFileEntry? {
        let trimmed = line.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }
        let parts = trimmed.split(separator: "|", omittingEmptySubsequences: false).map(String.init)
        let hash = parts[0].trimmingCharacters(in: .whitespacesAndNewlines)
        guard hash.count == 64 else { return nil }
        let name = parts.count > 1 ? unescape(parts[1]) : "(unknown)"
        let addedAt = parts.count > 2 ? unescape(parts[2]) : ""
        return TrustedFileEntry(id: hash, hash: hash, name: name, addedAt: addedAt)
    }

    private static func escape(_ value: String) -> String {
        value.replacingOccurrences(of: "\\", with: "\\\\").replacingOccurrences(of: "|", with: "\\p")
    }

    private static func unescape(_ value: String) -> String {
        value.replacingOccurrences(of: "\\p", with: "|").replacingOccurrences(of: "\\\\", with: "\\")
    }
}

struct QuarantineItem: Identifiable, Hashable {
    let id: String
    let originalPath: String
    let originalName: String
    let virusName: String
    let quarantineDate: String
    let size: Int64
    let quarantinedURL: URL
    let metadataURL: URL
}

enum K3QuarantineManager {
    static func quarantineDir(at root: URL) -> URL {
        root.appendingPathComponent(".k3_quarantine", isDirectory: true)
    }

    static func list(at root: URL) -> [QuarantineItem] {
        let dir = quarantineDir(at: root)
        guard let metas = try? FileManager.default.contentsOfDirectory(at: dir, includingPropertiesForKeys: nil).filter({ $0.pathExtension == "meta" }) else {
            return []
        }
        return metas.compactMap(parse).sorted { $0.quarantineDate > $1.quarantineDate }
    }

    @discardableResult
    static func quarantine(_ file: URL, virusName: String, root: URL) throws -> Bool {
        guard FileManager.default.fileExists(atPath: file.path) else { return false }
        let dir = quarantineDir(at: root)
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        try MacSystemTools.hide(dir)

        let id = UUID().uuidString
        let dest = dir.appendingPathComponent("\(id).k3q")
        try FileManager.default.moveItem(at: file, to: dest)

        let lines = [
            "OriginalPath=\(base64(file.path))",
            "OriginalName=\(base64(file.lastPathComponent))",
            "VirusName=\(base64(virusName))",
            "QuarantineDate=\(timestamp())",
            "Size=\((try? dest.resourceValues(forKeys: [.fileSizeKey]).fileSize) ?? 0)"
        ]
        try lines.joined(separator: "\n").write(to: dir.appendingPathComponent("\(id).meta"), atomically: true, encoding: .utf8)
        return true
    }

    static func restore(_ item: QuarantineItem) throws {
        let destination = URL(fileURLWithPath: item.originalPath)
        try FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
        let finalURL = availableURL(for: destination)
        try FileManager.default.moveItem(at: item.quarantinedURL, to: finalURL)
        try? FileManager.default.removeItem(at: item.metadataURL)
    }

    static func delete(_ item: QuarantineItem) throws {
        try? K3Maintenance.secureShredFile(item.quarantinedURL)
        try? FileManager.default.removeItem(at: item.metadataURL)
    }

    private static func parse(_ metadataURL: URL) -> QuarantineItem? {
        guard let text = try? String(contentsOf: metadataURL, encoding: .utf8) else { return nil }
        var values: [String: String] = [:]
        for line in text.split(whereSeparator: \.isNewline) {
            let parts = line.split(separator: "=", maxSplits: 1, omittingEmptySubsequences: false)
            if parts.count == 2 { values[String(parts[0])] = String(parts[1]) }
        }
        let id = metadataURL.deletingPathExtension().lastPathComponent
        let qURL = metadataURL.deletingLastPathComponent().appendingPathComponent("\(id).k3q")
        return QuarantineItem(
            id: id,
            originalPath: decode(values["OriginalPath"] ?? "") ?? "",
            originalName: decode(values["OriginalName"] ?? "") ?? id,
            virusName: decode(values["VirusName"] ?? "") ?? "",
            quarantineDate: values["QuarantineDate"] ?? "",
            size: Int64(values["Size"] ?? "0") ?? 0,
            quarantinedURL: qURL,
            metadataURL: metadataURL
        )
    }
}

struct ScanFinding: Identifiable, Hashable {
    let id = UUID()
    let url: URL
    let status: String
    let signature: String
}

struct VolumeUsage {
    let total: Int64
    let free: Int64

    var used: Int64 { max(0, total - free) }
}

enum K3ScanReportManager {
    static func reportRoot(at root: URL) -> URL {
        root.appendingPathComponent(".k3_scan_reports", isDirectory: true)
    }

    static func writeReport(findings: [ScanFinding], root: URL) throws -> URL {
        let reportDir = reportRoot(at: root)
        try FileManager.default.createDirectory(at: reportDir, withIntermediateDirectories: true)
        let reportURL = reportDir.appendingPathComponent("scan-report-\(fileTimestamp()).html")
        let threats = findings.filter { $0.status == "Threat" }.count
        let trusted = findings.filter { $0.status == "Trusted" }.count
        let clean = findings.filter { $0.status == "Clean" }.count
        let machine = Host.current().localizedName ?? ProcessInfo.processInfo.hostName
        let rows = findings.map { finding in
            """
            <tr class="\(finding.status.lowercased())">
              <td>\(escape(finding.url.lastPathComponent))</td>
              <td>\(escape(finding.url.path))</td>
              <td>\(escape(statusText(finding.status)))</td>
              <td>\(escape(finding.signature))</td>
            </tr>
            """
        }.joined(separator: "\n")

        let html = """
        <!doctype html>
        <html lang="vi">
        <head>
          <meta charset="utf-8">
          <title>K3 Scan Report</title>
          <style>
            body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; margin: 28px; color: #202124; }
            h1 { margin-bottom: 4px; }
            .meta { color: #666; margin-bottom: 22px; }
            .cards { display: grid; grid-template-columns: repeat(4, minmax(120px, 1fr)); gap: 12px; margin-bottom: 24px; }
            .card { border: 1px solid #ddd; border-radius: 8px; padding: 14px; }
            .value { font-size: 28px; font-weight: 700; }
            table { width: 100%; border-collapse: collapse; }
            th, td { text-align: left; border-bottom: 1px solid #e5e5e5; padding: 10px; vertical-align: top; }
            th { background: #f6f7f8; }
            tr.threat td { color: #b00020; }
            tr.trusted td { color: #0b57d0; }
            tr.clean td { color: #137333; }
          </style>
        </head>
        <body>
          <h1>USB An Toan K3 - Bao cao quet virus</h1>
          <div class="meta">
            Thoi gian: \(escape(timestamp()))<br>
            May quet: \(escape(machine))<br>
            USB: \(escape(root.path))
          </div>
          <div class="cards">
            <div class="card"><div class="value">\(findings.count)</div><div>Tong file</div></div>
            <div class="card"><div class="value">\(threats)</div><div>Nguy hiem</div></div>
            <div class="card"><div class="value">\(trusted)</div><div>Tin cay</div></div>
            <div class="card"><div class="value">\(clean)</div><div>Sach</div></div>
          </div>
          <table>
            <thead>
              <tr><th>Ten file</th><th>Duong dan</th><th>Trang thai</th><th>Chu ky</th></tr>
            </thead>
            <tbody>
              \(rows)
            </tbody>
          </table>
        </body>
        </html>
        """

        try html.write(to: reportURL, atomically: true, encoding: .utf8)
        try MacSystemTools.hide(reportDir)
        return reportURL
    }

    private static func statusText(_ status: String) -> String {
        switch status {
        case "Threat": return "Nguy hiem"
        case "Trusted": return "Tin cay"
        case "Clean": return "Sach"
        default: return status
        }
    }

    private static func escape(_ value: String) -> String {
        value
            .replacingOccurrences(of: "&", with: "&amp;")
            .replacingOccurrences(of: "<", with: "&lt;")
            .replacingOccurrences(of: ">", with: "&gt;")
            .replacingOccurrences(of: "\"", with: "&quot;")
    }
}

enum K3UsbVaccine {
    static func apply(at root: URL) throws -> (autorunProtected: Bool, quarantined: Int) {
        let autorun = root.appendingPathComponent("autorun.inf")
        let content = """
        [AutoRun]
        label=USB An Toan K3
        icon=AnToanUSB.exe
        action=Mo USB An Toan K3
        open=AnToanUSB.exe
        shellexecute=AnToanUSB.exe
        shell\\open=Mo USB An Toan K3
        shell\\open\\command=AnToanUSB.exe
        shell\\explore=Mo thu muc USB
        shell\\explore\\command=explorer.exe .
        """
        MacSystemTools.clearHidden(autorun)
        try content.write(to: autorun, atomically: true, encoding: .utf8)
        try MacSystemTools.hide(autorun)

        var quarantined = 0
        let suspicious = suspiciousRootItems(at: root)
        for file in suspicious {
            let result = K3MacScanner.scan(file, root: root)
            guard result.isThreat else { continue }
            if (try? K3QuarantineManager.quarantine(file, virusName: result.name, root: root)) == true {
                quarantined += 1
            }
        }

        try K3PortableLayout.hideSupportFiles(at: root)
        return (true, quarantined)
    }

    private static func suspiciousRootItems(at root: URL) -> [URL] {
        guard let items = try? FileManager.default.contentsOfDirectory(at: root, includingPropertiesForKeys: [.isRegularFileKey], options: []) else {
            return []
        }
        let riskyExtensions = ["lnk", "vbs", "vbe", "js", "jse", "wsf", "wsh", "hta", "scr", "pif", "com", "ps1", "cmd", "bat", "scf", "url"]
        return items.filter { url in
            guard (try? url.resourceValues(forKeys: [.isRegularFileKey]).isRegularFile) == true else { return false }
            if ["AnToanUSB.exe", "autorun.inf"].contains(url.lastPathComponent) { return false }
            return riskyExtensions.contains(url.pathExtension.lowercased())
        }
    }
}

struct RecoverySnapshotResult: Hashable {
    let snapshotURL: URL
    let itemCount: Int
    let fileCount: Int
    let bytes: Int64
}

enum K3RecoverySnapshotManager {
    static func snapshotRoot(at root: URL) -> URL {
        root.appendingPathComponent(".k3_recovery_snapshots", isDirectory: true)
    }

    static func createSnapshot(for urls: [URL], root: URL) throws -> RecoverySnapshotResult? {
        let files = urls.flatMap(snapshotFiles)
        guard !files.isEmpty else { return nil }

        let snapshotURL = snapshotRoot(at: root).appendingPathComponent(snapshotName(), isDirectory: true)
        try FileManager.default.createDirectory(at: snapshotURL, withIntermediateDirectories: true)
        var copied = 0
        var bytes: Int64 = 0

        for file in files {
            let relative = relativePath(for: file, root: root)
            let destination = snapshotURL.appendingPathComponent(relative)
            try FileManager.default.createDirectory(at: destination.deletingLastPathComponent(), withIntermediateDirectories: true)
            if !FileManager.default.fileExists(atPath: destination.path) {
                try FileManager.default.copyItem(at: file, to: destination)
                copied += 1
                bytes += Int64((try? file.resourceValues(forKeys: [.fileSizeKey]).fileSize) ?? 0)
            }
        }
        try MacSystemTools.hide(snapshotRoot(at: root))
        return RecoverySnapshotResult(snapshotURL: snapshotURL, itemCount: urls.count, fileCount: copied, bytes: bytes)
    }

    private static func snapshotFiles(_ url: URL) -> [URL] {
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: url.path, isDirectory: &isDirectory) else { return [] }
        if !isDirectory.boolValue { return [url] }
        guard let enumerator = FileManager.default.enumerator(at: url, includingPropertiesForKeys: [.isRegularFileKey], options: [.skipsHiddenFiles]) else { return [] }
        return enumerator.compactMap { item in
            guard let file = item as? URL,
                  (try? file.resourceValues(forKeys: [.isRegularFileKey]).isRegularFile) == true else { return nil }
            return file
        }
    }

    private static func relativePath(for file: URL, root: URL) -> String {
        let rootPath = root.path.hasSuffix("/") ? root.path : root.path + "/"
        if file.path.hasPrefix(rootPath) {
            return String(file.path.dropFirst(rootPath.count))
        }
        return file.lastPathComponent
    }

    private static func snapshotName() -> String {
        let formatter = DateFormatter()
        formatter.dateFormat = "yyyyMMdd-HHmmss"
        return formatter.string(from: Date())
    }
}

struct HistoryEntry: Identifiable, Hashable {
    let id = UUID()
    let timestamp: String
    let level: String
    let message: String
}

enum K3HistoryManager {
    static func url(at root: URL) -> URL {
        root.appendingPathComponent(".k3_history.log")
    }

    static func append(_ level: String, _ message: String, root: URL) {
        let line = "\(timestamp())|\(level)|\(message.replacingOccurrences(of: "\n", with: " "))\n"
        let fileURL = url(at: root)
        MacSystemTools.clearHidden(fileURL)
        if let data = line.data(using: .utf8) {
            if FileManager.default.fileExists(atPath: fileURL.path), let handle = try? FileHandle(forWritingTo: fileURL) {
                _ = try? handle.seekToEnd()
                try? handle.write(contentsOf: data)
                try? handle.close()
            } else {
                try? data.write(to: fileURL)
            }
        }
        try? MacSystemTools.hide(fileURL)
    }

    static func entries(at root: URL) -> [HistoryEntry] {
        guard let text = try? String(contentsOf: url(at: root), encoding: .utf8) else { return [] }
        return text.split(whereSeparator: \.isNewline).compactMap { line in
            let parts = String(line).split(separator: "|", maxSplits: 2, omittingEmptySubsequences: false).map(String.init)
            guard parts.count == 3 else { return nil }
            return HistoryEntry(timestamp: parts[0], level: parts[1], message: parts[2])
        }.reversed()
    }

    static func clear(at root: URL) throws {
        try? FileManager.default.removeItem(at: url(at: root))
    }
}

enum K3Maintenance {
    static func secureShredFile(_ file: URL) throws {
        guard FileManager.default.fileExists(atPath: file.path) else { return }
        let size = Int64((try? file.resourceValues(forKeys: [.fileSizeKey]).fileSize) ?? 0)
        if size > 0, let handle = try? FileHandle(forUpdating: file) {
            defer { try? handle.close() }
            let blockSize = 1024 * 1024
            let patterns: [UInt8?] = [0x00, 0xff, nil]
            for pattern in patterns {
                try handle.seek(toOffset: 0)
                var remaining = size
                while remaining > 0 {
                    let count = min(blockSize, Int(remaining))
                    let data: Data
                    if let byte = pattern {
                        data = Data(repeating: byte, count: count)
                    } else {
                        var random = Data(repeating: 0, count: count)
                        _ = random.withUnsafeMutableBytes {
                            SecRandomCopyBytes(kSecRandomDefault, count, $0.baseAddress!)
                        }
                        data = random
                    }
                    try handle.write(contentsOf: data)
                    remaining -= Int64(count)
                }
                try handle.synchronize()
            }
        }
        let renamed = file.deletingLastPathComponent().appendingPathComponent(UUID().uuidString + ".tmp")
        try? FileManager.default.moveItem(at: file, to: renamed)
        try? FileManager.default.removeItem(at: renamed)
        if FileManager.default.fileExists(atPath: file.path) {
            try FileManager.default.removeItem(at: file)
        }
    }

    static func cleanupMacMetadata(at root: URL) throws -> Int {
        var removed = 0
        let keys: [URLResourceKey] = [.isRegularFileKey]
        guard let enumerator = FileManager.default.enumerator(at: root, includingPropertiesForKeys: keys) else { return 0 }
        for case let url as URL in enumerator {
            let name = url.lastPathComponent
            if name == ".DS_Store" || name.hasPrefix("._") {
                try? FileManager.default.removeItem(at: url)
                removed += 1
            }
        }
        return removed
    }

    static func verifyVolume(at root: URL) throws -> String {
        try MacSystemTools.run("/usr/sbin/diskutil", ["verifyVolume", root.path])
    }

    static func repairPortableLayout(at root: URL) throws {
        try K3PortableLayout.ensure(at: root)
        try K3PortableLayout.hideSupportFiles(at: root)
        try MacSystemTools.hide(K3QuarantineManager.quarantineDir(at: root))
        try MacSystemTools.hide(K3RecoverySnapshotManager.snapshotRoot(at: root))
        try MacSystemTools.hide(K3TrustedFileManager.trustedFileURL(at: root))
        try MacSystemTools.hide(K3HistoryManager.url(at: root))
    }
}

enum K3DataWiper {
    static func wipeRealVaultOnly(at root: URL) throws {
        try wipe(root.appendingPathComponent(".vault", isDirectory: true))
    }

    static func wipeSensitiveData(at root: URL) throws {
        let targets = [
            ".vault",
            ".vault_decoy",
            "BaoMat",
            ".k3_quarantine",
            ".k3_recovery_snapshots",
            ".k3_trusted_hashes.txt",
            ".k3_history.log",
            ".vault_config.json"
        ]

        for name in targets {
            try wipe(root.appendingPathComponent(name))
        }
    }

    private static func wipe(_ url: URL) throws {
        guard FileManager.default.fileExists(atPath: url.path) else { return }
        let values = try? url.resourceValues(forKeys: [.isDirectoryKey, .isRegularFileKey])

        if values?.isDirectory == true {
            if let children = try? FileManager.default.contentsOfDirectory(at: url, includingPropertiesForKeys: [.isDirectoryKey, .isRegularFileKey], options: []) {
                for child in children {
                    try wipe(child)
                }
            }
            try? FileManager.default.removeItem(at: url)
            return
        }

        if values?.isRegularFile == true {
            try? K3Maintenance.secureShredFile(url)
        }
        if FileManager.default.fileExists(atPath: url.path) {
            try? FileManager.default.removeItem(at: url)
        }
    }
}

private func timestamp() -> String {
    let formatter = DateFormatter()
    formatter.dateFormat = "yyyy-MM-dd HH:mm:ss"
    return formatter.string(from: Date())
}

private func fileTimestamp() -> String {
    let formatter = DateFormatter()
    formatter.dateFormat = "yyyyMMdd-HHmmss"
    return formatter.string(from: Date())
}

private func base64(_ value: String) -> String {
    Data(value.utf8).base64EncodedString()
}

private func decode(_ value: String) -> String? {
    guard let data = Data(base64Encoded: value) else { return nil }
    return String(data: data, encoding: .utf8)
}

private func availableURL(for url: URL) -> URL {
    if !FileManager.default.fileExists(atPath: url.path) { return url }
    let directory = url.deletingLastPathComponent()
    let ext = url.pathExtension
    let base = url.deletingPathExtension().lastPathComponent
    var index = 1
    while true {
        let candidate = directory.appendingPathComponent("\(base) (\(index))").appendingPathExtension(ext)
        if !FileManager.default.fileExists(atPath: candidate.path) { return candidate }
        index += 1
    }
}
