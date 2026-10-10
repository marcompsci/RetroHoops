using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Franchise mode front office, drawn over the main menu: HOME (next game, sim, off-season steps),
    /// ROSTER (depth chart, contracts, release), TRADE, LEAGUE (standings, results, playoffs), MARKET
    /// (re-sign and free agents), DRAFT (lottery, scouting, picks) and HISTORY. Every rule lives in
    /// <see cref="Franchise"/>; this screen calls it and saves.
    /// </summary>
    public sealed partial class FranchiseScreen : MonoBehaviour
    {
        private enum Tab { Home = 0, Roster = 1, Trade = 2, League = 3, Market = 4, Draft = 5, History = 6 }
        private static readonly string[] TabNames = { "HOME", "ROSTER", "TRADE", "LEAGUE", "MARKET", "DRAFT", "HISTORY" };

        private Tab _tab;
        private RectTransform _content;
        private TextMeshProUGUI _header;
        private Image[] _tabImages;
        private GameObject _tabsRow;
        private int _pickTeam;
        private int _tradeTeam = -1;
        private readonly List<int> _give = new List<int>();
        private readonly List<int> _get = new List<int>();

        private static FranchiseSaveData F => App.Career.franchise;
        private static ContentCatalog C => App.Catalog;

        /// <summary>Opens the front office (or the new-franchise picker) on top of the current screen.</summary>
        public static FranchiseScreen Open()
        {
            var existing = FindAnyObjectByType<FranchiseScreen>();
            if (existing != null) return existing;
            var go = new GameObject("FranchiseScreen");
            var screen = go.AddComponent<FranchiseScreen>();
            screen.Build();
            return screen;
        }

        private void Build()
        {
            var canvas = UiKit.CreateScreenCanvas("FranchiseCanvas", 35);
            canvas.transform.SetParent(transform, false);
            var bg = UiKit.Panel(canvas.transform, Theme.Ink, name: "Backdrop");
            UiKit.Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            var safe = UiKit.SafeArea(canvas.transform);

            var top = UiKit.Row(safe, 16f, "Top");
            UiKit.Band(top, 0.93f, 0.995f, 24f);
            top.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var back = UiKit.Button(top, "< BACK", Close, ButtonStyle.Ghost, 90f, 32f);
            UiKit.Size(back, 90f, 200f);
            _header = UiKit.Label(top, "", 30f, Theme.Cream, TextAlignmentOptions.Right, true);
            UiKit.Size(_header, 90f).flexibleWidth = 1f;

            var tabs = UiKit.Row(safe, 8f, "Tabs");
            UiKit.Band(tabs, 0.875f, 0.925f, 16f);
            _tabsRow = tabs.gameObject;
            _tabImages = new Image[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                var t = (Tab)i;
                var b = UiKit.Button(tabs, TabNames[i], () => Show(t), ButtonStyle.Secondary, 80f, 20f);
                _tabImages[i] = b.GetComponent<Image>();
            }

            var holder = UiKit.NewRect("TabBody", safe);
            UiKit.Band(holder, 0f, 0.865f, 0f);
            _content = UiKit.ScrollColumn(holder, 14f, new RectOffset(40, 40, 12, 60));
            Franchise.Register(C, F);
            Show(Tab.Home);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame)) Close();
#endif
        }

        private void Close() => Destroy(gameObject);

        private void Show(Tab tab)
        {
            _tab = tab;
            Refresh();
        }

        private void Refresh()
        {
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
            var f = F;
            bool active = f != null && f.active;
            _tabsRow.SetActive(active);
            for (int i = 0; i < _tabImages.Length; i++)
                _tabImages[i].color = i == (int)_tab ? Color.white : new Color(0.55f, 0.55f, 0.6f, 1f);
            if (!active)
            {
                _header.text = Loc.T("FRANCHISE");
                BuildStart();
                return;
            }
            if (_create != null)
            {
                // CREATE A PLAYER takes the whole screen (no tabs) until you create or cancel.
                _tabsRow.SetActive(false);
                _header.text = Loc.T(_editId >= 0 ? "EDIT PLAYER" : "CREATE A PLAYER");
                BuildCreate();
                return;
            }
            var you = Team(f.you);
            var rec = Record(f.you);
            _header.text = (you?.abbreviation ?? "?") + "  ·  " + Loc.T("YEAR") + " " + f.year + "  ·  " + rec + "\n<size=24><color=#8D99AE>"
                           + Loc.T(Franchise.PhaseName(f.phase)) + "  ·  " + Loc.T("PAYROLL") + " " + Franchise.Money(Franchise.Payroll(f, f.you))
                           + " / " + Franchise.Money(Franchise.Cap) + "</color></size>";
            switch (_tab)
            {
                case Tab.Roster: BuildRoster(); break;
                case Tab.Trade: BuildTrade(); break;
                case Tab.League: BuildLeague(); break;
                case Tab.Market: BuildMarket(); break;
                case Tab.Draft: BuildDraft(); break;
                case Tab.History: BuildHistory(); break;
                default: BuildHome(); break;
            }
        }

        private void Save()
        {
            App.SaveCareer();
            Franchise.Register(C, F);
            Refresh();
        }

        // ------------------------------------------------------------------ helpers

        private static TeamDef Team(int index) => C.Team(Franchise.TeamId(index));

        private static string Abbr(string teamId) => C.Team(teamId)?.abbreviation ?? "?";

        private static string Record(int team)
        {
            var row = SeasonEngine.Standings(F.season).Find(r => r.TeamId == Franchise.TeamId(team));
            return row == null ? "0-0" : row.Wins + "-" + row.Losses;
        }

        private TextMeshProUGUI Heading(string text) =>
            Line(text, Theme.Gold, 34f, 56f, TextAlignmentOptions.Left, true);

        private TextMeshProUGUI Line(string text, Color color, float size = 28f, float height = 44f,
                                     TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false)
        {
            var l = UiKit.Label(_content, text, size, color, align, bold);
            UiKit.Size(l, height);
            l.textWrappingMode = TextWrappingModes.Normal;
            return l;
        }

        private void Big(string text, System.Action onClick, ButtonStyle style = ButtonStyle.Primary) =>
            UiKit.Button(_content, text, () => { onClick(); }, style, 110f, 40f);

        /// <summary>A row: text on the left, small buttons on the right.</summary>
        private RectTransform Row(string text, Color color, float height, params (string label, ButtonStyle style, System.Action onClick)[] buttons)
        {
            var row = UiKit.Row(_content, 10f, "Row");
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            UiKit.Size(row, height);
            var label = UiKit.Label(row, text, 26f, color, TextAlignmentOptions.Left);
            label.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Size(label).flexibleWidth = 1f;
            foreach (var b in buttons)
            {
                var action = b.onClick;
                var button = UiKit.Button(row, b.label, () => action?.Invoke(), b.style, height - 10f, 24f);
                UiKit.Size(button, height - 10f, 150f);
                if (action == null) button.interactable = false;
            }
            return row;
        }

        private static string Arch(FrPlayer p)
        {
            var def = C.Archetypes.Find(a => (int)a.archetype == p.archetype);
            return def != null ? def.displayName.ToUpperInvariant() : "";
        }

        private static string Card(FrPlayer p, bool contract = true) =>
            "<b>#" + p.number + " " + p.Name.ToUpperInvariant() + "</b>  <color=#FFD166>" + p.Overall + "</color>\n<size=22><color=#8D99AE>"
            + Arch(p) + " · " + Loc.T("AGE") + " " + p.age + (p.age <= 25 ? " · POT " + Franchise.PotentialGrade(p) : "")
            + (contract ? " · " + Franchise.Money(p.salary) + " × " + p.years : "") + (p.gp > 0 ? " · " + p.Ppg.ToString("0.0") + " PPG" : "") + "</color></size>";

        private static void Toast(string title, string body) => UiControls.Dialog(title, body, ("OK", ButtonStyle.Primary, null));

        // ------------------------------------------------------------------ start

        private void BuildStart()
        {
            var league = C.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            if (league.Count == 0) return;
            _pickTeam = Mathf.Clamp(_pickTeam, 0, league.Count - 1);
            var team = league[_pickTeam];
            Line("Run a Caller League club. Set the rotation, make trades, sign free agents, draft rookies and win titles, season after season. " +
                 "Your games are Full Court 5 on 5: play them, or simulate.", Theme.Cream, 30f, 200f);
            var logo = UiKit.Picture(_content, TextureFactory.TeamLogo(team), "Logo");
            UiKit.Size(logo, 260f);
            logo.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            Line(team.FullName.ToUpperInvariant(), Theme.Gold, 48f, 80f, TextAlignmentOptions.Center, true);
            Line("\"" + team.motto + "\"", Theme.Muted, 28f, 50f, TextAlignmentOptions.Center);
            Big("CHANGE CLUB", () => { _pickTeam = (_pickTeam + 1) % league.Count; Refresh(); }, ButtonStyle.Secondary);
            Big("START FRANCHISE", () =>
            {
                App.Career.franchise = Franchise.Create(C, team.id, (uint)System.Environment.TickCount | 1u);
                _tab = Tab.Home;
                Save();
            });
        }

        // ------------------------------------------------------------------ home

        private void BuildHome()
        {
            var f = F;
            // Phase 33: a GM on the phone.
            if (f.offer != null && FranchiseDepth.StillValid(f, f.offer))
            {
                Heading("TRADE OFFER");
                Line(f.offer.pitch, Theme.Cyan, 28f, 100f);
                Row("", Theme.Cream, 70f,
                    ("ACCEPT", ButtonStyle.Primary, () => { bool ok = FranchiseDepth.Accept(f); Save(); if (!ok) Toast("TRADE OFFER", "That deal isn't possible any more."); }),
                    ("DECLINE", ButtonStyle.Ghost, () => { FranchiseDepth.Decline(f); Save(); }));
            }
            else if (f.offer != null) f.offer = null;
            switch (f.phase)
            {
                case FranchisePhase.Regular:
                case FranchisePhase.Playoffs:
                    BuildGameDay(f);
                    break;
                case FranchisePhase.Draft:
                    Heading("THE DRAFT");
                    foreach (var l in Franchise.LastReport(f)) Line(l, Theme.Cream);
                    if (f.devReport.Count > 0)
                    {
                        Heading("PLAYER DEVELOPMENT  ·  " + FranchiseDepth.FocusNames[f.focus]);
                        foreach (var l in f.devReport) Line(l, Theme.Cream, 26f, 40f);
                    }
                    foreach (var l in f.lottery) Line(l, Theme.Cyan);
                    Big("GO TO THE DRAFT", () => Show(Tab.Draft));
                    break;
                case FranchisePhase.ReSign:
                    Heading("RE-SIGN YOUR PLAYERS");
                    Line("Players whose contracts are up. Re-sign them (you can go over the cap for your own players) or let them walk.", Theme.Cream, 28f, 100f);
                    BuildExpiring(f);
                    Big("DONE: OPEN FREE AGENCY", () => { Franchise.FinishReSign(f); Save(); });
                    break;
                case FranchisePhase.FreeAgency:
                    Heading("FREE AGENCY");
                    Line("Sign players in MARKET. When you finish, the other clubs sign who's left.", Theme.Cream, 28f, 80f);
                    Big("FREE AGENTS", () => Show(Tab.Market), ButtonStyle.Secondary);
                    Big("FINISH FREE AGENCY", () => { Franchise.FinishFreeAgency(f, C); Save(); });
                    break;
                default:
                    TrainingRow(f);
                    Heading("PRESEASON");
                    string why = Franchise.CannotStart(f);
                    Line(why ?? "Roster set: " + f.teams[f.you].roster.Count + " players. Your top five start; you control the first one.", why == null ? Theme.Cream : Theme.Pink, 28f, 90f);
                    if (why == null) Big("START SEASON " + f.year, () => { Franchise.StartSeason(f, C); Save(); });
                    else Big("ROSTER", () => Show(Tab.Roster), ButtonStyle.Secondary);
                    break;
            }
            var recent = f.moves.FindAll(m => m.year == f.year);
            if (recent.Count > 0)
            {
                Heading("MOVES THIS YEAR");
                for (int i = recent.Count - 1; i >= 0 && i >= recent.Count - 5; i--) Line(recent[i].text, Theme.Muted, 24f, 40f);
            }
        }

        /// <summary>Phase 33: what your team works on before next season (shown in the preseason and on the roster).</summary>
        private void TrainingRow(FranchiseSaveData f)
        {
            Heading("TRAINING FOCUS");
            Line("Players 27 and under who haven't reached their potential grow faster in what you train. It pays off next off-season.", Theme.Muted, 24f, 70f);
            UiControls.ChoiceRow(_content, "FOCUS", FranchiseDepth.FocusNames, f.focus, i => { f.focus = i; Save(); });
        }

        private void BuildGameDay(FranchiseSaveData f)
        {
            var g = Franchise.NextGame(f);
            string you = Franchise.TeamId(f.you);
            if (g != null)
            {
                bool home = g.homeId == you;
                string opp = home ? g.awayId : g.homeId;
                string what = g.round > 0 ? Franchise.RoundName(g.round) : Loc.T("WEEK") + " " + (g.week + 1) + " / " + Franchise.Weeks;
                Heading(what);
                var row = UiKit.Row(_content, 30f, "Matchup");
                UiKit.Size(row, 220f);
                foreach (var id in new[] { you, opp })
                {
                    var pic = UiKit.Picture(row, TextureFactory.TeamLogo(C.Team(id)), "Logo");
                    pic.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                }
                Line((home ? Loc.T("HOME vs") : Loc.T("AWAY at")) + " " + C.Team(opp)?.FullName.ToUpperInvariant() + "  (" + Record(Franchise.IndexOf(opp)) + ")",
                     Theme.Cream, 32f, 60f, TextAlignmentOptions.Center, true);
                // Phase 35: play it, or coach it from the sideline.
                UiControls.ChoiceRow(_content, "GAME DAY", new[] { "PLAY", "COACH" }, f.coach ? 1 : 0, i => { f.coach = i == 1; Save(); });
                Big(f.coach ? "COACH GAME" : "PLAY GAME", () =>
                {
                    var req = Franchise.NextMatch(f, C, App.Career.settings.difficultyId);
                    if (req == null) return;
                    App.PendingMatch = req;
                    SceneFlow.GoTo(SceneNames.Game);
                });
                Big("SIM GAME", () => { Franchise.SimNext(f, C); Save(); }, ButtonStyle.Secondary);
                Big(f.phase == FranchisePhase.Regular ? "SIM TO PLAYOFFS" : "SIM PLAYOFFS", () => { Franchise.SimToEnd(f, C); Save(); }, ButtonStyle.Ghost);
                var mine = Franchise.Roster(f, f.you);
                if (f.coach) Line("COACH MODE: your players play; you call plays, the offense, the defense and subs.", Theme.Muted, 24f, 40f, TextAlignmentOptions.Center);
                else if (mine.Count > 0) Line(Loc.T("You control") + " " + mine[0].Name + ". " + Loc.T("Change it in ROSTER."), Theme.Muted, 24f, 40f, TextAlignmentOptions.Center);
            }
            else if (f.phase == FranchisePhase.Playoffs)
            {
                Heading("PLAYOFFS");
                Line(f.season.games.Exists(x => x.round > 0 && x.Involves(you)) ? "You're out. Watch the rest play out." : "You missed the playoffs this year.", Theme.Cream, 30f, 60f);
                Big("SIM PLAYOFFS", () => { Franchise.SimToEnd(f, C); Save(); });
            }
            Heading("LAST RESULTS");
            var played = f.season.games.FindAll(x => x.played && x.Involves(you));
            if (played.Count == 0) Line("No games yet.", Theme.Muted);
            for (int i = played.Count - 1; i >= 0 && i >= played.Count - 4; i--)
            {
                var x = played[i];
                bool won = x.WinnerId == you;
                Line((won ? "<color=#4CC9F0>W</color> " : "<color=#F72585>L</color> ") + Abbr(x.awayId) + " " + x.awayScore + " @ " + Abbr(x.homeId) + " " + x.homeScore
                     + (x.round > 0 ? "  · " + Franchise.RoundName(x.round) : ""), Theme.Cream, 28f);
            }
            // Phase 35: where your team shoots from this season (games you played or coached).
            if (ShotCharts.Attempts(f.teamChart) > 0)
                ShotChartView.Build(_content, "TEAM SHOT CHART  ·  YEAR " + f.year, f.teamChart, 300f);
        }

        // ------------------------------------------------------------------ roster

        private void BuildRoster()
        {
            var f = F;
            var roster = Franchise.Roster(f, f.you);
            Line(Loc.T("CAP ROOM") + " " + Franchise.Money(Franchise.CapRoom(f, f.you)) + "  ·  " + roster.Count + "/" + Franchise.MaxRoster + " " + Loc.T("players")
                 + (f.teams[f.you].deadMoney > 0 ? "  ·  " + Loc.T("dead money") + " " + Franchise.Money(f.teams[f.you].deadMoney) : ""), Theme.Muted, 26f, 44f);
            CreateEntry(f);
            for (int i = 0; i < roster.Count; i++)
            {
                if (i == 0) Heading("STARTERS (YOU CONTROL #1)");
                if (i == 5) Heading("BENCH");
                var p = roster[i];
                int id = p.id;
                bool canRelease = f.phase != FranchisePhase.Playoffs && !(f.phase == FranchisePhase.Regular && roster.Count <= Franchise.MinRoster);
                Row(Card(p), i < 5 ? Theme.Cream : Theme.Muted, 110f,
                    ("UP", ButtonStyle.Ghost, i > 0 ? (System.Action)(() => { Franchise.MoveInRotation(f, id, -1); Save(); }) : null),
                    ("DOWN", ButtonStyle.Ghost, i < roster.Count - 1 ? (System.Action)(() => { Franchise.MoveInRotation(f, id, 1); Save(); }) : null),
                    ("CUT", ButtonStyle.Ghost, canRelease ? (System.Action)(() => ConfirmRelease(p)) : null));
            }
            CreatedList(f);
            TrainingRow(f);
        }

        private void ConfirmRelease(FrPlayer p)
        {
            UiControls.Dialog("RELEASE " + p.Name.ToUpperInvariant() + "?",
                "Half of this season's salary (" + Franchise.Money(p.salary / 2) + ") stays on your cap. He becomes a free agent.",
                ("RELEASE", ButtonStyle.Primary, () => { Franchise.Release(F, p.id); Save(); }),
                ("KEEP", ButtonStyle.Ghost, null));
        }

        // ------------------------------------------------------------------ trade

        private void BuildTrade()
        {
            var f = F;
            if (!Franchise.TradesOpen(f))
            {
                Line(f.phase == FranchisePhase.Playoffs ? "No trades during the playoffs." : "The trade deadline has passed (week " + Franchise.TradeDeadlineWeek + "). Trades open again at the draft.",
                     Theme.Cream, 30f, 100f);
                return;
            }
            if (_tradeTeam < 0 || _tradeTeam == f.you || _tradeTeam >= f.teams.Count) _tradeTeam = (f.you + 1) % f.teams.Count;
            _give.RemoveAll(id => Franchise.Player(f, id)?.team != f.you);
            _get.RemoveAll(id => Franchise.Player(f, id)?.team != _tradeTeam);

            var other = Team(_tradeTeam);
            Row(Loc.T("TRADE WITH") + "  <b>" + other?.FullName.ToUpperInvariant() + "</b>  (" + Record(_tradeTeam) + ")\n<size=22><color=#8D99AE>"
                + Loc.T("PAYROLL") + " " + Franchise.Money(Franchise.Payroll(f, _tradeTeam)) + "</color></size>", Theme.Cream, 100f,
                ("NEXT TEAM", ButtonStyle.Secondary, () =>
                {
                    do _tradeTeam = (_tradeTeam + 1) % f.teams.Count; while (_tradeTeam == f.you);
                    _get.Clear();
                    Refresh();
                }));

            var verdict = Franchise.Evaluate(f, _tradeTeam, _give, _get);
            float total = Mathf.Max(1f, verdict.TheyGet + verdict.TheyGive);
            Line((verdict.Accepted ? "<color=#4CC9F0>" : "<color=#F72585>") + Loc.T(verdict.Reason) + "</color>   <size=22><color=#8D99AE>"
                 + Loc.T("their view") + ": " + Mathf.RoundToInt(100f * verdict.TheyGet / total) + "% / " + Mathf.RoundToInt(100f * verdict.TheyGive / total) + "%</color></size>",
                 Theme.Cream, 28f, 60f, TextAlignmentOptions.Center, true);
            var propose = UiKit.Button(_content, "PROPOSE TRADE", () =>
            {
                if (Franchise.Trade(f, _tradeTeam, new List<int>(_give), new List<int>(_get)))
                {
                    _give.Clear();
                    _get.Clear();
                    Audio.AudioManager.Play(SfxId.Coin, 0.8f);
                    Save();
                    Toast("TRADE DONE", "Check your new depth chart in ROSTER.");
                }
            }, ButtonStyle.Primary, 110f, 40f);
            propose.interactable = verdict.Accepted;

            Heading("YOU SEND");
            foreach (var p in Franchise.Roster(f, f.you)) Pickable(p, _give);
            Heading("YOU GET");
            foreach (var p in Franchise.Roster(f, _tradeTeam)) Pickable(p, _get);
        }

        private void Pickable(FrPlayer p, List<int> side)
        {
            bool on = side.Contains(p.id);
            int id = p.id;
            Row(Card(p), on ? Theme.Gold : Theme.Cream, 100f,
                (on ? "IN" : "ADD", on ? ButtonStyle.Primary : ButtonStyle.Ghost, () =>
                {
                    if (!side.Remove(id) && side.Count < Franchise.MaxTradePlayers) side.Add(id);
                    Refresh();
                }));
        }

        // ------------------------------------------------------------------ league

        private void BuildLeague()
        {
            var f = F;
            Heading("STANDINGS");
            var table = SeasonEngine.Standings(f.season);
            for (int i = 0; i < table.Count; i++)
            {
                var r = table[i];
                bool mine = r.TeamId == Franchise.TeamId(f.you);
                Line((i + 1) + ".  " + (C.Team(r.TeamId)?.FullName ?? "?") + "   " + r.Wins + "-" + r.Losses + "   " + (r.Differential >= 0 ? "+" : "") + r.Differential
                     + "   " + r.StreakText + (i == SeasonEngine.PlayoffTeams - 1 ? "\n<size=18><color=#8D99AE>— " + Loc.T("PLAYOFF LINE") + " —</color></size>" : ""),
                     mine ? Theme.Gold : Theme.Cream, 28f, i == SeasonEngine.PlayoffTeams - 1 ? 70f : 44f);
            }
            // Phase 33: the playoff picture as a bracket (projected while the season is on).
            BracketView.Draw(_content, PlayoffBracket.From(f.season), Abbr, Franchise.TeamId(f.you), Loc.T("PLAYOFFS"));
            int week = Mathf.Max(0, f.season.currentWeek - 1);
            var last = f.season.games.FindAll(g => g.round == 0 && g.week == week && g.played);
            if (last.Count > 0)
            {
                Heading(Loc.T("WEEK") + " " + (week + 1) + " " + Loc.T("RESULTS"));
                foreach (var g in last) Line(Abbr(g.awayId) + " " + g.awayScore + " @ " + Abbr(g.homeId) + " " + g.homeScore, Theme.Cream);
            }
            Heading("TOP SCORERS");
            var scorers = f.players.FindAll(p => p.team >= 0 && p.gp > 0);
            scorers.Sort((a, b) => b.Ppg.CompareTo(a.Ppg));
            for (int i = 0; i < Mathf.Min(5, scorers.Count); i++)
                Line(scorers[i].Name + " (" + Abbr(Franchise.TeamId(scorers[i].team)) + ")  " + scorers[i].Ppg.ToString("0.0") + " PPG", Theme.Cream);
        }

        // ------------------------------------------------------------------ market

        private void BuildExpiring(FranchiseSaveData f)
        {
            var expiring = Franchise.Expiring(f);
            if (expiring.Count == 0) Line("Nobody's contract is up.", Theme.Muted);
            foreach (var p in expiring)
            {
                int id = p.id;
                Row(Card(p, false) + "\n<size=22><color=#4CC9F0>" + Loc.T("asks") + " " + Franchise.Money(Franchise.Demand(p)) + " × " + Franchise.DemandYears(p) + "</color></size>",
                    Theme.Cream, 130f, ("RE-SIGN", ButtonStyle.Primary, () => { Franchise.ReSign(f, id); Save(); }));
            }
        }

        private void BuildMarket()
        {
            var f = F;
            Line(Loc.T("CAP ROOM") + " " + Franchise.Money(Franchise.CapRoom(f, f.you)) + "  ·  " + Loc.T("minimum deals") + " (" + Franchise.Money(Franchise.MinSalary)
                 + ") " + Loc.T("always fit"), Theme.Muted, 26f, 44f);
            if (f.phase == FranchisePhase.ReSign)
            {
                Heading("YOUR EXPIRING CONTRACTS");
                BuildExpiring(f);
                return;
            }
            Heading("FREE AGENTS");
            var fas = Franchise.FreeAgents(f);
            if (fas.Count == 0) Line("No free agents right now.", Theme.Muted);
            foreach (var p in fas)
            {
                int id = p.id;
                string why = Franchise.CannotSign(f, p);
                Row(Card(p, false) + "\n<size=22><color=#4CC9F0>" + Loc.T("asks") + " " + Franchise.Money(Franchise.Demand(p)) + " × " + Franchise.DemandYears(p) + "</color></size>",
                    Theme.Cream, 130f, ("SIGN", ButtonStyle.Primary, () =>
                    {
                        if (why != null) { Toast("CAN'T SIGN", why); return; }
                        Franchise.SignFreeAgent(f, id);
                        Audio.AudioManager.Play(SfxId.Coin, 0.7f);
                        Save();
                    }));
            }
        }

        // ------------------------------------------------------------------ draft

        private void BuildDraft()
        {
            var f = F;
            if (f.phase != FranchisePhase.Draft)
            {
                Line("The draft is after the playoffs: the four teams that miss the playoffs draw for the top two picks.", Theme.Cream, 28f, 90f);
                var rookies = f.players.FindAll(p => p.draftYear == f.year && p.draftPick > 0);
                if (rookies.Count == 0) rookies = f.players.FindAll(p => p.draftYear == f.year - 1 && p.draftPick > 0);
                rookies.Sort((a, b) => a.draftPick.CompareTo(b.draftPick));
                if (rookies.Count > 0) Heading("LATEST DRAFT CLASS");
                foreach (var p in rookies)
                    Line(Loc.T("PICK") + " " + p.draftPick + ": " + p.Name + " (" + (p.team >= 0 ? Abbr(Franchise.TeamId(p.team)) : "FA") + ")  " + p.Overall, Theme.Cream);
                return;
            }
            Franchise.DraftUntilYou(f);
            App.SaveCareer();
            if (f.phase != FranchisePhase.Draft) { Save(); return; }
            int clock = Franchise.OnTheClock(f);
            Heading(Loc.T("PICK") + " " + (f.draftMade + 1) + " · " + (clock == f.you ? Loc.T("YOU'RE ON THE CLOCK") : Abbr(Franchise.TeamId(clock))));
            foreach (var l in f.lottery) Line(l, Theme.Cyan, 24f, 40f);
            for (int i = 0; i < f.draftMade; i++)
            {
                var taken = f.players.Find(p => p.draftYear == f.year && p.draftPick == i + 1);
                if (taken != null) Line(Loc.T("PICK") + " " + (i + 1) + " " + Abbr(Franchise.TeamId(f.draftOrder[i])) + ": " + taken.Name, Theme.Muted, 24f, 40f);
            }
            Line(Loc.T("SCOUTING VISITS LEFT") + ": " + f.scoutPoints + "  ·  " + Loc.T("each visit narrows a prospect's rating"), Theme.Gold, 26f, 50f);
            foreach (var p in Franchise.Prospects(f))
            {
                Franchise.ScoutedRange(p, out int lo, out int hi);
                int id = p.id;
                string rating = lo == hi ? lo.ToString() : lo + "-" + hi;
                Row("<b>" + p.Name.ToUpperInvariant() + "</b>  <color=#FFD166>" + rating + "</color>\n<size=22><color=#8D99AE>" + Arch(p) + " · " + Loc.T("AGE") + " " + p.age
                    + " · POT " + Franchise.PotentialGrade(p) + " · " + Loc.T("scouted") + " " + p.scout + "/3</color></size>", Theme.Cream, 120f,
                    ("SCOUT", ButtonStyle.Ghost, p.scout < 3 && f.scoutPoints > 0 ? (System.Action)(() => { Franchise.Scout(f, id); Save(); }) : null),
                    ("DRAFT", ButtonStyle.Primary, clock == f.you ? (System.Action)(() =>
                    {
                        Franchise.Pick(f, id);
                        Audio.AudioManager.Play(SfxId.Fanfare, 0.6f);
                        Save();
                    }) : null));
            }
        }

        // ------------------------------------------------------------------ history

        private void BuildHistory()
        {
            var f = F;
            var t = Franchise.Totals(f, f.you);
            Heading(Team(f.you)?.FullName.ToUpperInvariant() ?? "");
            UiControls.Stat(_content, "SEASONS", t.seasons.ToString());
            UiControls.Stat(_content, "RECORD", t.wins + "-" + t.losses);
            UiControls.Stat(_content, "PLAYOFFS", t.playoffs.ToString());
            UiControls.Stat(_content, "TITLES", t.titles.ToString());
            foreach (var h in f.history)
                if (h.team == f.you)
                    Line(Loc.T("YEAR") + " " + h.year + ":  " + h.wins + "-" + h.losses + "  " + Loc.T(Franchise.FinishName(h.finish)) + "\n<size=22><color=#8D99AE>" + h.best + " · "
                         + Loc.T("payroll") + " " + Franchise.Money(h.payroll) + "</color></size>", h.finish == 3 ? Theme.Gold : Theme.Cream, 28f, 70f);

            Heading("CHAMPIONS");
            foreach (var h in f.history)
                if (h.finish == 3) Line(Loc.T("YEAR") + " " + h.year + ":  " + Team(h.team)?.FullName + "  (" + h.wins + "-" + h.losses + ")", h.team == f.you ? Theme.Gold : Theme.Cream);
            Heading("AWARDS");
            foreach (var a in f.awards)
                Line(Loc.T("YEAR") + " " + a.year + "  " + a.kind + ":  " + a.name + (a.team >= 0 && a.kind != "HALL" ? " (" + Abbr(Franchise.TeamId(a.team)) + ")" : "") + "  " + a.line,
                     a.kind == "HALL" ? Theme.Gold : Theme.Cream, 26f, 44f);
            Heading("TRANSACTIONS");
            for (int i = f.moves.Count - 1; i >= 0 && i >= f.moves.Count - 12; i--)
                Line(Loc.T("YEAR") + " " + f.moves[i].year + "  " + f.moves[i].text, Theme.Muted, 24f, 40f);

            UiKit.Button(_content, "NEW FRANCHISE", () => UiControls.Dialog("START OVER?",
                "This ends your franchise and its history for good.",
                ("START OVER", ButtonStyle.Primary, () =>
                {
                    App.Career.franchise = new FranchiseSaveData();
                    App.SaveCareer();
                    Refresh();
                }),
                ("CANCEL", ButtonStyle.Ghost, null)), ButtonStyle.Ghost, 100f, 36f);
        }
    }
}
