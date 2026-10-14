using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>SUMMER STORY (chapters with scenes and goals) and the COUCH CUP (2 Player tournament on one device).</summary>
    public sealed partial class MainMenuController
    {
        // ------------------------------------------------------------------ summer story

        private void ShowStoryMode()
        {
            var career = App.Career;
            var s = career.story ?? (career.story = new StorySaveData());
            var column = OpenOverlay("SUMMER STORY", out var footer);
            UiKit.Size(UiKit.Label(column, "One summer, eight games, one league title. You, Nova, Big Sal, Mic Tally on the mic, and Kojo Stride's Velvet Hour standing in the way.",
                                   28f, Theme.Cream), 120f);
            UiKit.Size(UiKit.Label(column, s.finished ? "SUNBURST CHAMPIONS √" : "CHAPTERS CLEARED: " + s.cleared + "/" + StoryMode.Chapters,
                                   32f, Theme.Gold, TextAlignmentOptions.Center, true), 56f);

            foreach (var ch in StoryMode.All)
            {
                var chapter = ch;
                bool open = StoryMode.Unlocked(s, ch.Number);
                bool done = s.best.Count >= ch.Number && s.best[ch.Number - 1] > 0;
                var row = UiKit.Row(column, 10f, "Chapter");
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                UiKit.Size(row, 130f);
                var label = UiKit.Label(row, open
                    ? "<b>" + ch.Number + ". " + ch.Title + "</b>  <size=22>" + ch.Format + (done ? "  ·  <color=#4CC9F0>CLEARED</color>" : "") + "</size>\n<size=22><color=#FFD166>"
                      + StoryMode.GoalText(ch) + "</color>  <color=#8D99AE>" + ch.Blurb + "</color></size>"
                    : "<b>" + ch.Number + ". ???</b>\n<size=22><color=#8D99AE>" + Loc.T("Clear the chapter before to open it") + "</color></size>",
                    28f, open ? Theme.Cream : Theme.Muted, TextAlignmentOptions.Left);
                label.textWrappingMode = TextWrappingModes.Normal;
                UiKit.Size(label).flexibleWidth = 1f;
                var b = UiKit.Button(row, open ? (done ? "REPLAY" : "PLAY") : "LOCKED", () => PlayChapter(chapter),
                                     open && !done ? ButtonStyle.Primary : ButtonStyle.Ghost, 110f, 26f);
                UiKit.Size(b, 110f, 170f);
                b.interactable = open;
            }
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
            // 14 Oct 2026: your agent Dee texts about every chapter.
            int unread = App.Career != null ? AgentTexts.Unread(App.Career.phone, App.Career.story) : 0;
            UiKit.Button(footer, unread > 0 ? "TEXTS (" + unread + ")" : "TEXTS", () => PhoneScreen.Open(PhoneScreen.App.Texts),
                         unread > 0 ? ButtonStyle.Primary : ButtonStyle.Secondary, 130f, 40f);
        }

        private void PlayChapter(StoryChapter ch)
        {
            CloseOverlay();
            // The scene before the game plays the first time; replays go straight to tip-off (it's in the scene list on REPLAY too).
            var scene = StoryMode.Scene(ch.Number, false, App.Career.nickname);
            bool seen = App.Career.storySeen.Contains(scene.Id);
            System.Action go = () =>
            {
                App.PendingMatch = StoryMode.Request(App.Catalog, App.Career, ch);
                SceneFlow.GoTo(SceneNames.Game);
            };
            if (seen) go();
            else StoryView.Show(scene, go);
        }

        /// <summary>After a chapter: its closing scene (first clear), then the chapter list.</summary>
        private void ShowStoryAfterGame()
        {
            int n = App.StoryOutroPending;
            App.StoryOutroPending = 0;
            if (n > 0)
            {
                var scene = StoryMode.Scene(n, true, App.Career.nickname);
                if (!App.Career.storySeen.Contains(scene.Id))
                {
                    StoryView.Show(scene, ShowStoryMode);
                    return;
                }
            }
            ShowStoryMode();
        }

        // ------------------------------------------------------------------ couch cup

        private static readonly List<string> _couchNames = new List<string> { "PLAYER 1", "PLAYER 2", "PLAYER 3", "PLAYER 4" };
        private static readonly List<int> _couchTeams = new List<int> { 0, 1, 2, 3 };

        private void ShowCouchCup()
        {
            FirstVisit(Tours.Couch);
            var cup = App.Career.couch ?? (App.Career.couch = new CouchCupSaveData());
            if (cup.Active)
            {
                ShowCouchBracket();
                return;
            }
            var teams = App.Catalog.TeamsInTier(TeamTier.League);
            var column = OpenOverlay("COUCH CUP", out var footer);
            UiKit.Size(UiKit.Label(column, "A knockout for 2 to 8 friends on this iPhone or iPad. Type names, pick teams, then pass the phone: two players at a time, flat on the table between you (or with controllers).",
                                   28f, Theme.Cream), 150f);
            if (cup.champion >= 0 && cup.champion < cup.names.Count)
                UiKit.Size(UiKit.Label(column, "LAST CHAMPION: " + cup.names[cup.champion].ToUpperInvariant(), 32f, Theme.Gold, TextAlignmentOptions.Center, true), 56f);

            for (int i = 0; i < _couchNames.Count; i++)
            {
                int index = i;
                var row = UiKit.Row(column, 12f, "Entrant");
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                UiKit.Size(row, 100f);
                var field = UiControls.TextField(row, _couchNames[i], 14, v => _couchNames[index] = v);
                UiKit.Size(field, 90f, 360f);
                TextMeshProUGUI teamLabel = null;
                var teamButton = UiKit.Button(row, teams[_couchTeams[i] % teams.Count].abbreviation, () =>
                {
                    _couchTeams[index] = (_couchTeams[index] + 1) % teams.Count;
                    teamLabel.text = teams[_couchTeams[index]].abbreviation;
                }, ButtonStyle.Secondary, 90f, 30f);
                teamLabel = teamButton.GetComponentInChildren<TextMeshProUGUI>();
                UiKit.Size(teamButton, 90f, 170f);
                if (_couchNames.Count > CouchCup.MinPlayers)
                {
                    var remove = UiKit.Button(row, "×", () =>
                    {
                        _couchNames.RemoveAt(index);
                        _couchTeams.RemoveAt(index);
                        ShowCouchCup();
                    }, ButtonStyle.Ghost, 90f, 30f);
                    UiKit.Size(remove, 90f, 90f);
                }
            }
            if (_couchNames.Count < CouchCup.MaxPlayers)
                UiKit.Button(column, "+ ADD PLAYER", () =>
                {
                    _couchNames.Add("PLAYER " + (_couchNames.Count + 1));
                    _couchTeams.Add(_couchNames.Count - 1);
                    ShowCouchCup();
                }, ButtonStyle.Ghost, 90f, 30f);
            UiKit.Size(UiKit.Label(column, "Tap a team to change it. The order is shuffled when the cup starts. Ties are played again.", 24f, Theme.Muted), 80f);

            UiKit.Button(footer, "BACK", ShowVersus, ButtonStyle.Ghost, 130f, 44f);
            UiKit.Button(footer, "START CUP", () =>
            {
                var ids = new List<string>();
                foreach (int t in _couchTeams) ids.Add(teams[t % teams.Count].id);
                CouchCup.Start(cup, _couchNames, ids, (uint)System.Environment.TickCount | 1u);
                App.SaveCareer();
                ShowCouchBracket();
            }, ButtonStyle.Primary, 130f);
        }

        private void ShowCouchBracket()
        {
            var c = App.Catalog;
            var cup = App.Career.couch;
            var column = OpenOverlay("COUCH CUP", out var footer);
            var next = CouchCup.NextGame(cup);
            if (cup.champion >= 0)
                UiKit.Size(UiKit.Label(column, "CHAMPION: " + cup.names[cup.champion].ToUpperInvariant(), 44f, Theme.Gold, TextAlignmentOptions.Center, true), 80f);
            else if (next != null)
            {
                UiKit.Size(UiKit.Label(column, CouchCup.RoundName(cup, next.round) + "  ·  UP NEXT", 30f, Theme.Gold, TextAlignmentOptions.Center, true), 50f);
                UiKit.Size(UiKit.Label(column, "<color=#FFD166>" + cup.names[next.a].ToUpperInvariant() + "</color> (" + (c.Team(cup.teamIds[next.a])?.abbreviation ?? "?") + ")  vs  <color=#4CC9F0>"
                                       + cup.names[next.b].ToUpperInvariant() + "</color> (" + (c.Team(cup.teamIds[next.b])?.abbreviation ?? "?") + ")", 38f, Theme.Cream, TextAlignmentOptions.Center, true), 70f);
                UiKit.Size(UiKit.Label(column, cup.names[next.a] + " is Player 1 (bottom edge), " + cup.names[next.b] + " is Player 2 (top edge).", 24f, Theme.Muted, TextAlignmentOptions.Center), 50f);
            }

            int rounds = CouchCup.Rounds(cup);
            for (int r = 0; r < rounds; r++)
            {
                var games = cup.games.FindAll(g => g.round == r);
                if (games.Count == 0) continue;
                UiKit.Size(UiKit.Label(column, CouchCup.RoundName(cup, r), 28f, Theme.Gold, TextAlignmentOptions.Left, true), 46f);
                foreach (var g in games)
                {
                    string a = cup.names[g.a].ToUpperInvariant();
                    string line = g.b < 0 ? a + "  —  BYE"
                        : g.played ? (g.Winner == g.a ? "<b>" + a + "</b>" : a) + "  " + g.scoreA + " - " + g.scoreB + "  " + (g.Winner == g.b ? "<b>" + cup.names[g.b].ToUpperInvariant() + "</b>" : cup.names[g.b].ToUpperInvariant())
                        : a + "  vs  " + cup.names[g.b].ToUpperInvariant();
                    UiKit.Size(UiKit.Label(column, line, 28f, g.played ? Theme.Cream : Theme.Muted, TextAlignmentOptions.Left), 44f);
                }
            }

            UiKit.Button(footer, "BACK", ShowVersus, ButtonStyle.Ghost, 130f, 44f);
            if (next != null && NearbyLink.Supported)
                UiKit.Button(footer, "2 PHONES", ShowLinkCupHost, ButtonStyle.Secondary, 130f, 36f);
            if (next != null)
                UiKit.Button(footer, "PLAY", () =>
                {
                    App.PendingMatch = CouchCup.Request(cup, c, next, App.Career.settings.difficultyId);
                    SceneFlow.GoTo(SceneNames.Game);
                }, ButtonStyle.Primary, 130f);
            UiKit.Button(footer, cup.champion >= 0 ? "NEW CUP" : "END CUP", () =>
            {
                UiControls.Dialog("COUCH CUP", cup.champion >= 0 ? "Start a new Couch Cup?" : "End this Couch Cup? The bracket is lost.",
                           ("YES", ButtonStyle.Primary, () =>
                           {
                               // Keep the names and teams for the next cup.
                               _couchNames.Clear();
                               _couchTeams.Clear();
                               var teams = c.TeamsInTier(TeamTier.League);
                               for (int i = 0; i < cup.names.Count; i++)
                               {
                                   _couchNames.Add(cup.names[i]);
                                   _couchTeams.Add(System.Math.Max(0, teams.FindIndex(t => t.id == cup.teamIds[i])));
                               }
                               CouchCup.Reset(cup);
                               App.SaveCareer();
                               ShowCouchCup();
                           }),
                           ("NO", ButtonStyle.Ghost, null));
            }, ButtonStyle.Ghost, 130f, 40f);
        }
    }
}
