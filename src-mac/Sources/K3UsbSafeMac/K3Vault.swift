import Foundation

struct VaultItem: Identifiable, Hashable {
    let id = UUID()
    let url: URL
    let displayName: String
    let isFolderPackage: Bool
    let size: Int64
    let modified: Date
}

struct K3FolderViewSession {
    let packageURL: URL
    let tempRoot: URL
    let root: URL
    let displayName: String
}

enum K3Vault {
    static func listEncryptedFiles(in vault: URL) throws -> [VaultItem] {
        if !FileManager.default.fileExists(atPath: vault.path) {
            try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        }
        let urls = try FileManager.default.contentsOfDirectory(at: vault, includingPropertiesForKeys: [.fileSizeKey, .contentModificationDateKey], options: [.skipsHiddenFiles])
        return urls
            .filter { ["k3enc", "k3folder"].contains($0.pathExtension.lowercased()) }
            .map { url in
                let values = try? url.resourceValues(forKeys: [.fileSizeKey, .contentModificationDateKey])
                let name = url.deletingPathExtension().lastPathComponent
                return VaultItem(
                    url: url,
                    displayName: name,
                    isFolderPackage: url.pathExtension.lowercased() == "k3folder",
                    size: Int64(values?.fileSize ?? 0),
                    modified: values?.contentModificationDate ?? .distantPast
                )
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

    static func encryptFolderPackage(folder: URL, into vault: URL, crypto: K3Crypto) throws {
        if !FileManager.default.fileExists(atPath: vault.path) {
            try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
        }
        let didAccessFolder = folder.startAccessingSecurityScopedResource()
        defer {
            if didAccessFolder {
                folder.stopAccessingSecurityScopedResource()
            }
        }

        let folderName = try safeFileName(folder.lastPathComponent)
        let tempRoot = FileManager.default.temporaryDirectory
            .appendingPathComponent("K3FolderPackage-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: tempRoot, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempRoot) }

        let tempZip = tempRoot.appendingPathComponent("\(folderName).zip")
        let tempEncrypted = tempRoot.appendingPathComponent("\(folderName).k3folder")
        try createZip(from: folder, to: tempZip)
        try crypto.encrypt(file: tempZip, to: tempEncrypted, storedName: "\(folderName).zip")
        try crypto.verify(file: tempEncrypted)

        let destination = availableURL(for: vault.appendingPathComponent("\(folderName).k3folder"))
        do {
            try FileManager.default.copyItem(at: tempEncrypted, to: destination)
            guard fileSize(tempEncrypted) == fileSize(destination),
                  K3TrustedFileManager.sha256(tempEncrypted) == K3TrustedFileManager.sha256(destination) else {
                throw K3Error.userFacing("Copy package sang USB khong toan ven.")
            }
            try crypto.verify(file: destination)
            try MacSystemTools.hide(vault)
        } catch {
            try? FileManager.default.removeItem(at: destination)
            throw error
        }
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
        if file.pathExtension.lowercased() == "k3folder" {
            try decryptFolderPackage(file: file, to: folder, crypto: crypto)
        } else {
            _ = try crypto.decrypt(file: file, to: folder)
        }
    }

    static func openFolderPackageView(file: URL, crypto: K3Crypto) throws -> K3FolderViewSession {
        guard file.pathExtension.lowercased() == "k3folder" else {
            throw K3Error.userFacing("Muc da chon khong phai .k3folder.")
        }

        let tempRoot = FileManager.default.temporaryDirectory
            .appendingPathComponent("K3FolderView_\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: tempRoot, withIntermediateDirectories: true)

        do {
            let zipURL = try crypto.decrypt(file: file, to: tempRoot)
            guard zipURL.pathExtension.lowercased() == "zip" else {
                throw K3Error.userFacing(".k3folder khong chua metadata zip hop le.")
            }
            try validateZipEntries(zipURL)

            let folderName = try safeFileName(zipURL.deletingPathExtension().lastPathComponent)
            let outputRoot = tempRoot.appendingPathComponent(folderName, isDirectory: true)
            try FileManager.default.createDirectory(at: outputRoot, withIntermediateDirectories: true)
            _ = try MacSystemTools.run("/usr/bin/ditto", ["-x", "-k", "--norsrc", zipURL.path, outputRoot.path])
            try validateExtractedTree(outputRoot, inside: tempRoot)
            return K3FolderViewSession(packageURL: file, tempRoot: tempRoot, root: outputRoot, displayName: folderName)
        } catch {
            try? FileManager.default.removeItem(at: tempRoot)
            throw error
        }
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

    private static func availableDirectoryURL(for url: URL) -> URL {
        if !FileManager.default.fileExists(atPath: url.path) { return url }
        let directory = url.deletingLastPathComponent()
        let base = url.lastPathComponent
        var index = 1
        while true {
            let candidate = directory.appendingPathComponent("\(base) (\(index))", isDirectory: true)
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

    private static func safeFileName(_ value: String) throws -> String {
        let safe = try safeRelativePath(value)
        guard !safe.contains("/") else { throw K3Error.userFacing("Ten thu muc khong hop le.") }
        return safe
    }

    private static func createZip(from folder: URL, to zipURL: URL) throws {
        _ = try MacSystemTools.run("/usr/bin/ditto", ["-c", "-k", "--norsrc", folder.path, zipURL.path])
    }

    private static func decryptFolderPackage(file: URL, to folder: URL, crypto: K3Crypto) throws {
        let tempRoot = FileManager.default.temporaryDirectory
            .appendingPathComponent("K3FolderDecrypt-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: tempRoot, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempRoot) }

        let zipURL = try crypto.decrypt(file: file, to: tempRoot)
        guard zipURL.pathExtension.lowercased() == "zip" else {
            throw K3Error.userFacing(".k3folder khong chua metadata zip hop le.")
        }

        let folderName = try safeFileName(zipURL.deletingPathExtension().lastPathComponent)
        let outputRoot = availableDirectoryURL(for: folder.appendingPathComponent(folderName, isDirectory: true))
        try validateZipEntries(zipURL)
        try FileManager.default.createDirectory(at: outputRoot, withIntermediateDirectories: true)
        do {
            _ = try MacSystemTools.run("/usr/bin/ditto", ["-x", "-k", "--norsrc", zipURL.path, outputRoot.path])
            try validateExtractedTree(outputRoot, inside: folder)
        } catch {
            try? FileManager.default.removeItem(at: outputRoot)
            throw error
        }
    }

    private static func fileSize(_ url: URL) -> Int64 {
        Int64((try? url.resourceValues(forKeys: [.fileSizeKey]).fileSize) ?? -1)
    }

    private static func validateZipEntries(_ zipURL: URL) throws {
        let output = try zipEntryList(zipURL)
        for rawLine in output.split(whereSeparator: \.isNewline) {
            let entry = String(rawLine)
                .trimmingCharacters(in: .whitespacesAndNewlines)
                .replacingOccurrences(of: "\\", with: "/")
            if entry.isEmpty { continue }
            guard !entry.hasPrefix("/") && !entry.contains("\0") else {
                throw K3Error.userFacing("Zip trong .k3folder co duong dan khong an toan.")
            }
            let parts = entry.split(separator: "/", omittingEmptySubsequences: true).map(String.init)
            guard !parts.isEmpty else { continue }
            for part in parts {
                if part == "." || part == ".." {
                    throw K3Error.userFacing("Zip trong .k3folder co path traversal.")
                }
            }
        }
    }

    private static func zipEntryList(_ zipURL: URL) throws -> String {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/zipinfo")
        process.arguments = ["-1", zipURL.path]
        let pipe = Pipe()
        process.standardOutput = pipe
        process.standardError = pipe
        try process.run()
        process.waitUntilExit()
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        let output = String(data: data, encoding: .utf8) ?? ""
        if process.terminationStatus == 0 {
            return output
        }
        let lower = output.lowercased()
        if lower.contains("empty zipfile") || lower.contains("zipfile is empty") {
            return ""
        }
        throw K3Error.userFacing(output.isEmpty ? "Khong doc duoc danh sach zip." : output)
    }

    private static func validateExtractedTree(_ root: URL, inside container: URL) throws {
        let containerPath = container.standardizedFileURL.path
        let prefix = containerPath.hasSuffix("/") ? containerPath : containerPath + "/"
        guard root.standardizedFileURL.path.hasPrefix(prefix) else {
            throw K3Error.userFacing("Thu muc bung package khong an toan.")
        }
        guard let enumerator = FileManager.default.enumerator(at: root, includingPropertiesForKeys: [.isSymbolicLinkKey], options: []) else {
            return
        }
        for case let url as URL in enumerator {
            let path = url.standardizedFileURL.path
            guard path.hasPrefix(prefix) else {
                throw K3Error.userFacing("Package giai nen ra ngoai temp.")
            }
        }
    }
}
