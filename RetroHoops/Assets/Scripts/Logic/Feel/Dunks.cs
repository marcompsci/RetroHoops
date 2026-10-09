using System;

namespace CallerRetroBall.Logic
{
    public enum DunkStyle { TwoHand = 0, Tomahawk = 1, Windmill = 2, Reverse = 3, ThreeSixty = 4, Cradle = 5, Skyline = 6 }

    /// <summary>
    /// Dunk packages: how a dunk looks (pose, hang time, callout). Presentation only; the simulation
    /// decides make/miss. Your equipped package (Locker Room ► DUNK PACKAGE) is used for your dunks;
    /// the AI picks by finishing rating (big finishers throw down the fancy ones).
    /// </summary>
    public static class Dunks
    {
        public static readonly string[] Names = { "TWO-HAND JAM", "TOMAHAWK!", "WINDMILL!", "REVERSE JAM!", "THREE-SIXTY!", "CRADLE ROCK!", "SKYLINE SLAM!" };

        public static string Name(DunkStyle s) => Names[(int)s];

        public static DunkStyle ForCosmetic(string cosmeticId)
        {
            switch (cosmeticId)
            {
                case "cosmetic.dunk.tomahawk": return DunkStyle.Tomahawk;
                case "cosmetic.dunk.windmill": return DunkStyle.Windmill;
                case "cosmetic.dunk.reverse": return DunkStyle.Reverse;
                case "cosmetic.dunk.three_sixty": return DunkStyle.ThreeSixty;
                case "cosmetic.dunk.cradle": return DunkStyle.Cradle;
                case "cosmetic.pass.dunk.skyline": return DunkStyle.Skyline;
                default: return DunkStyle.TwoHand;
            }
        }

        /// <summary>AI dunk choice from finishing and a 0..1 roll: everyone can jam, only flyers go 360.</summary>
        public static DunkStyle ForAi(int finishing, float roll)
        {
            int options = finishing >= 88 ? 6 : finishing >= 80 ? 5 : finishing >= 72 ? 3 : 2;
            int i = Math.Min(options - 1, (int)(Math.Max(0f, Math.Min(0.999f, roll)) * options));
            return (DunkStyle)i;
        }

        /// <summary>How much higher than a plain dunk the sprite rises (1 = the two-hand jam).</summary>
        public static float LiftScale(DunkStyle s)
        {
            switch (s)
            {
                case DunkStyle.Skyline: return 1.5f;
                case DunkStyle.ThreeSixty: return 1.35f;
                case DunkStyle.Windmill: return 1.25f;
                case DunkStyle.Tomahawk: return 1.2f;
                case DunkStyle.Cradle: return 1.15f;
                default: return 1f;
            }
        }

        /// <summary>Camera shake when it goes down.</summary>
        public static float Shake(DunkStyle s) => s == DunkStyle.TwoHand ? 0.12f : 0.2f;

        /// <summary>Pose at <paramref name="u"/> (0..1 through the leap).</summary>
        public static FlairPose Pose(DunkStyle s, float u)
        {
            var p = new FlairPose();
            if (u < 0f || u >= 1f) return p;
            switch (s)
            {
                case DunkStyle.Tomahawk:
                    // Ball cocked back behind the head, then hammered down.
                    p.ArmsUp = true;
                    p.BallLift = u < 0.6f ? 4 : -1;
                    p.BallOffsetX = u < 0.6f ? -2 : 1;
                    break;
                case DunkStyle.Windmill:
                    // The ball circles: down, back, over the top.
                    double a = u * 2.0 * Math.PI;
                    p.BallOffsetX = (int)Math.Round(Math.Cos(a) * 3.0);
                    p.BallLift = (int)Math.Round(Math.Sin(a) * 3.0) + 1;
                    p.ArmsUp = u > 0.5f;
                    break;
                case DunkStyle.Reverse:
                    // Back to the rim, flushed over the head.
                    p.FlipOverride = true;
                    p.ArmsUp = u > 0.35f;
                    break;
                case DunkStyle.ThreeSixty:
                    // Four facing flips read as a full spin at pixel scale.
                    p.FlipOverride = ((int)(u * 4f)) % 2 == 1;
                    p.ArmsUp = u > 0.7f;
                    break;
                case DunkStyle.Skyline:
                    // Hang time: rises arms wide, a double windmill at the top, hammered home.
                    double w = u * 4.0 * Math.PI;
                    p.BallOffsetX = (int)Math.Round(Math.Cos(w) * 3.0);
                    p.BallLift = (int)Math.Round(Math.Sin(w) * 3.0) + 2;
                    p.ArmsUp = true;
                    p.FlipOverride = u > 0.45f && u < 0.6f;
                    break;
                case DunkStyle.Cradle:
                    // Ball rocked to the hip and back up.
                    p.BallOffsetX = (int)Math.Round(Math.Sin(u * Math.PI) * 3.0);
                    p.BallLift = u < 0.5f ? -1 : 3;
                    p.ArmsUp = u > 0.55f;
                    break;
                default:
                    p.ArmsUp = true;
                    p.BallLift = 2;
                    break;
            }
            return p;
        }
    }

    /// <summary>One moment of a drawn dunk: where the player stands, how high they are, and where the ball is.</summary>
    public struct DunkFrame
    {
        /// <summary>Court position to draw the player at (moves from where they took off to just in front of the rim).</summary>
        public Vec2 Ground;
        /// <summary>Body lift in art pixels.</summary>
        public int LiftPx;
        /// <summary>The ball is still in the dunker's hands (drawn there, not on its flight path).</summary>
        public bool BallInHands;
        /// <summary>Hanging on the rim right after the slam.</summary>
        public bool OnRim;
        /// <summary>Sideways swing while hanging (art pixels, + = right): the legs swing under the rim and settle.</summary>
        public int SwingPx;
        /// <summary>How far the rim is pulled down (art pixels): 2 at the slam, 1 while the dunker hangs.</summary>
        public int RimDipPx;
    }

    /// <summary>
    /// The drawn path of a dunk, so the dunker's hands actually reach the rim: the player glides from the
    /// take-off spot to just in front of the basket while rising, the hands meet the rim at the slam
    /// (when the simulation's ball arrives), the player hangs on the rim for a beat, then drops back down.
    /// Presentation only: the simulation (make/miss, positions, timing) is unchanged.
    /// </summary>
    public static class DunkPath
    {
        /// <summary>Metres in front of the rim's centre where the dunker meets it (portrait 3/4 view).</summary>
        public const float Approach = 0.45f;
        /// <summary>Side view (landscape): closer, since the rim is seen edge-on.</summary>
        public const float ApproachSide = 0.3f;
        /// <summary>Share of the time after the slam spent hanging on the rim.</summary>
        public const float HangShare = 0.35f;
        /// <summary>Art pixels the hands reach above the rim's centre line (hands over the rim).</summary>
        public const int OverRimPx = 2;

        /// <summary>Where the dunker meets the rim: <paramref name="approach"/> metres from the hoop toward the take-off spot.</summary>
        public static Vec2 MeetPoint(Vec2 start, Vec2 hoop, float approach)
        {
            // Dunkers finish nearly square to the rim, whatever angle they came from.
            var d = start - hoop;
            d = new Vec2(d.x * 0.35f, d.y);
            var dir = d.Magnitude < 0.05f ? new Vec2(0f, 1f) : d.Normalized;
            return hoop + dir * approach;
        }

        /// <param name="start">Take-off spot (court metres).</param>
        /// <param name="hoop">Rim centre on the floor (court metres).</param>
        /// <param name="end">Where the simulation has the player when the leap ends (they land back there).</param>
        /// <param name="u">0..1 through the leap.</param>
        /// <param name="slamU">0..1 point where the ball reaches the rim (the simulation's flight time / leap time).</param>
        /// <param name="liftAtRimPx">Body lift (art pixels) that puts the hands at the rim from the meet point.</param>
        public static DunkFrame At(Vec2 start, Vec2 hoop, Vec2 end, float u, float slamU, int liftAtRimPx, float approach) =>
            At(start, hoop, end, u, slamU, -1f, liftAtRimPx, approach);

        /// <summary>Swings on the rim: peak sideways swing (art pixels) and how many back-and-forth swings in a full hang.</summary>
        public const int SwingAmplitudePx = 3;
        public const float SwingCycles = 1.5f;

        /// <summary>
        /// As above, with <paramref name="hangU"/> = the share of the leap spent hanging on the rim after the slam
        /// (made dunks hang and swing; a value of 0 or less uses the short <see cref="HangShare"/> hang).
        /// </summary>
        public static DunkFrame At(Vec2 start, Vec2 hoop, Vec2 end, float u, float slamU, float hangU, int liftAtRimPx, float approach)
        {
            var meet = MeetPoint(start, hoop, approach);
            bool swings = hangU > 0f;
            slamU = Math.Max(0.15f, Math.Min(swings ? 0.7f : 0.85f, slamU));
            float hangEnd = swings ? Math.Min(0.95f, slamU + hangU) : slamU + HangShare * (1f - slamU);
            var f = new DunkFrame();
            if (u < 0f) u = 0f;
            if (u >= 1f) { f.Ground = end; return f; }
            if (u < slamU)
            {
                float p = u / slamU;
                f.Ground = Vec2.Lerp(start, meet, Smooth(p));
                f.LiftPx = (int)Math.Round(liftAtRimPx * (1f - (1f - p) * (1f - p)));
                f.BallInHands = true;
            }
            else if (u < hangEnd)
            {
                f.Ground = meet;
                // The slam frame is at full reach; hanging sags a pixel.
                bool slamFrame = u - slamU < 0.04f;
                f.LiftPx = slamFrame ? liftAtRimPx : liftAtRimPx - 1;
                f.OnRim = true;
                f.RimDipPx = slamFrame ? 2 : 1;
                if (swings)
                {
                    // A pendulum under the rim that dies down: out, back, out, then hanging still before the drop.
                    float h = (u - slamU) / Math.Max(0.001f, hangEnd - slamU);
                    double swing = Math.Sin(h * SwingCycles * 2.0 * Math.PI) * (1.0 - 0.7 * h);
                    f.SwingPx = (int)Math.Round(swing * SwingAmplitudePx);
                    // The body rises a touch at each end of the swing (it's on an arc).
                    if (!slamFrame && Math.Abs(swing) < 0.35) f.LiftPx = liftAtRimPx - 2;
                }
            }
            else
            {
                float q = (u - hangEnd) / (1f - hangEnd);
                f.Ground = Vec2.Lerp(meet, end, Smooth(q));
                f.LiftPx = (int)Math.Round((liftAtRimPx - 1) * (1f - q * q));
            }
            if (f.LiftPx < 0) f.LiftPx = 0;
            return f;
        }

        /// <summary>
        /// Lift that puts the hands' top at the rim (+<see cref="OverRimPx"/>): rim height on screen minus the
        /// meet point's floor height on screen, minus how high the hands reach above the feet.
        /// </summary>
        public static int LiftToRim(float rimScreenYPx, float meetFloorScreenYPx, int handReachPx) =>
            Math.Max(0, (int)Math.Round(rimScreenYPx - meetFloorScreenYPx) - handReachPx + OverRimPx);

        private static float Smooth(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3f - 2f * t);
        }
    }
}

