using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 30: watching a two-phone game on a third phone, and the Couch Cup over two phones.</summary>
    public class Phase30WatchTests
    {
        private const float Dt = 1f / 60f;

        private static LinkSetup Setup(ContentCatalog c, uint seed = 777)
        {
            var league = c.TeamsInTier(TeamTier.League);
            return LinkSetup.From(c, league[1].id, league[4].id, league[1].homeCourtId, DefaultContent.DefaultDifficultyId, seed, "1.0.0", "Host");
        }

        private static MatchSimulation MatchFor(LinkSetup s, out ContentCatalog c)
        {
            c = DefaultContent.Create();
            Assert.IsTrue(s.Register(c));
            return new MatchSimulation(MatchSetup.FromRequest(s.ToRequest(), c));
        }

        private static PlayerInput RandomInput(SeededRandom r) => new PlayerInput
        {
            Move = new Vec2(r.Range(-100, 101) / 100f, r.Range(-100, 101) / 100f),
            ShootPressed = r.Range(0, 40) == 0,
            ShootHeld = r.Range(0, 3) == 0,
            PassPressed = r.Range(0, 50) == 0,
            DefensePressed = r.Range(0, 30) == 0,
            DunkPressed = r.Range(0, 200) == 0,
            LayupPressed = r.Range(0, 200) == 0,
        };

        [Test]
        public void BothMessage_RoundTrips()
        {
            var r = new SeededRandom(3);
            var a = new List<PlayerInput>();
            var b = new List<PlayerInput>();
            for (int i = 0; i < 50; i++) { a.Add(LinkProtocol.Quantize(RandomInput(r))); b.Add(LinkProtocol.Quantize(RandomInput(r))); }
            var m = LinkProtocol.Both(1000, a, b, 10, 30);
            var ra = new List<PlayerInput>();
            var rb = new List<PlayerInput>();
            Assert.IsTrue(LinkProtocol.ReadBoth(m, out int first, ra, rb));
            Assert.AreEqual(1000, first);
            Assert.AreEqual(30, ra.Count);
            for (int i = 0; i < 30; i++)
            {
                Assert.AreEqual(a[10 + i].Move.x, ra[i].Move.x, 1e-6f);
                Assert.AreEqual(b[10 + i].ShootPressed, rb[i].ShootPressed);
                Assert.AreEqual(b[10 + i].DunkPressed, rb[i].DunkPressed);
            }
            Assert.IsFalse(LinkProtocol.ReadBoth(new byte[] { (byte)LinkMessage.Both, 0, 0, 0, 0, 9 }, out _, ra, rb), "short message refused");
        }

        [Test]
        public void WatchSetup_IsASetup_ButNotOneAPlayerAnswers()
        {
            var c = DefaultContent.Create();
            var s = Setup(c);
            var m = LinkProtocol.WatchSetupMessage(s);
            Assert.AreEqual((byte)LinkMessage.WatchSetup, m[0]);
            var back = LinkProtocol.ReadSetup(m);
            Assert.AreEqual(s.Seed, back.Seed);
            Assert.AreEqual(s.Session, back.Session);
            Assert.AreEqual(s.HomeTeamData, back.HomeTeamData);

            // A guest's lobby only answers a real Setup, never a WatchSetup.
            MemoryTransport.Pair(out var wa, out var wb);
            var guest = new LinkLobby(false, null, "1.0.0", LinkProtocol.ContentFingerprint(c), c);
            wa.Send(m);
            for (int i = 0; i < 5; i++) guest.Update(wb);
            Assert.AreEqual(LobbyStatus.Waiting, guest.Status);
            Assert.IsFalse(wa.TryReceive(out _), "the guest sent nothing back");
        }

        [Test]
        public void Watchers_SeeTheSameGame_EvenJoiningLate()
        {
            var hostCat = DefaultContent.Create();
            var s = Setup(hostCat, 9090);
            var host = MatchFor(s, out _);
            var feed = new SpectatorFeed(s);
            // Two watching phones, each on its own wire from the host (Multipeer sends to each peer).
            MemoryTransport.Pair(out var toW1, out var w1Wire, latencyTicks: 2);
            MemoryTransport.Pair(out var toW2, out var w2Wire, latencyTicks: 5);
            var w1 = new Spectator("1.0.0", LinkProtocol.ContentFingerprint(DefaultContent.Create()), DefaultContent.Create());
            var w2 = new Spectator("1.0.0", LinkProtocol.ContentFingerprint(DefaultContent.Create()), DefaultContent.Create());
            MatchSimulation m1 = null, m2 = null;

            var ra = new SeededRandom(1);
            var rb = new SeededRandom(2);
            var frames = new SeededRandom(8);
            int watchers = 0, frame = 0, maxCatchUp = 0;
            while (frame++ < 60 * 60 * 14)
            {
                if (frame == 600) watchers = 1;    // the first friend starts watching 10 s in
                if (frame == 4000) watchers = 2;   // a second one joins much later
                toW1.Tick(); toW2.Tick(); w1Wire.Tick(); w2Wire.Tick();
                int n = host.IsOver ? 0 : frames.Range(0, 3);
                for (int i = 0; i < n && !host.IsOver; i++)
                {
                    var a = LinkProtocol.Quantize(RandomInput(ra));
                    var b = LinkProtocol.Quantize(RandomInput(rb));
                    host.Step(Dt, a, b);
                    feed.Record(a, b);
                }
                if (host.IsOver) feed.End();
                foreach (var msg in feed.Take(watchers))
                {
                    if (watchers >= 1) toW1.Send(msg);
                    if (watchers >= 2) toW2.Send(msg);
                }
                Watch(w1Wire, w1, ref m1, ref maxCatchUp);
                Watch(w2Wire, w2, ref m2, ref maxCatchUp);
                if (host.IsOver && w1.HostLeft && w2.HostLeft && w1.Behind == 0 && w2.Behind == 0) break;
            }
            Assert.IsTrue(host.IsOver, "the game finished");
            Assert.IsTrue(w1.HostLeft && w2.HostLeft, "both watchers heard the host leave");
            Assert.IsTrue(m1.IsOver && m2.IsOver, "both watchers saw the final buzzer");
            Assert.AreEqual(SimHash.Of(host), SimHash.Of(m1), "watcher 1 saw exactly the same game");
            Assert.AreEqual(SimHash.Of(host), SimHash.Of(m2), "watcher 2 (late) saw exactly the same game");
            Assert.AreEqual(host.Score[0], m2.Score[0]);
            Assert.AreEqual(host.Score[1], m2.Score[1]);
            Assert.GreaterOrEqual(maxCatchUp, 10, "a late watcher fast-forwards");
        }

        private static void Watch(MemoryTransport wire, Spectator w, ref MatchSimulation m, ref int maxCatchUp)
        {
            while (wire.TryReceive(out var msg)) w.Receive(msg);
            if (!w.Ready) return;
            if (m == null) m = MatchFor(w.Setup, out _);
            int steps = Spectator.StepsThisFrame(w.Behind, 1);
            maxCatchUp = System.Math.Max(maxCatchUp, steps);
            for (int i = 0; i < steps && w.TryStep(out var a, out var b); i++) m.Step(Dt, a, b);
        }

        [Test]
        public void Spectator_HandsTheHostsNextGame_ToNext()
        {
            var c = DefaultContent.Create();
            var s = Setup(c, 1);
            var w = new Spectator("1.0.0", LinkProtocol.ContentFingerprint(c), c);
            w.Receive(LinkProtocol.WatchSetupMessage(s));
            Assert.IsTrue(w.Ready);
            w.Receive(LinkProtocol.WatchSetupMessage(s)); // a resend for another watcher: same game
            Assert.IsNull(w.Next);
            var again = s.Again(2);
            w.Receive(LinkProtocol.WatchSetupMessage(again));
            Assert.IsNotNull(w.Next);
            Assert.AreEqual(2u, w.Next.Setup.Seed);
            var a = new List<PlayerInput> { default, default };
            w.Receive(LinkProtocol.Both(0, a, a, 0, 2));
            Assert.AreEqual(0, w.Received, "the old game gets nothing more");
            Assert.AreEqual(2, w.Next.Received, "the new game gets its steps");
        }

        [Test]
        public void Spectator_RefusesAnIncompatibleGame()
        {
            var c = DefaultContent.Create();
            var s = Setup(c);
            var w = new Spectator("9.9.9", LinkProtocol.ContentFingerprint(c), c);
            w.Receive(LinkProtocol.WatchSetupMessage(s));
            Assert.IsFalse(w.Ready);
            Assert.IsNotNull(w.Error);
            StringAssert.Contains("same version", w.Error);
        }

        [Test]
        public void Feed_WithNobodyWatching_SendsNothing()
        {
            var c = DefaultContent.Create();
            var feed = new SpectatorFeed(Setup(c));
            for (int i = 0; i < 100; i++) feed.Record(default, default);
            Assert.AreEqual(0, feed.Take(0).Count);
            var first = feed.Take(1);
            Assert.AreEqual((byte)LinkMessage.WatchSetup, first[0][0]);
            Assert.AreEqual(2, first.Count, "setup plus one chunk of 100 steps");
            Assert.AreEqual(0, feed.Take(1).Count, "nothing new");
            feed.Record(default, default);
            Assert.AreEqual(1, feed.Take(1).Count);
            Assert.AreEqual(2, feed.Take(2).Count, "a new watcher gets everything again (setup + all 101 steps in one chunk)");
        }

        [Test]
        public void StepsThisFrame_RealTimeWhenCaughtUp_FastWhenBehind()
        {
            Assert.AreEqual(0, Spectator.StepsThisFrame(0, 1));
            Assert.AreEqual(1, Spectator.StepsThisFrame(3, 1));
            Assert.AreEqual(2, Spectator.StepsThisFrame(20, 1), "a small backlog drains slowly");
            Assert.AreEqual(10, Spectator.StepsThisFrame(500, 1), "catching up runs 10x");
            Assert.AreEqual(1, Spectator.StepsThisFrame(1, 4), "never more than received");
        }
    }

    public class Phase30CouchLinkTests
    {
        private static CouchCupSaveData Cup(ContentCatalog c, int players)
        {
            var league = c.TeamsInTier(TeamTier.League);
            var names = new List<string>();
            var ids = new List<string>();
            for (int i = 0; i < players; i++) { names.Add("P" + (i + 1)); ids.Add(league[i % league.Count].id); }
            var cup = new CouchCupSaveData();
            CouchCup.Start(cup, names, ids, 42);
            return cup;
        }

        [Test]
        public void CupGame_TravelsToTheOtherPhone()
        {
            var c = DefaultContent.Create();
            var cup = Cup(c, 4);
            var g = CouchCup.NextGame(cup);
            var s = CouchCup.LinkSetupFor(cup, c, g, DefaultContent.DefaultDifficultyId, 5, "1.0.0", "Host");
            Assert.IsTrue(s.IsCup);
            Assert.AreEqual(CouchCup.ContextPrefix + CouchCup.GameIndex(cup, g), s.Cup);
            var back = LinkProtocol.ReadSetup(LinkProtocol.SetupMessage(s));
            Assert.AreEqual(s.Cup, back.Cup);
            Assert.AreEqual(cup.names[g.a], back.HomeLabel);
            Assert.AreEqual(cup.names[g.b], back.AwayLabel);
            Assert.AreEqual(s.HomeTeamData, back.HomeTeamData);
            // The guest phone can play it.
            var guestCat = DefaultContent.Create();
            Assert.IsNull(LinkProtocol.Incompatible(back, "1.0.0", LinkProtocol.ContentFingerprint(guestCat), guestCat));
            Assert.IsTrue(back.Register(guestCat));
            // A replay (tie) keeps the cup game.
            Assert.AreEqual(s.Cup, s.Again(9).Cup);
            Assert.AreEqual(s.AwayLabel, s.Again(9).AwayLabel);
            // Ordinary two-phone games aren't cup games.
            var plain = LinkSetup.From(c, c.TeamsInTier(TeamTier.League)[0].id, c.TeamsInTier(TeamTier.League)[1].id, null, null, 1, "1.0.0", "H");
            Assert.IsFalse(LinkProtocol.ReadSetup(LinkProtocol.SetupMessage(plain)).IsCup);
        }

        [Test]
        public void CupResult_IsRecordedOnce_AndOnlyForTheRightGame()
        {
            var c = DefaultContent.Create();
            var cup = Cup(c, 3);
            var g = CouchCup.NextGame(cup);
            string key = CouchCup.ContextPrefix + CouchCup.GameIndex(cup, g);
            Assert.AreEqual(CouchOutcome.TieReplay, CouchCup.ReportLink(cup, key, 20, 20));
            Assert.IsTrue(ReferenceEquals(g, CouchCup.NextGame(cup)), "a tie is played again");
            Assert.AreEqual(CouchOutcome.Recorded, CouchCup.ReportLink(cup, key, 21, 18));
            Assert.AreEqual(CouchOutcome.NoGame, CouchCup.ReportLink(cup, key, 21, 18), "the same game never counts twice");
            var final = CouchCup.NextGame(cup);
            Assert.IsNotNull(final);
            Assert.AreEqual(CouchOutcome.NoGame, CouchCup.ReportLink(cup, "couch:99", 10, 2), "a stale game key is ignored");
            Assert.AreEqual(CouchOutcome.Champion, CouchCup.ReportLink(cup, CouchCup.ContextPrefix + CouchCup.GameIndex(cup, final), 10, 2));
            Assert.AreEqual(final.a, cup.champion);
        }
    }

    public class Phase30FrameTests
    {
        private static bool FrameEquals(PixelCanvas sheet, CallerRetroBall.Logic.PixelArt.CharacterView v, int f1, int f2)
        {
            CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.FrameOrigin(v, f1, out int x1, out int y1);
            CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.FrameOrigin(v, f2, out int x2, out int y2);
            for (int y = 0; y < CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.SheetFrameHeight; y++)
                for (int x = 0; x < CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.SheetFrameWidth; x++)
                    if (!sheet.Get(x1 + x, y1 + y).Equals(sheet.Get(x2 + x, y2 + y))) return false;
            return true;
        }

        [Test]
        public void NewPoseFrames_AreDrawn_AndDifferFromEveryOtherFrame()
        {
            var c = DefaultContent.Create();
            foreach (var player in new[] { c.Players[0], c.Players[7], c.Players[15] })
            {
                var look = player.appearance;
                foreach (var kit in new[] { CallerRetroBall.Logic.KitLook.Classic(RgbColor.FromHex("#C1121F"), RgbColor.White, RgbColor.FromHex("#FFD166"), null, TeamPattern.Solid, null) })
                {
                    var sheet = CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.GenerateSheet(look, kit);
                    Assert.AreEqual(CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.SheetFrameWidth * 14, sheet.Width, "14 frames per view (G2 appended defence, slide, dunk and pass after these)");
                    for (int v = 0; v < 3; v++)
                        foreach (int f in new[] { CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.CrossoverFrame,
                                                  CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.StepBackFrame,
                                                  CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.ChestThumpFrame })
                            for (int other = 0; other < CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.FramesPerView; other++)
                                if (other != f)
                                    Assert.IsFalse(FrameEquals(sheet, (CallerRetroBall.Logic.PixelArt.CharacterView)v, f, other), "view " + v + " frame " + f + " looks like frame " + other);
                }
            }
        }

        [Test]
        public void Poses_PickTheirFrames()
        {
            Assert.AreEqual(-1, CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.FrameFor(PoseFrame.None));
            Assert.AreEqual(CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.CrossoverFrame, CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.FrameFor(PoseFrame.Crossover));
            Assert.AreEqual(PoseFrame.Crossover, Flair.DribbleMove(DribbleMoveKind.Crossover, Flair.DribbleMoveSeconds * 0.5f).Frame);
            Assert.AreEqual(PoseFrame.StepBack, Flair.DribbleMove(DribbleMoveKind.StepBack, Flair.DribbleMoveSeconds * 0.5f).Frame);
            Assert.AreEqual(PoseFrame.None, Flair.DribbleMove(DribbleMoveKind.SpinCycle, Flair.DribbleMoveSeconds * 0.5f).Frame);
            Assert.AreEqual(CelebrationKind.ChestThump, Flair.CelebrationFor("cosmetic.celebration.chest_thump"));
            var c = DefaultContent.Create();
            Assert.IsNotNull(c.Find(c.Cosmetics, "cosmetic.celebration.chest_thump"));
            Assert.IsTrue(ContentValidator.Validate(c).IsValid);
            int thumps = 0, up = 0;
            for (float t = 0f; t < Flair.CelebrationSeconds; t += 0.02f)
            {
                var p = Flair.Celebration(CelebrationKind.ChestThump, t);
                if (p.Frame == PoseFrame.ChestThump) thumps++;
                if (p.ArmsUp) up++;
                Assert.IsFalse(p.Frame == PoseFrame.ChestThump && p.ArmsUp, "one pose at a time");
            }
            Assert.Greater(thumps, 10, "thumps the chest");
            Assert.Greater(up, 5, "then the fist goes up");
            Assert.IsTrue(Flair.Celebration(CelebrationKind.ChestThump, Flair.CelebrationSeconds).IsNone, "over after the celebration");
        }
    }
}
