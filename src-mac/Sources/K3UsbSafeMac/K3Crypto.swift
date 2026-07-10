import Foundation
import CommonCrypto

final class K3Crypto {
    private let key: Data
    private let macKey: Data

    init(password: String, cryptoSaltBase64: String) throws {
        guard let salt = Data(base64Encoded: cryptoSaltBase64) else {
            throw K3Error.userFacing("Invalid crypto salt.")
        }
        let material = try K3PasswordHasher.pbkdf2(password: password, salt: salt, count: 64)
        key = Data(material.prefix(32))
        macKey = Data(material.dropFirst(32).prefix(32))
    }

    func encrypt(file source: URL, to destination: URL) throws {
        let original = try Data(contentsOf: source)
        let shouldCompress = original.count > 1024 && !Self.isMedia(source.pathExtension)
        let payloadData = shouldCompress ? try Self.gzip(original) : original
        let iv = K3PasswordHasher.randomBytes(count: 16)
        let encrypted = try Self.aesCBC(data: payloadData, key: key, iv: iv, operation: CCOperation(kCCEncrypt))

        let nameData = Data(source.lastPathComponent.utf8)
        var payload = Data()
        payload.append(UInt8(shouldCompress ? 1 : 0))
        payload.append(Self.littleEndianInt32(nameData.count))
        payload.append(nameData)
        payload.append(encrypted)

        let mac = Self.hmacSHA256(key: macKey, data: iv + payload)
        try (iv + mac + payload).write(to: destination, options: .atomic)
    }

    func decrypt(file source: URL, to outputFolder: URL) throws -> URL {
        let decoded = try decode(file: source)

        let output = outputFolder.appendingPathComponent(decoded.fileName)
        try decoded.data.write(to: output, options: .atomic)
        return output
    }

    func verify(file source: URL) throws {
        _ = try decode(file: source)
    }

    private static func isMedia(_ ext: String) -> Bool {
        ["mp4", "zip", "jpg", "jpeg", "png", "mp3", "rar"].contains(ext.lowercased())
    }

    private func decode(file source: URL) throws -> (fileName: String, data: Data) {
        let allData = try Data(contentsOf: source)
        guard allData.count >= 16 + 32 + 1 + 4 else { throw K3Error.userFacing("Invalid encrypted file.") }

        let iv = allData.subdata(in: 0..<16)
        let mac = allData.subdata(in: 16..<48)
        let payload = allData.subdata(in: 48..<allData.count)
        let expected = Self.hmacSHA256(key: macKey, data: iv + payload)
        guard K3PasswordHasher.fixedTimeEquals(mac, expected) else {
            throw K3Error.userFacing("Integrity check failed.")
        }

        var offset = 0
        let isCompressed = payload[offset] == 1
        offset += 1
        let nameLength = Self.readLittleEndianInt32(payload, offset: offset)
        offset += 4
        guard nameLength >= 0, offset + nameLength <= payload.count else { throw K3Error.userFacing("Invalid metadata.") }

        let nameData = payload.subdata(in: offset..<(offset + nameLength))
        let fileName = String(data: nameData, encoding: .utf8) ?? "decrypted-file"
        offset += nameLength
        let encrypted = payload.subdata(in: offset..<payload.count)
        var decrypted = try Self.aesCBC(data: encrypted, key: key, iv: iv, operation: CCOperation(kCCDecrypt))
        if isCompressed {
            decrypted = try Self.gunzip(decrypted)
        }
        return (fileName, decrypted)
    }

    private static func aesCBC(data: Data, key: Data, iv: Data, operation: CCOperation) throws -> Data {
        let outputCapacity = data.count + kCCBlockSizeAES128
        var output = Data(repeating: 0, count: outputCapacity)
        var outputLength = 0
        let status = output.withUnsafeMutableBytes { outputBytes in
            data.withUnsafeBytes { dataBytes in
                key.withUnsafeBytes { keyBytes in
                    iv.withUnsafeBytes { ivBytes in
                        CCCrypt(
                            operation,
                            CCAlgorithm(kCCAlgorithmAES),
                            CCOptions(kCCOptionPKCS7Padding),
                            keyBytes.baseAddress,
                            key.count,
                            ivBytes.baseAddress,
                            dataBytes.baseAddress,
                            data.count,
                            outputBytes.baseAddress,
                            outputCapacity,
                            &outputLength
                        )
                    }
                }
            }
        }
        guard status == kCCSuccess else { throw K3Error.userFacing("AES failed: \(status).") }
        output.removeSubrange(outputLength..<output.count)
        return output
    }

    private static func hmacSHA256(key: Data, data: Data) -> Data {
        var mac = Data(repeating: 0, count: Int(CC_SHA256_DIGEST_LENGTH))
        mac.withUnsafeMutableBytes { macBytes in
            key.withUnsafeBytes { keyBytes in
                data.withUnsafeBytes { dataBytes in
                    CCHmac(CCHmacAlgorithm(kCCHmacAlgSHA256), keyBytes.baseAddress, key.count, dataBytes.baseAddress, data.count, macBytes.baseAddress)
                }
            }
        }
        return mac
    }

    private static func littleEndianInt32(_ value: Int) -> Data {
        var v = Int32(value).littleEndian
        return Data(bytes: &v, count: 4)
    }

    private static func readLittleEndianInt32(_ data: Data, offset: Int) -> Int {
        let value = data.withUnsafeBytes { rawBuffer -> Int32 in
            let ptr = rawBuffer.baseAddress!.advanced(by: offset).assumingMemoryBound(to: UInt8.self)
            return Int32(ptr[0]) | (Int32(ptr[1]) << 8) | (Int32(ptr[2]) << 16) | (Int32(ptr[3]) << 24)
        }
        return Int(Int32(littleEndian: value))
    }

    private static func gzip(_ data: Data) throws -> Data {
        try runGzip(arguments: ["-c"], input: data)
    }

    private static func gunzip(_ data: Data) throws -> Data {
        try runGzip(arguments: ["-cd"], input: data)
    }

    private static func runGzip(arguments: [String], input: Data) throws -> Data {
        let temporaryDirectory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        try FileManager.default.createDirectory(at: temporaryDirectory, withIntermediateDirectories: true)
        defer {
            try? FileManager.default.removeItem(at: temporaryDirectory)
        }

        let inputURL = temporaryDirectory.appendingPathComponent("input.bin")
        let outputURL = temporaryDirectory.appendingPathComponent("output.gz")
        try input.write(to: inputURL, options: .atomic)
        FileManager.default.createFile(atPath: outputURL.path, contents: nil)

        guard let outputHandle = try? FileHandle(forWritingTo: outputURL) else {
            throw K3Error.userFacing("Cannot prepare gzip output.")
        }
        defer {
            try? outputHandle.close()
        }

        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/gzip")
        process.arguments = arguments + [inputURL.path]

        let errorPipe = Pipe()
        process.standardOutput = outputHandle
        process.standardError = errorPipe

        try process.run()
        process.waitUntilExit()

        if process.terminationStatus != 0 {
            let error = String(data: errorPipe.fileHandleForReading.readDataToEndOfFile(), encoding: .utf8) ?? "gzip failed"
            throw K3Error.userFacing(error)
        }

        try outputHandle.close()
        return try Data(contentsOf: outputURL)
    }
}
