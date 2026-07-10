import Foundation
import AppKit
import SwiftUI

final class AppState: ObservableObject {
    @Published var usbRoot: URL
    @Published var config: K3Config
    @Published var isAuthenticated = false
    @Published var isDecoyMode = false
    @Published var statusMessage = "Chua ket noi"
    @Published var vaultFiles: [VaultItem] = []
    @Published var scanFindings: [ScanFinding] = []
    @Published var quarantineItems: [QuarantineItem] = []
    @Published var trustedFiles: [TrustedFileEntry] = []
    @Published var historyEntries: [HistoryEntry] = []
    @Published var securityRows: [SecurityRow] = []
    @Published var browserURL: URL
    @Published var localItems: [LocalFileItem] = []
    @Published var antivirusEngineInfo = K3MacScanner.engineInfo()
    @Published var autoEncryptActive = false

    private var crypto: K3Crypto?
    private var autoEncryptTimer: Timer?
    private var autoEncryptSeen: Set<String> = []

    init() {
        let cwd = URL(fileURLWithPath: FileManager.default.currentDirectoryPath, isDirectory: true)
        self.usbRoot = cwd
        self.browserURL = cwd
        self.config = K3Config.defaultConfig()
    }

    func bootstrap() {
        do {
            config = try K3ConfigStore.loadOrCreate(at: usbRoot)
            try K3PortableLayout.ensure(at: usbRoot)
            try K3PortableLayout.hideSupportFiles(at: usbRoot)
            reloadFeatureData()
            loadLocalBrowser(at: usbRoot)
            statusMessage = "San sang"
        } catch {
            statusMessage = "Canh bao khoi tao: \(error.localizedDescription)"
        }
    }

    func setupPassword(realPassword: String, decoyPassword: String) throws {
        config.realHash = try K3PasswordHasher.hash(realPassword)
        config.decoyHash = decoyPassword.isEmpty ? "" : try K3PasswordHasher.hash(decoyPassword)
        config.cryptoSalt = K3PasswordHasher.randomSaltBase64()
        config.needsInitialSetup = false
        try K3ConfigStore.save(config, at: usbRoot)
        try login(password: realPassword)
        K3HistoryManager.append("INFO", "Da thiet lap mat khau ban dau", root: usbRoot)
    }

    func login(password: String) throws {
        if K3PasswordHasher.verify(password, stored: config.decoyHash) {
            isDecoyMode = true
        } else if K3PasswordHasher.verify(password, stored: config.realHash) {
            isDecoyMode = false
        } else {
            throw K3Error.userFacing("Sai mat khau.")
        }

        crypto = try K3Crypto(password: password, cryptoSaltBase64: config.cryptoSalt)
        isAuthenticated = true
        statusMessage = "Da ket noi"
        K3HistoryManager.append("INFO", isDecoyMode ? "Da dang nhap ket gia" : "Da dang nhap ket that", root: usbRoot)
        refreshVault()
        reloadFeatureData()
        loadLocalBrowser(at: browserURL)
        startAutoEncryptWatcher()
    }

    func logout() {
        K3HistoryManager.append("INFO", "Da dang xuat", root: usbRoot)
        stopAutoEncryptWatcher()
        crypto = nil
        isAuthenticated = false
        isDecoyMode = false
        vaultFiles = []
        statusMessage = "Da ngat ket noi"
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
            statusMessage = "Nap lai ket that bai: \(error.localizedDescription)"
        }
    }

    func encryptIntoVault(files: [URL], removeOriginal: Bool = false) {
        guard let crypto else { return }
        do {
            var encryptedCount = 0
            for file in files {
                if K3TrustedFileManager.isTrusted(file, root: usbRoot) {
                    K3HistoryManager.append("INFO", "File tin cay bo qua quet: \(file.lastPathComponent)", root: usbRoot)
                    try K3Vault.encrypt(file: file, into: vaultURL, crypto: crypto)
                    if removeOriginal { try? K3Maintenance.secureShredFile(file) }
                    encryptedCount += 1
                    continue
                }
                let scan = K3MacScanner.scan(file)
                if scan.isThreat {
                    statusMessage = "Da chan: \(file.lastPathComponent) - \(scan.name)"
                    scanFindings.append(ScanFinding(url: file, status: "Threat", signature: scan.name))
                    K3HistoryManager.append("WARN", statusMessage, root: usbRoot)
                    continue
                }
                try K3Vault.encrypt(file: file, into: vaultURL, crypto: crypto)
                if removeOriginal { try? K3Maintenance.secureShredFile(file) }
                encryptedCount += 1
                K3HistoryManager.append("INFO", "Da ma hoa \(file.lastPathComponent)", root: usbRoot)
            }
            refreshVault()
            loadLocalBrowser(at: browserURL)
            refreshHistory()
            statusMessage = "Da ma hoa \(encryptedCount) muc."
        } catch {
            statusMessage = "Ma hoa that bai: \(error.localizedDescription)"
        }
    }

    func loadLocalBrowser(at folder: URL) {
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: folder.path, isDirectory: &isDirectory), isDirectory.boolValue else {
            statusMessage = "Khong tim thay thu muc."
            return
        }

        browserURL = folder
        do {
            let keys: [URLResourceKey] = [.isDirectoryKey, .isHiddenKey, .fileSizeKey, .contentModificationDateKey]
            let urls = try FileManager.default.contentsOfDirectory(at: folder, includingPropertiesForKeys: keys, options: [])
            localItems = urls.compactMap { url in
                let values = try? url.resourceValues(forKeys: Set(keys))
                if values?.isHidden == true && config.showHidden != "true" { return nil }
                if [".vault", ".vault_decoy", ".k3_quarantine"].contains(url.lastPathComponent) { return nil }
                return LocalFileItem(
                    url: url,
                    name: url.lastPathComponent,
                    isDirectory: values?.isDirectory == true,
                    size: Int64(values?.fileSize ?? 0),
                    modified: values?.contentModificationDate ?? .distantPast
                )
            }
            .sorted {
                if $0.isDirectory != $1.isDirectory { return $0.isDirectory && !$1.isDirectory }
                return $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending
            }
        } catch {
            localItems = []
            statusMessage = "Duyet thu muc that bai: \(error.localizedDescription)"
        }
    }

    func goToParentFolder() {
        let parent = browserURL.deletingLastPathComponent()
        guard parent.path != browserURL.path else { return }
        loadLocalBrowser(at: parent)
    }

    func openLocalItem(_ item: LocalFileItem) {
        if item.isDirectory {
            loadLocalBrowser(at: item.url)
        } else {
            NSWorkspace.shared.open(item.url)
        }
    }

    func encryptLocalSelection(_ item: LocalFileItem?) {
        guard let item else {
            statusMessage = "Hay chon file hoac thu muc de dua vao ket."
            return
        }
        let urls = item.isDirectory ? scanFiles(at: item.url) : [item.url]
        guard !urls.isEmpty else {
            statusMessage = "Thu muc da chon khong co file de ma hoa."
            return
        }
        encryptIntoVault(files: urls, removeOriginal: true)
    }

    func decryptVaultSelectionToBrowser(_ item: VaultItem?) {
        guard let item else {
            statusMessage = "Hay chon file ma hoa de dua ra khoi ket."
            return
        }
        decrypt(item, to: browserURL)
        loadLocalBrowser(at: browserURL)
    }

    func createAndEncryptTextNote(title: String, content: String) {
        guard !title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            statusMessage = "Can nhap ten ghi chu."
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
            statusMessage = "Da ma hoa ghi chu \(safeTitle).txt."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Tao ghi chu that bai: \(error.localizedDescription)"
        }
    }

    func encryptBaoMatFiles() {
        do {
            try FileManager.default.createDirectory(at: baoMatURL, withIntermediateDirectories: true)
            let files = scanFiles(at: baoMatURL).filter { $0.pathExtension.lowercased() != "k3enc" }
            guard !files.isEmpty else {
                statusMessage = "BaoMat khong co file de ma hoa."
                return
            }
            encryptIntoVault(files: files, removeOriginal: true)
            loadLocalBrowser(at: browserURL)
        } catch {
            statusMessage = "Ma hoa BaoMat that bai: \(error.localizedDescription)"
        }
    }

    func openBaoMatInFinder() {
        do {
            try FileManager.default.createDirectory(at: baoMatURL, withIntermediateDirectories: true)
            MacSystemTools.clearHidden(baoMatURL)
            NSWorkspace.shared.open(baoMatURL)
            statusMessage = "Da mo thu muc BaoMat."
        } catch {
            statusMessage = "Mo BaoMat that bai: \(error.localizedDescription)"
        }
    }

    func decrypt(_ item: VaultItem, to outputFolder: URL) {
        guard let crypto else { return }
        do {
            try K3Vault.decrypt(file: item.url, to: outputFolder, crypto: crypto)
            statusMessage = "Da giai ma \(item.displayName)."
            K3HistoryManager.append("INFO", "Da giai ma \(item.displayName)", root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Giai ma that bai: \(error.localizedDescription)"
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
            statusMessage = "Day USB/ISO ra that bai: \(error.localizedDescription)"
        }
    }

    func reloadFeatureData() {
        antivirusEngineInfo = K3MacScanner.engineInfo()
        refreshTrustedFiles()
        refreshQuarantine()
        refreshHistory()
        reloadSecurityRows()
    }

    func scan(urls: [URL]) {
        var findings: [ScanFinding] = []
        if let snapshot = try? K3RecoverySnapshotManager.createSnapshot(for: urls, root: usbRoot) {
            K3HistoryManager.append("INFO", "Da tao snapshot phuc hoi: \(snapshot.fileCount) file, \(snapshot.bytes / 1024) KB", root: usbRoot)
        }
        for url in urls {
            let didAccess = url.startAccessingSecurityScopedResource()
            defer {
                if didAccess { url.stopAccessingSecurityScopedResource() }
            }
            let files = scanFiles(at: url)
            for file in files {
                if K3TrustedFileManager.isTrusted(file, root: usbRoot) {
                    findings.append(ScanFinding(url: file, status: "Trusted", signature: "Danh sach tin cay SHA-256"))
                    continue
                }
                let result = K3MacScanner.scan(file)
                findings.append(ScanFinding(url: file, status: result.isThreat ? "Threat" : "Clean", signature: result.name))
            }
        }
        scanFindings = findings
        guard !findings.isEmpty else {
            statusMessage = "Khong tim thay file de quet."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            refreshHistory()
            return
        }
        let threats = findings.filter { $0.status == "Threat" }.count
        statusMessage = "Quet xong: \(threats) moi de doa, \(findings.count) file."
        K3HistoryManager.append(threats == 0 ? "INFO" : "WARN", statusMessage, root: usbRoot)
        refreshHistory()
    }

    func scanCurrentFolder() {
        scan(urls: [browserURL])
    }

    func scanBaoMat() {
        scan(urls: [baoMatURL])
    }

    func scanUsbRoot() {
        scan(urls: [usbRoot])
    }

    func updateClamAVDatabase() {
        do {
            let output = try K3MacScanner.updateClamAVDatabase()
            statusMessage = output.split(whereSeparator: \.isNewline).last.map(String.init) ?? "Da cap nhat CSDL ClamAV."
            K3HistoryManager.append("INFO", "Da cap nhat CSDL ClamAV", root: usbRoot)
            antivirusEngineInfo = K3MacScanner.engineInfo()
            refreshHistory()
        } catch {
            statusMessage = "Cap nhat ClamAV that bai: \(error.localizedDescription)"
            K3HistoryManager.append("WARN", statusMessage, root: usbRoot)
            refreshHistory()
        }
    }

    func quarantine(_ finding: ScanFinding) {
        do {
            try K3QuarantineManager.quarantine(finding.url, virusName: finding.signature, root: usbRoot)
            statusMessage = "Da cach ly \(finding.url.lastPathComponent)."
            K3HistoryManager.append("WARN", statusMessage, root: usbRoot)
            scanFindings.removeAll { $0.id == finding.id }
            refreshQuarantine()
            refreshHistory()
        } catch {
            statusMessage = "Cach ly that bai: \(error.localizedDescription)"
        }
    }

    func restoreQuarantine(_ item: QuarantineItem) {
        do {
            try K3QuarantineManager.restore(item)
            statusMessage = "Da khoi phuc \(item.originalName)."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            refreshQuarantine()
            refreshHistory()
        } catch {
            statusMessage = "Khoi phuc that bai: \(error.localizedDescription)"
        }
    }

    func deleteQuarantine(_ item: QuarantineItem) {
        do {
            try K3QuarantineManager.delete(item)
            statusMessage = "Da xoa an toan file cach ly \(item.originalName)."
            K3HistoryManager.append("WARN", statusMessage, root: usbRoot)
            refreshQuarantine()
            refreshHistory()
        } catch {
            statusMessage = "Xoa that bai: \(error.localizedDescription)"
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
                K3HistoryManager.append("INFO", "Da tin cay \(file.lastPathComponent)", root: usbRoot)
            }
            refreshTrustedFiles()
            refreshHistory()
            statusMessage = "Da cap nhat danh sach tin cay."
        } catch {
            statusMessage = "Them tin cay that bai: \(error.localizedDescription)"
        }
    }

    func removeTrusted(_ entry: TrustedFileEntry) {
        do {
            try K3TrustedFileManager.remove(entry, root: usbRoot)
            refreshTrustedFiles()
            statusMessage = "Da xoa khoi danh sach tin cay."
        } catch {
            statusMessage = "Xoa tin cay that bai: \(error.localizedDescription)"
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
            statusMessage = "Kiem tra ket: \(ok) hop le, \(bad) loi."
            K3HistoryManager.append(bad == 0 ? "INFO" : "WARN", statusMessage, root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Kiem tra ket that bai: \(error.localizedDescription)"
        }
    }

    func cleanupMacMetadata() {
        do {
            let count = try K3Maintenance.cleanupMacMetadata(at: usbRoot)
            statusMessage = "Da xoa \(count) file metadata macOS."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Don dep that bai: \(error.localizedDescription)"
        }
    }

    func repairPortableLayout() {
        do {
            try K3Maintenance.repairPortableLayout(at: usbRoot)
            reloadFeatureData()
            statusMessage = "Da sua cau truc portable."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Sua cau truc that bai: \(error.localizedDescription)"
        }
    }

    func verifyVolume() {
        do {
            let output = try K3Maintenance.verifyVolume(at: usbRoot)
            statusMessage = output.split(whereSeparator: \.isNewline).last.map(String.init) ?? "Da kiem tra volume."
            K3HistoryManager.append("INFO", "Da chay diskutil verifyVolume", root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Kiem tra volume that bai: \(error.localizedDescription)"
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
            statusMessage = "Da luu cai dat."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            reloadFeatureData()
            startAutoEncryptWatcher()
        } catch {
            statusMessage = "Luu cai dat that bai: \(error.localizedDescription)"
        }
    }

    func changePasswords(realPassword: String, decoyPassword: String) {
        do {
            guard realPassword.count >= 6 else { throw K3Error.userFacing("Mat khau that can it nhat 6 ky tu.") }
            guard realPassword != decoyPassword else { throw K3Error.userFacing("Mat khau gia phai khac mat khau that.") }
            config.realHash = try K3PasswordHasher.hash(realPassword)
            config.decoyHash = decoyPassword.isEmpty ? "" : try K3PasswordHasher.hash(decoyPassword)
            try K3ConfigStore.save(config, at: usbRoot)
            crypto = try K3Crypto(password: realPassword, cryptoSaltBase64: config.cryptoSalt)
            isDecoyMode = false
            statusMessage = "Da cap nhat mat khau."
            K3HistoryManager.append("WARN", statusMessage, root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Doi mat khau that bai: \(error.localizedDescription)"
        }
    }

    func refreshHistory() {
        historyEntries = K3HistoryManager.entries(at: usbRoot)
    }

    func clearHistory() {
        do {
            try K3HistoryManager.clear(at: usbRoot)
            historyEntries = []
            statusMessage = "Da xoa nhat ky."
        } catch {
            statusMessage = "Xoa nhat ky that bai: \(error.localizedDescription)"
        }
    }

    func startAutoEncryptWatcher() {
        stopAutoEncryptWatcher()
        guard isAuthenticated, !config.autoEncryptFolder.isEmpty else {
            autoEncryptActive = false
            return
        }
        let watchURL = config.autoEncryptFolder == "Toan bo USB" || config.autoEncryptFolder == "Toàn bộ USB" ? usbRoot : baoMatURL
        try? FileManager.default.createDirectory(at: watchURL, withIntermediateDirectories: true)
        autoEncryptSeen = Set(scanFiles(at: watchURL).map(\.path))
        autoEncryptActive = true
        autoEncryptTimer = Timer.scheduledTimer(withTimeInterval: 3, repeats: true) { [weak self] _ in
            self?.processAutoEncryptTick(watchURL: watchURL)
        }
    }

    func stopAutoEncryptWatcher() {
        autoEncryptTimer?.invalidate()
        autoEncryptTimer = nil
        autoEncryptActive = false
    }

    private func processAutoEncryptTick(watchURL: URL) {
        guard isAuthenticated else {
            stopAutoEncryptWatcher()
            return
        }
        let candidates = scanFiles(at: watchURL)
            .filter { !$0.pathExtension.lowercased().elementsEqual("k3enc") }
            .filter { !$0.path.contains("/.vault/") && !$0.path.contains("/.vault_decoy/") && !$0.path.contains("/.k3_quarantine/") }
        let newFiles = candidates.filter { autoEncryptSeen.insert($0.path).inserted }
        guard !newFiles.isEmpty else { return }
        encryptIntoVault(files: newFiles, removeOriginal: true)
        K3HistoryManager.append("INFO", "Tu dong ma hoa \(newFiles.count) file moi", root: usbRoot)
        refreshHistory()
    }

    private func reloadSecurityRows() {
        let configURL = K3ConfigStore.configURL(at: usbRoot)
        let realVault = usbRoot.appendingPathComponent(".vault", isDirectory: true)
        let decoyVault = usbRoot.appendingPathComponent(".vault_decoy", isDirectory: true)
        let baoMat = usbRoot.appendingPathComponent("BaoMat", isDirectory: true)
        securityRows = [
            SecurityRow(name: "Ket that", status: exists(realVault) ? "San sang" : "Thieu", suggestion: ".vault"),
            SecurityRow(name: "Ket gia", status: exists(decoyVault) ? "San sang" : "Thieu", suggestion: ".vault_decoy"),
            SecurityRow(name: "Thu muc BaoMat", status: exists(baoMat) ? "San sang" : "Thieu", suggestion: "Noi staging tu dong ma hoa"),
            SecurityRow(name: "Cau hinh bao mat", status: exists(configURL) ? "San sang" : "Thieu", suggestion: ".vault_config.json"),
            SecurityRow(name: "File tin cay", status: "\(trustedFiles.count) muc", suggestion: ".k3_trusted_hashes.txt"),
            SecurityRow(name: "Khu cach ly", status: "\(quarantineItems.count) muc", suggestion: ".k3_quarantine"),
            SecurityRow(name: "Snapshot phuc hoi", status: exists(K3RecoverySnapshotManager.snapshotRoot(at: usbRoot)) ? "Da bat" : "Chua co", suggestion: ".k3_recovery_snapshots"),
            SecurityRow(name: "Tu dong ma hoa", status: autoEncryptActive ? "Dang theo doi" : "Tat", suggestion: config.autoEncryptFolder.isEmpty ? "Tat trong cai dat" : config.autoEncryptFolder),
            SecurityRow(name: "ClamAV", status: antivirusEngineInfo.clamAvailable ? "Co san" : "Chua cai", suggestion: antivirusEngineInfo.clamScanPath ?? "Cai ClamAV de quet chu ky"),
            SecurityRow(name: "File ma hoa", status: "\(vaultFiles.count) muc", suggestion: isDecoyMode ? "Che do ket gia" : "Ket that")
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
        return cleaned.isEmpty ? "Ghi chu bao mat" : cleaned
    }
}

struct SecurityRow: Identifiable, Hashable {
    let id = UUID()
    let name: String
    let status: String
    let suggestion: String
}

struct LocalFileItem: Identifiable, Hashable {
    let id = UUID()
    let url: URL
    let name: String
    let isDirectory: Bool
    let size: Int64
    let modified: Date
}
