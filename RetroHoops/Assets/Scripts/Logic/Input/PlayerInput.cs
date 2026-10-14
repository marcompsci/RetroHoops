using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum ActionButton
    {
        Pass = 0,
        Shoot = 1,
        Defense = 2,
        Call = 3,
        /// <summary>Attack the rim and dunk it (a layup if you can't dunk from there).</summary>
        Dunk = 4,
        /// <summary>Attack the rim and lay it in.</summary>
        Layup = 5,
    }

    /// <summary>One frame of controller intent, produced by touch/keyboard/gamepad adapters.</summary>
    public struct PlayerInput
    {
        /// <summary>Stick direction, magnitude 0..1. +y is toward half-court (away from the hoop).</summary>
        public Vec2 Move;
        /// <summary>Shoot was pressed (edge). Starts the meter if a shot is allowed; on defense, jumps.</summary>
        public bool ShootPressed;
        /// <summary>Shoot is being held. Releasing it releases the shot.</summary>
        public bool ShootHeld;
        /// <summary>Pass (with the ball) or ask for the ball (teammate has it). On defense: switch onto the ball.</summary>
        public bool PassPressed;
        /// <summary>Defense button: reach for a steal.</summary>
        public bool DefensePressed;
        /// <summary>DUNK (with the ball): drive to the rim and throw it down.</summary>
        public bool DunkPressed;
        /// <summary>LAYUP (with the ball): drive to the rim and lay it in.</summary>
        public bool LayupPressed;
        /// <summary>Play chosen from the CALL menu this frame (offense only).</summary>
        public PlayCall CallPlay;

        public static PlayerInput None => default;
    }

    /// <summary>
    /// Buffers action presses briefly so a tap slightly before an action becomes legal
    /// (e.g. just before catching the ball) still counts. Forgiving without being sticky.
    /// </summary>
    public sealed class InputBuffer
    {
        public const float DefaultWindow = 0.15f;

        private readonly float _window;
        private readonly Dictionary<ActionButton, float> _pressedAt = new Dictionary<ActionButton, float>();

        public InputBuffer(float window = DefaultWindow)
        {
            _window = window;
        }

        public void Press(ActionButton button, float time) => _pressedAt[button] = time;

        /// <summary>True (once) if the button was pressed within the window before <paramref name="now"/>.</summary>
        public bool Consume(ActionButton button, float now)
        {
            if (!_pressedAt.TryGetValue(button, out float at)) return false;
            _pressedAt.Remove(button);
            return now - at <= _window && now >= at;
        }

        public bool IsBuffered(ActionButton button, float now) =>
            _pressedAt.TryGetValue(button, out float at) && now - at <= _window && now >= at;

        public void Clear() => _pressedAt.Clear();
    }

    /// <summary>Floating virtual-joystick maths (pure so it can be tested without touches).</summary>
    public static class JoystickMath
    {
        /// <summary>
        /// 14 Oct 2026: a stick that follows the thumb. When the thumb is more than <paramref name="radius"/> from the
        /// centre, the centre moves toward it so the thumb sits exactly on the rim. Otherwise the centre stays put.
        /// </summary>
        public static Vec2 FollowOrigin(Vec2 origin, Vec2 thumb, float radius)
        {
            var d = new Vec2(thumb.x - origin.x, thumb.y - origin.y);
            float len = d.Magnitude;
            if (radius <= 0f || len <= radius) return origin;
            float k = (len - radius) / len;
            return new Vec2(origin.x + d.x * k, origin.y + d.y * k);
        }

        /// <summary>
        /// Converts a drag offset (screen pixels) into a stick vector. Inside the dead zone the
        /// stick reads zero; beyond it the magnitude is rescaled so it still reaches 0..1 smoothly.
        /// </summary>
        public static Vec2 Evaluate(Vec2 dragPixels, float radiusPixels, float deadZone = 0.12f)
        {
            if (radiusPixels <= 0f) return Vec2.Zero;
            var v = dragPixels / radiusPixels;
            float m = v.Magnitude;
            if (m <= deadZone) return Vec2.Zero;
            float scaled = Math.Min(1f, (m - deadZone) / (1f - deadZone));
            return v / m * scaled;
        }

        /// <summary>Knob offset for display: the drag clamped to the joystick radius.</summary>
        public static Vec2 KnobOffset(Vec2 dragPixels, float radiusPixels) => Vec2.ClampMagnitude(dragPixels, radiusPixels);
    }
}
