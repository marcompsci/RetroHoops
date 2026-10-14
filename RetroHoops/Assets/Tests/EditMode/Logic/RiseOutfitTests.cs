using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>15 Oct 2026: CREATE MY PLAYER shows and edits what your Rise player wears; onboarding offers it once.</summary>
    public class RiseOutfitTests
    {
        [Test]
        public void Draft_IsACopy_InYourCrewColours_UntilYouSave()
        {
            var c = DefaultContent.Create();
            var d = new CareerSaveData();
            var draft = RiseOutfit.Draft(d, c);
            Assert.AreEqual(Kits.SlotCount, draft.slots.Count);
            Assert.IsFalse(draft.designed);
            var crew = c.Team(DefaultContent.PlayerCrewId);
            var home = RiseOutfit.Worn(draft);
            Assert.AreEqual(Kits.Pack(crew.primary), home.jersey, "HOME = the crew's jersey, the look the game draws by default");
            Assert.AreEqual((int)JerseyCut.Tank, home.cut);
            Assert.IsFalse(RiseOutfit.Changed(d.kits, draft));

            home.jersey = Kits.Pack(RiseOutfit.Swatches[0]);
            Assert.AreNotEqual(home.jersey, d.kits.slots[0].jersey, "editing the draft doesn't touch the save");
            Assert.IsTrue(RiseOutfit.Changed(d.kits, draft));
            Assert.IsTrue(RiseOutfit.Apply(d, draft));
            Assert.IsTrue(d.kits.designed, "saved: your crew wears it");
            Assert.AreEqual(home.jersey, d.kits.slots[0].jersey);
            Assert.IsFalse(RiseOutfit.Apply(d, RiseOutfit.Draft(d, c)), "nothing changed, nothing saved");
        }

        [Test]
        public void PickingAnotherKit_ChangesWhatsWorn()
        {
            var c = DefaultContent.Create();
            var d = new CareerSaveData();
            var draft = RiseOutfit.Draft(d, c);
            draft.wear = 2;
            Assert.IsTrue(ReferenceEquals(draft.slots[2], RiseOutfit.Worn(draft)));
            Assert.IsTrue(RiseOutfit.Apply(d, draft));
            Assert.AreEqual(2, d.kits.wear);
            Assert.AreEqual(2, d.kits.slots.IndexOf(RiseOutfit.Worn(d.kits)));
        }

        [Test]
        public void LockedStyles_AreSkipped_UntilEarned()
        {
            var d = new CareerSaveData();
            // LONG SLEEVE needs 10 wins: TANK -> TEE -> TANK.
            Assert.AreEqual((int)JerseyCut.Tee, RiseOutfit.NextStyle(Kits.PartCut, (int)JerseyCut.Tank, 3, d, 1));
            Assert.AreEqual((int)JerseyCut.Tank, RiseOutfit.NextStyle(Kits.PartCut, (int)JerseyCut.Tee, 3, d, 1));
            Assert.AreEqual((int)JerseyCut.Tee, RiseOutfit.NextStyle(Kits.PartCut, (int)JerseyCut.Tank, 3, d, -1));
            Assert.AreEqual(2, RiseOutfit.UnlockedCount(Kits.PartCut, 3, d));
            d.totals.wins = 10;
            Assert.AreEqual((int)JerseyCut.LongSleeve, RiseOutfit.NextStyle(Kits.PartCut, (int)JerseyCut.Tee, 3, d, 1));
            Assert.AreEqual(3, RiseOutfit.UnlockedCount(Kits.PartCut, 3, d));
            Assert.AreEqual(3, RiseOutfit.UnlockedCount(Kits.PartCollar, 3, d), "collars are all free");
        }

        [Test]
        public void CrewColours_PutsTheClassicKitBack()
        {
            var c = DefaultContent.Create();
            var d = new CareerSaveData();
            var draft = RiseOutfit.Draft(d, c);
            var before = draft.slots[0].Clone();
            draft.slots[0] = Kits.Randomize(77u, d, "HOME");
            RiseOutfit.ResetSlot(draft, 0, d, c);
            Assert.IsTrue(draft.slots[0].SameLook(before));
            Assert.AreEqual("HOME", draft.slots[0].name);
        }

        [Test]
        public void Swatches_AreDistinct_AndOnTheKitGrid()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var s in RiseOutfit.Swatches)
            {
                Assert.IsTrue(seen.Add(Kits.Pack(s)), "no two the same");
                Assert.AreEqual(Kits.Pack(s), Kits.Pack(Kits.Snap(s)));
            }
            Assert.GreaterOrEqual(seen.Count, 10);
        }

        [Test]
        public void Onboarding_OffersCreatePlayer_OnceToNewPlayers()
        {
            var d = new CareerSaveData();
            Assert.IsTrue(Onboarding.OfferCreatePlayer(d));
            Tours.MarkSeen(d, Tours.MakePlayer);
            Assert.IsFalse(Onboarding.OfferCreatePlayer(d), "once");

            var made = new CareerSaveData();
            made.customPlayer = new CustomPlayerData { created = true };
            Assert.IsFalse(Onboarding.OfferCreatePlayer(made), "already has a player");

            var veteran = new CareerSaveData();
            veteran.totals.games = 3;
            Assert.IsFalse(Onboarding.OfferCreatePlayer(veteran), "not for careers already playing");
            Assert.IsFalse(Onboarding.OfferCreatePlayer(null));
        }
    }
}
