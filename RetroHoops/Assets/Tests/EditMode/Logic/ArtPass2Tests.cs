using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Art pass 2: 2x ball, baskets, net swish and crowd fans.</summary>
    public class ArtPass2Tests
    {
        private static bool Same(PixelCanvas a, PixelCanvas b)
        {
            if (a.Width != b.Width || a.Height != b.Height) return false;
            for (int i = 0; i < a.Pixels.Length; i++) if (a.Pixels[i] != b.Pixels[i]) return false;
            return true;
        }

        [Test]
        public void Ball_IsTwiceTheDetailAndSpins()
        {
            var b0 = PropSpriteGenerator.Ball(0);
            Assert.AreEqual(16, b0.Width);
            Assert.AreEqual(16, b0.Height);
            Assert.Greater(b0.OpaqueCount(), 150);
            Assert.IsFalse(Same(b0, PropSpriteGenerator.Ball(2)), "the seams turn between frames");
            Assert.IsTrue(Same(b0, PropSpriteGenerator.Ball(PropSpriteGenerator.BallFrames)), "half a turn repeats");
            Assert.AreEqual(0, PropSpriteGenerator.BallSpinFrame(1.23f, false), "held: no spin");
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 40; i++) seen.Add(PropSpriteGenerator.BallSpinFrame(i * 0.03f, true));
            Assert.GreaterOrEqual(seen.Count, 6);
        }

        [Test]
        public void Hoops_AreTwiceTheDetailWithThreeNetFrames()
        {
            var h = PropSpriteGenerator.Hoop(0);
            Assert.AreEqual(64, h.Width);
            Assert.AreEqual(60, h.Height);
            var s = PropSpriteGenerator.HoopSide(0);
            Assert.AreEqual(52, s.Width);
            Assert.AreEqual(60, s.Height);
            Assert.IsFalse(Same(h, PropSpriteGenerator.Hoop(1)));
            Assert.IsFalse(Same(h, PropSpriteGenerator.Hoop(2)));
            Assert.IsFalse(Same(s, PropSpriteGenerator.HoopSide(1)));
            // Rim centre sits on the court-pixel pivot: the front of the rim is just below it.
            int px = PropSpriteGenerator.HoopPivotX * 2, py = PropSpriteGenerator.HoopPivotY * 2;
            Assert.Greater(h.Get(px, py - 4).a, 0, "front rim below the pivot");
            Assert.Greater(h.Get(px - 11, py).a, 0, "rim side at pivot height");
        }

        [Test]
        public void Net_KicksAfterAMakeThenRests()
        {
            Assert.AreEqual(0, PropSpriteGenerator.NetFrame(-1f));
            Assert.AreEqual(1, PropSpriteGenerator.NetFrame(0.02f));
            Assert.AreEqual(2, PropSpriteGenerator.NetFrame(0.15f));
            Assert.AreEqual(1, PropSpriteGenerator.NetFrame(0.28f));
            Assert.AreEqual(0, PropSpriteGenerator.NetFrame(0.5f));
        }

        [Test]
        public void Fans_AreTwiceTheDetailWithDistinctLooks()
        {
            var shirt = RgbColor.FromHex("#4CC9F0");
            var skin = RgbColor.FromHex("#C68A62");
            var a = CrowdGenerator.Fan(shirt, skin, false, 0);
            Assert.AreEqual(CrowdGenerator.Width * CrowdGenerator.ArtScale, a.Width);
            Assert.AreEqual(CrowdGenerator.Height * CrowdGenerator.ArtScale, a.Height);
            int distinct = 0;
            for (int v = 1; v < 6; v++) if (!Same(a, CrowdGenerator.Fan(shirt, skin, false, v))) distinct++;
            Assert.AreEqual(5, distinct, "six hair styles");
            Assert.IsFalse(Same(CrowdGenerator.Fan(shirt, skin, true, 0), CrowdGenerator.Fan(shirt, skin, true, 6)), "foam finger");
        }
    }
}
