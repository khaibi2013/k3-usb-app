import SwiftUI

struct LoginView: View {
    private enum LoginField {
        case password
        case confirmPassword
        case decoyPassword
    }

    @EnvironmentObject private var appState: AppState
    @State private var password = ""
    @State private var confirmPassword = ""
    @State private var decoyPassword = ""
    @State private var errorText = ""
    @FocusState private var focusedField: LoginField?

    var body: some View {
        VStack(spacing: 22) {
            Image(systemName: "externaldrive.badge.shield.checkmark")
                .font(.system(size: 62))
                .foregroundStyle(.teal)

            Text(appState.config.loginTitle)
                .font(.title2.bold())

            if appState.config.needsInitialSetup {
                SecureField("Mat khau moi", text: $password)
                    .textFieldStyle(.roundedBorder)
                    .focused($focusedField, equals: .password)
                    .onSubmit { focusedField = .confirmPassword }
                SecureField("Nhap lai mat khau", text: $confirmPassword)
                    .textFieldStyle(.roundedBorder)
                    .focused($focusedField, equals: .confirmPassword)
                    .onSubmit { focusedField = .decoyPassword }
                SecureField("Mat khau gia (tuy chon)", text: $decoyPassword)
                    .textFieldStyle(.roundedBorder)
                    .focused($focusedField, equals: .decoyPassword)
                    .onSubmit { setup() }
                Button("Tao ket sat") { setup() }
                    .buttonStyle(.borderedProminent)
            } else {
                SecureField("Mat khau", text: $password)
                    .textFieldStyle(.roundedBorder)
                    .focused($focusedField, equals: .password)
                    .onSubmit { login() }
                Button("Dang nhap") { login() }
                    .buttonStyle(.borderedProminent)
            }

            if !errorText.isEmpty {
                Text(errorText)
                    .foregroundStyle(.red)
                    .font(.callout)
            }

            HStack {
                Circle()
                    .fill(appState.isAuthenticated ? .green : .red)
                    .frame(width: 10, height: 10)
                Text(appState.statusMessage)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
        .padding(40)
        .frame(width: 420)
        .onAppear {
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) {
                focusedField = .password
            }
        }
    }

    private func setup() {
        errorText = ""
        guard password.count >= 6 else {
            errorText = "Mat khau can it nhat 6 ky tu."
            return
        }
        guard password == confirmPassword else {
            errorText = "Hai mat khau khong khop."
            return
        }
        do {
            try appState.setupPassword(realPassword: password, decoyPassword: decoyPassword)
        } catch {
            errorText = error.localizedDescription
        }
    }

    private func login() {
        errorText = ""
        do {
            try appState.login(password: password)
        } catch {
            errorText = error.localizedDescription
        }
    }
}
