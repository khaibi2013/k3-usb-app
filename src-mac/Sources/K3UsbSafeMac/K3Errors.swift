import Foundation

enum K3Error: LocalizedError {
    case userFacing(String)

    var errorDescription: String? {
        switch self {
        case .userFacing(let message):
            return message
        }
    }
}
