using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>One text from your agent: when it arrives, what it says, two replies you can send and what the agent says back.</summary>
    public sealed class AgentText
    {
        public int Chapter;
        /// <summary>Arrives after the chapter is cleared (else when it unlocks).</summary>
        public bool AfterClear;
        public string Text;
        public string[] Replies;
        public string[] FollowUps;
    }

    /// <summary>
    /// 14 Oct 2026 (Omari's request): SUMMER STORY on the phone. Dee Marlowe (an original character, your agent)
    /// texts you before each chapter and after you clear it. You can answer with one of two replies, and Dee
    /// answers back. Read and answered texts are saved in <see cref="PhoneData"/>.
    /// </summary>
    public static class AgentTexts
    {
        public const string Name = "DEE MARLOWE";
        public const string Role = "Your agent";
        public const string Color = "#06D6A0";

        public static readonly AgentText[] All =
        {
            T(1, false, "Saw a clip of you at the overpass. Name's Dee Marlowe. I rep hoopers. Win that 1-on-1 and we talk.",
              "Who sent you the clip?", "I'm listening.",
              "Doesn't matter. Half the city's got it now. Go win.", "Good. Listening wins games too. Go."),
            T(1, true, "That's what I'm talking about. The talker is Nova Quinn, by the way. Keep her close.",
              "She never stops talking.", "Is she good?",
              "Neither do I. That's how you know we care.", "Good enough to make you better. That's the best kind."),
            T(2, false, "The Pier Brothers pass like they share a brain. Cut hard and make them guess.",
              "Got it.", "What do I get if we win?",
              "Love it. Short answers, long highlights.", "A seat at Big Sal's table. That's worth more than money right now."),
            T(2, true, "Big Sal called me. BIG SAL. He wants a look at you. Tryout's this week.",
              "Let's go.", "What's Sal like?",
              "Wear something you can win in.", "Loud, fair, and he never forgets a win by four."),
            T(3, false, "Sal wants you to win by four. Not three. Four. He counts.",
              "Four it is.", "Why four?",
              "That's my client.", "One for each corner of his league. Don't ask him, just do it."),
            T(3, true, "You're in the Sunburst League. I already have two brands asking about you. We say no to both. For now.",
              "Why no?", "Whatever you think.",
              "Because next month they'll pay double.", "Trust looks good on you. Keep winning."),
            T(4, false, "Night Market tonight. The Lantern Kids switch everything. Move the ball: three assists.",
              "I can pass.", "Can't I just score?",
              "Prove it and the scouts write it down.", "You can. But tonight the passes are the story."),
            T(4, true, "The feed is going crazy about your passing. Check FEED on your phone. You're trending on the blacktop.",
              "That's wild.", "Who's posting?",
              "Get used to it.", "Courtside Cam mostly. Those kids are everywhere."),
            T(5, false, "Rain Check. Velvet Hour's second unit. Lock them up: three steals. Kojo is watching the tape.",
              "Let him watch.", "Kojo?",
              "That's the energy.", "Captain of the Velvet Hour. Best in the city. For now."),
            T(5, true, "Kojo texted ME. Asked for your number. I said he can talk to me first. Rooftop, two-on-two.",
              "Set it up.", "Should I be worried?",
              "Already done.", "Worried no. Ready yes."),
            T(6, false, "Rooftop tonight. It's Kojo and his guy Saint. Don't play their game, make them play yours.",
              "My game.", "What's their game?",
              "Exactly.", "Slow, physical, mind games. So go fast."),
            T(6, true, "You beat Kojo on his own roof. My phone hasn't stopped ringing. Stay humble, stay hungry.",
              "Humble and hungry.", "Who's calling?",
              "Put that on a shirt.", "Everybody. Mostly people I'll tell no."),
            T(7, false, "Sundown Yard, street rules. Mic Tally wants a highlight for the show. Break somebody's ankles.",
              "Say less.", "Is that nice?",
              "That's my client.", "It's street ball. They'll get up. They always do."),
            T(7, true, "Mic played your crossover nine times on air. NINE. The final is set: Sunburst Final, full court, Velvet Hour.",
              "We're ready.", "Full court?",
              "I know. Get some sleep.", "Both baskets, five on five. Run your legs into it this week."),
            T(8, false, "Sunburst Final. Whole city's watching. Whatever happens, I'm proud of where you started. Now go finish it.",
              "Let's finish it.", "Thanks, Dee.",
              "That's the one.", "Don't thank me yet. Thank me with a trophy."),
            T(8, true, "CHAMPIONS. Sunburst League champions. I'm not crying, it's raining. Check the FEED, you're everywhere.",
              "We did it.", "What's next?",
              "YOU did it. I just answered the phone.", "Next season. Bigger courts. And yes, now we say yes to the brands."),
        };

        private static AgentText T(int chapter, bool after, string text, string r1, string r2, string f1, string f2) =>
            new AgentText { Chapter = chapter, AfterClear = after, Text = text, Replies = new[] { r1, r2 }, FollowUps = new[] { f1, f2 } };

        /// <summary>The texts that have arrived: each unlocked chapter's "before" text, and each cleared chapter's "after".</summary>
        public static List<AgentText> Arrived(StorySaveData story)
        {
            int cleared = story?.cleared ?? 0;
            var list = new List<AgentText>();
            foreach (var t in All)
            {
                bool here = t.AfterClear ? t.Chapter <= cleared : t.Chapter <= Math.Min(StoryMode.Chapters, cleared + 1);
                if (here) list.Add(t);
            }
            return list;
        }

        public static int Unread(PhoneData phone, StorySaveData story) => Math.Max(0, Arrived(story).Count - (phone?.agentRead ?? 0));

        public static void MarkRead(PhoneData phone, StorySaveData story)
        {
            if (phone == null) return;
            phone.agentRead = Math.Max(phone.agentRead, Arrived(story).Count);
        }

        /// <summary>The reply picked for text <paramref name="index"/> (-1 = none yet).</summary>
        public static int ReplyOf(PhoneData phone, int index) =>
            phone?.agentReplies != null && index >= 0 && index < phone.agentReplies.Count ? phone.agentReplies[index] : -1;

        /// <summary>Answers text <paramref name="index"/> with reply 0 or 1 (once). True when it was saved.</summary>
        public static bool Reply(PhoneData phone, StorySaveData story, int index, int choice)
        {
            if (phone == null || choice < 0 || choice > 1 || index < 0 || index >= Arrived(story).Count) return false;
            if (phone.agentReplies == null) phone.agentReplies = new List<int>();
            while (phone.agentReplies.Count <= index) phone.agentReplies.Add(-1);
            if (phone.agentReplies[index] >= 0) return false;
            phone.agentReplies[index] = choice;
            return true;
        }
    }

    /// <summary>
    /// 14 Oct 2026: quick chat with the other player in LIVE and two-phone games. Preset messages only, so there's
    /// nothing to moderate and nothing goes through a server (it rides the game's own connection). Typed messages
    /// and a friends list are on the v1.1 list (they need a server, filtering, report and block).
    /// </summary>
    public static class QuickChat
    {
        public static readonly string[] Messages =
        {
            "GG!", "NICE SHOT!", "REMATCH?", "GOOD LUCK!", "WHAT A DUNK!", "TOO EASY", "MY BAD", "LET'S RUN IT BACK",
        };

        /// <summary>The message for <paramref name="id"/>, or null for an unknown id (from a newer version).</summary>
        public static string Text(int id) => id >= 0 && id < Messages.Length ? Messages[id] : null;

        /// <summary>Seconds between two messages from one player (no spamming).</summary>
        public const float Cooldown = 2.5f;
    }
}
