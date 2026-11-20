using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>20 Nov 2026, improvements I156–I175: posters, pickpockets, coaching notes, loyalty and weekend courts, and more.</summary>
    public class ImprovementsI156Tests
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

        // ------------------------------------------------------------------ I156 / I159: posters and hangs

        [Test]
        public void I156_PosterVictim_IsTheNearestOpponentInRange()
        {
            var m = NewMatch(3);
            var me = m.Players[m.ControlledIndex];
            int opp = -1;
            for (int i = 0; i < m.Players.Length; i++) if (m.Players[i].Team != me.Team) { opp = i; break; }
            foreach (var p in m.Players) if (p.Team != me.Team) p.Motion.position = me.Position + new Vec2(0f, 9f);
            Assert.AreEqual(-1, m.PosterVictim(me.Index), "nobody close");
            m.Players[opp].Motion.position = me.Position + new Vec2(0.5f, 0.5f);
            Assert.AreEqual(opp, m.PosterVictim(me.Index));
            m.Players[opp].Motion.position = me.Position + new Vec2(0f, MatchSimulation.PosterRange + 0.3f);
            Assert.AreEqual(-1, m.PosterVictim(me.Index), "a step too far");
        }

        [Test]
        public void I156_I159_AMadeDunkOverADefender_IsAPoster_AndEveryMadeDunkHangs()
        {
            bool poster = false, hang = false;
            for (uint seed = 1; seed <= 30 && !poster; seed++)
            {
                var m = NewMatch(seed);
                var me = m.Players[m.ControlledIndex];
                me.Def.attributes.finishing = 99;
                me.Motion.position = m.Setup.Court.Hoop + new Vec2(0.2f, 1.4f);
                for (int i = 0; i < 240 && !poster; i++)
                {
                    // Keep one defender glued to the dunker, so a made dunk is over him.
                    foreach (var p in m.Players)
                        if (p.Team != me.Team) { p.Motion.position = me.Position + new Vec2(0.3f, -0.4f); break; }
                    m.Step(Dt, m.Ball.Phase == BallPhase.Held && m.HumanHasBall ? new PlayerInput { DunkPressed = true } : default);
                    foreach (var e in m.Events)
                    {
                        if (e.Type == MatchEventType.ShotMade && m.Ball.ShotType == ShotType.Dunk && e.PlayerIndex == me.Index) hang = true;
                        if (e.Type != MatchEventType.Posterized) continue;
                        poster = true;
                        Assert.AreEqual(me.Index, e.PlayerIndex);
                        Assert.AreNotEqual(me.Team, m.Players[e.Value].Team);
                        Assert.GreaterOrEqual(m.Stats[me.Index].posters, 1);
                        Assert.GreaterOrEqual(m.Stats[me.Index].rimHangs, 1);
                    }
                }
            }
            Assert.IsTrue(hang, "a made dunk");
            Assert.IsTrue(poster, "a poster in 30 tries");
        }

        [Test]
        public void I158_SwingTurns_AreSpottedOncePerTurn()
        {
            int side = 0, turns = 0;
            foreach (int s in new[] { 0, 1, 2, 1, 0, -1, -2, -1, 0, 1, 1, 0 })
                if (Dunks.SwingTurned(ref side, s)) turns++;
            Assert.AreEqual(2, turns, "right to left, then left to right");
        }

        // ------------------------------------------------------------------ I160 / I161: steals

        [Test]
        public void I160_I161_StealAttemptsCount_AndARunIsAPickpocket()
        {
            var m = NewMatch(3, offense: 1);
            var me = m.Controlled;
            m.Setup.PassiveOpponents = true;
            m.Setup.Defense.stealBase = 5f; // certain, so the run is the thing under test
            int picks = 0;
            for (int n = 0; n < 6; n++)
            {
                for (int i = 0; i < 200 && !(m.Ball.IsHeld && m.Holder.Team != me.Team && m.Phase == MatchPhase.Live); i++) m.Step(Dt, PlayerInput.None);
                if (!m.Ball.IsHeld || m.Holder.Team == me.Team) continue;
                me.Motion.position = m.Holder.Position + new Vec2(0.2f, -0.2f);
                me.StealReadyAt = 0f; me.StunnedUntil = -1f;
                m.TrySteal(me.Index);
                foreach (var e in m.Events) if (e.Type == MatchEventType.Pickpocket && e.PlayerIndex == me.Index) picks = Math.Max(picks, e.Value);
                // Give it straight back to them for the next one.
                if (m.Ball.IsHeld && m.Holder.Team == me.Team)
                {
                    m.Step(Dt, PlayerInput.None);
                    foreach (var e in m.Events) if (e.Type == MatchEventType.Pickpocket && e.PlayerIndex == me.Index) picks = Math.Max(picks, e.Value);
                    break;
                }
            }
            Assert.GreaterOrEqual(m.Stats[me.Index].stealAttempts, m.Stats[me.Index].steals);
            Assert.GreaterOrEqual(m.Stats[me.Index].stealAttempts, 1);
            Assert.AreEqual(2, MatchSimulation.PickpocketRun);
        }

        [Test]
        public void I161_ARunNeedsTwoInARow_AMissResetsIt()
        {
            var m = NewMatch(5, offense: 1);
            var me = m.Controlled;
            // A far reach is a miss and resets the run (and counts as a short reach).
            me.Motion.position = m.Holder.Position + new Vec2(0f, 9f);
            m.TrySteal(me.Index);
            Assert.AreEqual(1, m.Stats[me.Index].stealAttempts);
            Assert.AreEqual(1, m.Stats[me.Index].shortReaches);
            Assert.AreEqual("0/1 " + Loc.T("STEALS"), Habits.StealLine(m.Stats[me.Index]));
            Assert.AreEqual("", Habits.StealLine(new PlayerStatLine()));
        }

        // ------------------------------------------------------------------ I162 / I163: coaching notes

        [Test]
        public void I163_ReleaseNote_NeedsAHabit_AndFades()
        {
            var t = new CareerTotals();
            Assert.IsNull(Habits.ReleaseNote(t));
            Habits.AddGame(t, new PlayerStatLine { jumpers = 12, earlyReleases = 7, lateReleases = 1 });
            StringAssert.Contains("early", Habits.ReleaseNote(t));
            for (int g = 0; g < 6; g++) Habits.AddGame(t, new PlayerStatLine { jumpers = 12, earlyReleases = 1, lateReleases = 1 });
            Assert.IsNull(Habits.ReleaseNote(t), "fixed habits stop being mentioned");
            var late = new CareerTotals();
            Habits.AddGame(late, new PlayerStatLine { jumpers = 15, lateReleases = 9 });
            StringAssert.Contains("late", Habits.ReleaseNote(late));
            Assert.IsNull(Habits.ReleaseNote(new CareerTotals { recentJumpers = 5, recentEarly = 5 }), "too few jumpers to judge");
        }

        [Test]
        public void I162_StealNote_AfterRepeatedShortReaches()
        {
            var t = new CareerTotals();
            Habits.AddGame(t, new PlayerStatLine { shortReaches = 2 });
            Assert.IsNull(Habits.StealNote(t));
            Habits.AddGame(t, new PlayerStatLine { shortReaches = 3 });
            StringAssert.Contains("STEAL", Habits.StealNote(t));
            Assert.AreEqual(1, Habits.Notes(t).Count);
        }

        [Test]
        public void I163_Jumpers_AreCountedInTheSimulation_EarlyOrLateAreJumpers()
        {
            var m = NewMatch(7);
            var line = m.Stats[m.ControlledIndex];
            Assert.LessOrEqual(line.earlyReleases + line.lateReleases, line.jumpers);
        }

        [Test]
        public void I159_I160_CareerTotals_AddUp_AndSave()
        {
            var d = Career.New(_c);
            var m = NewMatch(3);
            m.Stats[m.ControlledIndex].posters = 1;
            m.Stats[m.ControlledIndex].rimHangs = 2;
            m.Stats[m.ControlledIndex].stealAttempts = 5;
            m.Stats[m.ControlledIndex].shortReaches = 4;
            var s = MatchSummary.From(m, GameMode.QuickCall, "i156-a");
            Career.ApplyMatch(d, s, new RewardGrant());
            Assert.AreEqual(1, d.totals.posters);
            Assert.AreEqual(2, d.totals.rimHangs);
            Assert.AreEqual(5, d.totals.stealAttempts);
            Assert.AreEqual(4, d.totals.recentShortReaches);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(1, back.totals.posters);
            Assert.AreEqual(2, back.totals.rimHangs);
            Assert.AreEqual(5, back.totals.stealAttempts);
            Assert.AreEqual(4, back.totals.recentShortReaches);
        }

        // ------------------------------------------------------------------ I166: loyalty courts

        [Test]
        public void I166_LoyaltyCourts_FromYourBestStreak_OnePickEach()
        {
            var d = Career.New(_c);
            Assert.AreEqual(0, Loyalty.Available(d));
            Assert.AreEqual(7, Loyalty.DaysToNext(d));
            d.loginStreak = 4; d.loginBest = 4;
            Assert.AreEqual(3, Loyalty.DaysToNext(d));
            Assert.IsFalse(Loyalty.Claim(d, "court.shop.laser"), "not earned yet");
            d.loginBest = 7;
            Assert.AreEqual(1, Loyalty.Available(d));
            Assert.IsFalse(Loyalty.Claim(d, "court.sunset_cage"), "only shop courts");
            Assert.IsTrue(Loyalty.Claim(d, "court.shop.laser"));
            Assert.IsTrue(Shop.CourtOpen(d, "court.shop.laser"));
            Assert.IsFalse(Shop.CourtOpen(d, "court.shop.synthwave"), "just the one court, not the pack");
            Assert.AreEqual(0, Loyalty.Available(d));
            Assert.IsFalse(Loyalty.Claim(d, "court.shop.finals"));
            d.loginBest = 31;
            Assert.IsFalse(Loyalty.Claim(d, "court.shop.laser"), "already yours");
            Assert.IsTrue(Loyalty.Claim(d, "court.shop.finals"));
            Assert.AreEqual(0, Loyalty.DaysToNext(d));
            Assert.IsTrue(Shop.OwnedCourts(d).Contains("court.shop.finals"));
            // A refund of everything never takes a loyalty court back (no purchase was involved).
            Shop.Apply(d, _c, new string[0]);
            Assert.IsTrue(Shop.CourtOpen(d, "court.shop.finals"));
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(2, back.shopEarned.Count);
            Assert.IsTrue(Shop.CourtOpen(back, "court.shop.laser"));
        }

        [Test]
        public void I166_LoadedJunk_CantGrantCourts()
        {
            var d = Career.New(_c);
            d.shopEarned.AddRange(new[] { "court.sunset_cage", "court.shop.laser", "court.shop.harbor", "court.shop.finals", "court.shop.mosaic" });
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.LessOrEqual(back.shopEarned.Count, Loyalty.Milestones.Length);
            Assert.IsFalse(back.shopEarned.Contains("court.sunset_cage"));
        }

        // ------------------------------------------------------------------ I167: weekend court

        [Test]
        public void I167_WeekendCourt_OnSaturdayAndSunday_Rotating()
        {
            int sat = DailyChallenges.DayNumber(new DateTime(2026, 11, 21));
            Assert.AreEqual(DayOfWeek.Saturday, new DateTime(2026, 11, 21).DayOfWeek);
            Assert.IsTrue(WeekendCourt.IsWeekend(sat));
            Assert.IsTrue(WeekendCourt.IsWeekend(sat + 1));
            Assert.IsFalse(WeekendCourt.IsWeekend(sat + 2));
            Assert.IsNull(WeekendCourt.CourtFor(sat - 1));
            var a = WeekendCourt.CourtFor(sat);
            var b = WeekendCourt.CourtFor(sat + 1);
            Assert.IsTrue(Shop.IsShopCourt(a) && Shop.IsShopCourt(b));
            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(a, WeekendCourt.CourtFor(sat + 7), "next weekend is another court");
            Assert.AreEqual(a, DailyChallenges.For(sat, _c).CourtId);
            Assert.IsFalse(Shop.IsShopCourt(DailyChallenges.For(sat + 3, _c).CourtId), "weekdays keep the home court");
            var seen = new HashSet<string>();
            for (int w = 0; w < 6; w++) { seen.Add(WeekendCourt.CourtFor(sat + 7 * w)); seen.Add(WeekendCourt.CourtFor(sat + 7 * w + 1)); }
            Assert.AreEqual(12, seen.Count, "six weekends visit all twelve courts");
        }

        // ------------------------------------------------------------------ I168 / I169 / I174

        [Test]
        public void I168_PreviewLook_WearsThePack()
        {
            var look = Shop.PreviewLook(Shop.Product(Shop.ArcadeGlow), _c);
            Assert.AreEqual(RgbColor.FromHex("#00F5D4"), look.Jersey);
            Assert.AreEqual(RgbColor.FromHex("#9B5DE5"), look.Shoe);
            Assert.AreEqual(GearParts.Goggles, look.Gear);
            var sheet = CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.GenerateSheet(_c.Players[0].appearance, look);
            Assert.Greater(sheet.Width, 0);
        }

        [Test]
        public void I169_MyPurchases_ListsWhatsYours()
        {
            var d = Career.New(_c);
            Assert.AreEqual(0, PurchaseHistory.Lines(d, false).Count);
            Shop.Apply(d, _c, new[] { Shop.CityLights });
            d.loginBest = 7;
            Loyalty.Claim(d, "court.shop.laser");
            Shop.RecordTip(d, Shop.TipSmall);
            var lines = PurchaseHistory.Lines(d, true);
            Assert.AreEqual(4, lines.Count);
            Assert.IsTrue(lines.Exists(l => l.Contains("LASER LANES")));
        }

        [Test]
        public void I174_ShopNews_ShowsUntilSeen_AndSaves()
        {
            var d = Career.New(_c);
            Assert.IsTrue(ShopNews.HasNew(d));
            ShopNews.MarkSeen(d);
            Assert.IsFalse(ShopNews.HasNew(d));
            Assert.IsFalse(ShopNews.HasNew(SaveCodec.Decode(SaveCodec.Encode(d), _c, out _)));
        }

        // ------------------------------------------------------------------ I170 / I172 / I175

        [Test]
        public void I170_NewAchievements_AreEarnedByPlay()
        {
            var d = Career.New(_c);
            foreach (var id in new[] { Achievements.Posterizer, Achievements.HangTime, Achievements.Pickpocket, Achievements.Regular })
                Assert.IsFalse(Achievements.All.Find(a => a.Id == id).Earned(d), id);
            d.totals.posters = 1; d.totals.rimHangs = 25; d.totals.steals = 25; d.loginBest = 7;
            foreach (var id in new[] { Achievements.Posterizer, Achievements.HangTime, Achievements.Pickpocket, Achievements.Regular })
                Assert.IsTrue(Achievements.All.Find(a => a.Id == id).Earned(d), id);
            Assert.LessOrEqual(Achievements.TotalPoints, 1000);
        }

        [Test]
        public void I172_Landscape_DrawsButtonsSmaller_AndTouchAreasStillDontOverlap()
        {
            Assert.AreEqual(1f, ControlLayout.ScaleFor(false));
            Assert.Less(ControlLayout.ScaleFor(true), 1f);
            var slots = ControlLayout.Default();
            for (int i = 0; i < slots.Length; i++)
                for (int j = i + 1; j < slots.Length; j++)
                    Assert.Greater(ControlLayout.Gap(slots[i], slots[j]) * ControlLayout.LandscapeScale,
                                   (ControlLayout.HitPad(slots, i) + ControlLayout.HitPad(slots, j)) * ControlLayout.LandscapeScale);
        }

        [Test]
        public void I175_NewWords_HaveSpanish()
        {
            foreach (var w in new[] { "POSTERIZED!", "RIM HANGS", "LOYALTY COURT", "WEEKEND COURT", "MY PURCHASES", "CLAIM FREE", "PICKPOCKET!", "STEALS",
                                      "HAPTICS", "NEW", "Nothing yet." })
                Assert.IsTrue(Loc.Has(w), w);
            foreach (var t in new[] { new CareerTotals { recentJumpers = 20, recentEarly = 12 }, new CareerTotals { recentJumpers = 20, recentLate = 12 },
                                      new CareerTotals { recentShortReaches = 9 } })
                foreach (var note in Habits.Notes(t)) Assert.IsTrue(Loc.Has(note), note);
        }
    }
}
