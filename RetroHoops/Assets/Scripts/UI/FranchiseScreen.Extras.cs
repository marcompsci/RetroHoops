using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// I111–I125 on the Franchise screen: save slots, trophies, the timeline, the scouting report, training camp,
    /// next season's payroll, AUTO OFF-SEASON, team stats and head-to-head, the assistant GM, award races and
    /// SHARE YOUR SEASON. Rules live in <see cref="FranchiseExtras"/>.
    /// </summary>
    public sealed partial class FranchiseScreen
    {
        /// <summary>Start screen: your other franchises (I111).</summary>
        private void SlotsOnStart()
        {
            var parked = FranchiseExtras.Parked(App.Career);
            if (parked.Count == 0) return;
            Heading("YOUR OTHER FRANCHISES");
            for (int i = 0; i < parked.Count; i++)
            {
                int index = i;
                Row(FranchiseExtras.SlotLabel(parked[i], C), Theme.Cream, 70f,
                    ("PLAY", ButtonStyle.Primary, () => { FranchiseExtras.Switch(App.Career, index); _tab = Tab.Home; Save(); }),
                    ("DELETE", ButtonStyle.Ghost, () => UiControls.Dialog("DELETE FRANCHISE?", "This franchise and its history are gone for good.",
                        ("DELETE", ButtonStyle.Primary, () => { FranchiseExtras.DeleteParked(App.Career, index); App.SaveCareer(); Refresh(); }),
                        ("CANCEL", ButtonStyle.Ghost, null))));
            }
        }

        /// <summary>HISTORY: new franchise (parks this one when there's a free slot) and switching (I111).</summary>
        private void SlotsOnHistory(FranchiseSaveData f)
        {
            Heading("SAVE SLOTS");
            Line(Loc.T("Franchises") + ": " + FranchiseExtras.SlotsUsed(App.Career) + "/" + FranchiseExtras.MaxSlots, Theme.Muted, 24f, 40f);
            var parked = FranchiseExtras.Parked(App.Career);
            for (int i = 0; i < parked.Count; i++)
            {
                int index = i;
                Row(FranchiseExtras.SlotLabel(parked[i], C), Theme.Cream, 70f,
                    ("SWITCH", ButtonStyle.Secondary, () => { FranchiseExtras.Switch(App.Career, index); _tab = Tab.Home; Save(); }));
            }
            if (FranchiseExtras.SlotsUsed(App.Career) < FranchiseExtras.MaxSlots)
                Big("START ANOTHER FRANCHISE", () => { FranchiseExtras.Park(App.Career); App.SaveCareer(); Refresh(); }, ButtonStyle.Secondary);
        }

        /// <summary>HOME: the assistant GM (I121) and the award races (I122).</summary>
        private void AssistantAndRaces(FranchiseSaveData f)
        {
            var tips = FranchiseExtras.Tips(f);
            if (tips.Count > 0)
            {
                Heading("ASSISTANT GM");
                foreach (var t in tips) Line("• " + Loc.T(t), Theme.Cyan, 24f, 56f);
            }
            if (f.phase == FranchisePhase.Regular)
            {
                Heading("AWARD RACES");
                foreach (var kind in FranchiseExtras.Races)
                {
                    var top = FranchiseExtras.Race(f, kind);
                    if (top.Count == 0) continue;
                    Line("<b>" + Loc.T(FranchiseLeague.AwardName(kind)) + "</b>  " + string.Join("  ·  ", top.ConvertAll(p => p.Short + " (" + TeamName(p.team) + ")")),
                         Theme.Cream, 24f, 56f);
                }
            }
            if (f.phase == FranchisePhase.Draft || f.phase == FranchisePhase.ReSign || f.phase == FranchisePhase.FreeAgency || f.phase == FranchisePhase.Preseason)
                Big("AUTO OFF-SEASON", () =>
                {
                    var log = FranchiseExtras.AutoOffSeason(f, C);
                    Save();
                    Toast("AUTO OFF-SEASON", log.Count > 0 ? string.Join("\n", log.GetRange(0, Mathf.Min(12, log.Count))) : Loc.T("Nothing to do."));
                }, ButtonStyle.Ghost);
        }

        /// <summary>Game day: the scouting report (I114).</summary>
        private void ScoutingReport(FranchiseSaveData f)
        {
            var lines = FranchiseExtras.ScoutingReport(f, C);
            if (lines.Count == 0) return;
            Heading("SCOUTING REPORT");
            foreach (var l in lines) Line(l, Theme.Cream, 24f, 44f);
        }

        /// <summary>ROSTER: next season's money (I117).</summary>
        private void PayrollProjection(FranchiseSaveData f)
        {
            int next = FranchiseExtras.NextSeasonPayroll(f);
            Line(Loc.T("NEXT SEASON") + ": " + Franchise.Money(next) + " " + Loc.T("committed") + "  ·  " + Franchise.Money(Franchise.Cap - next) + " " + Loc.T("room"),
                 Theme.Muted, 24f, 40f);
        }

        /// <summary>LEAGUE: your team's numbers and head-to-head (I119, I120).</summary>
        private void TeamNumbers(FranchiseSaveData f)
        {
            var s = FranchiseExtras.TeamStats(f, f.you);
            if (s.pfRank > 0)
            {
                Heading("YOUR TEAM");
                Line(Loc.T("SCORING") + " " + s.pf.ToString("0.0") + " (#" + s.pfRank + ")  ·  " + Loc.T("ALLOWED") + " " + s.pa.ToString("0.0") + " (#" + s.paRank + ")",
                     Theme.Cream, 26f, 44f);
            }
            var parts = new System.Collections.Generic.List<string>();
            for (int t = 0; t < f.teams.Count; t++)
            {
                if (t == f.you) continue;
                var h = FranchiseExtras.HeadToHead(f, t);
                if (h.wins + h.losses > 0) parts.Add(TeamName(t) + " " + h.wins + "-" + h.losses);
            }
            if (parts.Count > 0)
            {
                Heading("ALL-TIME HEAD TO HEAD");
                Line(string.Join("  ·  ", parts), Theme.Cream, 24f, 70f);
            }
        }

        /// <summary>HISTORY: trophies, the dynasty streak, the timeline, and sharing (I112, I113, I123, I124).</summary>
        private void TrophiesAndTimeline(FranchiseSaveData f)
        {
            var l = FranchiseLeague.Of(f);
            FranchiseExtras.CheckTrophies(f);
            if (l.bestTitleStreak >= 2) Line(Loc.T("LONGEST TITLE RUN") + ": " + l.bestTitleStreak, Theme.Gold, 26f, 44f);
            Heading("TROPHIES");
            foreach (var t in FranchiseExtras.Trophies)
            {
                var trophy = t;
                bool earned = FranchiseExtras.Earned(f, t), claimed = FranchiseExtras.Claimed(f, t);
                string text = "<b>" + Loc.T(t.Name) + "</b>  " + t.Sp + " SP\n<size=22><color=#8D99AE>" + Loc.T(t.Detail) + "</color></size>";
                Row(text, earned ? Theme.Gold : Theme.Muted, 80f,
                    (claimed ? "CLAIMED" : earned ? "CLAIM" : "LOCKED", earned && !claimed ? ButtonStyle.Primary : ButtonStyle.Ghost,
                     earned && !claimed ? () => { FranchiseExtras.Claim(App.Career, f, trophy); Audio.AudioManager.Play(SfxId.Coin, 0.7f); Save(); } : (System.Action)null));
            }
            if (l.timeline.Count > 0)
            {
                Heading("TIMELINE");
                for (int i = l.timeline.Count - 1; i >= 0; i--) Line(l.timeline[i], Theme.Cream, 24f, 56f);
            }
            Big("SHARE YOUR SEASON", () => ShareSeason(f), ButtonStyle.Secondary);
        }

        private void ShareSeason(FranchiseSaveData f)
        {
            try
            {
                string text = FranchiseExtras.ShareText(f, C);
                string path = System.IO.Path.Combine(Application.temporaryCachePath, "retro-hoops-franchise.txt");
                System.IO.File.WriteAllText(path, text);
                Share.File(path, text);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Franchise] Couldn't share: " + e.Message);
            }
        }

        /// <summary>A card button for training camp (I116), when it's open.</summary>
        private static (string, ButtonStyle, System.Action)? CampButton(FranchiseSaveData f, FrPlayer p, System.Action after)
        {
            if (FranchiseExtras.CannotCamp(f, p) != null) return null;
            return ("TRAINING CAMP", ButtonStyle.Primary, () => { FranchiseExtras.Camp(f, p.id); after(); });
        }
    }
}
