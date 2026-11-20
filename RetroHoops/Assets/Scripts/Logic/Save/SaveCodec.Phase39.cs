using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public static partial class SaveCodec
    {
        // Phase 39: the welcome-back bonus and the controls feel settings.
        // Phase 40: small integer lists (recent Rise margins).
        private static List<object> Ints(List<int> values)
        {
            var list = new List<object>();
            if (values != null) foreach (var v in values) list.Add(v);
            return list;
        }

        private static List<int> IntList(Dictionary<string, object> o, string key)
        {
            var list = new List<int>();
            foreach (var item in Arr(o, key)) if (item is double d) list.Add((int)Math.Round(d));
            return list;
        }

        private static object EncodeLogin(CareerSaveData d) =>
            new Dictionary<string, object> { ["day"] = d.loginDay, ["streak"] = d.loginStreak, ["best"] = d.loginBest };

        private static void DecodeLogin(CareerSaveData d, Dictionary<string, object> o)
        {
            if (o == null) return;
            d.loginDay = Math.Max(-1, Int(o, "day", -1));
            d.loginStreak = Math.Max(0, Int(o, "streak", 0));
            d.loginBest = Math.Max(d.loginStreak, Int(o, "best", 0));
        }

        // Phase 41: Retro Hoops Pro (cached App Store answer).
        private static object EncodePro(CareerSaveData d) =>
            new Dictionary<string, object>
            {
                ["owned"] = d.proOwned, ["granted"] = d.proGranted,
                // I141-I150: the SHOP (cached App Store answer, tips, free tries).
                ["shop"] = Strings(d.shopOwned ?? new List<string>()), ["tips"] = d.shopTips, ["tries"] = Strings(d.shopTries ?? new List<string>()),
                ["earned"] = Strings(d.shopEarned ?? new List<string>()), ["seen"] = d.shopSeenVersion,
            };

        private static void DecodePro(CareerSaveData d, Dictionary<string, object> o)
        {
            if (o == null) return;
            d.proOwned = Bool(o, "owned", false);
            d.proGranted = Bool(o, "granted", false);
            d.shopOwned = StrList(o, "shop").FindAll(id => Shop.Product(id) != null && !Shop.Product(id).Consumable);
            d.shopTips = Math.Max(0, Int(o, "tips", 0));
            d.shopTries = StrList(o, "tries");
            while (d.shopTries.Count > 40) d.shopTries.RemoveAt(0);
            d.shopEarned = StrList(o, "earned").FindAll(id => Shop.IsShopCourt(id) && Shop.PackOf(id) != null);
            if (d.shopEarned.Count > Loyalty.Milestones.Length) d.shopEarned.RemoveRange(Loyalty.Milestones.Length, d.shopEarned.Count - Loyalty.Milestones.Length);
            d.shopSeenVersion = Math.Max(0, Int(o, "seen", 0));
        }

        // 12 Oct 2026: BETA FEEDBACK profile.
        private static object EncodeBeta(BetaProfileData b)
        {
            b = b ?? new BetaProfileData();
            return new Dictionary<string, object> { ["name"] = b.name ?? "", ["games"] = b.hoopsGames, ["hoops"] = b.hoops, ["asked"] = b.asked, ["sent"] = b.sent };
        }

        private static BetaProfileData DecodeBeta(Dictionary<string, object> o)
        {
            var b = new BetaProfileData();
            if (o == null) return b;
            string name = Str(o, "name", "") ?? "";
            b.name = name.Length > BetaFeedback.MaxName ? name.Substring(0, BetaFeedback.MaxName) : name;
            b.hoopsGames = Math.Max(-1, Math.Min(BetaFeedback.HoopsGamesOptions.Length - 1, Int(o, "games", -1)));
            b.hoops = Math.Max(-1, Math.Min(BetaFeedback.HoopOptions.Length - 1, Int(o, "hoops", -1)));
            b.asked = Bool(o, "asked", false);
            b.sent = Math.Max(0, Int(o, "sent", 0));
            return b;
        }

        private static object EncodeFeel(SettingsData s)
        {
            s = s ?? new SettingsData();
            return new Dictionary<string, object> { ["stick"] = s.stickSize, ["dead"] = s.stickDeadZone, ["haptic"] = s.hapticStrength, ["smart"] = s.smartDifficulty,
                ["opacity"] = s.controlsOpacity, ["fill"] = s.fillScreen, ["preset"] = s.controlPreset, ["greenBuzz"] = s.greenBuzz,
                ["iconsOnly"] = s.iconsOnly, ["momentum"] = s.momentumBar };
        }

        private static void DecodeFeel(SettingsData s, Dictionary<string, object> o)
        {
            if (s == null) return;
            // I131: saves from before the SPREAD layout keep their own arrangement on the CLASSIC arc it was made on.
            int migrated = string.IsNullOrEmpty(s.controlLayout) ? ControlLayout.Spread : ControlLayout.Classic;
            if (o == null) { s.controlPreset = migrated; return; }
            s.controlPreset = o.ContainsKey("preset") ? ControlLayout.ClampPreset(Int(o, "preset", 0)) : migrated;
            s.greenBuzz = Bool(o, "greenBuzz", false);
            s.iconsOnly = Bool(o, "iconsOnly", false);
            s.momentumBar = Bool(o, "momentum", true);
            s.stickSize = ControlFeel.Clamp(Int(o, "stick", 1));
            s.stickDeadZone = ControlFeel.Clamp(Int(o, "dead", 1));
            s.hapticStrength = ControlFeel.Clamp(Int(o, "haptic", 1));
            s.smartDifficulty = Bool(o, "smart", true);
            s.controlsOpacity = ControlFeel.ClampOpacity(Int(o, "opacity", ControlFeel.DefaultOpacity));
            s.fillScreen = Bool(o, "fill", true);
        }
    }
}
