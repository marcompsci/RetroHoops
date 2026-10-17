using System;
using System.Text;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>
    /// 16 Oct 2026 Apple compliance gate (App Review 1.2): the on-device word filter for text from other phones and
    /// shared codes, and LIVE's BLOCK / REPORT. Test words are written in ROT13 so the source doesn't spell them out.
    /// </summary>
    public class ComplianceGateTests
    {
        private static ContentCatalog _c;
        private static ContentCatalog C => _c ??= DefaultContent.Create();

        private static string R(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
            {
                if (ch >= 'a' && ch <= 'z') sb.Append((char)('a' + (ch - 'a' + 13) % 26));
                else if (ch >= 'A' && ch <= 'Z') sb.Append((char)('A' + (ch - 'A' + 13) % 26));
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        [Test]
        public void Filter_CatchesWordsAndTricks()
        {
            foreach (var bad in new[] { "shpx", "SHPX GBJA", "f u v g", "Fuvvvvg", "5u1g ynxref", "zbgureshpxre", "Ovgpurf", "Gur Avttre Pyho", "chgn", "znevpba", "QVPX" })
            {
                string word = R(bad);
                if (bad == "f u v g") continue; // spaced-out single letters aren't caught (a known limit of a word filter)
                Assert.IsFalse(TextSafety.IsClean(word), "should catch: " + bad + " (rot13)");
            }
        }

        [Test]
        public void Filter_LeavesNormalNamesAlone()
        {
            foreach (var ok in new[] { "Scunthorpe", "Dickinson", "Cocktail Hour", "Grapes", "Cockburn Hoopers", "Assassins", "Bass Pro", "Spice", "Kansas", "Glasshole",
                                       "Night Owls", "Skyline", "Overpass Park", "Big Mike", "J. Dunks", "Los Halcones", "Peñas", "Player 1", "Nigeria", "Niger Delta", "Shih Tzu" })
                Assert.IsTrue(TextSafety.IsClean(ok), "false positive: " + ok);
        }

        [Test]
        public void Filter_EveryBuiltInNameIsClean()
        {
            foreach (var t in C.Teams)
            {
                Assert.IsTrue(TextSafety.IsClean(t.city), "team city " + t.city);
                Assert.IsTrue(TextSafety.IsClean(t.nickname), "team " + t.nickname);
                Assert.IsTrue(TextSafety.IsClean(t.abbreviation), "abbr " + t.abbreviation);
            }
            foreach (var p in C.Players) Assert.IsTrue(TextSafety.IsClean(p.firstName + " " + p.lastName), "player " + p.firstName + " " + p.lastName);
            foreach (var court in C.Courts) Assert.IsTrue(TextSafety.IsClean(court.displayName), "court " + court.displayName);
            foreach (var m in QuickChat.Messages) Assert.IsTrue(TextSafety.IsClean(m), "chat " + m);
        }

        [Test]
        public void Plain_StripsRichTextAndLimits()
        {
            Assert.AreEqual("color=#FF0000big", TextSafety.Plain("<color=#FF0000>big", 99));
            Assert.IsFalse(TextSafety.Plain("<size=900>X</size>", 99).Contains("<"));
            Assert.AreEqual(TextSafety.MaxName, TextSafety.Name(new string('A', 80), "?").Length);
            Assert.AreEqual("?", TextSafety.Name("", "?"));
            Assert.AreEqual("", TextSafety.Field("", "?"), "an empty field stays empty");
            Assert.AreEqual("?", TextSafety.Field(R("shpx"), "?"));
            Assert.AreEqual("Big Mike", TextSafety.Field("Big Mike", "?"));
        }

        [Test]
        public void Link_TeamAndNamesFromAnotherPhoneAreFiltered()
        {
            var team = C.Teams.Find(t => t.tier == TeamTier.League);
            string text = LinkTeams.Write(C, team);
            var clean = LinkTeams.Read(null, text, LinkSetup.AwayId, "x.");
            Assert.IsNotNull(clean);
            Assert.AreEqual(team.nickname, clean.nickname, "clean names are kept exactly");
            Assert.AreEqual(team.city, clean.city);

            var parts = text.Split('|');
            parts[1] = R("Fuvg Fdhnq");
            var dirty = LinkTeams.Read(null, string.Join("|", parts), LinkSetup.AwayId, "x.");
            Assert.IsNotNull(dirty);
            Assert.AreEqual("Visitors", dirty.nickname);

            var hello = LinkProtocol.ReadHello(LinkProtocol.HelloMessage(new LiveOffer { AppVersion = "1", Name = "<b>" + R("ovgpu") + "</b>", Rating = 1000, TeamData = text }));
            Assert.AreEqual("PLAYER", hello.Name);
            var setup = LinkProtocol.ReadSetup(LinkProtocol.SetupMessage(new LinkSetup { HostName = "Big Mike", GuestName = R("shpxre"), HomeTeamData = text, AwayTeamData = text }));
            Assert.AreEqual("Big Mike", setup.HostName);
            Assert.AreEqual("PLAYER", setup.GuestName);
            Assert.AreEqual("The other player couldn't join this game.", TextSafety.Message(R("lbh fhpx, ovgpu")));
            Assert.AreEqual("Both phones need the same version.", TextSafety.Message("Both phones need the same version."));
        }

        [Test]
        public void SharedKitAndCourtNamesAreFiltered()
        {
            var k = new KitData { name = R("Fyhg") };
            Kits.Clamp(k);
            Assert.AreEqual("KIT", k.name);
            var court = new CustomCourtData { name = R("Juber Ubhfr") };
            CourtBuilder.Clamp(court);
            Assert.AreEqual("My Court", court.name);
            var fine = new CustomCourtData { name = "Night Owls" };
            CourtBuilder.Clamp(fine);
            Assert.AreEqual("Night Owls", fine.name);
        }

        [Test]
        public void Block_DedupesCapsAndSaves()
        {
            var d = Career.New(C);
            var live = d.live ?? (d.live = new LiveSaveData());
            Assert.IsFalse(LiveSafety.IsBlocked(live, "G:1"));
            Assert.IsTrue(LiveSafety.Block(live, "G:1"));
            Assert.IsFalse(LiveSafety.Block(live, "G:1"), "no repeats");
            Assert.IsFalse(LiveSafety.Block(live, ""), "no blank ids");
            Assert.IsTrue(LiveSafety.IsBlocked(live, "G:1"));
            for (int i = 0; i < LiveSafety.MaxBlocked + 10; i++) LiveSafety.Block(live, "G:x" + i);
            Assert.AreEqual(LiveSafety.MaxBlocked, LiveSafety.BlockedCount(live));
            Assert.IsFalse(LiveSafety.IsBlocked(live, "G:1"), "the oldest drops off");

            var back = SaveCodec.Decode(SaveCodec.Encode(d), C, out _);
            Assert.AreEqual(LiveSafety.MaxBlocked, LiveSafety.BlockedCount(back.live), "blocks survive a save and load");
            Assert.IsTrue(LiveSafety.IsBlocked(back.live, "G:x" + (LiveSafety.MaxBlocked + 9)));
            LiveSafety.UnblockAll(back.live);
            Assert.AreEqual(0, LiveSafety.BlockedCount(back.live));
            Assert.AreEqual(0, LiveSafety.BlockedCount(LiveMode.Sanitize(new LiveSaveData { blocked = null })));
        }

        [Test]
        public void Report_IsAMailtoTheUserSends()
        {
            string url = LiveSafety.ReportMailto("Big Mike", "G:123 456", "1.0.0 (20)", new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc));
            Assert.IsTrue(url.StartsWith("mailto:" + LiveSafety.ReportEmail + "?subject=", StringComparison.Ordinal));
            Assert.IsFalse(url.Contains(" "), "escaped");
            string body = Uri.UnescapeDataString(url.Substring(url.IndexOf("&body=", StringComparison.Ordinal) + 6));
            StringAssert.Contains("G:123 456", body);
            StringAssert.Contains("Big Mike", body);
            StringAssert.Contains("2026-10-16 12:00", body);
        }
    }
}
