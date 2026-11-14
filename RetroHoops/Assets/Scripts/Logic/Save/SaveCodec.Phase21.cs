using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public static partial class SaveCodec
    {
        // A Franchise has ~70 players, so each one is a compact number array plus its name.
        private const int FrFields = 29;

        private static object EncodeFranchise(FranchiseSaveData f)
        {
            if (f == null || !f.active) return new Dictionary<string, object> { ["active"] = false };
            return new Dictionary<string, object>
            {
                ["active"] = true,
                ["year"] = f.year,
                ["seed"] = (double)f.seed,
                ["you"] = f.you,
                ["phase"] = (int)f.phase,
                ["nextId"] = f.nextId,
                ["teams"] = List(f.teams, t => new Dictionary<string, object>
                {
                    ["base"] = t.baseId, ["dead"] = t.deadMoney, ["roster"] = List(t.roster, id => (object)(double)id),
                }),
                ["players"] = List(f.players, p => new Dictionary<string, object>
                {
                    ["f"] = p.first, ["l"] = p.last,
                    ["n"] = new List<object>
                    {
                        (double)p.id, (double)p.number, (double)p.archetype, (double)p.skin, (double)p.hair, (double)p.hairColor, (double)p.body, (double)p.height,
                        (double)p.attrs.finishing, (double)p.attrs.shooting, (double)p.attrs.playmaking, (double)p.attrs.defense,
                        (double)p.attrs.rebounding, (double)p.attrs.speed, (double)p.attrs.stamina, (double)p.attrs.clutch,
                        (double)p.age, (double)p.potential, (double)p.salary, (double)p.years, (double)p.team, p.prospect ? 1.0 : 0.0, (double)p.scout,
                        (double)p.seasons, (double)p.titles, (double)p.peak, (double)p.gp, (double)p.pts, (double)p.careerPts,
                        (double)p.draftYear, (double)p.draftPick, p.created ? 1.0 : 0.0, (double)p.mood,
                        (double)p.reb, (double)p.ast, (double)p.stl, (double)p.blk, (double)p.careerReb, (double)p.careerAst,
                        (double)p.startOvr, (double)p.injured, (double)p.injSlot, p.onBlock ? 1.0 : 0.0, (double)p.extension,
                    },
                    ["h"] = List(p.history, h => (object)new List<object> { (double)h.year, (double)h.team, (double)h.gp, (double)h.pts, (double)h.reb, (double)h.ast, (double)h.ovr }),
                }),
                ["season"] = new Dictionary<string, object>
                {
                    ["n"] = f.season.seasonNumber, ["seed"] = (double)f.season.seed, ["teams"] = Strings(f.season.teamIds),
                    ["week"] = f.season.currentWeek, ["weeks"] = f.season.weeks, ["games"] = List(f.season.games, EncodeGame),
                },
                ["draft"] = List(f.draftOrder, t => (object)(double)t),
                ["made"] = f.draftMade,
                ["lottery"] = Strings(f.lottery),
                ["scout"] = f.scoutPoints,
                ["focus"] = f.focus,
                ["offerWeek"] = f.offerWeek,
                ["dev"] = Strings(f.devReport),
                ["shots"] = ShotCharts.Encode(f.teamChart),
                ["coach"] = f.coach,
                ["offer"] = f.offer == null ? null : (object)new Dictionary<string, object>
                {
                    ["team"] = f.offer.team, ["want"] = List(f.offer.want, id => (object)(double)id), ["send"] = List(f.offer.send, id => (object)(double)id), ["week"] = f.offer.week, ["pitch"] = f.offer.pitch ?? "",
                },
                ["history"] = List(f.history, h => new Dictionary<string, object>
                {
                    ["y"] = h.year, ["t"] = h.team, ["w"] = h.wins, ["l"] = h.losses, ["f"] = h.finish, ["p"] = h.payroll, ["b"] = h.best,
                }),
                ["awards"] = List(f.awards, a => new Dictionary<string, object> { ["y"] = a.year, ["k"] = a.kind, ["n"] = a.name, ["t"] = a.team, ["l"] = a.line }),
                ["moves"] = List(f.moves, m => new Dictionary<string, object> { ["y"] = m.year, ["x"] = m.text }),
                ["titles"] = f.titles,
                ["office"] = EncodeOffice(FrontOffice.Of(f)),
                ["league"] = EncodeLeague(FranchiseLeague.Of(f)),
            };
        }

        /// <summary>I61–I70 front office (older saves have none: everything starts at zero).</summary>
        private static object EncodeOffice(FrontOfficeData o) => new Dictionary<string, object>
        {
            ["c"] = o.credits, ["fans"] = o.fanDelta, ["gym"] = o.gym, ["film"] = o.film, ["arena"] = o.arena, ["lounge"] = o.lounge,
            ["huddle"] = o.huddle, ["press"] = o.press, ["pp"] = o.pressPlayer, ["lock"] = o.locker, ["lp"] = o.lockerPlayer, ["lw"] = o.lockerWeek,
            ["g"] = o.games, ["st"] = o.streak, ["note"] = o.note ?? "",
            ["len"] = o.length, ["bs"] = o.bestStreak, ["bw"] = o.biggestWin, ["mp"] = o.mostPoints, ["rival"] = o.rival,
            ["goal"] = o.goal, ["gy"] = o.goalYear, ["ask"] = o.tradeAsk, ["talked"] = o.talked, ["cap"] = o.captain,
            ["sc"] = o.seasonCredits, ["sf"] = o.seasonFans, ["review"] = Strings(o.review),
        };

        private static FrontOfficeData DecodeOffice(Dictionary<string, object> d)
        {
            var o = new FrontOfficeData();
            if (d == null) return o;
            int Lv(string k) => Math.Max(0, Math.Min(FrontOffice.MaxLevel, Int(d, k, 0)));
            o.credits = Math.Max(0, Math.Min(100000, Int(d, "c", 0)));
            o.fanDelta = Math.Max(-FrontOffice.BaseFans, Math.Min(100 - FrontOffice.BaseFans, Int(d, "fans", 0)));
            o.gym = Lv("gym"); o.film = Lv("film"); o.arena = Lv("arena"); o.lounge = Lv("lounge");
            o.huddle = Bool(d, "huddle", false);
            o.press = Int(d, "press", -1);
            if (o.press < -1 || o.press >= FrontOffice.Questions.Length) o.press = -1;
            o.pressPlayer = Int(d, "pp", -1);
            o.locker = Int(d, "lock", -1);
            if (o.locker < -1 || o.locker >= FrontOffice.Events.Length) o.locker = -1;
            o.lockerPlayer = Int(d, "lp", -1);
            o.lockerWeek = Int(d, "lw", -1);
            o.games = Math.Max(0, Int(d, "g", 0));
            o.streak = Int(d, "st", 0);
            o.note = Str(d, "note", "");
            o.length = Math.Max(0, Math.Min(FrontOffice.LengthNames.Length - 1, Int(d, "len", 0)));
            o.bestStreak = Math.Max(0, Int(d, "bs", 0));
            o.biggestWin = Math.Max(0, Int(d, "bw", 0));
            o.mostPoints = Math.Max(0, Int(d, "mp", 0));
            o.rival = Int(d, "rival", -1);
            if (o.rival < -1 || o.rival >= Franchise.Teams) o.rival = -1;
            o.goal = Int(d, "goal", -1);
            if (o.goal < -1 || o.goal >= FrontOffice.GoalNames.Length) o.goal = -1;
            o.goalYear = Int(d, "gy", 0);
            o.tradeAsk = Int(d, "ask", -1);
            o.talked = Bool(d, "talked", false);
            o.captain = Int(d, "cap", -1);
            o.seasonCredits = Math.Max(0, Int(d, "sc", 0));
            o.seasonFans = Math.Max(0, Math.Min(100, Int(d, "sf", FrontOffice.BaseFans)));
            o.review = StrList(d, "review");
            return o;
        }

        /// <summary>I81–I95 league extras.</summary>
        private static object EncodeLeague(FrLeagueData l) => new Dictionary<string, object>
        {
            ["inj"] = l.injuries, ["city"] = l.city ?? "", ["nick"] = l.nickname ?? "", ["abbr"] = l.abbr ?? "", ["legacy"] = l.legacy,
            ["pv"] = List(l.preview, t => (object)(double)t), ["pvy"] = l.previewYear, ["pvr"] = l.previewResult ?? "",
            ["grades"] = List(l.grades, g => new Dictionary<string, object> { ["y"] = g.year, ["t"] = g.team, ["g"] = g.grade, ["l"] = g.line }),
            ["ext"] = List(l.extended, id => (object)(double)id),
        };

        private static FrLeagueData DecodeLeague(Dictionary<string, object> d)
        {
            var l = new FrLeagueData();
            if (d == null) return l;
            l.injuries = Bool(d, "inj", false);
            l.city = TextSafety.Field(Str(d, "city", ""), "");
            l.nickname = TextSafety.Field(Str(d, "nick", ""), "");
            l.abbr = TextSafety.Plain(TextSafety.Field(Str(d, "abbr", ""), ""), FranchiseLeague.MaxAbbr);
            if (l.city.Length > FranchiseLeague.MaxCity) l.city = l.city.Substring(0, FranchiseLeague.MaxCity);
            if (l.nickname.Length > FranchiseLeague.MaxNickname) l.nickname = l.nickname.Substring(0, FranchiseLeague.MaxNickname);
            l.legacy = Math.Max(0, Int(d, "legacy", 0));
            foreach (var v in Arr(d, "pv")) if (v is double x && x >= 0 && x < Franchise.Teams) l.preview.Add((int)x);
            l.previewYear = Int(d, "pvy", 0);
            l.previewResult = Str(d, "pvr", "");
            foreach (var item in Arr(d, "grades"))
                if (item is Dictionary<string, object> g)
                    l.grades.Add(new FrDraftGrade { year = Int(g, "y", 0), team = Int(g, "t", 0), grade = Str(g, "g", ""), line = Str(g, "l", "") });
            foreach (var v in Arr(d, "ext")) if (v is double x) l.extended.Add((int)x);
            return l;
        }

        private static FranchiseSaveData DecodeFranchise(Dictionary<string, object> o)
        {
            var f = new FranchiseSaveData();
            if (o == null || !Bool(o, "active", false)) return f;
            try
            {
                f.active = true;
                f.year = Math.Max(1, Int(o, "year", 1));
                f.seed = (uint)Math.Max(1, Num(o, "seed", 1));
                f.phase = (FranchisePhase)Math.Max(0, Math.Min((int)FranchisePhase.Preseason, Int(o, "phase", 0)));
                f.nextId = Math.Max(1, Int(o, "nextId", 1));
                foreach (var item in Arr(o, "teams"))
                {
                    if (!(item is Dictionary<string, object> t)) continue;
                    var team = new FrTeam { baseId = Str(t, "base", null), deadMoney = Math.Max(0, Int(t, "dead", 0)) };
                    foreach (var v in Arr(t, "roster")) if (v is double d) team.roster.Add((int)d);
                    f.teams.Add(team);
                }
                foreach (var item in Arr(o, "players"))
                {
                    if (!(item is Dictionary<string, object> p)) continue;
                    var n = Arr(p, "n");
                    if (n.Count < FrFields) continue;
                    int I(int k) => n[k] is double d ? (int)Math.Round(d) : 0;
                    int R(int k) => RatingScale.Clamp(I(k));
                    var fp = new FrPlayer
                    {
                        first = Str(p, "f", "?"), last = Str(p, "l", ""),
                        id = I(0), number = Math.Max(0, Math.Min(99, I(1))), archetype = Math.Max(0, Math.Min(11, I(2))),
                        skin = I(3), hair = I(4), hairColor = I(5), body = Math.Max(0, Math.Min(2, I(6))), height = Math.Max(0, Math.Min(2, I(7))),
                        attrs = new AttributeSet(R(8), R(9), R(10), R(11), R(12), R(13), R(14), R(15)),
                        age = Math.Max(16, Math.Min(50, I(16))), potential = R(17), salary = Math.Max(0, Math.Min(Franchise.MaxSalary, I(18))),
                        years = Math.Max(0, Math.Min(5, I(19))), team = I(20), prospect = I(21) != 0, scout = Math.Max(0, Math.Min(3, I(22))),
                        seasons = I(23), titles = I(24), peak = I(25), gp = I(26), pts = I(27), careerPts = I(28),
                        draftYear = n.Count > 29 ? I(29) : 0, draftPick = n.Count > 30 ? I(30) : 0, created = n.Count > 31 && I(31) != 0,
                        mood = n.Count > 32 ? Math.Max(-FrontOffice.BaseMorale, Math.Min(100 - FrontOffice.BaseMorale, I(32))) : 0,
                    };
                    if (n.Count > 43)
                    {
                        // I81–I95 (older saves don't have these: zeros).
                        fp.reb = Math.Max(0, I(33)); fp.ast = Math.Max(0, I(34)); fp.stl = Math.Max(0, I(35)); fp.blk = Math.Max(0, I(36));
                        fp.careerReb = Math.Max(0, I(37)); fp.careerAst = Math.Max(0, I(38)); fp.startOvr = Math.Max(0, I(39));
                        fp.injured = Math.Max(0, Math.Min(5, I(40))); fp.injSlot = I(41); fp.onBlock = I(42) != 0;
                        fp.extension = Math.Max(0, Math.Min(Franchise.MaxSalary, I(43)));
                    }
                    foreach (var hv in Arr(p, "h"))
                    {
                        if (!(hv is List<object> hl) || hl.Count < 7) continue;
                        int H(int k) => hl[k] is double d ? (int)Math.Round(d) : 0;
                        fp.history.Add(new FrSeasonLine { year = H(0), team = H(1), gp = Math.Max(0, H(2)), pts = Math.Max(0, H(3)), reb = Math.Max(0, H(4)), ast = Math.Max(0, H(5)), ovr = H(6) });
                        if (fp.history.Count >= 20) break;
                    }
                    if (fp.team >= f.teams.Count || fp.team < -1) fp.team = -1;
                    f.players.Add(fp);
                }
                var s = Obj(o, "season");
                f.season = new SeasonSaveData
                {
                    seasonNumber = Int(s, "n", f.year), seed = (uint)Math.Max(0, Num(s, "seed", 1)), teamIds = StrList(s, "teams"),
                    currentWeek = Int(s, "week", 0), weeks = Int(s, "weeks", Franchise.Weeks),
                };
                foreach (var item in Arr(s, "games")) if (item is Dictionary<string, object> g) f.season.games.Add(DecodeGame(g));
                foreach (var v in Arr(o, "draft")) if (v is double d) f.draftOrder.Add((int)d);
                f.draftMade = Math.Max(0, Int(o, "made", 0));
                f.lottery = StrList(o, "lottery");
                f.scoutPoints = Math.Max(0, Int(o, "scout", 0));
                f.focus = Math.Max(0, Math.Min(FranchiseDepth.FocusNames.Length - 1, Int(o, "focus", 0)));
                f.offerWeek = Int(o, "offerWeek", -1);
                f.devReport = StrList(o, "dev");
                f.teamChart = ShotCharts.Decode(Str(o, "shots", ""));
                f.coach = Bool(o, "coach", false);
                var off = Obj(o, "offer");
                if (off != null)
                {
                    f.offer = new FrOffer { team = Int(off, "team", -1), week = Int(off, "week", 0), pitch = Str(off, "pitch", "") };
                    foreach (var v in Arr(off, "want")) if (v is double d) f.offer.want.Add((int)d);
                    foreach (var v in Arr(off, "send")) if (v is double d) f.offer.send.Add((int)d);
                    if (f.offer.team < 0 || f.offer.team >= f.teams.Count || f.offer.want.Count == 0 || f.offer.send.Count == 0) f.offer = null;
                }
                foreach (var item in Arr(o, "history"))
                    if (item is Dictionary<string, object> h)
                        f.history.Add(new FrHistory { year = Int(h, "y", 0), team = Int(h, "t", 0), wins = Int(h, "w", 0), losses = Int(h, "l", 0), finish = Int(h, "f", 0), payroll = Int(h, "p", 0), best = Str(h, "b", "") });
                foreach (var item in Arr(o, "awards"))
                    if (item is Dictionary<string, object> a)
                        f.awards.Add(new FrAward { year = Int(a, "y", 0), kind = Str(a, "k", ""), name = Str(a, "n", ""), team = Int(a, "t", -1), line = Str(a, "l", "") });
                foreach (var item in Arr(o, "moves"))
                    if (item is Dictionary<string, object> m) f.moves.Add(new FrMove { year = Int(m, "y", 0), text = Str(m, "x", "") });
                f.titles = Math.Max(0, Int(o, "titles", 0));
                f.office = DecodeOffice(Obj(o, "office"));
                f.league = DecodeLeague(Obj(o, "league"));
                f.you = Math.Max(0, Math.Min(f.teams.Count - 1, Int(o, "you", 0)));

                // Rosters must point at real players on that team; anything else is dropped.
                for (int t = 0; t < f.teams.Count; t++)
                    f.teams[t].roster.RemoveAll(id => !f.players.Exists(p => p.id == id && p.team == t));
                foreach (var p in f.players) if (p.team >= 0 && !f.teams[p.team].roster.Contains(p.id)) f.teams[p.team].roster.Add(p.id);
                if (f.teams.Count != Franchise.Teams || f.season.teamIds.Count != Franchise.Teams) return new FranchiseSaveData();
            }
            catch (Exception)
            {
                return new FranchiseSaveData();
            }
            return f;
        }
    
        private static object EncodeAllStar(AllStarSaveData a)
        {
            a = a ?? new AllStarSaveData();
            var k = a.contest ?? new ContestSaveData();
            return new Dictionary<string, object>
            {
                ["season"] = a.season, ["dunkDone"] = a.dunkDone, ["threeDone"] = a.threeDone, ["gameDone"] = a.gameDone,
                ["dunkTitles"] = a.dunkTitles, ["threeTitles"] = a.threeTitles, ["games"] = a.allStarGames, ["wins"] = a.allStarWins, ["bestDunk"] = a.bestDunk,
                ["contest"] = new Dictionary<string, object>
                {
                    ["kind"] = k.kind ?? "", ["seed"] = (double)k.seed, ["round"] = k.round, ["dunks"] = k.yourDunks, ["roundTotal"] = k.yourRoundTotal,
                    ["rise"] = k.rise, ["difficulty"] = k.difficultyId, ["champion"] = k.championName, ["youWon"] = k.youWon,
                    ["field"] = List(k.field, e => new Dictionary<string, object>
                    {
                        ["id"] = e.playerId, ["name"] = e.name, ["you"] = e.you, ["r1"] = e.r1, ["r2"] = e.r2, ["used"] = Strings(e.used),
                    }),
                },
            };
        }

        private static AllStarSaveData DecodeAllStar(Dictionary<string, object> o)
        {
            var a = new AllStarSaveData();
            if (o == null) return a;
            a.season = Int(o, "season", 0);
            a.dunkDone = Bool(o, "dunkDone", false);
            a.threeDone = Bool(o, "threeDone", false);
            a.gameDone = Bool(o, "gameDone", false);
            a.dunkTitles = Math.Max(0, Int(o, "dunkTitles", 0));
            a.threeTitles = Math.Max(0, Int(o, "threeTitles", 0));
            a.allStarGames = Math.Max(0, Int(o, "games", 0));
            a.allStarWins = Math.Max(0, Int(o, "wins", 0));
            a.bestDunk = Math.Max(0, Int(o, "bestDunk", 0));
            var k = Obj(o, "contest");
            if (k != null)
            {
                var c = a.contest;
                c.kind = Str(k, "kind", "");
                if (c.kind != "dunk" && c.kind != "three") c.kind = "";
                c.seed = (uint)Math.Max(1, Num(k, "seed", 1));
                c.round = Math.Max(1, Math.Min(3, Int(k, "round", 1)));
                c.yourDunks = Math.Max(0, Math.Min(AllStar.DunksPerRound - 1, Int(k, "dunks", 0)));
                c.yourRoundTotal = Math.Max(0, Int(k, "roundTotal", 0));
                c.rise = Bool(k, "rise", false);
                c.difficultyId = Str(k, "difficulty", null);
                c.championName = Str(k, "champion", "");
                c.youWon = Bool(k, "youWon", false);
                foreach (var item in Arr(k, "field"))
                    if (item is Dictionary<string, object> e)
                        c.field.Add(new ContestEntrant
                        {
                            playerId = Str(e, "id", null), name = Str(e, "name", "?"), you = Bool(e, "you", false),
                            r1 = Int(e, "r1", -1), r2 = Int(e, "r2", -1), used = StrList(e, "used"),
                        });
                if (c.field.Count != AllStar.Entrants || !c.field.Exists(e => e.you)) a.contest = new ContestSaveData();
            }
            return a;
        }
    
        private static object EncodeCourts(List<CustomCourtData> courts) => List(CourtBuilder.Ensure(courts), d => new Dictionary<string, object>
        {
            ["built"] = d.built, ["name"] = d.name, ["style"] = d.floorStyle, ["floor"] = d.floor, ["lines"] = d.lines, ["paint"] = d.paint,
            ["sky"] = d.sky, ["crowd"] = d.crowd, ["stands"] = d.stands, ["motif"] = d.logoMotif, ["shape"] = d.logoShape, ["logo"] = d.logoColor,
        });

        private static List<CustomCourtData> DecodeCourts(List<object> items)
        {
            var list = new List<CustomCourtData>();
            foreach (var item in items)
            {
                if (!(item is Dictionary<string, object> o)) continue;
                var d = new CustomCourtData();
                d.built = Bool(o, "built", false);
                d.name = Str(o, "name", "My Court");
                d.floorStyle = Int(o, "style", 0);
                d.floor = Int(o, "floor", d.floor);
                d.lines = Int(o, "lines", d.lines);
                d.paint = Int(o, "paint", d.paint);
                d.sky = Int(o, "sky", 0);
                d.crowd = Int(o, "crowd", 2);
                d.stands = Int(o, "stands", 0);
                d.logoMotif = Int(o, "motif", -1);
                d.logoShape = Int(o, "shape", 0);
                d.logoColor = Int(o, "logo", d.logoColor);
                list.Add(d);
            }
            return CourtBuilder.Ensure(list);
        }
    }
}
