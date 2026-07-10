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

    static func ejectVolumeAfterAppTerminates(at root: URL) throws {
        let scriptURL = FileManager.default.temporaryDirectory
            .appendingPathComponent("k3-eject-\(UUID().uuidString).zsh")
        let script = """
        #!/bin/zsh
        sleep 1
        /usr/sbin/diskutil eject "\(root.path.replacingOccurrences(of: "\"", with: "\\\""))"
        rm -f "$0"
        """
        try script.write(to: scriptURL, atomically: true, encoding: .utf8)
        try FileManager.default.setAttributes([.posixPermissions: 0o700], ofItemAtPath: scriptURL.path)

        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/zsh")
        process.arguments = [scriptURL.path]
        process.standardOutput = nil
        process.standardError = nil
        try process.run()
    }

    static func volumeIdentifier(at root: URL) -> String {
        guard let output = try? run("/usr/sbin/diskutil", ["info", root.path]) else {
            return "path:\(root.path)"
        }
        for line in output.split(whereSeparator: \.isNewline) {
            let text = String(line)
            if text.contains("Volume UUID:"),
               let value = text.split(separator: ":", maxSplits: 1).last?.trimmingCharacters(in: .whitespacesAndNewlines),
               !value.isEmpty {
                return "mac-volume:\(value)"
            }
            if text.contains("Disk / Partition UUID:"),
               let value = text.split(separator: ":", maxSplits: 1).last?.trimmingCharacters(in: .whitespacesAndNewlines),
               !value.isEmpty {
                return "mac-partition:\(value)"
            }
        }
        return "path:\(root.path)"
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
