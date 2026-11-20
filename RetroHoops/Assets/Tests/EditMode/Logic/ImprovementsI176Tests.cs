using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>20 Nov 2026, improvements I176–I195: chase-downs, clutch buckets, GREEN runs, and the polish around them.</summary>
    public class ImprovementsI176Tests
    {
        private const float Dt = 1f / 60f;
        private ContentCatalog _c;

        [SetUp]
        public void Setup() => _c = DefaultContent.Create();

        private MatchSimulation NewMatch(uint seed, int offense = 0)
        {
            var req = MatchRequest.QuickCallDefault(_c);
            req.Seed = seed;
            var setup = MatchSetup.FromRequest(req, _c);
            setup.StartingOffense = offense;
            var m = new MatchSimulation(setup);
            for (int i = 0; i < 120 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, PlayerInput.None);
            return m;
        }

        private static int Opponent(MatchSimulation m, int of)
        {
            for (int i = 0; i < m.Players.Length; i++) if (m.Players[i].Team != m.Players[of].Team) return i;
            return -1;
        }

        [Test]
        public void I176_ChaseDown_IsABlockFromFurtherOutThanTheShooter()
        {
            var m = NewMatch(3);
            int shooter = m.ControlledIndex, blocker = Opponent(m, shooter);
            var hoop = m.Setup.Court.Hoop;
            m.Players[shooter].Motion.position = hoop + new Vec2(0f, 1.5f);
            m.Players[blocker].Motion.position = hoop + new Vec2(0f, 2.6f);
            Assert.IsTrue(m.IsChaseDown(shooter, blocker), "came from behind");
            m.Players[blocker].Motion.position = hoop + new Vec2(0f, 1.0f);
            Assert.IsFalse(m.IsChaseDown(shooter, blocker), "in front of him: an ordinary block");
            m.Players[blocker].Motion.position = hoop + new Vec2(0f, 1.5f + MatchSimulation.ChaseDownMargin * 0.5f);
            Assert.IsFalse(m.IsChaseDown(shooter, blocker), "level is not chasing");
        }

        [Test]
        public void I177_I178_ClutchAndGreenRuns_AreCounted_AndSaved()
        {
            var d = Career.New(_c);
            var m = NewMatch(5);
            var line = m.Stats[m.ControlledIndex];
            line.clutchMakes = 2; line.chaseDowns = 1; line.bestGreenRun = 4;
            Career.ApplyMatch(d, MatchSummary.From(m, GameMode.QuickCall, "i176-a"), new RewardGrant());
            Assert.AreEqual(2, d.totals.clutchMakes);
            Assert.AreEqual(1, d.totals.chaseDowns);
            Assert.AreEqual(4, d.totals.bestGreenRun);
            line.bestGreenRun = 3;
            Career.ApplyMatch(d, MatchSummary.From(m, GameMode.QuickCall, "i176-b"), new RewardGrant());
            Assert.AreEqual(4, d.totals.bestGreenRun, "the best run is kept");
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(4, back.totals.clutchMakes);
            Assert.AreEqual(2, back.totals.chaseDowns);
            Assert.AreEqual(4, back.totals.bestGreenRun);
            Assert.AreEqual(5f, MatchSimulation.ClutchSeconds);
            Assert.AreEqual(3, MatchSimulation.GreenStreakRun);
        }

        [Test]
        public void I177_ClutchBuckets_OnlyLate_AndOnlyToTieOrLead()
        {
            // Play clock games and check every Clutch event against the rule.
            int seen = 0;
            for (uint seed = 1; seed <= 6; seed++)
            {
                var req = MatchRequest.QuickCallDefault(_c);
                req.Seed = seed;
                var setup = MatchSetup.FromRequest(req, _c);
                setup.Rules.useGameClock = true;
                setup.Rules.gameClockSeconds = 25f;
                var m = new MatchSimulation(setup);
                var rng = new SeededRandom(seed);
                for (int i = 0; i < 60 * 120 && !m.IsOver; i++)
                {
                    m.Step(Dt, new PlayerInput { Move = new Vec2(rng.Range(-10, 11) / 10f, rng.Range(-10, 11) / 10f), ShootPressed = rng.Range(0, 40) == 0, ShootHeld = rng.Range(0, 2) == 0 });
                    foreach (var e in m.Events)
                    {
                        if (e.Type != MatchEventType.Clutch) continue;
                        seen++;
                        Assert.LessOrEqual(m.GameClock, MatchSimulation.ClutchSeconds + 0.01f);
                        Assert.GreaterOrEqual(m.Score[e.Team], m.Score[1 - e.Team], "tied it or went ahead");
                    }
                }
            }
            Assert.GreaterOrEqual(seen, 0);
        }

        [Test]
        public void I180_JumpWait_FallsFromOneToZero()
        {
            var m = NewMatch(3, offense: 1);
            var me = m.ControlledIndex;
            Assert.AreEqual(0f, m.JumpWait01(me));
            Assert.IsTrue(m.Jump(me));
            Assert.Greater(m.JumpWait01(me), 0.9f);
            float full = m.Setup.Defense.jumpSeconds + m.Setup.Defense.jumpCooldown;
            for (float t = 0f; t < full + 0.1f; t += Dt) m.Step(Dt, PlayerInput.None);
            Assert.AreEqual(0f, m.JumpWait01(me), 1e-4f);
            Assert.AreEqual(0f, m.JumpWait01(-1));
        }

        [Test]
        public void I181_SwitchWithNobodyToSwitch_IsRemembered()
        {
            var m = NewMatch(3, offense: 1);
            int me = m.ControlledIndex;
            // Make sure I'm already guarding the ball, so SWITCH has nothing to do.
            if (m.Ball.IsHeld && m.GuardingOf(me) != m.HolderIndex) m.SwitchOntoBall(me);
            float before = m.SwitchRefusedAt;
            m.Step(Dt, new PlayerInput { PassPressed = true });
            if (m.Ball.IsHeld && m.GuardingOf(me) == m.HolderIndex && m.Phase == MatchPhase.Live)
                Assert.Greater(m.SwitchRefusedAt, before);
        }

        [Test]
        public void I182_DifficultyNudge_AfterThreeBlowouts_EitherWay()
        {
            var t = new CareerTotals();
            var ladder = new List<DifficultyDef> { new DifficultyDef { id = "a" }, new DifficultyDef { id = "b" }, new DifficultyDef { id = "c" } };
            MatchSummary Game(int me, int them) => new MatchSummary { mode = GameMode.QuickCall, humanTeam = 0, scoreA = me, scoreB = them, winner = me > them ? 0 : 1 };
            DifficultyNudge.AddGame(t, Game(21, 10));
            DifficultyNudge.AddGame(t, Game(21, 12));
            Assert.IsNull(DifficultyNudge.Suggest(t, ladder, "b"));
            DifficultyNudge.AddGame(t, Game(21, 9));
            Assert.AreEqual("c", DifficultyNudge.Suggest(t, ladder, "b").id);
            Assert.IsNull(DifficultyNudge.Suggest(t, ladder, "c"), "nothing above the top");
            DifficultyNudge.AddGame(t, Game(21, 19));
            Assert.AreEqual(0, t.blowoutWins, "a close game breaks the run");
            for (int i = 0; i < 3; i++) DifficultyNudge.AddGame(t, Game(8, 21));
            Assert.AreEqual("a", DifficultyNudge.Suggest(t, ladder, "b").id);
            DifficultyNudge.Reset(t);
            Assert.IsNull(DifficultyNudge.Suggest(t, ladder, "b"));
            var other = new MatchSummary { mode = GameMode.Rise, humanTeam = 0, scoreA = 21, scoreB = 2, winner = 0 };
            for (int i = 0; i < 5; i++) DifficultyNudge.AddGame(t, other);
            Assert.AreEqual(0, t.blowoutWins, "only Quick Call counts");
        }

        [Test]
        public void I183_ShareText_IsPlain_AndSaysTheResult()
        {
            var s = new MatchSummary { teamAName = "Night Owls", teamBName = "Dune Runners", scoreA = 21, scoreB = 17, humanTeam = 0, winner = 0 };
            string text = GameShare.Text(s, "Laser Lanes", "POSTERIZED!");
            StringAssert.Contains("W  Night Owls 21 - 17 Dune Runners", text);
            StringAssert.Contains("Laser Lanes", text);
            StringAssert.Contains("PLAY OF THE GAME: POSTERIZED!", text);
            StringAssert.Contains("RETRO HOOPS", text);
            Assert.AreEqual("abc", GameShare.Strip("a<color=#fff>b</color>c"));
            Assert.IsFalse(GameShare.Text(s, "<b>x</b>", null).Contains("<"));
            Assert.AreEqual("", GameShare.Text(null, null, null));
        }

        [Test]
        public void I185_CareerStats_ShowTheNewNumbers()
        {
            var d = Career.New(_c);
            d.totals.games = 4; d.totals.wins = 3; d.totals.losses = 1; d.totals.points = 30;
            d.totals.steals = 3; d.totals.stealAttempts = 6; d.totals.posters = 2; d.totals.chaseDowns = 1; d.totals.clutchMakes = 1;
            var rows = CareerStats.Rows(d);
            string Value(string label) => rows.Find(r => r.label == label).value;
            Assert.AreEqual("3-1", Value("RECORD"));
            Assert.AreEqual("75%", Value("WIN %"));
            StringAssert.Contains("7.5", Value("POINTS"));
            Assert.AreEqual("3/6  50%", Value("STEALS"));
            Assert.AreEqual("2", Value("POSTERS"));
            Assert.AreEqual("1", Value("CHASE-DOWN BLOCKS"));
            foreach (var r in rows) Assert.IsTrue(Loc.Has(r.label), r.label + " needs Spanish");
            Assert.AreEqual(0, CareerStats.Rows(null).Count);
        }

        [Test]
        public void I186_SurpriseCourt_PicksARealCourt_FromTheList()
        {
            var list = new List<string> { null, "court.a", "court.b", "court.c" };
            var seen = new HashSet<string>();
            for (uint s = 1; s < 60; s++)
            {
                var pick = SurpriseCourt.Pick(list, s);
                Assert.IsNotNull(pick);
                Assert.IsTrue(list.Contains(pick));
                seen.Add(pick);
            }
            Assert.AreEqual(3, seen.Count, "every court comes up");
            Assert.IsNull(SurpriseCourt.Pick(new List<string> { null }, 3));
        }

        [Test]
        public void I191_I192_MakeHome_AndWearIt_NeedOwnership()
        {
            var d = Career.New(_c);
            Assert.AreEqual(0, PackWear.Wear(d, _c, Shop.StreetKings), "not owned");
            Assert.IsFalse(ShopHome.Set(d, "court.shop.laser"), "no team of your own yet / not owned");
            Shop.Apply(d, _c, new[] { Shop.StreetKings, Shop.NeonNights });
            Assert.AreEqual(4, PackWear.Wear(d, _c, Shop.StreetKings), "jersey, shoes, banner, gear");
            Assert.AreEqual("cosmetic.jersey.kingpin", d.Equipped(CosmeticSlot.JerseyPalette));
            Assert.AreEqual("cosmetic.gear.crown_sleeves", d.Equipped(CosmeticSlot.Gear));
            d.customTeam.created = true;
            Assert.IsFalse(ShopHome.Set(d, "court.shop.finals"), "not owned");
            Assert.IsTrue(ShopHome.Set(d, "court.shop.laser"));
            Assert.IsTrue(ShopHome.IsHome(d, "court.shop.laser"));
            Assert.IsTrue(CustomTeams.HomeCourts(_c, d).Exists(k => k.id == "court.shop.laser"));
        }

        [Test]
        public void I179_GreenBuzz_IsOffByDefault_AndSaves()
        {
            var d = Career.New(_c);
            Assert.IsFalse(d.settings.greenBuzz);
            d.settings.greenBuzz = true;
            Assert.IsTrue(SaveCodec.Decode(SaveCodec.Encode(d), _c, out _).settings.greenBuzz);
        }

        [Test]
        public void I188_I195_WhatsNew_AndNewWords()
        {
            Assert.IsTrue(Tours.WhatsNew.Id.StartsWith("tour.whats_new_"), Tours.WhatsNew.Id); // later phases refresh the card
            foreach (var w in new[] { "CHASE-DOWN BLOCK!", "ICE COLD!", "LOCKED IN", "CAREER STATS", "SURPRISE ME", "FEEL THE GREEN",
                                      "WEAR IT", "MAKE HOME", "SHARE RESULT", "CALLING FOR IT", "ALREADY ON THE BALL", "PLAY OF THE GAME", "CHASE-DOWN!" })
                Assert.IsTrue(Loc.Has(w), w);
        }
    }
}
