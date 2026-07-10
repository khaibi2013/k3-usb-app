import SwiftUI
import UniformTypeIdentifiers

struct MainView: View {
    @EnvironmentObject private var appState: AppState
    @State private var selectedItem: VaultItem?
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
                    .tabItem { Label("Vault", systemImage: "externaldrive.badge.lock") }
                securityTab
                    .tabItem { Label("Security", systemImage: "shield.checkered") }
                antivirusTab
                    .tabItem { Label("Antivirus", systemImage: "cross.case") }
                quarantineTab
                    .tabItem { Label("Quarantine", systemImage: "shippingbox") }
                trustedTab
                    .tabItem { Label("Trusted", systemImage: "checkmark.seal") }
                historyTab
                    .tabItem { Label("History", systemImage: "clock.arrow.circlepath") }
                settingsTab
                    .tabItem { Label("Settings", systemImage: "gearshape") }
                maintenanceTab
                    .tabItem { Label("Tools", systemImage: "wrench.and.screwdriver") }
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
            ZStack {
                RoundedRectangle(cornerRadius: 8)
                    .fill(appState.isDecoyMode ? Color.orange.opacity(0.15) : Color.teal.opacity(0.15))
                Image(systemName: appState.isDecoyMode ? "theatermasks.fill" : "lock.shield.fill")
                    .font(.system(size: 24, weight: .semibold))
                    .foregroundStyle(appState.isDecoyMode ? .orange : .teal)
            }
            .frame(width: 46, height: 46)
            VStack(alignment: .leading, spacing: 2) {
                Text(appState.isDecoyMode ? "Decoy Vault" : "USB An Toan K3")
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
                Label("Logout", systemImage: "rectangle.portrait.and.arrow.right")
            }
            .buttonStyle(.bordered)
            Button(role: .destructive) {
                appState.ejectUsb()
            } label: {
                Label("Lock & Eject", systemImage: "eject")
            }
            .buttonStyle(.borderedProminent)
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 14)
        .background(Color(nsColor: .controlBackgroundColor))
    }

    private var vaultTab: some View {
        HSplitView {
            VStack(alignment: .leading, spacing: 12) {
                Text("Vault Actions")
                    .font(.headline)
                    .padding(.bottom, 4)
                Button { showingImporter = true } label: {
                    Label("Encrypt Files", systemImage: "lock.doc")
                }
                .buttonStyle(.borderedProminent)
                Button { showingNewNote = true } label: {
                    Label("New Note", systemImage: "note.text.badge.plus")
                }
                .buttonStyle(.bordered)
                Button { appState.openBaoMatInFinder() } label: {
                    Label("Open BaoMat", systemImage: "folder")
                }
                .buttonStyle(.bordered)
                Button { appState.encryptBaoMatFiles() } label: {
                    Label("Encrypt BaoMat", systemImage: "folder.badge.lock")
                }
                .buttonStyle(.bordered)
                Button { showingDecryptFolder = true } label: {
                    Label("Decrypt Selected", systemImage: "lock.open")
                }
                .disabled(selectedItem == nil)
                Button { appState.refreshVault() } label: {
                    Label("Refresh Vault", systemImage: "arrow.clockwise")
                }
                .buttonStyle(.bordered)
                Divider()
                MetricTile(title: "Encrypted", value: "\(appState.vaultFiles.count)", icon: "doc.badge.lock", tint: .teal)
                MetricTile(title: "Quarantine", value: "\(appState.quarantineItems.count)", icon: "shippingbox", tint: .orange)
                MetricTile(title: "Trusted", value: "\(appState.trustedFiles.count)", icon: "checkmark.seal", tint: .blue)
                Divider()
                Label(appState.vaultURL.path, systemImage: "folder")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .textSelection(.enabled)
                Spacer()
            }
            .padding(16)
            .frame(minWidth: 240, idealWidth: 280)

            VStack(alignment: .leading, spacing: 0) {
                SectionHeader(
                    title: "Encrypted Files",
                    subtitle: appState.isDecoyMode ? "Showing files in .vault_decoy" : "Showing files in .vault"
                )
                if appState.vaultFiles.isEmpty {
                    EmptyStateView(
                        icon: "tray",
                        title: "Vault is empty",
                        message: "Choose files to encrypt. They will appear here as .k3enc files compatible with the Windows build.",
                        primaryTitle: "Encrypt Files",
                        primaryIcon: "lock.doc"
                    ) {
                        showingImporter = true
                    }
                    .overlay(alignment: .bottom) {
                        HStack(spacing: 10) {
                            Button {
                                showingNewNote = true
                            } label: {
                                Label("Write Note", systemImage: "note.text.badge.plus")
                            }
                            Button {
                                appState.openBaoMatInFinder()
                            } label: {
                                Label("Open BaoMat", systemImage: "folder")
                            }
                            Button {
                                appState.encryptBaoMatFiles()
                            } label: {
                                Label("Encrypt BaoMat", systemImage: "folder.badge.lock")
                            }
                        }
                        .buttonStyle(.bordered)
                        .padding(.bottom, 48)
                    }
                } else {
                    List(appState.vaultFiles, selection: $selectedItem) { item in
                        VaultRow(item: item)
                            .tag(item)
                    }
                }
            }
            .frame(minWidth: 560)
        }
    }

    private var securityTab: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                SectionHeader(title: "Security Center", subtitle: "Portable layout, vault integrity, and runtime health")
                Spacer()
                Button { appState.verifyVaultIntegrity() } label: {
                    Label("Verify Vault", systemImage: "checkmark.shield")
                }
                Button { appState.repairPortableLayout() } label: {
                    Label("Repair Layout", systemImage: "cross.case")
                }
                Button { appState.reloadFeatureData() } label: {
                    Label("Reload", systemImage: "arrow.clockwise")
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
                SectionHeader(title: "Antivirus", subtitle: "Scan with K3 USB heuristics and ClamAV when available")
                Spacer()
                Button {
                    scanAllowsDirectories = true
                    showingScanImporter = true
                } label: {
                    Label("Scan Folder", systemImage: "folder.badge.gearshape")
                }
                Button {
                    scanAllowsDirectories = false
                    showingScanImporter = true
                } label: {
                    Label("Scan Files", systemImage: "doc.text.magnifyingglass")
                }
                Button {
                    if let finding = selectedFinding { appState.quarantine(finding) }
                } label: {
                    Label("Quarantine", systemImage: "shippingbox")
                }
                .disabled(selectedFinding?.status != "Threat")
            }
            if appState.scanFindings.isEmpty {
                EmptyStateView(
                    icon: "magnifyingglass",
                    title: "No scan results yet",
                    message: "Scan a folder or individual files to see clean, trusted, and suspicious items here.",
                    primaryTitle: "Scan Folder",
                    primaryIcon: "folder.badge.gearshape"
                ) {
                    scanAllowsDirectories = true
                    showingScanImporter = true
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
                        Text(finding.status)
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
                SectionHeader(title: "Quarantine", subtitle: "Restore or permanently remove isolated files")
                Spacer()
                Button { appState.refreshQuarantine() } label: {
                    Label("Reload", systemImage: "arrow.clockwise")
                }
                Button {
                    if let item = selectedQuarantine { appState.restoreQuarantine(item) }
                } label: {
                    Label("Restore", systemImage: "arrow.uturn.backward")
                }
                .disabled(selectedQuarantine == nil)
                Button(role: .destructive) {
                    if let item = selectedQuarantine { appState.deleteQuarantine(item) }
                } label: {
                    Label("Delete", systemImage: "trash")
                }
                .disabled(selectedQuarantine == nil)
            }
            if appState.quarantineItems.isEmpty {
                EmptyStateView(
                    icon: "shippingbox",
                    title: "No quarantined files",
                    message: "Threats you quarantine from the Antivirus tab will appear here with restore and delete controls."
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
                SectionHeader(title: "Trusted Files", subtitle: "SHA-256 allowlist shared through .k3_trusted_hashes.txt")
                Spacer()
                Button { showingTrustImporter = true } label: {
                    Label("Add Files", systemImage: "plus")
                }
                Button(role: .destructive) {
                    if let entry = selectedTrusted { appState.removeTrusted(entry) }
                } label: {
                    Label("Remove", systemImage: "minus.circle")
                }
                .disabled(selectedTrusted == nil)
                Button { appState.refreshTrustedFiles() } label: {
                    Label("Reload", systemImage: "arrow.clockwise")
                }
            }
            if appState.trustedFiles.isEmpty {
                EmptyStateView(
                    icon: "checkmark.seal",
                    title: "No trusted files",
                    message: "Add known-good tools or installers to prevent scanner false positives by SHA-256.",
                    primaryTitle: "Add Trusted Files",
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
                SectionHeader(title: "History", subtitle: "Recent K3 activity on this Mac volume")
                Spacer()
                Button { appState.refreshHistory() } label: {
                    Label("Reload", systemImage: "arrow.clockwise")
                }
                Button(role: .destructive) { appState.clearHistory() } label: {
                    Label("Clear", systemImage: "trash")
                }
            }
            if appState.historyEntries.isEmpty {
                EmptyStateView(
                    icon: "clock.arrow.circlepath",
                    title: "No history yet",
                    message: "Login, encryption, scan, quarantine, and maintenance actions will be recorded here."
                )
            } else {
                List(appState.historyEntries) { entry in
                    HStack {
                        Text(entry.timestamp)
                            .frame(width: 150, alignment: .leading)
                        Text(entry.level)
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
            SectionHeader(title: "Maintenance Tools", subtitle: "macOS-safe equivalents for USB repair and cleanup")
            Button { appState.cleanupMacMetadata() } label: {
                Label("Clean macOS Metadata", systemImage: "sparkles")
            }
            Button { appState.verifyVolume() } label: {
                Label("Verify Volume", systemImage: "externaldrive.badge.checkmark")
            }
            Button { appState.repairPortableLayout() } label: {
                Label("Repair Portable Layout", systemImage: "bandage")
            }
            Divider()
            Text("Autorun is not available on macOS. Use this app binary directly from the USB or ISO volume.")
                .foregroundStyle(.secondary)
            Text("CHKDSK and DiskPart are Windows-only. This build uses diskutil verifyVolume and portable layout repair.")
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
            Text("\(appState.vaultFiles.count) encrypted | \(appState.quarantineItems.count) quarantined | \(appState.trustedFiles.count) trusted")
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .padding(8)
    }
}

private struct NewNoteSheet: View {
    @Environment(\.dismiss) private var dismiss
    @State private var title = "Secure Note"
    @State private var content = ""
    let onSave: (String, String) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("New Secure Note")
                .font(.title3.bold())
            TextField("File name", text: $title)
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
                Button("Cancel") { dismiss() }
                Button {
                    onSave(title, content)
                    dismiss()
                } label: {
                    Label("Encrypt Note", systemImage: "lock.doc")
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
            Section("Passwords") {
                SecureField("New real password", text: $realPassword)
                SecureField("New decoy password", text: $decoyPassword)
                Button {
                    appState.changePasswords(realPassword: realPassword, decoyPassword: decoyPassword)
                    realPassword = ""
                    decoyPassword = ""
                } label: {
                    Label("Save Passwords", systemImage: "key")
                }
            }

            Section("Login") {
                TextField("Login title", text: $loginTitle)
                TextField("Help text", text: $loginHelp)
                Toggle("Hide login help", isOn: $hideLoginHelp)
            }

            Section("General") {
                Picker("Auto encrypt folder", selection: $autoEncryptFolder) {
                    Text("BaoMat").tag("BaoMat")
                    Text("Whole USB").tag("Toan bo USB")
                    Text("Off").tag("")
                }
                Picker("Max encrypt size", selection: $maxSizeBytes) {
                    Text("No limit").tag("-1")
                    Text("1 GB").tag("\(1 * 1024 * 1024 * 1024)")
                    Text("2 GB").tag("\(2 * 1024 * 1024 * 1024)")
                    Text("4 GB").tag("\(4 * 1024 * 1024 * 1024)")
                }
                Toggle("Auto decrypt when copying out", isOn: $autoDecrypt)
                Toggle("Show hidden files", isOn: $showHidden)
                Toggle("Clear K3 history on exit", isOn: $wipeHistory)
                Toggle("Clean macOS metadata on exit", isOn: $wipeMacos)
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
                Label("Save Settings", systemImage: "square.and.arrow.down")
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
