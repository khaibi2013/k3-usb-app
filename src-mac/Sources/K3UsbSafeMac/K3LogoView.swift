import AppKit
import SwiftUI

struct K3LogoView: View {
    var size: CGFloat = 56
    var cornerRadius: CGFloat = 10

    var body: some View {
        Group {
            if let image = Self.logoImage {
                Image(nsImage: image)
                    .resizable()
                    .scaledToFill()
            } else {
                ZStack {
                    RoundedRectangle(cornerRadius: cornerRadius)
                        .fill(Color.teal.opacity(0.15))
                    Image(systemName: "lock.shield.fill")
                        .font(.system(size: size * 0.48, weight: .semibold))
                        .foregroundStyle(.teal)
                }
            }
        }
        .frame(width: size, height: size)
        .clipShape(RoundedRectangle(cornerRadius: cornerRadius))
    }

    private static var logoImage: NSImage? {
        guard let url = Bundle.module.url(forResource: "K3Logo", withExtension: "jpg") else { return nil }
        return NSImage(contentsOf: url)
    }
}
