using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Names and how-tos for the skill moves (shown in Settings ▸ SKILL MOVES). Secret moves show "???" until
    /// your player pulls one off in a game. Then it's saved in <see cref="CareerSaveData.movesFound"/>.
    /// </summary>
    public static class SkillMoveInfo
    {
        public static readonly SkillMoveKind[] All =
            { SkillMoveKind.Spin, SkillMoveKind.Jab, SkillMoveKind.Hesi, SkillMoveKind.StepBack, SkillMoveKind.Shake };

        public static bool IsSecret(SkillMoveKind k) => k == SkillMoveKind.Hesi || k == SkillMoveKind.StepBack || k == SkillMoveKind.Shake;

        public static string Id(SkillMoveKind k) => "move." + k.ToString().ToLowerInvariant();

        /// <summary>Toast when it happens.</summary>
        public static string Name(SkillMoveKind k)
        {
            switch (k)
            {
                case SkillMoveKind.Spin: return "SPIN MOVE";
                case SkillMoveKind.Jab: return "JAB STEP";
                case SkillMoveKind.Hesi: return "HESI";
                case SkillMoveKind.StepBack: return "STEP-BACK";
                case SkillMoveKind.Shake: return "SHAKE";
                default: return "";
            }
        }

        public static string HowTo(SkillMoveKind k)
        {
            switch (k)
            {
                case SkillMoveKind.Spin: return "Swirl the stick half a circle, fast. Spins past your man and keeps the ball safe.";
                case SkillMoveKind.Jab: return "Standing still, flick the stick at the rim and let go. He may bite, then attack or shoot.";
                case SkillMoveKind.Hesi: return "At full speed, let go of the stick for a blink, then push the same way again.";
                case SkillMoveKind.StepBack: return "Driving with a defender close, snap the stick straight back. Then shoot with room.";
                case SkillMoveKind.Shake: return "Wiggle the stick left, right, left, fast. It can break ankles.";
                default: return "";
            }
        }

        /// <summary>Records a move your player pulled off. True the first time a secret move is found.</summary>
        public static bool Discover(CareerSaveData d, SkillMoveKind k)
        {
            if (d == null || k == SkillMoveKind.None) return false;
            if (d.movesFound == null) d.movesFound = new List<string>();
            string id = Id(k);
            if (d.movesFound.Contains(id)) return false;
            d.movesFound.Add(id);
            return IsSecret(k);
        }

        public static bool Found(CareerSaveData d, SkillMoveKind k) => d?.movesFound != null && d.movesFound.Contains(Id(k));

        /// <summary>The SKILL MOVES list: a line per move, secrets hidden until found.</summary>
        public static string ListText(CareerSaveData d)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(Loc.T("With the ball, use the movement stick:")).Append("\n\n");
            sb.Append(Loc.T("LAYUP OFF THE GLASS")).Append(": ")
              .Append(Loc.T("Lay it up from the side and it banks in off the backboard. The glass helps against a contest.")).Append("\n\n");
            int hidden = 0;
            foreach (var k in All)
            {
                if (IsSecret(k) && !Found(d, k)) { hidden++; continue; }
                sb.Append(Loc.T(Name(k))).Append(IsSecret(k) ? " (" + Loc.T("SECRET") + ")" : "").Append(": ").Append(Loc.T(HowTo(k))).Append("\n\n");
            }
            if (hidden > 0)
                sb.Append("??? × ").Append(hidden).Append("  ").Append(Loc.T("Secret moves. Play around with the stick to find them."));
            return sb.ToString().TrimEnd();
        }
    }
}
