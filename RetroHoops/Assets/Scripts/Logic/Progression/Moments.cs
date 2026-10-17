using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum MomentKind { GameWinner = 0, BuzzerBeater = 1, BigGame = 2, PerfectNight = 3, Lockdown = 4, Blowout = 5, /** I15 */ Comeback = 6 }

    /// <summary>One moment worth keeping (newest first in <see cref="CareerSaveData.moments"/>).</summary>
    [Serializable]
    public class MomentEntry
    {
        public int day;
        public MomentKind kind;
        public GameMode mode;
        public string opponentId;
        public int scoreFor, scoreAgainst;
        /// <summary>The number that made it (points, steals, margin; 0 for a game-winner).</summary>
        public int value;
    }

    /// <summary>
    /// 16 Oct 2026 improvement phase I3 LEGACY: the MOMENTS book. Players in App Store reviews ask for records and
    /// history that last (career highs that save, buzzer-beaters remembered). The one-game records already keep the
    /// numbers; this keeps the nights themselves: your game-winners and buzzer-beaters, 20-point games, perfect
    /// shooting nights, lockdown games and big wins, with who it was against and the score.
    /// </summary>
    public static class MomentsBook
    {
        public const int Keep = 40;
        public const int BigGamePoints = 20;
        public const int PerfectAttempts = 5;
        public const int LockdownSteals = 4;
        public const int BlowoutMargin = 12;

        /// <summary>The moments in a finished game. <paramref name="yourWinner"/>: you hit the shot that won it.</summary>
        /// <summary>I15: a win from this far down is a comeback.</summary>
        public const int ComebackFrom = 6;

        /// <param name="maxDeficit">I15: the most your team trailed by in this game.</param>
        public static List<MomentEntry> FromGame(MatchSummary s, bool yourWinner, bool buzzer, int maxDeficit = 0)
        {
            var list = new List<MomentEntry>();
            if (s == null || s.mode == GameMode.Practice || s.mode == GameMode.Tutorial || s.mode == GameMode.Versus || s.mode == GameMode.Demo)
                return list;
            var line = s.HumanLine?.stats;
            string opp = s.humanTeam == 0 ? s.teamBId : s.teamAId;
            MomentEntry M(MomentKind k, int v) => new MomentEntry
            {
                day = s.day, kind = k, mode = s.mode, opponentId = opp, scoreFor = s.HumanScore, scoreAgainst = s.OpponentScore, value = v,
            };
            if (yourWinner && s.HumanWon) list.Add(M(buzzer ? MomentKind.BuzzerBeater : MomentKind.GameWinner, 0));
            if (line != null)
            {
                // Full Court counts 2s and 3s, so its 20 isn't the half-court 20: it needs 30 there.
                int big = s.fullCourt || s.mode == GameMode.FullCourt ? 30 : BigGamePoints;
                if (line.points >= big) list.Add(M(MomentKind.BigGame, line.points));
                if (line.fieldGoalsAttempted >= PerfectAttempts && line.fieldGoalsMade == line.fieldGoalsAttempted)
                    list.Add(M(MomentKind.PerfectNight, line.fieldGoalsMade));
                if (line.steals >= LockdownSteals) list.Add(M(MomentKind.Lockdown, line.steals));
            }
            if (s.HumanWon && s.Margin >= BlowoutMargin && s.mode != GameMode.Clutch) list.Add(M(MomentKind.Blowout, s.Margin));
            if (Comebacks.IsComeback(s.HumanWon, maxDeficit, s.fullCourt || s.mode == GameMode.FullCourt)) list.Add(M(MomentKind.Comeback, maxDeficit));
            return list;
        }

        /// <summary>Adds a game's moments to the front of the book, keeping the newest <see cref="Keep"/>.</summary>
        public static void Add(CareerSaveData d, List<MomentEntry> moments)
        {
            if (d == null || moments == null || moments.Count == 0) return;
            if (d.moments == null) d.moments = new List<MomentEntry>();
            for (int i = moments.Count - 1; i >= 0; i--) d.moments.Insert(0, moments[i]);
            while (d.moments.Count > Keep) d.moments.RemoveAt(d.moments.Count - 1);
        }

        /// <summary>
        /// I6: gear earned by playing, not bought: your first buzzer-beater gets the Sport Goggles, three game-winners
        /// the Vet Package, five big games Full Compression. Returns what this call newly gave you (already owned = skipped).
        /// </summary>
        public static readonly (MomentKind[] kinds, int count, string itemId)[] Rewards =
        {
            (new[] { MomentKind.BuzzerBeater }, 1, "cosmetic.gear.goggles"),
            (new[] { MomentKind.GameWinner, MomentKind.BuzzerBeater }, 3, "cosmetic.gear.vet_package"),
            (new[] { MomentKind.BigGame }, 5, "cosmetic.gear.full_compression"),
        };

        public static List<CosmeticDef> GrantRewards(CareerSaveData d, ContentCatalog c)
        {
            var got = new List<CosmeticDef>();
            if (d == null || c == null) return got;
            foreach (var r in Rewards)
            {
                int n = 0;
                foreach (var k in r.kinds) n += Count(d, k);
                if (n < r.count || d.ownedCosmetics.Contains(r.itemId)) continue;
                var item = c.Find(c.Cosmetics, r.itemId);
                if (item == null) continue;
                d.ownedCosmetics.Add(r.itemId);
                got.Add(item);
            }
            return got;
        }

        public static int Count(CareerSaveData d, MomentKind k)
        {
            int n = 0;
            if (d?.moments != null) foreach (var m in d.moments) if (m.kind == k) n++;
            return n;
        }

        public static string Title(MomentKind k)
        {
            switch (k)
            {
                case MomentKind.GameWinner: return "GAME WINNER";
                case MomentKind.BuzzerBeater: return "BUZZER BEATER";
                case MomentKind.BigGame: return "BIG GAME";
                case MomentKind.PerfectNight: return "PERFECT NIGHT";
                case MomentKind.Lockdown: return "LOCKDOWN";
                case MomentKind.Comeback: return "COMEBACK";
                default: return "BLOWOUT";
            }
        }

        /// <summary>"BUZZER BEATER  ·  vs SPL 21-19" / "BIG GAME: 23 PTS  ·  vs SPL 21-15".</summary>
        public static string Line(MomentEntry m, string opponentAbbr)
        {
            if (m == null) return "";
            string what = Loc.T(Title(m.kind));
            switch (m.kind)
            {
                case MomentKind.BigGame: what += ": " + m.value + " " + Loc.T("PTS"); break;
                case MomentKind.PerfectNight: what += ": " + m.value + "/" + m.value + " " + Loc.T("FG"); break;
                case MomentKind.Lockdown: what += ": " + m.value + " " + Loc.T("STL"); break;
                case MomentKind.Blowout: what += ": +" + m.value; break;
                case MomentKind.Comeback: what += ": " + Loc.T("DOWN") + " " + m.value; break;
            }
            return what + "  ·  vs " + (string.IsNullOrEmpty(opponentAbbr) ? "?" : opponentAbbr) + " " + m.scoreFor + "-" + m.scoreAgainst;
        }
    }

    [Flags]
    public enum GearParts { None = 0, ArmSleeves = 1, LegSleeves = 2, Ink = 4, Goggles = 8 }

    /// <summary>
    /// I3 GEAR: shooter sleeves, knee sleeves, an ink sleeve and sport goggles for your player (the CORNER STORE and the
    /// Locker Room's GEAR slot sell and equip them; looks only, never ratings). Each gear item's parts come from its id.
    /// </summary>
    public static class Gear
    {
        public const string NoneId = "cosmetic.gear.none";

        public static GearParts Parts(string cosmeticId)
        {
            switch (cosmeticId)
            {
                case "cosmetic.gear.shooter_sleeves":
                case "cosmetic.gear.cloud_sleeves": return GearParts.ArmSleeves;
                case "cosmetic.gear.knee_sleeves": return GearParts.LegSleeves;
                case "cosmetic.gear.full_compression": return GearParts.ArmSleeves | GearParts.LegSleeves;
                case "cosmetic.gear.ink_sleeve": return GearParts.Ink;
                case "cosmetic.gear.goggles": return GearParts.Goggles;
                case "cosmetic.gear.vet_package": return GearParts.Goggles | GearParts.LegSleeves;
                default: return GearParts.None;
            }
        }
    }
}
