using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>15 Oct 2026: THE STREETS phase 2 (the STREET TOUR, your crew, off the glass, LIVE / TWO PHONES streets).</summary>
    public class StreetsPhase2Tests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void Tour_HasFourCitiesOfThree_ThenTheCrown_AndEveryCrewIsPlayable()
        {
            var c = DefaultContent.Create();
            Assert.AreEqual(13, StreetsTour.Stops.Length);
            for (int t = 0; t < StreetsTour.Cities.Length; t++) Assert.AreEqual(3, StreetsTour.InCity(t).Count);
            Assert.AreEqual(1, StreetsTour.InCity(4).Count);
            var ids = new HashSet<string>();
            foreach (var s in StreetsTour.Stops)
            {
                Assert.IsTrue(ids.Add(s.Id));
                Assert.IsNotNull(c.Court(s.CourtId), s.Id + " court");
                var team = StreetsTour.Register(c, s);
                Assert.AreEqual(3, team.rosterPlayerIds.Count);
                Assert.IsNotNull(c.Player(s.LeaderId));
                var r = StreetsTour.Challenge(c, new CareerSaveData(), s);
                Assert.AreEqual(GameMode.Streets, r.Mode);
                var rules = c.Find(c.Rules, r.RulesId);
                Assert.IsNotNull(rules, r.RulesId);
                Assert.AreEqual(s.Target, rules.targetScore);
                Assert.IsTrue(rules.winByTwo);
                Assert.IsTrue(ReferenceEquals(s, StreetsTour.FromContext(r.ContextId)));
                var setup = MatchSetup.FromRequest(r, c);
                Assert.IsTrue(setup.StyleMeter);
            }
            // Later crews are better.
            Assert.Greater(c.Player(StreetsTour.Find("commissioner").LeaderId).attributes.Overall, c.Player(StreetsTour.Find("tollbooth").LeaderId).attributes.Overall + 15);
        }

        [Test]
        public void Tour_CitiesOpenInOrder_StarsAndRecruitsArePaidOnce()
        {
            var d = new StreetsSaveData();
            var career = new CareerSaveData();
            Assert.IsTrue(StreetsTour.CityOpen(d, 0));
            Assert.IsFalse(StreetsTour.CityOpen(d, 1));
            var first = StreetsTour.InCity(0);

            var loss = StreetsTour.Apply(d, career, first[0], false, 0, -3);
            Assert.AreEqual(0, loss.Stars);
            Assert.AreEqual(0, d.tourBeaten.Count);

            int sp0 = career.signalPoints;
            var r = StreetsTour.Apply(d, career, first[0], true, 0, 2);
            Assert.IsTrue(r.FirstWin);
            Assert.AreEqual(1, r.Stars);
            Assert.Greater(r.Sp, 0);
            Assert.AreEqual(sp0 + r.Sp, career.signalPoints);
            Assert.AreEqual(first[0].Leader, r.Recruit);
            Assert.Contains(first[0].LeaderId, d.recruits);

            var again = StreetsTour.Apply(d, career, first[0], true, 1, 9);
            Assert.IsFalse(again.FirstWin);
            Assert.AreEqual(3, again.Stars);
            Assert.AreEqual(2, again.NewStars);
            Assert.AreEqual(0, again.Sp, "SP only the first time");
            Assert.IsNull(again.Recruit);
            Assert.AreEqual(3, StreetsTour.Stars(d, first[0]));
            Assert.AreEqual(1, d.recruits.Count);

            StreetsTour.Apply(d, career, first[1], true, 0, 2);
            Assert.IsFalse(StreetsTour.CityOpen(d, 1));
            var clear = StreetsTour.Apply(d, career, first[2], true, 0, 2);
            Assert.IsTrue(clear.CityCleared);
            Assert.IsTrue(StreetsTour.CityOpen(d, 1));
            Assert.IsFalse(StreetsTour.CityOpen(d, 4), "the finale needs every city");
            Assert.Greater(d.rep, 0);
            Assert.AreEqual(StreetsTour.MaxStars, 39);
        }

        [Test]
        public void Crew_TakesOnlyYourRecruits_AndJoinsYourGames()
        {
            var c = DefaultContent.Create();
            var d = new StreetsSaveData();
            var stop = StreetsTour.Stops[0];
            Assert.IsFalse(StreetsTour.SetCrew(d, 0, stop.LeaderId), "not recruited yet");
            StreetsTour.Apply(d, new CareerSaveData(), stop, true, 0, 1);
            Assert.IsTrue(StreetsTour.SetCrew(d, 0, stop.LeaderId));
            Assert.IsTrue(StreetsTour.SetCrew(d, 1, stop.LeaderId), "moving him");
            Assert.AreEqual("", d.crew[0]);
            Assert.AreEqual(stop.LeaderId, d.crew[1]);
            var defs = StreetsTour.CrewDefs(c, d);
            Assert.IsNull(defs[0]);
            Assert.AreEqual(stop.LeaderId, defs[1].id);
            Assert.IsFalse(StreetsTour.SetCrew(d, 2, ""));
        }

        [Test]
        public void TourProgress_SavesAndLoads_AndDropsUnknownIds()
        {
            var c = DefaultContent.Create();
            var career = new CareerSaveData();
            var d = career.streets;
            StreetsTour.Apply(d, career, StreetsTour.Stops[0], true, 1, 8);
            StreetsTour.SetCrew(d, 0, StreetsTour.Stops[0].LeaderId);
            d.tourBeaten.Add("not.a.stop");
            d.recruits.Add("player.fake");
            d.liveWins = 4;
            var back = SaveCodec.Decode(SaveCodec.Encode(career), c, out _).streets;
            Assert.AreEqual(1, back.tourBeaten.Count);
            Assert.AreEqual(3, StreetsTour.Stars(back, StreetsTour.Stops[0]));
            Assert.AreEqual(1, back.recruits.Count);
            Assert.AreEqual(StreetsTour.Stops[0].LeaderId, back.crew[0]);
            Assert.AreEqual(d.rep, back.rep);
            Assert.AreEqual(4, back.liveWins);
        }

        [Test]
        public void OffTheGlass_BouncesOffTheBoard_AndFinishesYourOwnOop()
        {
            var c = DefaultContent.Create();
            var r = Streets.Pickup(c, new CareerSaveData(), null, null);
            r.Seed = 11;
            var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
            for (int i = 0; i < 240 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, new PlayerInput { Move = new Vec2(0f, -0.1f) });
            int me = m.ControlledIndex;
            Assert.IsTrue(m.HumanHasBall);
            var p = m.Players[me];
            p.Motion.position = m.Setup.Court.Hoop + new Vec2(0.3f, 3.2f);
            m.Players[me].Def.attributes.finishing = 90;
            Assert.IsTrue(m.CanGlassToss(me));
            var toHoop = (m.Setup.Court.Hoop - p.Position).Normalized;
            var picked = m.TrickFor(me, toHoop);
            Assert.IsTrue(picked == StreetTrick.OffTheGlass || picked == StreetTrick.OffTheHead, "at the rim: off the glass (or his head if he's in your face)");
            Assert.IsTrue(m.DoTrick(me, StreetTrick.OffTheGlass));
            Assert.IsTrue(m.GlassTossInFlight);
            var log = new List<MatchEvent>();
            for (int i = 0; i < 180 && log.FindIndex(e => e.Type == MatchEventType.AlleyOop) < 0; i++)
            {
                m.Step(Dt, new PlayerInput { Move = toHoop * 0.3f });
                log.AddRange(m.Events);
            }
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.BankKiss && e.PlayerIndex == me), "kissed the glass");
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.AlleyOop && e.PlayerIndex == me && e.Value == -1), "his own alley-oop");
        }

        [Test]
        public void TrickBall_MovesTheBallAroundTheHandler()
        {
            var c = DefaultContent.Create();
            var r = Streets.Pickup(c, new CareerSaveData(), null, null);
            r.Seed = 12;
            var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
            for (int i = 0; i < 240 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, new PlayerInput { Move = new Vec2(0f, -0.1f) });
            int me = m.ControlledIndex;
            Assert.IsTrue(m.DoTrick(me, StreetTrick.ThroughLegs));
            float low = 9f;
            for (int i = 0; i < 20; i++)
            {
                m.Step(Dt, default);
                if (m.Ball.IsHeld) low = System.Math.Min(low, m.Ball.Height);
            }
            for (int i = 0; i < 30; i++) m.Step(Dt, default);
            Assert.AreEqual(StreetTrick.None, m.TrickBallOf(me), "back to a normal dribble");
            Assert.Less(low, 0.45f, "through the legs goes low");
        }

        [Test]
        public void StreetsStyle_TravelsInTheLinkSetup_AndLiveStreetsHasItsOwnGroup()
        {
            var c = DefaultContent.Create();
            var league = c.TeamsInTier(TeamTier.League);
            var setup = LinkSetup.From(c, league[0].id, league[1].id, "court.pier_nine", DefaultContent.DefaultDifficultyId, 9, "1.0", "Host");
            setup.Style = LinkSetup.StreetsStyle;
            var back = LinkProtocol.ReadSetup(LinkProtocol.SetupMessage(setup));
            Assert.IsTrue(back.IsStreets);
            var req = back.ToRequest();
            Assert.IsTrue(req.StreetsStyle);
            Assert.AreEqual(Streets.RulesId, req.RulesId);
            Assert.IsTrue(back.Register(c));
            var ms = MatchSetup.FromRequest(req, c);
            Assert.IsTrue(ms.StyleMeter);
            Assert.IsFalse(ms.WinnersBall);
            Assert.IsTrue(ms.SecondHuman);

            setup.Style = "";
            Assert.IsFalse(LinkProtocol.ReadSetup(LinkProtocol.SetupMessage(setup)).IsStreets);
            Assert.IsFalse(MatchSetup.FromRequest(LinkProtocol.ReadSetup(LinkProtocol.SetupMessage(setup)).ToRequest(), c).StyleMeter);

            var hello = LinkProtocol.ReadHello(LinkProtocol.HelloMessage(new LiveOffer { AppVersion = "1.0", Name = "A", Rating = 1000, TeamData = "x", Style = LinkSetup.StreetsStyle }));
            Assert.AreEqual(LinkSetup.StreetsStyle, hello.Style);
            Assert.AreNotEqual(LiveMode.PlayerGroup("1.0"), LiveMode.PlayerGroup("1.0", true));
            Assert.AreEqual(LiveMode.PlayerGroup("1.0"), LiveMode.PlayerGroup("1.0", false));
        }

        [Test]
        public void TheMic_CallsTheStreets()
        {
            var c = DefaultContent.Create();
            var r = Streets.Pickup(c, new CareerSaveData(), null, null);
            r.Seed = 3;
            r.Coach = true;
            var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
            var mic = new Commentary("A", "B", 3);
            var lines = new List<string>();
            for (int i = 0; i < 60 * 60 * 20 && m.Phase != MatchPhase.Final; i++)
            {
                m.Step(Dt, default);
                var line = mic.React(m, k => m.Players[k].Def.firstName, m.Ball.ShotType);
                if (line != null) lines.Add(line);
            }
            Assert.IsTrue(lines.Exists(l => l.Contains("SHOWSTOPPER") || l.Contains("SHOW") || l.Contains("HANDLE") || l.Contains("BAG") || l.Contains("HEAD") || l.Contains("GLASS")),
                          "street lines: " + string.Join(" | ", lines));
        }
    }
}
