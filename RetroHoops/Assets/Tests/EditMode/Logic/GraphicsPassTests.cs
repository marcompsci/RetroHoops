using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>16 Oct 2026 graphics phases G1-G3: character detail pass, action frames, gameplay effects.</summary>
    public class GraphicsPassTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        private static readonly RgbColor Jersey = RgbColor.FromHex("#1FB5A6");
        private static readonly RgbColor Trim = RgbColor.FromHex("#FF6F59");

        private PixelCanvas Sheet(int player = 0, int number = 23) =>
            CharacterSpriteGenerator.GenerateSheet(_c.Players[player].appearance, KitLook.Classic(Jersey, Trim, RgbColor.White, null, TeamPattern.Solid, null), number);

        private static int CountIn(PixelCanvas s, CharacterView v, int frame, RgbColor col)
        {
            CharacterSpriteGenerator.FrameOrigin(v, frame, out int ox, out int oy);
            int n = 0;
            for (int y = 0; y < CharacterSpriteGenerator.SheetFrameHeight; y++)
                for (int x = 0; x < CharacterSpriteGenerator.SheetFrameWidth; x++)
                    if (s.Get(ox + x, oy + y).Equals(col)) n++;
            return n;
        }

        /// <summary>Opaque bounds of one frame: lowest row, highest row, leftmost and rightmost column.</summary>
        private static (int lo, int hi, int left, int right) Bounds(PixelCanvas s, CharacterView v, int frame)
        {
            CharacterSpriteGenerator.FrameOrigin(v, frame, out int ox, out int oy);
            int lo = int.MaxValue, hi = -1, left = int.MaxValue, right = -1;
            for (int y = 0; y < CharacterSpriteGenerator.SheetFrameHeight; y++)
                for (int x = 0; x < CharacterSpriteGenerator.SheetFrameWidth; x++)
                {
                    var p = s.Get(ox + x, oy + y);
                    if (p.a == 0 || p.Equals(CharacterSpriteGenerator.Outline)) continue;
                    lo = System.Math.Min(lo, y); hi = System.Math.Max(hi, y);
                    left = System.Math.Min(left, x); right = System.Math.Max(right, x);
                }
            return (lo, hi, left, right);
        }

        private static bool Same(PixelCanvas s, CharacterView v, int a, int b)
        {
            CharacterSpriteGenerator.FrameOrigin(v, a, out int ax, out int ay);
            CharacterSpriteGenerator.FrameOrigin(v, b, out int bx, out int by);
            for (int y = 0; y < CharacterSpriteGenerator.SheetFrameHeight; y++)
                for (int x = 0; x < CharacterSpriteGenerator.SheetFrameWidth; x++)
                    if (!s.Get(ax + x, ay + y).Equals(s.Get(bx + x, by + y))) return false;
            return true;
        }

        // ---- G1: character detail pass

        [Test]
        public void G1_CrewSocksShowAboveTheShoes()
        {
            var sock = new RgbColor(0xE4, 0xE6, 0xEC);
            var s = Sheet();
            Assert.Greater(CountIn(s, CharacterView.Front, 0, sock), 0, "socks on the idle frame");
            Assert.Greater(CountIn(s, CharacterView.Side, 0, sock), 0);
        }

        [Test]
        public void G1_FrontFacesHaveEyeWhites()
        {
            var white = new RgbColor(0xF4, 0xF4, 0xF0);
            for (int i = 0; i < 6; i++)
                Assert.Greater(CountIn(Sheet(i), CharacterView.Front, 0, white), 0, "player " + i);
        }

        [Test]
        public void G1_NumbersHaveADropShadow()
        {
            var drop = Jersey.Darken(0.32f);
            Assert.Greater(CountIn(Sheet(), CharacterView.Back, 0, drop), 0, "twill shadow under the back number");
            Assert.AreEqual(0, CountIn(Sheet(0, -1), CharacterView.Back, 0, drop), "no number, no shadow");
        }

        [Test]
        public void G1_ArmsAreSeparatedFromTheJersey()
        {
            var look = _c.Players[0].appearance;
            var skin = CharacterSpriteGenerator.SkinTones[((look.skinTone % 6) + 6) % 6];
            var seam = SpriteDetail.Mix(CharacterSpriteGenerator.Outline, skin.Darken(0.55f), 0.35f);
            var s = Sheet();
            Assert.Greater(CountIn(s, CharacterView.Front, 0, seam), 3, "a seam down each arm (front)");
            Assert.Greater(CountIn(s, CharacterView.Back, 0, seam), 3, "and from behind");
        }

        [Test]
        public void G1_SheetIsDeterministic()
        {
            var a = Sheet(3);
            var b = Sheet(3);
            for (int i = 0; i < a.Pixels.Length; i++) Assert.AreEqual(a.Pixels[i], b.Pixels[i]);
        }

        // ---- G2: action frames

        [Test]
        public void G2_ActionFramesAreAppended_OldIndicesUnchanged()
        {
            Assert.AreEqual(6, CharacterSpriteGenerator.ShootFrame);
            Assert.AreEqual(7, CharacterSpriteGenerator.CrossoverFrame);
            Assert.AreEqual(8, CharacterSpriteGenerator.StepBackFrame);
            Assert.AreEqual(9, CharacterSpriteGenerator.ChestThumpFrame);
            Assert.AreEqual(10, CharacterSpriteGenerator.DefenseFrame);
            Assert.AreEqual(11, CharacterSpriteGenerator.SlideFrame);
            Assert.AreEqual(12, CharacterSpriteGenerator.DunkFrame);
            Assert.AreEqual(13, CharacterSpriteGenerator.PassFrame);
            Assert.AreEqual(14, CharacterSpriteGenerator.FramesPerView);
            Assert.AreEqual(CharacterSpriteGenerator.DefenseFrame, CharacterSpriteGenerator.FrameFor(PoseFrame.Defense));
            Assert.AreEqual(CharacterSpriteGenerator.SlideFrame, CharacterSpriteGenerator.FrameFor(PoseFrame.Slide));
            Assert.AreEqual(CharacterSpriteGenerator.DunkFrame, CharacterSpriteGenerator.FrameFor(PoseFrame.Dunk));
            Assert.AreEqual(CharacterSpriteGenerator.PassFrame, CharacterSpriteGenerator.FrameFor(PoseFrame.Pass));
        }

        [Test]
        public void G2_EveryActionFrameIsItsOwnDrawing()
        {
            var s = Sheet();
            int[] action = { CharacterSpriteGenerator.DefenseFrame, CharacterSpriteGenerator.SlideFrame, CharacterSpriteGenerator.DunkFrame, CharacterSpriteGenerator.PassFrame };
            for (int v = 0; v < CharacterSpriteGenerator.ViewCount; v++)
                foreach (int f in action)
                    for (int g = 0; g < f; g++)
                        if (!(v == (int)CharacterView.Side && f == CharacterSpriteGenerator.SlideFrame && g == CharacterSpriteGenerator.DefenseFrame))
                            Assert.IsFalse(Same(s, (CharacterView)v, f, g), "view " + v + " frame " + f + " repeats frame " + g);
        }

        [Test]
        public void G2_DefenseIsWiderThanIdle_AndDunkTucksTheFeet()
        {
            var s = Sheet();
            var idle = Bounds(s, CharacterView.Front, 0);
            var d = Bounds(s, CharacterView.Front, CharacterSpriteGenerator.DefenseFrame);
            Assert.GreaterOrEqual(d.right - d.left, idle.right - idle.left, "hands out wide");
            Assert.Greater(d.hi, idle.hi - 4, "hands up near the head");
            var dunk = Bounds(s, CharacterView.Front, CharacterSpriteGenerator.DunkFrame);
            Assert.Greater(dunk.lo, idle.lo, "feet off the floor (knees tucked)");
            var shot = Bounds(s, CharacterView.Front, CharacterSpriteGenerator.ShootFrame);
            Assert.AreEqual(shot.hi, dunk.hi, "the dunk hand reaches as high as the shot (hands on the rim line up)");
            Assert.AreEqual(CharacterSpriteGenerator.HeadBottomRow(_c.Players[0].appearance, CharacterSpriteGenerator.ShootFrame),
                            CharacterSpriteGenerator.HeadBottomRow(_c.Players[0].appearance, CharacterSpriteGenerator.DunkFrame));
        }

        [Test]
        public void G2_PassFrameKeepsTheBackNumber()
        {
            var s = Sheet();
            Assert.Greater(CountIn(s, CharacterView.Back, CharacterSpriteGenerator.PassFrame, Trim), 0, "back number still shows");
        }

        [Test]
        public void G2_Stances()
        {
            Assert.AreEqual(PoseFrame.Pass, Stances.For(true, false, 9f, 0f, true, 0f), "the passer");
            Assert.AreEqual(PoseFrame.Defense, Stances.For(true, true, 1.5f, 0f, false, 0f), "set on the ball");
            Assert.AreEqual(PoseFrame.None, Stances.For(true, true, 5f, 0f, false, 0f), "too far from the ball");
            Assert.AreEqual(PoseFrame.None, Stances.For(true, false, 1f, 0f, false, 0f), "offense doesn't crouch");
            Assert.AreEqual(PoseFrame.None, Stances.For(false, true, 1f, 0f, false, 0f), "dead ball");
            Assert.AreEqual(PoseFrame.None, Stances.For(true, true, 1f, 5f, false, 0f), "sprinting runs");
            var a = Stances.For(true, true, 1f, 2f, false, 0.01f);
            var b = Stances.For(true, true, 1f, 2f, false, 0.01f + 1f / Stances.ShuffleFps);
            Assert.AreNotEqual(a, b, "the slide shuffles between two frames");
            Assert.IsTrue((a == PoseFrame.Slide || a == PoseFrame.Defense) && (b == PoseFrame.Slide || b == PoseFrame.Defense));
        }
        // ---- G3: gameplay effects

        [Test]
        public void G3_ReflectionsOnlyOnShinyFloors()
        {
            Assert.Greater(GameplayFx.ReflectionAlpha(FloorStyle.Hardwood), GameplayFx.ReflectionAlpha(FloorStyle.Tiles));
            Assert.Greater(GameplayFx.ReflectionAlpha(FloorStyle.NeonGrid), 0f);
            Assert.AreEqual(0f, GameplayFx.ReflectionAlpha(FloorStyle.Asphalt), "no mirror on the blacktop");
            Assert.AreEqual(0f, GameplayFx.ReflectionAlpha(FloorStyle.Rubber));
            Assert.LessOrEqual(GameplayFx.ReflectionAlpha(FloorStyle.Hardwood), 0.2f, "faint, never a second player");
            foreach (var court in _c.Courts)
            {
                var style = GameplayFx.StyleOf(court);
                if (court.circuit == CourtCircuit.League && court.floorStyle < 0) Assert.AreEqual(FloorStyle.Hardwood, style, court.id);
                if (court.circuit == CourtCircuit.Blacktop && court.floorStyle < 0) Assert.AreEqual(FloorStyle.Asphalt, style, court.id);
            }
            Assert.AreEqual(FloorStyle.Asphalt, GameplayFx.StyleOf(null));
        }

        [Test]
        public void G3_ShadowShrinksAndFadesWithTheJump()
        {
            GameplayFx.ShadowForJump(0f, out float s0, out float a0);
            GameplayFx.ShadowForJump(1f, out float s1, out float a1);
            GameplayFx.ShadowForJump(3f, out float s3, out float a3);
            Assert.AreEqual(1f, s0); Assert.AreEqual(1f, a0);
            Assert.Less(s1, s0); Assert.Less(a1, a0);
            Assert.Greater(s1, 0.5f, "still a shadow at the top of the jump");
            Assert.AreEqual(s1, s3, "SKY HIGH doesn't make it vanish");
            Assert.AreEqual(a1, a3);
        }

        [Test]
        public void G3_BallTrail_KeepsTheNewestSamplesInOrder()
        {
            var t = new BallTrail(3);
            Assert.IsTrue(t.Push(1f, 1f, 0f));
            Assert.IsFalse(t.Push(9f, 9f, GameplayFx.TrailStep * 0.5f), "too soon for another sample");
            Assert.IsTrue(t.Push(2f, 2f, GameplayFx.TrailStep));
            Assert.IsTrue(t.Push(3f, 3f, GameplayFx.TrailStep * 2f));
            Assert.IsTrue(t.Push(4f, 4f, GameplayFx.TrailStep * 3f));
            Assert.AreEqual(3, t.Count);
            t.Get(0, out float x0, out _); t.Get(2, out float x2, out _);
            Assert.AreEqual(4f, x0, "newest first");
            Assert.AreEqual(2f, x2, "the oldest kept sample (1 dropped off)");
            t.Clear();
            Assert.AreEqual(0, t.Count);
            Assert.IsTrue(t.Push(5f, 5f, 0f), "a cleared trail samples straight away");
        }

        [Test]
        public void G3_TrailsOnlyInFlight()
        {
            Assert.IsTrue(GameplayFx.Trails(BallPhase.Shot, 3f));
            Assert.IsTrue(GameplayFx.Trails(BallPhase.Pass, 1f));
            Assert.IsFalse(GameplayFx.Trails(BallPhase.Held, 1f));
            Assert.IsFalse(GameplayFx.Trails(BallPhase.Loose, 0.2f), "rolling on the floor");
            Assert.IsTrue(GameplayFx.Trails(BallPhase.Loose, 2f), "a big rebound bounce");
            Assert.AreEqual(3, GameplayFx.TrailAlphas.Length);
            for (int i = 1; i < GameplayFx.TrailAlphas.Length; i++) Assert.Less(GameplayFx.TrailAlphas[i], GameplayFx.TrailAlphas[i - 1]);
        }

        [Test]
        public void G3_DustOnHardCutsOnly()
        {
            var east = new Vec2(4f, 0f);
            Assert.AreEqual(0f, GameplayFx.CutStrength(east, east), "straight line");
            Assert.AreEqual(1f, GameplayFx.CutStrength(east, new Vec2(-4f, 0f)), 1e-5f, "full reversal");
            Assert.GreaterOrEqual(GameplayFx.CutStrength(east, new Vec2(0f, 4f)), GameplayFx.DustAt, "a right-angle cut kicks up dust");
            Assert.Less(GameplayFx.CutStrength(east, new Vec2(4f, 2f)), GameplayFx.DustAt, "a gentle curve doesn't");
            Assert.AreEqual(0f, GameplayFx.CutStrength(new Vec2(1f, 0f), new Vec2(-1f, 0f)), "jogging never counts");
        }

        [Test]
        public void G3_SweatOnlyWhenTiredAndMoving()
        {
            Assert.IsTrue(GameplayFx.Sweaty(0.2f, true));
            Assert.IsFalse(GameplayFx.Sweaty(0.2f, false));
            Assert.IsFalse(GameplayFx.Sweaty(0.9f, true));
        }
    }
}
