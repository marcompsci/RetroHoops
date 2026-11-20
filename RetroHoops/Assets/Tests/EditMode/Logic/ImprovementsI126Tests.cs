using System;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>
    /// 19 Nov 2026, improvements I126–I140 (Omari: "I want the players to actually hang from the rim when they dunk, make
    /// the stealing button and all the other buttons feel better to press by spreading the controls out").
    /// </summary>
    public class ImprovementsI126Tests
    {
        private const float Dt = 1f / 60f;

        // ------------------------------------------------------------------ the rim hang

        private static PixelCanvas Sheet(int player = 0)
        {
            var c = DefaultContent.Create();
            return CharacterSpriteGenerator.GenerateSheet(c.Players[player].appearance,
                KitLook.Classic(RgbColor.FromHex("#3355CC"), RgbColor.White, RgbColor.FromHex("#FFD166"), null, TeamPattern.Solid, null), 23);
        }

        private static (int lo, int hi, int left, int right) Bounds(PixelCanvas s, CharacterView v, int frame)
        {
            CharacterSpriteGenerator.FrameOrigin(v, frame, out int ox, out int oy);
            int lo = int.MaxValue, hi = -1, left = int.MaxValue, right = -1;
            for (int y = 0; y < CharacterSpriteGenerator.SheetFrameHeight; y++)
                for (int x = 0; x < CharacterSpriteGenerator.SheetFrameWidth; x++)
                {
                    var p = s.Get(ox + x, oy + y);
                    if (p.a == 0 || p.Equals(CharacterSpriteGenerator.Outline)) continue;
                    lo = Math.Min(lo, y); hi = Math.Max(hi, y);
                    left = Math.Min(left, x); right = Math.Max(right, x);
                }
            return (lo, hi, left, right);
        }

        [Test]
        public void I126_HangFrames_AreAppended_AndMapFromTheirPoses()
        {
            Assert.AreEqual(CharacterSpriteGenerator.PassFrame + 1, CharacterSpriteGenerator.HangFrame);
            Assert.AreEqual(CharacterSpriteGenerator.HangFrame + 1, CharacterSpriteGenerator.HangPullFrame);
            Assert.AreEqual(CharacterSpriteGenerator.FramesPerView - 1, CharacterSpriteGenerator.HangPullFrame);
            Assert.AreEqual(CharacterSpriteGenerator.HangFrame, CharacterSpriteGenerator.FrameFor(PoseFrame.Hang));
            Assert.AreEqual(CharacterSpriteGenerator.HangPullFrame, CharacterSpriteGenerator.FrameFor(PoseFrame.HangPull));
        }

        [Test]
        public void I126_HangFrame_HandsReachTheRimLine_AndTheChinUpIsTwoLower()
        {
            for (int player = 0; player < 4; player++)
            {
                var s = Sheet(player);
                foreach (CharacterView v in Enum.GetValues(typeof(CharacterView)))
                {
                    var shot = Bounds(s, v, CharacterSpriteGenerator.ShootFrame);
                    var hang = Bounds(s, v, CharacterSpriteGenerator.HangFrame);
                    var pull = Bounds(s, v, CharacterSpriteGenerator.HangPullFrame);
                    Assert.AreEqual(shot.hi, hang.hi, "hanging hands are where a raised hand is (on the rim) " + v);
                    if (v != CharacterView.Side)
                    {
                        Assert.Greater(hang.right - hang.left, shot.right - shot.left - 1, "a wide two-hand grip");
                        // The chin-up's hands are lower; the head (not the hands) may then be the top of the frame.
                        Assert.Less(pull.hi, hang.hi + 1);
                    }
                    Assert.AreNotEqual(Bounds(s, v, CharacterSpriteGenerator.DunkFrame), hang, "its own drawing");
                }
            }
        }

        [Test]
        public void I127_MadeDunksHangLonger()
        {
            Assert.GreaterOrEqual(MatchTuning.Default.rimHangSeconds, 1f - 1e-4f);
        }

        [Test]
        public void I127_GrabShake_DiesOut()
        {
            Assert.AreEqual(1, Dunks.GrabShakePx(0f));
            Assert.AreEqual(-1, Dunks.GrabShakePx(0.05f));
            Assert.AreEqual(0, Dunks.GrabShakePx(Dunks.ShakeSeconds));
            Assert.AreEqual(0, Dunks.GrabShakePx(-0.1f));
            for (float t = 0f; t < Dunks.ShakeSeconds; t += 0.01f) Assert.LessOrEqual(Math.Abs(Dunks.GrabShakePx(t)), 1);
        }

        [Test]
        public void I128_ChinUp_KeepsTheHandsOnTheRim_AndOnlyTwoHandStylesDoIt()
        {
            var hoop = new Vec2(0f, 1.6f);
            var start = new Vec2(0.6f, 3.6f);
            const float slamU = 0.35f, hangU = 0.5f;
            const int lift = 30;
            var motion = Dunks.Hang(DunkStyle.TwoHand);
            Assert.IsTrue(motion.PullUp);
            bool pulled = false;
            int lastPullState = -1, toggles = 0;
            for (float u = slamU; u < slamU + hangU; u += 0.002f)
            {
                var f = DunkPath.At(start, hoop, start, u, slamU, hangU, lift, DunkPath.Approach, 0f, motion);
                if (!f.OnRim) continue;
                // The hands: body lift plus reach, minus the chin-up frame's shorter reach, follow the rim.
                int hands = f.LiftPx + f.RimDipPx - (f.Pull ? CharacterSpriteGenerator.PullDropPx : 0);
                int gap = lift - hands;
                Assert.IsTrue(gap >= -1 && gap <= 1, "hands on the rim at " + u + " (gap " + gap + ")");
                if (f.Pull)
                {
                    pulled = true;
                    Assert.AreEqual(0, f.SwingPx, "he pulls up from a still hang");
                    Assert.AreEqual(PoseFrame.HangPull, Dunks.HangPose(DunkStyle.TwoHand, f).Frame);
                }
                else Assert.AreEqual(PoseFrame.Hang, Dunks.HangPose(DunkStyle.TwoHand, f).Frame);
                int state = f.Pull ? 1 : 0;
                if (lastPullState >= 0 && state != lastPullState) toggles++;
                lastPullState = state;
            }
            Assert.IsTrue(pulled, "a chin-up happens");
            Assert.AreEqual(2, toggles, "up once, down once (no flicker)");
            Assert.IsFalse(Dunks.Hang(DunkStyle.Tomahawk).PullUp);
            Assert.IsFalse(Dunks.Hang(DunkStyle.Windmill).PullUp);
            Assert.IsFalse(Dunks.Hang(DunkStyle.Reverse).PullUp);
        }

        [Test]
        public void I128_PowerSlams_SwingWider_ReverseHangsQuietly()
        {
            var hoop = new Vec2(0f, 1.6f);
            var start = new Vec2(0.6f, 3.6f);
            int Max(DunkStyle style)
            {
                int m = 0;
                for (float u = 0.35f; u < 0.85f; u += 0.002f)
                    m = Math.Max(m, Math.Abs(DunkPath.At(start, hoop, start, u, 0.35f, 0.5f, 30, DunkPath.Approach, 0f, Dunks.Hang(style)).SwingPx));
                return m;
            }
            Assert.AreEqual(3, Max(DunkStyle.Tomahawk));
            Assert.AreEqual(3, Max(DunkStyle.Windmill));
            Assert.AreEqual(1, Max(DunkStyle.Reverse));
            Assert.Greater(Max(DunkStyle.Windmill), Max(DunkStyle.Reverse));
        }

        [Test]
        public void I129_LetGo_RimSpringsBack_AndHeLands()
        {
            Assert.AreEqual(1, Dunks.ReleaseDipPx(-0.01f));
            Assert.AreEqual(-1, Dunks.ReleaseDipPx(0.01f), "flips up past level");
            Assert.AreEqual(1, Dunks.ReleaseDipPx(0.08f));
            Assert.AreEqual(0, Dunks.ReleaseDipPx(0.3f), "and settles");
            var hoop = new Vec2(0f, 1.6f);
            var start = new Vec2(0.6f, 3.6f);
            var land = DunkPath.At(start, hoop, start, 0.98f, 0.35f, 0.4f, 30, DunkPath.Approach, 0f, Dunks.Hang(DunkStyle.TwoHand));
            Assert.IsTrue(land.Dropping && land.Landing);
            var early = DunkPath.At(start, hoop, start, 0.77f, 0.35f, 0.4f, 30, DunkPath.Approach, 0f, Dunks.Hang(DunkStyle.TwoHand));
            Assert.IsTrue(early.Dropping);
            Assert.AreEqual(PoseFrame.Hang, Dunks.HangPose(DunkStyle.TwoHand, early).Frame, "lets go from the hang");
        }

        [Test]
        public void I130_MissedDunk_RattlesBriefly()
        {
            Assert.AreNotEqual(0, Dunks.MissRattlePx(0.01f));
            Assert.AreEqual(0, Dunks.MissRattlePx(0.2f));
            Assert.AreEqual(0, Dunks.MissRattlePx(-0.01f));
        }

        [Test]
        public void I127_AMadeDunk_StillHoldsTheDunkerForTheWholeHang()
        {
            var c = DefaultContent.Create();
            bool sawOne = false;
            for (uint seed = 1; seed <= 12 && !sawOne; seed++)
            {
                var req = MatchRequest.QuickCallDefault(c);
                req.Seed = seed;
                var m = new MatchSimulation(MatchSetup.FromRequest(req, c));
                m.Setup.PassiveOpponents = true;
                for (int i = 0; i < 120 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, default);
                var me = m.Players[m.ControlledIndex];
                me.Def.attributes.finishing = 95;
                me.Motion.position = m.Setup.Court.Hoop + new Vec2(0.2f, 1.5f);
                for (int i = 0; i < 180 && !sawOne; i++)
                {
                    m.Step(Dt, m.Ball.Phase == BallPhase.Held ? new PlayerInput { DunkPressed = true } : default);
                    foreach (var e in m.Events)
                        if (e.Type == MatchEventType.ShotMade && m.Ball.ShotType == ShotType.Dunk)
                        {
                            sawOne = true;
                            Assert.GreaterOrEqual(m.Players[e.PlayerIndex].StunnedUntil, m.Time + 0.98f);
                        }
                }
            }
            Assert.IsTrue(sawOne);
        }

        // ------------------------------------------------------------------ the controls

        [Test]
        public void I131_Spread_IsTheDefault_RoomierThanClassic_AndFits()
        {
            var spread = ControlLayout.Default();
            var classic = ControlLayout.Preset(ControlLayout.Classic);
            Assert.AreEqual(0, ControlLayout.Overlapping(spread).Count);
            Assert.AreEqual(0, ControlLayout.TooClose(spread).Count, "nothing too close in the default");
            float minSpread = float.MaxValue, minClassic = float.MaxValue;
            for (int i = 0; i < spread.Length; i++)
                for (int j = i + 1; j < spread.Length; j++)
                {
                    minSpread = Math.Min(minSpread, ControlLayout.Gap(spread[i], spread[j]));
                    minClassic = Math.Min(minClassic, ControlLayout.Gap(classic[i], classic[j]));
                }
            Assert.GreaterOrEqual(minSpread, 85f, "about 100 units between rings");
            Assert.Greater(minSpread, minClassic + 30f);
            Assert.Greater(spread[(int)ControlButton.Dunk].Size, classic[(int)ControlButton.Dunk].Size, "a bigger STEAL/DUNK");
            foreach (var s in spread)
            {
                Assert.GreaterOrEqual(1080f + s.X - s.Size / 2f, 0f);
                Assert.LessOrEqual(s.Y + s.Size / 2f, 1080f, "fits a landscape screen");
                Assert.AreEqual(s.Size, ControlLayout.Clamp(s).Size, 0.01f, "inside the size limits");
            }
            foreach (var s in classic) Assert.AreEqual(s.Size, ControlLayout.Clamp(s).Size, 0.01f, "classic sizes still allowed");
        }

        [Test]
        public void I131_CustomLayouts_KeepTheirArc()
        {
            var slots = ControlLayout.Preset(ControlLayout.Classic);
            slots[0] = new ControlSlot(ControlButton.Shoot, -260f, 260f, 240f);
            string saved = ControlLayout.Serialize(slots, ControlLayout.Classic);
            var back = ControlLayout.Parse(saved, ControlLayout.Classic);
            for (int i = 0; i < slots.Length; i++)
            {
                Assert.AreEqual(slots[i].X, back[i].X, 0.5f);
                Assert.AreEqual(slots[i].Y, back[i].Y, 0.5f);
            }
            Assert.AreEqual(saved, ControlLayout.Clean(saved));
            Assert.AreEqual("0:-260,260,240", ControlLayout.Clean("junk;0:-260,260,240;0:1,2,3;1:a,b,c"));
            Assert.AreEqual("", ControlLayout.Clean(null));
        }

        [Test]
        public void I131_OldSaves_WithACustomLayout_StayClassic_NewOnesGetSpread()
        {
            var c = DefaultContent.Create();
            var fresh = Career.New(c);
            Assert.AreEqual(ControlLayout.Spread, SaveCodec.Decode(SaveCodec.Encode(fresh), c, out _).settings.controlPreset);

            // A save written before I131: a custom layout and no "preset" in the feel block.
            var old = Career.New(c);
            var classic = ControlLayout.Preset(ControlLayout.Classic);
            classic[2] = new ControlSlot(ControlButton.Dunk, -300f, 600f, 200f);
            old.settings.controlLayout = ControlLayout.Serialize(classic, ControlLayout.Classic);
            string json = System.Text.RegularExpressions.Regex.Replace(SaveCodec.Encode(old), ",\\s*\"preset\"\\s*:\\s*\\d+", "");
            StringAssert.DoesNotContain("\"preset\"", json);
            var back = SaveCodec.Decode(json, c, out _);
            Assert.AreEqual(ControlLayout.Classic, back.settings.controlPreset);
            var slots = ControlLayout.Parse(back.settings.controlLayout, back.settings.controlPreset);
            Assert.AreEqual(classic[0].X, slots[0].X, 0.5f, "untouched buttons stay where they were");
            Assert.AreEqual(-300f, slots[2].X, 0.5f);

            // And the choice itself round-trips.
            old.settings.controlPreset = ControlLayout.Classic;
            Assert.AreEqual(ControlLayout.Classic, SaveCodec.Decode(SaveCodec.Encode(old), c, out _).settings.controlPreset);
        }

        [Test]
        public void I132_TouchAreas_GrowIntoTheGaps_ButNeverOverlap()
        {
            foreach (int preset in new[] { ControlLayout.Spread, ControlLayout.Classic })
            {
                var slots = ControlLayout.Preset(preset);
                for (int i = 0; i < slots.Length; i++)
                {
                    float pad = ControlLayout.HitPad(slots, i);
                    Assert.GreaterOrEqual(pad, 0f);
                    Assert.LessOrEqual(pad, slots[i].Size * ControlLayout.MaxHitGrow * 0.5f + 0.01f);
                    for (int j = i + 1; j < slots.Length; j++)
                        Assert.Greater(ControlLayout.Gap(slots[i], slots[j]), pad + ControlLayout.HitPad(slots, j), "touch areas overlap");
                }
            }
            // Spread's rings get real extra room.
            var spread = ControlLayout.Default();
            for (int i = 0; i < spread.Length; i++)
                Assert.GreaterOrEqual(ControlLayout.HitPad(spread, i), spread[i].Size * 0.08f);
            // Two rings on top of each other get no extra area at all.
            var tight = ControlLayout.Default();
            tight[1] = new ControlSlot(ControlButton.Pass, tight[0].X, tight[0].Y, tight[1].Size);
            Assert.AreEqual(0f, ControlLayout.HitPad(tight, 0));
        }

        [Test]
        public void I133_Press_SinksThenSpringsBack()
        {
            Assert.AreEqual(ControlFeel.PressedScale, ControlFeel.PressScale(true, 0f));
            Assert.Less(ControlFeel.PressScale(false, 0f), 1f);
            Assert.AreEqual(ControlFeel.ReboundScale, ControlFeel.PressScale(false, ControlFeel.ReboundUp), 1e-4f);
            Assert.AreEqual(1f, ControlFeel.PressScale(false, ControlFeel.ReboundSettle));
            Assert.AreEqual(1f, ControlFeel.PressScale(false, 5f));
            Assert.Greater(ControlFeel.PressFlash(0f), 0.4f);
            Assert.AreEqual(0f, ControlFeel.PressFlash(0.2f));
        }

        [Test]
        public void I135_Fade_WhenThePlayIsUnderAButton_SolidUnderYourThumb()
        {
            Assert.AreEqual(1f, ControlFeel.FadeAlpha(false, false));
            Assert.AreEqual(ControlFeel.BusyAlpha, ControlFeel.FadeAlpha(true, false));
            Assert.AreEqual(1f, ControlFeel.FadeAlpha(true, true));
            float a = 1f;
            for (int i = 0; i < 30; i++) a = ControlFeel.FadeStep(a, ControlFeel.BusyAlpha, Dt);
            Assert.AreEqual(ControlFeel.BusyAlpha, a, 1e-4f, "fades within half a second");
            Assert.Greater(ControlFeel.FadeStep(1f, ControlFeel.BusyAlpha, Dt), 0.8f, "not a hard blink");
        }

        // ------------------------------------------------------------------ STEAL

        private static MatchSimulation HumanOnDefense(uint seed = 3)
        {
            var c = DefaultContent.Create();
            var req = MatchRequest.QuickCallDefault(c);
            req.Seed = seed;
            var setup = MatchSetup.FromRequest(req, c);
            setup.StartingOffense = 1;
            var m = new MatchSimulation(setup);
            for (int i = 0; i < 60; i++) m.Step(Dt, PlayerInput.None);
            return m;
        }

        [Test]
        public void I134_HumansReachFurther_ThanTheAi()
        {
            var m = HumanOnDefense();
            Assert.AreEqual(m.Setup.Defense.stealRange * MatchSimulation.HumanReach, m.StealReach(m.ControlledIndex), 1e-4f);
            int ai = -1;
            for (int i = 0; i < m.Players.Length; i++) if (!m.IsHumanControlled(i)) { ai = i; break; }
            Assert.AreEqual(m.Setup.Defense.stealRange, m.StealReach(ai), 1e-4f);
        }

        [Test]
        public void I134_JustOutOfTheOldRange_IsNowAReach()
        {
            var m = HumanOnDefense();
            var me = m.Controlled;
            Assert.AreEqual(MatchPhase.Live, m.Phase);
            float d = m.Setup.Defense.stealRange * 1.1f;
            me.Motion.position = m.Ball.Position + new Vec2(0f, d);
            m.TrySteal(me.Index);
            var o = m.LastStealOutcome(me.Index);
            Assert.IsTrue(o == StealOutcome.Stole || o == StealOutcome.Missed, "within a human's reach: " + o);
        }

        [Test]
        public void I134_EarlyPress_IsHeld_AndUsedTheMomentYouCanReach()
        {
            var m = HumanOnDefense();
            m.Setup.PassiveOpponents = true; // the ball stays put: we're checking timing, not the AI
            m.Setup.Defense.stealBase = 0f;
            m.Setup.Defense.stealFromDefense = 0f;
            for (int i = 0; i < 120 && !m.Ball.IsHeld; i++) m.Step(Dt, PlayerInput.None);
            Assert.IsTrue(m.Ball.IsHeld);
            var me = m.Controlled;
            me.Motion.position = m.Holder.Position + new Vec2(0.2f, -0.3f);
            // A press a moment before the reach is back...
            me.StealReadyAt = m.Time + 0.2f;
            me.StunnedUntil = -1f;
            m.Step(Dt, new PlayerInput { DefensePressed = true });
            Assert.IsTrue(m.StealQueued(me.Index));
            Assert.AreEqual(StealOutcome.Waiting, m.LastStealOutcome(me.Index));
            Assert.Greater(m.StealWait01(me.Index), 0f, "the ring shows the wait");
            // ...comes out by itself when it is.
            bool reached = false;
            for (int i = 0; i < 40 && !reached; i++)
            {
                if (m.Holder != null) me.Motion.position = m.Holder.Position + new Vec2(0.2f, -0.3f);
                m.Step(Dt, PlayerInput.None);
                foreach (var e in m.Events) if (e.Type == MatchEventType.StealAttempt && e.PlayerIndex == me.Index) reached = true;
            }
            Assert.IsTrue(reached, "the held press reached on its own");
            Assert.IsFalse(m.StealQueued(me.Index));
        }

        [Test]
        public void I134_ALongWait_IsNotQueued_SoMashingDoesNothing()
        {
            var m = HumanOnDefense();
            var me = m.Controlled;
            me.StealReadyAt = m.Time + 0.9f;
            m.Step(Dt, new PlayerInput { DefensePressed = true });
            Assert.IsFalse(m.StealQueued(me.Index));
        }

        [Test]
        public void I138_StealHints()
        {
            Assert.AreEqual("TOO FAR", ControlFeel.StealHint(StealOutcome.TooFar));
            Assert.AreEqual("GET CLOSER", ControlFeel.StealHint(StealOutcome.Short));
            Assert.AreEqual("", ControlFeel.StealHint(StealOutcome.Stole));
            foreach (StealOutcome o in Enum.GetValues(typeof(StealOutcome)))
            {
                string h = ControlFeel.StealHint(o);
                if (h.Length > 0) Assert.IsTrue(Loc.Has(h), h + " needs Spanish");
            }
            var m = HumanOnDefense();
            var me = m.Controlled;
            me.Motion.position = m.Holder.Position + new Vec2(0f, 8f);
            m.TrySteal(me.Index);
            Assert.AreEqual(StealOutcome.TooFar, m.LastStealOutcome(me.Index));
            me.StunnedUntil = -1f; me.StealReadyAt = 0f;
            me.Motion.position = m.Ball.Position + new Vec2(0f, m.StealReach(me.Index) * 1.2f);
            m.TrySteal(me.Index);
            Assert.AreEqual(StealOutcome.Short, m.LastStealOutcome(me.Index));
        }

        // ------------------------------------------------------------------ words

        [Test]
        public void I139_I140_NewWords_HaveSpanish()
        {
            foreach (var w in new[] { "SPREAD", "CLASSIC", "CONTROL LAYOUT", "TOO CLOSE", "OVERLAPPING", "SIZE", "READY...", "MISSED", Tours.WhatsNew.Body,
                                      "SPREAD gives each button more room so your thumb finds the right one. CLASSIC is the older, tighter arc. Picking one clears CUSTOMIZE CONTROLS." })
                Assert.IsTrue(Loc.Has(w), w);
            Assert.IsTrue(Tours.WhatsNew.Id.StartsWith("tour.whats_new_", System.StringComparison.Ordinal), "a later card (I188) replaced I139's");
        }
    }
}
