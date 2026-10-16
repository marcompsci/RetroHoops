using System;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    public class CourtGeometryTests
    {
        private readonly CourtGeometry _court = CourtGeometry.Default;

        [Test]
        public void Layup_IsInsideArc()
        {
            Assert.AreEqual(ShotZone.InsideArc, _court.ZoneOf(new Vec2(0.5f, 1.8f)));
            Assert.AreEqual(ShotZone.InsideArc, _court.ZoneOf(new Vec2(0f, _court.hoopY + _court.arcRadius - 0.1f)));
        }

        [Test]
        public void TopOfArcAndBeyond_IsTwoPointZone()
        {
            Assert.AreEqual(ShotZone.BeyondArc, _court.ZoneOf(_court.ArcTop));
            Assert.AreEqual(ShotZone.BeyondArc, _court.ZoneOf(_court.CheckSpot));
        }

        [Test]
        public void Corner_UsesStraightLine()
        {
            float y = 1.0f; // below where the corner line meets the arc
            Assert.Less(y, _court.CornerLineTopY);
            Assert.AreEqual(ShotZone.BeyondArc, _court.ZoneOf(new Vec2(_court.CornerLineX + 0.1f, y)));
            Assert.AreEqual(ShotZone.InsideArc, _court.ZoneOf(new Vec2(_court.CornerLineX - 0.1f, y)));
        }

        [Test]
        public void Clamp_KeepsPlayersInsideTheLines()
        {
            var p = _court.Clamp(new Vec2(-50f, 99f));
            Assert.AreEqual(-_court.HalfWidth + _court.boundsInset, p.x, 1e-5);
            Assert.AreEqual(_court.depth - _court.boundsInset, p.y, 1e-5);
        }

        [Test]
        public void CheckSpot_IsOnCourtAndBeyondArc()
        {
            Assert.IsTrue(_court.Contains(_court.CheckSpot));
            Assert.Greater(_court.DistanceToHoop(_court.CheckSpot), _court.arcRadius);
        }
    }

    public class MovementTests
    {
        private readonly MovementTuning _t = MovementTuning.Default;

        [Test]
        public void MaxSpeed_GrowsWithRating_AndDribblingIsSlower()
        {
            float slow = Movement.MaxSpeed(20, false, _t);
            float fast = Movement.MaxSpeed(90, false, _t);
            Assert.Greater(fast, slow);
            Assert.Less(Movement.MaxSpeed(90, true, _t), fast);
        }

        [Test]
        public void DifficultyScale_CanNeverExceedRatings()
        {
            Assert.AreEqual(Movement.MaxSpeed(70, false, _t), Movement.MaxSpeed(70, false, _t, 1.5f), 1e-5);
            Assert.Less(Movement.MaxSpeed(70, false, _t, 0.9f), Movement.MaxSpeed(70, false, _t));
        }

        [Test]
        public void Step_AcceleratesToTopSpeed_ButNotBeyond()
        {
            var s = new MotionState(Vec2.Zero);
            float max = 5f;
            for (int i = 0; i < 120; i++) s = Movement.Step(s, Vec2.Right, max, 1f / 60f, _t);
            Assert.AreEqual(max, s.velocity.Magnitude, 1e-3);
            Assert.Greater(s.position.x, 0f);
        }

        [Test]
        public void Step_FirstFrameIsResponsive_NotInstant()
        {
            var s = Movement.Step(new MotionState(Vec2.Zero), Vec2.Right, 5f, 1f / 60f, _t);
            Assert.Greater(s.velocity.x, 0f);
            Assert.Less(s.velocity.x, 5f);
        }

        [Test]
        public void ReleasingStick_StopsQuickly()
        {
            var s = new MotionState(Vec2.Zero) { velocity = new Vec2(5f, 0f) };
            for (int i = 0; i < 10; i++) s = Movement.Step(s, Vec2.Zero, 5f, 1f / 60f, _t);
            Assert.AreEqual(0f, s.velocity.Magnitude, 1e-4); // 42 m/s² brakes 5 m/s in < 0.17 s
        }

        [Test]
        public void AnalogStick_GivesProportionalSpeed()
        {
            var s = new MotionState(Vec2.Zero);
            for (int i = 0; i < 120; i++) s = Movement.Step(s, new Vec2(0.5f, 0f), 6f, 1f / 60f, _t);
            Assert.AreEqual(3f, s.velocity.Magnitude, 1e-3);
        }

        [TestCase(0f, 1f, Facing8.N)]
        [TestCase(1f, 0f, Facing8.E)]
        [TestCase(0f, -1f, Facing8.S)]
        [TestCase(-1f, 0f, Facing8.W)]
        [TestCase(1f, 1f, Facing8.NE)]
        [TestCase(-1f, -1f, Facing8.SW)]
        public void FacingOf_MapsEightDirections(float x, float y, Facing8 expected)
        {
            Assert.AreEqual(expected, Movement.FacingOf(new Vec2(x, y)));
        }

        [Test]
        public void Facing_IsKeptWhenStopped()
        {
            var s = new MotionState(Vec2.Zero, Facing8.W);
            s = Movement.Step(s, Vec2.Zero, 5f, 1f / 60f, _t);
            Assert.AreEqual(Facing8.W, s.facing);
        }

        [Test]
        public void ArriveInput_SlowsNearTarget_AndStops()
        {
            Assert.AreEqual(1f, Movement.ArriveInput(Vec2.Zero, new Vec2(5f, 0f)).Magnitude, 1e-5);
            Assert.Less(Movement.ArriveInput(Vec2.Zero, new Vec2(0.6f, 0f)).Magnitude, 1f);
            Assert.AreEqual(Vec2.Zero, Movement.ArriveInput(Vec2.Zero, new Vec2(0.05f, 0f)));
        }

        [Test]
        public void Separate_PushesOverlappingBodiesApart_EvenWhenCoincident()
        {
            var court = CourtGeometry.Default;
            var p = new[] { new Vec2(0f, 5f), new Vec2(0f, 5f), new Vec2(0.2f, 5f) };
            Movement.Separate(p, _t.bodyRadius, court, 8);
            for (int i = 0; i < p.Length; i++)
                for (int j = i + 1; j < p.Length; j++)
                    Assert.GreaterOrEqual(Vec2.Distance(p[i], p[j]), _t.bodyRadius * 2f - 0.02f, i + "-" + j);
        }
    }

    public class BallTests
    {
        private readonly BallTuning _t = BallTuning.Default;
        private readonly CourtGeometry _court = CourtGeometry.Default;

        [Test]
        public void DribbleHeight_StaysBetweenFloorAndTop()
        {
            for (float time = 0f; time < 2f; time += 0.01f)
            {
                float h = BallPhysics.DribbleHeight(time, _t);
                Assert.GreaterOrEqual(h, _t.dribbleFloorHeight - 1e-4f);
                Assert.LessOrEqual(h, _t.dribbleTopHeight + 1e-4f);
            }
        }

        [Test]
        public void LooseBall_SettlesOnTheFloor_AndStops()
        {
            var b = new BallState { Phase = BallPhase.Loose, Position = new Vec2(0f, 5f), Velocity = new Vec2(3f, 0f), Height = 2f };
            for (int i = 0; i < 60 * 6; i++) BallPhysics.StepLoose(b, 1f / 60f, _court, _t);
            Assert.AreEqual(0f, b.Height, 1e-4);
            Assert.AreEqual(0f, b.Velocity.Magnitude, 1e-4);
            Assert.IsTrue(_court.Contains(b.Position));
        }

        [Test]
        public void LooseBall_BouncesOffTheLines()
        {
            var b = new BallState { Phase = BallPhase.Loose, Position = new Vec2(7f, 5f), Velocity = new Vec2(8f, 0f) };
            for (int i = 0; i < 30; i++) BallPhysics.StepLoose(b, 1f / 60f, _court, _t);
            Assert.LessOrEqual(b.Position.x, _court.HalfWidth);
            Assert.Less(b.Velocity.x, 0f);
        }

        [Test]
        public void Pickup_RequiresDistanceAndLowHeight()
        {
            var b = new BallState { Phase = BallPhase.Loose, Position = new Vec2(0f, 5f), Height = 0.5f };
            Assert.IsTrue(BallPhysics.CanPickUp(b, new Vec2(0.3f, 5f), _t));
            Assert.IsFalse(BallPhysics.CanPickUp(b, new Vec2(2f, 5f), _t));
            b.Height = 2.5f;
            Assert.IsFalse(BallPhysics.CanPickUp(b, new Vec2(0.3f, 5f), _t));
            b.Height = 0.5f;
            b.Phase = BallPhase.Held;
            Assert.IsFalse(BallPhysics.CanPickUp(b, new Vec2(0.3f, 5f), _t));
        }
    }

    public class MatchSimulationTests
    {
        private static MatchSimulation NewMatch()
        {
            var c = DefaultContent.Create();
            var setup = MatchSetup.FromRequest(MatchRequest.QuickCallDefault(c), c);
            return new MatchSimulation(setup);
        }

        [Test]
        public void Setup_UsesThreePlayersPerSide_FromEachRoster()
        {
            var c = DefaultContent.Create();
            var req = MatchRequest.QuickCallDefault(c);
            var setup = MatchSetup.FromRequest(req, c);
            Assert.AreEqual(3, setup.RosterA.Count);
            Assert.AreEqual(3, setup.RosterB.Count);
            Assert.AreEqual(c.Team(req.HomeTeamId).rosterPlayerIds[0], setup.RosterA[0].id);
        }

        [Test]
        public void Start_IsZeroZero_HomeBallAtCheckSpot_HumanHasIt()
        {
            var m = NewMatch();
            Assert.AreEqual(0, m.Score[0]);
            Assert.AreEqual(0, m.Score[1]);
            Assert.AreEqual(0, m.OffenseTeam);
            Assert.AreEqual(0, m.HolderIndex);
            Assert.AreEqual(0, m.ControlledIndex);
            Assert.IsTrue(m.Controlled.IsHuman);
            Assert.IsTrue(m.Holder.Position.ApproximatelyEquals(m.Setup.Court.CheckSpot));
            Assert.AreEqual(m.Setup.Rules.shotClockSeconds, m.ShotClock, 1e-5);
            Assert.AreEqual(m.Setup.Rules.gameClockSeconds, m.GameClock, 1e-5);
        }

        [Test]
        public void Defenders_StartBetweenTheirManAndTheHoop()
        {
            var m = NewMatch();
            var hoop = m.Setup.Court.Hoop;
            for (int slot = 0; slot < MatchSimulation.PlayersPerTeam; slot++)
            {
                var man = m.Players[MatchSimulation.IndexOf(0, slot)];
                var d = m.Players[MatchSimulation.IndexOf(1, slot)];
                Assert.Less(Vec2.Distance(d.Position, hoop), Vec2.Distance(man.Position, hoop), "slot " + slot);
            }
        }

        [Test]
        public void HumanInput_MovesTheBallHandler_AndTheBallFollows()
        {
            var m = NewMatch();
            var start = m.Controlled.Position;
            for (int i = 0; i < 30; i++) m.Step(1f / 60f, new PlayerInput { Move = new Vec2(-1f, 0f) });
            Assert.Less(m.Controlled.Position.x, start.x - 0.5f);
            Assert.AreEqual(0, m.HolderIndex);
            Assert.Less(Vec2.Distance(m.Ball.Position, m.Controlled.Position), 0.6f);
            Assert.AreEqual(Facing8.W, m.Controlled.Motion.facing);
        }

        [Test]
        public void Clocks_AreFrozenDuringCheckBall_AndRunWhenLive()
        {
            var m = NewMatch();
            Assert.AreEqual(MatchPhase.CheckBall, m.Phase);
            m.Step(1f / 60f, PlayerInput.None);
            Assert.AreEqual(m.Setup.Rules.gameClockSeconds, m.GameClock, 1e-5);
            for (int i = 0; i < 60; i++) m.Step(1f / 60f, PlayerInput.None); // check phase times out (0.8 s)
            Assert.AreEqual(MatchPhase.Live, m.Phase);
            float clock = m.GameClock;
            for (int i = 0; i < 60; i++) m.Step(1f / 60f, PlayerInput.None);
            Assert.AreEqual(clock - 1f, m.GameClock, 0.02);
        }

        [Test]
        public void PlayersNeverLeaveTheCourt_OrOverlap()
        {
            var m = NewMatch();
            for (int i = 0; i < 600; i++) m.Step(1f / 60f, new PlayerInput { Move = new Vec2(1f, 1f) });
            var court = m.Setup.Court;
            foreach (var p in m.Players) Assert.IsTrue(court.Contains(p.Position), p.Def.id);
            for (int i = 0; i < m.Players.Length; i++)
                for (int j = i + 1; j < m.Players.Length; j++)
                    Assert.GreaterOrEqual(Vec2.Distance(m.Players[i].Position, m.Players[j].Position), 0.6f);
        }

        [Test]
        public void LooseBall_PickedUpByDefense_ChangesPossession_AndControl()
        {
            var m = NewMatch();
            for (int i = 0; i < 60; i++) m.Step(1f / 60f, PlayerInput.None); // past the check-ball freeze
            // Drop the ball right on top of the human's defender.
            var defender = m.Players[MatchSimulation.IndexOf(1, 0)];
            m.KnockLoose(Vec2.Zero, 0f);
            m.Ball.Position = defender.Position;
            m.Ball.Height = 0.2f;
            // Freeze the offense so only the defender can reach it.
            for (int i = 0; i < 3; i++) m.Players[i].Motion.position = m.Players[i].Position + new Vec2(0f, 3f);

            bool changed = false;
            for (int i = 0; i < 30 && !changed; i++)
            {
                m.Step(1f / 60f, PlayerInput.None);
                changed = m.Events.Exists(e => e.Type == MatchEventType.PossessionChanged);
            }
            Assert.IsTrue(changed, "No possession change");
            Assert.AreEqual(1, m.OffenseTeam);
            Assert.AreEqual(1, m.Players[m.HolderIndex].Team);
            Assert.AreEqual(0, m.Controlled.Team, "Human should keep controlling a team-0 player on defense");
        }

        [Test]
        public void CheckBall_ResetsShotClockAndPlacesOffense()
        {
            var m = NewMatch();
            m.ClocksRunning = true;
            for (int i = 0; i < 120; i++) m.Step(1f / 60f, PlayerInput.None);
            m.CheckBall(1);
            Assert.AreEqual(1, m.OffenseTeam);
            Assert.AreEqual(MatchSimulation.IndexOf(1, 0), m.HolderIndex);
            Assert.AreEqual(m.Setup.Rules.shotClockSeconds, m.ShotClock, 1e-5);
            Assert.AreEqual(0, m.Controlled.Team);
        }

        [Test]
        public void Simulation_IsDeterministic()
        {
            var a = NewMatch();
            var b = NewMatch();
            var input = new PlayerInput { Move = new Vec2(0.7f, -0.4f) };
            for (int i = 0; i < 300; i++)
            {
                a.Step(1f / 60f, input);
                b.Step(1f / 60f, input);
            }
            for (int i = 0; i < a.Players.Length; i++) Assert.AreEqual(a.Players[i].Position, b.Players[i].Position);
        }

        [Test]
        public void PracticeRequest_BuildsAMatch()
        {
            var c = DefaultContent.Create();
            var m = new MatchSimulation(MatchSetup.FromRequest(MatchRequest.PracticeDefault(), c));
            Assert.AreEqual(6, m.Players.Length);
            Assert.AreEqual(0, m.HolderIndex);
        }
    }

    public class InputTests
    {
        [Test]
        public void Joystick_DeadZoneReadsZero()
        {
            Assert.AreEqual(Vec2.Zero, JoystickMath.Evaluate(new Vec2(5f, 0f), 100f));
        }

        [Test]
        public void Joystick_FullDeflection_IsOne_AndClamped()
        {
            Assert.AreEqual(1f, JoystickMath.Evaluate(new Vec2(100f, 0f), 100f).Magnitude, 1e-5);
            Assert.AreEqual(1f, JoystickMath.Evaluate(new Vec2(0f, -400f), 100f).Magnitude, 1e-5);
        }

        [Test]
        public void Joystick_JustPastDeadZone_IsSmall()
        {
            float m = JoystickMath.Evaluate(new Vec2(15f, 0f), 100f).Magnitude;
            Assert.Greater(m, 0f);
            Assert.Less(m, 0.1f);
        }

        [Test]
        public void KnobOffset_IsClampedToRadius()
        {
            Assert.AreEqual(80f, JoystickMath.KnobOffset(new Vec2(300f, 0f), 80f).Magnitude, 1e-4);
        }

        [Test]
        public void InputBuffer_AcceptsPressWithinWindow_Once()
        {
            var b = new InputBuffer(0.15f);
            b.Press(ActionButton.Pass, 1.00f);
            Assert.IsTrue(b.IsBuffered(ActionButton.Pass, 1.10f));
            Assert.IsTrue(b.Consume(ActionButton.Pass, 1.10f));
            Assert.IsFalse(b.Consume(ActionButton.Pass, 1.11f));
        }

        [Test]
        public void InputBuffer_ExpiresOldPresses()
        {
            var b = new InputBuffer(0.15f);
            b.Press(ActionButton.Shoot, 1.0f);
            Assert.IsFalse(b.Consume(ActionButton.Shoot, 1.3f));
        }
    }

    public class CameraMathTests
    {
        [Test]
        public void Follow_ConvergesAndIsFrameRateIndependent()
        {
            var a = Vec2.Zero;
            var b = Vec2.Zero;
            var target = new Vec2(10f, 0f);
            for (int i = 0; i < 60; i++) a = CameraMath.Follow(a, target, 1f / 60f, 6f);
            for (int i = 0; i < 30; i++) b = CameraMath.Follow(b, target, 1f / 30f, 6f);
            Assert.AreEqual(a.x, b.x, 1e-3);
            Assert.Greater(a.x, 9f);
        }

        [Test]
        public void ClampView_KeepsViewInsideArea()
        {
            var c = CameraMath.ClampView(new Vec2(100f, 0f), 5f, 3f, new Vec2(-7.5f, -11f), new Vec2(7.5f, 0f));
            Assert.AreEqual(2.5f, c.x, 1e-5);
            Assert.AreEqual(-3f, c.y, 1e-5);
        }

        [Test]
        public void ClampView_CentresWhenViewIsLargerThanArea()
        {
            var c = CameraMath.ClampView(new Vec2(3f, 3f), 20f, 20f, new Vec2(-7.5f, -11f), new Vec2(7.5f, 0f));
            Assert.AreEqual(0f, c.x, 1e-5);
            Assert.AreEqual(-5.5f, c.y, 1e-5);
        }

        [TestCase(1170, 12f, 16f, 6)]
        [TestCase(750, 12f, 16f, 3)]
        [TestCase(100, 12f, 16f, 1)]
        public void IntegerZoom_FitsMinimumVisibleWidth(int screen, float units, float ppu, int expected)
        {
            int z = CameraMath.IntegerZoom(screen, units, ppu);
            Assert.AreEqual(expected, z);
            if (z > 1) Assert.GreaterOrEqual(screen / (z * ppu), units - 1e-3f);
        }
    }
}

namespace CallerRetroBall.Tests
{
    using CallerRetroBall.Logic.PixelArt;

    public class Phase2ArtTests
    {
        [Test]
        public void CourtTexture_HasExpectedSize_AndIsFullyOpaque()
        {
            var g = CourtGeometry.Default;
            var court = DefaultContent.Create().Court("court.sunset_cage");
            var tex = CourtGenerator.Generate(court, g, 5);
            Assert.AreEqual(CourtGenerator.TextureWidth(g), tex.Width);
            Assert.AreEqual(CourtGenerator.TextureHeight(g), tex.Height);
            Assert.AreEqual(tex.Width * tex.Height, tex.OpaqueCount());
        }

        [Test]
        public void CourtToPixel_PutsBaselineNearTop_AndHoopBelowIt()
        {
            var g = CourtGeometry.Default;
            CourtGenerator.CourtToPixel(g, Vec2.Zero, out float bx, out float by);
            CourtGenerator.CourtToPixel(g, g.Hoop, out _, out float hy);
            CourtGenerator.CourtToPixel(g, new Vec2(0f, g.depth), out _, out float ty);
            Assert.AreEqual(CourtGenerator.TextureWidth(g) / 2f, bx, 0.5);
            Assert.Greater(by, hy);
            Assert.Greater(hy, ty);
            Assert.AreEqual(CourtGenerator.TopMargin * CourtGenerator.PixelsPerMeter, ty, 1e-3);
        }

        [Test]
        public void CourtTexture_IsDeterministic()
        {
            var g = CourtGeometry.Default;
            var court = DefaultContent.Create().Court("court.volt_box");
            var a = CourtGenerator.Generate(court, g, 9);
            var b = CourtGenerator.Generate(court, g, 9);
            for (int i = 0; i < a.Pixels.Length; i++) Assert.AreEqual(a.Pixels[i], b.Pixels[i]);
        }

        [Test]
        public void EveryPlayer_GetsACompleteSpriteSheet()
        {
            var c = DefaultContent.Create();
            foreach (var p in c.Players)
            {
                var sheet = CharacterSpriteGenerator.GenerateSheet(p.appearance, RgbColor.FromHex("#FFD400"), RgbColor.FromHex("#2B2B2E"), RgbColor.FromHex("#4B1F7A"));
                Assert.AreEqual(CharacterSpriteGenerator.SheetFrameWidth * CharacterSpriteGenerator.FramesPerView, sheet.Width);
                Assert.AreEqual(CharacterSpriteGenerator.SheetFrameHeight * CharacterSpriteGenerator.ViewCount, sheet.Height);
                for (int v = 0; v < CharacterSpriteGenerator.ViewCount; v++)
                    for (int f = 0; f < CharacterSpriteGenerator.FramesPerView; f++)
                    {
                        CharacterSpriteGenerator.FrameOrigin((CharacterView)v, f, out int ox, out int oy);
                        int opaque = 0;
                        bool feetOnFloor = false;
                        for (int y = 0; y < CharacterSpriteGenerator.SheetFrameHeight; y++)
                            for (int x = 0; x < CharacterSpriteGenerator.SheetFrameWidth; x++)
                                if (sheet.Get(ox + x, oy + y).a > 0)
                                {
                                    opaque++;
                                    if (y <= CharacterSpriteGenerator.ArtScale) feetOnFloor = true;
                                }
                        Assert.Greater(opaque, 80 * CharacterSpriteGenerator.ArtScale * CharacterSpriteGenerator.ArtScale, p.id + " view " + v + " frame " + f);
                        // G2 (16 Oct 2026): the dunk frame is drawn airborne on purpose (knees tucked); every other frame stands on the floor.
                        if (f != CharacterSpriteGenerator.DunkFrame) Assert.IsTrue(feetOnFloor, p.id + " frame floats above the floor");
                    }
            }
        }

        [Test]
        public void Sprites_HaveDarkOutlines()
        {
            var look = new AppearanceDef(2, 1, 0, BodyType.Standard, 1);
            var sheet = CharacterSpriteGenerator.GenerateSheet(look, RgbColor.White, RgbColor.White, RgbColor.White);
            int outline = 0;
            foreach (var px in sheet.Pixels) if (px == CharacterSpriteGenerator.Outline) outline++;
            Assert.Greater(outline, 200);
        }

        [TestCase(Facing8.N, CharacterView.Front, false)]
        [TestCase(Facing8.S, CharacterView.Back, false)]
        [TestCase(Facing8.E, CharacterView.Side, false)]
        [TestCase(Facing8.W, CharacterView.Side, true)]
        [TestCase(Facing8.SW, CharacterView.Back, true)]
        public void ViewFor_MapsFacing(Facing8 facing, CharacterView view, bool flip)
        {
            Assert.AreEqual(view, CharacterSpriteGenerator.ViewFor(facing, out bool f));
            Assert.AreEqual(flip, f);
        }

        [Test]
        public void Props_AreNotEmpty()
        {
            Assert.Greater(PropSpriteGenerator.Ball().OpaqueCount(), 40);
            Assert.Greater(PropSpriteGenerator.Shadow().OpaqueCount(), 20);
            Assert.Greater(PropSpriteGenerator.Ring().OpaqueCount(), 20);
            Assert.Greater(PropSpriteGenerator.Hoop().OpaqueCount(), 300);
        }
    }
}
