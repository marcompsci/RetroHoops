using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>Your street money, streak and milestones.</summary>
    [Serializable]
    public class StreetCashSaveData
    {
        /// <summary>Street cash in the bank (in-game play money; spendable at the Corner Store).</summary>
        public long cash;
        public long earned;
        public int games, wins, streak, bestStreak;
        /// <summary>Games where you scored more than 20 (the secret $20,000).</summary>
        public int bigGames;
        /// <summary>Games you led everyone in scoring.</summary>
        public int topScorer;
        public int bonusSp;
        /// <summary>0..1: how hard the CPUs key on you after your big scoring games (cools off with ordinary games).</summary>
        public float heat;
        public List<string> achievements = new List<string>();
    }

    public sealed class StreetMilestone
    {
        public string Id, Name, Detail;
        public int Sp;
        /// <summary>Hidden until earned (the secret ones show as ???).</summary>
        public bool Secret;
        public Func<StreetCashSaveData, bool> Met;
    }

    /// <summary>What a street game paid.</summary>
    public sealed class StreetPayout
    {
        public long Cash;
        public bool Doubled, BigGame, TopScorer, KeyedOn;
        public int Sp;
        public readonly List<StreetMilestone> Milestones = new List<StreetMilestone>();

        /// <summary>The post-game lines: CASHED OUT first, then the bonuses and milestones.</summary>
        public string Note()
        {
            var lines = new List<string>();
            if (Cash > 0) lines.Add(Loc.T("CASHED OUT") + " " + StreetCash.Money(Cash) + (Doubled ? "  ·  " + Loc.T("3-GAME STREAK: DOUBLED") : ""));
            if (BigGame) lines.Add(Loc.T("20+ POINTS BONUS") + " " + StreetCash.Money(StreetCash.BigGamePay));
            if (Sp > 0) lines.Add("+" + Sp + " " + Loc.T("BONUS SP") + (TopScorer ? "  ·  " + Loc.T("LEADING SCORER") : ""));
            foreach (var m in Milestones) lines.Add(Loc.T("MILESTONE:") + " " + Loc.T(m.Name));
            if (KeyedOn) lines.Add(Loc.T("Word's out. The CPUs are keying on you now."));
            return string.Join("\n", lines);
        }
    }

    /// <summary>
    /// 17 Oct 2026 (Omari): CASHED OUT. Every street game (THE STREETS, THE PARK, the Baller City crews) pays:
    /// - a win cashes out $5,000; on a three-game win streak (or longer) every win doubles to $10,000;
    /// - score more than 20 yourself and there's a secret $20,000 on top;
    /// - bonus SP for playing, winning, and leading everyone in scoring;
    /// - milestones for streaks, big games, and the bank.
    /// But the CPUs catch on: every 20+ game turns up the HEAT, and while it's up the defence keys on you (a tighter
    /// on-ball defender and help that doubles you near the rim). Ordinary games let it cool off. It's play money:
    /// it never touches real money; the Corner Store takes it for gear.
    /// </summary>
    public static class StreetCash
    {
        public const long WinPay = 5000, StreakPay = 10000, BigGamePay = 20000;
        public const int StreakForDouble = 3, BigGameOver = 20;
        public const int PlaySp = 10, WinSp = 20, TopScorerSp = 25;
        public const float HeatPerBigGame = 0.34f, HeatCool = 0.12f;
        /// <summary>How many SP one dollar of street cash is worth at the store (an item's SP price × 100).</summary>
        public const int CashPerSp = 100;

        public static readonly StreetMilestone[] Milestones =
        {
            M("first_cash", "FIRST CHECK", "Win your first street game.", 50, s => s.wins >= 1),
            M("streak3", "HOT HAND", "Win 3 street games in a row.", 75, s => s.bestStreak >= 3),
            M("streak5", "ON FIRE", "Win 5 in a row.", 150, s => s.bestStreak >= 5),
            M("streak10", "UNTOUCHABLE", "Win 10 in a row.", 400, s => s.bestStreak >= 10),
            M("top5", "TOP DOG", "Lead everyone in scoring 5 times.", 120, s => s.topScorer >= 5),
            M("games25", "STREET VET", "Play 25 street games.", 150, s => s.games >= 25),
            M("wins50", "STREET LEGEND", "Win 50 street games.", 500, s => s.wins >= 50),
            M("bank100k", "BANKROLL", "Earn $100,000 on the streets.", 150, s => s.earned >= 100000),
            M("bank500k", "HIGH ROLLER", "Earn $500,000 on the streets.", 400, s => s.earned >= 500000),
            M("bank1m", "MILLIONAIRE", "Earn $1,000,000 on the streets.", 1000, s => s.earned >= 1000000),
            M("big1", "BUCKETS", "Score more than 20 in a street game.", 200, s => s.bigGames >= 1, secret: true),
            M("big3", "THEY KNOW YOUR NAME", "Do it 3 times. They'll be ready for you.", 400, s => s.bigGames >= 3, secret: true),
        };

        private static StreetMilestone M(string id, string name, string detail, int sp, Func<StreetCashSaveData, bool> met, bool secret = false) =>
            new StreetMilestone { Id = id, Name = name, Detail = detail, Sp = sp, Met = met, Secret = secret };

        /// <summary>"$12,500".</summary>
        public static string Money(long amount) => "$" + Math.Max(0, amount).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Games that count as the streets: THE STREETS (pickup, tour, homecourt, city crews) and THE PARK.</summary>
        public static bool Counts(MatchRequest r) => r != null && (r.Mode == GameMode.Streets || Street.FromContext(r.ContextId) != null);

        /// <summary>
        /// Pays out a finished street game. <paramref name="humanPoints"/> is your points; <paramref name="topPoints"/>
        /// the most anyone else on the floor scored. Adds the cash, SP and milestones to the career.
        /// </summary>
        public static StreetPayout ApplyGame(CareerSaveData d, MatchRequest r, bool won, int humanPoints, int topPoints)
        {
            var p = new StreetPayout();
            if (d == null || !Counts(r)) return p;
            var s = d.streetCash ?? (d.streetCash = new StreetCashSaveData());
            s.games++;
            p.Sp = PlaySp;
            if (won)
            {
                s.wins++;
                s.streak++;
                s.bestStreak = Math.Max(s.bestStreak, s.streak);
                p.Doubled = s.streak >= StreakForDouble;
                p.Cash = p.Doubled ? StreakPay : WinPay;
                p.Sp += WinSp;
            }
            else s.streak = 0;

            if (humanPoints > BigGameOver)
            {
                p.BigGame = true;
                p.Cash += BigGamePay;
                s.bigGames++;
                float before = s.heat;
                s.heat = Math.Min(1f, s.heat + HeatPerBigGame);
                p.KeyedOn = before < 0.5f && s.heat >= 0.5f;
            }
            else s.heat = Math.Max(0f, s.heat - HeatCool);

            if (humanPoints > 0 && humanPoints > topPoints)
            {
                p.TopScorer = true;
                s.topScorer++;
                p.Sp += TopScorerSp;
            }

            s.cash += p.Cash;
            s.earned += p.Cash;
            foreach (var m in Milestones)
            {
                if (s.achievements.Contains(m.Id) || !m.Met(s)) continue;
                s.achievements.Add(m.Id);
                p.Milestones.Add(m);
                p.Sp += m.Sp;
            }
            s.bonusSp += p.Sp;
            d.signalPoints += p.Sp;
            return p;
        }

        /// <summary>How hard the CPUs key on you in a street game right now (0 = not at all).</summary>
        public static float KeyOn(CareerSaveData d, MatchRequest r) =>
            d?.streetCash == null || !Counts(r) ? 0f : Math.Max(0f, Math.Min(1f, d.streetCash.heat));

        /// <summary>Street-cash price of a store item.</summary>
        public static long Price(CosmeticDef c) => c == null ? 0 : (long)c.cost * CashPerSp;

        /// <summary>Spends street cash; false (nothing spent) if there isn't enough.</summary>
        public static bool Spend(CareerSaveData d, long amount)
        {
            var s = d?.streetCash;
            if (s == null || amount < 0 || s.cash < amount) return false;
            s.cash -= amount;
            return true;
        }
    }
}
