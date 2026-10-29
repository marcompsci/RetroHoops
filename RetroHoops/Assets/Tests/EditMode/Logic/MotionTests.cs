using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>28 Oct 2026 (Emil Kowalski audit): the easing curves behave like the CSS curves they copy.</summary>
    public class MotionTests
    {
        [Test]
        public void Curves_StartAtZero_EndAtOne_AndNeverGoBackwards()
        {
            foreach (var f in new System.Func<float, float>[] { Easing.Out, Easing.InOut, Easing.Drawer })
            {
                Assert.AreEqual(0f, f(0f), 1e-5f);
                Assert.AreEqual(1f, f(1f), 1e-5f);
                float last = 0f;
                for (int i = 1; i <= 100; i++)
                {
                    float v = f(i / 100f);
                    Assert.GreaterOrEqual(v, last - 1e-4f);
                    Assert.LessOrEqual(v, 1.0001f);
                    last = v;
                }
            }
        }

        [Test]
        public void EaseOut_IsStrong_EaseInOut_IsSlowAtBothEnds()
        {
            // A strong ease-out has covered most of the way a quarter of the way in: it answers the tap at once.
            Assert.Greater(Easing.Out(0.25f), 0.75f);
            Assert.Greater(Easing.Drawer(0.25f), 0.5f);
            // Ease-in-out is slow at both ends and about half way at half time.
            Assert.AreEqual(0.55f, Easing.InOut(0.5f), 0.1f);
            Assert.Less(Easing.InOut(0.1f), 0.05f);
            Assert.Greater(Easing.InOut(0.9f), 0.95f);
        }

        [Test]
        public void ScreenWipe_IsQuickerAndExitsFasterThanItEnters()
        {
            Assert.LessOrEqual(ScreenWipe.CoverSeconds, 0.3f);
            Assert.Less(ScreenWipe.RevealSeconds, ScreenWipe.CoverSeconds);
        }
    }
}
