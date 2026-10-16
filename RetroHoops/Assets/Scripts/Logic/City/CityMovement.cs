using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Walking in Baller City: a body at the feet (a small box) that speeds up and slows down smoothly, and slides
    /// along walls instead of sticking to them. Pure maths over a <see cref="CityMap"/>, shared by the player and NPCs.
    /// </summary>
    public struct CityBody
    {
        public Vec2 Position;
        public Vec2 Velocity;
        /// <summary>0 down (toward the camera), 1 up, 2 left, 3 right.</summary>
        public int Facing;
        /// <summary>Distance walked (drives the walk cycle).</summary>
        public float Stride;
    }

    public static class CityMovement
    {
        public const float WalkSpeed = 3.4f;
        public const float SprintSpeed = 5.6f;
        public const float Accel = 18f;
        /// <summary>Half the size of the feet box (tiles).</summary>
        public const float HalfW = 0.3f, HalfH = 0.18f;
        private const float Skin = 0.001f;

        /// <summary>Moves a body one step from a stick (magnitude 0..1, +y up the screen).</summary>
        public static void Step(ref CityBody b, CityMap map, Vec2 stick, bool sprint, float dt)
        {
            float mag = Math.Min(1f, stick.Magnitude);
            var dir = mag > 0.001f ? stick / stick.Magnitude : Vec2.Zero;
            float top = sprint ? SprintSpeed : WalkSpeed;
            var target = dir * (top * mag);
            var dv = target - b.Velocity;
            float maxDv = Accel * dt;
            if (dv.Magnitude > maxDv) dv = dv.Normalized * maxDv;
            b.Velocity += dv;
            if (mag > 0.15f) b.Facing = FacingOf(dir);
            MoveAndSlide(ref b, map, b.Velocity * dt);
        }

        /// <summary>Which of the four facings a direction is.</summary>
        public static int FacingOf(Vec2 dir)
        {
            if (Math.Abs(dir.x) > Math.Abs(dir.y) * 1.1f) return dir.x < 0f ? 2 : 3;
            return dir.y > 0f ? 1 : 0;
        }

        /// <summary>Moves by <paramref name="delta"/> in small steps, x then y, stopping at solid tiles (so it slides along walls).</summary>
        public static void MoveAndSlide(ref CityBody b, CityMap map, Vec2 delta)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(delta.Magnitude / 0.2f));
            var step = delta / steps;
            var before = b.Position;
            for (int i = 0; i < steps; i++)
            {
                var p = b.Position;
                float nx = p.x + step.x;
                if (Overlaps(map, nx, p.y)) { nx = p.x; b.Velocity = new Vec2(0f, b.Velocity.y); }
                float ny = p.y + step.y;
                if (Overlaps(map, nx, ny)) { ny = p.y; b.Velocity = new Vec2(b.Velocity.x, 0f); }
                b.Position = new Vec2(nx, ny);
            }
            b.Stride += Vec2.Distance(before, b.Position);
        }

        /// <summary>True when a feet box centred at (x, y) touches a blocked tile.</summary>
        public static bool Overlaps(CityMap map, float x, float y)
        {
            int x0 = (int)Math.Floor(x - HalfW + Skin), x1 = (int)Math.Floor(x + HalfW - Skin);
            int y0 = (int)Math.Floor(y - HalfH + Skin), y1 = (int)Math.Floor(y + HalfH - Skin);
            for (int ty = y0; ty <= y1; ty++)
                for (int tx = x0; tx <= x1; tx++)
                    if (map.Blocked(tx, ty)) return true;
            return false;
        }

        /// <summary>A safe place to stand at or near <paramref name="p"/> (the save might point into a wall).</summary>
        public static Vec2 Settle(CityMap map, Vec2 p)
        {
            if (!float.IsNaN(p.x) && !float.IsNaN(p.y) && !Overlaps(map, p.x, p.y)) return p;
            return map.NearestWalkable(p);
        }
    }

    /// <summary>The city camera: follows the player smoothly and never shows past the edge of the district.</summary>
    public static class CityCamera
    {
        public const float Stiffness = 6f;

        /// <summary>Next camera centre: eased toward <paramref name="target"/>, then kept inside the map (centred when the map is smaller than the view).</summary>
        public static Vec2 Follow(Vec2 current, Vec2 target, float halfW, float halfH, int mapW, int mapH, float dt, bool snap = false)
        {
            var next = snap ? target : Vec2.Lerp(current, target, 1f - (float)Math.Exp(-Stiffness * dt));
            return Clamp(next, halfW, halfH, mapW, mapH);
        }

        public static Vec2 Clamp(Vec2 c, float halfW, float halfH, int mapW, int mapH)
        {
            float x = halfW * 2f >= mapW ? mapW * 0.5f : Math.Max(halfW, Math.Min(mapW - halfW, c.x));
            float y = halfH * 2f >= mapH ? mapH * 0.5f : Math.Max(halfH, Math.Min(mapH - halfH, c.y));
            return new Vec2(x, y);
        }

        /// <summary>Zoom steps: how many tiles fit across the screen (bounded).</summary>
        public static readonly float[] TilesAcross = { 11f, 14f, 18f };
    }
}
