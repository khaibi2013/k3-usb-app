import SwiftUI

struct LoginView: View {
    private enum LoginField {
        case password
        case confirmPassword
        case decoyPassword
    }

    @EnvironmentObject private var appState: AppState
    @State private var setupStep = 0
    @State private var password = ""
    @State private var confirmPassword = ""
    @State private var decoyPassword = ""
    @State private var autoScanOnLogin = true
    @State private var selfDestructMode = "off"
    @State private var errorText = ""
    @FocusState private var focusedField: LoginField?

    var body: some View {
        VStack(spacing: 22) {
            K3LogoView(size: 82, cornerRadius: 16)
                .shadow(color: .teal.opacity(0.18), radius: 14, y: 6)

            Text(appState.config.loginTitle)
                .font(.title2.bold())

            if appState.config.needsInitialSetup {
                setupWizard
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

    private var setupWizard: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 6) {
                ForEach(0..<5, id: \.self) { index in
                    Capsule()
                        .fill(index <= setupStep ? Color.teal : Color.secondary.opacity(0.22))
                        .frame(height: 5)
                }
            }

            Text(setupTitle)
                .font(.headline)
            Text(setupSubtitle)
                .font(.caption)
                .foregroundStyle(.secondary)

            Group {
                switch setupStep {
                case 0:
                    VStack(spacing: 10) {
                        SecureField("Mat khau that moi", text: $password)
                            .textFieldStyle(.roundedBorder)
                            .focused($focusedField, equals: .password)
                            .onSubmit { focusedField = .confirmPassword }
                        SecureField("Nhap lai mat khau", text: $confirmPassword)
                            .textFieldStyle(.roundedBorder)
                            .focused($focusedField, equals: .confirmPassword)
                            .onSubmit { nextSetupStep() }
                    }
                case 1:
                    VStack(alignment: .leading, spacing: 10) {
                        SecureField("Mat khau gia (tuy chon)", text: $decoyPassword)
                            .textFieldStyle(.roundedBorder)
                            .focused($focusedField, equals: .decoyPassword)
                            .onSubmit { nextSetupStep() }
                        Text("Mat khau gia mo ket .vault_decoy de bao ve ban khi bi ep mo USB.")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                case 2:
                    VStack(alignment: .leading, spacing: 10) {
                        Picker("Nhap sai 10 lan", selection: $selfDestructMode) {
                            Text("Tat tu huy, chi khoa 5 phut").tag("off")
                            Text("Khoa vinh vien").tag("lock")
                            Text("Xoa ket that").tag("wipe_real")
                            Text("Xoa tat ca du lieu bao mat").tag("wipe_all")
                        }
                        Text("Canh bao: cac lua chon xoa se xoa du lieu that khi nhap sai 10 lan.")
                            .font(.caption)
                            .foregroundStyle(.red)
                    }
                case 3:
                    VStack(alignment: .leading, spacing: 10) {
                        Toggle("Tu dong quet USB sau khi dang nhap", isOn: $autoScanOnLogin)
                        Label(appState.antivirusEngineInfo.clamAvailable ? "ClamAV portable san sang" : "Chua co ClamAV, K3 rules van hoat dong", systemImage: appState.antivirusEngineInfo.clamAvailable ? "checkmark.shield" : "exclamationmark.triangle")
                            .foregroundStyle(appState.antivirusEngineInfo.clamAvailable ? .green : .orange)
                    }
                default:
                    VStack(alignment: .leading, spacing: 10) {
                        Label("San sang tao ket USB An Toan K3", systemImage: "checkmark.seal")
                            .font(.headline)
                        Text("K3 se tao .vault, .vault_decoy, BaoMat va .vault_config.json tren USB.")
                            .foregroundStyle(.secondary)
                    }
                }
            }

            HStack {
                Button("Quay lai") {
                    errorText = ""
                    setupStep = max(0, setupStep - 1)
                }
                .disabled(setupStep == 0)
                Spacer()
                Button(setupStep == 4 ? "Tao ket sat" : "Tiep tuc") {
                    if setupStep == 4 {
                        setup()
                    } else {
                        nextSetupStep()
                    }
                }
                .buttonStyle(.borderedProminent)
            }
        }
    }

    private var setupTitle: String {
        switch setupStep {
        case 0: return "Buoc 1: Mat khau that"
        case 1: return "Buoc 2: Mat khau gia"
        case 2: return "Buoc 3: Chinh sach tu huy"
        case 3: return "Buoc 4: Quet virus"
        default: return "Buoc 5: Hoan tat"
        }
    }

    private var setupSubtitle: String {
        switch setupStep {
        case 0: return "Mat khau nay mo ket that .vault."
        case 1: return "Co the bo trong neu khong dung che do ket gia."
        case 2: return "Chon cach xu ly neu nhap sai qua nhieu lan."
        case 3: return "Bat/tat quet USB sau khi dang nhap."
        default: return "Kiem tra lai va tao ket."
        }
    }

    private func nextSetupStep() {
        errorText = ""
        if setupStep == 0 {
            guard password.count >= 6 else {
                errorText = "Mat khau can it nhat 6 ky tu."
                return
            }
            guard password == confirmPassword else {
                errorText = "Hai mat khau khong khop."
                return
            }
        }
        if setupStep == 1, !decoyPassword.isEmpty, decoyPassword == password {
            errorText = "Mat khau gia phai khac mat khau that."
            return
        }
        setupStep = min(4, setupStep + 1)
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
            try appState.setupPassword(
                realPassword: password,
                decoyPassword: decoyPassword,
                autoScanOnLogin: autoScanOnLogin,
                selfDestructMode: selfDestructMode
            )
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
