import Foundation

enum K3Version {
    static let appVersion = "0.4.0-mac"
    static let buildDate = "2026-07-10"
    static let channel = "portable"

    static var display: String {
        "\(appVersion) (\(channel), \(buildDate))"
    }

    static let changelog = [
        "First-run wizard",
        "Folder encryption with structure preservation",
        "USB integrity manifest",
        "K3 rule updater",
        "Quarantine detail panel",
        "Drag and drop encrypt/decrypt",
        "macOS clone warning"
    ]
}
