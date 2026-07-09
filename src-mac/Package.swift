// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "K3UsbSafeMac",
    platforms: [
        .macOS(.v13)
    ],
    products: [
        .executable(name: "K3UsbSafeMac", targets: ["K3UsbSafeMac"])
    ],
    targets: [
        .executableTarget(
            name: "K3UsbSafeMac",
            linkerSettings: [
                .linkedFramework("Security")
            ]
        )
    ]
)
