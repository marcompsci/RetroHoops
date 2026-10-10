using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>What your Franchise team works on between seasons (Phase 33).</summary>
    public enum TrainingFocus { Balanced = 0, Shooting = 1, Defense = 2, Playmaking = 3, Athleticism = 4 }

    /// <summary>A trade another GM brings to you (Phase 33).</summary>
    [Serializable]
    public class FrOffer
    {
        public int team = -1;
        /// <summary>Your players they want.</summary>
        public List<int> want = new List<int>();
        /// <summary>Their players they'd send.</summary>
        public List<int> send = new List<int>();
        public int week;
        public string pitch = "";
    }

    /// <summary>
    /// Phase 33, season depth for Franchise: the other GMs call with trade offers during the season, your team's
    /// training focus shapes how your young players develop, and each off-season ends with a development report.
    /// </summary>
    public static class FranchiseDepth
    {
        /// <summary>Chance a GM calls in a given week (before the deadline).</summary>
        public const float OfferChance = 0.4f;
        /// <summary>Training only helps players this age or younger who haven't hit their potential.</summary>
        public const int FocusMaxAge = 27;

        public static readonly string[] FocusNames = { "BALANCED", "SHOOTING", "DEFENSE", "PLAYMAKING", "ATHLETICISM" };

        public static AttributeType[] FocusAttributes(TrainingFocus focus)
        {
            switch (focus)
            {
                case TrainingFocus.Shooting: return new[] { AttributeType.Shooting, AttributeType.Finishing };
                case TrainingFocus.Defense: return new[] { AttributeType.Defense, AttributeType.Rebounding };
                case TrainingFocus.Playmaking: return new[] { AttributeType.Playmaking, AttributeType.Clutch };
                case TrainingFocus.Athleticism: return new[] { AttributeType.Speed, AttributeType.Stamina };
                default: return new AttributeType[0];
            }
        }

        /// <summary>
        /// The extra off-season growth from your training focus: +2 in the two focus attributes for a young player
        /// still below his potential (Balanced gives no extra, but no player is left out either).
        /// </summary>
        public static AttributeSet ApplyFocus(FrPlayer p, TrainingFocus focus)
        {
            var a = p.attrs;
            if (p.age > FocusMaxAge || p.Overall >= p.potential) return a;
            foreach (var t in FocusAttributes(focus)) a = a.With(t, RatingScale.Clamp(a.Get(t) + 2));
            return a;
        }

        /// <summary>Called by Franchise after each regular-season week: maybe a GM calls with an offer.</summary>
        public static void AfterWeek(FranchiseSaveData f, ContentCatalog c)
        {
            if (f == null || !f.active) return;
            if (f.offer != null && (!Franchise.TradesOpen(f) || !StillValid(f, f.offer))) f.offer = null;
            if (f.phase != FranchisePhase.Regular || !Franchise.TradesOpen(f)) return;
            int week = f.season.currentWeek;
            if (f.offerWeek == week) return;
            f.offerWeek = week;
            var rng = new SeededRandom(StableHash.Of("fr:offer:" + f.seed + ":" + f.year + ":" + week));
            if (!rng.Chance(OfferChance)) return;
            f.offer = MakeOffer(f, c, rng, week);
        }

        /// <summary>
        /// A deal the other GM would really make (it passes <see cref="Franchise.Evaluate"/>): they ask for one of your
        /// players and offer the player of theirs that is the fairest swap for you.
        /// </summary>
        public static FrOffer MakeOffer(FranchiseSaveData f, ContentCatalog c, SeededRandom rng, int week)
        {
            var mine = Franchise.Roster(f, f.you);
            if (mine.Count == 0) return null;
            int start = rng.Range(0, f.teams.Count);
            for (int k = 0; k < f.teams.Count; k++)
            {
                int other = (start + k) % f.teams.Count;
                if (other == f.you) continue;
                // They like young players with upside and good value; never your one best player.
                var targets = new List<FrPlayer>(mine);
                targets.RemoveAll(x => x.created); // your created players aren't on the table
                if (targets.Count == 0) continue;
                targets.Sort((x, y) => Franchise.Value(y).CompareTo(Franchise.Value(x)));
                if (targets.Count > 1) targets.RemoveAt(0);
                int pickTarget = Math.Min(targets.Count - 1, rng.Range(0, Math.Max(1, Math.Min(3, targets.Count))));
                var want = targets[pickTarget];
                FrPlayer best = null;
                float bestGap = float.MaxValue;
                foreach (var theirs in Franchise.Roster(f, other))
                {
                    var v = Franchise.Evaluate(f, other, new List<int> { want.id }, new List<int> { theirs.id });
                    if (!v.Accepted) continue;
                    float gap = v.TheyGet - v.TheyGive; // how much they win by: smaller is fairer for you
                    if (gap < bestGap) { bestGap = gap; best = theirs; }
                }
                if (best == null) continue;
                string club = c?.Team(f.teams[other].baseId)?.FullName ?? ("Team " + (other + 1));
                return new FrOffer
                {
                    team = other, want = new List<int> { want.id }, send = new List<int> { best.id }, week = week,
                    pitch = club + " call: " + best.Name + " (" + best.Overall + ", age " + best.age + ") for your " + want.Name + " (" + want.Overall + ", age " + want.age + ").",
                };
            }
            return null;
        }

        public static bool StillValid(FranchiseSaveData f, FrOffer o) =>
            o != null && o.team >= 0 && Franchise.Evaluate(f, o.team, o.want, o.send).Accepted;

        /// <summary>Takes the offer (true if the trade went through).</summary>
        public static bool Accept(FranchiseSaveData f)
        {
            var o = f?.offer;
            if (o == null) return false;
            f.offer = null;
            return Franchise.Trade(f, o.team, o.want, o.send);
        }

        public static void Decline(FranchiseSaveData f)
        {
            if (f != null) f.offer = null;
        }
    }
}
