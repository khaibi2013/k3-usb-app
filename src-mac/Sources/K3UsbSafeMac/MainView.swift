import SwiftUI
import UniformTypeIdentifiers

struct MainView: View {
    @EnvironmentObject private var appState: AppState
    @State private var selectedItem: VaultItem?
    @State private var showingImporter = false
    @State private var showingDecryptFolder = false

    var body: some View {
        VStack(spacing: 0) {
            toolbar
            Divider()
            HSplitView {
                sidebar
                    .frame(minWidth: 260, idealWidth: 300)
                vaultList
                    .frame(minWidth: 520)
            }
            Divider()
            statusBar
        }
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
        .onAppear { appState.refreshVault() }
    }

    private var toolbar: some View {
        HStack(spacing: 12) {
            Button {
                showingImporter = true
            } label: {
                Label("Encrypt Files", systemImage: "lock.doc")
            }

            Button {
                showingDecryptFolder = true
            } label: {
                Label("Decrypt Selected", systemImage: "lock.open")
            }
            .disabled(selectedItem == nil)

            Button {
                appState.refreshVault()
            } label: {
                Label("Refresh", systemImage: "arrow.clockwise")
            }

            Spacer()

            Button(role: .destructive) {
                appState.ejectUsb()
            } label: {
                Label("Lock & Eject", systemImage: "eject")
            }
        }
        .padding(12)
    }

    private var sidebar: some View {
        VStack(alignment: .leading, spacing: 12) {
            Label("USB Root", systemImage: "externaldrive")
                .font(.headline)
            Text(appState.usbRoot.path)
                .font(.caption)
                .foregroundStyle(.secondary)
                .textSelection(.enabled)

            Label(appState.isDecoyMode ? "Decoy Vault" : "Secure Vault", systemImage: "shield.lefthalf.filled")
                .font(.headline)
                .padding(.top, 8)
            Text(appState.vaultURL.path)
                .font(.caption)
                .foregroundStyle(.secondary)
                .textSelection(.enabled)

            Spacer()

            Button("Logout") {
                appState.logout()
            }
        }
        .padding(16)
    }

    private var vaultList: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text("Encrypted Files")
                .font(.title3.bold())
                .padding([.top, .horizontal], 16)

            List(appState.vaultFiles, selection: $selectedItem) { item in
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
                .tag(item)
            }
        }
    }

    private var statusBar: some View {
        HStack {
            Circle()
                .fill(appState.isAuthenticated ? .green : .red)
                .frame(width: 10, height: 10)
            Text(appState.statusMessage)
                .font(.caption)
            Spacer()
            Text("\(appState.vaultFiles.count) encrypted file(s)")
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .padding(8)
    }
}
