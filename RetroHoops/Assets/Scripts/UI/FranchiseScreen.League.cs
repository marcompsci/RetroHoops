using System.Collections.Generic;
using System.Text;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// I81–I95 on the Franchise screen: player cards (with EXTEND and TRADE BLOCK), league leaders, power rankings and the
    /// preseason preview, free-agent interest, draft grades, the GM legacy, injuries and renaming your club.
    /// Every rule lives in <see cref="FranchiseLeague"/>.
    /// </summary>
    public sealed partial class FranchiseScreen
    {
        private string TeamName(int team) => Team(team)?.abbreviation ?? "?";

        /// <summary>I83: a player's card, with what you can do with him.</summary>
        private void ShowCard(FrPlayer p)
        {
            var f = F;
            var sb = new StringBuilder();
            sb.Append(Arch(p)).Append(" · ").Append(Loc.T("AGE")).Append(' ').Append(p.age).Append(" · OVR ").Append(p.Overall);
            if (p.team >= 0) sb.Append(" · ").Append(TeamName(p.team));
            if (p.team == f.you) sb.Append("\n").Append(Loc.T("MOOD")).Append(' ').Append(FrontOffice.Morale(p)).Append(" (").Append(Loc.T(FrontOffice.MoodName(FrontOffice.Morale(p)))).Append(')');
            if (p.tenure > 0 && p.team >= 0) sb.Append("\n").Append(p.tenure + 1).Append(' ').Append(Loc.T("seasons with this club"));
            if (p.allStarNods > 0) sb.Append("\n").Append(Loc.T("ALL-STAR")).Append(" × ").Append(p.allStarNods);
            if (p.team == f.you && FranchiseLife.Mentees(f).Contains(p)) sb.Append("\n").Append(Loc.T("Learning from a veteran mentor"));
            if (p.injured > 0) sb.Append("\n").Append(Loc.T("INJURED")).Append(": ").Append(p.injured).Append(' ').Append(Loc.T(p.injured == 1 ? "game" : "games"));
            sb.Append("\n").Append(Franchise.Money(p.salary)).Append(" × ").Append(p.years);
            if (p.extension > 0) sb.Append("  →  ").Append(Franchise.Money(p.extension)).Append(" ").Append(Loc.T("from next season"));
            foreach (var l in FranchiseLeague.CardLines(f, p, TeamName)) sb.Append("\n").Append(l);

            var buttons = new List<(string, ButtonStyle, System.Action)>();
            if (p.team == f.you)
            {
                string why = FranchiseLeague.CannotExtend(f, p);
                if (why == null)
                    buttons.Add(("EXTEND " + Franchise.Money(FranchiseLeague.ExtensionSalary(p)), ButtonStyle.Primary, () =>
                    {
                        FranchiseLeague.Extend(f, p.id);
                        Save();
                    }));
                if (!p.created)
                    buttons.Add((p.onBlock ? "TAKE OFF THE BLOCK" : "PUT ON THE BLOCK", ButtonStyle.Secondary, () =>
                    {
                        FranchiseLeague.ToggleBlock(f, p.id);
                        Save();
                    }));
            }
            var camp = CampButton(f, p, Save); // I116
            if (camp.HasValue && buttons.Count < 2) buttons.Add(camp.Value);
            buttons.Add(("CLOSE", ButtonStyle.Ghost, null));
            UiControls.Dialog("#" + p.number + " " + p.Name.ToUpperInvariant(), sb.ToString(), buttons.ToArray());
        }

        /// <summary>I82 + I92 + I93 on the LEAGUE tab.</summary>
        private void LeagueExtras(FranchiseSaveData f)
        {
            Heading("LEAGUE LEADERS");
            for (int s = 0; s < FranchiseLeague.LeaderStats.Length; s++)
            {
                var top = FranchiseLeague.Leaders(f, s, 3);
                if (top.Count == 0) continue;
                var parts = new List<string>();
                foreach (var p in top) parts.Add(p.Short + " (" + TeamName(p.team) + ") " + FranchiseLeague.PerGame(p, s).ToString("0.0"));
                Line("<b>" + FranchiseLeague.LeaderStats[s] + "</b>  " + string.Join("  ·  ", parts), Theme.Cream, 24f, 60f);
            }

            if (f.phase == FranchisePhase.Regular || f.phase == FranchisePhase.Playoffs)
            {
                Heading("POWER RANKINGS");
                var power = FranchiseLeague.PowerRankings(f);
                var preview = FranchiseLeague.Preview(f);
                for (int i = 0; i < power.Count; i++)
                {
                    int t = power[i];
                    int was = preview.IndexOf(t);
                    string move = was < 0 || was == i ? "" : was > i ? "  <color=#7BE0AD>▲" + (was - i) + "</color>" : "  <color=#FF6B8B>▼" + (i - was) + "</color>";
                    Line((i + 1) + ".  " + (Team(t)?.FullName ?? "?") + "  <size=20><color=#8D99AE>" + Loc.T("preview") + " #" + (was + 1) + "</color></size>" + move,
                         t == f.you ? Theme.Gold : Theme.Cream, 26f, 44f);
                }
            }
            else if (f.phase == FranchisePhase.Preseason)
            {
                Heading("SEASON PREVIEW");
                var preview = FranchiseLeague.Preview(f);
                for (int i = 0; i < preview.Count; i++)
                    Line((i + 1) + ".  " + (Team(preview[i])?.FullName ?? "?"), preview[i] == f.you ? Theme.Gold : Theme.Cream, 26f, 44f);
            }
            var l = FranchiseLeague.Of(f);
            if (!string.IsNullOrEmpty(l.previewResult) && f.phase != FranchisePhase.Regular && f.phase != FranchisePhase.Playoffs)
                Line(Loc.T("LAST SEASON") + "  " + l.previewResult, Theme.Cyan, 24f, 44f);
        }

        /// <summary>I86: how keen a free agent is.</summary>
        private static string InterestTag(FranchiseSaveData f, FrPlayer p)
        {
            int i = FranchiseLeague.Interest(f, p);
            string color = i >= 60 ? "#7BE0AD" : i >= FranchiseLeague.InterestNeeded ? "#FFD166" : "#FF6B8B";
            return "<color=" + color + ">" + Loc.T("INTEREST") + " " + i + "%</color>";
        }

        /// <summary>I91 on the DRAFT tab (outside the draft).</summary>
        private void DraftGrades(FranchiseSaveData f)
        {
            var l = FranchiseLeague.Of(f);
            if (l.grades.Count == 0) return;
            Heading(Loc.T("DRAFT GRADES") + "  ·  " + Loc.T("YEAR") + " " + l.grades[0].year);
            foreach (var g in l.grades)
                Line("<b>" + g.grade + "</b>  " + (Team(g.team)?.FullName ?? "?") + "\n<size=22><color=#8D99AE>" + g.line + "</color></size>",
                     g.team == f.you ? Theme.Gold : Theme.Cream, 26f, 70f);
        }

        /// <summary>I90 on the HISTORY tab.</summary>
        private void LegacyBlock(FranchiseSaveData f)
        {
            int legacy = FranchiseLeague.Of(f).legacy;
            int next = FranchiseLeague.NextRankAt(legacy);
            Heading("GM LEGACY");
            Line("<b>" + Loc.T(FranchiseLeague.Rank(legacy)) + "</b>  ·  " + legacy + " " + Loc.T("points")
                 + (next > 0 ? "  ·  " + (next - legacy) + " " + Loc.T("to the next rank") : ""), Theme.Gold, 28f, 50f);
            Line("Wins, playoff runs, titles and your club's awards add up every season.", Theme.Muted, 22f, 40f);
        }

        /// <summary>I88 + I89 on the OFFICE tab.</summary>
        private void OfficeLeagueExtras(FranchiseSaveData f)
        {
            var l = FranchiseLeague.Of(f);
            Heading("YOUR CLUB'S NAME");
            Line("Leave a box empty to keep the original. Names are checked for bad words.", Theme.Muted, 22f, 40f);
            string city = l.city, nick = l.nickname, abbr = l.abbr;
            UiControls.TextField(_content, l.city, FranchiseLeague.MaxCity, v => city = v, Loc.T("City"));
            UiControls.TextField(_content, l.nickname, FranchiseLeague.MaxNickname, v => nick = v, Loc.T("Nickname"));
            UiControls.TextField(_content, l.abbr, FranchiseLeague.MaxAbbr, v => abbr = v, Loc.T("ABC"));
            Big("SAVE NAME", () =>
            {
                if (!FranchiseLeague.Rename(f, city, nick, abbr)) { Toast("NAME", Loc.T("Pick a different name.")); return; }
                Save();
            }, ButtonStyle.Secondary);

            Heading("INJURIES");
            UiControls.ToggleRow(_content, "INJURIES", l.injuries, on => { l.injuries = on; Save(); });
            Line("Off by default. On: starters can pick up knocks and sit 1-3 games. The PLAYERS' LOUNGE shortens them.", Theme.Muted, 22f, 60f);
            foreach (var p in FranchiseLeague.Injured(f, f.you))
                Line(p.Name + "  ·  " + Loc.T("out") + " " + p.injured, Theme.Pink, 24f, 40f);
        }
    }
}
