using System.Collections;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    // 16 Oct 2026 (Omari): BALLER CITY. LEAVE COURT walks you off a Rise game, a STREETS game or any game started from
    // the city, into the city at that court's gate. Walking off a game that's still on asks first and ends it exactly
    // like QUIT does (no result, no rewards). Steps: CityTransition (Idle → ExitRequested → Validating → Saving →
    // WalkOff → LoadingCity), then the city scene finishes Spawn → FreeRoam.
    public sealed partial class GameSceneController
    {
        private readonly CityTransition _leave = new CityTransition();
        private bool _leaving;
        private string _leaveDistrict, _leaveSpawn;

        /// <summary>Games that can be walked off into the city.</summary>
        private bool CityLinked =>
            _request != null && _link == null && _watch == null && !Demo && !Versus && _request.Mode != GameMode.Tutorial
            && (CityDistricts.TryParseReturn(_request.ReturnTo, out _, out _)
                || _request.Mode == GameMode.Rise || _request.Mode == GameMode.Rival || _request.Mode == GameMode.Streets);

        private void StartCity()
        {
            if (!CityLinked) return;
            _hud.LeaveCourtRequested += LeaveCourt;
            _hud.EnableLeaveCourt();
        }

        /// <summary>A game started from the city: CONTINUE goes back to the same court in the city.</summary>
        private bool ContinueToCity()
        {
            if (_request == null || _request.Mode == GameMode.Rise || _request.Mode == GameMode.Rival || _request.Mode == GameMode.AllStar) return false;
            if (!CityDistricts.TryParseReturn(_request.ReturnTo, out var d, out var s)) return false;
            ArriveInCity(d, s);
            App.SaveCareer();
            SceneFlow.GoTo(SceneNames.City);
            return true;
        }

        private void LeaveCourt()
        {
            if (_leaving || _hud.ConfirmOpen) return;
            var exit = CityDistricts.ExitFor(_request);
            bool live = !_finalShown && _match != null && !_match.IsOver;
            var check = _leave.Request(live, exit.district, exit.spawn);
            if (check == CityTransition.Check.Busy) return;
            if (check == CityTransition.Check.NeedsConfirmation)
            {
                _hud.Confirm("LEAVE COURT?", "The game ends now. It won't count and there are no rewards.", "LEAVE",
                             () => ProceedLeave(true), () => _leave.Validate(false));
                return;
            }
            ProceedLeave(false);
        }

        private void ProceedLeave(bool confirmed)
        {
            if (!_leave.Validate(confirmed)) return;
            // Saving: only where you'll be in the city. The game itself is never applied (same as QUIT).
            bool ok = true;
            try
            {
                ArriveInCity(_leave.District, _leave.Spawn);
                App.SaveCareer();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[BallerCity] Couldn't save before leaving the court: " + e.Message);
                ok = false;
            }
            _leave.Saved(ok);
            if (_leave.State == CityTransitionState.Failed)
            {
                _hud.Toast(_leave.Error, 2f);
                _leave.Recover();
                return;
            }
            _leaveDistrict = _leave.District;
            _leaveSpawn = _leave.Spawn;
            StartCoroutine(WalkOff());
        }

        private static void ArriveInCity(string district, string spawn)
        {
            var career = App.Career;
            if (career == null) return;
            if (career.city == null) career.city = new CitySaveData();
            career.city.Arrive(district, spawn);
            // Off to the city, not the menu: the panels these modes open on the way home stay closed.
            App.OpenStreetsOnMenu = false;
            App.OpenParkOnMenu = false;
        }

        /// <summary>WalkOff: you walk to the nearest sideline; teammates turn and wave; then the wipe into the city.</summary>
        private IEnumerator WalkOff()
        {
            _leaving = true;
            HangUp();
            _hud.ShowPause(false);
            _hud.HideFinal();
            _hud.SetHudVisible(false);
            if (_streetsHud != null) _streetsHud.SetVisible(false);
            SetTouchVisible(false);
            Audio.AudioManager.SetAmbience(false);

            bool reduced = App.Career?.settings != null && App.Career.settings.reduceMotion;
            float seconds = reduced ? 0.3f : 1.6f;
            int me = -1;
            for (int i = 0; i < _playerViews.Length; i++)
                if (_match.IsHumanControlled(i)) { me = i; break; }
            if (me < 0) me = 0;
            var players = _match.Players;
            var start = players[me].Motion.position;
            float side = start.x >= 0f ? 1f : -1f;
            var dir = new Vec2(side, 0.35f).Normalized;
            var facing = Movement.FacingOf(dir);
            float t = 0f;
            while (t < seconds)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                var at = start + dir * (2.4f * t);
                _playerViews[me].SyncScripted(dt, at, facing, true, false);
                for (int i = 0; i < _playerViews.Length; i++)
                {
                    if (i == me) continue;
                    var p = players[i];
                    bool mate = p.Team == players[me].Team;
                    var look = Movement.FacingOf(at - p.Motion.position);
                    _playerViews[i].SyncScripted(dt, p.Motion.position, mate ? look : p.Motion.facing, false, mate && t > 0.2f);
                }
                yield return null;
            }
            _leave.WalkedOff();
            // LoadingCity: the wipe covers the load; the city scene picks up Spawn and FreeRoam.
            if (!SceneFlow.GoTo(SceneNames.City))
            {
                // Another load was already running: try again next frame rather than strand the player.
                yield return null;
                SceneFlow.GoTo(SceneNames.City);
            }
        }
    }
}
