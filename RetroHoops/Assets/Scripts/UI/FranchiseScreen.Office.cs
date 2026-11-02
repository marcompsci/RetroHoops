using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// I61–I70 FRONT OFFICE: the post-game huddle, press room and locker-room cards on HOME, and the OFFICE tab
    /// (credits, fans, facilities, the room's mood). Every rule lives in <see cref="FrontOffice"/>.
    /// </summary>
    public sealed partial class FranchiseScreen
    {
        private static string MoodTag(FrPlayer p)
        {
            int m = FrontOffice.Morale(p);
            string color = m >= 65 ? "#7BE0AD" : m >= 45 ? "#8D99AE" : "#FF6B8B";
            return "<color=" + color + ">" + Loc.T(FrontOffice.MoodName(m)) + "</color>";
        }

        private void OfficeCards(FranchiseSaveData f)
        {
            var o = FrontOffice.Of(f);
            if (!string.IsNullOrEmpty(o.note)) Line(o.note, Theme.Cyan, 24f, 44f);

            // I79: last season's front-office review, through the off-season.
            if (f.phase != FranchisePhase.Regular && f.phase != FranchisePhase.Playoffs && o.review != null && o.review.Count > 0)
            {
                Heading("SEASON REVIEW");
                foreach (var l in o.review) Line(l, Theme.Cream, 26f, 40f);
                if (o.rival >= 0) Line(Loc.T("RIVAL") + "  " + (Team(o.rival)?.FullName.ToUpperInvariant() ?? "?"), Theme.Pink, 26f, 40f);
            }

            // I74: what the owner wants this season.
            if ((f.phase == FranchisePhase.Regular || f.phase == FranchisePhase.Playoffs) && o.goal >= 0)
                Line(Loc.T("OWNER'S GOAL") + ": <color=#FFD166>" + Loc.T(FrontOffice.GoalText(f)) + "</color>", Theme.Cream, 26f, 44f);

            // I75: a trade request.
            var asker = o.tradeAsk >= 0 ? Franchise.Player(f, o.tradeAsk) : null;
            if (asker != null && asker.team == f.you)
            {
                Heading("TRADE REQUEST");
                Line(asker.Name + " " + Loc.T("wants out. Every game he stays, the room gets a little worse."), Theme.Pink, 26f, 70f);
                bool canTalk = !o.talked && o.credits >= FrontOffice.TalkCost;
                Row(Loc.T("Once a season") + ", " + FrontOffice.TalkCost + " " + Loc.T("credits"), canTalk ? Theme.Muted : Theme.Pink, 70f,
                    ("TALK IT OUT", ButtonStyle.Secondary, canTalk ? () => { FrontOffice.TalkItOut(f); Save(); } : (System.Action)null),
                    ("TRADE TAB", ButtonStyle.Ghost, () => Show(Tab.Trade)));
            }

            if (o.huddle)
            {
                Heading("POST-GAME HUDDLE");
                Line("Who gets the credit for that one? Pick one before the next game.", Theme.Muted, 24f, 44f);
                for (int i = 0; i < FrontOffice.HuddleNames.Length; i++)
                {
                    var choice = (HuddleChoice)i;
                    Row(Loc.T(FrontOffice.HuddleBlurbs[i]), Theme.Cream, 70f,
                        (FrontOffice.HuddleNames[i], ButtonStyle.Secondary, () => { FrontOffice.Huddle(f, choice); Save(); }));
                }
            }

            if (o.press >= 0)
            {
                Heading("PRESS ROOM");
                Line(FrontOffice.PressText(f), Theme.Cream, 28f, 80f);
                var q = FrontOffice.Questions[o.press];
                for (int i = 0; i < q.answers.Length; i++)
                {
                    int a = i;
                    Row(Loc.T(FrontOffice.Effects(q.effects[i, 0], q.effects[i, 1], q.effects[i, 2], q.effects[i, 3])), Theme.Muted, 70f,
                        (q.answers[i], ButtonStyle.Secondary, () => { FrontOffice.AnswerPress(f, a); Save(); }));
                }
                Row("", Theme.Muted, 60f, ("NO COMMENT", ButtonStyle.Ghost, () => { FrontOffice.SkipPress(f); Save(); }));
            }

            if (o.locker >= 0)
            {
                Heading("LOCKER ROOM");
                Line(FrontOffice.LockerText(f), Theme.Cream, 28f, 80f);
                var e = FrontOffice.Events[o.locker];
                for (int i = 0; i < e.choices.Length; i++)
                {
                    int c = i;
                    bool afford = e.effects[i, 0] >= 0 || o.credits >= -e.effects[i, 0];
                    Row(Loc.T(FrontOffice.Effects(e.effects[i, 0], e.effects[i, 1], e.effects[i, 2], e.effects[i, 3])), afford ? Theme.Muted : Theme.Pink, 70f,
                        (e.choices[i], ButtonStyle.Secondary, afford ? () => { FrontOffice.DecideLocker(f, c); Save(); } : (System.Action)null));
                }
            }
        }

        private void BuildOffice()
        {
            var f = F;
            var o = FrontOffice.Of(f);
            Heading("FRONT OFFICE");
            Line(Loc.T("CREDITS") + "  <color=#FFD166>" + o.credits + "</color>     " + Loc.T("FANS") + "  <color=#FFD166>" + FrontOffice.Fans(f) + "</color>     "
                 + Loc.T("TEAM MOOD") + "  <color=#FFD166>" + Mathf.RoundToInt(FrontOffice.TeamMorale(f, f.you)) + "</color>",
                 Theme.Cream, 30f, 56f, TextAlignmentOptions.Center, true);
            Line("Credits come from every game (more for wins), the owner in the huddle, and the press. Nothing here costs real money.",
                 Theme.Muted, 24f, 70f);
            int heat = FrontOffice.Heat(f);
            if (heat > 0)
                Line(Loc.T("LEAGUE HEAT") + " " + heat + "/4: " + Loc.T("every title makes the other clubs a little better prepared."), Theme.Pink, 24f, 60f);

            // I71: how long your games are.
            UiControls.ChoiceRow(_content, "GAME LENGTH", FrontOffice.LengthNames, Mathf.Clamp(o.length, 0, 2), i => { o.length = i; Save(); });
            Line("QUICK is two minutes, LONG is six. Simulated games aren't affected.", Theme.Muted, 22f, 40f);

            // I73, I77
            if (o.rival >= 0) Line(Loc.T("RIVAL") + "  " + (Team(o.rival)?.FullName.ToUpperInvariant() ?? "?") + "  ·  " + Loc.T("beat them for extra credits and fans."), Theme.Pink, 24f, 50f);
            if (FrontOffice.IsSellout(f)) Line("SELL-OUT CROWDS: +1 credit every home game, and the building is worth a little extra.", Theme.Gold, 24f, 50f);

            Heading("FACILITIES");
            for (int i = 0; i < FrontOffice.FacilityNames.Length; i++)
            {
                var which = (Facility)i;
                int lv = FrontOffice.Level(f, which);
                int cost = FrontOffice.UpgradeCost(f, which);
                string text = "<b>" + Loc.T(FrontOffice.FacilityNames[i]) + "</b>  " + Loc.T("LEVEL") + " " + lv + "/" + FrontOffice.MaxLevel
                              + "\n<size=22><color=#8D99AE>" + Loc.T(FrontOffice.FacilityBlurbs[i]) + "</color></size>";
                bool can = cost > 0 && o.credits >= cost;
                Row(text, Theme.Cream, 90f,
                    (cost > 0 ? "UPGRADE " + cost : "MAXED", can ? ButtonStyle.Primary : ButtonStyle.Ghost,
                     can ? () => { FrontOffice.Upgrade(f, which); Save(); } : (System.Action)null));
            }

            // I72: the club record book.
            Heading("CLUB RECORDS");
            foreach (var l in FrontOffice.RecordLines(f)) Line(l, Theme.Cream, 26f, 40f);

            // I76: the captain.
            Heading("CAPTAIN");
            var cap = FrontOffice.Captain(f);
            Line(cap != null ? cap.Name + "  " + MoodTag(cap) : Loc.T("No captain yet."), Theme.Cream, 26f, 40f);
            Line("A happy captain takes the sting out of losses for the whole team.", Theme.Muted, 22f, 40f);

            Heading("THE ROOM");
            Line("Winning, starting and the huddle lift moods; losing and the deep bench drag them down. A happy room shoots a touch better and re-signs for less. An unhappy player wants paying to stay.",
                 Theme.Muted, 24f, 100f);
            foreach (var p in Franchise.Roster(f, f.you))
            {
                int id = p.id;
                bool isCap = cap != null && cap.id == id;
                Row(p.Short + "  <color=#FFD166>" + FrontOffice.Morale(p) + "</color>  " + MoodTag(p), Theme.Cream, 60f,
                    (isCap ? "CAPTAIN" : "MAKE CAPTAIN", isCap ? ButtonStyle.Ghost : ButtonStyle.Secondary,
                     isCap ? (System.Action)null : () => { FrontOffice.SetCaptain(f, id); Save(); }));
            }
        }
    }
}
