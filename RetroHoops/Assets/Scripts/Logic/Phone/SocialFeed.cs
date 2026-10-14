using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>One post on the phone's FEED (made up, inside the game).</summary>
    [Serializable]
    public class FeedPost
    {
        public string id = "";
        public int day;
        /// <summary>Display name and handle of the made-up account.</summary>
        public string account = "", handle = "";
        public string text = "";
        /// <summary>PLAYOFFS, FINAL, PLAY OF THE GAME, or empty.</summary>
        public string tag = "";
        public int likes, reposts;
        public List<string> comments = new List<string>();
        /// <summary>The play-of-the-game photo saved with the post (file name in the phone's feed folder), or empty.</summary>
        public string photo = "";
        /// <summary>Accent colour of the account's avatar.</summary>
        public string color = "#4CC9F0";
    }

    /// <summary>What the feed knows about the game just played (besides the box score).</summary>
    public struct FeedContext
    {
        /// <summary>The best play's label ("SLAM!", "ALLEY-OOP!", "SWISH"...), or null.</summary>
        public string BestPlay;
        /// <summary>A play-of-the-game photo was taken (its file name), or null.</summary>
        public string Photo;
        /// <summary>The player's fans (bigger following, more likes).</summary>
        public int Fans;
        /// <summary>Summer Story chapter just played (0 = not story).</summary>
        public int StoryChapter;
        /// <summary>The game was won and it ended a season, cup or story (CHAMPIONS).</summary>
        public bool Title;
    }

    /// <summary>
    /// 14 Oct 2026 (Omari's request): a made-up social feed on the phone. After your games, original accounts in
    /// the Retro Hoops world post about them:
    /// - the final score;
    /// - the player of the game's line;
    /// - playoff games and titles;
    /// - a PLAY OF THE GAME photo of your best play.
    /// Likes grow with your fans. It's all fiction, stays on the phone, and isn't a real social network.
    /// </summary>
    public static class SocialFeed
    {
        public const int MaxPosts = 40;

        public struct Account
        {
            public string Name, Handle, Color;
            public Account(string name, string handle, string color) { Name = name; Handle = handle; Color = color; }
        }

        public static readonly Account CourtsideCam = new Account("Courtside Cam", "@courtside.cam", "#F72585");
        public static readonly Account BlacktopDaily = new Account("Blacktop Daily", "@blacktopdaily", "#FFD166");
        public static readonly Account HoopHeads = new Account("Hoop Heads", "@hoopheads", "#4CC9F0");
        public static readonly Account MicTally = new Account("Mic Tally", "@mictally", "#4CC9F0");
        public static readonly Account Bracket = new Account("Bracket Watch", "@bracketwatch", "#06D6A0");

        private static readonly string[] FanComments =
        {
            "THIS IS CRAZY", "best player in the city fr", "run it back!!", "I was there!!", "certified bucket",
            "the crowd went silent", "somebody check on the defender", "clip of the year", "they can't guard that",
            "need this on repeat", "too smooth", "who's stopping this?", "MVP MVP MVP", "that pass tho",
        };
        private static readonly string[] FanNames = { "@buckets4days", "@rimrattler", "@blacktopqueen", "@nothingbutnet", "@sixthman", "@pixelhooper", "@courtvision", "@ballislife22" };

        /// <summary>Posts for a finished game, newest first. Nothing for practice, demos or games that don't count.</summary>
        public static List<FeedPost> PostsFor(MatchSummary s, FeedContext ctx)
        {
            var list = new List<FeedPost>();
            if (s == null || s.mode == GameMode.Practice || s.mode == GameMode.Demo || s.mode == GameMode.Tutorial) return list;
            uint seed = StableHash.Of(s.matchId + ":feed");
            var rng = new SeededRandom(seed);
            int reach = 40 + Math.Min(5000, Math.Max(0, ctx.Fans)) / 3;
            string a = Cap(s.teamAName), b = Cap(s.teamBName);
            string score = a + " " + s.scoreA + ", " + b + " " + s.scoreB;
            var winnerName = s.winner == 0 ? a : s.winner == 1 ? b : null;
            var potg = s.playerOfTheGame >= 0 ? s.lines.Find(l => l.playerIndex == s.playerOfTheGame) : null;

            // The final score, with the playoffs or a title when it was one.
            string tag = ctx.Title && s.HumanWon ? "CHAMPIONS" : s.isFinal ? "FINAL" : s.isPlayoff ? "PLAYOFFS" : "";
            string resultText;
            if (ctx.Title && s.HumanWon) resultText = "CHAMPIONS! " + winnerName + " take the title. " + score + ".";
            else if (s.isPlayoff && winnerName != null)
                resultText = (s.isFinal ? "Championship game: " : "Playoffs: ") + winnerName + (s.isFinal ? " win it all. " : " move on. ") + score + ".";
            else if (winnerName == null) resultText = "All square. " + score + ".";
            else
            {
                int margin = Math.Abs(s.scoreA - s.scoreB);
                resultText = margin <= 2 ? "Down to the wire! " + winnerName + " survive, " + score + "."
                           : margin >= 8 ? winnerName + " cruise. " + score + "."
                           : "Final: " + score + ".";
            }
            var feedAccount = s.isPlayoff || ctx.Title ? Bracket : ctx.StoryChapter > 0 ? MicTally : BlacktopDaily;
            list.Add(Post(s, "result", feedAccount, resultText, tag, reach, rng));

            // The player of the game.
            if (potg != null && potg.stats != null)
            {
                var st = potg.stats;
                string line = Cap(potg.name) + " tonight: " + st.points + " PTS";
                if (st.assists > 0) line += ", " + st.assists + " AST";
                if (st.rebounds > 0) line += ", " + st.rebounds + " REB";
                if (st.steals > 1) line += ", " + st.steals + " STL";
                if (st.blocks > 1) line += ", " + st.blocks + " BLK";
                line += potg.isHuman ? ". Somebody's on the rise." : ". Respect.";
                list.Add(Post(s, "potg-line", HoopHeads, line, "", reach * 2 / 3, rng));
            }

            // The play of the game, with the photo.
            if (!string.IsNullOrEmpty(ctx.BestPlay))
            {
                string who = s.HumanLine != null ? Cap(s.HumanLine.name) : "This one";
                string caption = PlayCaption(ctx.BestPlay, who, rng);
                var photoPost = Post(s, "photo", CourtsideCam, caption, "PLAY OF THE GAME", reach * 3 / 2, rng);
                photoPost.photo = ctx.Photo ?? "";
                list.Add(photoPost);
            }
            list.Reverse(); // newest first: the photo, then the line, then the score
            return list;
        }

        private static string PlayCaption(string play, string who, SeededRandom rng)
        {
            string p = (play ?? "").TrimEnd('!');
            string[] options =
            {
                "PLAY OF THE GAME: " + who + " with the " + p.ToLowerInvariant() + ". Look at the crowd.",
                who + ". " + p + ". That's it, that's the post.",
                "We were RIGHT there for this " + p.ToLowerInvariant() + " from " + who + ".",
            };
            return options[rng.Range(0, options.Length)];
        }

        private static FeedPost Post(MatchSummary s, string kind, Account acc, string text, string tag, int reach, SeededRandom rng)
        {
            var p = new FeedPost
            {
                id = s.matchId + ":" + kind,
                day = s.day,
                account = acc.Name,
                handle = acc.Handle,
                color = acc.Color,
                text = text,
                tag = tag ?? "",
                likes = reach + rng.Range(0, Math.Max(1, reach)),
                reposts = (reach + rng.Range(0, Math.Max(1, reach))) / 8,
            };
            int comments = 1 + rng.Range(0, 2);
            for (int i = 0; i < comments; i++)
                p.comments.Add(FanNames[rng.Range(0, FanNames.Length)] + "  " + FanComments[rng.Range(0, FanComments.Length)]);
            return p;
        }

        /// <summary>Adds posts to the top of the feed (no duplicates) and keeps the newest <see cref="MaxPosts"/>.
        /// Returns the photo files of posts that fell off, so the caller can delete them.</summary>
        public static List<string> Add(PhoneData phone, List<FeedPost> posts)
        {
            var dropped = new List<string>();
            if (phone == null || posts == null) return dropped;
            if (phone.feed == null) phone.feed = new List<FeedPost>();
            for (int i = posts.Count - 1; i >= 0; i--)
            {
                if (phone.feed.Exists(p => p.id == posts[i].id)) continue;
                phone.feed.Insert(0, posts[i]);
                phone.feedNew++;
            }
            while (phone.feed.Count > MaxPosts)
            {
                var old = phone.feed[phone.feed.Count - 1];
                if (!string.IsNullOrEmpty(old.photo)) dropped.Add(old.photo);
                phone.feed.RemoveAt(phone.feed.Count - 1);
            }
            return dropped;
        }

        /// <summary>New posts since the FEED was last opened.</summary>
        public static int Unseen(PhoneData phone) => phone?.feed == null ? 0 : Math.Max(0, Math.Min(phone.feed.Count, phone.feedNew));

        public static void MarkSeen(PhoneData phone)
        {
            if (phone != null) phone.feedNew = 0;
        }

        private static string Cap(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var words = s.ToLowerInvariant().Split(' ');
            for (int i = 0; i < words.Length; i++)
                if (words[i].Length > 0) words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            return string.Join(" ", words);
        }
    }
}
