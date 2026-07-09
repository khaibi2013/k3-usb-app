import Foundation

struct VaultItem: Identifiable, Hashable {
    let id = UUID()
    let url: URL
    let displayName: String
    let size: Int64
    let modified: Date
}

enum K3Vault {
    static func listEncryptedFiles(in vault: URL) throws -> [VaultItem] {
        if !FileManager.default.fileExists(atPath: vault.path) {
            try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        }
        let urls = try FileManager.default.contentsOfDirectory(at: vault, includingPropertiesForKeys: [.fileSizeKey, .contentModificationDateKey], options: [.skipsHiddenFiles])
        return urls
            .filter { $0.pathExtension.lowercased() == "k3enc" }
            .map { url in
                let values = try? url.resourceValues(forKeys: [.fileSizeKey, .contentModificationDateKey])
                let name = url.deletingPathExtension().lastPathComponent
                return VaultItem(url: url, displayName: name, size: Int64(values?.fileSize ?? 0), modified: values?.contentModificationDate ?? .distantPast)
            }
            .sorted { $0.displayName.localizedCaseInsensitiveCompare($1.displayName) == .orderedAscending }
    }

    static func encrypt(file: URL, into vault: URL, crypto: K3Crypto) throws {
        if !FileManager.default.fileExists(atPath: vault.path) {
            try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        }
        let destination = availableURL(for: vault.appendingPathComponent(file.lastPathComponent + ".k3enc"))
        try crypto.encrypt(file: file, to: destination)
        try MacSystemTools.hide(vault)
    }

    static func decrypt(file: URL, to folder: URL, crypto: K3Crypto) throws {
        if !FileManager.default.fileExists(atPath: folder.path) {
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        }
        _ = try crypto.decrypt(file: file, to: folder)
    }

    private static func availableURL(for url: URL) -> URL {
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
}
