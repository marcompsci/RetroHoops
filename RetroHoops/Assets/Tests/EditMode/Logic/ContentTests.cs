using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    public class DefaultContentTests
    {
        private ContentCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = DefaultContent.Create();
        }

        [Test]
        public void DefaultContent_PassesValidationWithNoErrors()
        {
            var report = ContentValidator.Validate(_catalog);
            Assert.IsTrue(report.IsValid, report.ToString());
        }

        [Test]
        public void DefaultContent_HasNoWarnings()
        {
            var report = ContentValidator.Validate(_catalog);
            Assert.AreEqual(0, report.Warnings.Count, report.ToString());
        }

        [Test]
        public void League_HasEightTeams_TwoUnlocked()
        {
            var league = _catalog.TeamsInTier(TeamTier.League);
            Assert.AreEqual(8, league.Count);
            int unlocked = 0;
            foreach (var t in league) if (t.unlockedByDefault) unlocked++;
            Assert.AreEqual(2, unlocked);
        }

        [Test]
        public void BlacktopCircuit_HasFiveCrews_EachOnAStreetCourt()
        {
            int courts = 0;
            foreach (var c in _catalog.Courts) if (c.circuit == CourtCircuit.Blacktop) courts++;
            Assert.AreEqual(30, courts, "five circuit courts, three street courts each for Seasons 2, 3 and 4, and two each for Seasons 5 to 12");
            var crews = _catalog.TeamsInTier(TeamTier.Circuit);
            Assert.AreEqual(5, crews.Count);
            foreach (var crew in crews) Assert.AreEqual(CourtCircuit.Blacktop, _catalog.Court(crew.homeCourtId).circuit, crew.id);
        }

        [Test]
        public void HasTwelveDistinctArchetypes()
        {
            Assert.AreEqual(12, _catalog.Archetypes.Count);
            var seen = new HashSet<Archetype>();
            foreach (var a in _catalog.Archetypes) Assert.IsTrue(seen.Add(a.archetype), "Duplicate " + a.archetype);
        }

        [Test]
        public void Rook_ExistsOnPlayerCrew()
        {
            var crew = _catalog.Team(DefaultContent.PlayerCrewId);
            Assert.IsNotNull(crew);
            Assert.IsTrue(crew.rosterPlayerIds.Contains(DefaultContent.RookPlayerId));
            Assert.AreEqual("Rook", _catalog.Player(DefaultContent.RookPlayerId).firstName);
        }

        [Test]
        public void ThreeDifficulties_InOrder_AndNoneBoostMovement()
        {
            Assert.AreEqual(5, _catalog.Difficulties.Count); // 17 Oct 2026: + BALLER and LEGENDARY (city crews)
            Assert.AreEqual(3, CityCrews.Pickable(_catalog).Count);
            Assert.AreEqual("Baller", _catalog.Difficulties[3].displayName);
            Assert.AreEqual("Legendary", _catalog.Difficulties[4].displayName);
            Assert.AreEqual("Rookie", _catalog.Difficulties[0].displayName);
            Assert.AreEqual("Caller", _catalog.Difficulties[1].displayName);
            Assert.AreEqual("Legend", _catalog.Difficulties[2].displayName);
            foreach (var d in _catalog.Difficulties) Assert.LessOrEqual(d.movementScale, 1f);
        }

        [Test]
        public void Content_IsDeterministic()
        {
            var again = DefaultContent.Create();
            Assert.AreEqual(_catalog.Players.Count, again.Players.Count);
            for (int i = 0; i < _catalog.Players.Count; i++)
            {
                Assert.AreEqual(_catalog.Players[i].id, again.Players[i].id);
                Assert.AreEqual(_catalog.Players[i].attributes.Overall, again.Players[i].attributes.Overall);
                Assert.AreEqual(_catalog.Players[i].appearance.skinTone, again.Players[i].appearance.skinTone);
            }
        }

        [Test]
        public void Season_UsesAllLeagueTeams()
        {
            Assert.AreEqual(1, _catalog.Seasons.Count);
            Assert.AreEqual(8, _catalog.Seasons[0].teamIds.Count);
            Assert.AreEqual(10, _catalog.Seasons[0].regularSeasonGames);
        }
    }

    public class ContentValidatorTests
    {
        [Test]
        public void DetectsDuplicatePlayerIds()
        {
            var c = DefaultContent.Create();
            var copy = c.Players[0];
            c.Players.Add(new PlayerDef { id = copy.id, firstName = "Dup", archetypeId = copy.archetypeId, attributes = copy.attributes });
            var report = ContentValidator.Validate(c);
            Assert.IsFalse(report.IsValid);
            Assert.IsTrue(report.Errors.Exists(e => e.Contains("Duplicate Player id")), report.ToString());
        }

        [Test]
        public void DetectsDuplicateTeamIds()
        {
            var c = DefaultContent.Create();
            c.Teams[1].id = c.Teams[0].id;
            var report = ContentValidator.Validate(c);
            Assert.IsTrue(report.Errors.Exists(e => e.Contains("Duplicate Team id")), report.ToString());
        }

        [TestCase(0)]
        [TestCase(100)]
        [TestCase(-5)]
        public void DetectsOutOfRangeRatings(int badRating)
        {
            var c = DefaultContent.Create();
            c.Players[0].attributes = c.Players[0].attributes.With(AttributeType.Shooting, badRating);
            var report = ContentValidator.Validate(c);
            Assert.IsTrue(report.Errors.Exists(e => e.Contains("Shooting rating")), report.ToString());
        }

        [Test]
        public void AcceptsBoundaryRatings()
        {
            var c = DefaultContent.Create();
            c.Players[0].attributes = c.Players[0].attributes.With(AttributeType.Shooting, 1).With(AttributeType.Speed, 99);
            Assert.IsTrue(ContentValidator.Validate(c).IsValid);
        }

        [Test]
        public void DetectsMissingRosterReference()
        {
            var c = DefaultContent.Create();
            c.Teams[0].rosterPlayerIds.Add("player.does_not_exist");
            var report = ContentValidator.Validate(c);
            Assert.IsTrue(report.Errors.Exists(e => e.Contains("missing player")), report.ToString());
        }

        [Test]
        public void DetectsUnfairDifficulty()
        {
            var c = DefaultContent.Create();
            c.Difficulties[2].movementScale = 1.2f;
            var report = ContentValidator.Validate(c);
            Assert.IsTrue(report.Errors.Exists(e => e.Contains("hidden speed")), report.ToString());
        }

        [Test]
        public void DetectsEmptyId()
        {
            var c = DefaultContent.Create();
            c.Courts[0].id = "";
            var report = ContentValidator.Validate(c);
            Assert.IsTrue(report.Errors.Exists(e => e.Contains("empty id")), report.ToString());
        }

        [Test]
        public void DetectsTooFewUnlockedTeams()
        {
            var c = DefaultContent.Create();
            foreach (var t in c.Teams) t.unlockedByDefault = false;
            var report = ContentValidator.Validate(c);
            Assert.IsTrue(report.Errors.Exists(e => e.Contains("unlocked by default")), report.ToString());
        }

        [Test]
        public void DetectsInvalidRules()
        {
            var c = DefaultContent.Create();
            c.Rules[0].beyondArcPoints = 1;
            var report = ContentValidator.Validate(c);
            Assert.IsTrue(report.Errors.Exists(e => e.Contains("worth more")), report.ToString());
        }

        [Test]
        public void NullCatalog_IsInvalid()
        {
            Assert.IsFalse(ContentValidator.Validate(null).IsValid);
        }
    }
}
