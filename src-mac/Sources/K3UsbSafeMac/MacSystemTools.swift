import Foundation

enum MacSystemTools {
    static func hide(_ url: URL) throws {
        guard FileManager.default.fileExists(atPath: url.path) else { return }
        _ = try run("/usr/bin/chflags", ["hidden", url.path])
    }

    static func clearHidden(_ url: URL) {
        guard FileManager.default.fileExists(atPath: url.path) else { return }
        _ = try? run("/usr/bin/chflags", ["nohidden", url.path])
    }

    static func ejectVolume(at root: URL) throws {
        _ = try run("/usr/sbin/diskutil", ["eject", root.path])
    }

    static func run(_ executable: String, _ arguments: [String]) throws -> String {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: executable)
        process.arguments = arguments
        let pipe = Pipe()
        process.standardOutput = pipe
        process.standardError = pipe
        try process.run()
        process.waitUntilExit()
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        let output = String(data: data, encoding: .utf8) ?? ""
        guard process.terminationStatus == 0 else {
            throw K3Error.userFacing(output.isEmpty ? "Command failed: \(executable)" : output)
        }
        return output
    }
}
