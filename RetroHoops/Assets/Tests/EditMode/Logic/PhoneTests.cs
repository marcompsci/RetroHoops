using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>14 Oct 2026: the in-game phone (playlist shuffle, agent texts, quick chat, the FEED).</summary>
    public class PhoneTests
    {
        [Test]
        public void Playlist_HasTheSevenSongs_WithCredits()
        {
            Assert.AreEqual(7, PhonePlaylist.Tracks.Length);
            var keys = new HashSet<string>();
            foreach (var t in PhonePlaylist.Tracks)
            {
                Assert.IsTrue(keys.Add(t.Key), "unique keys");
                Assert.IsNotEmpty(t.Title);
                Assert.IsNotEmpty(t.Artist);
            }
            var joe = PhonePlaylist.Find("free_joe_exotic");
            Assert.AreEqual("Free Joe Exotic (feat. Sada Baby)", joe.DisplayTitle);
            StringAssert.Contains("Sada Baby", joe.Credits);
            StringAssert.Contains("Babyface Ray", PhonePlaylist.Find("7sixers").DisplayTitle);
            Assert.IsTrue((PhonePlaylist.SearchUrl(joe)).StartsWith("https://music.apple.com/"));
            StringAssert.Contains("Free%20Joe%20Exotic", PhonePlaylist.SearchUrl(joe));
            var req = PhonePlaylist.ResolveRequest().Split('\n');
            Assert.AreEqual(8, req.Length, "a line per song plus the trailing newline");
            StringAssert.Contains("\t3200 Tre", req[3]);
        }

        [Test]
        public void Shuffle_IsAFullOrder_DifferentEveryLoop_AndNeverRepeatsAcrossTheReset()
        {
            int n = 7;
            uint seed = 12345;
            var seen = new HashSet<string>();
            int last = -1;
            for (int cycle = 0; cycle < 30; cycle++)
            {
                var order = PhonePlaylist.ShuffleOrder(n, seed, cycle, last);
                Assert.AreEqual(n, order.Length);
                var set = new HashSet<int>(order);
                Assert.AreEqual(n, set.Count, "every song once per loop");
                Assert.AreNotEqual(last, order[0], "the loop doesn't start with the song that just played");
                seen.Add(string.Join(",", order));
                Assert.AreEqual(string.Join(",", order), string.Join(",", PhonePlaylist.ShuffleOrder(n, seed, cycle, last)), "same seed and loop, same order");
                last = order[n - 1];
            }
            Assert.Greater(seen.Count, 25, "the order changes from loop to loop");
            Assert.AreEqual(0, PhonePlaylist.ShuffleOrder(0, seed, 0).Length);
            Assert.AreEqual(1, PhonePlaylist.ShuffleOrder(1, seed, 3, 0).Length);
        }

        [Test]
        public void AgentTexts_ArriveWithTheStory_AndRepliesSaveOnce()
        {
            var phone = new PhoneData();
            var story = new StorySaveData();
            var arrived = AgentTexts.Arrived(story);
            Assert.AreEqual(1, arrived.Count, "chapter 1's text is waiting from the start");
            Assert.AreEqual(1, AgentTexts.Unread(phone, story));
            AgentTexts.MarkRead(phone, story);
            Assert.AreEqual(0, AgentTexts.Unread(phone, story));

            story.cleared = 1;
            Assert.AreEqual(3, AgentTexts.Arrived(story).Count, "after clearing 1: its 'after' text and chapter 2's 'before'");
            Assert.AreEqual(2, AgentTexts.Unread(phone, story));

            Assert.IsTrue(AgentTexts.Reply(phone, story, 1, 0));
            Assert.IsFalse(AgentTexts.Reply(phone, story, 1, 1), "one reply per text");
            Assert.AreEqual(0, AgentTexts.ReplyOf(phone, 1));
            Assert.AreEqual(-1, AgentTexts.ReplyOf(phone, 0));
            Assert.IsFalse(AgentTexts.Reply(phone, story, 5, 0), "not arrived yet");

            story.cleared = StoryMode.Chapters;
            Assert.AreEqual(AgentTexts.All.Length, AgentTexts.Arrived(story).Count, "all of them by the end");
            foreach (var t in AgentTexts.All)
            {
                Assert.AreEqual(2, t.Replies.Length);
                Assert.AreEqual(2, t.FollowUps.Length);
            }
        }

        [Test]
        public void QuickChat_RoundTripsOverTheLink_AndUnknownIdsAreIgnored()
        {
            var m = LinkProtocol.Chat(2);
            Assert.AreEqual((byte)LinkMessage.Chat, m[0]);
            var ls = new Lockstep(0);
            ls.Receive(m);
            Assert.AreEqual(1, ls.Chats.Count);
            Assert.AreEqual("REMATCH?", QuickChat.Text(ls.Chats.Dequeue()));
            Assert.IsNull(QuickChat.Text(200));
            Assert.IsNull(QuickChat.Text(-1));
        }

        private static MatchSummary Summary(bool playoff, bool final, int a, int b)
        {
            var s = new MatchSummary
            {
                matchId = "m1", day = 100, mode = GameMode.Tournament, teamAName = "METRO COMETS", teamBName = "HARBOR HOUNDS",
                scoreA = a, scoreB = b, humanTeam = 0, winner = a > b ? 0 : a < b ? 1 : -1, isPlayoff = playoff, isFinal = final, playerOfTheGame = 0,
            };
            s.lines.Add(new SummaryLine { playerIndex = 0, team = 0, name = "ROOK", isHuman = true, stats = new PlayerStatLine { points = 14, assists = 5, rebounds = 3 } });
            return s;
        }

        [Test]
        public void Feed_PostsTheScore_ThePlayerOfTheGame_AndThePhoto()
        {
            var posts = SocialFeed.PostsFor(Summary(false, false, 21, 17), new FeedContext { BestPlay = "SLAM!", Photo = "potg-1.png", Fans = 200 });
            Assert.AreEqual(3, posts.Count);
            Assert.AreEqual("PLAY OF THE GAME", posts[0].tag, "newest first: the photo");
            Assert.AreEqual("potg-1.png", posts[0].photo);
            StringAssert.Contains("Rook", posts[1].text);
            StringAssert.Contains("14 PTS", posts[1].text);
            StringAssert.Contains("Metro Comets 21", posts[2].text);
            foreach (var p in posts)
            {
                Assert.Greater(p.likes, 0);
                Assert.GreaterOrEqual(p.comments.Count, 1);
                Assert.IsTrue((p.handle).StartsWith("@"));
            }

            var playoff = SocialFeed.PostsFor(Summary(true, false, 21, 19), new FeedContext());
            Assert.AreEqual("PLAYOFFS", playoff[playoff.Count - 1].tag);
            var champs = SocialFeed.PostsFor(Summary(true, true, 21, 19), new FeedContext { Title = true });
            Assert.AreEqual("CHAMPIONS", champs[champs.Count - 1].tag);
            StringAssert.Contains("CHAMPIONS", champs[champs.Count - 1].text);

            var practice = Summary(false, false, 5, 0);
            practice.mode = GameMode.Practice;
            Assert.AreEqual(0, SocialFeed.PostsFor(practice, new FeedContext()).Count);
        }

        [Test]
        public void Feed_KeepsTheNewest_CountsUnseen_AndReturnsDroppedPhotos()
        {
            var phone = new PhoneData();
            var dropped = new List<string>();
            for (int i = 0; i < 30; i++)
            {
                var s = Summary(false, false, 21, 10 + i % 5);
                s.matchId = "g" + i;
                dropped.AddRange(SocialFeed.Add(phone, SocialFeed.PostsFor(s, new FeedContext { BestPlay = "SWISH", Photo = "p" + i + ".png" })));
            }
            Assert.AreEqual(SocialFeed.MaxPosts, phone.feed.Count);
            Assert.AreEqual("p29.png", phone.feed[0].photo, "newest on top");
            Assert.Greater(dropped.Count, 0, "old photos handed back to delete");
            Assert.AreEqual(SocialFeed.MaxPosts, SocialFeed.Unseen(phone));
            SocialFeed.MarkSeen(phone);
            Assert.AreEqual(0, SocialFeed.Unseen(phone));
            // The same game twice doesn't post twice.
            var again = Summary(false, false, 21, 10);
            again.matchId = "g29";
            SocialFeed.Add(phone, SocialFeed.PostsFor(again, new FeedContext { BestPlay = "SWISH", Photo = "p29.png" }));
            Assert.AreEqual(0, SocialFeed.Unseen(phone));
        }

        [Test]
        public void Phone_SavesAndLoads_AndRejectsPathsInPhotoNames()
        {
            var c = DefaultContent.Create();
            var d = new CareerSaveData();
            d.phone.musicOn = false;
            d.phone.shuffleSeed = 4000000000u;
            d.phone.cycle = 3;
            d.phone.agentRead = 2;
            d.phone.agentReplies = new List<int> { 1, -1, 0 };
            d.phone.quickChat = false;
            SocialFeed.Add(d.phone, SocialFeed.PostsFor(Summary(false, false, 21, 17), new FeedContext { BestPlay = "SLAM!", Photo = "potg-abc.png" }));
            d.phone.feed[1].photo = "../../secret.png";
            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _);
            Assert.IsFalse(back.phone.musicOn);
            Assert.AreEqual(4000000000u, back.phone.shuffleSeed);
            Assert.AreEqual(3, back.phone.cycle);
            Assert.AreEqual(2, back.phone.agentRead);
            Assert.AreEqual("1,-1,0", string.Join(",", back.phone.agentReplies));
            Assert.IsFalse(back.phone.quickChat);
            Assert.AreEqual(3, back.phone.feed.Count);
            Assert.AreEqual("potg-abc.png", back.phone.feed[0].photo);
            Assert.AreEqual("", back.phone.feed[1].photo, "no folders in a photo name");
            Assert.AreEqual(d.phone.feed[2].text, back.phone.feed[2].text);
            Assert.AreEqual(3, back.phone.feedNew);

            // An old save without a phone loads with the defaults.
            var fresh = SaveCodec.Decode(SaveCodec.Encode(new CareerSaveData()), c, out _);
            Assert.IsTrue(fresh.phone.musicOn);
            Assert.AreEqual(0, fresh.phone.feed.Count);
        }
    }
}
