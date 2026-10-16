using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>17 Oct 2026 (phases 5–7): the arena, daily challenges and call-outs, and your crib: what the save keeps.</summary>
    [Serializable]
    public class CityBigTimeSaveData
    {
        // Phase 5: the City Championship.
        /// <summary>0 = not in it today, 1 = semifinal next, 2 = final next.</summary>
        public int arenaRound;
        /// <summary>The day of the current run, and the last day a run ended (one run a day).</summary>
        public int arenaDay = -1, arenaDoneDay = -1;
        public int arenaTitles, arenaFinals;
        // Phase 6: daily challenges and call-outs.
        public int dailyDay = -1;
        public int[] dailyProgress = new int[3];
        public bool dailyPaid;
        public int dailySets;
        public int calloutDay = -1;
        public int calloutsWon;
    }

    // ====================================================================== Phase 5: BALLER ARENA

    /// <summary>
    /// 17 Oct 2026 (Omari, phase 5): the BALLER ARENA opens Downtown. The CITY CHAMPIONSHIP: one run a day, a
    /// semifinal against a BALLER crew and the final against a LEGENDARY crew (today's draw), under the arena lights.
    /// Win both for the title, a trophy for your crib and $50,000. Lose and you're out until tomorrow. You get in once
    /// you've beaten three BALLER crews (the same bar as the LEGENDARY crews).
    /// </summary>
    public static class CityArena
    {
        public const string CourtId = "court.baller_arena";
        public const long TitlePrize = 50000;
        public const int TitleSp = 250;

        public static bool Open(CareerSaveData d) => CityCrews.BallerBeaten(d?.city) >= CityCrews.BallerWinsForLegendary;

        /// <summary>Today's draw: the semifinal BALLER crew and the final LEGENDARY crew.</summary>
        public static (CityCrewDef semi, CityCrewDef final) Draw(int day)
        {
            var ballers = Array.FindAll(CityCrews.All, c => c.Tier == CityCrewTier.Baller);
            var legends = Array.FindAll(CityCrews.All, c => c.Tier == CityCrewTier.Legendary);
            uint h = StableHash.Of("arena:" + day);
            return (ballers[h % (uint)ballers.Length], legends[(h / 7) % (uint)legends.Length]);
        }

        /// <summary>Which round you'd play now (1 semi, 2 final), or 0 if today's run is over.</summary>
        public static int NextRound(CityBigTimeSaveData s, int day)
        {
            if (s == null) return 1;
            if (s.arenaDoneDay == day) return 0;
            return s.arenaDay == day && s.arenaRound == 2 ? 2 : 1;
        }

        public static MatchRequest Request(ContentCatalog c, CareerSaveData d, int day)
        {
            var s = d.bigTime ?? (d.bigTime = new CityBigTimeSaveData());
            int round = NextRound(s, day);
            if (round == 0 || !Open(d)) return null;
            var draw = Draw(day);
            var crew = round == 1 ? draw.semi : draw.final;
            var r = CityCrews.Challenge(c, d, crew);
            if (c.Court(CourtId) != null) r.CourtId = CourtId;
            r.ContextId = crew.ContextId + "#arena" + round;
            return r;
        }

        public static int RoundOf(MatchRequest r)
        {
            string tag = CityCrews.TagOf(r?.ContextId);
            return tag == "arena1" ? 1 : tag == "arena2" ? 2 : 0;
        }

        /// <summary>A finished arena game: on to the final, the title, or out for the day. Returns the post-game line.</summary>
        public static string ApplyGame(CareerSaveData d, MatchRequest r, bool won, int day)
        {
            int round = RoundOf(r);
            if (d == null || round == 0) return null;
            var s = d.bigTime ?? (d.bigTime = new CityBigTimeSaveData());
            if (!won)
            {
                s.arenaRound = 0;
                s.arenaDoneDay = day;
                return Loc.T("KNOCKED OUT. The arena opens again tomorrow.");
            }
            if (round == 1)
            {
                s.arenaRound = 2;
                s.arenaDay = day;
                return Loc.T("ON TO THE FINAL:") + " " + Draw(day).final.Name;
            }
            s.arenaRound = 0;
            s.arenaDoneDay = day;
            s.arenaTitles++;
            s.arenaFinals++;
            if (d.streetCash == null) d.streetCash = new StreetCashSaveData();
            d.streetCash.cash += TitlePrize;
            d.streetCash.earned += TitlePrize;
            d.signalPoints += TitleSp;
            return Loc.T("CITY CHAMPIONS!") + "  +" + StreetCash.Money(TitlePrize) + "  +" + TitleSp + " SP  ·  " + Loc.T("TITLES") + " " + s.arenaTitles;
        }

        public static List<CityActivity> Choices(CareerSaveData d, ContentCatalog c, int day, string back)
        {
            var list = new List<CityActivity>();
            var s = d.bigTime ?? (d.bigTime = new CityBigTimeSaveData());
            if (!Open(d))
            {
                list.Add(new CityActivity { Label = "CITY CHAMPIONSHIP", Locked = "Beat " + CityCrews.BallerWinsForLegendary + " BALLER crews to get in (" + CityCrews.BallerBeaten(d.city) + "/" + CityCrews.BallerWinsForLegendary + ")." });
                return list;
            }
            var draw = Draw(day);
            int round = NextRound(s, day);
            if (round == 0)
                list.Add(new CityActivity { Label = "CITY CHAMPIONSHIP", Locked = "Today's run is over. Back tomorrow for a new draw." });
            else
            {
                var r = Request(c, d, day);
                r.ReturnTo = back;
                var crew = round == 1 ? draw.semi : draw.final;
                list.Add(new CityActivity
                {
                    Label = (round == 1 ? Loc.T("SEMIFINAL") : Loc.T("FINAL")) + ": " + crew.Name, Primary = true, Request = r,
                    Detail = Loc.T(crew.Style) + "  ·  " + Loc.T(crew.TierName) + "  ·  " + Loc.T("Win the final:") + " " + StreetCash.Money(TitlePrize),
                });
            }
            list.Add(new CityActivity
            {
                Label = "TODAY'S DRAW", Action = CityAction.Info,
                Detail = Loc.T("SEMIFINAL") + ": " + draw.semi.Name + "  ·  " + Loc.T("FINAL") + ": " + draw.final.Name + "  ·  " + Loc.T("TITLES") + " " + s.arenaTitles,
            });
            return list;
        }
    }

    // ====================================================================== Phase 6: daily challenges and call-outs

    public enum CityDailyKind { WinIn = 0, Points = 1, Assists = 2, Stops = 3, BeatCrew = 4, Play = 5, Night = 6 }

    public sealed class CityDailyDef
    {
        public CityDailyKind Kind;
        public int Target;
        /// <summary>District id for WinIn.</summary>
        public string Ref;
        public string Text;
    }

    /// <summary>
    /// 17 Oct 2026 (Omari, phase 6): three CITY CHALLENGES a day (the same all day, new ones tomorrow), from games you
    /// start in the city: win in a district, score, dish, get stops, beat a crew, play at night. Each pays $7,500; all
    /// three pays a $15,000 bonus.
    /// </summary>
    public static class CityDaily
    {
        public const long EachPay = 7500, SetBonus = 15000;
        private static readonly string[] Districts = { CityDistricts.Blacktop, CityDistricts.HoopHeights, CityDistricts.Downtown, CityDistricts.Boardwalk, CityDistricts.SchoolYard };

        public static List<CityDailyDef> For(int day)
        {
            var list = new List<CityDailyDef>();
            uint h = StableHash.Of("daily:" + day);
            var kinds = new List<CityDailyKind> { CityDailyKind.WinIn, CityDailyKind.Points, CityDailyKind.Assists, CityDailyKind.Stops, CityDailyKind.BeatCrew, CityDailyKind.Play, CityDailyKind.Night };
            for (int i = 0; i < 3; i++)
            {
                var k = kinds[(int)((h >> (i * 5)) % (uint)kinds.Count)];
                kinds.Remove(k);
                var def = new CityDailyDef { Kind = k };
                switch (k)
                {
                    case CityDailyKind.WinIn:
                        def.Ref = Districts[(h >> 20) % (uint)Districts.Length];
                        def.Target = 1;
                        def.Text = Loc.T("Win a game in") + " " + CityDistricts.Find(def.Ref).Name;
                        break;
                    case CityDailyKind.Points: def.Target = 10 + (int)((h >> 3) % 3) * 2; def.Text = Loc.T("Score") + " " + def.Target + " " + Loc.T("in one city game"); break;
                    case CityDailyKind.Assists: def.Target = 3; def.Text = Loc.T("Get 3 assists in one city game"); break;
                    case CityDailyKind.Stops: def.Target = 3; def.Text = Loc.T("Get 3 steals and blocks in one city game"); break;
                    case CityDailyKind.BeatCrew: def.Target = 1; def.Text = Loc.T("Beat any crew"); break;
                    case CityDailyKind.Play: def.Target = 3; def.Text = Loc.T("Play 3 city games"); break;
                    default: def.Target = 1; def.Text = Loc.T("Win a city game at night (after 9 PM)"); break;
                }
                list.Add(def);
            }
            return list;
        }

        private static void Roll(CityBigTimeSaveData s, int day)
        {
            if (s.dailyDay == day) return;
            s.dailyDay = day;
            s.dailyProgress = new int[3];
            s.dailyPaid = false;
        }

        public static int[] Progress(CityBigTimeSaveData s, int day)
        {
            if (s == null) return new int[3];
            Roll(s, day);
            return s.dailyProgress;
        }

        /// <summary>A finished city game. Returns the cash it unlocked (challenges finished, and the set bonus).</summary>
        public static long ApplyGame(CareerSaveData d, MatchRequest r, bool won, PlayerStatLine human, int day, int hour, out List<CityDailyDef> finished)
        {
            finished = new List<CityDailyDef>();
            if (d == null || r == null || !CityDistricts.TryParseReturn(r.ReturnTo, out string district, out _)) return 0;
            if (r.Mode == GameMode.Practice || r.Mode == GameMode.Tutorial) return 0;
            var s = d.bigTime ?? (d.bigTime = new CityBigTimeSaveData());
            Roll(s, day);
            var defs = For(day);
            long pay = 0;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (s.dailyProgress[i] >= def.Target) continue;
                int v = 0;
                switch (def.Kind)
                {
                    case CityDailyKind.WinIn: v = won && district == def.Ref ? 1 : 0; break;
                    case CityDailyKind.Points: v = human?.points ?? 0; break;
                    case CityDailyKind.Assists: v = human?.assists ?? 0; break;
                    case CityDailyKind.Stops: v = (human?.steals ?? 0) + (human?.blocks ?? 0); break;
                    case CityDailyKind.BeatCrew: v = won && CityCrews.FromContext(r.ContextId) != null ? 1 : 0; break;
                    case CityDailyKind.Play: v = s.dailyProgress[i] + 1; break;
                    default: v = won && (hour >= 21 || hour < 5) ? 1 : 0; break;
                }
                s.dailyProgress[i] = Math.Min(def.Target, Math.Max(s.dailyProgress[i], v));
                if (s.dailyProgress[i] >= def.Target) { finished.Add(def); pay += EachPay; }
            }
            bool all = true;
            for (int i = 0; i < defs.Count; i++) if (s.dailyProgress[i] < defs[i].Target) all = false;
            if (all && !s.dailyPaid)
            {
                s.dailyPaid = true;
                s.dailySets++;
                pay += SetBonus;
            }
            if (pay > 0)
            {
                if (d.streetCash == null) d.streetCash = new StreetCashSaveData();
                d.streetCash.cash += pay;
                d.streetCash.earned += pay;
            }
            return pay;
        }
    }

    /// <summary>
    /// 17 Oct 2026 (Omari, phase 6): CALL-OUTS. Once a day, a crew from the district you walk into comes looking for
    /// you ("You. Me. Right now."). Beat them in the call-out for an extra $10,000 on top of the usual payout.
    /// </summary>
    public static class CityCallout
    {
        public const long Bonus = 10000;

        /// <summary>Today's call-out in this district (a crew that plays here and that you're allowed to face), or null.</summary>
        public static CityCrewDef For(CareerSaveData d, string district, int day)
        {
            var s = d?.bigTime;
            if (s != null && s.calloutDay == day) return null; // one a day
            var map = CityDistricts.Map(district);
            var crews = new List<CityCrewDef>();
            foreach (var k in map.Courts)
            {
                var crew = CityCrews.AtCourt(k.CourtId);
                if (crew != null && CityCrews.Unlocked(d?.city, crew)) crews.Add(crew);
            }
            if (crews.Count == 0) return null;
            uint h = StableHash.Of("callout:" + day + ":" + district);
            if (h % 3 == 0) return null; // not every district, every day
            return crews[(int)((h / 3) % (uint)crews.Count)];
        }

        public static MatchRequest Request(ContentCatalog c, CareerSaveData d, CityCrewDef crew)
        {
            var r = CityCrews.Challenge(c, d, crew);
            r.ContextId = crew.ContextId + "#callout";
            return r;
        }

        public static bool IsCallout(MatchRequest r) => CityCrews.TagOf(r?.ContextId) == "callout";

        /// <summary>Marks today's call-out answered (win or lose); a win pays the bonus. Returns the cash paid.</summary>
        public static long ApplyGame(CareerSaveData d, MatchRequest r, bool won, int day)
        {
            if (d == null || !IsCallout(r)) return 0;
            var s = d.bigTime ?? (d.bigTime = new CityBigTimeSaveData());
            s.calloutDay = day;
            if (!won) return 0;
            s.calloutsWon++;
            if (d.streetCash == null) d.streetCash = new StreetCashSaveData();
            d.streetCash.cash += Bonus;
            d.streetCash.earned += Bonus;
            return Bonus;
        }
    }

    // ====================================================================== Phase 7: YOUR CRIB

    /// <summary>
    /// 17 Oct 2026 (Omari, phase 7): YOUR CRIB, behind your front door. The trophy case fills with City Championship
    /// titles and street milestones, the banner wall hangs a flag for every crew you've beaten, the safe shows your
    /// bank, and the wardrobe opens the Locker Room. The art is drawn from your save each time you walk in.
    /// </summary>
    public static class CityCrib
    {
        /// <summary>The trophy case's art code: titles (0..9) + milestones × 10 (0..99).</summary>
        public static int TrophyVariant(CareerSaveData d) =>
            Math.Min(9, d?.bigTime?.arenaTitles ?? 0) + 10 * Math.Min(99, d?.streetCash?.achievements.Count ?? 0);

        /// <summary>The banner wall's art code: one bit per crew beaten (in <see cref="CityCrews.All"/> order).</summary>
        public static int BannerMask(CareerSaveData d)
        {
            int mask = 0;
            for (int i = 0; i < CityCrews.All.Length && i < 30; i++)
                if (d?.city != null && d.city.crewsBeaten.Contains(CityCrews.All[i].Id)) mask |= 1 << i;
            return mask;
        }

        /// <summary>The crib's save-dependent pieces (trophy case and banner wall), placed where the map leaves room for them.</summary>
        public static List<CityProp> Decor(CareerSaveData d) => new List<CityProp>
        {
            new CityProp { Kind = CityPropKind.TrophyCase, X = 2, Y = 10, W = 4, H = 1, Solid = true, Variant = TrophyVariant(d) },
            new CityProp { Kind = CityPropKind.BannerWall, X = 9, Y = 11, W = 6, H = 1, Solid = true, Variant = BannerMask(d) },
        };

        public static List<CityActivity> Choices(CityPortal portal, CareerSaveData d)
        {
            var list = new List<CityActivity>();
            switch (portal.Target)
            {
                case "crib.trophies":
                {
                    int titles = d?.bigTime?.arenaTitles ?? 0;
                    list.Add(new CityActivity { Label = Loc.T("CITY CHAMPIONSHIPS") + ": " + titles, Action = CityAction.Info, Detail = titles == 0 ? Loc.T("Win one at the Baller Arena, Downtown.") : null });
                    foreach (var m in StreetCash.Milestones)
                        if (d?.streetCash != null && d.streetCash.achievements.Contains(m.Id))
                            list.Add(new CityActivity { Label = Loc.T(m.Name), Detail = Loc.T(m.Detail), Action = CityAction.Info });
                    break;
                }
                case "crib.banners":
                    foreach (var crew in CityCrews.All)
                    {
                        bool beaten = d?.city != null && d.city.crewsBeaten.Contains(crew.Id);
                        list.Add(new CityActivity { Label = (beaten ? "" : "?  ") + crew.Name, Action = CityAction.Info,
                                                    Detail = beaten ? Loc.T(crew.Style) + "  ·  " + Loc.T(crew.TierName) : Loc.T("Not beaten yet.") });
                    }
                    break;
                case "crib.safe":
                    list.Add(new CityActivity
                    {
                        Label = Loc.T("THE SAFE") + ": " + StreetCash.Money(d?.streetCash?.cash ?? 0), Action = CityAction.Info,
                        Detail = Loc.T("EARNED") + " " + StreetCash.Money(d?.streetCash?.earned ?? 0) + "  ·  " + Loc.T("Spend it at the Corner Store."),
                    });
                    break;
            }
            return list;
        }
    }
}
