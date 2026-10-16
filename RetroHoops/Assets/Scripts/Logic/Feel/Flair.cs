using System;

namespace CallerRetroBall.Logic
{
    public enum CelebrationKind { FistPump = 0, CallIt = 1, ShimmyStep = 2, RaiseTheRoof = 3, PixelWave = 4, TakeABow = 5, ShoulderBrush = 6, PaperPlane = 7, ChestThump = 8, /** Season 6 (pass only). */ Spotlight = 9, /** Season 7 (pass only). */ VictoryLap = 10, /** Season 8 (pass only). */ LanternRelease = 11, /** Season 9 (pass only). */ SkateGlide = 12, /** Season 10 (pass only). */ TumbleDry = 13, /** Season 11 (pass only). */ KiteRun = 14, /** Season 12 (pass only). */ PuttDrop = 15 }

    public enum DribbleMoveKind { Crossover = 0, HesiHop = 1, SpinCycle = 2, BehindTheBack = 3, DoubleCross = 4, StepBack = 5, RockerStep = 6, SnatchBack = 7 }

    /// <summary>
    /// Presentation-only offsets for one player sprite at one moment, in art pixels.
    /// The views add these on top of what the simulation says; gameplay never reads them.
    /// </summary>
    /// <summary>Phase 30: special sprite frames a pose can ask for (see CharacterSpriteGenerator).</summary>
    public enum PoseFrame { None = 0, Crossover = 1, StepBack = 2, ChestThump = 3, Defense = 4, Slide = 5, Dunk = 6, Pass = 7 }

    public struct FlairPose
    {
        /// <summary>A special sprite frame (crossover dribble, step-back, chest thump); None = the usual idle/run frames.</summary>
        public PoseFrame Frame;
        /// <summary>Sprite lift in art pixels (up).</summary>
        public int Lift;
        /// <summary>Sideways sprite offset in art pixels.</summary>
        public int OffsetX;
        /// <summary>Flip the sprite horizontally relative to its normal facing.</summary>
        public bool FlipOverride;
        /// <summary>Use the arms-up frame.</summary>
        public bool ArmsUp;
        /// <summary>Held-ball offset in art pixels (dribble moves move the ball between hands).</summary>
        public int BallOffsetX;
        public int BallLift;

        public static FlairPose None => default;
        public bool IsNone => Frame == PoseFrame.None && Lift == 0 && OffsetX == 0 && !FlipOverride && !ArmsUp && BallOffsetX == 0 && BallLift == 0;
    }

    /// <summary>
    /// Celebrations (after you score) and dribble moves (sharp cuts with the ball), chosen by the
    /// equipped cosmetics. Pure functions of time, so they are deterministic and unit-tested.
    /// </summary>
    public static class Flair
    {
        public const float CelebrationSeconds = 0.9f;
        public const float DribbleMoveSeconds = 0.36f;
        public const float DribbleMoveCooldown = 0.7f;
        /// <summary>A direction change sharper than this (degrees) triggers a dribble move.</summary>
        public const float DribbleMoveAngle = 110f;

        public static CelebrationKind CelebrationFor(string cosmeticId)
        {
            switch (cosmeticId)
            {
                case "cosmetic.celebration.call_it": return CelebrationKind.CallIt;
                case "cosmetic.celebration.shimmy_step": return CelebrationKind.ShimmyStep;
                case "cosmetic.celebration.raise_roof": return CelebrationKind.RaiseTheRoof;
                case "cosmetic.celebration.pixel_wave": return CelebrationKind.PixelWave;
                case "cosmetic.celebration.take_a_bow": return CelebrationKind.TakeABow;
                case "cosmetic.celebration.shoulder_brush": return CelebrationKind.ShoulderBrush;
                case "cosmetic.celebration.paper_plane": return CelebrationKind.PaperPlane;
                case "cosmetic.celebration.chest_thump": return CelebrationKind.ChestThump;
                case "cosmetic.pass.celebration.spotlight": return CelebrationKind.Spotlight;
                case "cosmetic.pass.celebration.victory_lap": return CelebrationKind.VictoryLap;
                case "cosmetic.pass.celebration.lantern_release": return CelebrationKind.LanternRelease;
                case "cosmetic.pass.celebration.skate_glide": return CelebrationKind.SkateGlide;
                case "cosmetic.pass.celebration.tumble_dry": return CelebrationKind.TumbleDry;
                case "cosmetic.pass.celebration.kite_run": return CelebrationKind.KiteRun;
                case "cosmetic.pass.celebration.putt_drop": return CelebrationKind.PuttDrop;
                default: return CelebrationKind.FistPump;
            }
        }

        public static DribbleMoveKind DribbleMoveFor(string cosmeticId)
        {
            switch (cosmeticId)
            {
                case "cosmetic.move.hesi_hop": return DribbleMoveKind.HesiHop;
                case "cosmetic.move.spin_cycle": return DribbleMoveKind.SpinCycle;
                case "cosmetic.move.behind_back": return DribbleMoveKind.BehindTheBack;
                case "cosmetic.move.double_cross": return DribbleMoveKind.DoubleCross;
                case "cosmetic.move.step_back": return DribbleMoveKind.StepBack;
                case "cosmetic.move.rocker_step": return DribbleMoveKind.RockerStep;
                case "cosmetic.move.snatch_back": return DribbleMoveKind.SnatchBack;
                default: return DribbleMoveKind.Crossover;
            }
        }

        /// <summary>Pose <paramref name="t"/> seconds into a celebration (None once it's over).</summary>
        public static FlairPose Celebration(CelebrationKind kind, float t)
        {
            if (t < 0f || t >= CelebrationSeconds) return FlairPose.None;
            var p = new FlairPose();
            switch (kind)
            {
                case CelebrationKind.FistPump:
                    // Hop, then three quick arm pumps.
                    p.Lift = Hop(t, 0f, 0.3f, 3);
                    p.ArmsUp = t < 0.3f || ((int)((t - 0.3f) / 0.1f) % 2 == 0);
                    break;
                case CelebrationKind.CallIt:
                    // Arms up the whole time, two hops: "called it".
                    p.ArmsUp = true;
                    p.Lift = Hop(t, 0f, 0.35f, 3) + Hop(t, 0.45f, 0.35f, 2);
                    break;
                case CelebrationKind.RaiseTheRoof:
                    // Arms pumping up on the beat with a little bounce each time.
                    int beat = (int)(t / 0.15f);
                    p.ArmsUp = beat % 2 == 0;
                    p.Lift = beat % 2 == 0 ? 2 : 0;
                    break;
                case CelebrationKind.PixelWave:
                    // A ripple: the sprite rises one pixel at a time, then falls, twice, arms up at the crest.
                    int phase = (int)(t / 0.075f) % 6;
                    p.Lift = phase < 3 ? phase : 6 - phase;
                    p.ArmsUp = phase == 3;
                    break;
                case CelebrationKind.TakeABow:
                    // Step back, dip (a bow), then arms up to the crowd.
                    p.OffsetX = t < 0.2f ? -1 : 0;
                    p.Lift = t >= 0.25f && t < 0.6f ? -2 : 0;
                    p.ArmsUp = t >= 0.65f;
                    break;
                case CelebrationKind.ShoulderBrush:
                    // Turn away, brush one shoulder then the other (facing flips), stand tall.
                    int brush = (int)(t / 0.18f);
                    p.FlipOverride = brush % 2 == 1;
                    p.OffsetX = brush < 4 ? (brush % 2 == 0 ? -1 : 1) : 0;
                    p.ArmsUp = brush >= 4;
                    break;
                case CelebrationKind.PaperPlane:
                    // Wind up low, "throw" the plane (arms up, lean forward), then watch it fly away.
                    p.Lift = t < 0.2f ? -1 : (t < 0.45f ? Hop(t, 0.2f, 0.25f, 2) : 0);
                    p.ArmsUp = t >= 0.2f && t < 0.5f;
                    p.OffsetX = t >= 0.2f && t < 0.5f ? 1 : 0;
                    break;
                case CelebrationKind.PuttDrop:
                    // Crouch to line up the putt, a still beat, then pop up with both arms high as it drops.
                    if (t < 0.45f) p.Lift = -1;
                    else if (t < 0.6f) p.Lift = 0;
                    else { p.ArmsUp = true; p.Lift = t < 0.8f ? 1 : 0; }
                    break;
                case CelebrationKind.KiteRun:
                    // Jog in place with one arm up holding the string, then both arms up to let the kite climb, then a crouch to watch it.
                    if (t < 0.5f) { p.ArmsUp = (int)(t / 0.125f) % 2 == 0; p.Lift = (int)(t / 0.0625f) % 2; }
                    else if (t < 0.8f) { p.ArmsUp = true; p.Lift = 1; }
                    else p.Lift = -1;
                    break;
                case CelebrationKind.TumbleDry:
                    // Arms up and a little bounce on the spot like a drum turning, then flop down low, then pop back up.
                    if (t < 0.5f) { p.ArmsUp = true; p.Lift = ((int)(t / 0.1f) % 2 == 0) ? 1 : 0; }
                    else if (t < 0.75f) p.Lift = -1;
                    else p.Lift = 0;
                    break;
                case CelebrationKind.SkateGlide:
                    // Push off twice, then glide low on one skate with an arm out, and pop up with both arms high.
                    if (t < 0.35f) { p.Lift = ((int)(t / 0.0875f) % 2 == 0) ? 0 : 1; }
                    else if (t < 0.75f) p.Lift = -1;
                    else { p.ArmsUp = true; p.Lift = 1; }
                    break;
                case CelebrationKind.LanternRelease:
                    // Crouch and cup a lantern low, rise slowly lifting it, let it go with arms up, then watch it float away.
                    if (t < 0.25f) p.Lift = -1;
                    else if (t < 0.6f) { p.Lift = (int)((t - 0.25f) / 0.12f); p.ArmsUp = t > 0.45f; }
                    else { p.ArmsUp = t < 0.85f; p.Lift = t < 0.85f ? 2 : 0; }
                    break;
                case CelebrationKind.VictoryLap:
                    // A quick lap: run out to one side, turn, run back past the start, and finish with arms up.
                    {
                        float u = t / CelebrationSeconds;
                        p.OffsetX = (int)Math.Round(Math.Sin(u * 2.0 * Math.PI) * 4.0);
                        p.FlipOverride = u > 0.25f && u < 0.75f;
                        p.Lift = (int)(t / 0.1f) % 2;
                        p.ArmsUp = u > 0.8f;
                    }
                    break;
                case CelebrationKind.Spotlight:
                    // Slow turn in place (facing flips like a sweeping beam), then a chest thump, then arms up to the lights.
                    if (t < 0.48f)
                    {
                        p.FlipOverride = (int)(t / 0.12f) % 2 == 1;
                        p.Lift = (int)(t / 0.12f) % 2;
                    }
                    else if (t < 0.66f) p.Frame = PoseFrame.ChestThump;
                    else
                    {
                        p.ArmsUp = true;
                        p.Lift = Hop(t, 0.66f, 0.24f, 2);
                    }
                    break;
                case CelebrationKind.ChestThump:
                    // Two thumps on the chest (a little bounce on each), then the fist goes up to the crowd.
                    int thump = (int)(t / 0.16f);
                    p.Frame = thump < 4 ? PoseFrame.ChestThump : PoseFrame.None;
                    p.Lift = thump < 4 ? (thump % 2 == 0 ? 1 : 0) : Hop(t, 0.64f, 0.26f, 2);
                    p.ArmsUp = thump >= 4;
                    break;
                default: // ShimmyStep
                    int step = (int)(t / 0.12f);
                    p.OffsetX = step % 2 == 0 ? -1 : 1;
                    p.FlipOverride = step % 2 == 1;
                    p.Lift = step % 4 == 3 ? 1 : 0;
                    break;
            }
            return p;
        }

        /// <summary>Pose <paramref name="t"/> seconds into a dribble move (None once it's over).</summary>
        public static FlairPose DribbleMove(DribbleMoveKind kind, float t)
        {
            if (t < 0f || t >= DribbleMoveSeconds) return FlairPose.None;
            float u = t / DribbleMoveSeconds; // 0..1
            var p = new FlairPose();
            switch (kind)
            {
                case DribbleMoveKind.Crossover:
                    // Ball swings low from one hand to the other.
                    p.BallOffsetX = (int)Math.Round(-3f + 6f * u);
                    p.BallLift = -(int)Math.Round(Math.Sin(u * Math.PI) * 2f);
                    p.Frame = u > 0.15f && u < 0.85f ? PoseFrame.Crossover : PoseFrame.None;
                    break;
                case DribbleMoveKind.HesiHop:
                    // Freeze-and-hop: the player pops up while the ball is held high.
                    p.Lift = Hop(t, 0.1f, 0.22f, 2);
                    p.BallLift = u < 0.6f ? 3 : 0;
                    break;
                case DribbleMoveKind.BehindTheBack:
                    // Ball disappears behind the body (lowered, swung across) then pops out the far side.
                    p.BallOffsetX = (int)Math.Round(-3f + 6f * u);
                    p.BallLift = u > 0.2f && u < 0.8f ? -3 : 0;
                    p.FlipOverride = u > 0.5f && u < 0.7f;
                    break;
                case DribbleMoveKind.DoubleCross:
                    // Two crossovers back to back: left, right, left.
                    p.BallOffsetX = (int)Math.Round(3f * Math.Cos(u * 2.0 * Math.PI));
                    p.BallLift = -(int)Math.Round(Math.Abs(Math.Sin(u * 2.0 * Math.PI)) * 2f);
                    p.Frame = p.BallLift < 0 ? PoseFrame.Crossover : PoseFrame.None;
                    break;
                case DribbleMoveKind.StepBack:
                    // Hop back a couple of pixels, ball gathered high.
                    p.OffsetX = u < 0.6f ? -(int)Math.Round(u / 0.6f * 2f) : -2;
                    p.Lift = Hop(t, 0.05f, 0.2f, 1);
                    p.BallLift = 2;
                    p.Frame = u > 0.1f ? PoseFrame.StepBack : PoseFrame.None;
                    break;
                case DribbleMoveKind.RockerStep:
                    // Jab forward, rock back, jab again: the body sways while the ball stays tight.
                    p.OffsetX = (int)Math.Round(Math.Sin(u * 2.0 * Math.PI) * 2f);
                    p.BallOffsetX = p.OffsetX > 0 ? 1 : -1;
                    break;
                case DribbleMoveKind.SnatchBack:
                    // Ball pushed out low, then snatched back high across the body.
                    p.BallOffsetX = u < 0.45f ? (int)Math.Round(u / 0.45f * 4f) : (int)Math.Round(4f - (u - 0.45f) / 0.55f * 7f);
                    p.BallLift = u < 0.45f ? -2 : 2;
                    break;
                default: // SpinCycle
                    // Two facing flips in quick succession reads as a spin at pixel scale.
                    p.FlipOverride = u < 0.25f || (u >= 0.5f && u < 0.75f);
                    p.BallOffsetX = u < 0.5f ? 2 : -2;
                    break;
            }
            return p;
        }

        /// <summary>
        /// True when the move direction turned sharply (from <paramref name="previous"/> to
        /// <paramref name="current"/>), both being real moves rather than stick noise.
        /// </summary>
        public static bool IsSharpCut(Vec2 previous, Vec2 current)
        {
            if (previous.SqrMagnitude < 0.25f || current.SqrMagnitude < 0.25f) return false;
            float dot = (previous.x * current.x + previous.y * current.y) / (previous.Magnitude * current.Magnitude);
            dot = Math.Max(-1f, Math.Min(1f, dot));
            double angle = Math.Acos(dot) * 180.0 / Math.PI;
            return angle >= DribbleMoveAngle;
        }

        /// <summary>
        /// Shooter's leap during a dunk or layup, as a jump fraction for the sprite (1 = a normal
        /// defensive jump, so dunks rise higher). Zero for jump shots, which use the arms-up pose only.
        /// </summary>
        public static float Leap(ShotType type, float t, float duration)
        {
            if (duration <= 0f || t < 0f || t >= duration) return 0f;
            float peak = type == ShotType.Dunk ? 1.8f : type == ShotType.Layup ? 1.0f : 0f;
            if (peak <= 0f) return 0f;
            float u = t / duration;
            return 4f * u * (1f - u) * peak;
        }

        /// <summary>Parabolic hop of <paramref name="height"/> art pixels between start and start+duration.</summary>
        private static int Hop(float t, float start, float duration, int height)
        {
            float u = (t - start) / duration;
            if (u <= 0f || u >= 1f) return 0;
            return (int)Math.Round(4f * u * (1f - u) * height);
        }
    }

    /// <summary>
    /// Tap-to-shoot (accessibility): tap once to start the meter, tap again to release. Turns
    /// taps into the "held" signal the simulation expects. Pure, so it's unit-tested.
    /// </summary>
    public struct TapShoot
    {
        public bool Holding;
        private int _grace;

        /// <summary>Frames to wait for the meter to start after the first tap.</summary>
        public const int StartGraceFrames = 8;

        /// <param name="tapped">Shoot was tapped this frame.</param>
        /// <param name="charging">The player's shot meter is running.</param>
        /// <returns>Whether SHOOT counts as held this frame.</returns>
        public bool Update(bool tapped, bool charging)
        {
            if (!Holding)
            {
                if (tapped)
                {
                    Holding = true;
                    _grace = StartGraceFrames;
                }
            }
            else if (tapped && charging)
            {
                Holding = false;      // second tap releases
            }
            else if (charging)
            {
                _grace = 0;           // meter is running: keep holding until the next tap
            }
            else if (_grace > 0)
            {
                _grace--;             // waiting for the meter to start
            }
            else
            {
                Holding = false;      // the shot ended some other way (released, blocked, stripped)
            }
            return Holding;
        }
    }

    /// <summary>One particle of a pixel burst (score sparks, block dust).</summary>
    public struct BurstParticle
    {
        /// <summary>Initial velocity in world units per second.</summary>
        public float vx, vy;
        public float life;
    }

    /// <summary>Deterministic pixel bursts; the Unity layer moves and fades them.</summary>
    public static class Bursts
    {
        public const float Gravity = -9f;

        public static BurstParticle[] Create(uint seed, int count, float speed, float life)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var parts = new BurstParticle[count];
            Fill(seed, parts, count, speed, life);
            return parts;
        }

        /// <summary>Same as <see cref="Create"/> but writes into <paramref name="parts"/> (no allocation).</summary>
        public static void Fill(uint seed, BurstParticle[] parts, int count, float speed, float life)
        {
            if (count < 0 || count > parts.Length) throw new ArgumentOutOfRangeException(nameof(count));
            var rng = new SeededRandom(seed == 0 ? 1u : seed);
            for (int i = 0; i < count; i++)
            {
                // Mostly upward fan, like sparks off the rim.
                double angle = (20.0 + rng.NextFloat() * 140.0) * Math.PI / 180.0;
                float s = speed * (0.5f + 0.5f * rng.NextFloat());
                parts[i] = new BurstParticle
                {
                    vx = (float)(Math.Cos(angle) * s),
                    vy = (float)(Math.Sin(angle) * s),
                    life = life * (0.7f + 0.3f * rng.NextFloat()),
                };
            }
        }

        /// <summary>Offset from the burst origin after <paramref name="t"/> seconds.</summary>
        public static void Offset(BurstParticle p, float t, out float x, out float y)
        {
            x = p.vx * t;
            y = p.vy * t + 0.5f * Gravity * t * t;
        }
    }
}
