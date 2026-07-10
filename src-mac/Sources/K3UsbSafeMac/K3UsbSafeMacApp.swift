import AppKit
import SwiftUI

final class K3AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)

        DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) {
            NSApp.windows.first?.makeKeyAndOrderFront(nil)
        }
    }
}

@main
struct K3UsbSafeMacApp: App {
    @NSApplicationDelegateAdaptor(K3AppDelegate.self) private var appDelegate
    @StateObject private var appState = AppState()

    var body: some Scene {
        WindowGroup {
            Group {
                if appState.isAuthenticated {
                    MainView()
                } else {
                    LoginView()
                }
            }
            .environmentObject(appState)
            .frame(minWidth: 980, minHeight: 640)
            .onAppear {
                appState.bootstrap()
            }
        }
        .windowStyle(.titleBar)
    }
}
