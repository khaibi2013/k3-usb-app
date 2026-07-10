import Foundation
import AppKit
import SwiftUI

final class AppState: ObservableObject {
    @Published var usbRoot: URL
    @Published var config: K3Config
    @Published var isAuthenticated = false
    @Published var isDecoyMode = false
    @Published var statusMessage = "Disconnected"
    @Published var vaultFiles: [VaultItem] = []
    @Published var scanFindings: [ScanFinding] = []
    @Published var quarantineItems: [QuarantineItem] = []
    @Published var trustedFiles: [TrustedFileEntry] = []
    @Published var historyEntries: [HistoryEntry] = []
    @Published var securityRows: [SecurityRow] = []

    private var crypto: K3Crypto?

    init() {
        let cwd = URL(fileURLWithPath: FileManager.default.currentDirectoryPath, isDirectory: true)
        self.usbRoot = cwd
        self.config = K3Config.defaultConfig()
    }

    func bootstrap() {
        do {
            config = try K3ConfigStore.loadOrCreate(at: usbRoot)
            try K3PortableLayout.ensure(at: usbRoot)
            try K3PortableLayout.hideSupportFiles(at: usbRoot)
            reloadFeatureData()
            statusMessage = "Ready"
        } catch {
            statusMessage = "Setup warning: \(error.localizedDescription)"
        }
    }

    func setupPassword(realPassword: String, decoyPassword: String) throws {
        config.realHash = try K3PasswordHasher.hash(realPassword)
        config.decoyHash = decoyPassword.isEmpty ? "" : try K3PasswordHasher.hash(decoyPassword)
        config.cryptoSalt = K3PasswordHasher.randomSaltBase64()
        config.needsInitialSetup = false
        try K3ConfigStore.save(config, at: usbRoot)
        try login(password: realPassword)
        K3HistoryManager.append("INFO", "Initial password setup completed", root: usbRoot)
    }

    func login(password: String) throws {
        if K3PasswordHasher.verify(password, stored: config.decoyHash) {
            isDecoyMode = true
        } else if K3PasswordHasher.verify(password, stored: config.realHash) {
            isDecoyMode = false
        } else {
            throw K3Error.userFacing("Wrong password.")
        }

        crypto = try K3Crypto(password: password, cryptoSaltBase64: config.cryptoSalt)
        isAuthenticated = true
        statusMessage = "Connected"
        K3HistoryManager.append("INFO", isDecoyMode ? "Logged in to decoy vault" : "Logged in to secure vault", root: usbRoot)
        refreshVault()
        reloadFeatureData()
    }

    func logout() {
        K3HistoryManager.append("INFO", "Logged out", root: usbRoot)
        crypto = nil
        isAuthenticated = false
        isDecoyMode = false
        vaultFiles = []
        statusMessage = "Disconnected"
    }

    var vaultURL: URL {
        usbRoot.appendingPathComponent(isDecoyMode ? ".vault_decoy" : ".vault", isDirectory: true)
    }

    var baoMatURL: URL {
        usbRoot.appendingPathComponent("BaoMat", isDirectory: true)
    }

    func refreshVault() {
        do {
            vaultFiles = try K3Vault.listEncryptedFiles(in: vaultURL)
            reloadSecurityRows()
        } catch {
            vaultFiles = []
            statusMessage = "Vault refresh failed: \(error.localizedDescription)"
        }
    }

    func encryptIntoVault(files: [URL]) {
        guard let crypto else { return }
        do {
            for file in files {
                if K3TrustedFileManager.isTrusted(file, root: usbRoot) {
                    K3HistoryManager.append("INFO", "Trusted file skipped by scanner: \(file.lastPathComponent)", root: usbRoot)
                    try K3Vault.encrypt(file: file, into: vaultURL, crypto: crypto)
                    continue
                }
                let scan = K3MacScanner.scan(file)
                if scan.isThreat {
                    statusMessage = "Blocked: \(file.lastPathComponent) - \(scan.name)"
                    scanFindings.append(ScanFinding(url: file, status: "Threat", signature: scan.name))
                    K3HistoryManager.append("WARN", statusMessage, root: usbRoot)
                    continue
                }
                try K3Vault.encrypt(file: file, into: vaultURL, crypto: crypto)
                K3HistoryManager.append("INFO", "Encrypted \(file.lastPathComponent)", root: usbRoot)
            }
            refreshVault()
            refreshHistory()
            statusMessage = "Encrypted \(files.count) item(s)."
        } catch {
            statusMessage = "Encrypt failed: \(error.localizedDescription)"
        }
    }

    func createAndEncryptTextNote(title: String, content: String) {
        guard !title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            statusMessage = "Note title is required."
            return
        }
        guard let crypto else { return }
        do {
            let safeTitle = sanitizedFileName(title)
            let temporaryDirectory = FileManager.default.temporaryDirectory
                .appendingPathComponent("K3Note-\(UUID().uuidString)", isDirectory: true)
            try FileManager.default.createDirectory(at: temporaryDirectory, withIntermediateDirectories: true)
            defer { try? FileManager.default.removeItem(at: temporaryDirectory) }

            let noteURL = temporaryDirectory.appendingPathComponent("\(safeTitle).txt")
            try content.write(to: noteURL, atomically: true, encoding: .utf8)
            try K3Vault.encrypt(file: noteURL, into: vaultURL, crypto: crypto)
            refreshVault()
            statusMessage = "Encrypted note \(safeTitle).txt."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Create note failed: \(error.localizedDescription)"
        }
    }

    func encryptBaoMatFiles() {
        do {
            try FileManager.default.createDirectory(at: baoMatURL, withIntermediateDirectories: true)
            let files = scanFiles(at: baoMatURL).filter { $0.pathExtension.lowercased() != "k3enc" }
            guard !files.isEmpty else {
                statusMessage = "BaoMat has no files to encrypt."
                return
            }
            encryptIntoVault(files: files)
        } catch {
            statusMessage = "BaoMat encrypt failed: \(error.localizedDescription)"
        }
    }

    func openBaoMatInFinder() {
        do {
            try FileManager.default.createDirectory(at: baoMatURL, withIntermediateDirectories: true)
            MacSystemTools.clearHidden(baoMatURL)
            NSWorkspace.shared.open(baoMatURL)
            statusMessage = "Opened BaoMat folder."
        } catch {
            statusMessage = "Open BaoMat failed: \(error.localizedDescription)"
        }
    }

    func decrypt(_ item: VaultItem, to outputFolder: URL) {
        guard let crypto else { return }
        do {
            try K3Vault.decrypt(file: item.url, to: outputFolder, crypto: crypto)
            statusMessage = "Decrypted \(item.displayName)."
            K3HistoryManager.append("INFO", "Decrypted \(item.displayName)", root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Decrypt failed: \(error.localizedDescription)"
        }
    }

    func ejectUsb() {
        do {
            if config.wipeMacos == "true" {
                _ = try? K3Maintenance.cleanupMacMetadata(at: usbRoot)
            }
            if config.wipeHistory == "true" {
                try? K3HistoryManager.clear(at: usbRoot)
            }
            logout()
            try MacSystemTools.ejectVolume(at: usbRoot)
        } catch {
            statusMessage = "Eject failed: \(error.localizedDescription)"
        }
    }

    func reloadFeatureData() {
        refreshTrustedFiles()
        refreshQuarantine()
        refreshHistory()
        reloadSecurityRows()
    }

    func scan(urls: [URL]) {
        var findings: [ScanFinding] = []
        for url in urls {
            let didAccess = url.startAccessingSecurityScopedResource()
            defer {
                if didAccess { url.stopAccessingSecurityScopedResource() }
            }
            let files = scanFiles(at: url)
            for file in files {
                if K3TrustedFileManager.isTrusted(file, root: usbRoot) {
                    findings.append(ScanFinding(url: file, status: "Trusted", signature: "SHA-256 allowlist"))
                    continue
                }
                let result = K3MacScanner.scan(file)
                findings.append(ScanFinding(url: file, status: result.isThreat ? "Threat" : "Clean", signature: result.name))
            }
        }
        scanFindings = findings
        let threats = findings.filter { $0.status == "Threat" }.count
        statusMessage = "Scan complete: \(threats) threat(s), \(findings.count) file(s)."
        K3HistoryManager.append(threats == 0 ? "INFO" : "WARN", statusMessage, root: usbRoot)
        refreshHistory()
    }

    func quarantine(_ finding: ScanFinding) {
        do {
            try K3QuarantineManager.quarantine(finding.url, virusName: finding.signature, root: usbRoot)
            statusMessage = "Quarantined \(finding.url.lastPathComponent)."
            K3HistoryManager.append("WARN", statusMessage, root: usbRoot)
            scanFindings.removeAll { $0.id == finding.id }
            refreshQuarantine()
            refreshHistory()
        } catch {
            statusMessage = "Quarantine failed: \(error.localizedDescription)"
        }
    }

    func restoreQuarantine(_ item: QuarantineItem) {
        do {
            try K3QuarantineManager.restore(item)
            statusMessage = "Restored \(item.originalName)."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            refreshQuarantine()
            refreshHistory()
        } catch {
            statusMessage = "Restore failed: \(error.localizedDescription)"
        }
    }

    func deleteQuarantine(_ item: QuarantineItem) {
        do {
            try K3QuarantineManager.delete(item)
            statusMessage = "Deleted quarantined \(item.originalName)."
            K3HistoryManager.append("WARN", statusMessage, root: usbRoot)
            refreshQuarantine()
            refreshHistory()
        } catch {
            statusMessage = "Delete failed: \(error.localizedDescription)"
        }
    }

    func refreshQuarantine() {
        quarantineItems = K3QuarantineManager.list(at: usbRoot)
    }

    func trust(files: [URL]) {
        do {
            for file in files {
                let didAccess = file.startAccessingSecurityScopedResource()
                defer {
                    if didAccess { file.stopAccessingSecurityScopedResource() }
                }
                _ = try K3TrustedFileManager.trust(file, root: usbRoot)
                K3HistoryManager.append("INFO", "Trusted \(file.lastPathComponent)", root: usbRoot)
            }
            refreshTrustedFiles()
            refreshHistory()
            statusMessage = "Trusted list updated."
        } catch {
            statusMessage = "Trust failed: \(error.localizedDescription)"
        }
    }

    func removeTrusted(_ entry: TrustedFileEntry) {
        do {
            try K3TrustedFileManager.remove(entry, root: usbRoot)
            refreshTrustedFiles()
            statusMessage = "Trusted entry removed."
        } catch {
            statusMessage = "Remove trusted failed: \(error.localizedDescription)"
        }
    }

    func refreshTrustedFiles() {
        trustedFiles = K3TrustedFileManager.entries(at: usbRoot)
    }

    func verifyVaultIntegrity() {
        guard let crypto else { return }
        do {
            let files = try K3Vault.listEncryptedFiles(in: vaultURL)
            var ok = 0
            var bad = 0
            for file in files {
                do {
                    try crypto.verify(file: file.url)
                    ok += 1
                } catch {
                    bad += 1
                }
            }
            statusMessage = "Vault verify: \(ok) valid, \(bad) failed."
            K3HistoryManager.append(bad == 0 ? "INFO" : "WARN", statusMessage, root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Vault verify failed: \(error.localizedDescription)"
        }
    }

    func cleanupMacMetadata() {
        do {
            let count = try K3Maintenance.cleanupMacMetadata(at: usbRoot)
            statusMessage = "Removed \(count) macOS metadata file(s)."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Cleanup failed: \(error.localizedDescription)"
        }
    }

    func repairPortableLayout() {
        do {
            try K3Maintenance.repairPortableLayout(at: usbRoot)
            reloadFeatureData()
            statusMessage = "Portable layout repaired."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Repair failed: \(error.localizedDescription)"
        }
    }

    func verifyVolume() {
        do {
            let output = try K3Maintenance.verifyVolume(at: usbRoot)
            statusMessage = output.split(whereSeparator: \.isNewline).last.map(String.init) ?? "Volume verify complete."
            K3HistoryManager.append("INFO", "diskutil verifyVolume completed", root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Volume verify failed: \(error.localizedDescription)"
        }
    }

    func saveSettings(
        loginTitle: String,
        loginHelp: String,
        hideLoginHelp: Bool,
        autoEncryptFolder: String,
        maxSizeBytes: String,
        autoDecrypt: Bool,
        showHidden: Bool,
        wipeHistory: Bool,
        wipeMacos: Bool
    ) {
        do {
            config.loginTitle = loginTitle.isEmpty ? "USB An Toan K3" : loginTitle
            config.loginHelp = loginHelp.isEmpty ? "Tro giup HELP!" : loginHelp
            config.hideLoginHelp = hideLoginHelp ? "true" : "false"
            config.autoEncryptFolder = autoEncryptFolder
            config.maxSizeBytes = maxSizeBytes
            config.autoDecrypt = autoDecrypt ? "true" : "false"
            config.showHidden = showHidden ? "true" : "false"
            config.wipeHistory = wipeHistory ? "true" : "false"
            config.wipeMacos = wipeMacos ? "true" : "false"
            try K3ConfigStore.save(config, at: usbRoot)
            statusMessage = "Settings saved."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            reloadFeatureData()
        } catch {
            statusMessage = "Settings save failed: \(error.localizedDescription)"
        }
    }

    func changePasswords(realPassword: String, decoyPassword: String) {
        do {
            guard realPassword.count >= 6 else { throw K3Error.userFacing("Real password must be at least 6 characters.") }
            guard realPassword != decoyPassword else { throw K3Error.userFacing("Decoy password must differ from real password.") }
            config.realHash = try K3PasswordHasher.hash(realPassword)
            config.decoyHash = decoyPassword.isEmpty ? "" : try K3PasswordHasher.hash(decoyPassword)
            try K3ConfigStore.save(config, at: usbRoot)
            crypto = try K3Crypto(password: realPassword, cryptoSaltBase64: config.cryptoSalt)
            isDecoyMode = false
            statusMessage = "Passwords updated."
            K3HistoryManager.append("WARN", statusMessage, root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Password update failed: \(error.localizedDescription)"
        }
    }

    func refreshHistory() {
        historyEntries = K3HistoryManager.entries(at: usbRoot)
    }

    func clearHistory() {
        do {
            try K3HistoryManager.clear(at: usbRoot)
            historyEntries = []
            statusMessage = "History cleared."
        } catch {
            statusMessage = "Clear history failed: \(error.localizedDescription)"
        }
    }

    private func reloadSecurityRows() {
        let configURL = K3ConfigStore.configURL(at: usbRoot)
        let realVault = usbRoot.appendingPathComponent(".vault", isDirectory: true)
        let decoyVault = usbRoot.appendingPathComponent(".vault_decoy", isDirectory: true)
        let baoMat = usbRoot.appendingPathComponent("BaoMat", isDirectory: true)
        securityRows = [
            SecurityRow(name: "Secure vault", status: exists(realVault) ? "Ready" : "Missing", suggestion: ".vault"),
            SecurityRow(name: "Decoy vault", status: exists(decoyVault) ? "Ready" : "Missing", suggestion: ".vault_decoy"),
            SecurityRow(name: "BaoMat folder", status: exists(baoMat) ? "Ready" : "Missing", suggestion: "Auto-encrypt staging folder"),
            SecurityRow(name: "Security config", status: exists(configURL) ? "Ready" : "Missing", suggestion: ".vault_config.json"),
            SecurityRow(name: "Trusted hashes", status: "\(trustedFiles.count) item(s)", suggestion: ".k3_trusted_hashes.txt"),
            SecurityRow(name: "Quarantine", status: "\(quarantineItems.count) item(s)", suggestion: ".k3_quarantine"),
            SecurityRow(name: "Encrypted files", status: "\(vaultFiles.count) item(s)", suggestion: isDecoyMode ? "Decoy mode" : "Real vault")
        ]
    }

    private func exists(_ url: URL) -> Bool {
        FileManager.default.fileExists(atPath: url.path)
    }

    private func scanFiles(at url: URL) -> [URL] {
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

    private func sanitizedFileName(_ value: String) -> String {
        let invalid = CharacterSet(charactersIn: "/\\?%*|\"<>:")
        let cleaned = value
            .components(separatedBy: invalid)
            .joined(separator: "-")
            .trimmingCharacters(in: .whitespacesAndNewlines)
        return cleaned.isEmpty ? "Secure Note" : cleaned
    }
}

struct SecurityRow: Identifiable, Hashable {
    let id = UUID()
    let name: String
    let status: String
    let suggestion: String
}
