using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Phase 41 money model. RETRO HOOPS PRO is the game's only purchase: a one-time, non-consumable unlock (not a
    /// subscription). Everything else is free, including LIVE online play through Game Center matchmaking.
    /// Pro adds:
    ///  • Rise seasons 4 and up: nine more rival crews with their stories, badges and courts (seasons 1-3 are free).
    ///  • Every Hoops Pass item in the locker right away (jerseys, shoes, banners, celebrations, the Skyline dunk)
    ///    instead of waiting for each one's pass season. Free players still earn them through the pass.
    /// The App Store decides whether Pro is owned; the career keeps the last answer so menus are right on launch.
    /// </summary>
    public static class Pro
    {
        /// <summary>App Store Connect product id (Non-Consumable).</summary>
        public const string ProductId = "com.phoronomicstudios.retrohoops.pro";
        public const string DisplayName = "Retro Hoops Pro";
        /// <summary>Shown only until the App Store returns the real, localised price.</summary>
        public const string PriceFallback = "$4.99";
        /// <summary>Rise seasons anyone can play; the next one needs Pro.</summary>
        public const int FreeRiseSeasons = 3;

        /// <summary>LIVE is free in v1 (Game Center matchmaking). A paid tier can come back once there are players.</summary>
        public const bool LiveIsFree = true;

        /// <summary>True when starting the next Rise season needs Pro (and it isn't owned).</summary>
        public static bool NextSeasonLocked(RiseSaveData r, bool owned) =>
            !owned && r != null && r.seasonsPlayed >= FreeRiseSeasons;

        /// <summary>Every Hoops Pass item that exists in the catalog, in pass order.</summary>
        public static List<string> PassItems(ContentCatalog c)
        {
            var ids = new List<string>();
            foreach (var set in HoopsPass.GearSets)
                foreach (var id in set)
                    if (!ids.Contains(id) && (c == null || c.Find(c.Cosmetics, id) != null)) ids.Add(id);
            return ids;
        }

        /// <summary>Puts every Hoops Pass item in the locker. Returns how many were new. Safe to call again.</summary>
        public static int GrantPassItems(CareerSaveData d, ContentCatalog c)
        {
            if (d == null) return 0;
            d.ownedCosmetics = d.ownedCosmetics ?? new List<string>();
            int added = 0;
            foreach (var id in PassItems(c))
                if (!d.ownedCosmetics.Contains(id)) { d.ownedCosmetics.Add(id); added++; }
            return added;
        }

        /// <summary>
        /// Records the App Store's answer. Becoming owned grants the pass items once; a refund (owned → not owned) only
        /// re-locks new Rise seasons. Items already in the locker stay (they can also be earned for free).
        /// Returns true if anything changed (the caller saves).
        /// </summary>
        public static bool Apply(CareerSaveData d, ContentCatalog c, bool owned)
        {
            if (d == null) return false;
            bool changed = d.proOwned != owned;
            d.proOwned = owned;
            if (owned && !d.proGranted)
            {
                GrantPassItems(d, c);
                d.proGranted = true;
                changed = true;
            }
            return changed;
        }

        /// <summary>Plain-language terms for the purchase screen (App Review 3.1.1: say clearly what you get).</summary>
        public static string Terms(string price)
        {
            string pr = string.IsNullOrEmpty(price) ? PriceFallback : price;
            if (Loc.Language == Loc.Spanish)
                return DisplayName + " es una compra única de " + pr + ". No es una suscripción y nunca se renueva. " +
                       "Desbloquea las temporadas 4 en adelante del modo Ascenso y pone todos los objetos del Hoops Pass en tu vestuario. " +
                       "El pago se carga a tu cuenta de Apple al confirmar. Usa RESTAURAR COMPRAS en otro dispositivo. Live y todos los demás modos siguen siendo gratis.";
            return DisplayName + " is a one-time purchase of " + (string.IsNullOrEmpty(price) ? PriceFallback : price) + ". It isn't a subscription and never renews. " +
            "It unlocks Rise seasons 4 and up and puts every Hoops Pass item in your locker. Payment is charged to your Apple Account when you confirm. " +
            "Use RESTORE PURCHASES on a new device. Live online play and every other mode stay free.";
        }

        public static readonly string[] Perks =
        {
            "Rise seasons 4 and up: nine more rival crews, their stories, courts and badges",
            "Every Hoops Pass item now: jerseys, shoes, banners, celebrations and the Skyline dunk",
            "One payment, yours for good, on every device with your Apple Account",
            "Supports a one-person studio. No ads, ever",
        };
    }
}
