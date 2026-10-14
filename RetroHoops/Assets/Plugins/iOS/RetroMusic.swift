// Retro Hoops — Apple Music bridge for the in-game phone's MUSIC app (original code), MusicKit (iOS 15+).
// Called from C# via [DllImport("__Internal")] through the @_cdecl functions at the bottom.
//
// - Asks the player for Apple Music access (NSAppleMusicUsageDescription).
// - Finds each playlist song in the Apple Music catalog, the clean version first (15 Oct 2026: explicit ones too,
//   flagged, so the game's EXPLICIT SONGS setting can decide). Before: clean only: a song was kept only
//   when the catalog has a version that isn't rated explicit.
// - Plays the queue C# gives it (C# decides the shuffle order) with ApplicationMusicPlayer, which needs an
//   Apple Music subscription. Without one, C# keeps the game's own soundtrack, and the + buttons still open
//   each song in Apple Music.
// Nothing leaves the phone except Apple's own MusicKit requests. No data comes to us.
import Foundation
import MusicKit
import MediaPlayer
import UIKit

// Auth codes read by C#: 0 not asked, 1 denied, 2 restricted, 3 authorized.
// Resolve codes: 0 idle, 1 working, 2 done, 3 failed.
private final class RetroMusicModel {
    static let shared = RetroMusicModel()

    private let lock = NSLock()
    private var authValue: Int32 = 0
    private var canPlayValue: Int32 = 0       // 1 = has an Apple Music subscription that can play catalog songs
    private var resolveStateValue: Int32 = 0
    private var resolvedJson = "[]"
    private var errorValue = ""
    private var songs: [String: Song] = [:]  // catalog id -> song

    var auth: Int32 { lock.lock(); defer { lock.unlock() }; return authValue }
    var canPlay: Int32 { lock.lock(); defer { lock.unlock() }; return canPlayValue }
    var resolveState: Int32 { lock.lock(); defer { lock.unlock() }; return resolveStateValue }
    var resolved: String { lock.lock(); defer { lock.unlock() }; return resolvedJson }
    var error: String { lock.lock(); defer { lock.unlock() }; return errorValue }

    private func set(auth: Int32? = nil, canPlay: Int32? = nil, resolveState: Int32? = nil, json: String? = nil, error: String? = nil) {
        lock.lock()
        if let a = auth { authValue = a }
        if let c = canPlay { canPlayValue = c }
        if let r = resolveState { resolveStateValue = r }
        if let j = json { resolvedJson = j }
        if let e = error { errorValue = e }
        lock.unlock()
    }

    private static func code(_ s: MusicAuthorization.Status) -> Int32 {
        switch s {
        case .authorized: return 3
        case .restricted: return 2
        case .denied: return 1
        default: return 0
        }
    }

    /// Reads the current status without asking.
    func refresh() {
        set(auth: RetroMusicModel.code(MusicAuthorization.currentStatus))
        Task { await self.checkSubscription() }
    }

    func requestAccess() {
        Task {
            let status = await MusicAuthorization.request()
            self.set(auth: RetroMusicModel.code(status))
            await self.checkSubscription()
        }
    }

    private func checkSubscription() async {
        guard MusicAuthorization.currentStatus == .authorized else { set(canPlay: 0); return }
        do {
            let sub = try await MusicSubscription.current
            set(canPlay: sub.canPlayCatalogContent ? 1 : 0)
        } catch {
            set(canPlay: 0, error: "\(error)")
        }
    }

    private static func norm(_ s: String) -> String {
        return s.lowercased().filter { $0.isLetter || $0.isNumber }
    }

    /// `request` is lines of "key\ttitle\tartist" (one per song). Result JSON: [{key,id,url,artwork,title,artist,album,seconds}].
    func resolve(_ request: String) {
        set(resolveState: 1)
        Task {
            guard MusicAuthorization.currentStatus == .authorized else {
                self.set(resolveState: 3, error: "Apple Music access not allowed")
                return
            }
            var out: [[String: Any]] = []
            var found: [String: Song] = [:]
            for line in request.split(separator: "\n") {
                let parts = line.split(separator: "\t", omittingEmptySubsequences: false).map(String.init)
                if parts.count < 3 { continue }
                let key = parts[0], title = parts[1], artist = parts[2]
                do {
                    // 15 Oct 2026: the clean version when Apple Music has one, otherwise the explicit one (the
                    // game's EXPLICIT SONGS setting decides whether those play). Matching is loose on purpose:
                    // titles often carry "(feat. ...)" and artist names differ in capitals.
                    let wantTitle = RetroMusicModel.norm(title)
                    let wantArtist = RetroMusicModel.norm(artist)
                    func matches(_ s: Song) -> Bool {
                        let t = RetroMusicModel.norm(s.title), a = RetroMusicModel.norm(s.artistName)
                        return (t.hasPrefix(wantTitle) || wantTitle.hasPrefix(t)) && (a.contains(wantArtist) || wantArtist.contains(a))
                    }
                    var candidates: [Song] = []
                    for term in [title + " " + artist, title] {
                        var search = MusicCatalogSearchRequest(term: term, types: [Song.self])
                        search.limit = 25
                        let response = try await search.response()
                        candidates = response.songs.filter(matches)
                        if !candidates.isEmpty { break }
                    }
                    let match = candidates.first { $0.contentRating != .explicit } ?? candidates.first
                    guard let song = match else { continue }
                    found[song.id.rawValue] = song
                    var row: [String: Any] = [
                        "key": key,
                        "id": song.id.rawValue,
                        "title": song.title,
                        "artist": song.artistName,
                        "album": song.albumTitle ?? "",
                        "seconds": song.duration ?? 0,
                        "explicit": song.contentRating == .explicit,
                    ]
                    if let url = song.url { row["url"] = url.absoluteString }
                    if let art = song.artwork?.url(width: 300, height: 300) { row["artwork"] = art.absoluteString }
                    out.append(row)
                } catch {
                    self.set(error: "\(error)")
                }
            }
            let data = (try? JSONSerialization.data(withJSONObject: out)) ?? Data("[]".utf8)
            self.store(found)
            self.set(resolveState: 2, json: String(data: data, encoding: .utf8) ?? "[]")
        }
    }

    /// Keeps the found songs (a plain function, so the lock isn't taken directly inside async code).
    private func store(_ found: [String: Song]) {
        lock.lock(); songs.merge(found) { _, new in new }; lock.unlock()
    }

    // ------------------------------------------------------------------ 15 Oct 2026: MY MIX (each player's own songs)
    // One request at a time: 1 search the catalog, 2 look up songs by id (saved MY MIX, a pasted link),
    // 3 pick from the player's own library (the system picker). Codes: 0 idle, 1 working, 2 done, 3 failed.

    private var requestStateValue: Int32 = 0
    private var requestJson = "[]"
    var requestState: Int32 { lock.lock(); defer { lock.unlock() }; return requestStateValue }
    var requestResult: String { lock.lock(); defer { lock.unlock() }; return requestJson }

    private func finish(_ list: [Song], failed: String? = nil) {
        var out: [[String: Any]] = []
        var found: [String: Song] = [:]
        for song in list {
            found[song.id.rawValue] = song
            var row: [String: Any] = [
                "key": "mix:" + song.id.rawValue,
                "id": song.id.rawValue,
                "title": song.title,
                "artist": song.artistName,
                "album": song.albumTitle ?? "",
                "seconds": song.duration ?? 0,
                "explicit": song.contentRating == .explicit,
            ]
            if let url = song.url { row["url"] = url.absoluteString }
            if let art = song.artwork?.url(width: 300, height: 300) { row["artwork"] = art.absoluteString }
            out.append(row)
        }
        store(found)
        let data = (try? JSONSerialization.data(withJSONObject: out)) ?? Data("[]".utf8)
        lock.lock()
        requestJson = String(data: data, encoding: .utf8) ?? "[]"
        requestStateValue = failed == nil ? 2 : 3
        if let f = failed { errorValue = f }
        lock.unlock()
    }

    private func begin() -> Bool {
        lock.lock(); defer { lock.unlock() }
        if requestStateValue == 1 { return false }
        requestStateValue = 1
        requestJson = "[]"
        return true
    }

    func search(_ term: String) {
        guard begin() else { return }
        Task {
            do {
                var request = MusicCatalogSearchRequest(term: term, types: [Song.self])
                request.limit = 25
                let response = try await request.response()
                self.finish(Array(response.songs))
            } catch { self.finish([], failed: "\(error)") }
        }
    }

    func lookup(_ idsCsv: String) {
        guard begin() else { return }
        let ids = idsCsv.split(separator: ",").map { MusicItemID(String($0)) }
        Task {
            do {
                var all: [Song] = []
                // In batches (the catalog takes a limited number of ids per request).
                var i = 0
                while i < ids.count {
                    let batch = Array(ids[i..<min(ids.count, i + 25)])
                    let request = MusicCatalogResourceRequest<Song>(matching: \.id, memberOf: batch)
                    let response = try await request.response()
                    all.append(contentsOf: response.items)
                    i += 25
                }
                self.finish(all)
            } catch { self.finish([], failed: "\(error)") }
        }
    }

    private var pickerDelegate: PickerDelegate?

    /// The system music picker over the game: the player picks songs from their own library.
    func pickFromLibrary() {
        guard begin() else { return }
        DispatchQueue.main.async {
            let picker = MPMediaPickerController(mediaTypes: .music)
            picker.allowsPickingMultipleItems = true
            picker.showsCloudItems = true
            picker.prompt = "Add songs to your Retro Hoops mix"
            let delegate = PickerDelegate { items in
                // Songs from Apple Music have a catalog id; songs only on this phone (not on Apple Music) can't play here.
                let ids = items.compactMap { item -> String? in
                    let id = item.playbackStoreID
                    return id.isEmpty || id == "0" ? nil : id
                }
                self.lock.lock(); self.requestStateValue = 0; self.lock.unlock()
                if ids.isEmpty { self.finish([]); return }
                self.lookup(ids.joined(separator: ","))
            }
            self.pickerDelegate = delegate
            picker.delegate = delegate
            guard let top = RetroMusicModel.topController() else { self.finish([], failed: "no window"); return }
            top.present(picker, animated: true)
        }
    }

    private static func topController() -> UIViewController? {
        let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
        let window = scenes.flatMap { $0.windows }.first { $0.isKeyWindow } ?? scenes.first?.windows.first
        var top = window?.rootViewController
        while let next = top?.presentedViewController { top = next }
        return top
    }

    /// Plays the songs with these catalog ids, in this order, from `start`.
    func play(_ idsCsv: String, start: Int) {
        lock.lock()
        let list = idsCsv.split(separator: ",").compactMap { songs[String($0)] }
        lock.unlock()
        guard !list.isEmpty else { return }
        let first = list[max(0, min(start, list.count - 1))]
        Task { @MainActor in
            let player = ApplicationMusicPlayer.shared
            player.queue = ApplicationMusicPlayer.Queue(for: list, startingAt: first)
            do { try await player.play() } catch { self.set(error: "\(error)") }
        }
    }

    func command(_ c: Int32) {
        Task { @MainActor in
            let player = ApplicationMusicPlayer.shared
            do {
                switch c {
                case 0: player.pause()
                case 1: try await player.play()
                case 2: try await player.skipToNextEntry()
                case 3: try await player.skipToPreviousEntry()
                default: player.stop()
                }
            } catch { self.set(error: "\(error)") }
        }
    }

    /// "id\tplaying(0/1)\tseconds" for the song playing now (empty id when nothing).
    @MainActor func nowPlaying() -> String {
        let player = ApplicationMusicPlayer.shared
        var id = ""
        if let entry = player.queue.currentEntry, let item = entry.item, case .song(let s) = item { id = s.id.rawValue }
        let playing = player.state.playbackStatus == .playing ? 1 : 0
        return id + "\t" + String(playing) + "\t" + String(format: "%.1f", player.playbackTime)
    }
}

private final class PickerDelegate: NSObject, MPMediaPickerControllerDelegate {
    private let done: ([MPMediaItem]) -> Void
    init(done: @escaping ([MPMediaItem]) -> Void) { self.done = done }
    func mediaPicker(_ mediaPicker: MPMediaPickerController, didPickMediaItems collection: MPMediaItemCollection) {
        mediaPicker.dismiss(animated: true)
        done(collection.items)
    }
    func mediaPickerDidCancel(_ mediaPicker: MPMediaPickerController) {
        mediaPicker.dismiss(animated: true)
        done([])
    }
}

private func retroCopy(_ s: String) -> UnsafeMutablePointer<CChar>? {
    return strdup(s) // freed by the C# marshaller
}

@_cdecl("RetroMusic_Refresh")
public func RetroMusic_Refresh() { RetroMusicModel.shared.refresh() }

@_cdecl("RetroMusic_RequestAccess")
public func RetroMusic_RequestAccess() { RetroMusicModel.shared.requestAccess() }

@_cdecl("RetroMusic_Auth")
public func RetroMusic_Auth() -> Int32 { return RetroMusicModel.shared.auth }

@_cdecl("RetroMusic_CanPlay")
public func RetroMusic_CanPlay() -> Int32 { return RetroMusicModel.shared.canPlay }

@_cdecl("RetroMusic_Resolve")
public func RetroMusic_Resolve(_ request: UnsafePointer<CChar>?) {
    guard let request = request else { return }
    RetroMusicModel.shared.resolve(String(cString: request))
}

@_cdecl("RetroMusic_ResolveState")
public func RetroMusic_ResolveState() -> Int32 { return RetroMusicModel.shared.resolveState }

@_cdecl("RetroMusic_Resolved")
public func RetroMusic_Resolved() -> UnsafeMutablePointer<CChar>? { return retroCopy(RetroMusicModel.shared.resolved) }

@_cdecl("RetroMusic_Error")
public func RetroMusic_Error() -> UnsafeMutablePointer<CChar>? { return retroCopy(RetroMusicModel.shared.error) }

@_cdecl("RetroMusic_Play")
public func RetroMusic_Play(_ ids: UnsafePointer<CChar>?, _ start: Int32) {
    guard let ids = ids else { return }
    RetroMusicModel.shared.play(String(cString: ids), start: Int(start))
}

/// 0 pause, 1 play, 2 next, 3 previous, 4 stop.
@_cdecl("RetroMusic_Command")
public func RetroMusic_Command(_ c: Int32) { RetroMusicModel.shared.command(c) }

@_cdecl("RetroMusic_NowPlaying")
public func RetroMusic_NowPlaying() -> UnsafeMutablePointer<CChar>? {
    // Unity calls in from the main thread.
    let s = MainActor.assumeIsolated { RetroMusicModel.shared.nowPlaying() }
    return retroCopy(s)
}

// 15 Oct 2026: MY MIX requests (see the model).
@_cdecl("RetroMusic_Search")
public func RetroMusic_Search(_ term: UnsafePointer<CChar>?) {
    guard let term = term else { return }
    RetroMusicModel.shared.search(String(cString: term))
}

@_cdecl("RetroMusic_Lookup")
public func RetroMusic_Lookup(_ ids: UnsafePointer<CChar>?) {
    guard let ids = ids else { return }
    RetroMusicModel.shared.lookup(String(cString: ids))
}

@_cdecl("RetroMusic_PickFromLibrary")
public func RetroMusic_PickFromLibrary() { RetroMusicModel.shared.pickFromLibrary() }

@_cdecl("RetroMusic_RequestState")
public func RetroMusic_RequestState() -> Int32 { return RetroMusicModel.shared.requestState }

@_cdecl("RetroMusic_RequestResult")
public func RetroMusic_RequestResult() -> UnsafeMutablePointer<CChar>? { return retroCopy(RetroMusicModel.shared.requestResult) }
