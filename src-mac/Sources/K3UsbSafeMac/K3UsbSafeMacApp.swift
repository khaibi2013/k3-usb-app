import SwiftUI

@main
struct K3UsbSafeMacApp: App {
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
