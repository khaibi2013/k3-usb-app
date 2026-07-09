import Foundation
import SwiftUI

final class AppState: ObservableObject {
    @Published var usbRoot: URL
    @Published var config: K3Config
    @Published var isAuthenticated = false
    @Published var isDecoyMode = false
    @Published var statusMessage = "Disconnected"
    @Published var vaultFiles: [VaultItem] = []

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
        refreshVault()
    }

    func logout() {
        crypto = nil
        isAuthenticated = false
        isDecoyMode = false
        vaultFiles = []
        statusMessage = "Disconnected"
    }

    var vaultURL: URL {
        usbRoot.appendingPathComponent(isDecoyMode ? ".vault_decoy" : ".vault", isDirectory: true)
    }

    func refreshVault() {
        do {
            vaultFiles = try K3Vault.listEncryptedFiles(in: vaultURL)
        } catch {
            vaultFiles = []
            statusMessage = "Vault refresh failed: \(error.localizedDescription)"
        }
    }

    func encryptIntoVault(files: [URL]) {
        guard let crypto else { return }
        do {
            for file in files {
                let scan = K3MacScanner.scan(file)
                if scan.isThreat {
                    statusMessage = "Blocked: \(file.lastPathComponent) - \(scan.name)"
                    continue
                }
                try K3Vault.encrypt(file: file, into: vaultURL, crypto: crypto)
            }
            refreshVault()
            statusMessage = "Encrypted \(files.count) item(s)."
        } catch {
            statusMessage = "Encrypt failed: \(error.localizedDescription)"
        }
    }

    func decrypt(_ item: VaultItem, to outputFolder: URL) {
        guard let crypto else { return }
        do {
            try K3Vault.decrypt(file: item.url, to: outputFolder, crypto: crypto)
            statusMessage = "Decrypted \(item.displayName)."
        } catch {
            statusMessage = "Decrypt failed: \(error.localizedDescription)"
        }
    }

    func ejectUsb() {
        do {
            logout()
            try MacSystemTools.ejectVolume(at: usbRoot)
        } catch {
            statusMessage = "Eject failed: \(error.localizedDescription)"
        }
    }
}
