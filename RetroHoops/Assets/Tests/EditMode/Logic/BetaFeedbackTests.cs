using System;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>12 Oct 2026: BETA FEEDBACK (TestFlight tester profile + the report they paste into TestFlight feedback).</summary>
    public class BetaFeedbackTests
    {
        [Test]
        public void Report_HasProfileBuildPhoneAndStats()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            d.beta.name = "Omari";
            d.beta.hoopsGames = 2;
            d.beta.hoops = 1;
            d.totals.games = 5; d.totals.wins = 3; d.totals.losses = 2;
            string r = BetaFeedback.Report(d, "1.0.0 (12)", "iPhone18,1", "iOS 26.3", new DateTime(2026, 10, 12, 20, 0, 0, DateTimeKind.Utc));
            StringAssert.Contains("1.0.0 (12)", r);
            StringAssert.Contains("Tester: Omari", r);
            StringAssert.Contains("A LOT", r);
            StringAssert.Contains("FOR FUN", r);
            StringAssert.Contains("iPhone18,1", r);
            StringAssert.Contains("Games: 5 (W 3 L 2)", r);
        }

        [Test]
        public void Profile_SurvivesSaving_AndBadValuesAreCleaned()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            Assert.IsFalse(d.beta.asked);
            d.beta.name = "Kai";
            d.beta.hoops = 2;
            d.beta.asked = true;
            d.beta.sent = 3;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _);
            Assert.AreEqual("Kai", back.beta.name);
            Assert.AreEqual(2, back.beta.hoops);
            Assert.IsTrue(back.beta.asked);
            Assert.AreEqual(3, back.beta.sent);
            d.beta.hoops = 99;
            d.beta.name = new string('x', 80);
            back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _);
            Assert.AreEqual(BetaFeedback.HoopOptions.Length - 1, back.beta.hoops);
            Assert.AreEqual(BetaFeedback.MaxName, back.beta.name.Length);
        }

        [Test]
        public void EmptyProfile_StillMakesAReport()
        {
            string r = BetaFeedback.Report(null, "1.0.0", null, null, DateTime.UtcNow);
            StringAssert.Contains("(no name)", r);
        }
    }
}
