using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>28 Oct 2026, improvements I31–I40.</summary>
    public class ImprovementsI31Tests
    {
        private static ContentCatalog C => _c ??= DefaultContent.Create();
        private static ContentCatalog _c;

        private static CareerSaveData NewCareer()
        {
            var d = Career.New(C);
            d.world = new CityWorldSaveData();
            d.city = new CitySaveData();
            return d;
        }

        [Test]
        public void I32_BadgeProgress_CountsUpToTheTarget()
        {
            var d = NewCareer();
            var frequent = CityAchievements.FindBadge("frequent");
            Assert.AreEqual((0, 25), CityAchievements.Progress(d, frequent));
            d.world.drives = 12;
            Assert.AreEqual((12, 25), CityAchievements.Progress(d, frequent));
            d.world.drives = 40;
            Assert.AreEqual((25, 25), CityAchievements.Progress(d, frequent));
            d.world.walkedTotal = 4300;
            Assert.AreEqual((4, 10), CityAchievements.Progress(d, CityAchievements.FindBadge("marathon")));
            foreach (var b in CityAchievements.Badges)
            {
                var (now, target) = CityAchievements.Progress(d, b);
                Assert.IsTrue(target >= 1 && now >= 0 && now <= target, b.Id);
            }
            Assert.AreEqual(25, CityAchievements.Badges.Length);
        }

        [Test]
        public void I33_RegionWeather_HasItsOwnSky()
        {
            int bayFog = 0, vegasRain = 0, vegasSnow = 0, laClear = 0, nySnowWinter = 0, ballerFog = 0;
            for (int day = 0; day < 400; day++)
            {
                if (CityWeather.For(day, 7, CityDistricts.BayHills, false) == CityWeatherKind.Fog) bayFog++;
                var lv = CityWeather.For(day, 1, CityDistricts.LvStrip, false);
                if (lv == CityWeatherKind.Rain) vegasRain++;
                if (lv == CityWeatherKind.Snow) vegasSnow++;
                if (CityWeather.For(day, 7, CityDistricts.LaBeach, false) == CityWeatherKind.Clear) laClear++;
                if (CityWeather.For(day, 1, CityDistricts.NyUptown, false) == CityWeatherKind.Snow) nySnowWinter++;
                if (CityWeather.For(day, 7, CityDistricts.Blacktop, false) == CityWeatherKind.Fog) ballerFog++;
            }
            Assert.Greater(bayFog, 80, "the Bay is often foggy");
            Assert.AreEqual(0, vegasRain + vegasSnow, "no rain or snow in the desert");
            Assert.Greater(laClear, 280, "LA is mostly sunny");
            Assert.Greater(nySnowWinter, 60, "New York snows in winter");
            Assert.AreEqual(0, ballerFog);
            Assert.AreEqual(CityWeatherKind.Clear, CityWeather.For(3, 7, CityDistricts.BayHills, false, off: true));
            Assert.AreEqual("FOG", CityWeather.Name(CityWeatherKind.Fog));
        }

        [Test]
        public void I34_TimeTrial_PaysOnceADay_UnderPar_WithoutEasyDrive()
        {
            var d = NewCareer();
            Assert.IsNull(TimeTrial.Target(d, WorldRegions.BallerCity, 100), "nowhere open to drive to yet");
            foreach (var r in WorldRegions.All) d.world.regionsSeen.Add(r.Id);
            string to = TimeTrial.Target(d, WorldRegions.BallerCity, 100);
            Assert.IsNotNull(to);
            Assert.AreNotEqual(WorldRegions.BallerCity, to);
            Assert.AreEqual(to, TimeTrial.Target(d, WorldRegions.BallerCity, 100), "the same all day");

            HighwayDrive Run(string dest, uint seed)
            {
                var drive = new HighwayDrive(WorldRegions.BallerCity, dest, seed);
                while (!drive.Done) drive.Step(1f / 60f, drive.AutoSteer(), true); // boosting the whole way
                return drive;
            }
            var fast = Run(to, 4);
            Assert.LessOrEqual(fast.Time, TimeTrial.Par, "boosting all the way beats par");
            Assert.AreEqual(0, TimeTrial.Apply(d, fast, true, 100), "not with EASY DRIVE");
            long cash = d.streetCash?.cash ?? 0;
            Assert.AreEqual(TimeTrial.Prize, TimeTrial.Apply(d, fast, false, 100));
            Assert.AreEqual(cash + TimeTrial.Prize, d.streetCash.cash);
            Assert.AreEqual(0, TimeTrial.Apply(d, fast, false, 100), "once a day");
            Assert.AreEqual(1, d.world.trialsWon);
            string other = Array.Find(WorldRegions.All, r => r.Id != to && r.Id != WorldRegions.BallerCity).Id;
            int otherDay = 101;
            while (TimeTrial.Target(d, WorldRegions.BallerCity, otherDay) == other) otherDay++;
            Assert.AreEqual(0, TimeTrial.Apply(d, Run(other, 5), false, otherDay), "a different road isn't the trial");
            StringAssert.Contains("won today", TimeTrial.Line(d, WorldRegions.BallerCity, 100));
        }

        [Test]
        public void I35_WorldStats_ListEveryRegionAndRecord()
        {
            var d = NewCareer();
            d.world.NoteRoute(WorldRegions.LosAngeles, WorldRegions.NewYork, 21.5f);
            var rows = WorldStats.Rows(d);
            foreach (var r in WorldRegions.All) Assert.IsTrue(rows.Exists(x => x.label == r.Name), r.Id);
            Assert.IsTrue(rows.Exists(x => x.label == "LA > NYC" && x.value.StartsWith("21.5")));
        }

        [Test]
        public void I37_Tips_ShowOncePerVisit_AndNeverTwice()
        {
            var w = new CityWorldSaveData();
            var t = WorldTips.For(w, CityDistricts.FirstCallers, WorldRegions.BallerCity);
            Assert.AreEqual(1, t.Count);
            Assert.AreEqual("world", t[0].Id);
            WorldTips.Mark(w, "world");
            Assert.AreEqual("minimap", WorldTips.For(w, CityDistricts.FirstCallers, WorldRegions.BallerCity)[0].Id);
            WorldTips.Mark(w, "minimap");
            Assert.AreEqual(0, WorldTips.For(w, CityDistricts.FirstCallers, WorldRegions.BallerCity).Count);
            Assert.AreEqual("region:" + WorldRegions.NewYork, WorldTips.For(w, CityDistricts.NyUptown, WorldRegions.NewYork)[0].Id);
            WorldTips.Mark(w, "region:" + WorldRegions.NewYork);
            w.drives = 1;
            Assert.AreEqual("trial", WorldTips.For(w, CityDistricts.NyUptown, WorldRegions.NewYork)[0].Id);
            // Saved.
            var d = NewCareer();
            d.world = w;
            d.world.comicVoice = true;
            d.world.trialsWon = 3;
            d.world.trialDay = 77;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), C, out _).world;
            Assert.IsTrue(back.tipsShown.Contains("minimap"));
            Assert.IsTrue(back.comicVoice);
            Assert.AreEqual(3, back.trialsWon);
            Assert.AreEqual(77, back.trialDay);
        }

        [Test]
        public void I38_TrophyRoom_ShowsBigRunCrownsAndBadges()
        {
            var d = NewCareer();
            d.world.runs.Add(WorldRegions.NewYork + "|10|0|10|2");
            d.world.badges.Add("road_trip");
            var trophies = CityCrib.Choices(new CityPortal { Target = "crib.trophies" }, d);
            Assert.IsTrue(trophies.Exists(a => a.Label.StartsWith(CityBigRun.KingTitle(WorldRegions.NewYork)) && a.Label.EndsWith("x2")));
            Assert.IsTrue(trophies.Exists(a => a.Label.StartsWith("BADGES 1/")));
            Assert.AreEqual(2, CityCrib.TrophyVariant(d) % 10, "the case holds the crowns too");
        }

        [Test]
        public void I40_ItemsAndTips_AreInTheSpanishCheck()
        {
            var all = WorldText.All();
            Assert.IsTrue(all.Contains("SANDWICHES"));
            foreach (var t in WorldTips.AllText()) Assert.IsTrue(all.Contains(t));
            foreach (var s in all) Assert.IsTrue(Loc.Has(s), s);
        }
    }
}
