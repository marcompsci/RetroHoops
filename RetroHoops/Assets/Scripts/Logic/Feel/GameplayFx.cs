using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 16 Oct 2026 graphics phase G3: the numbers behind the gameplay effects (drawing only; the simulation never reads
    /// them). Floor reflections on shiny floors, shadows that shrink and fade as a player leaves the floor, a short
    /// afterimage trail behind a ball in flight, dust off hard cuts and sweat off tired legs.
    /// </summary>
    public static class GameplayFx
    {
        /// <summary>The floor a court is drawn with (the court's own choice, else its circuit's).</summary>
        public static FloorStyle StyleOf(CourtDef court)
        {
            if (court == null) return FloorStyle.Asphalt;
            return court.floorStyle >= 0 ? (FloorStyle)court.floorStyle
                 : court.circuit == CourtCircuit.League ? FloorStyle.Hardwood
                 : court.circuit == CourtCircuit.Secret ? FloorStyle.NeonGrid : FloorStyle.Asphalt;
        }

        /// <summary>How strongly players are mirrored in the floor: polished hardwood most, then the neon glass and tiles; never outdoors.</summary>
        public static float ReflectionAlpha(FloorStyle style) =>
            style == FloorStyle.Hardwood ? 0.16f : style == FloorStyle.NeonGrid ? 0.13f : style == FloorStyle.Tiles ? 0.08f : 0f;

        /// <summary>A player's drop shadow at a jump height (0 = feet down, 1 = the top of the jump): smaller and fainter higher up.</summary>
        public static void ShadowForJump(float jump01, out float scale, out float alpha)
        {
            float j = Math.Max(0f, Math.Min(1f, jump01));
            scale = 1f - 0.35f * j;
            alpha = 1f - 0.5f * j;
        }

        /// <summary>Afterimages behind a ball in flight (most recent first).</summary>
        public static readonly float[] TrailAlphas = { 0.36f, 0.22f, 0.11f };
        /// <summary>Seconds between trail samples.</summary>
        public const float TrailStep = 1f / 30f;

        /// <summary>Whether a ball leaves a trail: shots and passes in flight (never in hands, never rolling on the floor).</summary>
        public static bool Trails(BallPhase phase, float height) =>
            phase == BallPhase.Shot || phase == BallPhase.Pass || (phase == BallPhase.Loose && height > 1.2f);

        /// <summary>Speed (m/s) a player must be carrying for a cut to kick up dust.</summary>
        public const float CutSpeed = 2.4f;
        /// <summary>Seconds between the velocity samples compared for a cut.</summary>
        public const float CutWindow = 0.12f;

        /// <summary>
        /// How hard a player changed direction between two velocity samples: 0 for a straight line, 1 for a full
        /// reversal. Slow movement never counts. Dust flies at <see cref="DustAt"/> and above.
        /// </summary>
        public static float CutStrength(Vec2 before, Vec2 now)
        {
            float a = before.Magnitude, b = now.Magnitude;
            if (a < CutSpeed || b < CutSpeed) return 0f;
            float cos = (before.x * now.x + before.y * now.y) / (a * b);
            return Math.Max(0f, Math.Min(1f, (1f - cos) / 2f));
        }

        /// <summary>Cut strength that kicks up dust (a turn of about 70 degrees or more).</summary>
        public const float DustAt = 0.33f;

        /// <summary>Stamina below which a moving player drips sweat.</summary>
        public const float SweatStamina = 0.35f;
        /// <summary>Seconds between sweat drops.</summary>
        public const float SweatEvery = 0.55f;

        public static bool Sweaty(float stamina, bool moving) => moving && stamina < SweatStamina;
    }

    /// <summary>Recent ball positions (world units), sampled every <see cref="GameplayFx.TrailStep"/> seconds.</summary>
    public sealed class BallTrail
    {
        private readonly float[] _x, _y;
        private int _count, _head;
        private float _nextAt = float.MinValue;

        public BallTrail(int length = 3)
        {
            _x = new float[Math.Max(1, length)];
            _y = new float[_x.Length];
        }

        public int Count => _count;
        public int Capacity => _x.Length;

        /// <summary>Records the ball's position if a sample is due. Returns whether it took one.</summary>
        public bool Push(float x, float y, float time)
        {
            if (time < _nextAt) return false;
            _nextAt = time + GameplayFx.TrailStep;
            _head = (_head + _x.Length - 1) % _x.Length;
            _x[_head] = x;
            _y[_head] = y;
            if (_count < _x.Length) _count++;
            return true;
        }

        /// <summary>The i-th most recent sample (0 = newest).</summary>
        public void Get(int i, out float x, out float y)
        {
            int k = (_head + i) % _x.Length;
            x = _x[k];
            y = _y[k];
        }

        public void Clear()
        {
            _count = 0;
            _nextAt = float.MinValue;
        }
    }
}
