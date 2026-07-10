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

    static func encrypt(file: URL, into vault: URL, crypto: K3Crypto, storedName: String? = nil) throws {
        if !FileManager.default.fileExists(atPath: vault.path) {
            try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        }
        let didAccessFile = file.startAccessingSecurityScopedResource()
        defer {
            if didAccessFile {
                file.stopAccessingSecurityScopedResource()
            }
        }

        let archiveName = flatArchiveName(for: storedName ?? file.lastPathComponent)
        let destination = availableURL(for: vault.appendingPathComponent(archiveName + ".k3enc"))
        try crypto.encrypt(file: file, to: destination, storedName: storedName)
        try MacSystemTools.hide(vault)
    }

    static func decrypt(file: URL, to folder: URL, crypto: K3Crypto) throws {
        let didAccessFolder = folder.startAccessingSecurityScopedResource()
        defer {
            if didAccessFolder {
                folder.stopAccessingSecurityScopedResource()
            }
        }

        if !FileManager.default.fileExists(atPath: folder.path) {
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        }
        _ = try crypto.decrypt(file: file, to: folder)
    }

    static func storedName(for file: URL, inside folder: URL, includeRootFolder: Bool = true) throws -> String {
        let folderPath = folder.standardizedFileURL.path
        let filePath = file.standardizedFileURL.path
        let prefix = folderPath.hasSuffix("/") ? folderPath : folderPath + "/"
        guard filePath.hasPrefix(prefix) else {
            throw K3Error.userFacing("File khong nam trong thu muc da chon.")
        }
        let relative = String(filePath.dropFirst(prefix.count))
        let safeRelative = try safeRelativePath(relative)
        return includeRootFolder ? try safeRelativePath(folder.lastPathComponent + "/" + safeRelative) : safeRelative
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

    private static func flatArchiveName(for storedName: String) -> String {
        storedName
            .replacingOccurrences(of: "\\", with: "/")
            .split(separator: "/", omittingEmptySubsequences: true)
            .joined(separator: "__")
            .replacingOccurrences(of: ":", with: "_")
    }

    private static func safeRelativePath(_ value: String) throws -> String {
        let normalized = value.replacingOccurrences(of: "\\", with: "/")
        guard !normalized.hasPrefix("/") else { throw K3Error.userFacing("Ten thu muc khong hop le.") }
        let parts = normalized.split(separator: "/", omittingEmptySubsequences: false).map(String.init)
        guard !parts.isEmpty else { throw K3Error.userFacing("Ten thu muc khong hop le.") }
        for part in parts {
            if part.isEmpty || part == "." || part == ".." {
                throw K3Error.userFacing("Ten thu muc khong hop le.")
            }
        }
        return parts.joined(separator: "/")
    }
}
