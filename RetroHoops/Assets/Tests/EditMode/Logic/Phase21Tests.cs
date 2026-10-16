using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Franchise mode: league setup, season, off-season, contracts, trades, save.</summary>
    public class FranchiseTests
    {
        private ContentCatalog _c;
        private FranchiseSaveData _f;

        [SetUp]
        public void Setup()
        {
            _c = DefaultContent.Create();
            var league = _c.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            _f = Franchise.Create(_c, league[2].id, 77);
        }

        [Test]
        public void Create_EightClubs_LegalRosters_UnderCap_AndYourTeam()
        {
            Assert.AreEqual(Franchise.Teams, _f.teams.Count);
            Assert.AreEqual(2, _f.you);
            for (int t = 0; t < Franchise.Teams; t++)
            {
                int n = _f.teams[t].roster.Count;
                Assert.GreaterOrEqual(n, Franchise.MinRoster, "team " + t);
                Assert.LessOrEqual(n, Franchise.MaxRoster);
                Assert.LessOrEqual(Franchise.Payroll(_f, t), Franchise.Cap, "payroll team " + t);
            }
            Assert.Greater(Franchise.FreeAgents(_f).Count, 0);
            Assert.AreEqual(Franchise.Weeks * Franchise.Teams / 2, _f.season.games.Count);
        }

        [Test]
        public void Schedule_IsDoubleRoundRobin_OneGameAWeek()
        {
            var meet = new Dictionary<string, int>();
            for (int w = 0; w < Franchise.Weeks; w++)
            {
                var seen = new HashSet<string>();
                foreach (var g in _f.season.games)
                {
                    if (g.week != w) continue;
                    Assert.IsTrue(seen.Add(g.homeId) && seen.Add(g.awayId), "a team plays twice in week " + w);
                    string key = string.CompareOrdinal(g.homeId, g.awayId) < 0 ? g.homeId + g.awayId : g.awayId + g.homeId;
                    meet[key] = meet.TryGetValue(key, out int k) ? k + 1 : 1;
                }
                Assert.AreEqual(Franchise.Teams, seen.Count);
            }
            Assert.AreEqual(28, meet.Count);
            foreach (var kv in meet) Assert.AreEqual(2, kv.Value);
        }

        [Test]
        public void NextMatch_IsFullCourt_WithFranchiseClubs_InTheCatalog()
        {
            var req = Franchise.NextMatch(_f, _c, DefaultContent.DefaultDifficultyId);
            Assert.IsNotNull(req);
            Assert.AreEqual(GameMode.Franchise, req.Mode);
            Assert.IsTrue(req.FullCourt);
            Assert.AreEqual(Franchise.TeamId(_f.you), req.HomeTeamId);
            var setup = MatchSetup.FromRequest(req, _c);
            Assert.AreEqual(5, setup.RosterA.Count);
            Assert.AreEqual(2, setup.BenchA.Count);
            Assert.AreEqual(Franchise.PlayerId(_f.teams[_f.you].roster[0]), setup.RosterA[0].id, "you control the top of your depth chart");
            Assert.AreEqual(TeamTier.Franchise, _c.Team(req.AwayTeamId).tier);
            Assert.IsNotNull(_c.Court(req.CourtId));
        }

        [Test]
        public void RecordYourGame_CountsIt_AndSimulatesTheWeek()
        {
            var g = Franchise.NextGame(_f);
            Assert.IsTrue(Franchise.RecordYourGame(_f, _c, 30, 22));
            Assert.IsTrue(g.played);
            Assert.AreEqual(Franchise.TeamId(_f.you), g.WinnerId);
            foreach (var other in _f.season.games) if (other.week == 0) Assert.IsTrue(other.played);
            Assert.AreEqual(1, _f.season.currentWeek);
            int pts = 0;
            foreach (var p in Franchise.Roster(_f, _f.you)) pts += p.pts;
            Assert.AreEqual(30, pts, "your points are shared among your players");
        }

        private void PlaySeason()
        {
            Franchise.SimToEnd(_f, _c);
            Assert.AreEqual(FranchisePhase.Playoffs, _f.phase);
            Franchise.SimToEnd(_f, _c);
            Assert.AreEqual(FranchisePhase.Draft, _f.phase);
        }

        [Test]
        public void FullSeason_PlayoffsThenDraft_WithHistoryAndAwards()
        {
            PlaySeason();
            Assert.AreEqual(2, _f.year);
            Assert.AreEqual(Franchise.Teams, _f.history.Count);
            Assert.AreEqual(1, _f.history.FindAll(h => h.finish == 3).Count, "one champion");
            Assert.AreEqual(1, _f.history.FindAll(h => h.finish == 2).Count);
            Assert.AreEqual(2, _f.history.FindAll(h => h.finish == 1).Count);
            Assert.IsTrue(_f.awards.Exists(a => a.kind == "MVP" && a.year == 1));
            Assert.AreEqual(Franchise.ProspectsPerClass, Franchise.Prospects(_f).Count);
            Assert.AreEqual(Franchise.Teams, _f.draftOrder.Count);
            CollectionAssert.AllItemsAreUnique(_f.draftOrder);
            Assert.Greater(_f.lottery.Count, 0);
            // The champion picks last; the first two picks are lottery (non-playoff) teams.
            int champ = Franchise.IndexOf(Franchise.ChampionId(_f));
            Assert.AreEqual(champ, _f.draftOrder[Franchise.Teams - 1]);
            for (int i = 0; i < 4; i++) Assert.AreEqual(0, _f.history.Find(h => h.team == _f.draftOrder[i]).finish);
        }

        [Test]
        public void Lottery_IsWeighted_TheWorstTeamWinsMostOften()
        {
            int worstWins = 0, bestOfLottery = 0;
            for (uint s = 1; s <= 40; s++)
            {
                var league = _c.TeamsInTier(TeamTier.League);
                var f = Franchise.Create(_c, league[0].id, s);
                Franchise.SimToEnd(f, _c);
                var table = SeasonEngine.Standings(f.season);
                Franchise.SimToEnd(f, _c);
                int worst = Franchise.IndexOf(table[7].TeamId), fourth = Franchise.IndexOf(table[4].TeamId);
                if (f.draftOrder[0] == worst) worstWins++;
                if (f.draftOrder[0] == fourth) bestOfLottery++;
            }
            Assert.Greater(worstWins, bestOfLottery);
            Assert.Greater(worstWins, 6);
        }

        [Test]
        public void Scouting_NarrowsTheRange_AndAlwaysContainsTheTruth()
        {
            PlaySeason();
            var p = Franchise.Prospects(_f)[0];
            Franchise.ScoutedRange(p, out int lo0, out int hi0);
            Assert.AreEqual("?", Franchise.PotentialGrade(p));
            Assert.IsTrue(Franchise.Scout(_f, p.id));
            Assert.IsTrue(Franchise.Scout(_f, p.id));
            Assert.AreNotEqual("?", Franchise.PotentialGrade(p));
            Assert.IsTrue(Franchise.Scout(_f, p.id));
            Assert.IsFalse(Franchise.Scout(_f, p.id), "fully scouted");
            Franchise.ScoutedRange(p, out int lo3, out int hi3);
            Assert.AreEqual(p.Overall, lo3);
            Assert.AreEqual(p.Overall, hi3);
            Assert.Greater(hi0 - lo0, 0);
            Assert.AreEqual(Franchise.ScoutPointsPerDraft - 3, _f.scoutPoints);
            foreach (var q in Franchise.Prospects(_f))
            {
                Franchise.ScoutedRange(q, out int lo, out int hi);
                Assert.LessOrEqual(lo, q.Overall);
                Assert.GreaterOrEqual(hi, q.Overall);
            }
        }

        [Test]
        public void Draft_AiPicksUntilYou_YouPick_RookieContract_ThenReSign()
        {
            PlaySeason();
            Franchise.DraftUntilYou(_f);
            Assert.AreEqual(_f.you, Franchise.OnTheClock(_f));
            int slot = _f.draftMade;
            var pick = Franchise.Prospects(_f)[0];
            int before = _f.teams[_f.you].roster.Count;
            Assert.IsTrue(Franchise.Pick(_f, pick.id));
            Assert.AreEqual(before + 1, _f.teams[_f.you].roster.Count);
            Assert.AreEqual(Franchise.RookieScale[slot], pick.salary);
            Assert.AreEqual(3, pick.years);
            Assert.AreEqual(FranchisePhase.ReSign, _f.phase);
            Assert.AreEqual(0, Franchise.Prospects(_f).Count, "undrafted become free agents");
        }

        private void ToFreeAgency()
        {
            PlaySeason();
            Franchise.DraftUntilYou(_f);
            if (Franchise.OnTheClock(_f) == _f.you) Franchise.Pick(_f, Franchise.Prospects(_f)[0].id);
            foreach (var p in Franchise.Expiring(_f)) Franchise.ReSign(_f, p.id);
            Franchise.FinishReSign(_f);
            Assert.AreEqual(FranchisePhase.FreeAgency, _f.phase);
        }

        [Test]
        public void ReSign_KeepsYourPlayers_FinishReSign_FreesTheRest()
        {
            PlaySeason();
            Franchise.DraftUntilYou(_f);
            if (Franchise.OnTheClock(_f) == _f.you) Franchise.Pick(_f, Franchise.Prospects(_f)[0].id);
            var expiring = Franchise.Expiring(_f);
            foreach (var p in Franchise.Roster(_f, _f.you)) Assert.GreaterOrEqual(p.years, 0);
            if (expiring.Count > 0)
            {
                Assert.IsTrue(Franchise.ReSign(_f, expiring[0].id));
                Assert.Greater(expiring[0].years, 0);
            }
            Franchise.FinishReSign(_f);
            foreach (var p in _f.players) if (p.team >= 0) Assert.Greater(p.years, 0, p.Name + " is under contract");
        }

        [Test]
        public void FreeAgency_CapRules_AiFillsRosters_ThenPreseason()
        {
            ToFreeAgency();
            var fas = Franchise.FreeAgents(_f);
            Assert.Greater(fas.Count, 0);
            // Fill your roster to the max with the cheapest players.
            fas.Sort((a, b) => Franchise.Demand(a).CompareTo(Franchise.Demand(b)));
            foreach (var p in fas)
            {
                if (_f.teams[_f.you].roster.Count >= Franchise.MaxRoster) break;
                if (Franchise.CannotSign(_f, p) == null) Assert.IsTrue(Franchise.SignFreeAgent(_f, p.id));
            }
            var next = Franchise.FreeAgents(_f);
            if (next.Count > 0 && _f.teams[_f.you].roster.Count >= Franchise.MaxRoster)
                StringAssert.Contains("Roster full", Franchise.CannotSign(_f, next[0]));
            Franchise.FinishFreeAgency(_f, _c);
            Assert.AreEqual(FranchisePhase.Preseason, _f.phase);
            for (int t = 0; t < Franchise.Teams; t++)
                if (t != _f.you) Assert.GreaterOrEqual(_f.teams[t].roster.Count, Franchise.MinRoster);
            Assert.IsNull(Franchise.CannotStart(_f));
            Assert.IsTrue(Franchise.StartSeason(_f, _c));
            Assert.AreEqual(FranchisePhase.Regular, _f.phase);
            Assert.AreEqual(2, _f.season.seasonNumber);
            Assert.AreEqual(0, _f.season.currentWeek);
        }

        [Test]
        public void Cap_BlocksAnExpensiveSigning_ButMinimumDealsAlwaysFit()
        {
            ToFreeAgency();
            // Load your payroll up to the cap with dead money.
            _f.teams[_f.you].deadMoney = Franchise.Cap;
            while (_f.teams[_f.you].roster.Count > Franchise.MinRoster) Franchise.Release(_f, _f.teams[_f.you].roster[0]);
            var star = new FrPlayer { id = 9001, first = "Test", last = "Star", attrs = new AttributeSet(85, 85, 85, 85, 85, 85, 85, 85), age = 27, potential = 85 };
            var scrub = new FrPlayer { id = 9002, first = "Test", last = "Min", attrs = new AttributeSet(45, 45, 45, 45, 45, 45, 45, 45), age = 27, potential = 45 };
            _f.players.Add(star);
            _f.players.Add(scrub);
            StringAssert.Contains("cap room", Franchise.CannotSign(_f, star));
            Assert.AreEqual(Franchise.MinSalary, Franchise.Demand(scrub));
            Assert.IsNull(Franchise.CannotSign(_f, scrub));
        }

        [Test]
        public void Release_LeavesDeadMoney_AndNotBelowSeven_InSeason()
        {
            var you = _f.teams[_f.you];
            while (you.roster.Count > Franchise.MinRoster)
            {
                var p = Franchise.Player(_f, you.roster[you.roster.Count - 1]);
                int dead = you.deadMoney, salary = p.salary;
                Assert.IsTrue(Franchise.Release(_f, p.id));
                Assert.AreEqual(dead + salary / 2, you.deadMoney, "half of a salary stays");
                Assert.AreEqual(-1, p.team);
            }
            Assert.IsFalse(Franchise.Release(_f, you.roster[0]), "seven is the minimum in season");
        }

        [Test]
        public void Values_StarsOutweighDepth_YouthAndContractsMatter()
        {
            FrPlayer P(int ovr, int age, int pot, int salary, int years) =>
                new FrPlayer { attrs = new AttributeSet(ovr, ovr, ovr, ovr, ovr, ovr, ovr, ovr), age = age, potential = pot, salary = salary, years = years };
            var star = P(82, 27, 82, Franchise.MarketSalary(82), 2);
            var role = P(66, 27, 66, Franchise.MarketSalary(66), 2);
            Assert.Greater(Franchise.Value(star), 2 * Franchise.Value(role));
            Assert.Greater(Franchise.Value(P(66, 21, 82, 30, 3)), Franchise.Value(P(66, 27, 66, 30, 3)), "young and rising");
            Assert.Greater(Franchise.Value(P(70, 27, 70, 40, 3)), Franchise.Value(P(70, 33, 70, 40, 3)), "age");
            Assert.Greater(Franchise.Value(P(70, 27, 70, 30, 3)), Franchise.Value(P(70, 27, 70, 250, 3)), "a bargain contract");
            Assert.Greater(Franchise.MarketSalary(85), Franchise.MarketSalary(70));
            Assert.AreEqual(Franchise.MinSalary, Franchise.MarketSalary(40));
        }

        [Test]
        public void Trades_AiWantsToWin_RosterAndCapRulesHold()
        {
            int other = (_f.you + 1) % Franchise.Teams;
            var mine = Franchise.Roster(_f, _f.you);
            var theirs = Franchise.Roster(_f, other);
            mine.Sort((a, b) => Franchise.Value(b).CompareTo(Franchise.Value(a)));
            theirs.Sort((a, b) => Franchise.Value(a).CompareTo(Franchise.Value(b)));

            // Your worst for their best: refused.
            var bad = Franchise.Evaluate(_f, other, new List<int> { mine[mine.Count - 1].id }, new List<int> { theirs[theirs.Count - 1].id });
            Assert.IsFalse(bad.Accepted);
            // Your best for their worst: accepted (if the cap allows it).
            var give = new List<int> { mine[0].id };
            var get = new List<int> { theirs[0].id };
            var good = Franchise.Evaluate(_f, other, give, get);
            Assert.IsTrue(good.Accepted, good.Reason);
            Assert.IsTrue(Franchise.Trade(_f, other, give, get));
            Assert.AreEqual(other, mine[0].team);
            Assert.AreEqual(_f.you, theirs[0].team);
            Assert.IsTrue(_f.teams[other].roster.Contains(mine[0].id));
            Assert.IsTrue(_f.moves.Exists(m => m.text.StartsWith("TRADE")));

            // Two for nothing would leave them over nine or you under seven.
            var lopsided = new List<int>();
            foreach (var p in Franchise.Roster(_f, _f.you)) if (lopsided.Count < 2) lopsided.Add(p.id);
            Assert.IsFalse(Franchise.Evaluate(_f, other, lopsided, new List<int>()).Accepted);

            // After the deadline: closed.
            _f.season.currentWeek = Franchise.TradeDeadlineWeek;
            StringAssert.Contains("deadline", Franchise.Evaluate(_f, other, give, get).Reason);
        }

        [Test]
        public void Aging_YoungGrowTowardPotential_OldDecline_OldestRetire()
        {
            var rng = new SeededRandom(5);
            FrPlayer P(int ovr, int age, int pot) => new FrPlayer { attrs = new AttributeSet(ovr, ovr, ovr, ovr, ovr, ovr, ovr, ovr), age = age, potential = pot };
            int young = 0, old = 0;
            for (int i = 0; i < 50; i++)
            {
                young += Franchise.Progression(P(60, 21, 80), rng);
                old += Franchise.Progression(P(75, 33, 75), rng);
                Assert.LessOrEqual(Franchise.Progression(P(70, 21, 70), rng), 0, "can't pass potential");
            }
            Assert.Greater(young, 100);
            Assert.Less(old, -100);

            var vet = Franchise.Roster(_f, 0)[0];
            vet.age = Franchise.RetireAge - 1;
            int id = vet.id;
            PlaySeason();
            Assert.IsNull(Franchise.Player(_f, id), "retired");
            Assert.IsFalse(_f.teams[0].roster.Contains(id));
            Assert.IsTrue(_f.moves.Exists(m => m.text.Contains("RETIRED")));
        }

        [Test]
        public void ThreeSeasons_LeagueStaysHealthy()
        {
            for (int year = 0; year < 3; year++)
            {
                PlaySeason();
                Franchise.DraftUntilYou(_f);
                if (Franchise.OnTheClock(_f) == _f.you) Franchise.Pick(_f, Franchise.Prospects(_f)[0].id);
                foreach (var p in Franchise.Expiring(_f)) Franchise.ReSign(_f, p.id);
                Franchise.FinishReSign(_f);
                foreach (var p in Franchise.FreeAgents(_f))
                    if (_f.teams[_f.you].roster.Count < Franchise.MinRoster && Franchise.CannotSign(_f, p) == null) Franchise.SignFreeAgent(_f, p.id);
                Franchise.FinishFreeAgency(_f, _c);
                while (Franchise.CannotStart(_f) != null && _f.teams[_f.you].roster.Count > Franchise.MaxRoster)
                    Franchise.Release(_f, _f.teams[_f.you].roster[_f.teams[_f.you].roster.Count - 1]);
                Assert.IsTrue(Franchise.StartSeason(_f, _c), Franchise.CannotStart(_f));
                for (int t = 0; t < Franchise.Teams; t++)
                {
                    Assert.GreaterOrEqual(_f.teams[t].roster.Count, Franchise.MinRoster);
                    Assert.LessOrEqual(_f.teams[t].roster.Count, Franchise.MaxRoster);
                }
            }
            Assert.AreEqual(4, _f.year);
            Assert.AreEqual(3 * Franchise.Teams, _f.history.Count);
            Assert.Less(_f.players.Count, 140, "the player list doesn't grow without limit");
            var totals = Franchise.Totals(_f, _f.you);
            Assert.AreEqual(3, totals.seasons);
            Assert.AreEqual(3 * Franchise.Weeks, totals.wins + totals.losses);
        }

        private static void Same(List<int> a, List<int> b)
        {
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i], b[i]);
        }

        [Test]
        public void Save_RoundTrips_AFranchiseMidOffSeason()
        {
            PlaySeason();
            Franchise.Scout(_f, Franchise.Prospects(_f)[1].id);
            var career = new CareerSaveData { franchise = _f };
            string json = SaveCodec.Encode(career);
            var back = SaveCodec.Decode(json, _c, out var status).franchise;
            Assert.IsTrue(back.active);
            Assert.AreEqual(_f.year, back.year);
            Assert.AreEqual(_f.phase, back.phase);
            Assert.AreEqual(_f.you, back.you);
            Assert.AreEqual(_f.players.Count, back.players.Count);
            Assert.AreEqual(_f.history.Count, back.history.Count);
            Same(_f.draftOrder, back.draftOrder);
            for (int i = 0; i < _f.players.Count; i++)
            {
                var a = _f.players[i];
                var b = back.players[i];
                Assert.AreEqual(a.Name, b.Name);
                Assert.AreEqual(a.Overall, b.Overall);
                Assert.AreEqual(a.salary, b.salary);
                Assert.AreEqual(a.prospect, b.prospect);
                Assert.AreEqual(a.scout, b.scout);
                Assert.AreEqual(a.team, b.team);
            }
            for (int t = 0; t < Franchise.Teams; t++) Same(_f.teams[t].roster, back.teams[t].roster);
            Assert.AreEqual(_f.season.games.Count, back.season.games.Count);
            Assert.IsFalse(SaveCodec.Decode(SaveCodec.Encode(new CareerSaveData()), _c, out _).franchise.active);
        }
    }

    /// <summary>All-Star Weekend: dunk judging, both contests, the weekend in Rise, the All-Star Game.</summary>
    public class AllStarTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void Moves_AreOriginal_WithCombosThatGrowWithDifficulty()
        {
            Assert.GreaterOrEqual(AllStar.Moves.Length, 10);
            var names = new List<string>();
            foreach (var m in AllStar.Moves)
            {
                names.Add(m.Name);
                Assert.GreaterOrEqual(m.Combo.Length, 2);
                Assert.GreaterOrEqual(m.Difficulty, 1);
                Assert.LessOrEqual(m.Difficulty, 5);
                Assert.Greater(AllStar.ComboSeconds(m), 0.5f * m.Combo.Length);
            }
            CollectionAssert.AllItemsAreUnique(names);
            Assert.Greater(AllStar.Moves[AllStar.Moves.Length - 1].Combo.Length, AllStar.Moves[0].Combo.Length);
        }

        [Test]
        public void Judges_RewardDifficultyExecutionTiming_PunishRepeatsAndMisses()
        {
            var easy = AllStar.Moves[0];
            var hard = AllStar.Moves[AllStar.Moves.Length - 1];
            int Avg(DunkMove m, bool made, float e, float t, int rep)
            {
                int sum = 0;
                for (uint s = 1; s <= 40; s++) sum += AllStar.Judge(m, made, e, t, rep, s).Total;
                return sum / 40;
            }
            Assert.Greater(Avg(hard, true, 1f, 1f, 0), Avg(easy, true, 1f, 1f, 0), "difficulty");
            Assert.Greater(Avg(hard, true, 1f, 1f, 0), Avg(hard, true, 0.2f, 1f, 0), "execution");
            Assert.Greater(Avg(hard, true, 1f, 1f, 0), Avg(hard, true, 1f, 0f, 0), "timing");
            Assert.Greater(Avg(hard, true, 1f, 1f, 0), Avg(hard, true, 1f, 1f, 1), "repeat");
            Assert.Greater(Avg(easy, true, 0.5f, 0.5f, 0), Avg(hard, false, 1f, 1f, 0), "a made dunk beats a miss");
            var perfect = AllStar.Judge(hard, true, 1f, 1f, 0, 3);
            Assert.GreaterOrEqual(perfect.Total, 46);
            foreach (uint s in new uint[] { 1, 2, 3, 99 })
                foreach (var j in AllStar.Judge(easy, false, 0f, 0f, 3, s).Judges) { Assert.GreaterOrEqual(j, 5); Assert.LessOrEqual(j, 10); }
            Assert.AreEqual(1f, AllStar.SlamTiming(0.5f), 1e-4f);
            Assert.AreEqual(0f, AllStar.SlamTiming(0f), 1e-4f);
        }

        [Test]
        public void DunkContest_TwoRounds_StarsSimulated_YouCanWin()
        {
            var s = AllStar.Start(_c, "dunk", "ROOK", 11, false);
            Assert.AreEqual(AllStar.Entrants, s.field.Count);
            Assert.IsTrue(s.field.TrueForAll(e => e.you || e.r1 > 0), "stars dunk first");
            Assert.Greater(AllStar.Target(s), 0);
            var best = AllStar.Moves[AllStar.Moves.Length - 1];
            var second = AllStar.Moves[AllStar.Moves.Length - 2];
            AllStar.RecordDunk(s, _c, best, true, 1f, 1f);
            Assert.AreEqual(1, s.round);
            AllStar.RecordDunk(s, _c, second, true, 1f, 1f);
            Assert.AreEqual(2, s.round);
            Assert.IsTrue(AllStar.InFinal(s, AllStar.You(s)), "two near-perfect dunks make the final");
            var rival = AllStar.Finalists(s).Find(e => !e.you);
            Assert.Greater(rival.r2, 0);
            AllStar.RecordDunk(s, _c, AllStar.Moves[AllStar.Moves.Length - 3], true, 1f, 1f);
            AllStar.RecordDunk(s, _c, AllStar.Moves[AllStar.Moves.Length - 4], true, 1f, 1f);
            Assert.AreEqual(3, s.round);
            Assert.IsFalse(s.Active);
            Assert.AreEqual(s.youWon ? "ROOK" : rival.name, s.championName);
        }

        [Test]
        public void DunkContest_TwoMisses_AreOutInRoundOne()
        {
            var s = AllStar.Start(_c, "dunk", "ROOK", 5, false);
            AllStar.RecordDunk(s, _c, AllStar.Moves[0], false, 0f, 0f);
            AllStar.RecordDunk(s, _c, AllStar.Moves[0], false, 0f, 0f);
            Assert.AreEqual(3, s.round, "the stars' final is simulated");
            Assert.IsFalse(s.youWon);
            Assert.IsFalse(string.IsNullOrEmpty(s.championName));
        }

        [Test]
        public void ThreeContest_TargetsAndFinal_UseTheShootout()
        {
            var s = AllStar.Start(_c, "three", "ROOK", 21, false);
            Assert.IsTrue(s.field.TrueForAll(e => e.you || e.r1 >= 0));
            int target = AllStar.Target(s);
            var req = AllStar.ThreeRound(s, DefaultContent.DefaultDifficultyId);
            Assert.AreEqual(GameMode.Practice, req.Mode);
            Assert.AreEqual((int)DrillKind.Shootout, req.Drill);
            StringAssert.Contains(AllStar.ThreeContext, req.ContextId);
            AllStar.RecordThrees(s, _c, target);
            Assert.AreEqual(2, s.round);
            Assert.IsTrue(AllStar.InFinal(s, AllStar.You(s)), "hitting the target makes the final");
            int final = AllStar.Target(s);
            AllStar.RecordThrees(s, _c, final);
            Assert.IsTrue(s.youWon);

            var lose = AllStar.Start(_c, "three", "ROOK", 21, false);
            AllStar.RecordThrees(lose, _c, 0);
            Assert.AreEqual(3, lose.round);
            Assert.IsFalse(lose.youWon);
        }

        [Test]
        public void Settle_PaysOnce_AndMarksTheRiseWeekend()
        {
            var career = new CareerSaveData();
            var s = AllStar.Start(_c, "three", "ROOK", 21, true);
            AllStar.RecordThrees(s, _c, AllStar.Target(s));
            AllStar.RecordThrees(s, _c, AllStar.Target(s));
            int sp = career.signalPoints;
            Assert.AreEqual(AllStar.ContestTitleBonus, AllStar.Settle(career, s));
            Assert.AreEqual(sp + AllStar.ContestTitleBonus, career.signalPoints);
            Assert.AreEqual(1, career.allStar.threeTitles);
            Assert.IsTrue(career.allStar.threeDone);
            Assert.AreEqual(0, AllStar.Settle(career, s), "paid once");
        }

        [Test]
        public void Weekend_OpensAtMidSeason_ResetsEachSeason()
        {
            var career = new CareerSaveData();
            RiseEngine.StartSeason(career.rise, _c);
            Assert.IsFalse(AllStar.WeekendOpen(career));
            career.rise.season.currentWeek = career.rise.season.weeks / 2;
            Assert.IsTrue(AllStar.WeekendOpen(career));
            var w = AllStar.Weekend(career);
            w.dunkDone = true;
            Assert.IsTrue(AllStar.Weekend(career).dunkDone);
            career.rise.season.seasonNumber++;
            Assert.IsFalse(AllStar.Weekend(career).dunkDone, "a new season, a new weekend");
        }

        [Test]
        public void AllStarGame_TwoOriginalSides_YouStartForSunrise()
        {
            var career = new CareerSaveData();
            var req = AllStar.GameRequest(_c, career, DefaultContent.DefaultDifficultyId);
            Assert.AreEqual(GameMode.AllStar, req.Mode);
            Assert.IsTrue(req.FullCourt);
            var setup = MatchSetup.FromRequest(req, _c);
            Assert.AreEqual(5, setup.RosterA.Count);
            Assert.AreEqual(5, setup.RosterB.Count);
            Assert.AreEqual(req.HumanPlayer.id, setup.RosterA[0].id);
            var ids = new HashSet<string>();
            foreach (var p in setup.RosterA) Assert.IsTrue(ids.Add(p.id));
            foreach (var p in setup.RosterB) Assert.IsTrue(ids.Add(p.id), "nobody plays for both sides");
            Assert.IsNotNull(_c.Court(req.CourtId));
            Assert.AreEqual("Sunrise", _c.Team(AllStar.GameTeamA).nickname);
        }

        [Test]
        public void Save_RoundTrips_AContestInProgress()
        {
            var career = new CareerSaveData();
            career.allStar.contest = AllStar.Start(_c, "dunk", "ROOK", 8, true);
            AllStar.RecordDunk(career.allStar.contest, _c, AllStar.Moves[3], true, 0.8f, 0.6f);
            career.allStar.dunkTitles = 2;
            var back = SaveCodec.Decode(SaveCodec.Encode(career), _c, out _).allStar;
            Assert.AreEqual(2, back.dunkTitles);
            Assert.AreEqual("dunk", back.contest.kind);
            Assert.AreEqual(1, back.contest.yourDunks);
            Assert.AreEqual(career.allStar.contest.yourRoundTotal, back.contest.yourRoundTotal);
            Assert.AreEqual(AllStar.Entrants, back.contest.field.Count);
            Assert.AreEqual(career.allStar.contest.field[1].r1, back.contest.field[1].r1);
            Assert.IsTrue(back.contest.rise);
        }
    }

    /// <summary>Court Builder and the soundtrack.</summary>
    public class CourtAndMusicTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void CourtBuilder_BuiltCourtsJoinTheCatalog_AndLeaveWhenDeleted()
        {
            var courts = CourtBuilder.Ensure(null);
            Assert.AreEqual(CourtBuilder.Slots, courts.Count);
            CourtBuilder.Apply(_c, courts);
            Assert.IsNull(_c.Court(CourtBuilder.Id(0)), "nothing built yet");
            courts[1] = CourtBuilder.Randomize(9, "Backyard");
            courts[1].built = true;
            CourtBuilder.Apply(_c, courts);
            var def = _c.Court(CourtBuilder.Id(1));
            Assert.IsNotNull(def);
            Assert.AreEqual(CourtCircuit.Custom, def.circuit);
            Assert.AreEqual("Backyard", def.displayName);
            Assert.IsTrue(CustomTeams.HomeCourts(_c).Contains(def), "can be your home court");
            courts[1].name = "Back Lot";
            CourtBuilder.Apply(_c, courts);
            Assert.AreEqual("Back Lot", _c.Court(CourtBuilder.Id(1)).displayName);
            Assert.AreEqual(1, _c.Courts.FindAll(x => x.circuit == CourtCircuit.Custom).Count);
            courts[1].built = false;
            CourtBuilder.Apply(_c, courts);
            Assert.IsNull(_c.Court(CourtBuilder.Id(1)));
            Assert.AreEqual(1, CourtBuilder.SlotOf("court.custom.1"));
            Assert.AreEqual(-1, CourtBuilder.SlotOf("court.pier_nine"));
        }

        [Test]
        public void CourtBuilder_Clamps_AndCrowdOnlyInStands()
        {
            var d = new CustomCourtData { floorStyle = 99, sky = -4, crowd = 9, stands = 7, logoMotif = 40, logoShape = -2, name = "  <<Way too long a court name>>  ", floor = 0x1FFFF };
            CourtBuilder.Clamp(d);
            Assert.AreEqual(CourtBuilder.FloorNames.Length - 1, d.floorStyle);
            Assert.AreEqual(0, d.sky);
            Assert.AreEqual(4, d.crowd);
            Assert.AreEqual(CourtBuilder.StandNames.Length - 1, d.stands);
            Assert.AreEqual(10, d.logoMotif);
            Assert.AreEqual(0, d.logoShape);
            Assert.LessOrEqual(d.name.Length, CourtBuilder.MaxName);
            Assert.AreEqual(0x7FFF, d.floor & 0x7FFF);
            Assert.AreEqual(0f, CourtBuilder.Build(d, 0).crowdDensity, "a brick wall has no crowd");
            d.stands = 0;
            Assert.AreEqual(1f, CourtBuilder.Build(d, 0).crowdDensity);
        }

        [Test]
        public void CourtArt_EveryFloorStandAndLogo_Renders_AndDiffers()
        {
            var g = CourtGeometry.Default;
            var seen = new HashSet<uint>();
            for (int f = 0; f < CourtBuilder.FloorNames.Length; f++)
                for (int st = 0; st < CourtBuilder.StandNames.Length; st++)
                {
                    var d = new CustomCourtData { built = true, floorStyle = f, stands = st, logoMotif = (f + st) % 11, logoShape = st, floor = 0x2D6B, paint = 0x7C00 };
                    var canvas = CourtGenerator.Generate(CourtBuilder.Build(d, 0), g, 3);
                    Assert.AreEqual(CourtGenerator.TextureWidth(g), canvas.Width);
                    uint h = 17;
                    foreach (var p in canvas.Pixels) h = h * 31 + (uint)p.GetHashCode();
                    Assert.IsTrue(seen.Add(h), "floor " + f + " stands " + st + " looks like another combination");
                }
            // The logo changes the floor near the half-court line; no logo leaves it plain.
            var plain = new CustomCourtData { built = true, floorStyle = 0, logoMotif = -1 };
            var withLogo = plain.Clone();
            withLogo.logoMotif = 3;
            var a = CourtGenerator.Generate(CourtBuilder.Build(plain, 0), g, 3);
            var b = CourtGenerator.Generate(CourtBuilder.Build(withLogo, 0), g, 3);
            CourtGenerator.CourtToPixel(g, new Vec2(0f, g.depth - 1.05f), out float cx, out float cy);
            int diff = 0;
            for (int y = (int)cy - 16; y < (int)cy + 16; y++)
                for (int x = (int)cx - 16; x < (int)cx + 16; x++) if (a.Get(x, y) != b.Get(x, y)) diff++;
            Assert.Greater(diff, 200);
            // Full Court draws built courts too.
            Assert.Greater(CourtGenerator.GenerateFullCourt(CourtBuilder.Build(withLogo, 0), FullCourt.HalfGeometry(), 3, null, null, false).OpaqueCount(), 0);
        }

        [Test]
        public void Courts_SaveRoundTrip_AndHomeCourtSurvivesLoading()
        {
            var career = new CareerSaveData();
            career.courts[0] = CourtBuilder.Randomize(4, "Rooftop Two");
            career.courts[0].built = true;
            career.customTeam.created = true;
            career.customTeam.homeCourtId = CourtBuilder.Id(0);
            string json = SaveCodec.Encode(career);
            var fresh = DefaultContent.Create();
            var back = SaveCodec.Decode(json, fresh, out _);
            Assert.IsTrue(back.courts[0].built);
            Assert.AreEqual("Rooftop Two", back.courts[0].name);
            Assert.AreEqual(career.courts[0].floor, back.courts[0].floor);
            Assert.AreEqual(career.courts[0].logoMotif, back.courts[0].logoMotif);
            Assert.AreEqual(CourtBuilder.Id(0), back.customTeam.homeCourtId, "a built court can stay your home court");
            Assert.IsNotNull(fresh.Court(CourtBuilder.Id(0)));
        }

        [Test]
        public void Soundtrack_SixOriginalSongs_InKey_Looping_AndNotSilent()
        {
            Assert.AreEqual(Soundtrack.ClassicCount + 6 + StreetBeats.Beats.Length, Soundtrack.Count);
            var titles = new List<string>();
            for (int t = 0; t < Soundtrack.Count; t++) titles.Add(Soundtrack.Title(t));
            CollectionAssert.AllItemsAreUnique(titles);
            foreach (var song in Soundtrack.Songs)
            {
                var melody = Soundtrack.Melody(song);
                Assert.AreEqual(64, melody.Length);
                int rests = 0;
                var scale = new HashSet<int>();
                for (int d = 0; d < 7; d++) scale.Add(((Soundtrack.Note(song, d) - song.Root) % 12 + 12) % 12);
                foreach (int m in melody)
                {
                    if (m == -99) { rests++; continue; }
                    int pc = ((Soundtrack.Note(song, m) - song.Root) % 12 + 12) % 12;
                    Assert.IsTrue(scale.Contains(pc), song.Title + ": note out of key");
                }
                Assert.Less(rests, 16);
                Assert.AreEqual(7, melody[63], song.Title + " ends on the tonic");
            }
            var samples = Soundtrack.Render(Soundtrack.ClassicCount);
            float peak = 0f;
            double energy = 0;
            foreach (var x in samples) { Assert.IsFalse(float.IsNaN(x)); peak = System.Math.Max(peak, System.Math.Abs(x)); energy += x * x; }
            Assert.AreEqual(0.8f, peak, 0.001f);
            Assert.Greater(energy / samples.Length, 0.005);
            Assert.AreEqual((int)System.Math.Round(Soundtrack.Seconds(Soundtrack.Songs[0]) * AudioSynth.SampleRate), samples.Length, "exact loop length");
            Assert.AreEqual(AudioSynth.MusicLoop(1).Length, Soundtrack.Render(1).Length, "classic tracks are unchanged");
        }

        [Test]
        public void Soundtrack_IsDeterministic_AndSongsDiffer()
        {
            var a = Soundtrack.Render(Soundtrack.ClassicCount + 2);
            var b = Soundtrack.Render(Soundtrack.ClassicCount + 2);
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i += 997) Assert.AreEqual(a[i], b[i]);
            var m0 = Soundtrack.Melody(Soundtrack.Songs[0]);
            var m1 = Soundtrack.Melody(Soundtrack.Songs[1]);
            int same = 0;
            for (int i = 0; i < 64; i++) if (m0[i] == m1[i]) same++;
            Assert.Less(same, 40);
        }

        [Test]
        public void MusicSettings_SaveAndClamp()
        {
            var career = new CareerSaveData();
            career.settings.musicMenu = 5;
            career.settings.musicGame = -2;
            var back = SaveCodec.Decode(SaveCodec.Encode(career), _c, out _);
            Assert.AreEqual(5, back.settings.musicMenu);
            Assert.AreEqual(-2, back.settings.musicGame);
            career.settings.musicMenu = 99;
            Assert.AreEqual(Soundtrack.Count - 1, SaveCodec.Decode(SaveCodec.Encode(career), _c, out _).settings.musicMenu);
        }
    }
}
