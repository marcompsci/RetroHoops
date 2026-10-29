using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>A car on the highway: its lane, how far along the road it is, and its speed.</summary>
    public sealed class DriveCar
    {
        public int Lane, Variant;
        public float Y, Speed;
        public bool Passed, Hit;
    }

    /// <summary>
    /// 28 Oct 2026 (Omari, BALLER WORLD phase W6): THE DRIVE between regions. A top-down run up a three-lane highway in
    /// your ride: steer between lanes, hold BOOST (it refills, and threading past traffic tops it up), and the scenery
    /// turns from where you left to where you're going. Bumping a car slows you for a moment. However it goes, you get
    /// there in under 30 seconds: a cruise of about 22 s, and if you're running late the ride speeds up so the trip
    /// never passes <see cref="MaxSeconds"/>. Pure rules (no Unity), so the timing is tested.
    /// </summary>
    public sealed class HighwayDrive
    {
        public const int Lanes = 3;
        /// <summary>The road's length (units; the view draws 1 unit = 1 tile).</summary>
        public const float Length = 600f;
        public const float TargetSeconds = 22f;
        /// <summary>The trip never takes longer than this.</summary>
        public const float MaxSeconds = 28f;
        public const float CarLength = 2.6f;
        public const float SteerRate = 3.2f;
        public const float BoostFactor = 1.45f;
        public const float StunSeconds = 0.7f;

        public static float Cruise => Length / TargetSeconds;

        public readonly string From, To;
        public readonly List<DriveCar> Traffic = new List<DriveCar>();

        /// <summary>Lateral position in lanes (0 = left lane, Lanes − 1 = right).</summary>
        public float X { get; private set; } = 1f;
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public float Time { get; private set; }
        /// <summary>The boost meter, 0..1.</summary>
        public float Boost { get; private set; } = 1f;
        public bool Boosting { get; private set; }
        public int Bumps { get; private set; }
        public int NearMisses { get; private set; }
        /// <summary>I25: set by <see cref="Finish"/> when this drive beat the route's best.</summary>
        public bool RouteRecord { get; set; }
        public float Stun { get; private set; }

        public bool Done => Distance >= Length;
        public float Progress => Math.Min(1f, Distance / Length);
        /// <summary>Speed for the dial (mph-ish).</summary>
        public int Mph => (int)Math.Round(Speed * 4f);

        public HighwayDrive(string from, string to, uint seed)
        {
            From = from;
            To = to;
            var rng = new SeededRandom(seed == 0 ? 1u : seed);
            float y = 34f;
            while (y < Length - 50f)
            {
                Traffic.Add(new DriveCar
                {
                    Lane = rng.Range(0, Lanes),
                    Y = y,
                    Speed = Cruise * (0.42f + rng.NextFloat() * 0.25f),
                    Variant = rng.Range(0, 8),
                });
                y += 9f + rng.NextFloat() * 11f;
            }
        }

        /// <summary>One step. <paramref name="steer"/> −1..1 (left / right), <paramref name="boost"/> while held.</summary>
        public void Step(float dt, float steer, bool boost)
        {
            if (Done || dt <= 0f) return;
            dt = Math.Min(dt, 0.1f);
            Time += dt;
            X = Math.Max(0f, Math.Min(Lanes - 1, X + Math.Max(-1f, Math.Min(1f, steer)) * SteerRate * dt));

            Boosting = boost && Boost > 0.02f && Stun <= 0f;
            float target = Boosting ? Cruise * BoostFactor : Cruise;
            if (Stun > 0f) { Stun -= dt; target = Cruise * 0.55f; }
            // Never late: if what's left can't be covered by MaxSeconds at this pace, the ride speeds up.
            float left = Length - Distance, timeLeft = MaxSeconds - 0.6f - Time;
            float need = timeLeft > 0.05f ? left / timeLeft : Cruise * 3f;
            if (need > target) target = need;
            float accel = target > Speed ? 40f : 30f;
            Speed = target > Speed ? Math.Min(target, Speed + accel * dt) : Math.Max(target, Speed - accel * dt);
            Boost = Boosting ? Math.Max(0f, Boost - 0.5f * dt) : Math.Min(1f, Boost + 0.12f * dt);
            Distance = Math.Min(Length, Distance + Speed * dt);

            foreach (var car in Traffic)
            {
                car.Y += car.Speed * dt;
                float gap = car.Y - Distance;
                float side = Math.Abs(car.Lane - X);
                if (!car.Hit && Math.Abs(gap) < CarLength && side < 0.62f && Stun <= 0f)
                {
                    car.Hit = true;
                    Bumps++;
                    Stun = StunSeconds;
                    Speed *= 0.6f;
                    car.Y = Distance + CarLength + 0.4f; // it gets nudged ahead
                }
                if (!car.Passed && gap < -CarLength)
                {
                    car.Passed = true;
                    if (!car.Hit && side >= 0.62f && side < 1.25f)
                    {
                        NearMisses++;
                        Boost = Math.Min(1f, Boost + 0.15f);
                    }
                }
            }
        }

        /// <summary>
        /// I27 EASY DRIVE: the steering a helping hand would give: toward the lane with the most open road ahead
        /// (staying put when it's clear), −1..1. Added to the player's own steering when the setting is on.
        /// </summary>
        public float AutoSteer()
        {
            const float look = 26f;
            float best = -1f;
            int bestLane = (int)Math.Round(X);
            for (int lane = 0; lane < Lanes; lane++)
            {
                float clear = look;
                foreach (var c in Traffic)
                {
                    float gap = c.Y - Distance;
                    if (c.Lane != lane || gap < -CarLength || gap > look) continue;
                    clear = Math.Min(clear, Math.Max(0f, gap - CarLength));
                }
                // Prefer staying near where you are when lanes are equally clear.
                float score = clear - Math.Abs(lane - X) * 0.8f;
                if (score > best) { best = score; bestLane = lane; }
            }
            float delta = bestLane - X;
            return Math.Abs(delta) < 0.05f ? 0f : Math.Max(-1f, Math.Min(1f, delta * 3f));
        }

        /// <summary>The drive with nobody steering (for tests and the worst case): it still arrives in time.</summary>
        public static float SimulateWorstCase(string from, string to, uint seed, float dt = 1f / 60f)
        {
            var d = new HighwayDrive(from, to, seed);
            int guard = 0;
            while (!d.Done && guard++ < 100000)
            {
                // Steer into the next car's lane every time (as bad as it gets).
                DriveCar next = null;
                foreach (var c in d.Traffic) if (c.Y > d.Distance && (next == null || c.Y < next.Y)) next = c;
                float steer = next == null ? 0f : Math.Sign(next.Lane - d.X);
                d.Step(dt, steer, false);
            }
            return d.Time;
        }

        // ------------------------------------------------------------------ the scenery along the way

        /// <summary>The roadside at <paramref name="progress"/> 0..1: where you left, open country, then where you're going.</summary>
        public static WorldBiome BiomeAt(string from, string to, float progress)
        {
            var a = WorldRegions.Find(from)?.Biome ?? WorldBiome.Suburbs;
            var b = WorldRegions.Find(to)?.Biome ?? WorldBiome.Suburbs;
            if (progress < 0.22f) return a;
            if (progress > 0.72f) return b;
            // In between: desert if either end is in the desert or it's a long haul west; plains otherwise.
            bool west = a == WorldBiome.Desert || b == WorldBiome.Desert || a == WorldBiome.Coast || b == WorldBiome.Coast || a == WorldBiome.Bay || b == WorldBiome.Bay;
            return west ? WorldBiome.Desert : WorldBiome.Plains;
        }

        /// <summary>The Bay is reached over the red bridge (the last stretch is on it).</summary>
        public static bool OnBridge(string to, float progress) => to == WorldRegions.TheBay && progress > 0.86f;

        // ------------------------------------------------------------------ the result

        public const long CleanBonus = 2500, NearMissPay = 150;

        /// <summary>Records a finished drive: counts, best time, today's achievements; pays a clean-drive bonus and near misses. Returns the cash.</summary>
        public static long Finish(CareerSaveData d, HighwayDrive drive, int day)
        {
            var w = d?.world;
            if (w == null || drive == null) return 0;
            w.drives++;
            if (drive.Bumps == 0) w.cleanDrives++;
            w.nearMisses += drive.NearMisses;
            if (drive.Done && (w.bestDrive <= 0f || drive.Time < w.bestDrive)) w.bestDrive = drive.Time;
            drive.RouteRecord = drive.Done && w.NoteRoute(drive.From, drive.To, drive.Time); // I25
            CityAchievements.NoteDrive(w, day, drive.NearMisses);
            long pay = (drive.Bumps == 0 ? CleanBonus : 0) + drive.NearMisses * NearMissPay;
            if (pay > 0)
            {
                if (d.streetCash == null) d.streetCash = new StreetCashSaveData();
                d.streetCash.cash += pay;
                d.streetCash.earned += pay;
            }
            return pay;
        }

        /// <summary>Where a drive to a region arrives: its main district, by your ride.</summary>
        public static (string district, string spawn) Arrival(string region)
        {
            var r = WorldRegions.Find(region) ?? WorldRegions.All[0];
            var m = CityDistricts.Map(r.Main);
            return (r.Main, m.TryGetSpawn("ride", out _) ? "ride" : m.DefaultSpawn.Id);
        }
    }
}
