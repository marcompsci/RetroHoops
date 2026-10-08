using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// SeasonScene: the Rise Mode hub. Shows the current stage, crew record, energy and
    /// chemistry, the objective, and PLAY NEXT. In the league it adds standings and, in the
    /// playoffs, the bracket. Pending event cards open as a modal before the next game.
    /// All rules live in <see cref="RiseEngine"/> / <see cref="SeasonEngine"/>; this only draws them.
    /// </summary>
    public sealed class SeasonScreenController : ScreenBase
    {
        protected override string ScreenTitle => "RISE MODE";
        protected override string BackdropCourtId => "court.overpass_park";
        protected override int MusicTrack => 2;

        private RectTransform _content;

        protected override void Build()
        {
            _content = UiKit.ScrollColumn(Body, 18f, new RectOffset(48, 48, 12, 40));
            Rebuild();

            if (App.OpenAllStar)
            {
                App.OpenAllStar = false;
                AllStarScreen.Open(AllStar.WeekendOpen(App.Career));
            }
            var outcome = App.LastRiseOutcome;
            App.LastRiseOutcome = RiseOutcome.None;
            // Story scenes play first, then whatever the last game changed.
            PlayStory(() => AfterStory(outcome));
        }

        private void PlayStory(System.Action then)
        {
            string pending = Story.Pending(App.Career);
            if (pending == null)
            {
                then();
                return;
            }
            StoryView.Show(Story.Beat(pending, App.Career.nickname), () =>
            {
                Rebuild();
                PlayStory(then); // several scenes can be due at once (e.g. circuit cleared + rival)
            });
        }

        private void AfterStory(RiseOutcome outcome)
        {
            if (outcome == RiseOutcome.Champion)
                UiControls.Dialog("CHAMPIONS!", "The First Callers hold " + DefaultContent.ChampionshipName + ". Start the next season when you're ready to defend it.",
                                  ("NICE", ButtonStyle.Primary, ShowPendingEvent));
            else if (outcome == RiseOutcome.EnteredLeague)
                UiControls.Dialog("CIRCUIT CLEARED", "Every street crew is beaten. " + DefaultContent.LeagueName + " has an open spot — it's yours.",
                                  ("LET'S GO", ButtonStyle.Primary, ShowPendingEvent));
            else if (outcome == RiseOutcome.MadePlayoffs)
                UiControls.Dialog("PLAYOFFS", "Top " + SeasonEngine.PlayoffTeams + " finish. Win two more for the Cup.",
                                  ("OK", ButtonStyle.Primary, ShowPendingEvent));
            else
                ShowPendingEvent();
        }

        private void Rebuild()
        {
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);

            var c = App.Catalog;
            var career = App.Career;
            var r = career.rise;

            UiKit.Size(UiKit.Label(_content, StageName(r.stage), 52f, Theme.Gold, TextAlignmentOptions.Center, true), 80f);
            UiKit.Size(UiKit.Label(_content, RiseEngine.Objective(r, c), 36f, Theme.Cream), 110f);

            var stats = UiKit.Row(_content, 12f, "Meters");
            UiKit.Size(stats, 64f);
            UiControls.Stat(stats, "ENERGY", r.energy.ToString());
            UiControls.Stat(stats, "CHEM", r.chemistry.ToString());
            if (r.season != null && r.stage != RiseStage.Circuit)
            {
                var rec = FindRecord(r.season, RiseEngine.CrewId);
                UiControls.Stat(stats, "RECORD", rec != null ? rec.Wins + "-" + rec.Losses : "0-0");
            }
            else
            {
                UiControls.Stat(stats, "CIRCUIT", r.circuitBeaten.Count + "/" + RiseEngine.CircuitOrder.Length);
            }

            // Rival Challenge: once a season, from week 5. Doesn't count in the standings.
            if (RivalEngine.ChallengeAvailable(career))
            {
                var rivalReq = RivalEngine.Challenge(career, c, career.settings.difficultyId);
                var rivalTeam = c.Team(rivalReq?.AwayTeamId);
                UiKit.Button(_content, Loc.T("RIVAL CHALLENGE:") + " " + (rivalTeam?.FullName ?? "?").ToUpperInvariant(), () =>
                {
                    App.PendingMatch = rivalReq;
                    SceneFlow.GoTo(SceneNames.Game);
                }, ButtonStyle.Primary, 120f, 40f);
                UiKit.Size(UiKit.Label(_content, "Doesn't count in the standings. Win for +" + RivalEngine.WinBonus + " SP.  Rival record: " +
                                       career.rival.wins + "-" + career.rival.losses, 28f, Theme.Muted), 50f);
            }

            // All-Star Weekend: from mid-season to the end of the regular season.
            if (AllStar.WeekendOpen(career))
            {
                var w = AllStar.Weekend(career);
                int done = (w.dunkDone ? 1 : 0) + (w.threeDone ? 1 : 0) + (w.gameDone ? 1 : 0);
                UiKit.Button(_content, "ALL-STAR WEEKEND", () => AllStarScreen.Open(true), done < 3 ? ButtonStyle.Primary : ButtonStyle.Secondary, 120f, 40f);
                UiKit.Size(UiKit.Label(_content, "Dunk Contest, 3-Point Contest, All-Star Game  ·  " + done + "/3 " + Loc.T("done"), 28f, Theme.Muted), 50f);
            }

            var next = RiseEngine.NextMatch(r, c, career.settings.difficultyId);
            if (next != null)
            {
                var opp = c.Team(next.AwayTeamId);
                var court = c.Court(next.CourtId);
                var card = UiKit.Panel(_content, Color.white, Theme.PanelSprite(), true, "NextGame");
                UiKit.Size(card, 170f);
                if (opp != null)
                {
                    var logo = UiKit.Picture(card.transform, TextureFactory.TeamLogo(opp));
                    logo.rectTransform.anchorMin = logo.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    logo.rectTransform.pivot = new Vector2(0f, 0.5f);
                    logo.rectTransform.sizeDelta = new Vector2(130f, 130f);
                    logo.rectTransform.anchoredPosition = new Vector2(20f, 0f);
                }
                string round = next.Round == 2 ? "FINAL  ·  FULL COURT 5 ON 5" : (next.Round == 1 ? "SEMIFINAL" : "NEXT GAME");
                var text = UiKit.Label(card.transform, "<color=#FFD166>" + round + "</color>\nVS " + (opp?.FullName ?? "?").ToUpperInvariant() +
                                       (court != null ? "\n<size=70%><color=#8D99AE>" + court.displayName + "</color></size>" : ""),
                                       36f, Theme.Cream, TextAlignmentOptions.Left, true);
                UiKit.Stretch(text.rectTransform);
                text.rectTransform.offsetMin = new Vector2(170f, 8f);
                text.rectTransform.offsetMax = new Vector2(-20f, -8f);

                UiKit.Button(_content, "PLAY NEXT", () => Play(next), ButtonStyle.Primary, 150f);
            }
            else if (r.stage == RiseStage.Complete && Pro.NextSeasonLocked(r, ProStore.Owned))
            {
                // Season 1 is free; season 2 and up (eleven more rival crews) come with Retro Hoops Pro.
                UiKit.Size(UiKit.Label(_content, Loc.T("Season") + " " + (r.seasonsPlayed + 1) + ": " +
                                       Loc.T("eleven more rival crews are waiting. Seasons 2 and up come with Retro Hoops Pro, a one-time unlock."),
                                       30f, Theme.Cream, TextAlignmentOptions.Center), 110f);
                UiKit.Button(_content, "GET RETRO HOOPS PRO", () => { App.OpenProOnMenu = true; SceneFlow.GoTo(SceneNames.MainMenu); }, ButtonStyle.Primary, 150f);
            }
            else if (r.stage == RiseStage.Complete)
            {
                UiKit.Button(_content, "START NEXT SEASON", StartNextSeason, ButtonStyle.Primary, 150f);
            }

            if (r.season != null && r.stage != RiseStage.Circuit)
            {
                Bracket(r.season);
                StandingsTable(r.season);
            }
            else
            {
                CircuitList(r);
            }

            if (career.dynasty.draftPool.Count > 0)
                UiKit.Button(_content, "DRAFT PICK WAITING", ShowDraft, ButtonStyle.Primary, 110f, 40f);
            UiKit.Button(_content, "YOUR CREW", ShowCrew, ButtonStyle.Secondary, 110f, 40f);
            UiKit.Button(_content, "LEAGUE HISTORY", ShowHistory, ButtonStyle.Ghost, 100f, 36f);
            var mates = CrewEngine.TeammateDefs(career, c);
            UiKit.Size(UiKit.Label(_content, "With " + string.Join(" & ", mates.ConvertAll(m => m.DisplayName)) +
                                   "  ·  " + CrewEngine.Pool(career, c).Count + " players available", 28f, Theme.Muted), 50f);

            var diffs = c.Difficulties;
            int di = Mathf.Max(0, diffs.FindIndex(d => d.id == career.settings.difficultyId));
            UiControls.ChoiceRow(_content, "DIFFICULTY", diffs.ConvertAll(d => d.displayName.ToUpperInvariant()).ToArray(), di, i =>
            {
                career.settings.difficultyId = diffs[i].id;
                App.SaveCareer();
            });
        }

        private void Play(MatchRequest request)
        {
            if (!string.IsNullOrEmpty(App.Career.rise.pendingEventId))
            {
                ShowPendingEvent();
                return;
            }
            App.PendingMatch = request;
            SceneFlow.GoTo(SceneNames.Game);
        }

        private void ShowPendingEvent()
        {
            var r = App.Career.rise;
            var card = EventCards.Find(r.pendingEventId);
            if (card == null) return;
            var buttons = new (string, ButtonStyle, System.Action)[card.choices.Count];
            for (int i = 0; i < card.choices.Count; i++)
            {
                int index = i;
                var choice = card.choices[i];
                buttons[i] = (Loc.T(choice.label).ToUpperInvariant() + "\n<size=60%>" + choice.effect.Describe() + "</size>",
                              i == 0 ? ButtonStyle.Primary : ButtonStyle.Secondary,
                              () =>
                              {
                                  RiseEngine.ResolveEvent(r, App.Career, index);
                                  App.SaveCareer();
                                  Rebuild();
                              });
            }
            UiControls.Dialog(Loc.T(card.title).ToUpperInvariant(), Loc.T(card.body), buttons);
        }

        // ------------------------------------------------------------------ dynasty

        /// <summary>Off-season first (aging, retirements, Hall of Fame, your draft pick), then the new season.</summary>
        private void StartNextSeason()
        {
            var career = App.Career;
            var c = App.Catalog;
            if (Pro.NextSeasonLocked(career.rise, ProStore.Owned)) { Rebuild(); return; }
            var report = DynastyEngine.OffSeason(career, c);
            if (report == null)
            {
                RiseEngine.StartNextSeason(career.rise, c);
                App.SaveCareer();
                Rebuild();
                return;
            }
            App.SaveCareer();
            string champ = c.Team(report.ChampionId)?.FullName ?? "?";
            string body = "Champion: " + champ + ".\n" +
                          (report.Retired.Count > 0 ? "Retired: " + Short(report.Retired) + ".\n" : "Nobody retired.\n") +
                          (report.HallOfFame.Count > 0 ? "Hall of Fame: " + string.Join(", ", report.HallOfFame) + "!\n" : "") +
                          (report.Improved.Count > 0 ? "Rising: " + Short(report.Improved) + "." : "");
            UiControls.Dialog(Loc.T("OFF-SEASON") + " · " + Loc.T("YEAR") + " " + report.Year, body,
                              ("DRAFT DAY", ButtonStyle.Primary, ShowDraft));
        }

        private static string Short(System.Collections.Generic.List<string> names) =>
            names.Count <= 3 ? string.Join(", ", names) : string.Join(", ", names.GetRange(0, 3)) + " +" + (names.Count - 3);

        /// <summary>Draft day: take one of three prospects for your crew (free to put on the floor).</summary>
        private void ShowDraft()
        {
            var c = App.Catalog;
            var career = App.Career;
            var col = OpenPanel("DRAFT DAY", out var footer);
            UiKit.Size(UiKit.Label(col, "Pick one prospect. They join your crew for free and grow every season they play.", 30f, Theme.Cream), 90f);
            foreach (var id in new System.Collections.Generic.List<string>(career.dynasty.draftPool))
            {
                var p = c.Player(id);
                if (p == null) continue;
                var arch = c.ArchetypeById(p.archetypeId);
                int age = DynastyEngine.AgeOf(career.dynasty, id);
                UiKit.Size(UiKit.Label(col, p.DisplayName.ToUpperInvariant() + "  #" + p.jerseyNumber +
                                       "\n<size=75%><color=#8D99AE>" + (arch?.displayName ?? "") + "  ·  AGE " + age + "  ·  OVR " + p.attributes.Overall + "</color></size>",
                                       36f, Theme.Gold, TextAlignmentOptions.Left, true), 100f);
                UiKit.Button(col, "DRAFT " + p.lastName.ToUpperInvariant(), () =>
                {
                    DynastyEngine.Draft(App.Career, App.Catalog, id);
                    RiseEngine.StartNextSeason(App.Career.rise, App.Catalog);
                    App.SaveCareer();
                    Core.Haptics.Success();
                    Audio.AudioManager.Play(SfxId.Fanfare, 0.7f);
                    ClosePanel();
                    Rebuild();
                }, ButtonStyle.Secondary, 100f, 36f);
            }
            UiKit.Button(footer, "LATER", () =>
            {
                // The pick waits in the hub; the season can still start.
                if (App.Career.rise.stage == RiseStage.Complete) RiseEngine.StartNextSeason(App.Career.rise, App.Catalog);
                App.SaveCareer();
                ClosePanel();
                Rebuild();
            }, ButtonStyle.Ghost, 120f, 40f);
        }

        /// <summary>League history: champions by year, titles by team, and the Hall of Fame.</summary>
        private void ShowHistory()
        {
            var c = App.Catalog;
            var d = App.Career.dynasty;
            var col = OpenPanel("LEAGUE HISTORY", out var footer);
            UiKit.Button(footer, "DONE", ClosePanel, ButtonStyle.Primary, 120f);
            UiKit.Size(UiKit.Label(col, "CHAMPIONS", 36f, Theme.Cyan, TextAlignmentOptions.Left, true), 56f);
            if (d.champions.Count == 0) UiKit.Size(UiKit.Label(col, "No champions yet. Finish a Rise season.", 30f, Theme.Muted), 50f);
            for (int i = d.champions.Count - 1; i >= 0; i--)
            {
                var ch = d.champions[i];
                bool you = ch.teamId == DefaultContent.PlayerCrewId;
                UiKit.Size(UiKit.Label(col, "SEASON " + ch.season + "   " + (c.Team(ch.teamId)?.FullName ?? "?").ToUpperInvariant(), 32f,
                                       you ? Theme.Gold : Theme.Cream, TextAlignmentOptions.Left, true), 48f);
            }
            var titles = DynastyEngine.TitlesByTeam(d);
            if (titles.Count > 0)
            {
                UiKit.Size(UiKit.Label(col, "MOST TITLES", 36f, Theme.Cyan, TextAlignmentOptions.Left, true), 56f);
                foreach (var kv in titles)
                    UiKit.Size(UiKit.Label(col, (c.Team(kv.Key)?.FullName ?? "?").ToUpperInvariant() + "   " + kv.Value, 32f, Theme.Cream, TextAlignmentOptions.Left, true), 48f);
            }
            UiKit.Size(UiKit.Label(col, "HALL OF FAME", 36f, Theme.Cyan, TextAlignmentOptions.Left, true), 56f);
            if (d.hall.Count == 0) UiKit.Size(UiKit.Label(col, "Empty for now. Legends retire after a few seasons.", 30f, Theme.Muted), 50f);
            foreach (var h in d.hall)
                UiKit.Size(UiKit.Label(col, h.name.ToUpperInvariant() + "\n<size=70%><color=#8D99AE>" + h.team + "  ·  " + h.seasons + " SEASONS  ·  PEAK " + h.peak +
                                       "  ·  YEAR " + h.year + "</color></size>", 32f, Theme.Gold, TextAlignmentOptions.Left, true), 80f);
        }

        private RectTransform OpenPanel(string title, out RectTransform footer)
        {
            ClosePanel();
            var canvas = UiKit.CreateScreenCanvas("HubPanel", 30);
            _crewOverlay = canvas.gameObject;
            var scrim = UiKit.Panel(canvas.transform, Theme.Scrim, name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(canvas.transform);
            var panel = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "Panel");
            UiKit.Band(panel.rectTransform, 0.04f, 0.96f, 40f);
            panel.raycastTarget = true;
            footer = UiKit.Row(panel.transform, 20f, "Footer");
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(-80f, 130f);
            footer.anchoredPosition = new Vector2(0f, 24f);
            var body = UiKit.NewRect("Body", panel.transform);
            UiKit.Stretch(body);
            body.offsetMin = new Vector2(0f, 170f);
            var col = UiKit.ScrollColumn(body, 14f, new RectOffset(36, 36, 30, 20));
            UiKit.Size(UiKit.ShadowLabel(col, title, 60f, Theme.Cream, Theme.Pink, 6f).transform.parent.GetComponent<RectTransform>(), 90f);
            return col;
        }

        private void ClosePanel()
        {
            if (_crewOverlay != null) Destroy(_crewOverlay);
            _crewOverlay = null;
        }

        // ------------------------------------------------------------------ crew

        private GameObject _crewOverlay;

        /// <summary>Your two teammate spots and everyone you can sign or swap in.</summary>
        private void ShowCrew()
        {
            if (_crewOverlay != null) Destroy(_crewOverlay);
            var c = App.Catalog;
            var career = App.Career;
            var canvas = UiKit.CreateScreenCanvas("CrewOverlay", 30);
            _crewOverlay = canvas.gameObject;
            var scrim = UiKit.Panel(canvas.transform, Theme.Scrim, name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(canvas.transform);
            var panel = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "Panel");
            UiKit.Band(panel.rectTransform, 0.04f, 0.96f, 40f);
            panel.raycastTarget = true;
            var footer = UiKit.Row(panel.transform, 20f, "Footer");
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(-80f, 130f);
            footer.anchoredPosition = new Vector2(0f, 24f);
            UiKit.Button(footer, "DONE", () =>
            {
                Destroy(_crewOverlay);
                _crewOverlay = null;
                Rebuild();
            }, ButtonStyle.Primary, 120f);
            var body = UiKit.NewRect("Body", panel.transform);
            UiKit.Stretch(body);
            body.offsetMin = new Vector2(0f, 170f);
            var col = UiKit.ScrollColumn(body, 14f, new RectOffset(36, 36, 30, 20));

            UiKit.Size(UiKit.ShadowLabel(col, "YOUR CREW", 60f, Theme.Cream, Theme.Pink, 6f).transform.parent.GetComponent<RectTransform>(), 90f);
            UiKit.Size(UiKit.Label(col, "SP: " + career.signalPoints + "    Beat a team to unlock its players (league teams: their bench player).",
                                   28f, Theme.Muted), 70f);

            var mates = CrewEngine.Teammates(career, c);
            for (int slot = 0; slot < mates.Count; slot++)
            {
                var p = c.Player(mates[slot]);
                UiKit.Size(UiKit.Label(col, "SPOT " + (slot + 1) + ":  " + Describe(p), 34f, Theme.Gold, TextAlignmentOptions.Left, true), 56f);
            }

            UiKit.Size(UiKit.Label(col, "AVAILABLE", 36f, Theme.Cyan, TextAlignmentOptions.Left, true), 56f);
            foreach (var id in CrewEngine.Pool(career, c))
            {
                if (mates.Contains(id)) continue;
                var p = c.Player(id);
                var check = CrewEngine.CanRecruit(career, c, id);
                bool free = CrewEngine.IsFree(career, c, id);
                string price = free ? "FREE" : CrewEngine.Cost(p) + " SP";
                UiKit.Size(UiKit.Label(col, Describe(p) + "   <color=#FFD166>" + price + "</color>", 32f, Theme.Cream, TextAlignmentOptions.Left, false), 52f);
                var row = UiKit.Row(col, 16f, "Swap " + id);
                UiKit.Size(row, 90f);
                for (int slot = 0; slot < CrewEngine.TeammateSlots; slot++)
                {
                    int s = slot;
                    var b = UiKit.Button(row, "PUT IN SPOT " + (slot + 1), () =>
                    {
                        if (CrewEngine.Recruit(App.Career, App.Catalog, id, s) == RecruitCheck.Ok)
                        {
                            App.SaveCareer();
                            Core.Haptics.Success();
                        }
                        ShowCrew();
                    }, check == RecruitCheck.Ok ? ButtonStyle.Secondary : ButtonStyle.Ghost, 80f, 28f);
                    b.interactable = check == RecruitCheck.Ok;
                }
            }
        }

        private string Describe(PlayerDef p)
        {
            if (p == null) return "?";
            var arch = App.Catalog.ArchetypeById(p.archetypeId);
            return p.DisplayName.ToUpperInvariant() + " #" + p.jerseyNumber + "  ·  " +
                   (arch?.displayName ?? "").ToUpperInvariant() + "  ·  OVR " + p.attributes.Overall;
        }

        private void CircuitList(RiseSaveData r)
        {
            var c = App.Catalog;
            UiKit.Size(UiKit.Label(_content, DefaultContent.CircuitName.ToUpperInvariant(), 40f, Theme.Gold, TextAlignmentOptions.Left, true), 60f);
            foreach (var id in RiseEngine.CircuitOrder)
            {
                var team = c.Team(id);
                if (team == null) continue;
                bool beaten = r.circuitBeaten.Contains(id);
                UiKit.Size(UiKit.Label(_content, (beaten ? "<color=#4CC9F0>BEATEN</color>  " : "<color=#8D99AE>—</color>  ") + team.FullName.ToUpperInvariant(),
                                       34f, Theme.Cream, TextAlignmentOptions.Left, true), 52f);
            }
        }

        private void Bracket(SeasonSaveData s)
        {
            // Phase 33: drawn as a bracket (the projected picture from mid-season on).
            if (!SeasonEngine.HasRound(s, 1) && s.currentWeek < s.weeks / 2) return;
            BracketView.Draw(_content, PlayoffBracket.From(s), Abbr, RiseEngine.CrewId, DefaultContent.ChampionshipName.ToUpperInvariant());
        }

        private void StandingsTable(SeasonSaveData s)
        {
            UiKit.Size(UiKit.Label(_content, "STANDINGS  ·  WEEK " + Mathf.Min(s.currentWeek + 1, Mathf.Max(1, s.weeks)) + "/" + s.weeks,
                                   40f, Theme.Gold, TextAlignmentOptions.Left, true), 60f);
            UiKit.Size(UiKit.Label(_content, "#  TEAM<pos=62%>W-L<pos=76%>DIFF<pos=90%>STK", 28f, Theme.Muted, TextAlignmentOptions.Left, true), 40f);
            var table = SeasonEngine.Standings(s);
            for (int i = 0; i < table.Count; i++)
            {
                var t = table[i];
                var team = App.Catalog.Team(t.TeamId);
                bool mine = t.TeamId == RiseEngine.CrewId;
                string diff = t.Differential > 0 ? "+" + t.Differential : t.Differential.ToString();
                string line = (i + 1) + "  " + (team?.FullName ?? t.TeamId).ToUpperInvariant() +
                              "<pos=62%>" + t.Wins + "-" + t.Losses + "<pos=76%>" + diff + "<pos=90%>" + t.StreakText;
                var label = UiKit.Label(_content, line, 30f, mine ? Theme.Gold : (i < SeasonEngine.PlayoffTeams ? Theme.Cream : Theme.Muted),
                                        TextAlignmentOptions.Left, mine);
                UiKit.Size(label, 46f);
            }
        }

        private static TeamRecord FindRecord(SeasonSaveData s, string id)
        {
            foreach (var t in SeasonEngine.Standings(s)) if (t.TeamId == id) return t;
            return null;
        }

        private static string Abbr(string id) => App.Catalog.Team(id)?.abbreviation ?? "?";

        private static string StageName(RiseStage stage)
        {
            switch (stage)
            {
                case RiseStage.Circuit: return DefaultContent.CircuitName.ToUpperInvariant();
                case RiseStage.Season: return DefaultContent.LeagueName.ToUpperInvariant();
                case RiseStage.Playoffs: return "PLAYOFFS";
                default: return "SEASON COMPLETE";
            }
        }
    }
}
