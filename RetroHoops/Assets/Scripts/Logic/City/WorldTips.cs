using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 28 Oct 2026, improvement I37 WORLD TIPS: short one-time tips as you find each new part of BALLER WORLD (the map
    /// and the comic, the minimap, each region, the time trial). One per visit to a district, never the same twice.
    /// </summary>
    public static class WorldTips
    {
        public struct Tip { public string Id, Text; }

        private static readonly Dictionary<string, string> RegionTips = new Dictionary<string, string>
        {
            [WorldRegions.NewYork] = "New York: the Cage, the rooftops, and the NYC STORE in THE AVENUES. Look for ! for side jobs.",
            [WorldRegions.LosAngeles] = "Los Angeles: beach courts, lowriders, and the LA STORE on THE BOULEVARD. Look for ! for side jobs.",
            [WorldRegions.LasVegas] = "Las Vegas: the Stage, the desert lots, and the LV STORE on NEON MILE. Look for ! for side jobs.",
            [WorldRegions.TheBay] = "The Bay: fog, cable cars, and the BAY STORE in CROSSTOWN. Look for ! for side jobs.",
        };

        public static List<Tip> For(CityWorldSaveData w, string district, string region)
        {
            var list = new List<Tip>();
            if (w == null) return list;
            void Offer(string id, string text) { if (list.Count == 0 && !w.tipsShown.Contains(id)) list.Add(new Tip { Id = id, Text = text }); }
            Offer("world", "BALLER WORLD: tap MAP for the whole world. MENU ► THE COMIC has the story.");
            Offer("minimap", "Tap the minimap for the map. MENU ► WORLD SETTINGS turns things on and off.");
            if (RegionTips.TryGetValue(region, out var text)) Offer("region:" + region, text);
            if (w.drives >= 1) Offer("trial", "New every day at YOUR RIDE: the TIME TRIAL. Beat par for a cash prize.");
            return list;
        }

        public static void Mark(CityWorldSaveData w, string id)
        {
            if (w != null && !w.tipsShown.Contains(id)) w.tipsShown.Add(id);
        }

        /// <summary>Every tip's words (for the Spanish check).</summary>
        public static IEnumerable<string> AllText()
        {
            yield return "BALLER WORLD: tap MAP for the whole world. MENU ► THE COMIC has the story.";
            yield return "Tap the minimap for the map. MENU ► WORLD SETTINGS turns things on and off.";
            yield return "New every day at YOUR RIDE: the TIME TRIAL. Beat par for a cash prize.";
            foreach (var t in RegionTips.Values) yield return t;
        }
    }
}
