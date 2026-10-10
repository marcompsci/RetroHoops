using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>10 Oct 2026: Rise ► CREATE A PLAYER (your player and created teammates).</summary>
    public class RiseCreateTests
    {
        private static CreatedPlayerSpec Spec(string first = "Kai", string last = "Storm") => new CreatedPlayerSpec
        {
            First = first, Last = last, Number = 7, Archetype = (int)Archetype.RimRunner,
            Skin = 2, Hair = 3, HairColor = 1, Body = 2, Height = 2,
        };

        [Test]
        public void CreateATeammate_GoesInTheSpot_AndPlaysInRise()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            var m = RiseCreate.CreateMate(d, c, Spec(), 1);
            Assert.IsNotNull(m);
            string id = RiseCreate.PlayerId(m.id);
            Assert.AreEqual(id, CrewEngine.Teammates(d, c)[1], "in spot 2");
            Assert.IsTrue(CrewEngine.Pool(d, c).Contains(id));
            Assert.IsTrue(CrewEngine.IsFree(d, c, id), "free");
            var def = c.Player(id);
            Assert.AreEqual("Kai", def.firstName);
            Assert.AreEqual(7, def.jerseyNumber);
            Assert.AreEqual(2, def.appearance.heightTier);
            Assert.AreEqual(RiseCreate.Ratings(c, def.archetypeId).Overall, def.attributes.Overall, "First Callers level");
            // The Rise match request picks him up.
            Assert.IsTrue(CrewEngine.TeammateDefs(d, c).Exists(p => p.id == id));
        }

        [Test]
        public void CreatedTeammates_SurviveSaving_InAFreshCatalog()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            var m = RiseCreate.CreateMate(d, c, Spec(), 0);
            string id = RiseCreate.PlayerId(m.id);
            var fresh = DefaultContent.Create();
            var back = SaveCodec.Decode(SaveCodec.Encode(d), fresh, out _);
            Assert.AreEqual(1, back.rise.made.Count);
            Assert.AreEqual("Storm", back.rise.made[0].last);
            Assert.IsNotNull(fresh.Player(id), "registered on load");
            Assert.AreEqual(id, CrewEngine.Teammates(back, fresh)[0], "still on the crew after a restart");
            Assert.Greater(back.rise.nextMade, m.id, "ids never reused");
        }

        [Test]
        public void ThreeMax_EditAndRemove()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            for (int i = 0; i < RiseCreate.MaxMade; i++) Assert.IsNotNull(RiseCreate.CreateMate(d, c, Spec("P" + i)));
            Assert.IsNotNull(RiseCreate.CannotCreate(d));
            Assert.IsNull(RiseCreate.CreateMate(d, c, Spec()));

            string id = RiseCreate.PlayerId(d.rise.made[0].id);
            Assert.AreEqual(RecruitCheck.Ok, CrewEngine.Recruit(d, c, id, 0));
            var s = RiseCreate.SpecOf(c, d.rise.made[0]);
            s.First = "Neo";
            s.Archetype = (int)Archetype.DeepShooter;
            Assert.IsTrue(RiseCreate.EditMate(d, c, id, s));
            Assert.AreEqual("Neo", c.Player(id).firstName);
            Assert.AreEqual(c.Archetypes.Find(a => a.archetype == Archetype.DeepShooter).id, c.Player(id).archetypeId);

            Assert.IsTrue(RiseCreate.RemoveMate(d, c, id));
            Assert.IsFalse(CrewEngine.Teammates(d, c).Contains(id), "an original takes his spot");
            Assert.AreEqual(2, CrewEngine.Teammates(d, c).Count);
            Assert.AreNotEqual(CrewEngine.Teammates(d, c)[0], CrewEngine.Teammates(d, c)[1]);
            Assert.IsNull(RiseCreate.CannotCreate(d), "room for another");
        }

        [Test]
        public void MyPlayer_IsYourRisePlayer()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            var s = RiseCreate.MySpec(d, c);
            s.First = "Omari";
            s.Number = 16;
            s.Hair = 4;
            s.Archetype = (int)Archetype.ShotCreator;
            RiseCreate.ApplyMine(d, c, s);
            Assert.IsTrue(d.customPlayer.created);
            Assert.AreEqual("Omari", d.nickname);
            var me = PlayerCreator.ForMatch(d, c);
            Assert.AreEqual(16, me.jerseyNumber);
            Assert.AreEqual(4, me.appearance.hairStyle);
            Assert.AreEqual(c.Archetypes.Find(a => a.archetype == Archetype.ShotCreator).id, me.archetypeId);
            Assert.AreEqual("Omari", RiseCreate.MySpec(d, c).First);
        }
    }
}
