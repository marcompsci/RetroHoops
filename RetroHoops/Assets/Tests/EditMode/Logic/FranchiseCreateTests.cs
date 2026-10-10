using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>10 Oct 2026: Franchise ► CREATE A PLAYER.</summary>
    public class FranchiseCreateTests
    {
        private static ContentCatalog C() => DefaultContent.Create();

        private static FranchiseSaveData NewFranchise(ContentCatalog c)
        {
            var league = c.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return Franchise.Create(c, league[0].id, 11u);
        }

        private static CreatedPlayerSpec Spec(int level = 0) => new CreatedPlayerSpec
        {
            First = "  Omari!! ", Last = "Bell", Number = 16, Archetype = (int)Archetype.ShotCreator,
            Skin = 3, Hair = 2, HairColor = 0, Body = 1, Height = 2, Level = level,
        };

        /// <summary>Cuts your club down so there's room (keeps the minimum).</summary>
        private static void MakeRoom(FranchiseSaveData f, int spots)
        {
            while (Franchise.Roster(f, f.you).Count > Franchise.MaxRoster - spots)
            {
                var r = Franchise.Roster(f, f.you).FindAll(x => !x.created);
                Assert.IsTrue(Franchise.Release(f, r[r.Count - 1].id), "made room");
            }
        }

        [Test]
        public void CreateAPlayer_JoinsYourClub_AsARookie()
        {
            var c = C();
            var f = NewFranchise(c);
            MakeRoom(f, 1);
            int before = Franchise.Roster(f, f.you).Count;
            Assert.IsNull(Franchise.CannotCreate(f));
            var p = Franchise.CreatePlayer(f, c, Spec());
            Assert.IsNotNull(p);
            Assert.IsTrue(p.created);
            Assert.AreEqual(f.you, p.team);
            Assert.AreEqual(before + 1, Franchise.Roster(f, f.you).Count);
            Assert.AreEqual(p.id, f.teams[f.you].roster[f.teams[f.you].roster.Count - 1], "end of the bench");
            Assert.AreEqual("Omari", p.first, "name cleaned");
            Assert.AreEqual("Bell", p.last);
            Assert.AreEqual(20, p.age);
            Assert.AreEqual(Franchise.MinSalary, p.salary, "rookie signs for the minimum");
            Assert.AreEqual(3, p.years);
            Assert.AreEqual((int)Archetype.ShotCreator, p.archetype);
            Assert.AreEqual(Franchise.CreateLevels[0].Overall, p.Overall, 2);
            Assert.Greater(p.potential, p.Overall, "room to grow");
            Assert.IsTrue(f.moves.Exists(m => m.text.Contains("Omari Bell")), "in the transactions log");
            // The game can put him on the court.
            Franchise.Register(c, f);
            var def = c.Player(Franchise.PlayerId(p.id));
            Assert.IsNotNull(def);
            Assert.AreEqual(2, def.appearance.heightTier);
        }

        [Test]
        public void Limits_RosterFull_Playoffs_Three_CapForStars()
        {
            var c = C();
            var f = NewFranchise(c);
            while (Franchise.Roster(f, f.you).Count < Franchise.MaxRoster)
                Franchise.CreatePlayer(f, c, Spec());
            Assert.IsNotNull(Franchise.CannotCreate(f), "roster full");
            Assert.IsNull(Franchise.CreatePlayer(f, c, Spec()));

            var g = NewFranchise(c);
            MakeRoom(g, 2);
            g.phase = FranchisePhase.Playoffs;
            Assert.IsNotNull(Franchise.CannotCreate(g), "not in the playoffs");
            g.phase = FranchisePhase.Regular;

            var h = NewFranchise(c);
            MakeRoom(h, 2);
            for (int i = 0; i < Franchise.MaxCreated; i++)
            {
                if (Franchise.Roster(h, h.you).Count >= Franchise.MaxRoster) MakeRoom(h, 1);
                Assert.IsNotNull(Franchise.CreatePlayer(h, c, Spec()), "created #" + (i + 1));
            }
            MakeRoom(h, 1);
            Assert.AreEqual(Franchise.MaxCreated, Franchise.CreatedCount(h));
            Assert.IsNotNull(Franchise.CannotCreate(h), "three is the limit");

            // A STAR costs market salary: no cap room, no star (a rookie still fits).
            var k = NewFranchise(c);
            MakeRoom(k, 2);
            k.teams[k.you].deadMoney = Franchise.Cap; // no room at all
            Assert.IsNotNull(Franchise.CannotCreate(k, 2));
            Assert.IsNull(Franchise.CannotCreate(k, 0));
            Assert.Greater(Franchise.CreateSalary(2), Franchise.CreateSalary(1));
            Assert.Greater(Franchise.CreatedRatings(c, 0, 2).Overall, Franchise.CreatedRatings(c, 0, 0).Overall);
        }

        [Test]
        public void Edit_ChangesLookAndName_NotRatings()
        {
            var c = C();
            var f = NewFranchise(c);
            MakeRoom(f, 1);
            var p = Franchise.CreatePlayer(f, c, Spec());
            var attrs = p.attrs;
            var s = Franchise.SpecOf(p);
            s.First = "Rod";
            s.Hair = 4;
            s.Level = 2; // ignored when editing
            Assert.IsTrue(Franchise.EditCreated(f, p.id, s));
            Assert.AreEqual("Rod", p.first);
            Assert.AreEqual(4, p.hair);
            Assert.AreEqual(attrs.Overall, p.Overall);
            var someoneElse = Franchise.Roster(f, f.you).Find(x => !x.created);
            Assert.IsFalse(Franchise.EditCreated(f, someoneElse.id, s), "only created players");
        }

        [Test]
        public void NumberClash_PicksAFreeNumber()
        {
            var c = C();
            var f = NewFranchise(c);
            MakeRoom(f, 1);
            int taken = Franchise.Roster(f, f.you)[0].number;
            var s = Spec();
            s.Number = taken;
            var p = Franchise.CreatePlayer(f, c, s);
            Assert.AreNotEqual(taken, p.number);
        }

        [Test]
        public void CreatedPlayers_SurviveSaving_AndAreNotTradeTargets()
        {
            var c = C();
            var d = new CareerSaveData();
            d.franchise = NewFranchise(c);
            var f = d.franchise;
            MakeRoom(f, 1);
            var p = Franchise.CreatePlayer(f, c, Spec());
            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _);
            var q = Franchise.Player(back.franchise, p.id);
            Assert.IsNotNull(q);
            Assert.IsTrue(q.created);
            Assert.AreEqual("Omari", q.first);

            // Make him the most valuable player you have: AI GMs still never ask for him.
            p.attrs = p.attrs.Offset(30);
            p.age = 21;
            for (int week = 0; week < 40; week++)
            {
                var offer = FranchiseDepth.MakeOffer(f, c, new SeededRandom((uint)(week + 1)), week);
                if (offer != null) Assert.IsFalse(offer.want.Contains(p.id), "never on the table");
            }
        }

        [Test]
        public void LockerRoomPlayer_FillsTheSpec()
        {
            var c = C();
            var d = new CareerSaveData { nickname = "Omari" };
            d.customPlayer = PlayerCreator.FromRook(c);
            d.customPlayer.created = true;
            d.customPlayer.jerseyNumber = 16;
            d.customPlayer.hairStyle = 3;
            var s = Franchise.SpecFromCareer(d, c);
            Assert.AreEqual("Omari", s.First);
            Assert.AreEqual(16, s.Number);
            Assert.AreEqual(3, s.Hair);
        }

        [Test]
        public void CleanName_KeepsItShortAndSafe()
        {
            Assert.AreEqual("", Franchise.CleanName(null));
            Assert.AreEqual("De'Andre", Franchise.CleanName(" De'Andre<>"));
            Assert.LessOrEqual(Franchise.CleanName("Abcdefghijklmnopqrstuvwxyz").Length, Franchise.MaxNameLength);
        }
    }
}
