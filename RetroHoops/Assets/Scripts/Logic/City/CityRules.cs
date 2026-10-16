using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>Your Baller City progress: where you are, the last court you used, and first-visit onboarding.</summary>
    [Serializable]
    public class CitySaveData
    {
        public string district = CityDistricts.FirstCallers;
        public float x = -1f, y = -1f;
        public int facing;
        /// <summary>The last court you played on from the city (a court id).</summary>
        public string lastCourt = "";
        /// <summary>Onboarding steps done: 1 walked, 2 used something, 3 stepped onto a court (3 = finished).</summary>
        public int onboarding;
        public int visits;
        /// <summary>Districts visited (for the map and the district banner).</summary>
        public List<string> seen = new List<string>();

        public bool HasPosition => x >= 0f && y >= 0f;
        /// <summary>BATTERY SAVER in the city: 30 fps instead of 60.</summary>
        public bool fps30;

        /// <summary>Puts you at a spawn (a court's gate, a door): where the next visit to the city starts.</summary>
        public void Arrive(string districtId, string spawnId)
        {
            var map = CityDistricts.Map(districtId);
            district = map.DistrictId;
            if (!map.TryGetSpawn(spawnId, out var s)) s = map.DefaultSpawn;
            x = s.Position.x;
            y = s.Position.y;
            facing = s.Facing;
        }
    }

    /// <summary>What the INTERACT button does right now, and its label.</summary>
    public struct CityPrompt
    {
        public CityPortal Portal;
        /// <summary>An NPC in reach (index into the active NPC list), or -1.</summary>
        public int Npc;
        public string Label;
        public bool IsNone => Portal == null && Npc < 0;
        public static CityPrompt None => new CityPrompt { Npc = -1 };
    }

    /// <summary>
    /// Context-sensitive interaction: what's in reach of the player. A court you stand on or a door you face wins
    /// over an NPC beside you only when it's closer. District roads aren't prompts: walking into them is enough.
    /// </summary>
    public static class CityInteraction
    {
        public const float NpcReach = 1.6f;
        public const float PortalReach = 0.9f;

        public static CityPrompt Find(CityMap map, Vec2 player, IList<Vec2> npcs)
        {
            var best = CityPrompt.None;
            float bestDist = float.MaxValue;
            foreach (var p in map.Portals)
            {
                if (p.Kind == CityPortalKind.District) continue;
                float d = DistanceToRect(player, p);
                if (d > PortalReach || d >= bestDist) continue;
                bestDist = d;
                best = new CityPrompt { Portal = p, Npc = -1, Label = p.Label };
            }
            if (npcs != null)
                for (int i = 0; i < npcs.Count; i++)
                {
                    float d = Vec2.Distance(npcs[i], player);
                    // An NPC needs to be really close to beat a portal you're standing in.
                    if (d > NpcReach || d + 0.4f >= bestDist) continue;
                    bestDist = d + 0.4f;
                    best = new CityPrompt { Portal = null, Npc = i, Label = "TALK" };
                }
            return best;
        }

        /// <summary>The district road the player has walked into (they go straight through), or null.</summary>
        public static CityPortal RoadAt(CityMap map, Vec2 player)
        {
            foreach (var p in map.Portals)
                if (p.Kind == CityPortalKind.District && p.Contains(player.x, player.y)) return p;
            return null;
        }

        public static float DistanceToRect(Vec2 p, CityPortal r)
        {
            float dx = Math.Max(Math.Max(r.X - p.x, 0f), p.x - (r.X + r.W));
            float dy = Math.Max(Math.Max(r.Y - p.y, 0f), p.y - (r.Y + r.H));
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }

    /// <summary>Where you are in a walk off the court and into the city.</summary>
    public enum CityTransitionState { Idle, ExitRequested, Validating, Saving, WalkOff, LoadingCity, Spawn, FreeRoam, Failed }

    /// <summary>
    /// 16 Oct 2026 (Omari): LEAVE COURT. The steps from a court (in a Rise game or THE STREETS) to Baller City:
    /// Idle → ExitRequested → Validating → Saving → WalkOff → LoadingCity → Spawn → FreeRoam.
    /// - Leaving a game that's still on needs a confirmation: it ends the game with no result and no rewards
    ///   (exactly like QUIT does), so nothing is awarded for walking off.
    /// - A second request while one is running is refused (no double loads).
    /// - It can be cancelled until the save starts; a failed save goes to Failed and then back to Idle with a reason,
    ///   so the court is never left half-way.
    /// </summary>
    public sealed class CityTransition
    {
        public CityTransitionState State { get; private set; } = CityTransitionState.Idle;
        public string Error { get; private set; }
        /// <summary>Where the player arrives.</summary>
        public string District { get; private set; }
        public string Spawn { get; private set; }
        public bool MatchActive { get; private set; }

        public enum Check { Ok, NeedsConfirmation, Busy }

        /// <summary>Asks to leave. Busy when a walk-off is already running.</summary>
        public Check Request(bool matchActive, string district, string spawn)
        {
            if (State != CityTransitionState.Idle && State != CityTransitionState.Failed) return Check.Busy;
            Error = null;
            MatchActive = matchActive;
            District = CityDistricts.Find(district) != null ? district : CityDistricts.FirstCallers;
            Spawn = string.IsNullOrEmpty(spawn) ? null : spawn;
            State = CityTransitionState.ExitRequested;
            return matchActive ? Check.NeedsConfirmation : Check.Ok;
        }

        /// <summary>Validating: a live game needs <paramref name="confirmed"/>. False (not confirmed) cancels back to Idle.</summary>
        public bool Validate(bool confirmed)
        {
            if (State != CityTransitionState.ExitRequested) return false;
            State = CityTransitionState.Validating;
            if (MatchActive && !confirmed)
            {
                State = CityTransitionState.Idle;
                return false;
            }
            State = CityTransitionState.Saving;
            return true;
        }

        public bool Cancel()
        {
            if (State != CityTransitionState.ExitRequested && State != CityTransitionState.Validating) return false;
            State = CityTransitionState.Idle;
            return true;
        }

        public void Saved(bool ok, string error = null)
        {
            if (State != CityTransitionState.Saving) return;
            if (ok) State = CityTransitionState.WalkOff;
            else
            {
                Error = string.IsNullOrEmpty(error) ? "COULDN'T SAVE" : error;
                State = CityTransitionState.Failed;
            }
        }

        public void WalkedOff() { if (State == CityTransitionState.WalkOff) State = CityTransitionState.LoadingCity; }
        public void CityLoaded() { if (State == CityTransitionState.LoadingCity) State = CityTransitionState.Spawn; }
        public void Spawned() { if (State == CityTransitionState.Spawn) State = CityTransitionState.FreeRoam; }

        /// <summary>After a failure: back to Idle so the court can be used (or left) again.</summary>
        public void Recover() { if (State == CityTransitionState.Failed) State = CityTransitionState.Idle; }
    }

    /// <summary>Times of day in the city (from the phone's clock).</summary>
    public enum CityTimeOfDay { Morning = 0, Afternoon = 1, Evening = 2, Night = 3 }

    public static class CityClock
    {
        public static CityTimeOfDay Of(int hour) =>
            hour >= 6 && hour < 12 ? CityTimeOfDay.Morning
            : hour >= 12 && hour < 17 ? CityTimeOfDay.Afternoon
            : hour >= 17 && hour < 21 ? CityTimeOfDay.Evening
            : CityTimeOfDay.Night;

        /// <summary>How dark it is (0 day .. 1 deep night), smooth through dusk and dawn.</summary>
        public static float Darkness(float hour)
        {
            hour = ((hour % 24f) + 24f) % 24f;
            if (hour >= 7f && hour < 17.5f) return 0f;
            if (hour >= 17.5f && hour < 20f) return (hour - 17.5f) / 2.5f * 0.8f;
            if (hour >= 20f || hour < 5f) return 0.8f;
            return 0.8f * (1f - (hour - 5f) / 2f); // dawn
        }

        /// <summary>Lamps and windows are lit.</summary>
        public static bool LightsOn(float hour) => Darkness(hour) > 0.25f;

        public static string Name(CityTimeOfDay t) =>
            t == CityTimeOfDay.Morning ? "MORNING" : t == CityTimeOfDay.Afternoon ? "AFTERNOON" : t == CityTimeOfDay.Evening ? "EVENING" : "NIGHT";
    }

    /// <summary>First-visit onboarding: walk, use something, step onto a court.</summary>
    public static class CityOnboarding
    {
        public const int Steps = 3;

        public static string Hint(int step) =>
            step <= 0 ? "Use the stick to walk around."
            : step == 1 ? "Walk up to a door or a player and tap the action button."
            : step == 2 ? "Walk onto a court and tap ENTER COURT."
            : null;

        public static bool Done(CitySaveData d) => d != null && d.onboarding >= Steps;

        /// <summary>Advances to <paramref name="reached"/> (steps only go forward). True when it moved.</summary>
        public static bool Reach(CitySaveData d, int reached)
        {
            if (d == null || reached > Steps || reached != d.onboarding + 1) return false;
            d.onboarding = reached;
            return true;
        }
    }
}
