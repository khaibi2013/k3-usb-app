import SwiftUI
import UniformTypeIdentifiers

struct MainView: View {
    @EnvironmentObject private var appState: AppState
    @State private var selectedItem: VaultItem?
    @State private var selectedLocalItem: LocalFileItem?
    @State private var selectedFinding: ScanFinding?
    @State private var selectedQuarantine: QuarantineItem?
    @State private var selectedTrusted: TrustedFileEntry?
    @State private var showingImporter = false
    @State private var showingDecryptFolder = false
    @State private var showingScanImporter = false
    @State private var showingTrustImporter = false
    @State private var showingNewNote = false
    @State private var scanAllowsDirectories = true

    var body: some View {
        VStack(spacing: 0) {
            header
            Divider()
            TabView {
                vaultTab
                    .tabItem { Label("Ket sat", systemImage: "externaldrive.badge.lock") }
                securityTab
                    .tabItem { Label("Bao mat", systemImage: "shield.checkered") }
                antivirusTab
                    .tabItem { Label("Diet virus", systemImage: "cross.case") }
                quarantineTab
                    .tabItem { Label("Cach ly", systemImage: "shippingbox") }
                trustedTab
                    .tabItem { Label("Tin cay", systemImage: "checkmark.seal") }
                historyTab
                    .tabItem { Label("Nhat ky", systemImage: "clock.arrow.circlepath") }
                settingsTab
                    .tabItem { Label("Cai dat", systemImage: "gearshape") }
                maintenanceTab
                    .tabItem { Label("Cong cu", systemImage: "wrench.and.screwdriver") }
            }
            Divider()
            statusBar
        }
        .background(Color(nsColor: .windowBackgroundColor))
        .fileImporter(isPresented: $showingImporter, allowedContentTypes: [.item], allowsMultipleSelection: true) { result in
            if case .success(let urls) = result {
                appState.encryptIntoVault(files: urls)
            }
        }
        .fileImporter(isPresented: $showingDecryptFolder, allowedContentTypes: [.folder], allowsMultipleSelection: false) { result in
            if case .success(let urls) = result, let item = selectedItem, let folder = urls.first {
                appState.decrypt(item, to: folder)
            }
        }
        .fileImporter(isPresented: $showingScanImporter, allowedContentTypes: scanAllowsDirectories ? [.folder, .item] : [.item], allowsMultipleSelection: true) { result in
            if case .success(let urls) = result {
                appState.scan(urls: urls)
            }
        }
        .fileImporter(isPresented: $showingTrustImporter, allowedContentTypes: [.item], allowsMultipleSelection: true) { result in
            if case .success(let urls) = result {
                appState.trust(files: urls)
            }
        }
        .sheet(isPresented: $showingNewNote) {
            NewNoteSheet { title, content in
                appState.createAndEncryptTextNote(title: title, content: content)
            }
        }
        .onAppear { appState.reloadFeatureData() }
    }

    private var header: some View {
        HStack(spacing: 14) {
            K3LogoView(size: 52, cornerRadius: 10)
            VStack(alignment: .leading, spacing: 2) {
                Text(appState.isDecoyMode ? "Ket gia" : "USB An Toan K3")
                    .font(.title3.bold())
                Text(appState.usbRoot.path)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
                    .textSelection(.enabled)
            }
            Spacer()
            Button {
                appState.logout()
            } label: {
                Label("Dang xuat", systemImage: "rectangle.portrait.and.arrow.right")
            }
            .buttonStyle(.bordered)
            Button(role: .destructive) {
                appState.ejectUsb()
            } label: {
                Label("Khoa & Day ra", systemImage: "eject")
            }
            .buttonStyle(.borderedProminent)
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 14)
        .background(Color(nsColor: .controlBackgroundColor))
    }

    private var vaultTab: some View {
        VStack(spacing: 0) {
            HStack(spacing: 10) {
                Button { showingImporter = true } label: {
                    Label("Ma hoa file", systemImage: "lock.doc")
                }
                .buttonStyle(.borderedProminent)
                Button { showingNewNote = true } label: {
                    Label("Ghi chu moi", systemImage: "note.text.badge.plus")
                }
                .buttonStyle(.bordered)
                Button { appState.openBaoMatInFinder() } label: {
                    Label("Mo BaoMat", systemImage: "folder")
                }
                .buttonStyle(.bordered)
                Button { appState.encryptBaoMatFiles() } label: {
                    Label("Ma hoa BaoMat", systemImage: "folder.badge.lock")
                }
                .buttonStyle(.bordered)
                Spacer()
                Button { showingDecryptFolder = true } label: {
                    Label("Giai ma muc chon", systemImage: "lock.open")
                }
                .disabled(selectedItem == nil)
                Button { appState.refreshVault() } label: {
                    Label("Nap lai ket", systemImage: "arrow.clockwise")
                }
                .buttonStyle(.bordered)
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 12)

            Divider()

            HStack(spacing: 0) {
                localBrowserPane
                    .frame(minWidth: 420)

                Divider()

                transferControls
                    .frame(width: 108)

                Divider()

                vaultBrowserPane
                    .frame(minWidth: 420)
            }
        }
    }

    private var localBrowserPane: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack {
                SectionHeader(title: "Tep ben ngoai", subtitle: appState.browserURL.path)
                Spacer()
                Button { appState.goToParentFolder() } label: {
                    Image(systemName: "arrow.up")
                }
                .help("Len thu muc cha")
                Button { appState.loadLocalBrowser(at: appState.baoMatURL) } label: {
                    Label("BaoMat", systemImage: "folder")
                }
                Button { appState.loadLocalBrowser(at: appState.usbRoot) } label: {
                    Label("Goc", systemImage: "externaldrive")
                }
            }
            .padding(.horizontal, 16)
            .padding(.top, 10)

            if appState.localItems.isEmpty {
                EmptyStateView(
                    icon: "folder",
                    title: "Chua co file hien thi",
                    message: "Mo BaoMat, tao ghi chu, hoac chon file tu Finder. Thu muc/file se hien o day de thao tac nhu ban Windows.",
                    primaryTitle: "Ghi chu moi",
                    primaryIcon: "note.text.badge.plus"
                ) {
                    showingNewNote = true
                }
            } else {
                List(appState.localItems, selection: $selectedLocalItem) { item in
                    LocalFileRow(item: item)
                        .tag(item)
                        .onTapGesture(count: 2) {
                            appState.openLocalItem(item)
                        }
                }
            }
        }
    }

    private var transferControls: some View {
        VStack(spacing: 14) {
            Spacer()
            Button {
                appState.encryptLocalSelection(selectedLocalItem)
            } label: {
                VStack(spacing: 6) {
                    Image(systemName: "arrow.right")
                        .font(.system(size: 24, weight: .bold))
                    Text("Dua vao")
                        .font(.caption.bold())
                }
                .frame(width: 82, height: 62)
            }
            .buttonStyle(.borderedProminent)
            .disabled(selectedLocalItem == nil)

            Button {
                appState.decryptVaultSelectionToBrowser(selectedItem)
            } label: {
                VStack(spacing: 6) {
                    Image(systemName: "arrow.left")
                        .font(.system(size: 24, weight: .bold))
                    Text("Dua ra")
                        .font(.caption.bold())
                }
                .frame(width: 82, height: 62)
            }
            .buttonStyle(.bordered)
            .disabled(selectedItem == nil)

            Divider()
                .padding(.vertical, 4)

            MetricPill(value: "\(appState.localItems.count)", title: "ngoai")
            MetricPill(value: "\(appState.vaultFiles.count)", title: "ket")
            Spacer()
        }
        .padding(.vertical, 16)
        .background(Color(nsColor: .controlBackgroundColor))
    }

    private var vaultBrowserPane: some View {
        VStack(alignment: .leading, spacing: 0) {
            SectionHeader(
                title: "Tep trong ket",
                subtitle: appState.isDecoyMode ? ".vault_decoy" : ".vault"
            )
            .padding(.horizontal, 16)
            .padding(.top, 10)

            if appState.vaultFiles.isEmpty {
                EmptyStateView(
                    icon: "tray",
                    title: "Ket dang trong",
                    message: "Chon file hoac thu muc ben trai, sau do bam mui ten de dua vao ket.",
                    primaryTitle: "Viet ghi chu",
                    primaryIcon: "note.text.badge.plus"
                ) {
                    showingNewNote = true
                }
            } else {
                List(appState.vaultFiles, selection: $selectedItem) { item in
                    VaultRow(item: item)
                        .tag(item)
                }
            }
        }
    }

    private var securityTab: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                SectionHeader(title: "Trung tam bao mat", subtitle: "Kiem tra cau truc portable, tinh toan ven ket va trang thai he thong")
                Spacer()
                Button { appState.verifyVaultIntegrity() } label: {
                    Label("Kiem tra ket", systemImage: "checkmark.shield")
                }
                Button { appState.repairPortableLayout() } label: {
                    Label("Sua cau truc", systemImage: "cross.case")
                }
                Button { appState.reloadFeatureData() } label: {
                    Label("Nap lai", systemImage: "arrow.clockwise")
                }
            }
            List(appState.securityRows) { row in
                HStack {
                    Text(row.name)
                        .frame(width: 180, alignment: .leading)
                    Text(row.status)
                        .frame(width: 150, alignment: .leading)
                    Text(row.suggestion)
                        .foregroundStyle(.secondary)
                    Spacer()
                }
            }
        }
        .padding(16)
    }

    private var antivirusTab: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                SectionHeader(title: "Diet virus", subtitle: "Quet bang luat K3 USB va ClamAV neu may da cai")
                Spacer()
                Label(appState.antivirusEngineInfo.clamAvailable ? "ClamAV san sang" : "Chua co ClamAV", systemImage: appState.antivirusEngineInfo.clamAvailable ? "checkmark.shield" : "exclamationmark.triangle")
                    .foregroundStyle(appState.antivirusEngineInfo.clamAvailable ? .green : .orange)
                Button {
                    appState.updateClamAVDatabase()
                } label: {
                    Label("Cap nhat DB", systemImage: "arrow.triangle.2.circlepath")
                }
                .disabled(!appState.antivirusEngineInfo.freshClamAvailable)
            }
            HStack(spacing: 10) {
                Button { appState.scanCurrentFolder() } label: {
                    Label("Quet thu muc dang mo", systemImage: "folder.badge.gearshape")
                }
                .buttonStyle(.borderedProminent)
                Button { appState.scanBaoMat() } label: {
                    Label("Quet BaoMat", systemImage: "shield.lefthalf.filled")
                }
                Button { appState.scanUsbRoot() } label: {
                    Label("Quet USB", systemImage: "externaldrive.badge.magnifyingglass")
                }
                Divider()
                    .frame(height: 24)
                Button {
                    scanAllowsDirectories = true
                    showingScanImporter = true
                } label: {
                    Label("Chon thu muc", systemImage: "folder.badge.gearshape")
                }
                Button {
                    scanAllowsDirectories = false
                    showingScanImporter = true
                } label: {
                    Label("Chon file", systemImage: "doc.text.magnifyingglass")
                }
                Button {
                    if let finding = selectedFinding { appState.quarantine(finding) }
                } label: {
                    Label("Cach ly", systemImage: "shippingbox")
                }
                .disabled(selectedFinding?.status != "Threat")
                Spacer()
            }
            if appState.scanFindings.isEmpty {
                EmptyStateView(
                    icon: "magnifyingglass",
                    title: "Chua co ket qua quet",
                    message: "Bam Quet thu muc dang mo, Quet BaoMat, Quet USB, hoac chon file/thu muc de bat dau.",
                    primaryTitle: "Quet thu muc dang mo",
                    primaryIcon: "folder.badge.gearshape"
                ) {
                    appState.scanCurrentFolder()
                }
            } else {
                List(appState.scanFindings, selection: $selectedFinding) { finding in
                    HStack {
                        Image(systemName: finding.status == "Threat" ? "exclamationmark.triangle.fill" : finding.status == "Trusted" ? "checkmark.seal.fill" : "checkmark.circle")
                            .foregroundStyle(finding.status == "Threat" ? .red : finding.status == "Trusted" ? .blue : .green)
                        VStack(alignment: .leading) {
                            Text(finding.url.lastPathComponent)
                            Text(finding.url.path)
                                .font(.caption)
                                .foregroundStyle(.secondary)
                                .lineLimit(1)
                        }
                        Spacer()
                        Text(scanStatusText(finding.status))
                            .frame(width: 80, alignment: .leading)
                        Text(finding.signature)
                            .foregroundStyle(.secondary)
                            .frame(width: 220, alignment: .leading)
                    }
                    .tag(finding)
                }
            }
        }
        .padding(16)
    }

    private var quarantineTab: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                SectionHeader(title: "Khu cach ly", subtitle: "Khoi phuc hoac xoa an toan file nghi nhiem")
                Spacer()
                Button { appState.refreshQuarantine() } label: {
                    Label("Nap lai", systemImage: "arrow.clockwise")
                }
                Button {
                    if let item = selectedQuarantine { appState.restoreQuarantine(item) }
                } label: {
                    Label("Khoi phuc", systemImage: "arrow.uturn.backward")
                }
                .disabled(selectedQuarantine == nil)
                Button(role: .destructive) {
                    if let item = selectedQuarantine { appState.deleteQuarantine(item) }
                } label: {
                    Label("Xoa", systemImage: "trash")
                }
                .disabled(selectedQuarantine == nil)
            }
            if appState.quarantineItems.isEmpty {
                EmptyStateView(
                    icon: "shippingbox",
                    title: "Chua co file cach ly",
                    message: "Tep nghi nhiem duoc cach ly tu tab Diet virus se hien o day."
                )
            } else {
                List(appState.quarantineItems, selection: $selectedQuarantine) { item in
                    HStack {
                        VStack(alignment: .leading) {
                            Text(item.originalName)
                            Text(item.originalPath)
                                .font(.caption)
                                .foregroundStyle(.secondary)
                                .lineLimit(1)
                        }
                        Spacer()
                        Text(item.virusName)
                            .frame(width: 200, alignment: .leading)
                        Text(item.quarantineDate)
                            .foregroundStyle(.secondary)
                            .frame(width: 150, alignment: .leading)
                    }
                    .tag(item)
                }
            }
        }
        .padding(16)
    }

    private var trustedTab: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                SectionHeader(title: "Tep tin cay", subtitle: "Danh sach SHA-256 dung chung trong .k3_trusted_hashes.txt")
                Spacer()
                Button { showingTrustImporter = true } label: {
                    Label("Them file", systemImage: "plus")
                }
                Button(role: .destructive) {
                    if let entry = selectedTrusted { appState.removeTrusted(entry) }
                } label: {
                    Label("Xoa", systemImage: "minus.circle")
                }
                .disabled(selectedTrusted == nil)
                Button { appState.refreshTrustedFiles() } label: {
                    Label("Nap lai", systemImage: "arrow.clockwise")
                }
            }
            if appState.trustedFiles.isEmpty {
                EmptyStateView(
                    icon: "checkmark.seal",
                    title: "Chua co file tin cay",
                    message: "Them cong cu/bo cai dat hop le de tranh bi bao nham, dua tren SHA-256.",
                    primaryTitle: "Them file tin cay",
                    primaryIcon: "plus"
                ) {
                    showingTrustImporter = true
                }
            } else {
                List(appState.trustedFiles, selection: $selectedTrusted) { entry in
                    HStack {
                        Text(entry.name)
                            .frame(width: 220, alignment: .leading)
                        Text(entry.hash)
                            .font(.system(.caption, design: .monospaced))
                            .lineLimit(1)
                        Spacer()
                        Text(entry.addedAt)
                            .foregroundStyle(.secondary)
                            .frame(width: 150, alignment: .leading)
                    }
                    .tag(entry)
                }
            }
        }
        .padding(16)
    }

    private var historyTab: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                SectionHeader(title: "Nhat ky", subtitle: "Cac hoat dong gan day cua K3 tren volume nay")
                Spacer()
                Button { appState.refreshHistory() } label: {
                    Label("Nap lai", systemImage: "arrow.clockwise")
                }
                Button(role: .destructive) { appState.clearHistory() } label: {
                    Label("Xoa", systemImage: "trash")
                }
            }
            if appState.historyEntries.isEmpty {
                EmptyStateView(
                    icon: "clock.arrow.circlepath",
                    title: "Chua co nhat ky",
                    message: "Dang nhap, ma hoa, quet virus, cach ly va bao tri se duoc ghi tai day."
                )
            } else {
                List(appState.historyEntries) { entry in
                    HStack {
                        Text(entry.timestamp)
                            .frame(width: 150, alignment: .leading)
                        Text(historyLevelText(entry.level))
                            .frame(width: 70, alignment: .leading)
                        Text(entry.message)
                        Spacer()
                    }
                }
            }
        }
        .padding(16)
    }

    private var settingsTab: some View {
        SettingsPane()
            .environmentObject(appState)
            .padding(16)
    }

    private var maintenanceTab: some View {
        VStack(alignment: .leading, spacing: 14) {
            SectionHeader(title: "Cong cu bao tri", subtitle: "Cac thao tac sua loi/don dep an toan cho macOS")
            Button { appState.cleanupMacMetadata() } label: {
                Label("Don metadata macOS", systemImage: "sparkles")
            }
            Button { appState.verifyVolume() } label: {
                Label("Kiem tra volume", systemImage: "externaldrive.badge.checkmark")
            }
            Button { appState.repairPortableLayout() } label: {
                Label("Sua cau truc portable", systemImage: "bandage")
            }
            Divider()
            Text("macOS khong cho tu dong chay USB autorun. Hay chay truc tiep file app tren USB/ISO.")
                .foregroundStyle(.secondary)
            Text("CHKDSK va DiskPart chi co tren Windows. Ban macOS dung diskutil verifyVolume va sua cau truc portable.")
                .foregroundStyle(.secondary)
            Spacer()
        }
        .padding(16)
    }

    private var statusBar: some View {
        HStack {
            Circle()
                .fill(appState.isAuthenticated ? .green : .red)
                .frame(width: 10, height: 10)
            Text(appState.statusMessage)
                .font(.caption)
            Spacer()
            Text("\(appState.vaultFiles.count) ma hoa | \(appState.quarantineItems.count) cach ly | \(appState.trustedFiles.count) tin cay | tu dong \(appState.autoEncryptActive ? "bat" : "tat")")
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .padding(8)
    }
}

private struct NewNoteSheet: View {
    @Environment(\.dismiss) private var dismiss
    @State private var title = "Ghi chu bao mat"
    @State private var content = ""
    let onSave: (String, String) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("Ghi chu bao mat moi")
                .font(.title3.bold())
            TextField("Ten file", text: $title)
                .textFieldStyle(.roundedBorder)
            TextEditor(text: $content)
                .font(.body)
                .frame(minHeight: 220)
                .overlay {
                    RoundedRectangle(cornerRadius: 6)
                        .stroke(Color.secondary.opacity(0.25))
                }
            HStack {
                Spacer()
                Button("Huy") { dismiss() }
                Button {
                    onSave(title, content)
                    dismiss()
                } label: {
                    Label("Ma hoa ghi chu", systemImage: "lock.doc")
                }
                .buttonStyle(.borderedProminent)
            }
        }
        .padding(20)
        .frame(width: 520, height: 380)
    }
}

private struct VaultRow: View {
    let item: VaultItem

    var body: some View {
        HStack {
            Image(systemName: "doc.badge.lock")
                .foregroundStyle(.teal)
            VStack(alignment: .leading) {
                Text(item.displayName)
                Text(item.url.path)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }
            Spacer()
            Text(ByteCountFormatter.string(fromByteCount: item.size, countStyle: .file))
                .font(.caption)
                .foregroundStyle(.secondary)
        }
    }
}

private struct LocalFileRow: View {
    let item: LocalFileItem

    var body: some View {
        HStack {
            Image(systemName: item.isDirectory ? "folder.fill" : "doc")
                .foregroundStyle(item.isDirectory ? .blue : .secondary)
                .frame(width: 22)
            VStack(alignment: .leading, spacing: 2) {
                Text(item.name)
                    .lineLimit(1)
                Text(item.url.path)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }
            Spacer()
            if item.isDirectory {
                Text("Thu muc")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            } else {
                Text(ByteCountFormatter.string(fromByteCount: item.size, countStyle: .file))
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
    }
}

private struct MetricPill: View {
    let value: String
    let title: String

    var body: some View {
        VStack(spacing: 2) {
            Text(value)
                .font(.headline)
            Text(title)
                .font(.caption2)
                .foregroundStyle(.secondary)
        }
        .frame(width: 82)
        .padding(.vertical, 8)
        .background(Color(nsColor: .windowBackgroundColor))
        .clipShape(RoundedRectangle(cornerRadius: 8))
    }
}

private func scanStatusText(_ status: String) -> String {
    switch status {
    case "Threat":
        return "Nguy hiem"
    case "Trusted":
        return "Tin cay"
    case "Clean":
        return "Sach"
    default:
        return status
    }
}

private func historyLevelText(_ level: String) -> String {
    switch level {
    case "INFO":
        return "Thong tin"
    case "WARN":
        return "Canh bao"
    default:
        return level
    }
}

private struct SectionHeader: View {
    let title: String
    let subtitle: String

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(title)
                .font(.title3.bold())
            Text(subtitle)
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .padding(.vertical, 6)
    }
}

private struct MetricTile: View {
    let title: String
    let value: String
    let icon: String
    let tint: Color

    var body: some View {
        HStack(spacing: 10) {
            Image(systemName: icon)
                .font(.system(size: 16, weight: .semibold))
                .foregroundStyle(tint)
                .frame(width: 24)
            VStack(alignment: .leading, spacing: 1) {
                Text(value)
                    .font(.headline)
                Text(title)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Spacer()
        }
        .padding(10)
        .background(tint.opacity(0.08))
        .clipShape(RoundedRectangle(cornerRadius: 8))
    }
}

private struct EmptyStateView: View {
    let icon: String
    let title: String
    let message: String
    var primaryTitle: String?
    var primaryIcon: String?
    var action: (() -> Void)?

    var body: some View {
        VStack(spacing: 14) {
            Spacer()
            Image(systemName: icon)
                .font(.system(size: 44, weight: .light))
                .foregroundStyle(.teal)
            Text(title)
                .font(.title3.bold())
            Text(message)
                .font(.callout)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
                .frame(maxWidth: 420)
            if let primaryTitle, let action {
                Button(action: action) {
                    Label(primaryTitle, systemImage: primaryIcon ?? "arrow.right")
                }
                .buttonStyle(.borderedProminent)
                .padding(.top, 4)
            }
            Spacer()
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(24)
    }
}

private struct SettingsPane: View {
    @EnvironmentObject private var appState: AppState
    @State private var realPassword = ""
    @State private var decoyPassword = ""
    @State private var loginTitle = ""
    @State private var loginHelp = ""
    @State private var hideLoginHelp = false
    @State private var autoEncryptFolder = "BaoMat"
    @State private var maxSizeBytes = "\(2 * 1024 * 1024 * 1024)"
    @State private var autoDecrypt = false
    @State private var showHidden = false
    @State private var wipeHistory = false
    @State private var wipeMacos = true

    var body: some View {
        Form {
            Section("Mat khau") {
                SecureField("Mat khau that moi", text: $realPassword)
                SecureField("Mat khau gia moi", text: $decoyPassword)
                Button {
                    appState.changePasswords(realPassword: realPassword, decoyPassword: decoyPassword)
                    realPassword = ""
                    decoyPassword = ""
                } label: {
                    Label("Luu mat khau", systemImage: "key")
                }
            }

            Section("Dang nhap") {
                TextField("Tieu de dang nhap", text: $loginTitle)
                TextField("Noi dung tro giup", text: $loginHelp)
                Toggle("An tro giup dang nhap", isOn: $hideLoginHelp)
            }

            Section("Cai dat chung") {
                Picker("Thu muc tu dong ma hoa", selection: $autoEncryptFolder) {
                    Text("BaoMat").tag("BaoMat")
                    Text("Toan bo USB").tag("Toan bo USB")
                    Text("Tat").tag("")
                }
                Picker("Dung luong ma hoa toi da", selection: $maxSizeBytes) {
                    Text("Khong gioi han").tag("-1")
                    Text("1 GB").tag("\(1 * 1024 * 1024 * 1024)")
                    Text("2 GB").tag("\(2 * 1024 * 1024 * 1024)")
                    Text("4 GB").tag("\(4 * 1024 * 1024 * 1024)")
                }
                Toggle("Tu dong giai ma khi dua ra may", isOn: $autoDecrypt)
                Toggle("Hien file an", isOn: $showHidden)
                Toggle("Xoa nhat ky K3 khi thoat", isOn: $wipeHistory)
                Toggle("Don metadata macOS khi thoat", isOn: $wipeMacos)
            }

            Button {
                appState.saveSettings(
                    loginTitle: loginTitle,
                    loginHelp: loginHelp,
                    hideLoginHelp: hideLoginHelp,
                    autoEncryptFolder: autoEncryptFolder,
                    maxSizeBytes: maxSizeBytes,
                    autoDecrypt: autoDecrypt,
                    showHidden: showHidden,
                    wipeHistory: wipeHistory,
                    wipeMacos: wipeMacos
                )
            } label: {
                Label("Luu cai dat", systemImage: "square.and.arrow.down")
            }
        }
        .onAppear {
            loginTitle = appState.config.loginTitle
            loginHelp = appState.config.loginHelp
            hideLoginHelp = appState.config.hideLoginHelp == "true"
            autoEncryptFolder = appState.config.autoEncryptFolder
            maxSizeBytes = appState.config.maxSizeBytes
            autoDecrypt = appState.config.autoDecrypt == "true"
            showHidden = appState.config.showHidden == "true"
            wipeHistory = appState.config.wipeHistory == "true"
            wipeMacos = appState.config.wipeMacos == "true"
        }
    }
}
