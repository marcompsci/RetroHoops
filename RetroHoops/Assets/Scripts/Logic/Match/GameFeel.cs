using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// I41–I50 game feel: tuning helpers for the AI that are easier to test on their own.
    /// Nothing here changes ratings or speeds; it changes what the AI decides.
    /// </summary>
    public static class GameFeel
    {
        /// <summary>I43: a drive goes up once it's inside layup range, so it's a layup or dunk and never a short jumper.</summary>
        public static float FinishRange(ShotTuning t) => t.layupMaxDistance * 0.95f;

        /// <summary>I43: a drive that hasn't gained this much ground (m) in <see cref="StallSeconds"/> is called off.</summary>
        public const float StallGain = 0.25f;
        public const float StallSeconds = 0.6f;

        /// <summary>I48: a teammate at least this open (m to the nearest defender) gets the kick-out.</summary>
        public const float KickOpenness = 2.0f;

        /// <summary>
        /// I42 TEAMMATES KEEP UP: the human's AI teammates think at least as well as the other bench —
        /// reaction, decisions and mistakes match the better of the two. Shooting touch only meets
        /// the other bench halfway, so a harder difficulty is still harder.
        /// </summary>
        public static DifficultyDef TeammateFor(DifficultyDef friendly, DifficultyDef opponent)
        {
            if (opponent == null) return friendly;
            return new DifficultyDef
            {
                id = friendly.id,
                displayName = friendly.displayName,
                reactionTime = Math.Min(friendly.reactionTime, opponent.reactionTime),
                decisionQuality = Math.Max(friendly.decisionQuality, opponent.decisionQuality),
                shotQualityThreshold = Math.Max(friendly.shotQualityThreshold, opponent.shotQualityThreshold),
                errorRate = Math.Min(friendly.errorRate, opponent.errorRate),
                movementScale = Math.Max(friendly.movementScale, Math.Min(1f, opponent.movementScale)),
                releaseAccuracy = Math.Max(friendly.releaseAccuracy, (friendly.releaseAccuracy + opponent.releaseAccuracy) * 0.5f),
            };
        }

        /// <summary>
        /// I43: extra drive appeal when the handler has room on his man (m between them) and isn't parked deep
        /// beyond the arc. Zero when he's smothered.
        /// </summary>
        public static float DriveEdge(float separation, float distToHoop)
        {
            if (separation < 0.9f || distToHoop > 7.5f) return 0f;
            return Math.Min(0.25f, (separation - 0.9f) * 0.3f);
        }

        /// <summary>
        /// I47 LATE-CLOCK IQ: 0 with time to work, rising to 0.6 between 6 s and 3 s left, then 1 (shoot now).
        /// Modes without a real shot clock (99 s or more) never feel it.
        /// </summary>
        public static float ClockUrgency(float shotClock, float clockSeconds)
        {
            if (clockSeconds >= 99f) return 0f;
            if (shotClock < 3f) return 1f;
            if (shotClock < 6f) return (6f - shotClock) / 3f * 0.6f;
            return 0f;
        }

        /// <summary>I48 CLOSE-OUTS: a defender closing out stops this far (m) short of the catch, between it and the rim.</summary>
        public const float CloseOutGap = 0.8f;

        /// <summary>I46: how far outside steal range (m) a defender will step up to reach, and how tight he gets to do it.</summary>
        public const float ReachStep = 0.5f;
        public const float ReachGap = 0.55f;

        /// <summary>
        /// I46 PRESSURE: chance per decision that an on-ball defender reaches. Rises with the gamble tendency and with
        /// difficulty (decision quality); a pressing scheme doubles it.
        /// </summary>
        public static float ReachChance(float gambleForSteals, float decisionQuality, bool pressing)
        {
            float c = gambleForSteals * (0.2f + 0.35f * decisionQuality);
            return pressing ? c * 2f : c;
        }
    }

    public sealed partial class MatchSimulation
    {
        /// <summary>I43: drives that finished at the rim this game (for tests and tuning).</summary>
        public int DriveFinishes { get; private set; }

        /// <summary>I48: drive-and-kick passes this game.</summary>
        public int KickOuts { get; private set; }

        /// <summary>I43 ATTACK THE RIM: commit to the drive, going around the man in front.</summary>
        private void StartDrive(PlayerRuntimeState p, AiState s, float dist)
        {
            var court = Setup.Court;
            int d = DefenderOf(p.Index);
            float side = p.Position.x >= court.Hoop.x ? 1f : -1f;
            if (d >= 0)
            {
                // Go to the side the defender isn't shading (no extra random draw: seeded games keep their shape).
                var toHoop = court.Hoop - p.Position;
                var perp = new Vec2(-toHoop.y, toHoop.x);
                float lean = Vec2.Dot(Players[d].Position - p.Position, perp);
                if (Math.Abs(lean) > 0.05f) side = lean > 0f ? -1f : 1f;
            }
            s.Intent = AiIntent.Drive;
            s.DriveSide = side;
            s.DriveBest = dist;
            s.DriveCheckAt = Time + GameFeel.StallSeconds;
            s.Target = DriveTarget(p, side);
            s.IntentUntil = Time + 1.0f + dist * 0.35f;
        }

        private Vec2 DriveTarget(PlayerRuntimeState p, float side)
        {
            var court = Setup.Court;
            var toHoop = court.Hoop - p.Position;
            float dist = toHoop.Magnitude;
            if (dist < 0.01f) return court.Hoop;
            var dir = toHoop / dist;
            var perp = new Vec2(-dir.y, dir.x) * side;
            // Far out: swing wide of the man; close in: straight at the front of the rim.
            float wide = Math.Min(1.2f, Math.Max(0f, (dist - 2f) * 0.35f));
            var aim = p.Position + dir * Math.Min(2f, dist) + perp * wide;
            var finish = court.Hoop - dir * 0.6f + new Vec2(0f, 0.3f);
            return court.Clamp(dist > 3f ? aim : finish);
        }

        /// <summary>
        /// I43 + I48: an AI ball handler on a drive keeps going, finishes at the rim, or kicks it out when help
        /// collapses on him. Returns false when the drive is over and the normal decision should run.
        /// </summary>
        private bool ContinueDrive(PlayerRuntimeState p, AiState s, DifficultyDef profile, float dist)
        {
            if (s.Intent != AiIntent.Drive || Time >= s.IntentUntil || MustClear || MustInbound) return false;

            float near = NearestOpponentDistance(p);
            if (dist <= GameFeel.FinishRange(Setup.Shot))
            {
                if (!CanShoot(p.Index)) return false;
                // Walled up at the rim with a shooter wide open: kick it (better decision makers see it more).
                if (near < 0.6f && TryKickOut(p, profile)) return true;
                DriveFinishes++;
                s.Intent = AiIntent.Hold;
                s.Target = p.Position;
                BeginCharge(p.Index, SampleReleaseMeter(p, profile));
                return true;
            }

            // Help came: kick out if someone's open, otherwise read it fresh.
            if (near < 0.8f && !LaneOpenToRim(p))
            {
                if (TryKickOut(p, profile)) return true;
            }

            if (Time >= s.DriveCheckAt)
            {
                if (s.DriveBest - dist < GameFeel.StallGain)
                {
                    s.IntentUntil = Time; // stalled: pick something else
                    return false;
                }
                s.DriveBest = dist;
                s.DriveCheckAt = Time + GameFeel.StallSeconds;
            }
            s.Target = DriveTarget(p, s.DriveSide);
            return true;
        }

        /// <summary>I48: close-outs this game (for tests and tuning).</summary>
        public int CloseOuts { get; private set; }

        /// <summary>
        /// I48 CLOSE-OUTS: when the pass is in the air to your man out on the floor, sprint to where he'll catch it and
        /// land between him and the rim, so the kick-out three is contested instead of free.
        /// </summary>
        private bool CloseOut(PlayerRuntimeState p, AiState s, PlayerRuntimeState man)
        {
            if (Ball.Phase != BallPhase.Pass || Ball.TargetIndex != man.Index) return false;
            var court = Setup.Court;
            var catchAt = Ball.FlightTo;
            if (court.DistanceToHoop(catchAt) < 3f) return false; // inside: the normal help rules handle it
            var toHoop = court.Hoop - catchAt;
            if (toHoop.SqrMagnitude < 0.01f) return false;
            if (s.Intent != AiIntent.Guard || Vec2.Distance(s.Target, catchAt) > 1.5f) CloseOuts++;
            s.Intent = AiIntent.Guard;
            s.Target = court.Clamp(catchAt + toHoop.Normalized * GameFeel.CloseOutGap);
            return true;
        }

        private float SeparationFromMan(PlayerRuntimeState p)
        {
            int d = DefenderOf(p.Index);
            return d >= 0 ? Vec2.Distance(Players[d].Position, p.Position) : 9f;
        }

        private bool TryKickOut(PlayerRuntimeState p, DifficultyDef profile)
        {
            int target = SelectPassTarget(p.Index, Vec2.Zero);
            if (target < 0 || OpennessOf(Players[target]) < GameFeel.KickOpenness) return false;
            if (_rng.NextFloat() > profile.decisionQuality) return false;
            KickOuts++;
            _ai[p.Index].Intent = AiIntent.Hold;
            PassFrom(p.Index, Vec2.Zero, target);
            return true;
        }
    }
}
