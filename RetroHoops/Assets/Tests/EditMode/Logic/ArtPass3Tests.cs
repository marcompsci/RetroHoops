using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Art pass 3: the near side of the court in portrait (board / wall, front-row fans, steps).</summary>
    public class ArtPass3Tests
    {
        private static CourtDef Court(ContentCatalog c, bool arena)
        {
            foreach (var court in c.Courts) if (CourtsideGenerator.IsArena(court) == arena) return court;
            Assert.Fail("no court");
            return null;
        }

        [Test]
        public void NearSide_IsOpaque_WiderThanTheCourt_AndFadesToTheDeepColour()
        {
            var c = DefaultContent.Create();
            foreach (bool arena in new[] { true, false })
            {
                var court = Court(c, arena);
                int texW = CourtGenerator.TextureWidth(CourtGeometry.Default);
                var ns = CourtsideGenerator.NearSide(court, texW, c.Teams[0].primary, c.Teams[1].primary);
                Assert.AreEqual(texW + CourtsideGenerator.SidePad * 2, ns.Width);
                Assert.AreEqual(CourtsideGenerator.Height, ns.Height);
                foreach (var p in ns.Pixels) Assert.AreEqual(255, p.a, "no holes");
                var deep = CourtsideGenerator.DeepColor(court);
                for (int x = 0; x < ns.Width; x++) Assert.AreEqual(deep, ns.Get(x, 0), "bottom row is the camera background colour");
                // The board / wall at the top is not the step colour.
                Assert.AreNotEqual(ns.Get(10, ns.Height - 30), ns.Get(10, ns.Height - 6));
            }
        }

        [Test]
        public void ArenaBoard_ShowsBothTeamsColours()
        {
            var c = DefaultContent.Create();
            var court = Court(c, true);
            var a = RgbColor.FromHex("#E4572E");
            var b = RgbColor.FromHex("#4CC9F0");
            var ns = CourtsideGenerator.NearSide(court, 200, a, b);
            bool sawA = false, sawB = false;
            for (int x = 0; x < ns.Width; x++)
                for (int r = 3; r <= 10; r++)
                {
                    var p = ns.Get(x, ns.Height - 1 - r);
                    if (p == a.Lighten(0.2f)) sawA = true;
                    if (p == b.Lighten(0.2f)) sawB = true;
                }
            Assert.IsTrue(sawA && sawB);
        }

        [Test]
        public void FrontRowFan_FromBehind_HasNoEyes_AndRaisesBothArms()
        {
            var skin = CharacterSpriteGenerator.SkinTones[2];
            var shirt = RgbColor.FromHex("#06D6A0");
            for (int v = 0; v < 6; v++)
            {
                var idle = CourtsideGenerator.FanBack(shirt, skin, false, v);
                var up = CourtsideGenerator.FanBack(shirt, skin, true, v);
                Assert.AreEqual(10, idle.Width);
                Assert.AreEqual(16, idle.Height);
                Assert.Greater(up.OpaqueCount(), idle.OpaqueCount(), "arms up adds pixels");
                Assert.AreEqual(0, idle.Get(0, 12).a, "no arm beside the head when sitting");
                Assert.Greater(up.Get(0, 12).a, 0, "arm beside the head when cheering");
            }
        }

        [Test]
        public void FanSlots_AreDeterministic_InsideTheArt_AndNotOverlapping()
        {
            int texW = CourtGenerator.TextureWidth(CourtGeometry.Default);
            var a = CourtsideGenerator.FanSlots(texW, 1234u, 0.7f);
            var b = CourtsideGenerator.FanSlots(texW, 1234u, 0.7f);
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++) Assert.AreEqual(a[i], b[i]);
            Assert.Greater(a.Length, 5);
            int w = texW + CourtsideGenerator.SidePad * 2;
            for (int i = 0; i < a.Length; i++)
            {
                Assert.GreaterOrEqual(a[i], 0);
                Assert.LessOrEqual(a[i] + CrowdGenerator.Width * 2, w);
                if (i > 0) Assert.GreaterOrEqual(a[i] - a[i - 1], CrowdGenerator.Width * 2, "fans don't overlap");
            }
        }
    }
}
