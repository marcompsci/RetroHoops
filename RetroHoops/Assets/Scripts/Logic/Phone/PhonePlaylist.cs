using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>One song on the phone's playlist, with its credits as Apple Music lists them.</summary>
    public sealed class PlaylistTrack
    {
        public string Key, Title, Artist, Featuring;

        /// <summary>"Free Joe Exotic (feat. Sada Baby)"</summary>
        public string DisplayTitle => string.IsNullOrEmpty(Featuring) ? Title : Title + " (feat. " + Featuring + ")";

        /// <summary>"BfB Da Packman · feat. Sada Baby" style credit line.</summary>
        public string Credits => string.IsNullOrEmpty(Featuring) ? Artist : Artist + " · feat. " + Featuring;
    }

    /// <summary>
    /// 14 Oct 2026 (Omari's request): the phone's MUSIC app plays a playlist in the background through
    /// Apple Music.
    /// - The song list comes from Omari's screenshots.
    /// - The clean version when Apple Music has one; otherwise the explicit one, which plays only with EXPLICIT SONGS on
    ///   (off by default; iPhone Screen Time limits still apply).
    /// - The playlist plays on shuffle. Every time it loops, it's dealt in a new order, and the first song is
    ///   never the one that just played.
    /// - Every song shows its credits and a + that opens it in Apple Music to add to your library.
    /// - Playback needs an Apple Music subscription. Without one, the game's own soundtrack keeps playing.
    /// The game doesn't include or stream any of these recordings itself.
    /// </summary>
    public static class PhonePlaylist
    {
        public static readonly PlaylistTrack[] Tracks =
        {
            new PlaylistTrack { Key = "free_joe_exotic", Title = "Free Joe Exotic", Artist = "BfB Da Packman", Featuring = "Sada Baby" },
            new PlaylistTrack { Key = "proud_of_me", Title = "Proud of Me", Artist = "Ysr Gramz" },
            new PlaylistTrack { Key = "law_n_order", Title = "Law N Order", Artist = "Veeze" },
            new PlaylistTrack { Key = "treehouse", Title = "Treehouse", Artist = "3200 Tre" },
            new PlaylistTrack { Key = "7sixers", Title = "7sixers", Artist = "Veeze", Featuring = "Babyface Ray" },
            new PlaylistTrack { Key = "lmaoooooo", Title = "Lmaoooooo", Artist = "Fox BD" },
            new PlaylistTrack { Key = "shakey_butts", Title = "Shakey Butts", Artist = "3200 Tre" },
        };

        public static PlaylistTrack Find(string key) => Array.Find(Tracks, t => t.Key == key);

        /// <summary>What the phone asks Apple Music for: "key\ttitle\tartist" lines.</summary>
        public static string ResolveRequest()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var t in Tracks) sb.Append(t.Key).Append('\t').Append(t.Title).Append('\t').Append(t.Artist).Append('\n');
            return sb.ToString();
        }

        /// <summary>The + when the phone didn't get a direct link: an Apple Music search for the song.</summary>
        public static string SearchUrl(PlaylistTrack t) =>
            "https://music.apple.com/us/search?term=" + Uri.EscapeDataString(t.Title + " " + t.Artist);

        /// <summary>
        /// The play order for loop number <paramref name="cycle"/> of <paramref name="count"/> songs: a shuffle that
        /// differs every loop. When <paramref name="previousLast"/> is a song index, the loop doesn't start with it
        /// (no song twice in a row across the reset).
        /// </summary>
        public static int[] ShuffleOrder(int count, uint seed, int cycle, int previousLast = -1)
        {
            var order = new int[Math.Max(0, count)];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            if (order.Length < 2) return order;
            var rng = new SeededRandom(StableHash.Next(unchecked(seed ^ ((uint)cycle * 2654435761u + 17u))));
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            if (order[0] == previousLast)
            {
                int swap = 1 + rng.Range(0, order.Length - 1);
                (order[0], order[swap]) = (order[swap], order[0]);
            }
            return order;
        }
    }

    /// <summary>A song in the player's own MY MIX (an Apple Music catalog song, with its credits).</summary>
    [Serializable]
    public class MixSong
    {
        public string id = "", title = "", artist = "", album = "", url = "";
        public bool @explicit;
    }

    /// <summary>
    /// 15 Oct 2026 (Omari): each player builds their own background music. MY MIX holds songs they add:
    /// - from their own library (the system music picker);
    /// - from an Apple Music search inside the game;
    /// - or from a link shared out of Apple Music (Share ▸ Copy Link, then PASTE LINK in the game).
    /// It plays on shuffle like the Retro Hoops playlist. Songs play through Apple Music and need a subscription.
    /// </summary>
    public static class MyMix
    {
        public const int MaxSongs = 150;
        public const string KeyPrefix = "mix:";

        public static string Key(string id) => KeyPrefix + id;

        /// <summary>Adds a song (no duplicates, up to <see cref="MaxSongs"/>). True when it was added.</summary>
        public static bool Add(PhoneData phone, MixSong song)
        {
            if (phone == null || song == null || !IsCatalogId(song.id)) return false;
            if (phone.myMix == null) phone.myMix = new List<MixSong>();
            if (phone.myMix.Count >= MaxSongs || phone.myMix.Exists(s => s.id == song.id)) return false;
            phone.myMix.Add(song);
            return true;
        }

        public static bool Remove(PhoneData phone, string id) =>
            phone?.myMix != null && phone.myMix.RemoveAll(s => s.id == id) > 0;

        /// <summary>Apple Music catalog song ids are digits.</summary>
        public static bool IsCatalogId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 20) return false;
            foreach (char c in id) if (c < '0' || c > '9') return false;
            return true;
        }

        /// <summary>
        /// The song id in an Apple Music link, or null. It handles the ways Apple Music shares a song:
        /// - https://music.apple.com/us/album/name/123?i=456 (the song is the i= part);
        /// - https://music.apple.com/us/song/name/456;
        /// - the same with geo.music.apple.com or itunes.apple.com.
        /// Text around the link (like "Listen on Apple Music: ...") is fine.
        /// </summary>
        public static string SongIdFromLink(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int at = text.IndexOf("music.apple.com/", StringComparison.OrdinalIgnoreCase);
            if (at < 0) at = text.IndexOf("itunes.apple.com/", StringComparison.OrdinalIgnoreCase);
            if (at < 0) return null;
            int end = at;
            while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '"' && text[end] != '>') end++;
            string url = text.Substring(at, end - at);
            int q = url.IndexOf('?');
            string path = q >= 0 ? url.Substring(0, q) : url;
            if (q >= 0)
            {
                foreach (var part in url.Substring(q + 1).Split('&'))
                    if (part.StartsWith("i=", StringComparison.Ordinal) && IsCatalogId(part.Substring(2))) return part.Substring(2);
            }
            var segs = path.TrimEnd('/').Split('/');
            for (int k = 0; k < segs.Length - 1; k++)
                if (segs[k] == "song")
                {
                    string last = segs[segs.Length - 1];
                    if (last.StartsWith("id", StringComparison.Ordinal)) last = last.Substring(2);
                    return IsCatalogId(last) ? last : null;
                }
            return null;
        }
    }

    /// <summary>The phone's saved state: the playlist position, agent texts read and answered, the feed.</summary>
    [Serializable]
    public class PhoneData
    {
        /// <summary>Play the Apple Music playlist (when allowed and subscribed).</summary>
        public bool musicOn = true;
        /// <summary>
        /// Play explicit versions when Apple Music has no clean one. Off by default (15 Oct 2026, Omari: keeps the
        /// age rating); the player turns it on with EXPLICIT SONGS, and iPhone Screen Time limits still apply.
        /// </summary>
        public bool explicitSongs;
        /// <summary>15 Oct 2026: which music plays: 0 = the Retro Hoops playlist, 1 = MY MIX (the player's own songs).</summary>
        public int musicSource;
        public List<MixSong> myMix = new List<MixSong>();
        /// <summary>Per-install shuffle seed (0 = not picked yet).</summary>
        public uint shuffleSeed;
        /// <summary>Which loop of the playlist this is (each loop is a new shuffle).</summary>
        public int cycle;
        /// <summary>Agent texts already seen, and the reply picked to each (index per text, -1 = not answered).</summary>
        public int agentRead;
        public List<int> agentReplies = new List<int>();
        /// <summary>FEED posts, newest first (capped at <see cref="SocialFeed.MaxPosts"/>).</summary>
        public List<FeedPost> feed = new List<FeedPost>();
        /// <summary>Posts added since the FEED was last opened.</summary>
        public int feedNew;
        /// <summary>Quick chat in LIVE / two-phone games (others' messages show; yours can be sent).</summary>
        public bool quickChat = true;
    }
}
