using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>15 Oct 2026: the RETRO HOOPS beat tape (original instrumental beats made by the game).</summary>
    public class BeatTapeTests
    {
        [Test]
        public void Tape_IsTenOriginalBeats_ThatLastAboutASongEach()
        {
            Assert.AreEqual(10, StreetBeats.Beats.Length);
            var keys = new HashSet<string>();
            var titles = new HashSet<string>();
            string[] notOurs = { "Veeze", "Sada", "Babyface", "Packman", "Gramz", "3200", "Fox BD", "Exotic", "7sixers", "Treehouse", "Law N Order" };
            foreach (var b in StreetBeats.Beats)
            {
                Assert.IsTrue(keys.Add(b.Key), b.Key);
                Assert.IsTrue(titles.Add(b.Title), b.Title);
                foreach (var n in notOurs) Assert.IsFalse(b.Title.Contains(n) || b.Vibe.Contains(n), b.Title + " borrows a name");
                float sec = StreetBeats.Seconds(b);
                Assert.Greater(sec, 70f, b.Title);
                Assert.Less(sec, 100f, b.Title);
                Assert.AreEqual(4, b.Chords.Length);
                Assert.AreEqual(4, b.ChordsB.Length);
                Assert.IsNotNull(StreetBeats.Find(b.Key));
            }
            // Every groove is on the tape.
            foreach (BeatGroove g in System.Enum.GetValues(typeof(BeatGroove)))
                Assert.IsTrue(System.Array.Exists(StreetBeats.Beats, b => b.Groove == g), g.ToString());
        }

        [Test]
        public void Patterns_SitInTheirGrooves()
        {
            foreach (BeatGroove g in System.Enum.GetValues(typeof(BeatGroove)))
                for (int bar = 0; bar < 2; bar++)
                {
                    Assert.AreEqual(16, StreetBeats.KickPattern(g, bar).Length);
                    Assert.AreEqual('x', StreetBeats.KickPattern(g, bar)[0], "a kick on the one");
                }
            Assert.AreEqual('x', StreetBeats.ClapPattern(BeatGroove.Halftime, 0)[8], "half-time: the clap on 3");
            Assert.AreEqual('x', StreetBeats.ClapPattern(BeatGroove.Bounce, 0)[4]);
            Assert.AreEqual('x', StreetBeats.ClapPattern(BeatGroove.Bounce, 0)[12]);
        }

        [Test]
        public void Motif_IsATwoBarHook_InKey()
        {
            foreach (var b in StreetBeats.Beats)
            {
                var m = StreetBeats.Motif(b);
                Assert.GreaterOrEqual(m.Count, 4, b.Title);
                Assert.AreEqual(0, m[0].Step, "starts on the one");
                int last = -1;
                foreach (var n in m)
                {
                    Assert.Greater(n.Step, last);
                    Assert.Less(n.Step, 32);
                    Assert.GreaterOrEqual(n.Length, 1);
                    last = n.Step;
                }
                Assert.AreEqual(m.Count, StreetBeats.Motif(b).Count, "deterministic");
            }
        }

        [Test]
        public void Render_IsAFullSong_NotSilent_AndTheSameEveryTime()
        {
            var b = StreetBeats.Find("beat.jit_bounce");
            var s = StreetBeats.Render(b);
            int expected = (int)System.Math.Round(StreetBeats.Bars(b) * 16 * (60.0 / b.Bpm / 4.0) * AudioSynth.SampleRate + StreetBeats.TailSeconds * AudioSynth.SampleRate);
            Assert.AreEqual(expected, s.Length);
            float peak = 0f;
            double energy = 0;
            foreach (var x in s) { Assert.IsFalse(float.IsNaN(x)); peak = System.Math.Max(peak, System.Math.Abs(x)); energy += x * x; }
            Assert.AreEqual(0.82f, peak, 0.001f);
            Assert.Greater(energy / s.Length, 0.01);
            Assert.AreEqual(0f, s[s.Length - 1], 1e-4f, "fades out at the end");
            // The intro is quieter than the drop.
            int bar = (int)(16 * (60.0 / b.Bpm / 4.0) * AudioSynth.SampleRate);
            Assert.Less(Rms(s, 0, bar * 2), Rms(s, bar * 6, bar * 8));
            var again = StreetBeats.Render(b);
            for (int i = 0; i < s.Length; i += 997) Assert.AreEqual(s[i], again[i]);
        }

        private static double Rms(float[] s, int from, int to)
        {
            double e = 0;
            for (int i = from; i < to; i++) e += s[i] * s[i];
            return System.Math.Sqrt(e / (to - from));
        }

        [Test]
        public void Soundtrack_IncludesTheTape()
        {
            for (int i = 0; i < StreetBeats.Beats.Length; i++)
            {
                int track = Soundtrack.BeatTrack(i);
                Assert.IsTrue(ReferenceEquals(StreetBeats.Beats[i], Soundtrack.BeatOf(track)));
                Assert.AreEqual(StreetBeats.Beats[i].Title, Soundtrack.Title(track));
                Assert.AreEqual(StreetBeats.Beats[i].Bpm, Soundtrack.Bpm(track));
            }
            Assert.IsNull(Soundtrack.BeatOf(0));
            Assert.IsNull(Soundtrack.BeatOf(Soundtrack.FirstBeat - 1));
            Assert.AreEqual(Soundtrack.Count - 1, Soundtrack.BeatTrack(StreetBeats.Beats.Length - 1));
        }

        [Test]
        public void Cover_IsPixelArt_DifferentForEveryBeat()
        {
            var seen = new HashSet<string>();
            foreach (var b in StreetBeats.Beats)
            {
                var px = StreetBeats.Cover(b, 32);
                Assert.AreEqual(32 * 32, px.Length);
                var orange = RgbColor.FromHex("#F77F00");
                Assert.IsTrue(System.Array.Exists(px, c => c.r == orange.r && c.g == orange.g && c.b == orange.b), "the ball");
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < px.Length; i += 7) sb.Append(px[i].r).Append(px[i].g).Append(px[i].b);
                Assert.IsTrue(seen.Add(sb.ToString()), b.Title);
            }
        }
    }
}
