using System;

namespace CallerRetroBall.Logic
{
    public enum BallPhase
    {
        /// <summary>A player has it (dribbling or holding).</summary>
        Held = 0,
        /// <summary>On the floor or in the air with nobody in control.</summary>
        Loose = 1,
        /// <summary>In flight between teammates (or toward an interceptor).</summary>
        Pass = 2,
        /// <summary>In flight toward the rim; outcome decided at release.</summary>
        Shot = 3,
    }

    [Serializable]
    public class BallTuning
    {
        public float gravity = -9.8f;
        /// <summary>Fraction of vertical speed kept on each floor bounce.</summary>
        public float bounceRestitution = 0.55f;
        /// <summary>Horizontal speed lost per second while rolling (m/s²).</summary>
        public float rollingFriction = 2.2f;
        /// <summary>A loose ball can be grabbed within this ground distance…</summary>
        public float pickupRadius = 0.6f;
        /// <summary>…and below this height (m).</summary>
        public float pickupMaxHeight = 1.4f;
        public float dribblePeriod = 0.42f;
        public float dribbleTopHeight = 0.95f;
        public float dribbleFloorHeight = 0.12f;

        public static BallTuning Default => new BallTuning();
    }

    /// <summary>
    /// Ball state in court space. Height is simulated separately from the ground plane so
    /// rebounds and loose balls are predictable (no unstable 3D physics).
    /// </summary>
    public sealed class BallState
    {
        public BallPhase Phase = BallPhase.Held;
        public int HolderIndex = -1;
        public Vec2 Position;
        public Vec2 Velocity;
        public float Height;
        public float VerticalVelocity;
        public float DribbleTime;

        // Flight (Pass / Shot)
        public Vec2 FlightFrom;
        public Vec2 FlightTo;
        public float FlightTime;
        public float FlightDuration;
        public float FlightStartHeight;
        public float FlightEndHeight;
        public float FlightArc;
        /// <summary>The shot in the air is a bank shot (off the glass).</summary>
        public bool ShotBank;
        /// <summary>Pass receiver (or interceptor); -1 for shots.</summary>
        public int TargetIndex = -1;
        public int PasserIndex = -1;
        public PassType PassType;
        public int ShooterIndex = -1;
        public bool ShotWillScore;
        public int ShotPoints;
        public TimingGrade ShotGrade;
        /// <summary>Kind of the shot in the air (or the last one taken); used for presentation.</summary>
        public ShotType ShotType;

        public bool InFlight => Phase == BallPhase.Pass || Phase == BallPhase.Shot;

        public bool IsHeld => Phase == BallPhase.Held && HolderIndex >= 0;
    }

    public static class BallPhysics
    {
        /// <summary>Dribble height for a given dribble time: a bouncing |sin| curve.</summary>
        public static float DribbleHeight(float time, BallTuning t)
        {
            double phase = Math.Abs(Math.Sin(Math.PI * time / t.dribblePeriod));
            return t.dribbleFloorHeight + (float)phase * (t.dribbleTopHeight - t.dribbleFloorHeight);
        }

        /// <summary>Advances a loose ball: gravity, floor bounces, rolling friction, court walls.</summary>
        public static void StepLoose(BallState b, float dt, CourtGeometry court, BallTuning t) => StepLoose(b, dt, court, t, true);

        /// <summary>As above; <paramref name="walls"/> false lets the ball roll out of bounds (1-on-1).</summary>
        public static void StepLoose(BallState b, float dt, CourtGeometry court, BallTuning t, bool walls)
        {
            if (b.Phase != BallPhase.Loose || dt <= 0f) return;

            b.VerticalVelocity += t.gravity * dt;
            b.Height += b.VerticalVelocity * dt;
            if (b.Height <= 0f)
            {
                b.Height = 0f;
                b.VerticalVelocity = Math.Abs(b.VerticalVelocity) * t.bounceRestitution;
                if (b.VerticalVelocity < 0.4f) b.VerticalVelocity = 0f;
            }

            if (b.Height <= 0.01f)
            {
                float speed = b.Velocity.Magnitude;
                float newSpeed = Math.Max(0f, speed - t.rollingFriction * dt);
                b.Velocity = speed > 1e-5f ? b.Velocity * (newSpeed / speed) : Vec2.Zero;
            }

            b.Position = b.Position + b.Velocity * dt;

            if (!walls) return;
            // Arcade walls: the ball bounces back in off the lines instead of going out.
            float hw = court.HalfWidth;
            if (b.Position.x < -hw) { b.Position.x = -hw; b.Velocity.x = Math.Abs(b.Velocity.x) * 0.6f; }
            if (b.Position.x > hw) { b.Position.x = hw; b.Velocity.x = -Math.Abs(b.Velocity.x) * 0.6f; }
            if (b.Position.y < 0f) { b.Position.y = 0f; b.Velocity.y = Math.Abs(b.Velocity.y) * 0.6f; }
            if (b.Position.y > court.depth) { b.Position.y = court.depth; b.Velocity.y = -Math.Abs(b.Velocity.y) * 0.6f; }
        }

        public static bool CanPickUp(BallState b, Vec2 playerPosition, BallTuning t)
        {
            return b.Phase == BallPhase.Loose
                   && b.Height <= t.pickupMaxHeight
                   && Vec2.Distance(b.Position, playerPosition) <= t.pickupRadius;
        }
    }
}
