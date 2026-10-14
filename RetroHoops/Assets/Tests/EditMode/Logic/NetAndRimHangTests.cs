using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>14 Oct 2026: a natural rim hang (no flicker) and a net that moves when the ball goes through.</summary>
    public class NetAndRimHangTests
    {
        private static bool Same(PixelCanvas a, PixelCanvas b)
        {
            for (int i = 0; i < a.Pixels.Length; i++) if (a.Pixels[i] != b.Pixels[i]) return false;
            return true;
        }

        private static int NetBottom(PixelCanvas h)
        {
            // Lowest row with net pixels under the rim (the net is light; the rim and board sit higher).
            int px = PropSpriteGenerator.HoopPivotX * 2;
            for (int y = 0; y < h.Height; y++)
                for (int x = px - 14; x <= px + 14; x++)
                    if (h.Get(x, y).a > 0) return y;
            return -1;
        }

        [Test]
        public void Net_StretchesAsTheBallDrops_WhipsUp_ThenSettles()
        {
            Assert.AreEqual(PropSpriteGenerator.NetFrames, PropSpriteGenerator.NetShapes.Length);
            Assert.AreEqual(PropSpriteGenerator.NetFrames, PropSpriteGenerator.NetTimes.Length);
            int rest = NetBottom(PropSpriteGenerator.Hoop(0));
            int stretched = NetBottom(PropSpriteGenerator.Hoop(3));
            int whipped = NetBottom(PropSpriteGenerator.Hoop(4));
            Assert.Less(stretched, rest, "the ball stretches it down (lower bottom row)");
            Assert.Greater(whipped, rest, "then it whips up shorter");
            for (int f = 0; f < PropSpriteGenerator.NetFrames; f++)
            {
                if (f > 0) Assert.IsFalse(Same(PropSpriteGenerator.Hoop(f), PropSpriteGenerator.Hoop(f - 1)), "every frame moves");
                Assert.AreEqual(52, PropSpriteGenerator.HoopSide(f).Width);
            }
            // Frames follow in order through the make and end at rest.
            int last = 0;
            for (float t = 0f; t < 1f; t += 0.01f)
            {
                int f = PropSpriteGenerator.NetFrame(t);
                Assert.IsTrue(f == 0 ? t >= 0.65f : f >= last, "plays forward once");
                last = f;
            }
            // The sway dies down.
            for (int f = 5; f < PropSpriteGenerator.NetFrames; f++)
                Assert.LessOrEqual(System.Math.Abs(PropSpriteGenerator.NetShapes[f].Sway), System.Math.Abs(PropSpriteGenerator.NetShapes[f - 1].Sway));
        }

        [Test]
        public void RimHang_HandsStayOnTheRim_TheSwingIsSmooth_AndThePoseNeverFlips()
        {
            var hoop = new Vec2(0f, 1.6f);
            var start = new Vec2(0.8f, 3.4f);
            float slamU = 0.4f, hangU = 0.35f;
            const int lift = 30;
            int lastSwing = 0, steps = 0, lastLift = -1, liftJumps = 0;
            for (float u = slamU; u < slamU + hangU; u += 0.002f)
            {
                var f = DunkPath.At(start, hoop, start, u, slamU, hangU, lift, DunkPath.Approach);
                if (!f.OnRim) continue;
                // Hands (lift + reach) follow the rim down: lift + dip stays within a pixel of full reach.
                int gap = lift - (f.LiftPx + f.RimDipPx);
                Assert.IsTrue(gap >= -1 && gap <= 1, "hands on the rim");
                Assert.LessOrEqual(System.Math.Abs(f.SwingPx - lastSwing), 1, "the swing moves a pixel at a time");
                if (f.SwingPx != lastSwing) steps++;
                if (lastLift >= 0 && f.LiftPx != lastLift) liftJumps++;
                lastSwing = f.SwingPx;
                lastLift = f.LiftPx;
                foreach (var style in new[] { DunkStyle.ThreeSixty, DunkStyle.Skyline, DunkStyle.Windmill })
                    Assert.IsFalse(Dunks.HangPose(style, f).FlipOverride, "no flips while hanging");
                Assert.IsTrue(Dunks.HangPose(DunkStyle.Reverse, f).FlipOverride, "a reverse dunker stays turned");
                Assert.IsTrue(Dunks.HangPose(DunkStyle.TwoHand, f).ArmsUp);
            }
            Assert.Greater(steps, 2, "it really swings");
            Assert.Less(liftJumps, 12, "the body doesn't flicker up and down");

            var drop = DunkPath.At(start, hoop, start, slamU + hangU + 0.1f, slamU, hangU, lift, DunkPath.Approach);
            Assert.IsTrue(drop.Dropping);
            Assert.AreEqual(0, Dunks.HangPose(DunkStyle.TwoHand, drop).OffsetX);
        }
    }
}
