using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>One "first time here" card.</summary>
    public sealed class TourCard
    {
        public string Id, Title, Body;
        public TourCard(string id, string title, string body) { Id = id; Title = title; Body = body; }
    }

    /// <summary>
    /// Phase 31 onboarding: a short card the first time each of the newer modes is opened, and one "what's new"
    /// card for players who already had a career before the update. Each shows once (kept in the save's
    /// story-seen list, so it follows the career through iCloud).
    /// </summary>
    public static class Tours
    {
        /// <summary>I269 (21 Nov 2026): a new id so players who saw the last card see this one once.</summary>
        public static readonly TourCard WhatsNew = new TourCard("tour.whats_new_241", "NEW IN RETRO HOOPS",
            "Set up a bucket for a DIME, score 6 straight and you're COOKING, string stops together for LOCKDOWN. Quick Call remembers your matchup (or tap RANDOM MATCHUP), the PLAY menu has a tip a day and your next milestone, and CAREER STATS has RECENT GAMES. Settings ► CALLOUTS turns the chatter down.");
        public static readonly TourCard TwoPhones = new TourCard("tour.two_phones", "TWO PHONES",
            "Each player on their own iPhone, side by side. One phone hosts and picks both teams; the other joins. No internet needed, just Wi-Fi or Bluetooth on.");
        public static readonly TourCard Live = new TourCard("tour.live", "RETRO HOOPS LIVE",
            "Play people anywhere over the internet with your own team. Wins and losses move your Live rating. Free: Game Center finds you an opponent.");
        public static readonly TourCard Couch = new TourCard("tour.couch", "COUCH CUP",
            "A knockout for 2 to 8 friends. Type everyone's name, pick teams, and the bracket says who's up. Pass one phone around, or use 2 PHONES.");
        public static readonly TourCard Watch = new TourCard("tour.watch", "WATCH A GAME",
            "Watch two friends' TWO PHONES game live on this phone. Join any time: you'll catch up from the tip-off. Nothing you do here affects their game.");
        public static readonly TourCard TapesCard = new TourCard("tour.tapes", "GAME TAPES",
            "After a two-phone, Live or watched game, tap SAVE GAME TAPE. Tapes replay the whole game exactly, and you can send one to a friend nearby.");

        /// <summary>15 Oct 2026: marks the first-launch CREATE YOUR PLAYER step as done (see Onboarding.OfferCreatePlayer).</summary>
        public static readonly TourCard MakePlayer = new TourCard("tour.make_player", "CREATE YOUR PLAYER",
            "Build the player you'll take from the street circuit to the league: name, look, outfit and style of play.");

        public static readonly TourCard[] All = { WhatsNew, TwoPhones, Live, Couch, Watch, TapesCard };

        public static bool Seen(CareerSaveData d, TourCard card) => d == null || card == null || d.storySeen.Contains(card.Id);

        public static void MarkSeen(CareerSaveData d, TourCard card) => Story.MarkSeen(d, card?.Id);

        /// <summary>
        /// The "what's new" card is for careers that existed before the update: anyone past the tutorial or with
        /// games played. A brand-new player gets the mode cards as they find each mode instead.
        /// </summary>
        public static bool ShowWhatsNew(CareerSaveData d) =>
            d != null && !Seen(d, WhatsNew) && (d.tutorialDone || d.totals.games > 0);

        /// <summary>A new player never needs "what's new": mark it seen at the start.</summary>
        public static void SkipWhatsNewForNewPlayer(CareerSaveData d)
        {
            if (d != null && !d.tutorialDone && d.totals.games == 0) MarkSeen(d, WhatsNew);
        }
    }
}
