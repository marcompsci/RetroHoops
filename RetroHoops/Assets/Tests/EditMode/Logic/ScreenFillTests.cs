using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>14 Oct 2026: the court fills the screen, see-through controls, and a stick that follows the thumb.</summary>
    public class ScreenFillTests
    {
        [Test]
        public void FillZoom_MakesTheCourtReachTheBottom_OnPhonesAndIPads()
        {
            const float ppu = 16f, art = 11f + 2f + 1f, hud = 0.1f;
            foreach (var (w, h) in new[] { (1320, 2868), (1179, 2556), (1170, 2532), (2064, 2752), (1640, 2360) })
            {
                int z = CameraMath.FillZoom(h, art, hud, ppu);
                float visible = h / (z * ppu);
                Assert.GreaterOrEqual(visible * (1f - hud), art - 0.01f, "the court reaches the bottom of the screen");
                Assert.Less(visible * (1f - hud), art * 1.25f, "and fills most of it (" + w + "x" + h + ")");
                Assert.GreaterOrEqual(z, CameraMath.IntegerZoom(w, h, 11.5f, 20f, ppu), "never further out than before");
            }
        }

        [Test]
        public void Stick_FollowsTheThumbPastTheEdge_AndStaysPutInside()
        {
            var o = new Vec2(0f, 0f);
            Assert.AreEqual(0f, JoystickMath.FollowOrigin(o, new Vec2(50f, 0f), 130f).x, "inside: stays");
            var moved = JoystickMath.FollowOrigin(o, new Vec2(300f, 0f), 130f);
            Assert.AreEqual(170f, moved.x, 0.01f);
            var away = JoystickMath.FollowOrigin(o, new Vec2(0f, -400f), 130f);
            Assert.AreEqual(-270f, away.y, 0.01f);
            // The thumb ends up exactly on the rim: full push in that direction.
            var v = JoystickMath.Evaluate(new Vec2(300f - moved.x, 0f), 130f, 0.12f);
            Assert.AreEqual(1f, v.Magnitude, 0.01f);
        }

        [Test]
        public void ControlsOpacity_Defaults65_AndSaves()
        {
            var c = DefaultContent.Create();
            var d = new CareerSaveData();
            Assert.AreEqual(0.65f, ControlFeel.OpacityFor(d.settings), 0.001f);
            Assert.IsTrue(d.settings.fillScreen);
            d.settings.controlsOpacity = 3;
            d.settings.fillScreen = false;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _);
            Assert.AreEqual(1f, ControlFeel.OpacityFor(back.settings), 0.001f);
            Assert.IsFalse(back.settings.fillScreen);
            Assert.AreEqual(ControlFeel.Opacity.Length - 1, ControlFeel.ClampOpacity(99));
            Assert.AreEqual(0, ControlFeel.ClampOpacity(-4));
        }
    }
}
