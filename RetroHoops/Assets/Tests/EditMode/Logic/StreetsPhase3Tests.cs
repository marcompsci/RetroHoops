using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>15 Oct 2026: THE STREETS phase 3 (HOMECOURT, crew chemistry, STREET GEAR, SIGNATURE SHOWSTOPPER).</summary>
    public class StreetsPhase3Tests
    {
        private const float Dt = 1f / 60f;

        private static void ClearCity(StreetsSaveData d, CareerSaveData career, int tier)
        {
            foreach (var s in StreetsTour.InCity(tier)) StreetsTour.Apply(d, career, s, true, 0, 1);
        }

        [Test]
        public void Homecourt_OpensAfterSouthside_AndClaimsOnlyRealCourts()
        {
            var c = DefaultContent.Create();
            var career = new CareerSaveData();
            var d = career.streets;
            Assert.IsFalse(StreetsHome.Unlocked(d));
            Assert.IsFalse(StreetsHome.Claim(d, c, career, Streets.Courts[0]), "locked");
            ClearCity(d, career, 0);
            Assert.IsTrue(StreetsHome.Unlocked(d));
            Assert.IsFalse(StreetsHome.Claim(d, c, career, "court.nope"));
            Assert.IsNull(StreetsHome.Defend(c, career), "nothing claimed yet");
            Assert.IsTrue(StreetsHome.Claim(d, c, career, Streets.Courts[2]));
            Assert.AreEqual(Streets.Courts[2], d.homeCourt);
            StreetsHome.NextName(d, -1);
            Assert.AreEqual(StreetsHome.Names[StreetsHome.Names.Length - 1], StreetsHome.Name(d));

            var r = StreetsHome.Defend(c, career);
            Assert.AreEqual(GameMode.Streets, r.Mode);
            Assert.AreEqual(Streets.Courts[2], r.CourtId);
            Assert.IsTrue(StreetsHome.IsHome(r));
            var challenger = StreetsTour.Find(StreetsHome.ChallengerOf(r.ContextId));
            Assert.IsNotNull(challenger);
            Assert.IsTrue(StreetsTour.CityOpen(d, challenger.Tier), "only crews from cities you've opened");
            Assert.AreEqual(0, r.StartMeterA, "no perks yet");
            var setup = MatchSetup.FromRequest(r, c);
            Assert.IsTrue(setup.StyleMeter);
            Assert.AreEqual(21, setup.Rules.targetScore);
        }

        [Test]
        public void Homecourt_DefensesBuildPerks_LosingTakesItUntilYouWinItBack()
        {
            var c = DefaultContent.Create();
            var career = new CareerSaveData();
            var d = career.streets;
            ClearCity(d, career, 0);
            StreetsHome.Claim(d, c, career, Streets.Courts[0]);

            int rep0 = d.rep, sp0 = career.signalPoints;
            var first = StreetsHome.Apply(d, career, StreetsHome.Challenger(d).Id, true);
            Assert.IsTrue(first.Defended);
            Assert.AreEqual("HOME CROWD", first.NewPerk);
            Assert.AreEqual(rep0 + first.Rep, d.rep);
            Assert.AreEqual(sp0 + first.Sp, career.signalPoints);
            Assert.AreEqual(MatchSimulation.MeterLevel / 4, StreetsHome.StartMeter(d));
            Assert.AreEqual(0f, StreetsHome.Boost(d));

            StreetsHome.Apply(d, career, StreetsHome.Challenger(d).Id, true);
            var third = StreetsHome.Apply(d, career, StreetsHome.Challenger(d).Id, true);
            Assert.AreEqual("BLOCK PARTY", third.NewPerk);
            Assert.AreEqual(0.1f, StreetsHome.Boost(d), 1e-6f);
            Assert.AreEqual(3, d.homeStreak);
            Assert.IsTrue(StreetsHome.BossNext(d) == false);
            StreetsHome.Apply(d, career, StreetsHome.Challenger(d).Id, true);
            Assert.IsTrue(StreetsHome.BossNext(d), "the fifth defense is a boss");
            var pool = StreetsHome.Pool(d);
            Assert.IsTrue(ReferenceEquals(pool[pool.Count - 1], StreetsHome.Challenger(d)));

            var taker = StreetsHome.Challenger(d);
            var loss = StreetsHome.Apply(d, career, taker.Id, false);
            Assert.IsTrue(loss.Taken);
            Assert.AreEqual(0, loss.Rep);
            Assert.IsTrue(d.homeTaken);
            Assert.AreEqual(0, d.homeStreak);
            Assert.AreEqual(0, StreetsHome.StartMeter(d), "perks off while it's taken");
            Assert.AreEqual(0f, StreetsHome.Boost(d));
            Assert.IsTrue(ReferenceEquals(taker, StreetsHome.Challenger(d)), "TAKE IT BACK is against the crew that took it");
            Assert.AreEqual(Streets.Courts[0], StreetsHome.Defend(c, career).CourtId, "still your court");

            var back = StreetsHome.Apply(d, career, taker.Id, true);
            Assert.IsTrue(back.Retaken);
            Assert.IsFalse(back.Defended);
            Assert.AreEqual(4, d.homeDefenses, "taking it back isn't a defense");
            Assert.IsFalse(d.homeTaken);
            Assert.Greater(StreetsHome.StartMeter(d), 0);
        }

        [Test]
        public void HomePerks_ReachTheGame_AndStartTheMeter()
        {
            var c = DefaultContent.Create();
            var career = new CareerSaveData();
            var d = career.streets;
            ClearCity(d, career, 0);
            StreetsHome.Claim(d, c, career, Streets.Courts[0]);
            d.homeDefenses = 6;
            var r = StreetsHome.Defend(c, career);
            Assert.AreEqual(MatchSimulation.MeterLevel / 2, r.StartMeterA);
            Assert.AreEqual(0.1f, r.StyleBoostA, 1e-6f);
            var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
            Assert.AreEqual(MatchSimulation.MeterLevel / 2, m.StyleMeter[0]);
            Assert.AreEqual(0, m.StyleMeter[1]);
            Assert.AreEqual(0, m.BreakerReady(0), "half a meter isn't a SHOWSTOPPER");

            // A request can't start anyone with a full meter.
            r.StartMeterA = 5000;
            Assert.Less(MatchSetup.FromRequest(r, c).StartMeterA, MatchSimulation.MeterLevel);
        }

        [Test]
        public void StyleBoost_FillsTheHomeMeterFaster()
        {
            var c = DefaultContent.Create();
            int Banked(float boost)
            {
                var r = Streets.Pickup(c, new CareerSaveData(), null, null);
                r.Seed = 21;
                r.Coach = true;
                r.StyleBoostA = boost;
                var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
                int total = 0;
                for (int i = 0; i < 60 * 60 * 6 && m.Phase != MatchPhase.Final; i++)
                {
                    m.Step(Dt, default);
                    foreach (var e in m.Events)
                        if (e.Type == MatchEventType.StyleBanked && e.Team == 0) total += e.Value;
                    if (total > 0 && m.StyleMeter[0] > 0) return m.StyleMeter[0] * 1000 / total;
                }
                return -1;
            }
            int plain = Banked(0f), boosted = Banked(0.2f);
            Assert.Greater(plain, 0);
            Assert.Greater(boosted, plain, "the same banked style puts more into a boosted meter");
        }

        [Test]
        public void Chemistry_GrowsWithGamesTogether_WinsCountDouble()
        {
            var c = DefaultContent.Create();
            var career = new CareerSaveData();
            var d = career.streets;
            var stop = StreetsTour.Stops[0];
            StreetsTour.Apply(d, career, stop, true, 0, 1);
            Assert.AreEqual(0, StreetsChem.CrewLevel(d));
            Assert.AreEqual(0, StreetsChem.AddGame(d, true).Count, "nobody in the crew yet");
            StreetsTour.SetCrew(d, 0, stop.LeaderId);
            var up = StreetsChem.AddGame(d, true);
            Assert.AreEqual(0, up.Count);
            Assert.AreEqual(2, StreetsChem.Points(d, stop.LeaderId));
            up = StreetsChem.AddGame(d, false);
            Assert.AreEqual(1, up.Count, "3 points = level 1");
            Assert.AreEqual(1, StreetsChem.Level(d, stop.LeaderId));
            Assert.AreEqual(5, StreetsChem.ToNext(d, stop.LeaderId));
            Assert.AreEqual(1, StreetsChem.CrewLevel(d));
            Assert.AreEqual(0.01f, StreetsChem.Bonus(d), 1e-6f);
            for (int i = 0; i < 30; i++) StreetsChem.AddGame(d, true);
            Assert.AreEqual(StreetsChem.MaxLevel, StreetsChem.Level(d, stop.LeaderId));
            Assert.AreEqual(0, StreetsChem.ToNext(d, stop.LeaderId));
            Assert.LessOrEqual(StreetsChem.Boost(d), 0.1f);
            // Benched: his chemistry stays, but it doesn't count for the crew.
            StreetsTour.SetCrew(d, 0, "");
            Assert.AreEqual(0, StreetsChem.CrewLevel(d));
            Assert.AreEqual(StreetsChem.MaxLevel, StreetsChem.Level(d, stop.LeaderId));
        }

        [Test]
        public void Gear_UnlocksFromTheTour_AndMakesAStreetKit()
        {
            var c = DefaultContent.Create();
            var career = new CareerSaveData();
            var d = career.streets;
            var ids = new HashSet<string>();
            foreach (var g in StreetsGear.Sets) Assert.IsTrue(ids.Add(g.Id), g.Id);
            Assert.AreEqual(StreetsTour.Stops.Length + 3, StreetsGear.Sets.Count);
            Assert.AreEqual(1, StreetsGear.UnlockedCount(d), "BLACKTOP from the start");
            Assert.IsNull(StreetsGear.Worn(d));

            StreetsGear.Next(d, 1);
            Assert.AreEqual("blacktop", d.gear);
            StreetsGear.Next(d, 1);
            Assert.AreEqual("", d.gear, "back to your usual kit");

            var stop = StreetsTour.Stops[0];
            StreetsTour.Apply(d, career, stop, true, 0, 1);
            var crewGear = StreetsGear.Find("crew." + stop.Id);
            Assert.IsTrue(StreetsGear.Unlocked(d, crewGear));
            d.gear = crewGear.Id;
            Assert.IsTrue(ReferenceEquals(crewGear, StreetsGear.Worn(d)));
            var kit = StreetsGear.Kit(crewGear);
            var team = StreetsTour.Register(c, stop);
            Assert.AreEqual(Kits.Pack(team.primary), kit.jersey, "their colors");
            Assert.AreEqual((int)team.pattern, kit.pattern);
            Assert.AreEqual((int)JerseyCut.Tee, kit.cut);

            d.gear = "legend";
            Assert.IsNull(StreetsGear.Worn(d), "not earned: counts as your usual kit");
        }

        [Test]
        public void Signature_OpensWithRep()
        {
            var d = new StreetsSaveData();
            Assert.IsNull(StreetsSignature.Chosen(d));
            Assert.AreEqual(2, StreetsSignature.UnlockedCount(d));
            StreetsSignature.Next(d, 1);
            Assert.AreEqual(DunkStyle.TwoHand, StreetsSignature.Chosen(d));
            d.signature = (int)DunkStyle.Skyline;
            Assert.IsNull(StreetsSignature.Chosen(d), "not open yet");
            d.rep = StreetsTour.RepNeeded[4];
            Assert.AreEqual(DunkStyle.Skyline, StreetsSignature.Chosen(d));
            Assert.AreEqual(StreetsSignature.Order.Length, StreetsSignature.UnlockedCount(d));
            Assert.AreEqual("SKYLINE SLAM", StreetsSignature.Label(d));
        }

        [Test]
        public void Phase3_SavesAndLoads_AndDropsBadValues()
        {
            var c = DefaultContent.Create();
            var career = new CareerSaveData();
            var d = career.streets;
            ClearCity(d, career, 0);
            StreetsHome.Claim(d, c, career, Streets.Courts[3]);
            d.homeName = 4;
            StreetsHome.Apply(d, career, StreetsHome.Challenger(d).Id, true);
            StreetsHome.Apply(d, career, "tollbooth", false);
            StreetsTour.SetCrew(d, 0, StreetsTour.Stops[0].LeaderId);
            StreetsChem.AddGame(d, true);
            d.chem.Add("player.fake=9");
            d.gear = "crew." + StreetsTour.Stops[0].Id;
            d.signature = (int)DunkStyle.Tomahawk;

            var back = SaveCodec.Decode(SaveCodec.Encode(career), c, out _).streets;
            Assert.AreEqual(Streets.Courts[3], back.homeCourt);
            Assert.AreEqual(4, back.homeName);
            Assert.AreEqual(1, back.homeDefenses);
            Assert.AreEqual(1, back.homeLosses);
            Assert.IsTrue(back.homeTaken);
            Assert.AreEqual("tollbooth", back.homeTaker);
            Assert.AreEqual(1, back.chem.Count, "only recruits keep chemistry");
            Assert.AreEqual(2, StreetsChem.Points(back, StreetsTour.Stops[0].LeaderId));
            Assert.AreEqual(d.gear, back.gear);
            Assert.AreEqual((int)DunkStyle.Tomahawk, back.signature);

            var raw = SaveCodec.Encode(career).Replace("\"tollbooth\"", "\"nobody\"").Replace("\"" + d.gear + "\"", "\"crew.fake\"");
            var bad = SaveCodec.Decode(raw, c, out _).streets;
            Assert.IsFalse(bad.homeTaken, "a taker that doesn't exist can't hold your court");
            Assert.AreEqual("", bad.gear);
        }

        [Test]
        public void HitStop_HoldsOnAShowstopperBucket()
        {
            var e = new MatchEvent(MatchEventType.BreakerScored, 0, 0, 1);
            Assert.AreEqual(HitStop.Showstopper, HitStop.For(e, ShotType.Layup, false));
            Assert.AreEqual(0f, HitStop.For(e, ShotType.Layup, true));
        }
    }
}
