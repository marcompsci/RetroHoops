using System;
using System.Text;

namespace CallerRetroBall.Logic
{
    /// <summary>The beta tester's short profile (TestFlight copies only). Stays on the phone unless they send it.</summary>
    public sealed class BetaProfileData
    {
        public string name = "";
        /// <summary>Index into <see cref="BetaFeedback.HoopsGamesOptions"/> (-1 = not answered).</summary>
        public int hoopsGames = -1;
        /// <summary>Index into <see cref="BetaFeedback.HoopOptions"/> (-1 = not answered).</summary>
        public int hoops = -1;
        /// <summary>The profile screen was shown (filled in or skipped).</summary>
        public bool asked;
        /// <summary>Times SEND FEEDBACK was used.</summary>
        public int sent;
    }

    /// <summary>
    /// BETA FEEDBACK (12 Oct 2026). Apple doesn't let an app post into TestFlight feedback itself, so SEND FEEDBACK puts a
    /// short report (the tester's profile, build, phone and game stats) on the clipboard and shows how to send it with
    /// TestFlight's own Share Beta Feedback. Nothing is sent by the game.
    /// </summary>
    public static class BetaFeedback
    {
        public const int MaxName = 24;
        public static readonly string[] HoopsGamesOptions = { "NEVER", "SOMETIMES", "A LOT" };
        public static readonly string[] HoopOptions = { "NO", "FOR FUN", "ON A TEAM" };

        public static string Choice(string[] options, int i) => i >= 0 && i < options.Length ? options[i] : "-";

        /// <summary>The text that goes on the clipboard for the tester to paste into TestFlight feedback.</summary>
        public static string Report(CareerSaveData d, string versionAndBuild, string device, string os, DateTime utcNow)
        {
            var b = d?.beta ?? new BetaProfileData();
            var t = d?.totals ?? new CareerTotals();
            var sb = new StringBuilder();
            sb.Append("RETRO HOOPS BETA ").Append(versionAndBuild).Append('\n');
            sb.Append("Tester: ").Append(string.IsNullOrWhiteSpace(b.name) ? "(no name)" : b.name.Trim()).Append('\n');
            sb.Append("Plays basketball games: ").Append(Choice(HoopsGamesOptions, b.hoopsGames))
              .Append(" | Hoops in real life: ").Append(Choice(HoopOptions, b.hoops)).Append('\n');
            sb.Append("Phone: ").Append(device ?? "?").Append(" | ").Append(os ?? "?").Append('\n');
            sb.Append("Games: ").Append(t.games).Append(" (W ").Append(t.wins).Append(" L ").Append(t.losses).Append(")")
              .Append(" | Points: ").Append(t.points).Append(" | 2P games: ").Append(t.versusGames).Append('\n');
            if (d != null)
            {
                sb.Append("Tutorial done: ").Append(d.tutorialDone ? "yes" : "no")
                  .Append(" | Pro: ").Append(d.proOwned || d.proGranted ? "yes" : "no")
                  .Append(" | Live games: ").Append(d.live != null ? d.live.games : 0)
                  .Append(" | Difficulty: ").Append(d.settings?.difficultyId ?? "-").Append('\n');
            }
            sb.Append("Sent: ").Append(utcNow.ToString("yyyy-MM-dd HH:mm")).Append(" UTC\n");
            sb.Append("---\nWhat happened / what you think:\n");
            return sb.ToString();
        }
    }
}
