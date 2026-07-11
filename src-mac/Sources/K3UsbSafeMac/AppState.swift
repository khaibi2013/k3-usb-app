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
    @Published var lastScanSummary = "Chua quet"
    @Published var lastScanDate: Date?
    @Published var isBusy = false
    @Published var progressMessage = ""
    @Published var isViewingFolderPackage = false
    @Published var folderViewerItems: [LocalFileItem] = []
    @Published var folderViewerTitle = ""
    @Published var folderViewerPath = ""

    private var crypto: K3Crypto?
    private var autoEncryptTimer: Timer?
    private var autoEncryptSeen: Set<String> = []
    private var folderViewerSession: K3FolderViewSession?
    private var folderViewerURL: URL?
    private var terminationObserver: NSObjectProtocol?

    init() {
        let root = Self.detectUsbRoot()
        self.usbRoot = root
        self.browserURL = root
        self.config = K3Config.defaultConfig()
        self.terminationObserver = NotificationCenter.default.addObserver(
            forName: NSApplication.willTerminateNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            self?.cleanupFolderPackageViewer()
        }
    }

    deinit {
        cleanupFolderPackageViewer()
        if let terminationObserver {
            NotificationCenter.default.removeObserver(terminationObserver)
        }
    }

    private static func detectUsbRoot() -> URL {
        let cwd = URL(fileURLWithPath: FileManager.default.currentDirectoryPath, isDirectory: true)
        let bundleURL = Bundle.main.bundleURL

        if bundleURL.pathExtension == "app" {
            let parent = bundleURL.deletingLastPathComponent()
            if parent.path.hasPrefix("/Volumes/") {
                return parent
            }
        }

        let executableURL = URL(fileURLWithPath: CommandLine.arguments.first ?? "")
        let executableParent = executableURL.deletingLastPathComponent()
        if executableParent.lastPathComponent == "mac", executableParent.deletingLastPathComponent().path.hasPrefix("/Volumes/") {
            return executableParent.deletingLastPathComponent()
        }

        return cwd
    }

    func bootstrap() {
        do {
            config = try K3ConfigStore.loadOrCreate(at: usbRoot)
            try K3PortableLayout.ensure(at: usbRoot)
            K3MacScanner.ensurePortableLayout(at: usbRoot)
            try K3PortableLayout.hideSupportFiles(at: usbRoot)
            reloadFeatureData()
            loadLocalBrowser(at: usbRoot)
            statusMessage = "San sang"
        } catch {
            statusMessage = "Canh bao khoi tao: \(error.localizedDescription)"
        }
    }

    func setupPassword(
        realPassword: String,
        decoyPassword: String,
        autoScanOnLogin: Bool = false,
        selfDestructMode: String = "wipe_all"
    ) throws {
        config.realHash = try K3PasswordHasher.hash(realPassword)
        config.decoyHash = decoyPassword.isEmpty ? "" : try K3PasswordHasher.hash(decoyPassword)
        config.cryptoSalt = K3PasswordHasher.randomSaltBase64()
        config.autoScanOnLogin = autoScanOnLogin ? "true" : "false"
        config.selfDestructMode = selfDestructMode
        config.macHwid = MacSystemTools.volumeIdentifier(at: usbRoot)
        config.failedLoginCount = 0
        config.lockedUntil = nil
        config.needsInitialSetup = false
        try K3ConfigStore.save(config, at: usbRoot)
        try login(password: realPassword)
        K3HistoryManager.append("INFO", "Da thiet lap mat khau ban dau", root: usbRoot)
    }

    func login(password: String) throws {
        if let lockedUntil = config.lockedUntil, lockedUntil > Date() {
            let seconds = max(1, Int(ceil(lockedUntil.timeIntervalSinceNow)))
            let minutes = max(1, Int(ceil(Double(seconds) / 60.0)))
            throw K3Error.userFacing("Da khoa dang nhap. Thu lai sau \(minutes) phut.")
        }
        if let lockedUntil = config.lockedUntil, lockedUntil <= Date() {
            config.lockedUntil = nil
            try? K3ConfigStore.save(config, at: usbRoot)
        }

        if K3PasswordHasher.verify(password, stored: config.decoyHash) {
            isDecoyMode = true
        } else if K3PasswordHasher.verify(password, stored: config.realHash) {
            isDecoyMode = false
        } else {
            try registerFailedLogin()
            return
        }

        config.failedLoginCount = 0
        config.lockedUntil = nil
        try K3ConfigStore.save(config, at: usbRoot)
        crypto = try K3Crypto(password: password, cryptoSaltBase64: config.cryptoSalt)
        isAuthenticated = true
        statusMessage = "Da ket noi"
        K3HistoryManager.append("INFO", isDecoyMode ? "Da dang nhap ket gia" : "Da dang nhap ket that", root: usbRoot)
        refreshVault()
        reloadFeatureData()
        loadLocalBrowser(at: browserURL)
        startAutoEncryptWatcher()
        if config.autoScanOnLogin == "true" {
            scan(urls: [usbRoot], createSnapshot: false)
        }
    }

    private func registerFailedLogin() throws {
        config.failedLoginCount += 1
        let failed = config.failedLoginCount

        if failed >= 10 {
            switch config.selfDestructMode {
            case "off":
                config.lockedUntil = Date().addingTimeInterval(5 * 60)
                try K3ConfigStore.save(config, at: usbRoot)
                throw K3Error.userFacing("Sai mat khau \(failed) lan. Tu huy dang tat, da khoa 5 phut.")
            case "lock":
                config.lockedUntil = Date.distantFuture
                try K3ConfigStore.save(config, at: usbRoot)
                K3HistoryManager.append("CRITICAL", "Nhap sai mat khau 10 lan. Da khoa vinh vien theo chinh sach.", root: usbRoot)
                throw K3Error.userFacing("Sai mat khau 10 lan. USB da bi khoa vinh vien.")
            case "wipe_real":
                K3HistoryManager.append("CRITICAL", "Nhap sai mat khau 10 lan. Dang xoa ket that theo chinh sach.", root: usbRoot)
                try K3DataWiper.wipeRealVaultOnly(at: usbRoot)
                config.failedLoginCount = 0
                config.lockedUntil = nil
                try K3ConfigStore.save(config, at: usbRoot)
                throw K3Error.userFacing("Sai mat khau 10 lan. Ket that da bi xoa.")
            default:
                K3HistoryManager.append("CRITICAL", "Nhap sai mat khau 10 lan. Dang xoa du lieu bao mat.", root: usbRoot)
                try K3DataWiper.wipeSensitiveData(at: usbRoot)
                config = K3Config.defaultConfig()
                statusMessage = "Da xoa du lieu sau 10 lan sai mat khau."
                throw K3Error.userFacing("Sai mat khau 10 lan. Du lieu bao mat da bi xoa.")
            }
        }

        if failed >= 5 {
            let until = Date().addingTimeInterval(5 * 60)
            config.lockedUntil = until
            try K3ConfigStore.save(config, at: usbRoot)
            let remaining = 10 - failed
            throw K3Error.userFacing("Sai mat khau \(failed) lan. Da khoa 5 phut. Con \(remaining) lan truoc khi xoa du lieu.")
        }

        try K3ConfigStore.save(config, at: usbRoot)
        let remainingToLock = 5 - failed
        throw K3Error.userFacing("Sai mat khau. Con \(remainingToLock) lan truoc khi khoa 5 phut.")
    }

    func logout() {
        K3HistoryManager.append("INFO", "Da dang xuat", root: usbRoot)
        stopAutoEncryptWatcher()
        cleanupFolderPackageViewer()
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
            if !isViewingFolderPackage {
                folderViewerItems = []
            }
            reloadSecurityRows()
        } catch {
            vaultFiles = []
            statusMessage = "Nap lai ket that bai: \(error.localizedDescription)"
        }
    }

    func encryptIntoVault(files: [URL], removeOriginal: Bool = false) {
        guard let crypto else { return }
        do {
            isBusy = true
            progressMessage = "Dang ma hoa..."
            defer {
                isBusy = false
                progressMessage = ""
            }
            var encryptedCount = 0
            for (index, file) in files.enumerated() {
                progressMessage = "Ma hoa \(index + 1)/\(files.count): \(file.lastPathComponent)"
                if K3TrustedFileManager.isTrusted(file, root: usbRoot) {
                    K3HistoryManager.append("INFO", "File tin cay bo qua quet: \(file.lastPathComponent)", root: usbRoot)
                    try K3Vault.encrypt(file: file, into: vaultURL, crypto: crypto)
                    if removeOriginal { try? K3Maintenance.secureShredFile(file) }
                    encryptedCount += 1
                    continue
                }
                let scan = K3MacScanner.scan(file, root: usbRoot)
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
        if item.isDirectory {
            encryptFolderIntoVault(folder: item.url, removeOriginal: true)
        } else {
            encryptIntoVault(files: [item.url], removeOriginal: true)
        }
    }

    func encryptDroppedURLs(_ urls: [URL]) {
        for url in urls {
            var isDirectory: ObjCBool = false
            guard FileManager.default.fileExists(atPath: url.path, isDirectory: &isDirectory) else { continue }
            if isDirectory.boolValue {
                encryptFolderIntoVault(folder: url, removeOriginal: false)
            } else {
                encryptIntoVault(files: [url], removeOriginal: false)
            }
        }
    }

    func decryptDroppedVaultURLs(_ urls: [URL]) {
        guard let crypto else { return }
        var decryptedCount = 0
        for url in urls where isEncryptedVaultFile(url) {
            do {
                try K3Vault.decrypt(file: url, to: browserURL, crypto: crypto)
                decryptedCount += 1
            } catch {
                statusMessage = "Giai ma keo tha that bai: \(error.localizedDescription)"
                return
            }
        }
        if decryptedCount > 0 {
            statusMessage = "Da giai ma \(decryptedCount) file vao \(browserURL.lastPathComponent)."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            loadLocalBrowser(at: browserURL)
            refreshHistory()
        }
    }

    func decryptVaultSelectionToBrowser(_ item: VaultItem?) {
        guard let item else {
            statusMessage = "Hay chon file ma hoa de dua ra khoi ket."
            return
        }
        decrypt(item, to: browserURL)
        loadLocalBrowser(at: browserURL)
    }

    func openFolderPackageViewer(_ item: VaultItem) {
        guard let crypto else { return }
        guard item.isFolderPackage else {
            statusMessage = "Chi .k3folder moi mo duoc trong viewer."
            return
        }
        do {
            cleanupFolderPackageViewer()
            isBusy = true
            progressMessage = "Dang mo .k3folder..."
            defer {
                isBusy = false
                progressMessage = ""
            }
            let session = try K3Vault.openFolderPackageView(file: item.url, crypto: crypto)
            folderViewerSession = session
            isViewingFolderPackage = true
            folderViewerTitle = session.displayName
            loadFolderViewer(at: session.root)
            statusMessage = "Dang xem \(item.displayName).k3folder trong temp local."
            K3HistoryManager.append("INFO", "Da mo viewer .k3folder: \(item.displayName)", root: usbRoot)
            refreshHistory()
        } catch {
            cleanupFolderPackageViewer()
            statusMessage = "Mo .k3folder that bai: \(error.localizedDescription)"
        }
    }

    func leaveFolderPackageViewer() {
        cleanupFolderPackageViewer()
        refreshVault()
        statusMessage = "Da quay lai ket."
    }

    func goToFolderViewerParent() {
        guard let session = folderViewerSession, let current = folderViewerURL else { return }
        if current.standardizedFileURL.path == session.root.standardizedFileURL.path {
            leaveFolderPackageViewer()
        } else {
            loadFolderViewer(at: current.deletingLastPathComponent())
        }
    }

    func openFolderViewerItem(_ item: LocalFileItem) {
        guard isViewingFolderPackage else { return }
        if item.isDirectory {
            loadFolderViewer(at: item.url)
        } else {
            statusMessage = "Chon 'Dua ra' de copy file tu .k3folder ra may."
        }
    }

    func copyFolderViewerSelectionToBrowser(_ item: LocalFileItem?) {
        guard let item else {
            statusMessage = "Hay chon file hoac thu muc trong .k3folder."
            return
        }
        guard isViewingFolderPackage, let session = folderViewerSession else { return }
        do {
            guard isInside(item.url, root: session.root) else {
                throw K3Error.userFacing("Muc viewer nam ngoai temp package.")
            }
            try FileManager.default.createDirectory(at: browserURL, withIntermediateDirectories: true)
            let destination = availableCopyURL(
                for: browserURL.appendingPathComponent(item.name, isDirectory: item.isDirectory),
                isDirectory: item.isDirectory
            )
            try FileManager.default.copyItem(at: item.url, to: destination)
            loadLocalBrowser(at: browserURL)
            statusMessage = "Da copy \(item.name) tu .k3folder ra \(browserURL.lastPathComponent)."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Copy tu .k3folder that bai: \(error.localizedDescription)"
        }
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
            let files = scanFiles(at: baoMatURL).filter { !isEncryptedVaultFile($0) }
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

    private func encryptFolderIntoVault(folder: URL, removeOriginal: Bool) {
        guard let crypto else { return }
        do {
            isBusy = true
            progressMessage = "Dang kiem tra thu muc..."
            defer {
                isBusy = false
                progressMessage = ""
            }
            let didAccessFolder = folder.startAccessingSecurityScopedResource()
            defer {
                if didAccessFolder {
                    folder.stopAccessingSecurityScopedResource()
                }
            }

            let checkedFiles = try preflightFolderForPackage(folder)
            progressMessage = "Dang dong goi .k3folder..."
            try K3Vault.encryptFolderPackage(folder: folder, into: vaultURL, crypto: crypto)
            if removeOriginal {
                try? K3Maintenance.secureShredDirectory(folder)
            }
            refreshVault()
            loadLocalBrowser(at: browserURL)
            refreshHistory()
            let detail = checkedFiles == 0 ? "thu muc rong" : "\(checkedFiles) file"
            statusMessage = "Da dua \(folder.lastPathComponent).k3folder vao ket (\(detail))."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
        } catch {
            statusMessage = "Ma hoa thu muc that bai: \(error.localizedDescription)"
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
            try MacSystemTools.ejectVolumeAfterAppTerminates(at: usbRoot)
            DispatchQueue.main.async {
                NSApp.terminate(nil)
            }
        } catch {
            statusMessage = "Day USB/ISO ra that bai: \(error.localizedDescription)"
        }
    }

    func reloadFeatureData() {
        K3MacScanner.ensurePortableLayout(at: usbRoot)
        antivirusEngineInfo = K3MacScanner.engineInfo(root: usbRoot)
        refreshTrustedFiles()
        refreshQuarantine()
        refreshHistory()
        reloadSecurityRows()
        let integrity = K3IntegrityManager.verify(at: usbRoot)
        if integrity.manifestExists && integrity.hasWarning {
            statusMessage = "Canh bao: app/rules/tools tren USB co the da bi sua."
        }
        if isMacCloneWarning {
            statusMessage = "Canh bao: USB khong khop ma dinh danh macOS da gan."
        }
    }

    func scan(urls: [URL], createSnapshot: Bool = true) {
        isBusy = true
        progressMessage = "Dang quet..."
        defer {
            isBusy = false
            progressMessage = ""
        }
        var findings: [ScanFinding] = []
        if createSnapshot, let snapshot = try? K3RecoverySnapshotManager.createSnapshot(for: urls, root: usbRoot) {
            K3HistoryManager.append("INFO", "Da tao snapshot phuc hoi: \(snapshot.fileCount) file, \(snapshot.bytes / 1024) KB", root: usbRoot)
        }
        for url in urls {
            let didAccess = url.startAccessingSecurityScopedResource()
            defer {
                if didAccess { url.stopAccessingSecurityScopedResource() }
            }
            let files = scanFiles(at: url)
            for (index, file) in files.enumerated() {
                progressMessage = "Quet \(index + 1)/\(files.count): \(file.lastPathComponent)"
                if K3TrustedFileManager.isTrusted(file, root: usbRoot) {
                    findings.append(ScanFinding(url: file, status: "Trusted", signature: "Danh sach tin cay SHA-256"))
                    continue
                }
                let result = K3MacScanner.scan(file, root: usbRoot)
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
        lastScanSummary = "\(threats) nguy hiem / \(findings.count) file"
        lastScanDate = Date()
        K3HistoryManager.append(threats == 0 ? "INFO" : "WARN", statusMessage, root: usbRoot)
        refreshHistory()
    }

    func exportScanReport() {
        guard !scanFindings.isEmpty else {
            statusMessage = "Chua co ket qua quet de xuat bao cao."
            return
        }
        do {
            let report = try K3ScanReportManager.writeReport(findings: scanFindings, root: usbRoot)
            statusMessage = "Da xuat bao cao: \(report.lastPathComponent)"
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            refreshHistory()
            NSWorkspace.shared.open(report)
        } catch {
            statusMessage = "Xuat bao cao that bai: \(error.localizedDescription)"
        }
    }

    func exportScanReportPDF() {
        guard !scanFindings.isEmpty else {
            statusMessage = "Chua co ket qua quet de xuat bao cao."
            return
        }
        do {
            let report = try K3ScanReportManager.writePDFReport(findings: scanFindings, root: usbRoot)
            statusMessage = "Da xuat PDF: \(report.lastPathComponent)"
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            refreshHistory()
            NSWorkspace.shared.open(report)
        } catch {
            statusMessage = "Xuat PDF that bai: \(error.localizedDescription)"
        }
    }

    func applyUsbVaccine() {
        do {
            let result = try K3UsbVaccine.apply(at: usbRoot)
            refreshQuarantine()
            reloadFeatureData()
            statusMessage = "Da vaccine USB. Autorun: \(result.autorunProtected ? "OK" : "bo qua"), cach ly \(result.quarantined) shortcut/script nghi ngo."
            K3HistoryManager.append(result.quarantined == 0 ? "INFO" : "WARN", statusMessage, root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "USB vaccine that bai: \(error.localizedDescription)"
        }
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

    func volumeUsage() -> VolumeUsage {
        let values = try? usbRoot.resourceValues(forKeys: [.volumeTotalCapacityKey, .volumeAvailableCapacityKey])
        return VolumeUsage(
            total: Int64(values?.volumeTotalCapacity ?? 0),
            free: Int64(values?.volumeAvailableCapacity ?? 0)
        )
    }

    func updateClamAVDatabase() {
        do {
            let output = try K3MacScanner.updateClamAVDatabase(root: usbRoot)
            statusMessage = output.split(whereSeparator: \.isNewline).last.map(String.init) ?? "Da cap nhat CSDL ClamAV."
            K3HistoryManager.append("INFO", "Da cap nhat CSDL ClamAV", root: usbRoot)
            antivirusEngineInfo = K3MacScanner.engineInfo(root: usbRoot)
            refreshHistory()
        } catch {
            statusMessage = "Cap nhat ClamAV that bai: \(error.localizedDescription)"
            K3HistoryManager.append("WARN", statusMessage, root: usbRoot)
            refreshHistory()
        }
    }

    func updateK3RulesFromConfiguredURL() {
        do {
            let status = try K3MacScanner.updatePortableRules(from: config.k3RuleUpdateURL, root: usbRoot)
            try? K3IntegrityManager.writeManifest(at: usbRoot)
            statusMessage = "Da cap nhat K3 rules: \(status)."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            reloadFeatureData()
        } catch {
            statusMessage = "Cap nhat K3 rules that bai: \(error.localizedDescription)"
        }
    }

    func importK3Rules(files: [URL]) {
        guard let file = files.first else { return }
        do {
            let status = try K3MacScanner.importPortableRules(from: file, root: usbRoot)
            try? K3IntegrityManager.writeManifest(at: usbRoot)
            statusMessage = "Da nhap K3 rules: \(status)."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            reloadFeatureData()
        } catch {
            statusMessage = "Nhap K3 rules that bai: \(error.localizedDescription)"
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

    func trustQuarantineAndRestore(_ item: QuarantineItem) {
        do {
            _ = try K3TrustedFileManager.trustQuarantined(item, root: usbRoot)
            try K3QuarantineManager.restore(item)
            statusMessage = "Da tin cay va khoi phuc \(item.originalName)."
            K3HistoryManager.append("INFO", statusMessage, root: usbRoot)
            refreshTrustedFiles()
            refreshQuarantine()
            refreshHistory()
        } catch {
            statusMessage = "Tin cay/khoi phuc that bai: \(error.localizedDescription)"
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
        wipeMacos: Bool,
        autoScanOnLogin: Bool,
        selfDestructMode: String,
        k3RuleUpdateURL: String
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
            config.autoScanOnLogin = autoScanOnLogin ? "true" : "false"
            config.selfDestructMode = selfDestructMode
            config.k3RuleUpdateURL = k3RuleUpdateURL.trimmingCharacters(in: .whitespacesAndNewlines)
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

    var currentMacHwid: String {
        MacSystemTools.volumeIdentifier(at: usbRoot)
    }

    var isMacCloneWarning: Bool {
        !config.macHwid.isEmpty && config.macHwid != currentMacHwid
    }

    func bindMacHwid() {
        do {
            config.macHwid = currentMacHwid
            try K3ConfigStore.save(config, at: usbRoot)
            statusMessage = "Da gan USB voi dinh danh macOS hien tai."
            K3HistoryManager.append("WARN", statusMessage, root: usbRoot)
            reloadFeatureData()
        } catch {
            statusMessage = "Gan USB that bai: \(error.localizedDescription)"
        }
    }

    func clearMacHwid() {
        do {
            config.macHwid = ""
            try K3ConfigStore.save(config, at: usbRoot)
            statusMessage = "Da bo gan dinh danh macOS."
            K3HistoryManager.append("WARN", statusMessage, root: usbRoot)
            reloadFeatureData()
        } catch {
            statusMessage = "Bo gan USB that bai: \(error.localizedDescription)"
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
            .filter { !isEncryptedVaultFile($0) }
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
        let integrity = K3IntegrityManager.verify(at: usbRoot)
        securityRows = [
            SecurityRow(name: "Ket that", status: exists(realVault) ? "San sang" : "Thieu", suggestion: ".vault"),
            SecurityRow(name: "Ket gia", status: exists(decoyVault) ? "San sang" : "Thieu", suggestion: ".vault_decoy"),
            SecurityRow(name: "Thu muc BaoMat", status: exists(baoMat) ? "San sang" : "Thieu", suggestion: "Noi staging tu dong ma hoa"),
            SecurityRow(name: "Cau hinh bao mat", status: exists(configURL) ? "San sang" : "Thieu", suggestion: ".vault_config.json"),
            SecurityRow(name: "Toan ven USB", status: integrity.status, suggestion: integrity.detail),
            SecurityRow(name: "Chong clone macOS", status: config.macHwid.isEmpty ? "Chua gan" : isMacCloneWarning ? "Canh bao" : "Dung USB", suggestion: config.macHwid.isEmpty ? "Gan trong Cai dat" : currentMacHwid),
            SecurityRow(name: "File tin cay", status: "\(trustedFiles.count) muc", suggestion: ".k3_trusted_hashes.txt"),
            SecurityRow(name: "Khu cach ly", status: "\(quarantineItems.count) muc", suggestion: ".k3_quarantine"),
            SecurityRow(name: "Snapshot phuc hoi", status: exists(K3RecoverySnapshotManager.snapshotRoot(at: usbRoot)) ? "Da bat" : "Chua co", suggestion: ".k3_recovery_snapshots"),
            SecurityRow(name: "Tu dong ma hoa", status: autoEncryptActive ? "Dang theo doi" : "Tat", suggestion: config.autoEncryptFolder.isEmpty ? "Tat trong cai dat" : config.autoEncryptFolder),
            SecurityRow(name: "Rule portable", status: K3MacScanner.portableRulesStatus(root: usbRoot), suggestion: "tools/rules/k3-rules.json"),
            SecurityRow(name: "ClamAV", status: antivirusEngineInfo.clamAvailable ? "Co san" : "Chua cai", suggestion: antivirusEngineInfo.clamScanPath ?? "Dat ClamAV vao tools/mac-arm64 hoac tools/mac-x64"),
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

    func cleanupFolderPackageViewer() {
        let tempRoot = folderViewerSession?.tempRoot
        folderViewerSession = nil
        folderViewerURL = nil
        isViewingFolderPackage = false
        folderViewerItems = []
        folderViewerTitle = ""
        folderViewerPath = ""
        if let tempRoot, !tempRoot.path.hasPrefix(usbRoot.path) {
            try? FileManager.default.removeItem(at: tempRoot)
        }
    }

    private func loadFolderViewer(at folder: URL) {
        guard let session = folderViewerSession else { return }
        guard isInside(folder, root: session.root) else {
            statusMessage = "Chan truy cap ngoai viewer temp."
            return
        }
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: folder.path, isDirectory: &isDirectory), isDirectory.boolValue else {
            statusMessage = "Khong tim thay thu muc trong .k3folder."
            return
        }

        folderViewerURL = folder
        folderViewerPath = relativePath(from: folder, root: session.root).map { "\(session.displayName)/\($0)" } ?? session.displayName
        do {
            let keys: [URLResourceKey] = [.isDirectoryKey, .isSymbolicLinkKey, .fileSizeKey, .contentModificationDateKey]
            let urls = try FileManager.default.contentsOfDirectory(at: folder, includingPropertiesForKeys: keys, options: [])
            folderViewerItems = urls.compactMap { url in
                let values = try? url.resourceValues(forKeys: Set(keys))
                if values?.isSymbolicLink == true { return nil }
                guard isInside(url, root: session.root) else { return nil }
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
            folderViewerItems = []
            statusMessage = "Duyet .k3folder that bai: \(error.localizedDescription)"
        }
    }

    private func preflightFolderForPackage(_ folder: URL) throws -> Int {
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: folder.path, isDirectory: &isDirectory), isDirectory.boolValue else {
            throw K3Error.userFacing("Hay chon dung thu muc.")
        }
        guard let enumerator = FileManager.default.enumerator(
            at: folder,
            includingPropertiesForKeys: [.isRegularFileKey, .isDirectoryKey],
            options: []
        ) else {
            throw K3Error.userFacing("Khong the doc danh sach thu muc.")
        }

        var checkedFiles = 0
        for case let url as URL in enumerator {
            let values = try url.resourceValues(forKeys: [.isRegularFileKey, .isDirectoryKey])
            if values.isDirectory == true { continue }
            guard values.isRegularFile == true else { continue }
            checkedFiles += 1
            do {
                let handle = try FileHandle(forReadingFrom: url)
                try? handle.close()
            } catch {
                throw K3Error.userFacing("Khong doc duoc file: \(url.path)")
            }
            if !K3TrustedFileManager.isTrusted(url, root: usbRoot) {
                let scan = K3MacScanner.scan(url, root: usbRoot)
                if scan.isThreat {
                    let message = "Da chan folder package: \(url.lastPathComponent) - \(scan.name)"
                    scanFindings.append(ScanFinding(url: url, status: "Threat", signature: scan.name))
                    K3HistoryManager.append("WARN", message, root: usbRoot)
                    throw K3Error.userFacing(message)
                }
            }
        }
        return checkedFiles
    }

    private func isEncryptedVaultFile(_ url: URL) -> Bool {
        ["k3enc", "k3folder"].contains(url.pathExtension.lowercased())
    }

    private func availableCopyURL(for url: URL, isDirectory: Bool) -> URL {
        if !FileManager.default.fileExists(atPath: url.path) { return url }
        let directory = url.deletingLastPathComponent()
        let ext = isDirectory ? "" : url.pathExtension
        let base = ext.isEmpty ? url.lastPathComponent : url.deletingPathExtension().lastPathComponent
        var index = 1
        while true {
            let candidate = ext.isEmpty
                ? directory.appendingPathComponent("\(base) (\(index))", isDirectory: isDirectory)
                : directory.appendingPathComponent("\(base) (\(index))").appendingPathExtension(ext)
            if !FileManager.default.fileExists(atPath: candidate.path) { return candidate }
            index += 1
        }
    }

    private func isInside(_ url: URL, root: URL) -> Bool {
        let path = url.standardizedFileURL.path
        let rootPath = root.standardizedFileURL.path
        let prefix = rootPath.hasSuffix("/") ? rootPath : rootPath + "/"
        return path == rootPath || path.hasPrefix(prefix)
    }

    private func relativePath(from url: URL, root: URL) -> String? {
        let path = url.standardizedFileURL.path
        let rootPath = root.standardizedFileURL.path
        guard path != rootPath else { return nil }
        let prefix = rootPath.hasSuffix("/") ? rootPath : rootPath + "/"
        guard path.hasPrefix(prefix) else { return nil }
        return String(path.dropFirst(prefix.count))
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
