using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 14 Oct 2026 (Omari's request): offensive skill moves done with the movement stick. Spin and jab are
    /// shown in the moves list; the other three are secret until you pull them off. Bank layups aren't a
    /// gesture: they happen on their own (see MatchSimulation.IsBankAngle).
    /// </summary>
    public enum SkillMoveKind
    {
        None = 0,
        /// <summary>Swirl the stick half a circle (or more) quickly.</summary>
        Spin = 1,
        /// <summary>Standing almost still: flick the stick at the rim and let go.</summary>
        Jab = 2,
        /// <summary>Secret. At full speed, let go of the stick for a blink, then push the same way again.</summary>
        Hesi = 3,
        /// <summary>Secret. Going at the rim with a defender close: snap the stick straight back.</summary>
        StepBack = 4,
        /// <summary>Secret. Wiggle the stick side to side fast: left, right, left.</summary>
        Shake = 5,
    }

    /// <summary>
    /// Recognises skill-move gestures from the stick, one sample per simulation step. It is a pure function of
    /// the samples (time, stick, direction to the rim, speed), so two phones and replays see the same moves.
    /// All directions are in court space.
    /// </summary>
    public sealed class StickGestures
    {
        public const float SpinWindow = 0.45f;
        public const float SpinDegrees = 200f;
        public const float JabWindow = 0.24f;
        public const float JabMaxSpeed = 1.3f;
        public const float HesiMinSpeed = 3.0f;
        public const float HesiPauseMin = 0.05f;
        public const float HesiPauseMax = 0.26f;
        public const float StepBackWindow = 0.2f;
        public const float ShakeWindow = 0.55f;
        public const float Pushed = 0.6f;
        public const float Neutral = 0.25f;

        private struct Sample
        {
            public float T;
            public Vec2 Stick;
            public float Speed;
        }

        private const int Capacity = 64;
        private readonly Sample[] _s = new Sample[Capacity];
        private int _count;
        private int _head; // index of the newest sample

        public void Clear() => _count = 0;

        private Sample At(int back) => _s[((_head - back) % Capacity + Capacity) % Capacity];

        /// <summary>
        /// Adds a sample and returns a move when one was just completed (the history is then cleared, so a move
        /// fires once).
        /// </summary>
        public SkillMoveKind Feed(float time, Vec2 stick, Vec2 toHoop, float speed)
        {
            _head = (_head + 1) % Capacity;
            _s[_head] = new Sample { T = time, Stick = stick, Speed = speed };
            if (_count < Capacity) _count++;
            if (toHoop.SqrMagnitude < 0.0001f) return SkillMoveKind.None;
            var hoop = toHoop.Normalized;

            var move = Detect(time, hoop);
            if (move != SkillMoveKind.None) Clear();
            return move;
        }

        private SkillMoveKind Detect(float now, Vec2 hoop)
        {
            if (IsSpin(now)) return SkillMoveKind.Spin;
            if (IsShake(now, hoop)) return SkillMoveKind.Shake;
            if (IsStepBack(now, hoop)) return SkillMoveKind.StepBack;
            if (IsHesi(now)) return SkillMoveKind.Hesi;
            if (IsJab(now, hoop)) return SkillMoveKind.Jab;
            return SkillMoveKind.None;
        }

        private static float Angle(Vec2 v) => (float)Math.Atan2(v.y, v.x);

        private static float Wrap(float a)
        {
            while (a > Math.PI) a -= (float)(2 * Math.PI);
            while (a < -Math.PI) a += (float)(2 * Math.PI);
            return a;
        }

        private static float Cos(Vec2 a, Vec2 b)
        {
            float m = a.Magnitude * b.Magnitude;
            return m < 1e-5f ? 0f : (a.x * b.x + a.y * b.y) / m;
        }

        /// <summary>The stick, held over halfway, turned at least <see cref="SpinDegrees"/> one way in the window.</summary>
        private bool IsSpin(float now)
        {
            if (_count < 4 || At(0).Stick.Magnitude < Pushed) return false;
            float sum = 0f;
            for (int k = 1; k < _count; k++)
            {
                var newer = At(k - 1);
                var older = At(k);
                if (now - older.T > SpinWindow || older.Stick.Magnitude < Pushed) break;
                float step = Wrap(Angle(newer.Stick) - Angle(older.Stick));
                if (Math.Abs(step) > 1.05f) break; // a jump across (a crossover), not a swirl
                sum += step;
            }
            return Math.Abs(sum) * 180f / Math.PI >= SpinDegrees;
        }

        /// <summary>Left, right, left (or right, left, right) across the line to the rim, fast and hard.</summary>
        private bool IsShake(float now, Vec2 hoop)
        {
            var perp = new Vec2(-hoop.y, hoop.x);
            int extremes = 0, lastSign = 0;
            for (int k = 0; k < _count; k++)
            {
                var s = At(k);
                if (now - s.T > ShakeWindow) break;
                float side = s.Stick.x * perp.x + s.Stick.y * perp.y;
                int sign = side >= Pushed ? 1 : side <= -Pushed ? -1 : 0;
                if (sign == 0 || sign == lastSign) continue;
                lastSign = sign;
                extremes++;
            }
            // The newest sample must be one of the swings, so the move fires as the third swing lands.
            float now0 = At(0).Stick.x * perp.x + At(0).Stick.y * perp.y;
            return extremes >= 3 && Math.Abs(now0) >= Pushed;
        }

        /// <summary>Pushing at the rim, then snapped straight back away from it within <see cref="StepBackWindow"/>.</summary>
        private bool IsStepBack(float now, Vec2 hoop)
        {
            var cur = At(0).Stick;
            if (cur.Magnitude < 0.7f || Cos(cur, hoop) > -0.77f) return false; // within ~40° of straight back
            // A snap, not a swirl: the stick crossed the middle (or jumped across) on the way back.
            bool snapped = false;
            for (int k = 1; k < _count; k++)
            {
                var s = At(k);
                if (now - s.T > StepBackWindow) break;
                var newer = At(k - 1);
                if (s.Stick.Magnitude < 0.5f || Math.Abs(Wrap(Angle(newer.Stick) - Angle(s.Stick))) > 1.75f) snapped = true;
                if (snapped && s.Stick.Magnitude >= 0.7f && Cos(s.Stick, hoop) >= 0.64f && s.Speed >= 1.0f) return true; // was going at the rim
            }
            return false;
        }

        /// <summary>
        /// At speed, a short let-go (<see cref="HesiPauseMin"/>–<see cref="HesiPauseMax"/>) and then the same way again.
        /// </summary>
        private bool IsHesi(float now)
        {
            var cur = At(0).Stick;
            if (cur.Magnitude < 0.7f || _count < 3 || At(1).Stick.Magnitude >= Neutral) return false; // just pushed again
            // Walk back through the pause to the push before it.
            int k = 1;
            float pauseStart = At(1).T;
            while (k < _count && At(k).Stick.Magnitude < Neutral)
            {
                pauseStart = At(k).T;
                k++;
            }
            if (k >= _count) return false;
            var before = At(k);
            float pause = At(0).T - pauseStart;
            if (pause < HesiPauseMin || pause > HesiPauseMax) return false;
            return before.Stick.Magnitude >= 0.7f && before.Speed >= HesiMinSpeed && Cos(before.Stick, cur) >= 0.7f;
        }

        /// <summary>Nearly standing still: a quick hard flick at the rim (under <see cref="JabWindow"/>), then let go.</summary>
        private bool IsJab(float now, Vec2 hoop)
        {
            if (At(0).Stick.Magnitude >= Neutral || _count < 3) return false;
            var flick = At(1);
            if (flick.Stick.Magnitude < 0.75f || Cos(flick.Stick, hoop) < 0.7f) return false; // released this step
            for (int k = 1; k < _count; k++)
            {
                var s = At(k);
                if (now - s.T > JabWindow || s.Speed > JabMaxSpeed) return false; // held too long, or moving: a drive
                if (s.Stick.Magnitude < Neutral) return true;                     // neutral before the flick: a clean jab
            }
            return false;
        }
    }
}
