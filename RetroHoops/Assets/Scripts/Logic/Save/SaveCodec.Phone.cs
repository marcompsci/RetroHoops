using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public static partial class SaveCodec
    {
        // 14 Oct 2026: the in-game phone (playlist position, agent texts, FEED posts).
        private static object EncodePhone(PhoneData p)
        {
            p = p ?? new PhoneData();
            var feed = new List<object>();
            foreach (var f in p.feed ?? new List<FeedPost>())
                feed.Add(new Dictionary<string, object>
                {
                    ["id"] = f.id ?? "", ["day"] = f.day, ["acc"] = f.account ?? "", ["h"] = f.handle ?? "", ["t"] = f.text ?? "",
                    ["tag"] = f.tag ?? "", ["likes"] = f.likes, ["rp"] = f.reposts, ["c"] = Strings(f.comments ?? new List<string>()),
                    ["photo"] = f.photo ?? "", ["col"] = f.color ?? "",
                });
            return new Dictionary<string, object>
            {
                ["music"] = p.musicOn, ["seed"] = (double)p.shuffleSeed, ["cycle"] = p.cycle,
                ["agentRead"] = p.agentRead, ["agentReplies"] = Ints(p.agentReplies ?? new List<int>()),
                ["feed"] = feed, ["feedNew"] = p.feedNew, ["chat"] = p.quickChat,
            };
        }

        private static PhoneData DecodePhone(Dictionary<string, object> o)
        {
            var p = new PhoneData();
            if (o == null) return p;
            p.musicOn = Bool(o, "music", true);
            double seed = Num(o, "seed", 0);
            p.shuffleSeed = seed > 0 && seed <= uint.MaxValue ? (uint)seed : 0u;
            p.cycle = Math.Max(0, Int(o, "cycle", 0));
            p.agentRead = Math.Max(0, Math.Min(AgentTexts.All.Length, Int(o, "agentRead", 0)));
            p.agentReplies = new List<int>();
            foreach (var v in Arr(o, "agentReplies"))
            {
                if (p.agentReplies.Count >= AgentTexts.All.Length) break;
                int r = v is double d ? (int)d : v is long l ? (int)l : v is int i ? i : -1;
                p.agentReplies.Add(r < -1 || r > 1 ? -1 : r);
            }
            p.feed = new List<FeedPost>();
            foreach (var v in Arr(o, "feed"))
            {
                if (!(v is Dictionary<string, object> f) || p.feed.Count >= SocialFeed.MaxPosts) continue;
                var post = new FeedPost
                {
                    id = Str(f, "id", "") ?? "", day = Int(f, "day", 0), account = Str(f, "acc", "") ?? "", handle = Str(f, "h", "") ?? "",
                    text = Str(f, "t", "") ?? "", tag = Str(f, "tag", "") ?? "", likes = Math.Max(0, Int(f, "likes", 0)),
                    reposts = Math.Max(0, Int(f, "rp", 0)), comments = StrList(f, "c"), photo = SafePhoto(Str(f, "photo", "") ?? ""),
                    color = Str(f, "col", "#4CC9F0") ?? "#4CC9F0",
                };
                p.feed.Add(post);
            }
            p.feedNew = Math.Max(0, Math.Min(p.feed.Count, Int(o, "feedNew", 0)));
            p.quickChat = Bool(o, "chat", true);
            return p;
        }

        /// <summary>A photo name is a plain file name (no folders), so an edited save can't point anywhere else.</summary>
        private static string SafePhoto(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 80) return "";
            foreach (char ch in name)
                if (!(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.')) return "";
            return name.Contains("..") ? "" : name;
        }
    }
}
