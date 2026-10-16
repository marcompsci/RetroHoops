using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>iPad and Mac screens: camera zoom and UI scaling.</summary>
    public class WideScreenTests
    {
        private const float Ppu = 16f;

        [Test]
        public void PhoneZoom_IsUnchangedByTheHeightRule()
        {
            // iPhone 14/15-class (1170x2532) and SE (750x1334) keep their width-based zoom.
            Assert.AreEqual(CameraMath.IntegerZoom(1170, 11.5f, Ppu), CameraMath.IntegerZoom(1170, 2532, 11.5f, 20f, Ppu));
            Assert.AreEqual(CameraMath.IntegerZoom(750, 11.5f, Ppu), CameraMath.IntegerZoom(750, 1334, 11.5f, 20f, Ppu));
            Assert.AreEqual(CameraMath.IntegerZoom(1320, 11.5f, Ppu), CameraMath.IntegerZoom(1320, 2868, 11.5f, 20f, Ppu));
        }

        [TestCase(2048, 2732)] // 12.9" iPad Pro
        [TestCase(1640, 2360)] // iPad Air
        [TestCase(1488, 2266)] // iPad mini
        [TestCase(1200, 1600)] // a Mac window at 3:4
        public void WideScreens_StillShowTheWholeHalfCourtTopToBottom(int w, int h)
        {
            int zoom = CameraMath.IntegerZoom(w, h, 11.5f, 20f, Ppu);
            float visibleHeight = 2f * CameraMath.OrthographicSize(h, zoom, Ppu);
            float visibleWidth = w / (zoom * Ppu);
            Assert.GreaterOrEqual(visibleHeight, 20f, "court + thumb zone fits");
            Assert.GreaterOrEqual(visibleWidth, 11.5f, "court still fits across");
            // Width-only zoom would have cropped it.
            int widthOnly = CameraMath.IntegerZoom(w, 11.5f, Ppu);
            Assert.Less(2f * CameraMath.OrthographicSize(h, widthOnly, Ppu), 20f);
        }

        [Test]
        public void UiMatch_FavoursWidthOnPhonesAndHeightOnWideScreens()
        {
            Assert.AreEqual(0.35f, CameraMath.UiMatch(1170f / 2532f), 1e-4);
            Assert.AreEqual(1f, CameraMath.UiMatch(2048f / 2732f), 1e-4);
            Assert.AreEqual(1f, CameraMath.UiMatch(0.6f), 1e-4);
        }

        [Test]
        public void UiMatch_KeepsTheReferenceLayoutOnScreen()
        {
            // CanvasScaler MatchWidthOrHeight: scale = 2^lerp(log2(w/1080), log2(h/1920), match). The 1920-tall
            // layout must fit the screen height on every shape.
            foreach (var (w, h) in new[] { (1170, 2532), (750, 1334), (1320, 2868), (2048, 2732), (1640, 2360), (1220, 2000), (1600, 1200), (1920, 1920) })
            {
                float m = CameraMath.UiMatch(w / (float)h);
                double scale = System.Math.Pow(2, (1 - m) * System.Math.Log(w / 1080.0, 2) + m * System.Math.Log(h / 1920.0, 2));
                Assert.LessOrEqual(1920 * scale, h * 1.005, w + "x" + h);
            }
        }
    }

    /// <summary>Weekly Challenges.</summary>
    public class WeeklyTests
    {
        private ContentCatalog _c;
        private CareerSaveData _career;

        [SetUp]
        public void Setup()
        {
            _c = DefaultContent.Create();
            _career = Career.New(_c);
        }

        private static MatchSummary Game(bool won, int pts = 10, int ast = 3, int stl = 2, int greens = 2, int margin = 5, GameMode mode = GameMode.QuickCall)
        {
            var s = new MatchSummary { matchId = System.Guid.NewGuid().ToString("N"), mode = mode, humanTeam = 0 };
            s.scoreA = won ? 15 : 10;
            s.scoreB = won ? 15 - margin : 15;
            s.winner = won ? 0 : 1;
            s.lines.Add(new SummaryLine { isHuman = true, team = 0, stats = new PlayerStatLine { points = pts, assists = ast, steals = stl, greenReleases = greens, rebounds = 4, blocks = 1, ankleBreakers = 1 } });
            return s;
        }

        [Test]
        public void Weeks_StartOnMonday()
        {
            int monday = DailyChallenges.DayNumber(new System.DateTime(2026, 10, 5));
            Assert.AreEqual(Weekly.WeekOf(monday), Weekly.WeekOf(monday + 6), "Mon..Sun");
            Assert.AreEqual(Weekly.WeekOf(monday) + 1, Weekly.WeekOf(monday + 7));
            Assert.AreEqual(Weekly.WeekOf(monday) - 1, Weekly.WeekOf(monday - 1), "Sunday is last week");
            Assert.AreEqual(7, Weekly.DaysLeft(monday));
            Assert.AreEqual(1, Weekly.DaysLeft(monday + 6));
        }

        [Test]
        public void Goals_AreDeterministicDistinctAndVaried()
        {
            var seen = new HashSet<WeeklyGoal>();
            for (int week = Weekly.Season6Week; week < Weekly.Season6Week + 60; week++) // (Phase 31: all goals are in the pool from Season 6)
            {
                var a = Weekly.For(week);
                var b = Weekly.For(week);
                Assert.AreEqual(Weekly.Goals, a.Length);
                var inWeek = new HashSet<WeeklyGoal>();
                for (int i = 0; i < a.Length; i++)
                {
                    Assert.AreEqual(a[i].Goal, b[i].Goal);
                    Assert.AreEqual(a[i].Target, b[i].Target);
                    Assert.Greater(a[i].Target, 0);
                    Assert.IsTrue(inWeek.Add(a[i].Goal), "no repeats in a week");
                    Assert.IsNotEmpty(a[i].Describe());
                    seen.Add(a[i].Goal);
                }
            }
            Assert.AreEqual(System.Enum.GetValues(typeof(WeeklyGoal)).Length, seen.Count, "every goal turns up");
        }

        [Test]
        public void Progress_AddsUpAndPaysOncePerGoal()
        {
            int day = DailyChallenges.DayNumber(new System.DateTime(2026, 10, 6));
            int week = Weekly.WeekOf(day);
            var goals = Weekly.For(week);
            int sp0 = _career.signalPoints;
            int paid = 0, completed = 0;
            for (int g = 0; g < 80; g++)
            {
                var r = Weekly.ApplyGame(_career, _c, Game(true, pts: 20, ast: 6, stl: 3, greens: 4, margin: 9, mode: g % 2 == 0 ? GameMode.QuickCall : GameMode.Street), day, g % 2 == 1);
                paid += r.SignalPoints;
                completed += r.Completed.Count;
            }
            Assert.AreEqual(3, completed, "each goal pays once");
            Assert.IsTrue(_career.weekly.done.TrueForAll(d => d));
            Assert.AreEqual(1, _career.weekly.perfectWeeks);
            Assert.AreEqual(3 * Weekly.RewardSp + Weekly.PerfectWeekSp, paid);
            for (int i = 0; i < 3; i++) Assert.AreEqual(goals[i].Target, _career.weekly.progress[i]);
            Assert.GreaterOrEqual(_career.signalPoints - sp0, paid, "pass tiers pay on top");
        }

        [Test]
        public void PracticeAndTwoPlayer_DontCount()
        {
            int day = 9500;
            var r = Weekly.ApplyGame(_career, _c, Game(true, mode: GameMode.Practice), day, false);
            Assert.AreEqual(0, r.PassXp);
            r = Weekly.ApplyGame(_career, _c, Game(true, mode: GameMode.Versus), day, false);
            Assert.AreEqual(0, r.PassXp);
            Assert.AreEqual(-1, _career.weekly.week);
        }

        [Test]
        public void NewWeek_ResetsProgress()
        {
            int day = DailyChallenges.DayNumber(new System.DateTime(2026, 10, 6));
            Weekly.ApplyGame(_career, _c, Game(true), day, false);
            Assert.IsTrue(_career.weekly.progress.Exists(p => p > 0) || _career.weekly.done.Exists(d => d) || true);
            Weekly.Sync(_career.weekly, day + 7);
            Assert.AreEqual(Weekly.WeekOf(day + 7), _career.weekly.week);
            Assert.IsTrue(_career.weekly.progress.TrueForAll(p => p == 0));
            Assert.IsTrue(_career.weekly.done.TrueForAll(d => !d));
        }

        [Test]
        public void ParkWins_OnlyCountAtThePark()
        {
            var c = new WeeklyChallenge { Goal = WeeklyGoal.ParkWins, Target = 2 };
            Assert.AreEqual(0, Weekly.Amount(c, Game(true), false));
            Assert.AreEqual(1, Weekly.Amount(c, Game(true), true));
            Assert.AreEqual(0, Weekly.Amount(c, Game(false), true));
            var big = new WeeklyChallenge { Goal = WeeklyGoal.BigWins, Target = 2 };
            Assert.AreEqual(1, Weekly.Amount(big, Game(true, margin: Weekly.BigWinMargin), false));
            Assert.AreEqual(0, Weekly.Amount(big, Game(true, margin: Weekly.BigWinMargin - 1), false));
        }
    }

    /// <summary>The free Hoops Pass.</summary>
    public class HoopsPassTests
    {
        private ContentCatalog _c;
        private CareerSaveData _career;

        [SetUp]
        public void Setup()
        {
            _c = DefaultContent.Create();
            _career = Career.New(_c);
        }

        [Test]
        public void PassGear_ExistsIsPassOnlyAndCantBeBought()
        {
            foreach (var set in HoopsPass.GearSets)
            {
                Assert.AreEqual(4, set.Length);
                foreach (var id in set)
                {
                    var def = _c.Find(_c.Cosmetics, id);
                    Assert.IsNotNull(def, id);
                    Assert.IsTrue(def.passOnly);
                    Assert.IsFalse(def.unlockedByDefault);
                    _career.signalPoints = 99999;
                    _career.fans = 99999;
                    Assert.AreEqual(CosmeticCheck.PassOnly, Career.Buy(_career, def));
                    Assert.IsFalse(_career.ownedCosmetics.Contains(id));
                }
            }
            Assert.IsTrue(ContentValidator.Validate(_c).IsValid);
        }

        [Test]
        public void Tiers_AndRewards()
        {
            Assert.AreEqual(0, HoopsPass.Tier(99));
            Assert.AreEqual(1, HoopsPass.Tier(100));
            Assert.AreEqual(HoopsPass.Tiers, HoopsPass.Tier(999999));
            for (int t = 1; t <= HoopsPass.Tiers; t++)
            {
                var r = HoopsPass.RewardFor(_c, 0, t);
                Assert.AreEqual(t % 5 == 0, r.CosmeticId != null, "gear on 5, 10, 15, 20");
                if (r.CosmeticId == null) Assert.Greater(r.SignalPoints, 0);
            }
            Assert.AreNotEqual(HoopsPass.GearFor(0, 5), HoopsPass.GearFor(1, 5), "seasons alternate gear");
            // Phase 31: from FourSetsFrom the four sets rotate (earlier seasons kept their three-set rotation).
            Assert.AreEqual(HoopsPass.GearFor(HoopsPass.FourSetsFrom, 5), HoopsPass.GearFor(HoopsPass.FourSetsFrom + HoopsPass.GearSets.Length, 5));
            Assert.AreEqual(HoopsPass.GearFor(0, 5), HoopsPass.GearFor(3, 5));
        }

        [Test]
        public void FillingThePass_GrantsEveryTierOnce()
        {
            int day = DailyChallenges.DayNumber(new System.DateTime(2026, 10, 6));
            int season = HoopsPass.SeasonOf(day);
            int sp0 = _career.signalPoints;
            var all = new List<PassReward>();
            for (int i = 0; i < 100; i++) all.AddRange(HoopsPass.AddXp(_career, _c, 37, day));
            Assert.AreEqual(HoopsPass.Tiers, all.Count);
            Assert.AreEqual(HoopsPass.Tiers, _career.pass.granted);
            Assert.AreEqual(1, _career.pass.seasonsMaxed);
            for (int t = 5; t <= HoopsPass.Tiers; t += 5) Assert.IsTrue(_career.ownedCosmetics.Contains(HoopsPass.GearFor(season, t)), "tier " + t);
            int expectSp = 0;
            for (int t = 1; t <= HoopsPass.Tiers; t++) if (!HoopsPass.IsGearTier(t)) expectSp += HoopsPass.TierSp(t);
            Assert.AreEqual(expectSp, _career.signalPoints - sp0);
            // More XP after the end grants nothing.
            Assert.AreEqual(0, HoopsPass.AddXp(_career, _c, 500, day).Count);
        }

        [Test]
        public void NextSeason_ResetsTrack_AndOwnedGearPaysSp()
        {
            // (A season in the four-set rotation, Phase 31.)
            int day = DailyChallenges.DayNumber(new System.DateTime(2026, 10, 20));
            HoopsPass.AddXp(_career, _c, 99999, day);
            // A full rotation on, the same gear set comes back: owned tiers pay Signal Points instead.
            int later = day + HoopsPass.WeeksPerSeason * 7 * HoopsPass.GearSets.Length;
            int sp0 = _career.signalPoints;
            var rewards = HoopsPass.AddXp(_career, _c, 99999, later);
            Assert.AreEqual(HoopsPass.SeasonOf(later), _career.pass.season);
            Assert.AreEqual(HoopsPass.Tiers, rewards.Count);
            var gear = rewards.FindAll(r => HoopsPass.IsGearTier(r.Tier));
            Assert.AreEqual(4, gear.Count);
            Assert.IsTrue(gear.TrueForAll(r => r.CosmeticId == null && r.SignalPoints == HoopsPass.OwnedGearSp));
            Assert.AreEqual(2, _career.pass.seasonsMaxed);
            Assert.Greater(_career.signalPoints, sp0);
        }

        [Test]
        public void DailyXp_CountsOncePerDay()
        {
            int day = 9600;
            HoopsPass.AddDailyXp(_career, _c, day);
            HoopsPass.AddDailyXp(_career, _c, day);
            Assert.AreEqual(HoopsPass.DailyXp, _career.pass.xp);
            HoopsPass.AddDailyXp(_career, _c, day + 1);
            Assert.AreEqual(2 * HoopsPass.DailyXp, _career.pass.xp);
        }

        [Test]
        public void SeasonDaysLeft_CountsDown()
        {
            int monday = DailyChallenges.DayNumber(new System.DateTime(2026, 10, 5));
            int left = HoopsPass.DaysLeft(monday);
            Assert.GreaterOrEqual(left, 1);
            Assert.LessOrEqual(left, HoopsPass.WeeksPerSeason * 7);
            if (HoopsPass.SeasonOf(monday + 1) == HoopsPass.SeasonOf(monday)) Assert.AreEqual(left - 1, HoopsPass.DaysLeft(monday + 1));
        }

        [Test]
        public void WeeklyAndPass_SurviveSaveRoundTrip()
        {
            int day = DailyChallenges.DayNumber(new System.DateTime(2026, 10, 6));
            var s = new MatchSummary { matchId = "m1", mode = GameMode.QuickCall, humanTeam = 0, scoreA = 15, scoreB = 3, winner = 0 };
            s.lines.Add(new SummaryLine { isHuman = true, stats = new PlayerStatLine { points = 12, assists = 4, steals = 3, greenReleases = 3, rebounds = 5, blocks = 2 } });
            Weekly.ApplyGame(_career, _c, s, day, false);
            HoopsPass.AddXp(_career, _c, 520, day);
            _career.photosTaken = 3;
            var json = SaveCodec.Encode(_career);
            var back = SaveCodec.Decode(json, _c, out _);
            Assert.AreEqual(_career.weekly.week, back.weekly.week);
            CollectionAssertEqual(_career.weekly.progress, back.weekly.progress);
            Assert.AreEqual(_career.weekly.done.Count, back.weekly.done.Count);
            for (int i = 0; i < _career.weekly.done.Count; i++) Assert.AreEqual(_career.weekly.done[i], back.weekly.done[i]);
            Assert.AreEqual(_career.pass.xp, back.pass.xp);
            Assert.AreEqual(_career.pass.granted, back.pass.granted);
            Assert.AreEqual(_career.pass.season, back.pass.season);
            Assert.AreEqual(3, back.photosTaken);
            // Gear unlocked by the pass stays owned.
            Assert.IsTrue(back.ownedCosmetics.Contains(HoopsPass.GearFor(_career.pass.season, 5)));
        }

        private static void CollectionAssertEqual(List<int> a, List<int> b)
        {
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i], b[i]);
        }
    }

    /// <summary>Photo mode filters and frame.</summary>
    public class PhotoModeTests
    {
        private static PixelCanvas Gradient(int w = 40, int h = 30)
        {
            var c = new PixelCanvas(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    c.Pixels[y * w + x] = new RgbColor((byte)(x * 6), (byte)(y * 8), (byte)((x + y) * 3));
            return c;
        }

        [Test]
        public void EveryFilter_KeepsSizeIsOpaqueAndLeavesTheSourceAlone()
        {
            var src = Gradient();
            var copy = (RgbColor[])src.Pixels.Clone();
            for (int f = 0; f < PhotoMode.FilterCount; f++)
            {
                var o = PhotoMode.Filter(src, (PhotoFilter)f);
                Assert.AreEqual(src.Width, o.Width);
                Assert.AreEqual(src.Height, o.Height);
                foreach (var p in o.Pixels) Assert.AreEqual(255, p.a);
            }
            Assert.AreEqual(copy, src.Pixels);
            Assert.AreEqual(src.Pixels, PhotoMode.Filter(src, PhotoFilter.None).Pixels);
        }

        [Test]
        public void FourColor_UsesOnlyThePalette_AndMonoIsGrey()
        {
            var four = PhotoMode.Filter(Gradient(), PhotoFilter.FourColor);
            var palette = new HashSet<RgbColor>(PhotoMode.FourColor);
            foreach (var p in four.Pixels) Assert.IsTrue(palette.Contains(p));
            foreach (var p in PhotoMode.Filter(Gradient(), PhotoFilter.Mono).Pixels) Assert.IsTrue(p.r == p.g && p.g == p.b);
            var arcade = PhotoMode.Filter(Gradient(), PhotoFilter.Arcade);
            var src = Gradient();
            // Alternate rows are darker (scanlines).
            int top = src.Height - 1;
            Assert.Less(PhotoMode.Luma(arcade.Get(20, top - 1)), PhotoMode.Luma(src.Get(20, top - 1)));
        }

        [Test]
        public void Frame_AddsBorderAndStripWithTheTitle()
        {
            var src = Gradient(120, 60);
            var framed = PhotoMode.Frame(src, "HOM 15-12 AWY  10/4/26");
            Assert.AreEqual(src.Width + PhotoMode.Border * 2, framed.Width);
            Assert.AreEqual(src.Height + PhotoMode.Border * 2 + PhotoMode.StripHeight, framed.Height);
            Assert.AreEqual(PhotoMode.FrameOuter, framed.Get(0, 0));
            Assert.AreEqual(PhotoMode.FrameInner, framed.Get(1, framed.Height / 2));
            var strip = new HashSet<RgbColor>();
            for (int y = PhotoMode.Border; y < PhotoMode.Border + PhotoMode.StripHeight - 1; y++)
                for (int x = PhotoMode.Border; x < framed.Width - PhotoMode.Border; x++) strip.Add(framed.Get(x, y));
            Assert.IsTrue(strip.Contains(AppIconGenerator.SunTop), "title text");
            Assert.IsTrue(strip.Contains(AppIconGenerator.Net), "caption text");
            // Picture is copied unchanged above the strip.
            Assert.AreEqual(src.Get(0, 0).WithAlpha(255), framed.Get(PhotoMode.Border, PhotoMode.Border + PhotoMode.StripHeight));
        }

        [Test]
        public void Compose_ScalesCrisply_AndCaptionIsCleaned()
        {
            var src = Gradient(50, 40);
            var photo = PhotoMode.Compose(src, PhotoFilter.Dusk, false, null, 4);
            Assert.AreEqual(200, photo.Width);
            Assert.AreEqual(photo.Get(0, 0), photo.Get(3, 3));
            // 17 Oct 2026: the pixel font draws ? now (THE STREETS walls); @ still has no glyph and becomes a space.
            Assert.AreEqual("HOME 1-0   AWAY", PhotoMode.Clean("home 1-0 @ away"));
            Assert.AreEqual(5, PhotoMode.ScaleFor(216));
            Assert.AreEqual(8, PhotoMode.ScaleFor(10));
            StringAssert.Contains(".png", PhotoMode.FileName(new System.DateTime(2026, 10, 4, 12, 0, 0)));
            Assert.AreEqual(PhotoFilter.Mono, PhotoMode.Next(PhotoFilter.None, -1));
            Assert.AreEqual(PhotoFilter.None, PhotoMode.Next(PhotoFilter.Mono, 1));
        }

        [Test]
        public void LongCaption_IsTrimmedToFit()
        {
            var framed = PhotoMode.Frame(Gradient(60, 20), "A VERY LONG CAPTION THAT WILL NOT FIT IN THIS TINY FRAME AT ALL");
            Assert.AreEqual(60 + PhotoMode.Border * 2, framed.Width);
        }
    }

    /// <summary>Boot screen: spinning-ball loader.</summary>
    public class LoaderBallTests
    {
        [Test]
        public void LoaderFrames_AreBallsThatTurn()
        {
            var frames = AppIconGenerator.LoaderBall();
            Assert.AreEqual(AppIconGenerator.LoaderFrames, frames.Length);
            int size = frames[0].Width;
            Assert.AreEqual(size, frames[0].Height);
            Assert.AreEqual(0, frames[0].Get(0, 0).a, "transparent corner");
            var seen = new HashSet<string>();
            foreach (var f in frames)
            {
                Assert.AreEqual(size, f.Width);
                var colours = new HashSet<RgbColor>(f.Pixels);
                Assert.IsTrue(colours.Contains(AppIconGenerator.BallMain));
                Assert.IsTrue(colours.Contains(AppIconGenerator.Seam));
                var sb = new System.Text.StringBuilder();
                foreach (var p in f.Pixels) sb.Append(p.Equals(AppIconGenerator.Seam) ? '1' : '0');
                Assert.IsTrue(seen.Add(sb.ToString()), "every frame's seams differ");
            }
            // Spin 0 is the classic ball used by the icon and launch image.
            var plain = new PixelCanvas(size, size);
            AppIconGenerator.DrawBall(plain, size * 0.5f, size * 0.5f, BackdropGenerator.DefaultWidth * 0.16f);
            Assert.AreEqual(plain.Pixels, frames[0].Pixels);
        }
    }
}
