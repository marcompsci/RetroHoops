// Retro Hoops — App Store purchase bridge (original code), StoreKit 2.
// Called from C# via [DllImport("__Internal")] through the @_cdecl functions at the bottom.
// One product (Phase 41): Retro Hoops Pro, a one-time non-consumable unlock. (It was the Live monthly
// subscription before; the code handles both.) The App Store decides whether it's owned
// (Transaction.currentEntitlements); nothing is stored on any server of ours.
import Foundation
import StoreKit

// State codes read by C#: 0 loading, 1 not owned, 2 owned, 3 purchasing, 4 failed, 5 pending (Ask to Buy).
private let kLoading: Int32 = 0
private let kNotSubscribed: Int32 = 1
private let kSubscribed: Int32 = 2
private let kPurchasing: Int32 = 3
private let kFailed: Int32 = 4
private let kPending: Int32 = 5

private final class RetroStoreModel {
    static let shared = RetroStoreModel()

    private let lock = NSLock()
    private var productId = ""
    private var product: Product?
    private var stateValue: Int32 = kLoading
    private var priceValue = ""
    private var expiresValue: Double = 0
    private var errorValue = ""
    private var originalIdValue = ""
    private var accountToken: UUID?
    private var listener: Task<Void, Never>?

    var state: Int32 { lock.lock(); defer { lock.unlock() }; return stateValue }
    var price: String { lock.lock(); defer { lock.unlock() }; return priceValue }
    var expires: Double { lock.lock(); defer { lock.unlock() }; return expiresValue }
    var error: String { lock.lock(); defer { lock.unlock() }; return errorValue }
    var originalId: String { lock.lock(); defer { lock.unlock() }; return originalIdValue }

    /// Tags purchases with the player's account token (from the Retro Hoops server) so the server can tell whose they are.
    func setAccountToken(_ s: String) {
        lock.lock(); accountToken = UUID(uuidString: s); lock.unlock()
    }

    private func apply(state: Int32? = nil, price: String? = nil, expires: Double? = nil, error: String? = nil, originalId: String? = nil) {
        lock.lock()
        if let o = originalId { originalIdValue = o }
        if let s = state { stateValue = s }
        if let p = price { priceValue = p }
        if let e = expires { expiresValue = e }
        if let m = error { errorValue = m }
        lock.unlock()
    }

    func start(_ id: String) {
        lock.lock(); productId = id; lock.unlock()
        if listener == nil {
            // Renewals, refunds and purchases made on other devices arrive here while the game runs.
            listener = Task.detached { [weak self] in
                for await update in Transaction.updates {
                    if case .verified(let transaction) = update {
                        await transaction.finish()
                    }
                    await self?.refresh()
                }
            }
        }
        Task { [weak self] in
            guard let self = self else { return }
            do {
                let products = try await Product.products(for: [id])
                if let p = products.first {
                    self.product = p
                    self.apply(price: p.displayPrice)
                }
            } catch {
                self.apply(error: error.localizedDescription)
            }
            await self.refresh()
        }
    }

    // Phase 36: async code reads shared state through these synchronous helpers (NSLock's lock/unlock can't be
    // called directly from an async context; that's an error in Swift 6 mode).
    private func lockedProductId() -> String { lock.lock(); defer { lock.unlock() }; return productId }
    private func lockedAccountToken() -> UUID? { lock.lock(); defer { lock.unlock() }; return accountToken }

    func refresh() async {
        let wanted = lockedProductId()
        var active = false
        var until: Double = 0
        var original = ""
        for await result in Transaction.currentEntitlements {
            guard case .verified(let t) = result, t.productID == wanted, t.revocationDate == nil else { continue }
            if let end = t.expirationDate {
                if end > Date() {
                    active = true
                    until = max(until, end.timeIntervalSince1970)
                }
                original = String(t.originalID)
            } else {
                active = true
            }
        }
        let current = state
        if current == kPurchasing && !active { return } // a purchase in progress decides the state
        apply(state: active ? kSubscribed : kNotSubscribed, expires: until, originalId: original)
    }

    func buy() {
        guard let p = product else {
            apply(state: kFailed, error: "The App Store didn't return Retro Hoops Pro. Check your connection and try again.")
            return
        }
        apply(state: kPurchasing, error: "")
        Task { [weak self] in
            guard let self = self else { return }
            do {
                var options = Set<Product.PurchaseOption>()
                let token = self.lockedAccountToken()
                if let token = token { options.insert(.appAccountToken(token)) }
                let result = try await p.purchase(options: options)
                switch result {
                case .success(let verification):
                    if case .verified(let t) = verification {
                        await t.finish()
                        self.apply(state: kNotSubscribed)
                        await self.refresh()
                    } else {
                        self.apply(state: kFailed, error: "The purchase couldn't be verified.")
                    }
                case .pending:
                    self.apply(state: kPending)
                case .userCancelled:
                    self.apply(state: kNotSubscribed)
                    await self.refresh()
                @unknown default:
                    self.apply(state: kNotSubscribed)
                    await self.refresh()
                }
            } catch {
                self.apply(state: kFailed, error: error.localizedDescription)
            }
        }
    }

    func restore() {
        apply(state: kLoading, error: "")
        Task { [weak self] in
            guard let self = self else { return }
            do {
                try await AppStore.sync()
            } catch {
                self.apply(error: error.localizedDescription)
            }
            await self.refresh()
        }
    }
}

private func retroCopy(_ s: String) -> UnsafeMutablePointer<CChar>? {
    return strdup(s) // freed by the C# marshaller
}

@_cdecl("RetroStore_Start")
public func RetroStore_Start(_ productId: UnsafePointer<CChar>?) {
    guard let productId = productId else { return }
    RetroStoreModel.shared.start(String(cString: productId))
}

@_cdecl("RetroStore_State")
public func RetroStore_State() -> Int32 {
    return RetroStoreModel.shared.state
}

@_cdecl("RetroStore_Price")
public func RetroStore_Price() -> UnsafeMutablePointer<CChar>? {
    return retroCopy(RetroStoreModel.shared.price)
}

@_cdecl("RetroStore_Error")
public func RetroStore_Error() -> UnsafeMutablePointer<CChar>? {
    return retroCopy(RetroStoreModel.shared.error)
}

@_cdecl("RetroStore_Expires")
public func RetroStore_Expires() -> Double {
    return RetroStoreModel.shared.expires
}

@_cdecl("RetroStore_OriginalTransactionId")
public func RetroStore_OriginalTransactionId() -> UnsafeMutablePointer<CChar>? {
    return retroCopy(RetroStoreModel.shared.originalId)
}

@_cdecl("RetroStore_SetAccountToken")
public func RetroStore_SetAccountToken(_ token: UnsafePointer<CChar>?) {
    guard let token = token else { return }
    RetroStoreModel.shared.setAccountToken(String(cString: token))
}

@_cdecl("RetroStore_Buy")
public func RetroStore_Buy() {
    RetroStoreModel.shared.buy()
}

@_cdecl("RetroStore_Restore")
public func RetroStore_Restore() {
    RetroStoreModel.shared.restore()
}

@_cdecl("RetroStore_Refresh")
public func RetroStore_Refresh() {
    Task { await RetroStoreModel.shared.refresh() }
}
