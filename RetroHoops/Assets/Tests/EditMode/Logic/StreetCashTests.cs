using System;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>17 Oct 2026: CASHED OUT (street cash, streaks, the secret 20+ bonus, milestones, the CPUs keying on you) and THE STREETS walls in BALLER CITY.</summary>
    public class StreetCashTests
    {
        private static ContentCatalog C => _c ??= DefaultContent.Create();
        private static ContentCatalog _c;

        private static MatchRequest StreetsGame() => Streets.Pickup(C, new CareerSaveData(), null, "court.sunset_cage");

        [Test]
        public void AWin_CashesOut5000_AndThreeInARow_Doubles()
        {
            var d = new CareerSaveData();
            var r = StreetsGame();
            Assert.AreEqual(5000, StreetCash.ApplyGame(d, r, true, 8, 10).Cash);
            Assert.AreEqual(5000, StreetCash.ApplyGame(d, r, true, 8, 10).Cash);
            var third = StreetCash.ApplyGame(d, r, true, 8, 10);
            Assert.AreEqual(10000, third.Cash, "third straight win doubles");
            Assert.IsTrue(third.Doubled);
            Assert.AreEqual(10000, StreetCash.ApplyGame(d, r, true, 8, 10).Cash, "and stays doubled on the streak");
            Assert.AreEqual(0, StreetCash.ApplyGame(d, r, false, 8, 10).Cash, "a loss pays nothing");
            Assert.AreEqual(0, d.streetCash.streak);
            Assert.AreEqual(5000, StreetCash.ApplyGame(d, r, true, 8, 10).Cash, "back to $5,000 after a loss");
            Assert.AreEqual(35000, d.streetCash.cash);
            Assert.AreEqual(35000, d.streetCash.earned);
            Assert.AreEqual(4, d.streetCash.bestStreak);
            StringAssertContains(third.Note(), "CASHED OUT $10,000");
        }

        [Test]
        public void MoreThan20_IsTheSecret20000_EvenInALoss()
        {
            var d = new CareerSaveData();
            var r = StreetsGame();
            var twenty = StreetCash.ApplyGame(d, r, true, 20, 3);
            Assert.AreEqual(5000, twenty.Cash, "20 isn't over 20");
            Assert.IsFalse(twenty.BigGame);
            var big = StreetCash.ApplyGame(d, r, true, 21, 3);
            Assert.AreEqual(25000, big.Cash);
            Assert.IsTrue(big.BigGame);
            Assert.AreEqual(20000, StreetCash.ApplyGame(d, r, false, 22, 3).Cash, "the bonus pays even in a loss");
            Assert.AreEqual(2, d.streetCash.bigGames);
            Assert.IsTrue(big.Milestones.Exists(m => m.Id == "big1"), "a secret milestone");
        }

        [Test]
        public void BonusSP_ForPlaying_Winning_AndLeadingInScoring()
        {
            var d = new CareerSaveData();
            int sp0 = d.signalPoints;
            var r = StreetsGame();
            var p = StreetCash.ApplyGame(d, r, false, 6, 9);
            Assert.AreEqual(StreetCash.PlaySp, p.Sp, "played, lost, not the top scorer");
            Assert.IsFalse(p.TopScorer);
            int before = d.signalPoints;
            var w = StreetCash.ApplyGame(d, r, true, 12, 7);
            Assert.IsTrue(w.TopScorer);
            Assert.AreEqual(d.signalPoints - before, w.Sp);
            Assert.GreaterOrEqual(w.Sp, StreetCash.PlaySp + StreetCash.WinSp + StreetCash.TopScorerSp);
            Assert.AreEqual(d.signalPoints - sp0, d.streetCash.bonusSp);
            Assert.IsFalse(StreetCash.ApplyGame(d, r, true, 5, 5).TopScorer, "a tie isn't leading");
        }

        [Test]
        public void Milestones_PayOnce()
        {
            var d = new CareerSaveData();
            var r = StreetsGame();
            var first = StreetCash.ApplyGame(d, r, true, 4, 9);
            Assert.IsTrue(first.Milestones.Exists(m => m.Id == "first_cash"));
            Assert.IsFalse(StreetCash.ApplyGame(d, r, true, 4, 9).Milestones.Exists(m => m.Id == "first_cash"));
            CollectionAssert.AllItemsAreUnique(Array.ConvertAll(StreetCash.Milestones, m => m.Id));
            Assert.IsTrue(Array.Exists(StreetCash.Milestones, m => m.Secret));
        }

        [Test]
        public void OnlyStreetGamesPay()
        {
            Assert.IsTrue(StreetCash.Counts(StreetsGame()));
            Assert.IsTrue(StreetCash.Counts(Street.Challenge(C, new CareerSaveData(), Street.Challengers[0])), "THE PARK counts");
            Assert.IsTrue(StreetCash.Counts(CityCrews.Challenge(C, new CareerSaveData(), CityCrews.All[0])), "the city crews count");
            var quick = MatchRequest.QuickCallDefault(C);
            Assert.IsFalse(StreetCash.Counts(quick));
            var d = new CareerSaveData();
            Assert.AreEqual(0, StreetCash.ApplyGame(d, quick, true, 30, 2).Cash);
            Assert.AreEqual(0, d.streetCash.games);
        }

        [Test]
        public void TheCPUsCatchOn_AfterBigGames_AndCoolOff()
        {
            var d = new CareerSaveData();
            var r = StreetsGame();
            Assert.AreEqual(0f, StreetCash.KeyOn(d, r));
            var one = StreetCash.ApplyGame(d, r, true, 21, 3);
            Assert.IsFalse(one.KeyedOn, "one big game: they're not on to you yet");
            var two = StreetCash.ApplyGame(d, r, true, 22, 3);
            Assert.IsTrue(two.KeyedOn, "after a while they catch on");
            StringAssertContains(two.Note(), "keying on you");
            float heat = StreetCash.KeyOn(d, r);
            Assert.Greater(heat, 0.5f);
            Assert.AreEqual(0f, StreetCash.KeyOn(d, MatchRequest.QuickCallDefault(C)), "only on the streets");
            StreetCash.ApplyGame(d, r, true, 8, 9);
            Assert.Less(StreetCash.KeyOn(d, r), heat, "ordinary games cool it off");
            for (int i = 0; i < 20; i++) StreetCash.ApplyGame(d, r, false, 2, 9);
            Assert.AreEqual(0f, StreetCash.KeyOn(d, r), 1e-5);
        }

        [Test]
        public void KeyedOn_TheDefenderPlaysYouTighter()
        {
            float Gap(float keyOn)
            {
                var r = StreetsGame();
                r.Seed = 31;
                r.KeyOnHuman = keyOn;
                var m = new MatchSimulation(MatchSetup.FromRequest(r, C));
                Assert.AreEqual(Math.Min(1f, keyOn), m.Setup.KeyOnHuman, 1e-5);
                float sum = 0f;
                int n = 0;
                for (int i = 0; i < 60 * 8; i++)
                {
                    m.Step(1f / 60f, default);
                    var h = m.Holder;
                    if (h == null || !h.IsHuman || m.Phase != MatchPhase.Live) continue;
                    float best = float.MaxValue;
                    foreach (var p in m.Players) if (p.Team != h.Team) best = Math.Min(best, Vec2.Distance(p.Position, h.Position));
                    sum += best;
                    n++;
                }
                Assert.Greater(n, 30, "you had the ball");
                return sum / n;
            }
            Assert.Less(Gap(1f), Gap(0f) - 0.05f);
            var hot = StreetsGame();
            hot.KeyOnHuman = 5f;
            Assert.AreEqual(1f, MatchSetup.FromRequest(hot, C).KeyOnHuman, 1e-5, "clamped");
        }

        [Test]
        public void CashSaves_RoundTrip_AndJunkIsCleaned()
        {
            var d = new CareerSaveData();
            d.streetCash.cash = 45000;
            d.streetCash.earned = 60000;
            d.streetCash.games = 10;
            d.streetCash.wins = 7;
            d.streetCash.streak = 3;
            d.streetCash.bestStreak = 4;
            d.streetCash.bigGames = 1;
            d.streetCash.heat = 0.34f;
            d.streetCash.achievements.Add("first_cash");
            d.streetCash.achievements.Add("fake");
            var back = SaveCodec.Decode(SaveCodec.Encode(d), C, out var status).streetCash;
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.AreEqual(45000, back.cash);
            Assert.AreEqual(60000, back.earned);
            Assert.AreEqual(3, back.streak);
            Assert.AreEqual(0.34f, back.heat, 1e-4);
            Assert.AreEqual(1, back.achievements.Count);

            d.streetCash.cash = -5;
            d.streetCash.heat = float.NaN;
            d.streetCash.wins = 99; // more than games
            back = SaveCodec.Decode(SaveCodec.Encode(d), C, out _).streetCash;
            Assert.AreEqual(0, back.cash);
            Assert.AreEqual(0f, back.heat);
            Assert.AreEqual(10, back.wins);
            var old = SaveCodec.Encode(new CareerSaveData()).Replace("\"cash\":", "\"zzcash\":");
            Assert.IsNotNull(SaveCodec.Decode(old, C, out _).streetCash, "older saves start with an empty bank");
        }

        [Test]
        public void TheStore_TakesStreetCash()
        {
            var d = new CareerSaveData();
            d.fans = 100000;
            var item = CityShop.Stock(C, 9000)[0];
            Assert.IsFalse(CityShop.Choices(d, C, 9000).Exists(a => a.Action == CityAction.BuyWithCash), "no cash, no cash option");
            d.streetCash.cash = StreetCash.Price(item) - 1;
            var row = CityShop.Choices(d, C, 9000).Find(a => a.Action == CityAction.BuyWithCash && a.ItemId == item.id);
            Assert.IsNotNull(row.Locked);
            Assert.AreEqual(CosmeticCheck.NotEnoughPoints, CityShop.BuyWithCash(d, C, item.id, 9000));
            d.streetCash.cash = StreetCash.Price(item) + 500;
            int sp = d.signalPoints;
            Assert.AreEqual(CosmeticCheck.Ok, CityShop.BuyWithCash(d, C, item.id, 9000));
            Assert.AreEqual(500, d.streetCash.cash);
            Assert.AreEqual(sp, d.signalPoints, "SP untouched");
            Assert.IsTrue(d.ownedCosmetics.Contains(item.id));
            Assert.AreEqual(item.cost * StreetCash.CashPerSp, StreetCash.Price(item));
        }

        [Test]
        public void StreetsWalls_StandByTheStreetCourts()
        {
            var d = new CareerSaveData();
            foreach (var id in new[] { CityDistricts.Blacktop, CityDistricts.HoopHeights, CityDistricts.Downtown, CityDistricts.Boardwalk, CityDistricts.SchoolYard })
            {
                var m = CityDistricts.Map(id);
                var wall = m.Portals.Find(p => p.Target == "streets");
                Assert.IsNotNull(wall, id);
                Assert.IsTrue(m.Props.Exists(p => p.Kind == CityPropKind.Graffiti), id);
                var acts = CityActivities.ForPortal(m, wall, d, C, 9000);
                Assert.AreEqual(3, acts.Count, id);
                Assert.IsNotNull(acts[0].Request, id + ": run it right here");
                Assert.AreEqual(GameMode.Streets, acts[0].Request.Mode);
                Assert.IsTrue(CityDistricts.TryParseReturn(acts[0].Request.ReturnTo, out var dd, out _));
                Assert.AreEqual(id, dd);
                Assert.IsNotNull(m.Courts.Find(k => k.CourtId == acts[0].Request.CourtId), "a court in this district");
                Assert.AreEqual(CityAction.OpenStreets, acts[1].Action);
                Assert.AreEqual(CityAction.StreetsBoard, acts[2].Action);
            }
            var art = Logic.PixelArt.CityArtGenerator.Graffiti(new CityProp { Kind = CityPropKind.Graffiti, W = 6, H = 1 }, false);
            Assert.AreEqual(96, art.Width);
            Assert.IsTrue(Logic.PixelArt.PixelFont.Has('?') && Logic.PixelArt.PixelFont.Has('$') && Logic.PixelArt.PixelFont.Has(','));
        }

        [Test]
        public void ACityCrew_KeepsItsLevel_WhateverTheModeSettingSays()
        {
            // App.PrepareRequest is Unity-side; the rule it applies is this one.
            var crew = Array.Find(CityCrews.All, c => c.Tier == CityCrewTier.Legendary);
            var r = CityCrews.Challenge(C, new CareerSaveData(), crew);
            Assert.IsTrue(ReferenceEquals(crew, CityCrews.FromContext(r.ContextId)));
            Assert.AreEqual(CityCrews.LegendaryDifficultyId, CityCrews.DifficultyFor(CityCrews.FromContext(r.ContextId)));
        }

        private static void StringAssertContains(string s, string part) => Assert.IsTrue(s != null && s.Contains(part), s);
    }
}
