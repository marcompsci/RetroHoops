using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 41: free Live and the one-time Retro Hoops Pro unlock.</summary>
    public class Phase41MoneyTests
    {
        private static readonly ContentCatalog C = DefaultContent.Create();

        [Test]
        public void Pro_IsAOneTimeUnlock_AndLiveIsFree()
        {
            Assert.IsTrue(Pro.LiveIsFree);
            Assert.AreEqual("com.phoronomicstudios.retrohoops.pro", Pro.ProductId);
            Assert.AreNotEqual(LiveMode.ProductId, Pro.ProductId);
            string t = Pro.Terms("£4.99");
            Assert.IsTrue(t.Contains("£4.99") && t.Contains("one-time purchase") && t.Contains("isn't a subscription"), t);
            Assert.IsTrue(Pro.Terms(null).Contains(Pro.PriceFallback));
            Assert.AreEqual(5, Pro.Perks.Length);
            Assert.AreEqual("$3.99", Pro.PriceFallback);
            Assert.IsTrue(t.Contains("FRANCHISE") && t.Contains("seasons 2 and up"), t);
            Assert.IsTrue(Pro.FranchiseLocked(false));
            Assert.IsFalse(Pro.FranchiseLocked(true));
        }

        [Test]
        public void RiseSeasonOne_IsFree_TwoNeedsPro()
        {
            var r = new RiseSaveData();
            for (int played = 0; played < Pro.FreeRiseSeasons; played++)
            {
                r.seasonsPlayed = played;
                Assert.IsFalse(Pro.NextSeasonLocked(r, false), "season " + (played + 1) + " is free");
            }
            Assert.AreEqual(1, Pro.FreeRiseSeasons);
            r.seasonsPlayed = 1;
            Assert.IsTrue(Pro.NextSeasonLocked(r, false), "season 2 needs Pro");
            Assert.IsFalse(Pro.NextSeasonLocked(r, true));
            r.seasonsPlayed = 11;
            Assert.IsTrue(Pro.NextSeasonLocked(r, false));
            Assert.IsFalse(Pro.NextSeasonLocked(null, false));
        }

        [Test]
        public void ProGrantsEveryPassItem_Once_AndARefundKeepsThem()
        {
            var items = Pro.PassItems(C);
            int all = 0;
            foreach (var set in HoopsPass.GearSets) all += set.Length;
            Assert.AreEqual(all, items.Count, "every pass item exists in the catalog");
            Assert.AreEqual(40, items.Count);

            var d = new CareerSaveData();
            d.ownedCosmetics.Add(items[0]);
            Assert.IsTrue(Pro.Apply(d, C, true));
            Assert.IsTrue(d.proOwned && d.proGranted);
            foreach (var id in items) Assert.IsTrue(d.ownedCosmetics.Contains(id), id);
            int count = d.ownedCosmetics.Count;
            Assert.IsFalse(Pro.Apply(d, C, true), "nothing new the second time");
            Assert.AreEqual(count, d.ownedCosmetics.Count, "no duplicates");
            Assert.AreEqual(0, Pro.GrantPassItems(d, C));

            Assert.IsTrue(Pro.Apply(d, C, false), "refund");
            Assert.IsFalse(d.proOwned);
            Assert.AreEqual(count, d.ownedCosmetics.Count, "items stay");
            Assert.IsTrue(Pro.NextSeasonLocked(new RiseSaveData { seasonsPlayed = 1 }, d.proOwned));
            Assert.IsFalse(Pro.Apply(null, C, true));
        }

        [Test]
        public void ProCache_RoundTripsInTheSave_DefaultsOff()
        {
            var d = new CareerSaveData { proOwned = true, proGranted = true };
            var back = SaveCodec.Decode(SaveCodec.Encode(d), C, out _);
            Assert.IsTrue(back.proOwned && back.proGranted);
            var fresh = SaveCodec.Decode(SaveCodec.Encode(new CareerSaveData()), C, out _);
            Assert.IsFalse(fresh.proOwned || fresh.proGranted);
        }

        [Test]
        public void PassStillPaysOwnedItemsAsSp_AfterPro()
        {
            // A Pro owner climbing the pass gets SP where the gear would be (nothing is lost).
            var d = new CareerSaveData();
            Pro.Apply(d, C, true);
            int sp = d.signalPoints;
            int day = 2 + 7 * 6 * 240;
            var got = HoopsPass.AddXp(d, C, HoopsPass.XpPerTier * 5, day);
            Assert.AreEqual(5, got.Count);
            Assert.IsNull(got[4].CosmeticId);
            Assert.AreEqual(HoopsPass.OwnedGearSp, got[4].SignalPoints);
            Assert.Greater(d.signalPoints, sp);
        }
    }
}
