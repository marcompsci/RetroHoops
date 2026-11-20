using System.Collections.Generic;
using CallerRetroBall.Controls;
using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// I133-I138 (19 Nov 2026, Omari: make the STEAL button and the other buttons feel better to press, and let me see the
    /// whole screen): every frame the buttons learn where the play is (they fade while it's under them), the STEAL button
    /// shows how long until you can reach again, and a missed reach says why.
    /// </summary>
    public sealed partial class GameSceneController
    {
        private readonly List<Vector2> _playPoints = new List<Vector2>(12);

        private void SyncButtonFeel()
        {
            if (_controls == null || _match == null || !_touchShown) return;
            var cam = Camera.main;
            _playPoints.Clear();
            if (cam != null && !_reduceMotion)
            {
                foreach (var p in _match.Players)
                    _playPoints.Add(cam.WorldToScreenPoint(CourtSpace.ToWorld(p.Position, 0.9f)));
                _playPoints.Add(cam.WorldToScreenPoint(CourtSpace.ToWorld(_match.Ball.Position, _match.Ball.Height)));
            }
            _controls.SetPlayPoints(_playPoints);
            if (_controls2 != null) _controls2.SetPlayPoints(null);

            // I134: the dark sweep on STEAL while your reach recovers.
            bool defending = _match.OffenseTeam != _match.Setup.HumanTeam && _match.ControlledIndex >= 0;
            _controls.Defense.SetCooldown(defending ? _match.StealWait01(_match.ControlledIndex) : 0f);
            if (_controls2 != null && _match.SecondControlledIndex >= 0)
            {
                int p2 = _match.SecondControlledIndex;
                bool p2Defending = _match.OffenseTeam != _match.Players[p2].Team;
                _controls2.Defense.SetCooldown(p2Defending ? _match.StealWait01(p2) : 0f);
            }
        }

        /// <summary>I138: after your reach, a word over STEAL when it didn't work (TOO FAR, GET CLOSER, MISSED).</summary>
        private void StealFeedback(MatchEvent e)
        {
            if (_controls == null || e.PlayerIndex < 0) return;
            TouchControls pad = e.PlayerIndex == _match.ControlledIndex ? _controls
                : _controls2 != null && e.PlayerIndex == _match.SecondControlledIndex ? _controls2 : null;
            if (pad == null) return;
            string hint = ControlFeel.StealHint(_match.LastStealOutcome(e.PlayerIndex));
            if (hint.Length > 0) pad.Defense.ShowHint(hint, 0.7f);
        }
    }
}
