using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>16 Oct 2026: BALLER CITY — maps, walking, interaction, LEAVE COURT, saves, people and activities.</summary>
    public class BallerCityTests
    {
        private static ContentCatalog C => _c ??= DefaultContent.Create();
        private static ContentCatalog _c;

        // ------------------------------------------------------------------ maps

        [Test]
        public void EveryDistrict_HasWalkableSpawns_AndEverythingInBounds()
        {
            Assert.AreEqual(5, CityDistricts.All.Length);
            foreach (var def in CityDistricts.All)
            {
                var m = CityDistricts.Map(def.Id);
                Assert.IsNotNull(m, def.Id);
                Assert.AreEqual(def.Id, m.DistrictId);
                Assert.IsTrue(ReferenceEquals(m, CityDistricts.Map(def.Id)), "maps are built once and cached");
                Assert.Greater(m.Spawns.Count, 0, def.Id);
                foreach (var s in m.Spawns)
                    Assert.IsFalse(CityMovement.Overlaps(m, s.Position.x, s.Position.y), def.Id + " spawn " + s.Id + " is inside something");
                foreach (var p in m.Portals)
                {
                    Assert.IsTrue(p.X >= 0 && p.Y >= 0 && p.X + p.W <= m.Width && p.Y + p.H <= m.Height, def.Id + " portal " + p.Id + " out of bounds");
                    Assert.IsFalse(string.IsNullOrEmpty(p.Label), p.Id);
                }
                foreach (var c in m.Courts)
                    Assert.IsTrue(c.X >= 0 && c.Y >= 0 && c.X + c.W <= m.Width && c.Y + c.H <= m.Height, c.Id);
                foreach (var b in m.Buildings)
                    Assert.IsTrue(b.X >= 0 && b.Y >= 0 && b.X + b.W <= m.Width && b.Y + b.H <= m.Height, b.Id);
                foreach (var kv in m.Hangouts)
                    Assert.IsTrue(m.Walkable(kv.Value), def.Id + " hangout " + kv.Key + " isn't walkable");
                CollectionAssert.AllItemsAreUnique(m.Spawns.ConvertAll(s => s.Id), def.Id + " spawn ids");
                CollectionAssert.AllItemsAreUnique(m.Portals.ConvertAll(p => p.Id), def.Id + " portal ids");
            }
        }

        [Test]
        public void EveryPortal_LeadsSomewhereReal_AndEveryDoorIsReachable()
        {
            foreach (var def in CityDistricts.All)
            {
                var m = CityDistricts.Map(def.Id);
                foreach (var p in m.Portals)
                {
                    if (p.Kind == CityPortalKind.District || p.Kind == CityPortalKind.Exit
                        || (p.Kind == CityPortalKind.Building && CityDistricts.Find(p.Target) != null))
                    {
                        var target = CityDistricts.Map(p.Target);
                        Assert.IsNotNull(target, p.Id + " → " + p.Target);
                        Assert.IsTrue(target.TryGetSpawn(p.TargetSpawn, out _), p.Id + " → missing spawn " + p.TargetSpawn);
                    }
                    // From the district's first spawn, you can walk to every portal (nothing is walled off).
                    var spot = m.NearestWalkable(p.Center);
                    var path = CityPath.Find(m, m.DefaultSpawn.Position, spot, m.Width * m.Height);
                    Assert.IsTrue(path.Count > 0 || Vec2.Distance(spot, m.DefaultSpawn.Position) < 1f, def.Id + ": can't walk to " + p.Id);
                }
            }
        }

        [Test]
        public void CityCourts_PointAtRealCourts()
        {
            foreach (var def in CityDistricts.All)
                foreach (var c in CityDistricts.Map(def.Id).Courts)
                {
                    if (c.CourtId == "court.first_callers_home" || c.CourtId == "court.practice_gym") continue; // the city's own
                    Assert.IsNotNull(C.Court(c.CourtId), c.Id + " → " + c.CourtId);
                }
            Assert.IsTrue(CityDistricts.Map(CityDistricts.Blacktop).Court("court.overpass_park") != null);
        }

        [Test]
        public void GymStations_CoverEveryPracticeDrill_AndSitOnTheFloor()
        {
            var m = CityDistricts.Map(CityDistricts.Gym);
            Assert.IsTrue(m.Interior);
            foreach (DrillKind k in new[] { DrillKind.FreeShoot, DrillKind.PassingTargets, DrillKind.DribbleLane, DrillKind.ThreePoint, DrillKind.AroundTheWorld })
            {
                var p = m.Portals.Find(x => x.Target == "drill:" + k);
                Assert.IsNotNull(p, k.ToString());
                Assert.AreEqual(CityPortalKind.Station, p.Kind);
                Assert.AreEqual(CityGround.Hardwood, m.Get((int)p.Center.x, (int)p.Center.y), k + " station off the floor");
            }
            Assert.IsNotNull(m.Portals.Find(x => x.Target == "lockers"));
            Assert.IsNotNull(m.Portals.Find(x => x.Target == "halloween"));
            Assert.AreEqual("LEAVE BUILDING", m.Portal("door.exit").Label);
        }

        // ------------------------------------------------------------------ walking

        [Test]
        public void Walking_ReachesTopSpeed_SprintIsFaster_AndStopsSmoothly()
        {
            var m = new CityMap("t", 60, 10, CityGround.Sidewalk);
            var b = new CityBody { Position = new Vec2(5f, 5f) };
            for (int i = 0; i < 60; i++) CityMovement.Step(ref b, m, new Vec2(1f, 0f), false, 1f / 60f);
            Assert.AreEqual(CityMovement.WalkSpeed, b.Velocity.x, 0.01);
            Assert.AreEqual(3, b.Facing);
            for (int i = 0; i < 60; i++) CityMovement.Step(ref b, m, new Vec2(1f, 0f), true, 1f / 60f);
            Assert.AreEqual(CityMovement.SprintSpeed, b.Velocity.x, 0.01);
            CityMovement.Step(ref b, m, Vec2.Zero, false, 1f / 60f);
            Assert.Greater(b.Velocity.x, 0f, "eases out, no instant stop");
            for (int i = 0; i < 60; i++) CityMovement.Step(ref b, m, Vec2.Zero, false, 1f / 60f);
            Assert.AreEqual(0f, b.Velocity.Magnitude, 0.001);
            Assert.Greater(b.Stride, 0f);
        }

        [Test]
        public void Walls_StopYou_AndYouSlideAlongThem()
        {
            var m = new CityMap("t", 20, 20, CityGround.Sidewalk);
            for (int y = 0; y < 20; y++) m.Set(10, y, CityGround.Wall);
            var b = new CityBody { Position = new Vec2(8f, 5f) };
            for (int i = 0; i < 240; i++) CityMovement.Step(ref b, m, new Vec2(1f, 1f), true, 1f / 60f);
            Assert.Less(b.Position.x, 10f - CityMovement.HalfW + 0.01f, "never into the wall");
            Assert.Greater(b.Position.y, 10f, "slid up along it");
            // Huge frame hitch: still doesn't tunnel through.
            b = new CityBody { Position = new Vec2(8f, 5f), Velocity = new Vec2(200f, 0f) };
            CityMovement.MoveAndSlide(ref b, m, new Vec2(50f, 0f));
            Assert.Less(b.Position.x, 10f);
            // Off the edge of the map counts as a wall.
            b = new CityBody { Position = new Vec2(1f, 1f) };
            for (int i = 0; i < 120; i++) CityMovement.Step(ref b, m, new Vec2(-1f, -1f), true, 1f / 60f);
            Assert.GreaterOrEqual(b.Position.x, CityMovement.HalfW - 0.01f);
            Assert.GreaterOrEqual(b.Position.y, CityMovement.HalfH - 0.01f);
        }

        [Test]
        public void Settle_MovesASavedSpotOutOfAWall()
        {
            var m = CityDistricts.Map(CityDistricts.FirstCallers);
            var hq = m.Buildings.Find(b => b.Id == "hq");
            var inside = new Vec2(hq.X + hq.W * 0.5f, hq.Y + hq.H * 0.5f);
            Assert.IsTrue(CityMovement.Overlaps(m, inside.x, inside.y));
            var safe = CityMovement.Settle(m, inside);
            Assert.IsTrue(m.Walkable(safe));
            Assert.IsTrue(CityMovement.Settle(m, new Vec2(float.NaN, 3f)).x >= 0f);
        }

        [Test]
        public void Camera_StaysInsideTheDistrict_AndCentresSmallRooms()
        {
            var c = CityCamera.Clamp(new Vec2(0f, 0f), 7f, 4f, 36, 44);
            Assert.AreEqual(7f, c.x, 1e-4);
            Assert.AreEqual(4f, c.y, 1e-4);
            c = CityCamera.Clamp(new Vec2(100f, 100f), 7f, 4f, 36, 44);
            Assert.AreEqual(29f, c.x, 1e-4);
            Assert.AreEqual(40f, c.y, 1e-4);
            c = CityCamera.Clamp(new Vec2(3f, 3f), 20f, 4f, 24, 30);
            Assert.AreEqual(12f, c.x, 1e-4, "room narrower than the view: centred");
            var f = CityCamera.Follow(new Vec2(10f, 10f), new Vec2(20f, 10f), 7f, 4f, 36, 44, 1f / 60f);
            Assert.Greater(f.x, 10f);
            Assert.Less(f.x, 20f, "eases, doesn't jump");
            Assert.AreEqual(20f, CityCamera.Follow(new Vec2(10f, 10f), new Vec2(20f, 10f), 7f, 4f, 36, 44, 0f, snap: true).x, 1e-4);
        }

        // ------------------------------------------------------------------ interaction

        [Test]
        public void Interaction_PicksTheNearestThing_AndRoadsAreNotPrompts()
        {
            var m = CityDistricts.Map(CityDistricts.FirstCallers);
            var court = m.Portal("court.first_callers");
            Assert.IsNotNull(court);
            var on = court.Center;
            var p = CityInteraction.Find(m, on, null);
            Assert.IsTrue(ReferenceEquals(court, p.Portal));
            Assert.AreEqual("ENTER COURT", p.Label);

            // An NPC right next to you doesn't steal the prompt from the court you're standing on...
            p = CityInteraction.Find(m, on, new List<Vec2> { on + new Vec2(0.8f, 0f) });
            Assert.IsTrue(ReferenceEquals(court, p.Portal));
            // ...but away from any door, they're who you talk to.
            var open = FindOpenSpot(m);
            p = CityInteraction.Find(m, open, new List<Vec2> { open + new Vec2(3f, 0f), open + new Vec2(1f, 0f) });
            Assert.AreEqual(1, p.Npc);
            Assert.AreEqual("TALK", p.Label);
            Assert.IsTrue(CityInteraction.Find(m, open, new List<Vec2> { open + new Vec2(5f, 0f) }).IsNone);

            var road = m.Portals.Find(x => x.Kind == CityPortalKind.District);
            Assert.IsNotNull(road);
            Assert.IsTrue(CityInteraction.Find(m, road.Center, null).Portal?.Kind != CityPortalKind.District);
            Assert.IsTrue(ReferenceEquals(road, CityInteraction.RoadAt(m, road.Center)));
        }

        private static Vec2 FindOpenSpot(CityMap m)
        {
            for (int y = 2; y < m.Height - 2; y++)
                for (int x = 2; x < m.Width - 6; x++)
                {
                    var v = new Vec2(x + 0.5f, y + 0.5f);
                    bool clear = true;
                    for (int k = 0; k <= 4 && clear; k++) clear = !CityMovement.Overlaps(m, v.x + k, v.y);
                    if (clear && CityInteraction.Find(m, v, null).IsNone && CityInteraction.Find(m, v + new Vec2(3f, 0f), null).IsNone) return v;
                }
            Assert.Fail("no open spot");
            return Vec2.Zero;
        }

        // ------------------------------------------------------------------ LEAVE COURT

        [Test]
        public void LeaveCourt_FinishedGame_GoesStraightThrough()
        {
            var t = new CityTransition();
            Assert.AreEqual(CityTransition.Check.Ok, t.Request(false, CityDistricts.Blacktop, "court.sunset_cage"));
            Assert.AreEqual(CityTransitionState.ExitRequested, t.State);
            Assert.IsTrue(t.Validate(false));
            Assert.AreEqual(CityTransitionState.Saving, t.State);
            t.Saved(true);
            Assert.AreEqual(CityTransitionState.WalkOff, t.State);
            t.WalkedOff();
            Assert.AreEqual(CityTransitionState.LoadingCity, t.State);
            t.CityLoaded();
            Assert.AreEqual(CityTransitionState.Spawn, t.State);
            t.Spawned();
            Assert.AreEqual(CityTransitionState.FreeRoam, t.State);
            Assert.AreEqual(CityDistricts.Blacktop, t.District);
            Assert.AreEqual("court.sunset_cage", t.Spawn);
        }

        [Test]
        public void LeaveCourt_LiveGame_NeedsConfirmation_AndNoCancelsIt()
        {
            var t = new CityTransition();
            Assert.AreEqual(CityTransition.Check.NeedsConfirmation, t.Request(true, CityDistricts.FirstCallers, "court.first_callers"));
            Assert.IsFalse(t.Validate(false), "not confirmed");
            Assert.AreEqual(CityTransitionState.Idle, t.State);
            Assert.AreEqual(CityTransition.Check.NeedsConfirmation, t.Request(true, CityDistricts.FirstCallers, "court.first_callers"));
            Assert.IsTrue(t.Cancel());
            Assert.AreEqual(CityTransitionState.Idle, t.State);
            t.Request(true, CityDistricts.FirstCallers, null);
            Assert.IsTrue(t.Validate(true));
            Assert.IsFalse(t.Cancel(), "can't cancel once the save started");
            Assert.AreEqual(CityTransitionState.Saving, t.State);
        }

        [Test]
        public void LeaveCourt_RefusesASecondRequest_AndIgnoresStepsOutOfOrder()
        {
            var t = new CityTransition();
            t.Request(false, CityDistricts.FirstCallers, "x");
            Assert.AreEqual(CityTransition.Check.Busy, t.Request(false, CityDistricts.Blacktop, "y"));
            Assert.AreEqual(CityDistricts.FirstCallers, t.District, "the first request stands");
            t.WalkedOff();
            t.CityLoaded();
            t.Spawned();
            Assert.AreEqual(CityTransitionState.ExitRequested, t.State, "later steps can't skip ahead");
            t.Validate(false);
            t.Saved(true);
            Assert.AreEqual(CityTransition.Check.Busy, t.Request(false, CityDistricts.FirstCallers, "x"), "busy during the walk-off");
            Assert.IsFalse(t.Validate(true));
        }

        [Test]
        public void LeaveCourt_FailedSave_RecoversToIdle_WithAReason()
        {
            var t = new CityTransition();
            t.Request(false, "nowhere", "x");
            Assert.AreEqual(CityDistricts.FirstCallers, t.District, "unknown districts fall back to home");
            t.Validate(false);
            t.Saved(false);
            Assert.AreEqual(CityTransitionState.Failed, t.State);
            Assert.IsFalse(string.IsNullOrEmpty(t.Error));
            t.WalkedOff();
            Assert.AreEqual(CityTransitionState.Failed, t.State, "no walking off after a failed save");
            t.Recover();
            Assert.AreEqual(CityTransitionState.Idle, t.State);
            Assert.AreEqual(CityTransition.Check.Ok, t.Request(false, CityDistricts.FirstCallers, "x"), "can try again");
            Assert.IsNull(t.Error);
        }

        [Test]
        public void ExitFor_SendsYouToTheRightCourt()
        {
            var rise = new MatchRequest { Mode = GameMode.Rise, CourtId = "court.overpass_park" };
            Assert.AreEqual((CityDistricts.FirstCallers, "court.first_callers"), CityDistricts.ExitFor(rise));
            var street = new MatchRequest { Mode = GameMode.QuickCall, CourtId = "court.pier_nine" };
            var home = CityDistricts.ExitFor(street);
            Assert.AreEqual(CityDistricts.Blacktop, home.district);
            Assert.IsTrue(CityDistricts.Map(home.district).TryGetSpawn(home.spawn, out _));
            street.ReturnTo = CityDistricts.ReturnTag(CityDistricts.Gym, "gym.halloween");
            Assert.AreEqual((CityDistricts.Gym, "gym.halloween"), CityDistricts.ExitFor(street), "ReturnTo wins");
            Assert.AreEqual((CityDistricts.FirstCallers, "court.first_callers"), CityDistricts.ExitFor(new MatchRequest { CourtId = "court.nowhere" }));
            Assert.AreEqual((CityDistricts.FirstCallers, "court.first_callers"), CityDistricts.ExitFor(null));

            Assert.IsTrue(CityDistricts.TryParseReturn("city:blacktop:court.pier_nine", out var d, out var s));
            Assert.AreEqual("blacktop", d);
            Assert.AreEqual("court.pier_nine", s);
            Assert.IsFalse(CityDistricts.TryParseReturn("city:mars:x", out _, out _));
            Assert.IsFalse(CityDistricts.TryParseReturn("menu", out _, out _));
            Assert.IsFalse(CityDistricts.TryParseReturn(null, out _, out _));
        }

        [Test]
        public void Arrive_PutsYouAtTheGate_AndSurvivesASave_WithNothingElseChanged()
        {
            var d = new CareerSaveData();
            d.signalPoints = 120;
            d.fans = 33;
            var exit = CityDistricts.ExitFor(new MatchRequest { Mode = GameMode.QuickCall, CourtId = "court.sunset_cage" });
            d.city.Arrive(exit.district, exit.spawn);
            Assert.AreEqual(CityDistricts.Blacktop, d.city.district);
            var m = CityDistricts.Map(d.city.district);
            Assert.IsTrue(m.TryGetSpawn(exit.spawn, out var sp));
            Assert.AreEqual(sp.Position.x, d.city.x, 1e-4);
            Assert.IsFalse(CityMovement.Overlaps(m, d.city.x, d.city.y));
            var back = SaveCodec.Decode(SaveCodec.Encode(d), C, out _);
            Assert.AreEqual(CityDistricts.Blacktop, back.city.district);
            Assert.AreEqual(d.city.y, back.city.y, 1e-4);
            Assert.AreEqual(120, back.signalPoints, "walking off awards nothing");
            Assert.AreEqual(33, back.fans);

            d.city.Arrive("mars", "nowhere");
            Assert.AreEqual(CityDistricts.FirstCallers, d.city.district, "unknown: home");
            Assert.IsTrue(d.city.HasPosition);
            d.city.fps30 = true;
            Assert.IsTrue(SaveCodec.Decode(SaveCodec.Encode(d), C, out _).city.fps30);
        }

        // ------------------------------------------------------------------ saves

        [Test]
        public void Save_RoundTripsTheCity()
        {
            var d = new CareerSaveData();
            d.city.district = CityDistricts.Blacktop;
            d.city.x = 12.25f;
            d.city.y = 30.5f;
            d.city.facing = 2;
            d.city.lastCourt = "court.pier_nine";
            d.city.onboarding = 2;
            d.city.visits = 7;
            d.city.seen.Add(CityDistricts.FirstCallers);
            d.city.seen.Add(CityDistricts.Blacktop);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), C, out var status);
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.AreEqual(CityDistricts.Blacktop, back.city.district);
            Assert.AreEqual(12.25f, back.city.x, 1e-4);
            Assert.AreEqual(30.5f, back.city.y, 1e-4);
            Assert.AreEqual(2, back.city.facing);
            Assert.AreEqual("court.pier_nine", back.city.lastCourt);
            Assert.AreEqual(2, back.city.onboarding);
            Assert.AreEqual(7, back.city.visits);
            Assert.AreEqual(2, back.city.seen.Count);
        }

        [Test]
        public void Save_OlderSavesWithoutACity_StartAtHome_AndKeepEverythingElse()
        {
            var d = new CareerSaveData();
            d.signalPoints = 345;
            string json = SaveCodec.Encode(d);
            Assert.IsTrue(json.Contains("\"city\""));
            string old = json.Replace("\"city\"", "\"zzcity\""); // what a save from before BALLER CITY looks like
            var back = SaveCodec.Decode(old, C, out var status);
            Assert.AreNotEqual(LoadStatus.Recovered, status);
            Assert.AreEqual(345, back.signalPoints);
            Assert.IsNotNull(back.city);
            Assert.AreEqual(CityDistricts.FirstCallers, back.city.district);
            Assert.IsFalse(back.city.HasPosition);
            Assert.AreEqual(0, back.city.onboarding);
        }

        [Test]
        public void Save_BadCityValues_FallBackSafely()
        {
            var d = new CareerSaveData();
            d.city.district = "mars";
            d.city.x = float.NaN;
            d.city.y = 4f;
            d.city.facing = 9;
            d.city.onboarding = 50;
            d.city.visits = -3;
            d.city.seen.Add("mars");
            d.city.seen.Add(CityDistricts.Gym);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), C, out _);
            Assert.AreEqual(CityDistricts.FirstCallers, back.city.district);
            Assert.IsFalse(back.city.HasPosition, "NaN is 'no position', not (0, 4)");
            Assert.AreEqual(3, back.city.facing);
            Assert.AreEqual(CityOnboarding.Steps, back.city.onboarding);
            Assert.AreEqual(0, back.city.visits);
            Assert.AreEqual(1, back.city.seen.Count);

            d.city.x = 90000f;
            d.city.y = 3f;
            Assert.IsFalse(SaveCodec.Decode(SaveCodec.Encode(d), C, out _).city.HasPosition, "way off the map");

            string json = SaveCodec.Encode(new CareerSaveData());
            int at = json.IndexOf("\"city\"", StringComparison.Ordinal);
            string broken = json.Substring(0, at) + "\"city\":\"garbage\"," + json.Substring(at).Replace("\"city\"", "\"zzcity\"");
            var b2 = SaveCodec.Decode(broken, C, out var st);
            Assert.AreNotEqual(LoadStatus.Recovered, st, "a bad city block doesn't cost the career");
            Assert.AreEqual(CityDistricts.FirstCallers, b2.city.district);
        }

        // ------------------------------------------------------------------ people

        [Test]
        public void Cast_HasTeammatesAndRivals_AndEveryHauntIsARealSpot()
        {
            var d = new CareerSaveData();
            var cast = CityNpcScheduler.Cast(d, C);
            Assert.Greater(cast.FindAll(n => n.Role == CityNpcRole.Rival).Count, 0);
            Assert.Greater(cast.FindAll(n => n.Role == CityNpcRole.Legend).Count, 0);
            CollectionAssert.AllItemsAreUnique(cast.ConvertAll(n => n.Id));
            foreach (var n in cast)
            {
                Assert.IsNotNull(n.Player, n.Id);
                Assert.IsFalse(string.IsNullOrEmpty(n.Name), n.Id);
                foreach (var h in n.Haunts)
                {
                    int slash = h.IndexOf('/');
                    Assert.Greater(slash, 0, h);
                    var m = CityDistricts.Map(h.Substring(0, slash));
                    Assert.IsNotNull(m, h);
                    Assert.IsTrue(m.Hangouts.ContainsKey(h.Substring(slash + 1)), n.Id + ": no hangout " + h);
                }
            }
        }

        [Test]
        public void Schedule_IsTheSameEveryLaunch_OnePlacePerPerson_AndQuieterAtNight()
        {
            var cast = CityNpcScheduler.Cast(new CareerSaveData(), C);
            int day = CityNpcScheduler.DayNumber(new DateTime(2026, 10, 16));
            Assert.AreEqual(CityNpcScheduler.DayNumber(new DateTime(2026, 10, 16, 23, 59, 0)), day);
            int dayOut = 0, nightOut = 0;
            for (int dd = 0; dd < 30; dd++)
                foreach (var n in cast)
                {
                    var a = CityNpcScheduler.Place(n, day + dd, CityTimeOfDay.Afternoon);
                    Assert.AreEqual(a.ToString(), CityNpcScheduler.Place(n, day + dd, CityTimeOfDay.Afternoon).ToString(), "deterministic");
                    if (!a.Away) dayOut++;
                    if (!CityNpcScheduler.Place(n, day + dd, CityTimeOfDay.Night).Away) nightOut++;
                }
            Assert.Greater(dayOut, nightOut * 2, "most people go home at night");

            // Each person is in at most one district at a time.
            foreach (var t in new[] { CityTimeOfDay.Morning, CityTimeOfDay.Evening })
            {
                var seen = new HashSet<string>();
                foreach (var def in CityDistricts.All)
                    foreach (var (npc, spot) in CityNpcScheduler.InDistrict(cast, def.Id, day, t))
                    {
                        Assert.IsTrue(seen.Add(npc.Id), npc.Id + " is in two places");
                        Assert.IsTrue(CityDistricts.Map(def.Id).Walkable(spot));
                    }
            }
        }

        [Test]
        public void NpcBrain_WandersWithoutLeavingTheMapOrEnteringWalls_AndStopsToTalk()
        {
            var m = CityDistricts.Map(CityDistricts.FirstCallers);
            var anchor = m.Hangouts["hq.front"];
            var brain = new CityNpcBrain(anchor, 42u, false, m);
            float t = 0f;
            bool walked = false;
            for (int i = 0; i < 60 * 40; i++)
            {
                t += 1f / 60f;
                brain.Step(m, t, 1f / 60f, new Vec2(-50f, -50f), false);
                Assert.IsFalse(CityMovement.Overlaps(m, brain.Body.Position.x, brain.Body.Position.y), "inside a wall at " + brain.Body.Position);
                walked |= brain.Walking;
            }
            Assert.IsTrue(walked, "they move around");
            var before = brain.Body.Position;
            for (int i = 0; i < 60; i++) brain.Step(m, t += 1f / 60f, 1f / 60f, before + new Vec2(1f, 0f), true);
            Assert.AreEqual(CityNpcActivity.Talk, brain.Activity);
            Assert.Less(Vec2.Distance(before, brain.Body.Position), 0.5f, "stands still to talk");
        }

        [Test]
        public void Paths_GoAroundWalls_AndGiveUpCleanly()
        {
            var m = new CityMap("t", 12, 12, CityGround.Sidewalk);
            for (int y = 0; y < 10; y++) m.Set(6, y, CityGround.Wall);
            var path = CityPath.Find(m, new Vec2(2.5f, 2.5f), new Vec2(9.5f, 2.5f), 500);
            Assert.Greater(path.Count, 7);
            foreach (var p in path) Assert.IsTrue(m.Walkable(p));
            Assert.AreEqual(9.5f, path[path.Count - 1].x, 1e-4);
            Assert.AreEqual(0, CityPath.Find(m, new Vec2(2.5f, 2.5f), new Vec2(6.5f, 2.5f), 500).Count, "goal in a wall");
            Assert.AreEqual(0, CityPath.Find(m, new Vec2(2.5f, 2.5f), new Vec2(9.5f, 2.5f), 10).Count, "search budget");
            m.Set(6, 10, CityGround.Wall);
            m.Set(6, 11, CityGround.Wall);
            Assert.AreEqual(0, CityPath.Find(m, new Vec2(2.5f, 2.5f), new Vec2(9.5f, 2.5f), 500).Count, "walled off");
        }

        [Test]
        public void Dialogue_IsStableForTheDay_AndUsesTheRealRecord()
        {
            var d = new CareerSaveData();
            Assert.IsNull(CityDialogue.Record(d));
            var cast = CityNpcScheduler.Cast(d, C);
            foreach (var n in cast)
            {
                string a = CityDialogue.Line(n, d, 9000);
                Assert.IsFalse(string.IsNullOrEmpty(a), n.Id);
                Assert.AreEqual(a, CityDialogue.Line(n, d, 9000));
            }
            Assert.AreEqual("", CityDialogue.Line(null, d, 1));
        }

        // ------------------------------------------------------------------ activities

        [Test]
        public void StreetCourt_OffersPickup_AndEveryGameReturnsToTheCity()
        {
            var d = new CareerSaveData();
            var m = CityDistricts.Map(CityDistricts.Blacktop);
            foreach (var court in m.Courts)
            {
                var list = CityActivities.ForCourt(m, court, d, C, 9000);
                Assert.Greater(list.Count, 0, court.Id);
                Assert.IsTrue(list[0].Primary);
                Assert.AreEqual("PICKUP 3 ON 3", list[0].Label);
                foreach (var a in list)
                {
                    if (a.Locked != null) { Assert.IsNull(a.Request, a.Label + ": locked choices don't start games"); continue; }
                    if (a.Action != CityAction.PlayMatch) continue;
                    Assert.IsNotNull(a.Request, a.Label);
                    Assert.IsTrue(CityDistricts.TryParseReturn(a.Request.ReturnTo, out var dd, out var ss), a.Label);
                    Assert.AreEqual(CityDistricts.Blacktop, dd);
                    Assert.AreEqual(court.Id, ss);
                }
                Assert.AreEqual(court.CourtId, list[0].Request.CourtId, "pickup is played on this court");
            }
            // Same day, same opponent; it's offline, so nothing changes until tomorrow.
            var c0 = m.Courts[0];
            Assert.AreEqual(CityActivities.ForCourt(m, c0, d, C, 9000)[0].Detail, CityActivities.ForCourt(m, c0, d, C, 9000)[0].Detail);
        }

        [Test]
        public void HomeCourt_OffersTheRiseHub_AndALockedRivalBeforeTheSeason()
        {
            var d = new CareerSaveData();
            var m = CityDistricts.Map(CityDistricts.FirstCallers);
            var list = CityActivities.ForCourt(m, m.Court("court.first_callers"), d, C, 1);
            Assert.AreEqual("RISE HUB", list[0].Label);
            Assert.AreEqual(CityAction.OpenRiseHub, list[0].Action);
            var rival = list.Find(a => a.Label == "RIVAL CHALLENGE");
            Assert.IsNotNull(rival);
            Assert.IsNotNull(rival.Locked, "no rival game without a season");
            var shoot = list.Find(a => a.Label == "SHOOTAROUND");
            Assert.AreEqual((int)DrillKind.FreeShoot, shoot.Request.Drill);
        }

        [Test]
        public void GymDoors_OpenDrills_LockerRoom_AndTheHalloweenGame()
        {
            var d = new CareerSaveData();
            var m = CityDistricts.Map(CityDistricts.Gym);
            foreach (var (id, _, drill) in CityDistricts.Stations)
            {
                var a = CityActivities.ForPortal(m, m.Portal(id), d, C);
                Assert.AreEqual(1, a.Count, id);
                Assert.AreEqual(GameMode.Practice, a[0].Request.Mode);
                Assert.AreEqual((int)drill, a[0].Request.Drill);
                Assert.IsTrue(CityDistricts.TryParseReturn(a[0].Request.ReturnTo, out var dd, out var ss));
                Assert.AreEqual(CityDistricts.Gym, dd);
                Assert.IsTrue(m.TryGetSpawn(ss, out _), "comes back to the station: " + ss);
            }
            Assert.AreEqual(CityAction.OpenLockerRoom, CityActivities.ForPortal(m, m.Portals.Find(p => p.Target == "lockers"), d, C)[0].Action);
            var h = CityActivities.ForPortal(m, m.Portals.Find(p => p.Target == "halloween"), d, C);
            Assert.AreEqual(1, h.Count);
            Assert.AreEqual("HALLOWEEN GAME", h[0].Label);
            Assert.IsNotNull(h[0].Request);
            Assert.IsTrue(CityDistricts.TryParseReturn(h[0].Request.ReturnTo, out _, out var hs));
            Assert.IsTrue(m.TryGetSpawn(hs, out _));
            var fc = CityDistricts.Map(CityDistricts.FirstCallers);
            var store = CityActivities.ForPortal(fc, fc.Portals.Find(p => p.Target == "store"), d, C);
            Assert.AreEqual("Opening soon.", store[0].Locked);
        }

        [Test]
        public void PracticeBest_ShowsYourRecord()
        {
            var d = new CareerSaveData();
            Assert.IsNull(CityActivities.PracticeBest(d, DrillKind.FreeShoot));
            d.practice.freeShootMakes = 14;
            StringAssert(CityActivities.PracticeBest(d, DrillKind.FreeShoot), "14");
            Assert.IsNull(CityActivities.PracticeBest(null, DrillKind.FreeShoot));
        }

        // ------------------------------------------------------------------ onboarding, clock

        [Test]
        public void Onboarding_OnlyMovesForward_OneStepAtATime()
        {
            var s = new CitySaveData();
            Assert.IsNotNull(CityOnboarding.Hint(0));
            Assert.IsFalse(CityOnboarding.Reach(s, 2), "can't skip a step");
            Assert.IsTrue(CityOnboarding.Reach(s, 1));
            Assert.IsFalse(CityOnboarding.Reach(s, 1));
            Assert.IsTrue(CityOnboarding.Reach(s, 2));
            Assert.IsTrue(CityOnboarding.Reach(s, 3));
            Assert.IsTrue(CityOnboarding.Done(s));
            Assert.IsNull(CityOnboarding.Hint(s.onboarding));
            Assert.IsFalse(CityOnboarding.Reach(s, 4));
        }

        [Test]
        public void Clock_DayAndNight()
        {
            Assert.AreEqual(CityTimeOfDay.Morning, CityClock.Of(8));
            Assert.AreEqual(CityTimeOfDay.Afternoon, CityClock.Of(13));
            Assert.AreEqual(CityTimeOfDay.Evening, CityClock.Of(19));
            Assert.AreEqual(CityTimeOfDay.Night, CityClock.Of(2));
            Assert.AreEqual(0f, CityClock.Darkness(12f), 1e-5);
            Assert.AreEqual(0.8f, CityClock.Darkness(23f), 1e-5);
            Assert.Greater(CityClock.Darkness(19f), CityClock.Darkness(18f), "dusk darkens gradually");
            Assert.IsFalse(CityClock.LightsOn(12f));
            Assert.IsTrue(CityClock.LightsOn(22f));
            Assert.AreEqual(CityClock.Darkness(1f), CityClock.Darkness(25f), 1e-5);
        }

        private static void StringAssert(string s, string part) => Assert.IsTrue(s != null && s.Contains(part), s);
    }
}
