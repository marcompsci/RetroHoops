using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 15 Oct 2026 (Omari): what your Rise player wears, so CREATE MY PLAYER can show the real clothes and let you
    /// change them.
    /// - Rise games are played by your crew, and your crew wears your Kit Studio kits: the slot picked in
    ///   WEAR IN GAMES (or the Away kit when the colours clash with the opponent).
    /// - Before you've designed a kit, the HOME kit is your crew's colours with the equipped jersey and shoes,
    ///   which is exactly the classic look the game draws.
    /// - The OUTFIT step edits a copy of the kits; nothing changes until you save.
    /// </summary>
    public static class RiseOutfit
    {
        /// <summary>Quick colours for the OUTFIT step (the Kit Studio still has the full palette).</summary>
        public static readonly RgbColor[] Swatches = BuildSwatches();

        private static RgbColor[] BuildSwatches()
        {
            string[] hex =
            {
                "#F2F2F2", "#1A1A1F", "#8A8A96", "#D62828", "#F77F00", "#FFD166",
                "#06D6A0", "#2EC4B6", "#4CC9F0", "#1D4ED8", "#7209B7", "#F72585",
            };
            var list = new RgbColor[hex.Length];
            for (int i = 0; i < hex.Length; i++) list[i] = Kits.Snap(RgbColor.FromHex(hex[i]));
            return list;
        }

        /// <summary>
        /// Makes sure the career has three kit slots, starting from your current colours: your created team's,
        /// or your crew's with the equipped jersey palette and shoes.
        /// </summary>
        public static void EnsureKits(CareerSaveData d, ContentCatalog c)
        {
            if (d == null || c == null) return;
            if (d.kits == null) d.kits = new KitSaveData();
            var team = c.Team(CustomTeams.TeamId);
            if (team != null)
            {
                Kits.Ensure(d.kits, team.primary, team.secondary, team.accent, team.shorts, team.shoes);
                return;
            }
            var crew = c.Team(DefaultContent.PlayerCrewId);
            var jersey = c.Find(c.Cosmetics, d.equippedJersey);
            var shoes = c.Find(c.Cosmetics, d.equippedShoes);
            var primary = jersey?.colorA ?? crew?.primary ?? new RgbColor(0xF7, 0x25, 0x85);
            var trim = jersey?.colorB ?? crew?.secondary ?? new RgbColor(0x4C, 0xC9, 0xF0);
            var accent = crew?.accent ?? new RgbColor(0x1A, 0x1A, 0x2E);
            Kits.Ensure(d.kits, primary, trim, accent, null, shoes?.colorA);
        }

        /// <summary>A copy of the career's kits to edit (slots made if needed).</summary>
        public static KitSaveData Draft(CareerSaveData d, ContentCatalog c)
        {
            var copy = new KitSaveData();
            if (d == null) { Kits.Ensure(copy, new RgbColor(0xF7, 0x25, 0x85), new RgbColor(0x4C, 0xC9, 0xF0), new RgbColor(0x1A, 0x1A, 0x2E), null, null); return copy; }
            EnsureKits(d, c);
            copy.designed = d.kits.designed;
            copy.wear = d.kits.wear;
            copy.autoAway = d.kits.autoAway;
            foreach (var k in d.kits.slots) copy.slots.Add(k.Clone());
            return copy;
        }

        /// <summary>The kit worn in Rise games (before any clash swap).</summary>
        public static KitData Worn(KitSaveData kits) =>
            kits?.slots == null || kits.slots.Count == 0 ? null : kits.slots[Mod(kits.wear, kits.slots.Count)];

        /// <summary>True when the draft dresses the player differently from the saved kits.</summary>
        public static bool Changed(KitSaveData saved, KitSaveData draft)
        {
            if (draft == null) return false;
            if (saved == null || saved.slots == null || saved.slots.Count != draft.slots.Count) return true;
            if (saved.wear != draft.wear) return true;
            for (int i = 0; i < draft.slots.Count; i++)
                if (!draft.slots[i].SameLook(saved.slots[i])) return true;
            return false;
        }

        /// <summary>
        /// Saves the OUTFIT draft. Returns true when it changed anything; the kits then count as designed, so
        /// your crew wears them in Rise and every other game with your team.
        /// </summary>
        public static bool Apply(CareerSaveData d, KitSaveData draft)
        {
            if (d == null || draft == null || draft.slots == null || draft.slots.Count < Kits.SlotCount) return false;
            if (!Changed(d.kits, draft)) return false;
            if (d.kits == null) d.kits = new KitSaveData();
            d.kits.slots = new List<KitData>();
            foreach (var k in draft.slots)
            {
                var copy = k.Clone();
                Kits.Clamp(copy);
                d.kits.slots.Add(copy);
            }
            d.kits.wear = Mod(draft.wear, Kits.SlotCount);
            d.kits.designed = true;
            return true;
        }

        /// <summary>
        /// The next style option for a part in direction <paramref name="dir"/>, skipping ones you haven't unlocked.
        /// Stays put when nothing else is unlocked.
        /// </summary>
        public static int NextStyle(string part, int current, int count, CareerSaveData d, int dir)
        {
            if (count <= 0) return 0;
            dir = dir < 0 ? -1 : 1;
            for (int step = 1; step <= count; step++)
            {
                int v = Mod(current + dir * step, count);
                if (Kits.IsUnlocked(part, v, d)) return v;
            }
            return Mod(current, count);
        }

        /// <summary>How many options of a part you can pick now.</summary>
        public static int UnlockedCount(string part, int count, CareerSaveData d)
        {
            int n = 0;
            for (int v = 0; v < count; v++) if (Kits.IsUnlocked(part, v, d)) n++;
            return n;
        }

        /// <summary>Puts the classic crew-colour kit back in this slot.</summary>
        public static void ResetSlot(KitSaveData draft, int slot, CareerSaveData d, ContentCatalog c)
        {
            if (draft?.slots == null || slot < 0 || slot >= draft.slots.Count) return;
            var fresh = new CareerSaveData();
            if (d != null) { fresh.equippedJersey = d.equippedJersey; fresh.equippedShoes = d.equippedShoes; }
            EnsureKits(fresh, c);
            var k = fresh.kits.slots[slot].Clone();
            k.name = draft.slots[slot].name;
            draft.slots[slot] = k;
        }

        private static int Mod(int v, int n) => n <= 0 ? 0 : ((v % n) + n) % n;
    }
}
