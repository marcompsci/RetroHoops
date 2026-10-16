using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>16 Oct 2026 improvement phases I1-I3 (from App Store basketball reviews): big moments, fair play, gear and legacy.</summary>
    public class ImprovementTests
    {
        // ---- I1: big moments

        [Test]
        public void I1_GameWinner_OnlyForTheWinnersLastBasket()
        {
            Assert.IsTrue(GameWinnerMob.IsGameWinner(0, 0, 10f, 10.1f));
            Assert.IsFalse(GameWinnerMob.IsGameWinner(0, 1, 10f, 10.1f), "the losers scored last");
            Assert.IsFalse(GameWinnerMob.IsGameWinner(0, 0, 5f, 10f), "the clock ran out long after the last basket");
            Assert.IsFalse(GameWinnerMob.IsGameWinner(-1, 0, 10f, 10f), "a tie has no winner");
            Assert.IsTrue(GameWinnerMob.IsBuzzerBeater(true, 0.4f));
            Assert.IsFalse(GameWinnerMob.IsBuzzerBeater(true, 8f));
            Assert.IsFalse(GameWinnerMob.IsBuzzerBeater(false, 0f), "no clock, no buzzer");
            Assert.AreEqual("BUZZER BEATER!", GameWinnerMob.Title(true));
            Assert.AreEqual("GAME WINNER!", GameWinnerMob.Title(false));
        }

        [Test]
        public void I1_Mob_RunsInThenJumps_InARingRoundTheShooter()
        {
            var shooter = new Vec2(0f, 5f);
            var from = new Vec2(4f, 1f);
            GameWinnerMob.Pose(from, shooter, 0, 2, 0f, out var g0, out int l0, out bool a0);
            Assert.AreEqual(from.x, g0.x, 1e-4f);
            Assert.AreEqual(0, l0);
            Assert.IsFalse(a0, "still running");
            bool jumped = false;
            Vec2 a = default, b = default;
            for (float t = GameWinnerMob.ArriveSeconds; t < GameWinnerMob.Seconds; t += 0.05f)
            {
                GameWinnerMob.Pose(from, shooter, 0, 2, t, out a, out int lift, out bool arms);
                Assert.IsTrue(arms);
                Assert.LessOrEqual(lift, 5);
                jumped |= lift >= 3;
                Assert.AreEqual(GameWinnerMob.Radius, Dist(a, shooter, 0.7f), 0.01f, "in the ring");
            }
            Assert.IsTrue(jumped, "they jump");
            GameWinnerMob.Pose(from, shooter, 1, 2, 1f, out b, out _, out _);
            Assert.Greater((a - b).Magnitude, 0.5f, "two teammates don't stand on the same spot");
        }

        private static float Dist(Vec2 p, Vec2 c, float squash)
        {
            float dx = p.x - c.x, dy = (p.y - c.y) / squash;
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }

        [Test]
        public void I1_Chants_FollowTheGame()
        {
            var score = new[] { 18, 19 };
            // Home = team 0 defending while team 1 needs 2: DEFENSE.
            Assert.AreEqual(CrowdChant.Defense, CrowdChants.For(true, 0, 1, score, new[] { 3, 2 }, -1f, false));
            // Tight late on a clock.
            Assert.AreEqual(CrowdChant.Defense, CrowdChants.For(true, 0, 1, new[] { 40, 41 }, null, 20f, false));
            // Home ball with a heated-up scorer: MVP.
            Assert.AreEqual(CrowdChant.Mvp, CrowdChants.For(true, 0, 0, new[] { 10, 4 }, new[] { 11, 17 }, -1f, true));
            // Down four: LET'S GO.
            Assert.AreEqual(CrowdChant.LetsGo, CrowdChants.For(true, 0, 0, new[] { 4, 9 }, new[] { 17, 12 }, -1f, false));
            // Nothing to shout about, dead ball, and versus (no home crowd).
            Assert.AreEqual(CrowdChant.None, CrowdChants.For(true, 0, 0, new[] { 6, 6 }, new[] { 15, 15 }, -1f, false));
            Assert.AreEqual(CrowdChant.None, CrowdChants.For(false, 0, 1, score, new[] { 3, 2 }, -1f, false));
            Assert.AreEqual(CrowdChant.None, CrowdChants.For(true, -1, 1, score, new[] { 3, 2 }, -1f, false));
            Assert.AreEqual("LET'S GO COMETS!", CrowdChants.Text(CrowdChant.LetsGo, "comets"));
        }
        // ---- I2: fair play and house rules

        [Test]
        public void I2_FairPlay_AddsUpTheOddsForBothSides()
        {
            var t = new FairPlayTally();
            t.Apply(new MatchEvent(MatchEventType.ShotReleased, 0, 0, 0, 0.6f));
            t.Apply(new MatchEvent(MatchEventType.ShotMade, 0, 0, 1));
            t.Apply(new MatchEvent(MatchEventType.ShotReleased, 0, 0, 0, 0.4f));
            t.Apply(new MatchEvent(MatchEventType.ShotMissed, 0, 0));
            t.Apply(new MatchEvent(MatchEventType.ShotReleased, 3, 1, 0, 0.5f));
            t.Apply(new MatchEvent(MatchEventType.ShotMade, 3, 1, 2));
            Assert.AreEqual(2, t.Attempts[0]); Assert.AreEqual(1, t.Makes[0]);
            Assert.AreEqual(1.0f, t.Expected[0], 1e-4f);
            Assert.AreEqual(0.5f, t.Luck(1), 1e-4f);
            string line = t.Line(0, "YOU", "CPU");
            StringAssert.Contains("YOU 1/2", line);
            StringAssert.Contains("CPU 1/1", line);
            StringAssert.Contains("1.0", line);
            StringAssert.Contains("ABOUT WHAT THE ODDS SAID", line);
            Assert.IsNull(new FairPlayTally().Line(0, "YOU", "CPU"), "nobody shot");
        }

        [Test]
        public void I2_FairPlay_CallsAHotNight()
        {
            var t = new FairPlayTally();
            for (int i = 0; i < 6; i++)
            {
                t.Apply(new MatchEvent(MatchEventType.ShotReleased, 3, 1, 0, 0.3f));
                t.Apply(new MatchEvent(MatchEventType.ShotMade, 3, 1, 1));
            }
            StringAssert.Contains("ABOVE THE ODDS", t.Line(0, "YOU", "CPU"));
        }

        [Test]
        public void I2_HouseRules_RoundTripAndApply()
        {
            var cat = DefaultContent.Create();
            var defaults = cat.Find(cat.Rules, DefaultContent.DefaultRulesId);
            var d = HouseRules.From(defaults);
            var plain = HouseRules.Apply(defaults, d);
            Assert.AreEqual(defaults.targetScore, plain.targetScore, "the default choice is the default game");
            Assert.AreEqual(defaults.useGameClock, plain.useGameClock);
            Assert.AreEqual(defaults.insideArcPoints, plain.insideArcPoints);

            var c = new HouseRules.Choice { Target = 0, Clock = 0, Scoring = 1, WinByTwo = 1 };
            var back = HouseRules.Parse(HouseRules.Pack(c), defaults);
            Assert.AreEqual(c.Target, back.Target); Assert.AreEqual(c.Clock, back.Clock);
            Assert.AreEqual(c.Scoring, back.Scoring); Assert.AreEqual(c.WinByTwo, back.WinByTwo);
            var r = HouseRules.Apply(defaults, c);
            Assert.AreEqual(11, r.targetScore);
            Assert.IsFalse(r.useGameClock, "no clock: first to 11");
            Assert.AreEqual(2, r.insideArcPoints); Assert.AreEqual(3, r.beyondArcPoints);
            Assert.IsTrue(r.winByTwo);
            Assert.AreEqual(GameOverReason.None, Scoring.Evaluate(11, 10, 0f, r), "win by two");
            Assert.AreEqual(GameOverReason.TargetScore, Scoring.Evaluate(12, 10, 0f, r));
            Assert.IsFalse(ReferenceEquals(defaults, r), "the catalog rules are never changed");
            Assert.AreEqual(4 * 60f, HouseRules.Apply(defaults, new HouseRules.Choice { Target = 2, Clock = 3 }).gameClockSeconds);

            // Broken or missing codes fall back to the default game.
            foreach (var bad in new[] { null, "", "x|y", "9|9|9|9|9", "a|b|c|d" })
            {
                var p = HouseRules.Parse(bad, defaults);
                Assert.IsTrue(p.Target >= 0 && p.Target < HouseRules.Targets.Length, bad);
            }
            var clamped = HouseRules.Parse("9|-4|7|3", defaults);
            Assert.AreEqual(HouseRules.Targets.Length - 1, clamped.Target);
            Assert.AreEqual(0, clamped.Clock);
            StringAssert.Contains("GAME TO 11", HouseRules.Summary(c));
        }

        [Test]
        public void I2_HouseRules_ReachTheMatch_AndSurviveASave()
        {
            var cat = DefaultContent.Create();
            var rules = HouseRules.Apply(null, new HouseRules.Choice { Target = 1, Clock = 2, Scoring = 0, WinByTwo = 0 });
            var req = new MatchRequest { Mode = GameMode.QuickCall, HomeTeamId = cat.Teams[0].id, AwayTeamId = cat.Teams[1].id, RulesOverride = rules };
            var setup = MatchSetup.FromRequest(req, cat);
            Assert.AreEqual(15, setup.Rules.targetScore);
            Assert.AreEqual(180f, setup.Rules.gameClockSeconds);

            var d = new CareerSaveData();
            d.settings.houseRules = "0|1|1|1";
            var back = SaveCodec.Decode(SaveCodec.Encode(d), cat, out _);
            Assert.AreEqual("0|1|1|1", back.settings.houseRules);
        }
        // ---- I3: gear and legacy

        private static MatchSummary Game(int pts, int fgm, int fga, int steals, int us, int them, GameMode mode = GameMode.QuickCall)
        {
            var s = new MatchSummary { mode = mode, teamAId = "team.a", teamBId = "team.b", scoreA = us, scoreB = them, humanTeam = 0, winner = us > them ? 0 : 1, day = 7, playerOfTheGame = -1 };
            s.lines.Add(new SummaryLine { team = 0, isHuman = true, stats = new PlayerStatLine { points = pts, fieldGoalsMade = fgm, fieldGoalsAttempted = fga, steals = steals } });
            return s;
        }

        [Test]
        public void I3_Moments_KeepTheBigNights()
        {
            var list = MomentsBook.FromGame(Game(22, 6, 6, 4, 21, 8), true, true);
            Assert.AreEqual(5, list.Count);
            foreach (var k in new[] { MomentKind.BuzzerBeater, MomentKind.BigGame, MomentKind.PerfectNight, MomentKind.Lockdown, MomentKind.Blowout })
                Assert.IsTrue(list.Exists(m => m.kind == k), k.ToString());
            Assert.AreEqual("team.b", list[0].opponentId);
            Assert.AreEqual(21, list[0].scoreFor);
            Assert.AreEqual(0, MomentsBook.FromGame(Game(5, 2, 6, 0, 21, 19), false, false).Count, "an ordinary win");
            Assert.AreEqual(MomentKind.GameWinner, MomentsBook.FromGame(Game(5, 2, 6, 0, 21, 19), true, false)[0].kind);
            Assert.AreEqual(0, MomentsBook.FromGame(Game(5, 2, 6, 0, 18, 21), true, false).Count, "no game-winner in a loss");
            Assert.AreEqual(0, MomentsBook.FromGame(Game(25, 9, 9, 5, 21, 2, GameMode.Versus), true, true).Count, "2 player games don't count");
            Assert.AreEqual(0, MomentsBook.FromGame(Game(25, 9, 9, 5, 21, 2, GameMode.Practice), true, true).Count);
            Assert.IsFalse(MomentsBook.FromGame(Game(22, 3, 9, 0, 40, 38, GameMode.FullCourt), false, false).Exists(m => m.kind == MomentKind.BigGame), "Full Court needs 30");
            StringAssert.Contains("BIG GAME: 22 PTS", MomentsBook.Line(list[1], "SPL"));
            StringAssert.Contains("vs SPL 21-8", MomentsBook.Line(list[1], "SPL"));
        }

        [Test]
        public void I3_Moments_NewestFirst_CappedAndSaved()
        {
            var cat = DefaultContent.Create();
            var d = new CareerSaveData();
            for (int i = 0; i < MomentsBook.Keep + 5; i++)
                MomentsBook.Add(d, new System.Collections.Generic.List<MomentEntry> { new MomentEntry { day = i, kind = MomentKind.GameWinner, mode = GameMode.Streets, scoreFor = 21, scoreAgainst = 20 } });
            Assert.AreEqual(MomentsBook.Keep, d.moments.Count);
            Assert.AreEqual(MomentsBook.Keep + 4, d.moments[0].day, "newest first");
            Assert.AreEqual(MomentsBook.Keep, MomentsBook.Count(d, MomentKind.GameWinner));
            var back = SaveCodec.Decode(SaveCodec.Encode(d), cat, out _);
            Assert.AreEqual(MomentsBook.Keep, back.moments.Count);
            Assert.AreEqual(d.moments[0].day, back.moments[0].day);
            Assert.AreEqual(GameMode.Streets, back.moments[0].mode);
            Assert.AreEqual(MomentKind.GameWinner, back.moments[0].kind);
        }

        [Test]
        public void I3_Gear_IsSoldEquippedAndSaved()
        {
            var cat = DefaultContent.Create();
            var gear = cat.Cosmetics.FindAll(x => x.slot == CosmeticSlot.Gear);
            Assert.GreaterOrEqual(gear.Count, 7);
            Assert.AreEqual(1, gear.FindAll(x => x.unlockedByDefault).Count, "one free default (no gear)");
            foreach (var g in gear)
                if (!g.unlockedByDefault) Assert.AreNotEqual(GearParts.None, Gear.Parts(g.id), g.id + " has parts");
            Assert.AreEqual(GearParts.None, Gear.Parts(Gear.NoneId));
            Assert.IsTrue(CityShop.Catalog(cat).Exists(x => x.slot == CosmeticSlot.Gear), "the corner store can stock gear");
            Assert.AreEqual("GEAR", CityShop.SlotName(CosmeticSlot.Gear));
            var d = new CareerSaveData();
            d.ownedCosmetics.Add("cosmetic.gear.goggles"); // equipping needs owning (the load puts unowned gear back to none)
            d.SetEquipped(CosmeticSlot.Gear, "cosmetic.gear.goggles");
            Assert.AreEqual("cosmetic.gear.goggles", d.Equipped(CosmeticSlot.Gear));
            Assert.AreEqual("cosmetic.gear.goggles", SaveCodec.Decode(SaveCodec.Encode(d), cat, out _).equippedGear);
            Assert.IsTrue(ContentValidator.Validate(cat).Errors.Count == 0, "content still valid");
        }

        [Test]
        public void I3_Gear_ChangesOnlyTheLimbsAndFace()
        {
            var cat = DefaultContent.Create();
            var look = cat.Players[0].appearance;
            var plain = KitLook.Classic(RgbColor.FromHex("#1FB5A6"), RgbColor.FromHex("#FF6F59"), RgbColor.White, null, TeamPattern.Solid, null);
            var bare = CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.GenerateSheet(look, plain, 4);
            var sleeveCol = RgbColor.FromHex("#1C1C24");
            var sleeved = plain; sleeved.Gear = GearParts.ArmSleeves | GearParts.LegSleeves; sleeved.GearColor = sleeveCol;
            var s = CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.GenerateSheet(look, sleeved, 4);
            int before = 0, after = 0, jerseyBefore = 0, jerseyAfter = 0;
            for (int i = 0; i < s.Pixels.Length; i++)
            {
                if (bare.Pixels[i].Equals(sleeveCol)) before++;
                if (s.Pixels[i].Equals(sleeveCol)) after++;
                if (bare.Pixels[i].Equals(plain.Jersey)) jerseyBefore++;
                if (s.Pixels[i].Equals(plain.Jersey)) jerseyAfter++;
            }
            Assert.Greater(after, before + 200, "sleeves on arms and legs");
            Assert.AreEqual(jerseyBefore, jerseyAfter, "the jersey is untouched");
            var goggles = plain; goggles.Gear = GearParts.Goggles; goggles.GearColor = RgbColor.FromHex("#FFD166");
            var g = CallerRetroBall.Logic.PixelArt.CharacterSpriteGenerator.GenerateSheet(look, goggles, 4);
            int rims = 0;
            foreach (var px in g.Pixels) if (px.Equals(goggles.GearColor)) rims++;
            Assert.Greater(rims, 40, "goggle rims on the front and side views");
        }
        // ---- I4: tale of the tape

        [Test]
        public void I4_TaleOfTheTape_SeriesDangerManAndKey()
        {
            var hist = new System.Collections.Generic.List<MatchHistoryEntry>
            {
                new MatchHistoryEntry { opponentId = "team.x", scoreFor = 21, scoreAgainst = 18 },
                new MatchHistoryEntry { opponentId = "team.y", scoreFor = 10, scoreAgainst = 21 },
                new MatchHistoryEntry { opponentId = "team.x", scoreFor = 15, scoreAgainst = 21 },
                new MatchHistoryEntry { opponentId = "team.x", scoreFor = 21, scoreAgainst = 9 },
            };
            var s = TaleOfTheTape.Against(hist, "team.x");
            Assert.AreEqual(2, s.Wins); Assert.AreEqual(1, s.Losses);
            Assert.AreEqual(18, s.Last.scoreAgainst, "the newest meeting");
            StringAssert.Contains("2-1", TaleOfTheTape.SeriesLine(s));
            StringAssert.Contains("YOU LEAD", TaleOfTheTape.SeriesLine(s));
            StringAssert.Contains("LAST TIME: W 21-18", TaleOfTheTape.SeriesLine(s));
            Assert.AreEqual("FIRST MEETING", TaleOfTheTape.SeriesLine(TaleOfTheTape.Against(hist, "team.z")));

            var shooter = new PlayerDef { firstName = "Ace", lastName = "Lane", jerseyNumber = 3, attributes = new AttributeSet(50, 95, 50, 40, 40, 60, 60, 60) };
            var big = new PlayerDef { firstName = "Mo", lastName = "", jerseyNumber = 44, attributes = new AttributeSet(60, 30, 30, 50, 70, 40, 60, 40) };
            var roster = new[] { shooter, big };
            Assert.IsTrue(ReferenceEquals(shooter, TaleOfTheTape.DangerMan(roster)), "the best player");
            Assert.AreEqual("KEY: CLOSE OUT ON THEIR SHOOTERS", TaleOfTheTape.Key(roster));
            string card = TaleOfTheTape.Card(hist, "team.x", roster);
            StringAssert.Contains("WATCH: #3 LANE", card);
            Assert.IsNull(TaleOfTheTape.Card(hist, null, roster));
        }

        // ---- I5: storm the court

        [Test]
        public void I5_StormTheCourt_OnlyForBigWins()
        {
            Assert.IsTrue(CourtStorm.Should(true, true, false, 0f), "a championship");
            Assert.IsTrue(CourtStorm.Should(true, false, true, 0f), "a Baller/Legendary CPU");
            Assert.IsTrue(CourtStorm.Should(true, false, false, 5f), "a better roster");
            Assert.IsFalse(CourtStorm.Should(true, false, false, 1f), "an ordinary win");
            Assert.IsFalse(CourtStorm.Should(false, true, true, 9f), "never when you lose");
            Assert.IsTrue(CourtStorm.Tough("difficulty.legendary"));
            Assert.IsFalse(CourtStorm.Tough(DefaultContent.DefaultDifficultyId));
            var shooter = new Vec2(0f, 6f);
            for (int i = 0; i < CourtStorm.MaxFans; i++)
            {
                float d = (CourtStorm.Spot(shooter, i, CourtStorm.MaxFans) - shooter).Magnitude;
                Assert.Greater(d, GameWinnerMob.Radius, "fans stay outside the players' mob");
            }
            Assert.AreEqual(0f, CourtStorm.Progress(0f, 0));
            Assert.AreEqual(1f, CourtStorm.Progress(CourtStorm.Seconds, 4), 1e-4f);
            Assert.LessOrEqual(CourtStorm.Delay + 4 * 0.08f + CourtStorm.RunSeconds, CourtStorm.Seconds - 1f, "time to celebrate once there");
        }

        // ---- I6: earned gear and history

        [Test]
        public void I6_MomentsEarnGear_Once()
        {
            var cat = DefaultContent.Create();
            var d = new CareerSaveData();
            Assert.AreEqual(0, MomentsBook.GrantRewards(d, cat).Count);
            MomentsBook.Add(d, new System.Collections.Generic.List<MomentEntry> { new MomentEntry { kind = MomentKind.BuzzerBeater } });
            var got = MomentsBook.GrantRewards(d, cat);
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual("cosmetic.gear.goggles", got[0].id);
            Assert.IsTrue(d.ownedCosmetics.Contains("cosmetic.gear.goggles"));
            Assert.AreEqual(0, MomentsBook.GrantRewards(d, cat).Count, "only once");
            for (int i = 0; i < 2; i++) MomentsBook.Add(d, new System.Collections.Generic.List<MomentEntry> { new MomentEntry { kind = MomentKind.GameWinner } });
            Assert.AreEqual("cosmetic.gear.vet_package", MomentsBook.GrantRewards(d, cat)[0].id, "three game-winners (buzzer-beaters count)");
            foreach (var r in MomentsBook.Rewards) Assert.IsNotNull(cat.Find(cat.Cosmetics, r.itemId), r.itemId);
        }

        [Test]
        public void I6_History_KeepsEveryMode()
        {
            var cat = DefaultContent.Create();
            var d = new CareerSaveData();
            d.history.Add(new MatchHistoryEntry { mode = GameMode.Streets, opponentId = "x", scoreFor = 21, scoreAgainst = 3 });
            d.history.Add(new MatchHistoryEntry { mode = GameMode.Clutch, opponentId = "y", scoreFor = 2, scoreAgainst = 3 });
            var back = SaveCodec.Decode(SaveCodec.Encode(d), cat, out _);
            Assert.AreEqual(GameMode.Streets, back.history[0].mode);
            Assert.AreEqual(GameMode.Clutch, back.history[1].mode);
        }
    }
}
