import Foundation
import CommonCrypto
import Security

struct K3Config: Codable {
    var hwid: String
    var realHash: String
    var decoyHash: String
    var cryptoSalt: String
    var autoEncryptFolder: String
    var maxSizeBytes: String
    var autoDecrypt: String
    var showHidden: String
    var wipeHistory: String
    var wipeMacos: String
    var loginTitle: String
    var loginHelp: String
    var hideLoginHelp: String
    var needsInitialSetup: Bool = false

    enum CodingKeys: String, CodingKey {
        case hwid
        case realHash = "real_hash"
        case decoyHash = "decoy_hash"
        case cryptoSalt = "crypto_salt"
        case autoEncryptFolder = "auto_enc_folder"
        case maxSizeBytes = "max_size_bytes"
        case autoDecrypt = "auto_dec"
        case showHidden = "show_hidden"
        case wipeHistory = "wipe_history"
        case wipeMacos = "wipe_macos"
        case loginTitle = "login_title"
        case loginHelp = "login_help"
        case hideLoginHelp = "hide_login_help"
    }

    static func defaultConfig() -> K3Config {
        K3Config(
            hwid: "",
            realHash: "",
            decoyHash: "",
            cryptoSalt: K3PasswordHasher.randomSaltBase64(),
            autoEncryptFolder: "BaoMat",
            maxSizeBytes: "\(2 * 1024 * 1024 * 1024)",
            autoDecrypt: "false",
            showHidden: "false",
            wipeHistory: "false",
            wipeMacos: "true",
            loginTitle: "USB An Toan K3",
            loginHelp: "Tro giup HELP!",
            hideLoginHelp: "false",
            needsInitialSetup: true
        )
    }
}

enum K3ConfigStore {
    static func configURL(at root: URL) -> URL {
        root.appendingPathComponent(".vault_config.json")
    }

    static func loadOrCreate(at root: URL) throws -> K3Config {
        let url = configURL(at: root)
        if FileManager.default.fileExists(atPath: url.path) {
            let data = try Data(contentsOf: url)
            var config = try JSONDecoder().decode(K3Config.self, from: data)
            config.needsInitialSetup = config.realHash.isEmpty
            if config.cryptoSalt.isEmpty {
                config.cryptoSalt = K3PasswordHasher.randomSaltBase64()
                try save(config, at: root)
            }
            return config
        }

        let config = K3Config.defaultConfig()
        try save(config, at: root)
        return config
    }

    static func save(_ config: K3Config, at root: URL) throws {
        let url = configURL(at: root)
        MacSystemTools.clearHidden(url)
        let data = try JSONEncoder().encode(config)
        try data.write(to: url, options: .atomic)
        try MacSystemTools.hide(url)
    }
}

enum K3PortableLayout {
    static func ensure(at root: URL) throws {
        let folders = [".vault", ".vault_decoy", "BaoMat"]
        for folder in folders {
            let url = root.appendingPathComponent(folder, isDirectory: true)
            if !FileManager.default.fileExists(atPath: url.path) {
                try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
            }
            try MacSystemTools.hide(url)
        }
    }

    static func hideSupportFiles(at root: URL) throws {
        let names = [".vault_config.json", ".vault", ".vault_decoy", "BaoMat", "tools", "AutoLauncher", "icon.png", "autorun.inf"]
        for name in names {
            let url = root.appendingPathComponent(name)
            if FileManager.default.fileExists(atPath: url.path) {
                try MacSystemTools.hide(url)
            }
        }
    }
}

enum K3PasswordHasher {
    private static let iterations = 100_000
    private static let saltSize = 16
    private static let hashSize = 32

    static func hash(_ password: String) throws -> String {
        let salt = randomBytes(count: saltSize)
        let derived = try pbkdf2(password: password, salt: salt, count: hashSize)
        return "pbkdf2$\(iterations)$\(salt.base64EncodedString())$\(derived.base64EncodedString())"
    }

    static func verify(_ password: String, stored: String) -> Bool {
        guard !stored.isEmpty else { return false }
        guard stored.hasPrefix("pbkdf2$") else { return password == stored }
        let parts = stored.split(separator: "$").map(String.init)
        guard parts.count == 4,
              let iterations = Int(parts[1]),
              let salt = Data(base64Encoded: parts[2]),
              let expected = Data(base64Encoded: parts[3]) else { return false }
        guard let actual = try? pbkdf2(password: password, salt: salt, iterations: iterations, count: expected.count) else { return false }
        return fixedTimeEquals(actual, expected)
    }

    static func randomSaltBase64() -> String {
        randomBytes(count: saltSize).base64EncodedString()
    }

    static func pbkdf2(password: String, salt: Data, count: Int) throws -> Data {
        try pbkdf2(password: password, salt: salt, iterations: iterations, count: count)
    }

    private static func pbkdf2(password: String, salt: Data, iterations: Int, count: Int) throws -> Data {
        var output = Data(repeating: 0, count: count)
        let passwordData = Data(password.utf8)
        let result = output.withUnsafeMutableBytes { outputBytes in
            salt.withUnsafeBytes { saltBytes in
                passwordData.withUnsafeBytes { passwordBytes in
                    CCKeyDerivationPBKDF(
                        CCPBKDFAlgorithm(kCCPBKDF2),
                        passwordBytes.bindMemory(to: Int8.self).baseAddress,
                        passwordData.count,
                        saltBytes.bindMemory(to: UInt8.self).baseAddress,
                        salt.count,
                        CCPseudoRandomAlgorithm(kCCPRFHmacAlgSHA1),
                        UInt32(iterations),
                        outputBytes.bindMemory(to: UInt8.self).baseAddress,
                        count
                    )
                }
            }
        }
        guard result == kCCSuccess else { throw K3Error.userFacing("PBKDF2 failed.") }
        return output
    }

    static func randomBytes(count: Int) -> Data {
        var data = Data(repeating: 0, count: count)
        _ = data.withUnsafeMutableBytes { SecRandomCopyBytes(kSecRandomDefault, count, $0.baseAddress!) }
        return data
    }

    static func fixedTimeEquals(_ lhs: Data, _ rhs: Data) -> Bool {
        guard lhs.count == rhs.count else { return false }
        var diff: UInt8 = 0
        for index in lhs.indices {
            diff |= lhs[index] ^ rhs[index]
        }
        return diff == 0
    }
}
