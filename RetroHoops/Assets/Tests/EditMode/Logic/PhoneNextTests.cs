using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>15 Oct 2026: the MUSIC app's NEXT button (it did nothing when only one song could play).</summary>
    public class PhoneNextTests
    {
        [Test]
        public void Next_SkipsMidLoop_StartsANewLoopAtTheEnd_AndExplainsWhenOnlyOneSongCanPlay()
        {
            Assert.AreEqual(PhonePlaylist.NextStep.Skip, PhonePlaylist.Next(5, 0, 5));
            Assert.AreEqual(PhonePlaylist.NextStep.Skip, PhonePlaylist.Next(5, 3, 5));
            Assert.AreEqual(PhonePlaylist.NextStep.NewLoop, PhonePlaylist.Next(5, 4, 5), "the last song: next loop, not silence");
            Assert.AreEqual(PhonePlaylist.NextStep.Skip, PhonePlaylist.Next(5, -1, 5), "unknown position: just skip");
            Assert.AreEqual(PhonePlaylist.NextStep.Explain, PhonePlaylist.Next(1, 0, 1));
            Assert.AreEqual(PhonePlaylist.NextStep.Explain, PhonePlaylist.Next(0, -1, 0));
        }

        [Test]
        public void OnlyOneSong_SaysTheRestAreExplicit()
        {
            string four = PhonePlaylist.OnlyOneSong(4);
            StringAssertContains(four, "4 MORE ARE EXPLICIT");
            StringAssertContains(four, "EXPLICIT SONGS");
            StringAssertContains(PhonePlaylist.OnlyOneSong(1), "1 MORE IS EXPLICIT");
            Assert.AreEqual("ONLY 1 SONG IN THIS PLAYLIST", PhonePlaylist.OnlyOneSong(0));
        }

        private static void StringAssertContains(string s, string part) => Assert.IsTrue(s.Contains(part), s);
    }
}
