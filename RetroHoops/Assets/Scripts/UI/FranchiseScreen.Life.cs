using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// I96–I110 on the Franchise screen: WHAT'S NEW, the job market when you're let go, HEADLINES, the commissioner,
    /// the record book, retired numbers and the hometown-discount ask. Rules live in <see cref="FranchiseLife"/>.
    /// </summary>
    public sealed partial class FranchiseScreen
    {
        /// <summary>Top of HOME: what's new, a job hunt, and the headlines.</summary>
        private void LifeCards(FranchiseSaveData f)
        {
            var l = FranchiseLeague.Of(f);
            if (FranchiseLife.ShowWhatsNew(f))
            {
                Heading("WHAT'S NEW IN FRANCHISE");
                foreach (var s in FranchiseLife.WhatsNew) Line("• " + Loc.T(s), Theme.Cream, 24f, 50f);
                Big("GOT IT", () => { FranchiseLife.DismissWhatsNew(f); Save(); }, ButtonStyle.Ghost);
            }

            if (l.fired)
            {
                Heading("YOU'RE OUT OF A JOB");
                Line("The owner made a change after three missed goals. These clubs want you. Your legacy, credits and history come with you.", Theme.Pink, 26f, 90f);
                foreach (int t in l.jobOffers)
                {
                    int team = t;
                    Row((Team(t)?.FullName ?? "?") + "  ·  " + Record(t), Theme.Cream, 70f,
                        ("TAKE THE JOB", ButtonStyle.Primary, () => { FranchiseLife.TakeJob(f, team); Save(); }));
                }
            }
            else if (l.missedGoals == 2)
                Line(Loc.T("HOT SEAT") + ": " + Loc.T("miss the owner's goal again and you're out."), Theme.Pink, 26f, 50f);

            var news = FranchiseLife.Latest(f, 5);
            if (news.Count > 0)
            {
                Heading("HEADLINES");
                foreach (var n in news) Line(n, Theme.Cream, 24f, 44f);
            }
        }

        /// <summary>LEAGUE: the record book.</summary>
        private void RecordBook(FranchiseSaveData f)
        {
            var records = FranchiseLeague.Of(f).records;
            if (records.Count == 0) return;
            Heading("RECORD BOOK");
            foreach (var kind in FranchiseLife.RecordKinds)
            {
                var r = records.Find(x => x.kind == kind);
                if (r == null) continue;
                string v = kind == "WINS" ? (r.value / 10).ToString() : (r.value / 10f).ToString("0.0");
                Line("<b>" + kind + "</b>  " + v + "  ·  " + r.name + "  ·  " + Loc.T("YEAR") + " " + r.year, Theme.Cream, 24f, 44f);
            }
        }

        /// <summary>HISTORY: banners in your arena.</summary>
        private void Banners(FranchiseSaveData f)
        {
            var banners = FranchiseLeague.Of(f).banners;
            if (banners.Count == 0) return;
            Heading("RETIRED NUMBERS");
            foreach (var b in banners) Line("<b>#" + b.number + "</b>  " + b.name + "  ·  " + b.line, Theme.Gold, 26f, 44f);
        }

        /// <summary>OFFICE: the commissioner.</summary>
        private void Commissioner(FranchiseSaveData f)
        {
            var l = FranchiseLeague.Of(f);
            Heading("COMMISSIONER");
            Line("League rules for your franchise only. Edits show in your history.", Theme.Muted, 22f, 40f);
            UiControls.ToggleRow(_content, "SALARY CAP", !l.capOff, on => { l.capOff = !on; Save(); });
            UiControls.ChoiceRow(_content, "TRADES", FranchiseLife.TradeLevels, Mathf.Clamp(l.tradeLevel, 0, 2), i => { l.tradeLevel = i; Save(); });
            UiControls.ToggleRow(_content, "GM CALLS", !l.offersOff, on => { l.offersOff = !on; Save(); });
            if (l.commishEdits > 0) Line(Loc.T("Rating edits made") + ": " + l.commishEdits, Theme.Muted, 22f, 40f);
            foreach (var p in Franchise.Roster(f, f.you))
            {
                int id = p.id;
                Row(p.Short + "  <color=#FFD166>" + p.Overall + "</color>", Theme.Cream, 60f,
                    ("-", ButtonStyle.Ghost, () => { FranchiseLife.EditRatings(f, id, -1); Save(); }),
                    ("+", ButtonStyle.Ghost, () => { FranchiseLife.EditRatings(f, id, 1); Save(); }));
            }
        }

        /// <summary>MARKET at re-signing: ask for a hometown discount.</summary>
        private void AskDiscountButton(FranchiseSaveData f, FrPlayer p, System.Collections.Generic.List<(string, ButtonStyle, System.Action)> buttons)
        {
            if (FranchiseLeague.Of(f).asked.Contains(p.id)) return;
            int id = p.id;
            buttons.Add(("ASK FOR A DISCOUNT", ButtonStyle.Ghost, () =>
            {
                FranchiseLife.AskDiscount(f, id, out string result);
                Toast("CONTRACT TALKS", result);
                Save();
            }));
        }
    }
}
