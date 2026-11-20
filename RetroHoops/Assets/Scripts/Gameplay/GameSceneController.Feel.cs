using System.Collections.Generic;
using CallerRetroBall.Controls;
using CallerRetroBall.Core;
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
            // I180: BLOCK (SHOOT on defence) shows its jump recovery the same way.
            _controls.Shoot.SetCooldown(defending ? _match.JumpWait01(_match.ControlledIndex) : 0f);
            // I209 + I210: the ring glows gold when pressing now would reach.
            _controls.Defense.Glow = defending && _match.InStealReach(_match.ControlledIndex);
            _controls.Shoot.Glow = defending && _match.InBlockRange(_match.ControlledIndex);
            // I181: SWITCH with nobody to switch with says so.
            if (_match.SwitchRefusedAt > _lastSwitchRefused)
            {
                _lastSwitchRefused = _match.SwitchRefusedAt;
                if (defending) _controls.Pass.ShowHint("ALREADY ON THE BALL", 0.8f);
            }
            SyncGreenBuzz();
            if (_controls2 != null && _match.SecondControlledIndex >= 0)
            {
                int p2 = _match.SecondControlledIndex;
                bool p2Defending = _match.OffenseTeam != _match.Players[p2].Team;
                _controls2.Defense.SetCooldown(p2Defending ? _match.StealWait01(p2) : 0f);
                _controls2.Shoot.SetCooldown(p2Defending ? _match.JumpWait01(p2) : 0f);
                _controls2.Defense.Glow = p2Defending && _match.InStealReach(p2);
                _controls2.Shoot.Glow = p2Defending && _match.InBlockRange(p2);
            }
        }

        private float _lastSwitchRefused = -99f;
        private bool _inGreen;

        /// <summary>
        /// I179 FEEL THE GREEN (Settings, off by default): a light buzz the moment your shot meter enters the GREEN window,
        /// so the release can be timed by feel as well as by eye.
        /// </summary>
        private void SyncGreenBuzz()
        {
            int i = _match.ChargingIndex;
            bool on = App.Career != null && App.Career.settings.greenBuzz && i >= 0 && _match.IsHumanControlled(i);
            if (!on) { _inGreen = false; return; }
            var shooter = _match.Players[i];
            bool green = ShotModel.Grade(_match.ChargeMeter, shooter.Def.attributes.shooting, _match.TuningFor(i)) == TimingGrade.Green;
            if (green && !_inGreen) Core.Haptics.Light();
            _inGreen = green;
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
