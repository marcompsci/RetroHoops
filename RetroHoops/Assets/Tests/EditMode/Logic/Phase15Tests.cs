using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 15: HEAT CHECK, alley-oops, demo mode, secrets, Arcade Ladder, retro FX.</summary>
    public class HeatCheckTests
    {
        private const float Dt = 1f / 60f;

        private static MatchSimulation Shootaround()
        {
            var c = DefaultContent.Create();
            var r = MatchRequest.QuickCallDefault(c);
            r.Seed = 3;
            var setup = MatchSetup.FromRequest(r, c);
            setup.Shot.debugGreenAlwaysMakes = true;
            setup.PassiveOpponents = true;
            setup.KeepPossessionAfterScore = true;
            return new MatchSimulation(setup);
        }

        /// <summary>Waits for the ball, then a green jumper; returns the events seen until the next check.</summary>
        private static List<MatchEvent> GreenJumper(MatchSimulation m)
        {
            var log = new List<MatchEvent>();
            for (int i = 0; i < 600 && !(m.HumanHasBall && m.ChargingIndex < 0 && m.Phase != MatchPhase.DeadBall); i++) { m.Step(Dt, default); log.AddRange(m.Events); }
            m.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
            log.AddRange(m.Events);
            for (int i = 0; i < 120 && m.ChargingIndex >= 0 && m.ChargeMeter < m.Setup.Shot.greenCenter - 0.01f; i++)
            {
                m.Step(Dt, new PlayerInput { ShootHeld = true });
                log.AddRange(m.Events);
            }
            m.Step(Dt, default);
            log.AddRange(m.Events);
            for (int i = 0; i < 600 && m.Phase != MatchPhase.CheckBall; i++) { m.Step(Dt, default); log.AddRange(m.Events); }
            return log;
        }

        [Test]
        public void ThreeStraightMakes_HeatUp_AndAMissCoolsOff()
        {
            var m = Shootaround();
            int me = m.ControlledIndex;
            GreenJumper(m);
            GreenJumper(m);
            Assert.IsFalse(m.IsHeatedUp(me));
            var log = GreenJumper(m);
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.HeatUp && e.PlayerIndex == me), "third make heats up");
            Assert.IsTrue(m.IsHeatedUp(me));

            m.Setup.Shot.debugGreenAlwaysMakes = false;
            m.Setup.Shot.minChance = 0f;
            m.Setup.Shot.maxChance = 0f; // force a miss
            log = new List<MatchEvent>();
            for (int k = 0; k < 3 && m.IsHeatedUp(me); k++) log.AddRange(GreenJumper(m));
            Assert.IsFalse(m.IsHeatedUp(me));
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.HeatEnded && e.PlayerIndex == me));
        }

        [Test]
        public void ScoringOnAHotTeam_CoolsThemOff()
        {
            var m = Shootaround();
            int hot = MatchSimulation.IndexOf(1, 1);
            m.Players[hot].HotStreak = m.Setup.Shot.heatThreshold;
            Assert.IsTrue(m.IsHeatedUp(hot));
            var log = GreenJumper(m);
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.HeatEnded && e.PlayerIndex == hot));
            Assert.IsFalse(m.IsHeatedUp(hot));
        }

        [Test]
        public void Heat_AddsToTheMakeChance()
        {
            var t = ShotTuning.Default;
            var ctx = new ShotContext
            {
                Type = ShotType.MidRange, Distance = 4f, Meter = 0.6f, Shooter = new AttributeSet(),
                NearestDefenderDistance = 2f, NearestDefenderDefense = 50, HotStreak = 3, Stamina01 = 1f,
            };
            float cold = ShotModel.Evaluate(ctx, t).MakeChance;
            ctx.Heated = true;
            float hot = ShotModel.Evaluate(ctx, t).MakeChance;
            Assert.AreEqual(t.heatBonus, hot - cold, 1e-4);
        }

        [Test]
        public void AlwaysHotCode_StartsTheHumanHeated()
        {
            var c = DefaultContent.Create();
            var r = MatchRequest.QuickCallDefault(c);
            r.StartHeated = true;
            var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
            Assert.IsTrue(m.IsHeatedUp(m.ControlledIndex));
            Assert.IsFalse(m.IsHeatedUp(MatchSimulation.IndexOf(1, 0)));
        }
    }

    public class AlleyOopTests
    {
        private const float Dt = 1f / 60f;

        private static MatchSimulation LiveMatch()
        {
            var c = DefaultContent.Create();
            var r = MatchRequest.QuickCallDefault(c);
            r.Seed = 5;
            var setup = MatchSetup.FromRequest(r, c);
            setup.PassiveOpponents = true;
            setup.RosterA[1] = new PlayerDef
            {
                id = "test.finisher", firstName = "Test", lastName = "Finisher", jerseyNumber = 77,
                archetypeId = setup.RosterA[1].archetypeId, attributes = new AttributeSet { finishing = 90, speed = 60, stamina = 80 },
                appearance = setup.RosterA[1].appearance,
            };
            var m = new MatchSimulation(setup);
            for (int i = 0; i < 120 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, default);
            Assert.AreEqual(MatchPhase.Live, m.Phase);
            return m;
        }

        [Test]
        public void PassToATeammateAtTheRim_IsAnAlleyOopDunk()
        {
            var m = LiveMatch();
            var mate = m.Players[1];
            mate.Motion.position = m.Setup.Court.Hoop + new Vec2(0.3f, 1.2f);
            Assert.IsTrue(m.IsAlleyOopTarget(1));
            var aim = (mate.Position - m.Controlled.Position).Normalized;
            Assert.AreEqual(1, m.PreviewPassTarget(aim));

            var log = new List<MatchEvent>();
            m.Step(Dt, new PlayerInput { PassPressed = true, Move = aim });
            log.AddRange(m.Events);
            Assert.IsTrue(m.AlleyOopInFlight, "the pass is a lob");
            float peak = 0f;
            for (int i = 0; i < 240 && !log.Exists(e => e.Type == MatchEventType.AlleyOop); i++)
            {
                m.Players[1].Motion.position = m.Setup.Court.Hoop + new Vec2(0.3f, 1.2f); // hold the cut
                m.Step(Dt, default);
                peak = System.Math.Max(peak, m.Ball.Height);
                log.AddRange(m.Events);
            }
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.AlleyOop && e.PlayerIndex == 1 && e.Value == m.ControlledIndex));
            Assert.Greater(peak, m.Setup.Flow.passHeight + 0.5f, "lobbed above a normal pass");
            Assert.AreEqual(ShotType.Dunk, m.Ball.ShotType);
            Assert.AreEqual(1, m.Stats[1].alleyOops);
        }

        [Test]
        public void TeammateOnThePerimeter_IsNotAnAlleyOop()
        {
            var m = LiveMatch();
            m.Players[1].Motion.position = new Vec2(5f, 6f);
            Assert.IsFalse(m.IsAlleyOopTarget(1));
            Assert.IsFalse(m.IsAlleyOopTarget(m.ControlledIndex), "can't lob to yourself");
            Assert.IsFalse(m.IsAlleyOopTarget(MatchSimulation.IndexOf(1, 0)), "can't lob to the other team");
        }

        [Test]
        public void LowFinisher_CantCatchAnOop()
        {
            var m = LiveMatch();
            m.Players[2].Motion.position = m.Setup.Court.Hoop + new Vec2(-0.3f, 1.2f);
            m.Players[2].Def.attributes.finishing = 30;
            Assert.IsFalse(m.IsAlleyOopTarget(2));
        }
    }

    public class DemoAndFrameRateTests
    {
        private static MatchSimulation Demo(float gameSeconds)
        {
            var c = DefaultContent.Create();
            var r = MatchRequest.QuickCallDefault(c);
            r.Mode = GameMode.Demo;
            r.RulesId = ArcadeEngine.RulesId;
            r.Seed = 11;
            var setup = MatchSetup.FromRequest(r, c);
            setup.Rules.gameClockSeconds = gameSeconds;
            return new MatchSimulation(setup);
        }

        [Test]
        public void DemoGame_IsAllAi_AndFinishesOnItsOwn()
        {
            var m = Demo(60f);
            Assert.IsFalse(m.IsHumanControlled(m.ControlledIndex));
            for (int i = 0; i < 60 * 240 && !m.IsOver; i++) m.Step(1f / 60f, default);
            Assert.IsTrue(m.IsOver);
            Assert.Greater(m.Score[0] + m.Score[1], 0, "the AI scores on both ends");
        }

        [Test]
        public void Match_RunsAt120Hz_TooCompleting()
        {
            var m = Demo(45f);
            for (int i = 0; i < 120 * 240 && !m.IsOver; i++) m.Step(1f / 120f, default);
            Assert.IsTrue(m.IsOver);
            var rec = new ReplayRecorder(m.Players.Length, 2f, 120);
            Assert.AreEqual(240, rec.Capacity);
        }

        [Test]
        public void DemoGames_PayNothing_AndDontCount()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            var s = new MatchSummary { mode = GameMode.Demo, matchId = "demo1", winner = 0, humanTeam = 0, scoreA = 15, scoreB = 3 };
            Assert.AreEqual(0, Rewards.For(s, RewardTuning.Default).signalPoints);
        }
    }

    public class SecretsTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void Content_WithSecrets_StillValidates()
        {
            var report = ContentValidator.Validate(_c);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(2, _c.TeamsInTier(TeamTier.Secret).Count);
            Assert.IsNotNull(_c.Court(DefaultContent.BossCourtId));
            Assert.AreEqual(CourtCircuit.Secret, _c.Court(DefaultContent.SecretCourtId).circuit);
        }

        [Test]
        public void Codes_AreUniqueFourSymbolSequences()
        {
            var seen = new HashSet<string>();
            foreach (var code in Secrets.All)
            {
                Assert.AreEqual(Secrets.CodeLength, code.Sequence.Length, code.Id);
                Assert.IsTrue(seen.Add(code.SequenceText), "duplicate sequence " + code.SequenceText);
                Assert.IsFalse(string.IsNullOrEmpty(code.HintCondition));
            }
            foreach (CodeSymbol s in System.Enum.GetValues(typeof(CodeSymbol)))
            {
                var icon = Secrets.SymbolIcon(s);
                Assert.AreEqual(8, icon.Length);
                foreach (var row in icon) Assert.AreEqual(8, row.Length);
            }
        }

        [Test]
        public void WrongCode_DoesNothing_RightCodeTogglesAndUnlocks()
        {
            var s = new SecretsSaveData();
            Assert.AreEqual(CodeResult.Wrong, Secrets.Enter(s, new[] { CodeSymbol.Ball, CodeSymbol.Ball, CodeSymbol.Ball, CodeSymbol.Ball }, out _));
            Assert.AreEqual(CodeResult.Wrong, Secrets.Enter(s, new[] { CodeSymbol.Star }, out _));
            Assert.AreEqual(0, s.codesFound.Count);

            var big = Secrets.Find(Secrets.BigHeads).Sequence;
            Assert.AreEqual(CodeResult.Toggled, Secrets.Enter(s, big, out var code));
            Assert.AreEqual(Secrets.BigHeads, code.Id);
            Assert.IsTrue(Secrets.IsOn(s, Secrets.BigHeads));
            Secrets.Enter(s, big, out _);
            Assert.IsFalse(Secrets.IsOn(s, Secrets.BigHeads), "entering again switches it off");
            Assert.AreEqual(1, s.codesFound.Count);

            Assert.IsFalse(Secrets.PlayableTeams(_c, s).Exists(t => t.id == DefaultContent.SecretCrewId));
            Assert.AreEqual(CodeResult.Unlocked, Secrets.Enter(s, Secrets.Find(Secrets.CartridgeKids).Sequence, out _));
            Assert.IsTrue(Secrets.PlayableTeams(_c, s).Exists(t => t.id == DefaultContent.SecretCrewId));
            Assert.IsTrue(Secrets.OpponentTeams(_c, s).Exists(t => t.id == DefaultContent.SecretCrewId));

            var home = _c.Team(Secrets.PlayableTeams(_c, s)[0].id);
            Assert.AreEqual(home.homeCourtId, Secrets.CourtFor(home, s, true), "Pixel Void still locked");
            Secrets.Enter(s, Secrets.Find(Secrets.PixelVoid).Sequence, out _);
            Assert.AreEqual(DefaultContent.SecretCourtId, Secrets.CourtFor(home, s, true));
        }

        [Test]
        public void Hints_AreRevealedByPlaying_OnlyOnce()
        {
            var d = Career.New(_c);
            Assert.AreEqual(0, Secrets.RevealHints(d).Count);
            d.king.best = 3;
            d.totals.wins = 10;
            var got = Secrets.RevealHints(d);
            Assert.AreEqual(2, got.Count);
            Assert.AreEqual(0, Secrets.RevealHints(d).Count);
            Assert.IsTrue(d.secrets.hintsRevealed.Contains(Secrets.BigHeads));
        }

        [Test]
        public void Secrets_AndDisplaySettings_SurviveSaveAndLoad()
        {
            var d = Career.New(_c);
            Secrets.Enter(d.secrets, Secrets.Find(Secrets.RainbowBall).Sequence, out _);
            d.secrets.hintsRevealed.Add(Secrets.PixelVoid);
            Secrets.Unlock(d.secrets, DefaultContent.BossTeamId);
            ArcadeEngine.Start(d.secrets.arcade, _c.TeamsInTier(TeamTier.League)[0].id);
            d.secrets.arcade.rung = 3;
            d.settings.crt = 2;
            d.settings.highFrameRate = false;
            d.settings.attractMode = false;

            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out var status);
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.IsTrue(Secrets.IsOn(back.secrets, Secrets.RainbowBall));
            Assert.IsTrue(back.secrets.hintsRevealed.Contains(Secrets.PixelVoid));
            Assert.IsTrue(Secrets.IsUnlocked(back.secrets, DefaultContent.BossTeamId));
            Assert.IsTrue(back.secrets.arcade.active);
            Assert.AreEqual(3, back.secrets.arcade.rung);
            Assert.AreEqual(ArcadeEngine.Continues, back.secrets.arcade.continues);
            Assert.AreEqual(2, back.settings.crt);
            Assert.IsFalse(back.settings.highFrameRate);
            Assert.IsFalse(back.settings.attractMode);
        }

        [Test]
        public void OldSave_WithoutSecrets_LoadsWithDefaults()
        {
            var back = SaveCodec.Decode("{\"version\":1,\"nickname\":\"Old\"}", _c, out _);
            Assert.IsNotNull(back.secrets);
            Assert.IsNotNull(back.secrets.arcade);
            Assert.AreEqual(1, back.settings.crt);
            Assert.IsTrue(back.settings.highFrameRate);
        }

        [Test]
        public void NewBadges_ForTheLadderAndCodes()
        {
            var d = Career.New(_c);
            Assert.IsFalse(Badges.All.Find(b => b.Id == "badge.codes").Earned(d));
            foreach (var code in Secrets.All) Secrets.Enter(d.secrets, code.Sequence, out _);
            Assert.IsTrue(Badges.All.Find(b => b.Id == "badge.codes").Earned(d));
            d.secrets.arcade.clears = 1;
            Assert.IsTrue(Badges.All.Find(b => b.Id == "badge.ladder").Earned(d));
        }
    }

    public class ArcadeLadderTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();
        private string Home => _c.TeamsInTier(TeamTier.League)[0].id;

        private static MatchSummary Result(bool won) => new MatchSummary
        {
            mode = GameMode.Arcade, humanTeam = 0, winner = won ? 0 : 1, scoreA = won ? 15 : 9, scoreB = won ? 9 : 15, matchId = "a",
        };

        [Test]
        public void Ladder_IsFiveLeagueTeamsWeakestFirst_ThenTheBoss()
        {
            var ladder = ArcadeEngine.Ladder(_c, Home);
            Assert.AreEqual(ArcadeEngine.Rungs, ladder.Count);
            Assert.AreEqual(DefaultContent.BossTeamId, ladder[ladder.Count - 1]);
            Assert.IsFalse(ladder.Contains(Home));
            Assert.AreEqual(ladder.Count, new HashSet<string>(ladder).Count, "no repeats");
            for (int i = 1; i < ladder.Count - 1; i++)
                Assert.GreaterOrEqual(ArcadeEngine.TeamOverall(_c, _c.Team(ladder[i])), ArcadeEngine.TeamOverall(_c, _c.Team(ladder[i - 1])));
            Assert.Greater(ArcadeEngine.TeamOverall(_c, _c.Team(DefaultContent.BossTeamId)), ArcadeEngine.TeamOverall(_c, _c.Team(ladder[0])));
        }

        [Test]
        public void NextMatch_ClimbsDifficulty_AndTheBossPlaysOnItsCourt()
        {
            var a = new ArcadeSaveData();
            Assert.IsNull(ArcadeEngine.NextMatch(a, _c));
            ArcadeEngine.Start(a, Home);
            var first = ArcadeEngine.NextMatch(a, _c);
            Assert.AreEqual(GameMode.Arcade, first.Mode);
            Assert.AreEqual("difficulty.rookie", first.DifficultyId);
            Assert.AreEqual(ArcadeEngine.RulesId, first.RulesId);
            a.rung = ArcadeEngine.Rungs - 1;
            var boss = ArcadeEngine.NextMatch(a, _c);
            Assert.AreEqual(DefaultContent.BossTeamId, boss.AwayTeamId);
            Assert.AreEqual(DefaultContent.BossCourtId, boss.CourtId);
            Assert.AreEqual("difficulty.legend", boss.DifficultyId);
            Assert.IsNotNull(MatchSetup.FromRequest(boss, _c).TeamB);
        }

        [Test]
        public void Losses_UseContinues_ThenGameOver()
        {
            var d = Career.New(_c);
            ArcadeEngine.Start(d.secrets.arcade, Home);
            for (int i = 0; i < ArcadeEngine.Continues; i++)
                Assert.AreEqual(ArcadeOutcome.Continue, ArcadeEngine.ApplyResult(d, Result(false), out _));
            Assert.AreEqual(0, d.secrets.arcade.continues);
            Assert.AreEqual(ArcadeOutcome.GameOver, ArcadeEngine.ApplyResult(d, Result(false), out _));
            Assert.IsFalse(d.secrets.arcade.active);
        }

        [Test]
        public void ClearingTheLadder_UnlocksTheBoss_AndPaysOnce()
        {
            var d = Career.New(_c);
            int sp = d.signalPoints;
            ArcadeEngine.Start(d.secrets.arcade, Home);
            for (int i = 0; i < ArcadeEngine.Rungs - 1; i++)
                Assert.AreEqual(ArcadeOutcome.Advanced, ArcadeEngine.ApplyResult(d, Result(true), out _));
            Assert.AreEqual(ArcadeOutcome.Cleared, ArcadeEngine.ApplyResult(d, Result(true), out int bonus));
            Assert.AreEqual(ArcadeEngine.FirstClearBonus, bonus);
            Assert.AreEqual(sp + bonus, d.signalPoints);
            Assert.IsTrue(Secrets.IsUnlocked(d.secrets, DefaultContent.BossTeamId));
            Assert.IsTrue(Secrets.IsUnlocked(d.secrets, DefaultContent.BossCourtId));
            Assert.AreEqual(ArcadeEngine.Rungs, d.secrets.arcade.bestRung);
            Assert.IsTrue(Secrets.RevealHints(d).Exists(x => x.Id == Secrets.PixelVoid));

            ArcadeEngine.Start(d.secrets.arcade, Home);
            d.secrets.arcade.rung = ArcadeEngine.Rungs - 1;
            ArcadeEngine.ApplyResult(d, Result(true), out bonus);
            Assert.AreEqual(ArcadeEngine.ClearBonus, bonus);
        }

        [Test]
        public void ArcadeGames_PayLikeQuickCall()
        {
            var d = Career.New(_c);
            var s = Result(true);
            var q = Result(true);
            q.mode = GameMode.QuickCall;
            Assert.AreEqual(Rewards.For(q, RewardTuning.Default).signalPoints, Rewards.For(s, RewardTuning.Default).signalPoints);
        }
    }

    public class RetroFxTests
    {
        [Test]
        public void HitStop_OnlyForBigMoments_AndOffWithReduceMotion()
        {
            var dunk = new MatchEvent(MatchEventType.ShotMade, 0, 0, 2);
            Assert.AreEqual(HitStop.Dunk, HitStop.For(dunk, ShotType.Dunk, false), 1e-6);
            Assert.AreEqual(0f, HitStop.For(dunk, ShotType.MidRange, false), 1e-6);
            Assert.AreEqual(0f, HitStop.For(dunk, ShotType.Dunk, true), 1e-6);
            Assert.Greater(HitStop.For(new MatchEvent(MatchEventType.AlleyOop, 1, 0), ShotType.Dunk, false), HitStop.Dunk);
            Assert.AreEqual(HitStop.Max, HitStop.Combine(HitStop.GameWinner, 5f), 1e-6);
        }

        [Test]
        public void ScreenWipe_CoversProgressively()
        {
            const int cols = 16, rows = 9;
            int last = -1;
            for (int s = 0; s <= 10; s++)
            {
                float t = s / 10f;
                int n = 0;
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                    {
                        bool now = ScreenWipe.Covered(x, y, cols, rows, t);
                        if (s > 0 && ScreenWipe.Covered(x, y, cols, rows, (s - 1) / 10f)) Assert.IsTrue(now, "never uncovers");
                        if (now) n++;
                    }
                Assert.GreaterOrEqual(n, last);
                last = n;
                if (s == 0) Assert.AreEqual(0, n);
                if (s == 10) Assert.AreEqual(cols * rows, n);
            }
        }

        [Test]
        public void Crt_ScanlinesAndVignette()
        {
            Assert.AreEqual(0, (int)CrtPattern.Scanlines(0).Get(0, 0).a);
            Assert.Greater(CrtPattern.Scanlines(2).Get(0, 0).a, CrtPattern.Scanlines(1).Get(0, 0).a);
            Assert.AreEqual(0, (int)CrtPattern.Scanlines(2).Get(0, 1).a);
            var v = CrtPattern.Vignette(33, 2);
            Assert.AreEqual(0, (int)v.Get(16, 16).a, "clear centre");
            Assert.Greater(v.Get(0, 0).a, 60, "dark corners");
        }

        [Test]
        public void Announcer_CountsSyllables_AndVoicesAreDeterministic()
        {
            Assert.AreEqual(2, Announcer.Syllables("HEAT CHECK!"));
            Assert.AreEqual(3, Announcer.Syllables("ALLEY-OOP!"));
            Assert.AreEqual(0, Announcer.Syllables(""));
            var a = CallerRetroBall.Logic.AudioSynth.Voice("SLAM!");
            var b = CallerRetroBall.Logic.AudioSynth.Voice("SLAM!");
            Assert.Greater(a.Length, 1000);
            Assert.AreEqual(a.Length, b.Length);
            Assert.AreEqual(a[a.Length / 3], b[b.Length / 3]);
            var c = Announcer.Contour("GAME OVER!");
            Assert.Greater(c[c.Count - 1], c[0], "excited lines climb");
        }

        [Test]
        public void Colors_Cycle()
        {
            Assert.AreNotEqual(ArcadeColors.RainbowAt(0f), ArcadeColors.RainbowAt(0.13f));
            Assert.AreEqual(ArcadeColors.RainbowAt(0f), ArcadeColors.RainbowAt(0.75f), "six colours at 8 per second loop every 0.75 s");
            Assert.AreNotEqual(ArcadeColors.FlameAt(0f, 0), ArcadeColors.FlameAt(0.05f, 0));
        }

        [Test]
        public void HeadBottomRow_MatchesTheDrawnChin()
        {
            var look = new AppearanceDef(2, 0, 0, BodyType.Standard, 1);
            var sheet = CharacterSpriteGenerator.GenerateSheet(look, RgbColor.FromHex("#FF0000"), RgbColor.FromHex("#FFFFFF"), RgbColor.FromHex("#0000FF"));
            // 2x art: the chin row (HeadBottomRow at double size) is skin; the row under it is the shaded neck.
            var skin = CharacterSpriteGenerator.SkinTones[2];
            int a = CharacterSpriteGenerator.ArtScale;
            for (int f = 0; f < CharacterSpriteGenerator.IdleFrames; f++)
            {
                CharacterSpriteGenerator.FrameOrigin(CharacterView.Front, f, out int ox, out int oy);
                int row = CharacterSpriteGenerator.HeadBottomRow(look, f) * a;
                int x = ox + CharacterSpriteGenerator.SheetFrameWidth / 2 - 1;
                Assert.AreEqual(skin, sheet.Get(x, oy + row), "frame " + f + " chin");
                Assert.AreNotEqual(skin, sheet.Get(x, oy + row - 1), "frame " + f + " neck");
            }
        }
    }
}
