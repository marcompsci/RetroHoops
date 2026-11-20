// Retro Hoops — SHOP purchase bridge (original code), StoreKit 2. I142, 20 Nov 2026.
// Many products: one-time unlocks (court packs, gear packs, Court Designer Plus, ALL-ACCESS) and consumable tips.
// Called from C# (Scripts/Core/ShopStore.cs) through the @_cdecl functions at the bottom.
// The App Store decides what's owned (Transaction.currentEntitlements); nothing is stored on any server of ours.
// Retro Hoops Pro keeps its own bridge (RetroStore.swift).
import Foundation
import StoreKit

// Purchase state codes read by C#: 0 idle, 1 purchasing, 2 pending (Ask to Buy), 3 failed, 4 done.
private let kIdle: Int32 = 0
private let kBuying: Int32 = 1
private let kWaiting: Int32 = 2
private let kBuyFailed: Int32 = 3
private let kDone: Int32 = 4

private final class RetroShopModel {
    static let shared = RetroShopModel()

    private let lock = NSLock()
    private var productIds: [String] = []
    private var products: [String: Product] = [:]
    private var prices: [String: String] = [:]
    private var owned: Set<String> = []
    private var ready = false
    private var buyState: Int32 = kIdle
    private var errorText = ""
    // Tips (consumables) that went through and C# hasn't collected yet, and every tip transaction already counted.
    private var tipQueue: [String] = []
    private var countedTips: Set<UInt64> = []
    private var listener: Task<Void, Never>?

    // Synchronous, locked accessors (async code goes through these, never NSLock directly).
    func isReady() -> Bool { lock.lock(); defer { lock.unlock() }; return ready }
    func ownedCsv() -> String { lock.lock(); defer { lock.unlock() }; return owned.sorted().joined(separator: ",") }
    func price(_ id: String) -> String { lock.lock(); defer { lock.unlock() }; return prices[id] ?? "" }
    func state() -> Int32 { lock.lock(); defer { lock.unlock() }; return buyState }
    func error() -> String { lock.lock(); defer { lock.unlock() }; return errorText }
    private func ids() -> [String] { lock.lock(); defer { lock.unlock() }; return productIds }
    private func product(_ id: String) -> Product? { lock.lock(); defer { lock.unlock() }; return products[id] }

    func takeTipsCsv() -> String {
        lock.lock(); defer { lock.unlock() }
        let s = tipQueue.joined(separator: ",")
        tipQueue.removeAll()
        return s
    }

    private func setState(_ s: Int32, _ e: String? = nil) {
        lock.lock()
        buyState = s
        if let e = e { errorText = e }
        lock.unlock()
    }

    private func store(products list: [Product]) {
        lock.lock()
        for p in list {
            products[p.id] = p
            prices[p.id] = p.displayPrice
        }
        lock.unlock()
    }

    private func setOwned(_ set: Set<String>) {
        lock.lock()
        owned = set
        ready = true
        lock.unlock()
    }

    /// Counts a verified tip once (by transaction id) and queues it for C#. Returns false if already counted.
    private func countTip(_ t: Transaction) -> Bool {
        lock.lock(); defer { lock.unlock() }
        if countedTips.contains(t.id) { return false }
        countedTips.insert(t.id)
        tipQueue.append(t.productID)
        return true
    }

    /// A verified transaction: tips are counted (consumables), everything is finished, then ownership is refreshed.
    private func handle(_ t: Transaction) async {
        if t.productType == .consumable && t.revocationDate == nil {
            _ = countTip(t)
        }
        await t.finish()
    }

    func start(_ csv: String) {
        let list = csv.split(separator: ",").map { String($0) }.filter { !$0.isEmpty }
        lock.lock(); productIds = list; lock.unlock()
        if listener == nil {
            // Purchases approved later (Ask to Buy), refunds and purchases on other devices arrive here.
            listener = Task.detached { [weak self] in
                for await update in Transaction.updates {
                    if case .verified(let t) = update {
                        await self?.handle(t)
                    }
                    await self?.refresh()
                }
            }
        }
        Task { [weak self] in
            guard let self = self else { return }
            do {
                let found = try await Product.products(for: list)
                self.store(products: found)
            } catch {
                self.setState(self.state(), error.localizedDescription)
            }
            // Tips bought while the game wasn't running are waiting as unfinished transactions.
            for await result in Transaction.unfinished {
                if case .verified(let t) = result {
                    await self.handle(t)
                }
            }
            await self.refresh()
        }
    }

    func refresh() async {
        let wanted = Set(ids())
        var set = Set<String>()
        for await result in Transaction.currentEntitlements {
            guard case .verified(let t) = result, wanted.contains(t.productID), t.revocationDate == nil else { continue }
            if t.productType == .consumable { continue }
            set.insert(t.productID)
        }
        setOwned(set)
    }

    func buy(_ id: String) {
        guard let p = product(id) else {
            setState(kBuyFailed, "The App Store didn't return that item. Check your connection and try again.")
            return
        }
        setState(kBuying, "")
        Task { [weak self] in
            guard let self = self else { return }
            do {
                let result = try await p.purchase()
                switch result {
                case .success(let verification):
                    if case .verified(let t) = verification {
                        await self.handle(t)
                        await self.refresh()
                        self.setState(kDone)
                    } else {
                        self.setState(kBuyFailed, "The purchase couldn't be verified.")
                    }
                case .pending:
                    self.setState(kWaiting)
                case .userCancelled:
                    self.setState(kIdle)
                @unknown default:
                    self.setState(kIdle)
                }
            } catch {
                self.setState(kBuyFailed, error.localizedDescription)
            }
        }
    }

    func restore() {
        setState(kIdle, "")
        Task { [weak self] in
            guard let self = self else { return }
            do {
                try await AppStore.sync()
            } catch {
                self.setState(kIdle, error.localizedDescription)
            }
            await self.refresh()
        }
    }
}

private func retroShopCopy(_ s: String) -> UnsafeMutablePointer<CChar>? {
    return strdup(s) // freed by the C# marshaller
}

@_cdecl("RetroShop_Start")
public func RetroShop_Start(_ ids: UnsafePointer<CChar>?) {
    guard let ids = ids else { return }
    RetroShopModel.shared.start(String(cString: ids))
}

@_cdecl("RetroShop_Ready")
public func RetroShop_Ready() -> Int32 {
    return RetroShopModel.shared.isReady() ? 1 : 0
}

@_cdecl("RetroShop_OwnedCsv")
public func RetroShop_OwnedCsv() -> UnsafeMutablePointer<CChar>? {
    return retroShopCopy(RetroShopModel.shared.ownedCsv())
}

@_cdecl("RetroShop_Price")
public func RetroShop_Price(_ id: UnsafePointer<CChar>?) -> UnsafeMutablePointer<CChar>? {
    guard let id = id else { return retroShopCopy("") }
    return retroShopCopy(RetroShopModel.shared.price(String(cString: id)))
}

@_cdecl("RetroShop_Buy")
public func RetroShop_Buy(_ id: UnsafePointer<CChar>?) {
    guard let id = id else { return }
    RetroShopModel.shared.buy(String(cString: id))
}

@_cdecl("RetroShop_BuyState")
public func RetroShop_BuyState() -> Int32 {
    return RetroShopModel.shared.state()
}

@_cdecl("RetroShop_Error")
public func RetroShop_Error() -> UnsafeMutablePointer<CChar>? {
    return retroShopCopy(RetroShopModel.shared.error())
}

@_cdecl("RetroShop_TakeTipsCsv")
public func RetroShop_TakeTipsCsv() -> UnsafeMutablePointer<CChar>? {
    return retroShopCopy(RetroShopModel.shared.takeTipsCsv())
}

@_cdecl("RetroShop_Restore")
public func RetroShop_Restore() {
    RetroShopModel.shared.restore()
}
