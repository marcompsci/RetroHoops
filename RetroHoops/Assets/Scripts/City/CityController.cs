using System;
using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CallerRetroBall.City
{
    /// <summary>
    /// 16 Oct 2026 (Omari): BALLER CITY, the explorable city (CityScene's root). Walk around First Callers, the
    /// Blacktop and the Baller Gym; walk up to a court, a door or a person and tap the action button. Every game is
    /// one of the game's own modes and brings you back to the same spot. Offline: the people out today come from a
    /// fixed daily schedule on the phone, never from a server.
    /// The rules (maps, walking, prompts, schedules, choices, saves) are engine-free in Logic/City and tested there;
    /// this class draws them and wires the touch controls.
    /// </summary>
    public sealed class CityController : MonoBehaviour
    {
        private const int MaxNpcs = 12;
        private static int _zoom = 1;

        private CityMap _map;
        private CityWorld _world;
        private CityHud _hud;
        private Camera _camera;
        private CityBody _me;
        private CityActorView _meView;
        private Vec2 _cam;
        private bool _snapCamera = true;
        private bool _leaving;
        private bool _interactQueued;
        private bool _reduceMotion;
        private float _clockCheck;
        private int _day;
        private float _hour;
        private CityTimeOfDay _block;
        private float _stride;
        private CityPrompt _prompt = CityPrompt.None;

        private readonly List<CityNpc> _npcs = new List<CityNpc>();
        private readonly List<CityNpcBrain> _brains = new List<CityNpcBrain>();
        private readonly List<CityActorView> _npcViews = new List<CityActorView>();
        private readonly List<Vec2> _npcPositions = new List<Vec2>();
        private int _talking = -1;

        private void Start()
        {
            App.EnsureInitialized();
            Orientation.Portrait();
            var career = App.Career;
            var catalog = App.Catalog;
            if (career.city == null) career.city = new CitySaveData();
            var save = career.city;
            _reduceMotion = career.settings != null && career.settings.reduceMotion;

            // Spawn: where the save says (moved out of anything solid), or the district's front door.
            _map = CityDistricts.Map(save.district);
            var start = save.HasPosition ? CityMovement.Settle(_map, new Vec2(save.x, save.y)) : _map.DefaultSpawn.Position;
            _me = new CityBody { Position = start, Facing = save.HasPosition ? save.facing : _map.DefaultSpawn.Facing };
            bool firstTime = !save.seen.Contains(_map.DistrictId);
            if (firstTime) save.seen.Add(_map.DistrictId);
            save.visits++;

            _world = CityWorld.Build(_map, catalog, StableHash.Of(_map.DistrictId));
            var me = PlayerCreator.BasePlayer(career, catalog);
            var crew = catalog.Team(DefaultContent.PlayerCrewId);
            _meView = CityActorView.Create(_world.transform, "You", me, Kit(crew), _world.Shadow);

            _hud = CityHud.Create(career.settings);
            _hud.MenuRequested += OpenMenu;
            _hud.Interact.Pressed += () => _interactQueued = true;

            _camera = Camera.main;
            if (_camera == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                _camera = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color32(0x1A, 0x1A, 0x2E, 255);

            RefreshClock(force: true);
            SpawnPeople(career, catalog);
            if (save.fps30) Application.targetFrameRate = 30;
            Audio.AudioManager.PlayMusic(2);
            Audio.AudioManager.SetAmbience(!_map.Interior);
            PowerMonitor.Gameplay = true;

            var def = CityDistricts.Find(_map.DistrictId);
            if (firstTime && def != null) _hud.Toast(def.Name, 2.2f);
            App.SaveCareer();
        }

        private static KitLook Kit(TeamDef team) =>
            team == null
                ? KitLook.Classic(new RgbColor(0x2E, 0x6F, 0xD8), new RgbColor(0xF4, 0xF1, 0xDE), new RgbColor(0xFF, 0xD1, 0x66), null, TeamPattern.Solid, null)
                : KitLook.Classic(team.primary, team.secondary, team.accent, null, TeamPattern.Solid, null);

        // ------------------------------------------------------------------ people

        private void SpawnPeople(CareerSaveData career, ContentCatalog catalog)
        {
            var cast = CityNpcScheduler.Cast(career, catalog);
            var here = CityNpcScheduler.InDistrict(cast, _map.DistrictId, _day, _block);
            for (int i = 0; i < here.Count && _npcs.Count < MaxNpcs; i++)
            {
                var (npc, spot) = here[i];
                var place = CityNpcScheduler.Place(npc, _day, _block);
                bool onCourt = place.Hangout != null && place.Hangout.StartsWith("court.", StringComparison.Ordinal) && !place.Hangout.EndsWith(".side", StringComparison.Ordinal);
                _npcs.Add(npc);
                _brains.Add(new CityNpcBrain(spot, StableHash.Of(npc.Id + ":" + _day), onCourt, _map));
                _npcViews.Add(CityActorView.Create(_world.transform, npc.Id, npc.Player, Kit(npc.Team), _world.Shadow));
                _npcPositions.Add(spot);
            }
        }

        // ------------------------------------------------------------------ frame

        private void Update()
        {
            if (_leaving || _map == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            bool card = _hud.CardOpen;

            if (BackPressed())
            {
                if (card) { CloseCard(); return; }
                OpenMenu();
                return;
            }

            // Walk.
            var stick = card ? Vec2.Zero : _hud.Stick.Value + KeyStick();
            if (stick.Magnitude > 1f) stick = stick.Normalized;
            bool sprint = !card && (_hud.Sprint.IsHeld || SprintKey());
            CityMovement.Step(ref _me, _map, stick, sprint, dt);
            _stride += _me.Velocity.Magnitude * dt;
            if (_stride > 4f && CityOnboarding.Reach(App.Career.city, 1)) App.SaveCareer();

            // Into a district road: straight through to the next district.
            var road = CityInteraction.RoadAt(_map, _me.Position);
            if (road != null && !card)
            {
                Travel(road.Target, road.TargetSpawn);
                return;
            }

            // People.
            for (int i = 0; i < _brains.Count; i++)
            {
                var b = _brains[i];
                b.Step(_map, Time.time, dt, _me.Position, i == _talking, _npcPositions);
                _npcPositions[i] = b.Body.Position;
            }

            // What the action button does.
            _prompt = card ? CityPrompt.None : CityInteraction.Find(_map, _me.Position, _npcPositions);
            _hud.SetPrompt(_prompt.IsNone ? null : _prompt.Label);
            if (InteractKey()) _interactQueued = true;
            if (_interactQueued)
            {
                _interactQueued = false;
                if (!card && !_prompt.IsNone) Use(_prompt);
            }

            RefreshClock(force: false);
            _hud.SetHint(CityOnboarding.Hint(App.Career.city.onboarding));
        }

        private void LateUpdate()
        {
            if (_map == null) return;
            float dt = Time.deltaTime;
            _meView.Sync(dt, _me.Position, _me.Facing, _me.Velocity.Magnitude > 0.3f, _me.Velocity.Magnitude, false, _reduceMotion);

            // Camera: follow, kept inside the district.
            float across = CityCamera.TilesAcross[Mathf.Clamp(_zoom, 0, CityCamera.TilesAcross.Length - 1)];
            float aspect = Mathf.Max(0.3f, _camera.aspect);
            float halfW = across * 0.5f, halfH = halfW / aspect;
            _camera.orthographicSize = halfH;
            _cam = CityCamera.Follow(_cam, _me.Position + new Vec2(0f, 0.6f), halfW, halfH, _map.Width, _map.Height, dt, _snapCamera || _reduceMotion);
            _snapCamera = false;
            float px = 1f / CityWorld.Ppu;
            _camera.transform.position = new Vector3(Mathf.Round(_cam.x / px) * px, Mathf.Round(_cam.y / px) * px, -10f);

            // NPCs: drawn only when near the screen.
            float cullX = halfW + 2f, cullY = halfH + 3f;
            for (int i = 0; i < _brains.Count; i++)
            {
                var b = _brains[i];
                var p = b.Body.Position;
                bool near = Mathf.Abs(p.x - _cam.x) < cullX && Mathf.Abs(p.y - _cam.y) < cullY;
                _npcViews[i].SetVisible(near);
                if (!near) continue;
                bool moving = b.Activity == CityNpcActivity.Walk && b.Body.Velocity.Magnitude > 0.2f;
                bool arms = b.Activity == CityNpcActivity.Shoot || b.Activity == CityNpcActivity.Cheer;
                _npcViews[i].Sync(dt, p, b.Body.Facing, moving, b.Body.Velocity.Magnitude, arms, _reduceMotion);
            }
        }

        private void RefreshClock(bool force)
        {
            if (!force && Time.unscaledTime < _clockCheck) return;
            _clockCheck = Time.unscaledTime + 20f;
            var now = DateTime.Now;
            _hour = now.Hour + now.Minute / 60f;
            _day = CityNpcScheduler.DayNumber(now);
            _block = CityClock.Of(now.Hour);
            _world.SetHour(_hour);
            var def = CityDistricts.Find(_map.DistrictId);
            string block = Loc.T(CityClock.Name(_block));
            _hud.SetPlace(def?.Name ?? "", _map.Interior ? Loc.T("INSIDE") + "  ·  " + block : block + "  ·  " + now.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture));
        }

        // ------------------------------------------------------------------ using things

        private void Use(CityPrompt p)
        {
            var career = App.Career;
            var catalog = App.Catalog;
            bool advanced = CityOnboarding.Reach(career.city, 2);
            if (p.Npc >= 0 && p.Npc < _npcs.Count)
            {
                var npc = _npcs[p.Npc];
                _talking = p.Npc;
                string title = npc.Name.ToUpperInvariant() + "  ·  " + RoleName(npc);
                _hud.ShowCard(title, CityDialogue.Line(npc, career, _day), CityActivities.ForNpc(_map, npc, career, catalog, _me.Position), Pick, () => _talking = -1);
                if (advanced) App.SaveCareer();
                return;
            }
            var portal = p.Portal;
            if (portal == null) return;
            if ((portal.Kind == CityPortalKind.Building || portal.Kind == CityPortalKind.Exit) && CityDistricts.Find(portal.Target) != null)
            {
                Travel(portal.Target, portal.TargetSpawn);
                return;
            }
            if (portal.Kind == CityPortalKind.Court)
            {
                var court = _map.Court(portal.Id);
                if (court == null) return;
                if (CityOnboarding.Reach(career.city, 3)) advanced = true;
                _hud.ShowCard(court.Name, CourtLine(court), CityActivities.ForCourt(_map, court, career, catalog, _day), Pick, null);
            }
            else
            {
                var choices = CityActivities.ForPortal(_map, portal, career, catalog);
                _hud.ShowCard(portal.Label, null, choices, Pick, null);
            }
            if (advanced) App.SaveCareer();
        }

        private static string RoleName(CityNpc npc)
        {
            switch (npc.Role)
            {
                case CityNpcRole.Teammate: return Loc.T("TEAMMATE");
                case CityNpcRole.Rival: return Loc.T("RIVAL");
                case CityNpcRole.Legend: return Loc.T("PARK LEGEND");
                default: return Loc.T("CREW LEADER");
            }
        }

        private static string CourtLine(CityCourt court)
        {
            switch (court.Use)
            {
                case CityCourtUse.Rise: return "The First Callers' home court.";
                case CityCourtUse.Practice: return "The training floor.";
                default: return "Run with whoever's here, or take on the court's crews.";
            }
        }

        private void Pick(CityActivity a)
        {
            if (_leaving || a == null || a.Locked != null) return;
            switch (a.Action)
            {
                case CityAction.PlayMatch:
                    if (a.Request == null) return;
                    SavePosition();
                    App.PendingMatch = a.Request;
                    Leave(SceneNames.Game);
                    break;
                case CityAction.OpenRiseHub:
                    SavePosition();
                    App.BackToCity = true;
                    Leave(SceneNames.Season);
                    break;
                case CityAction.OpenLockerRoom:
                    SavePosition();
                    App.BackToCity = true;
                    Leave(SceneNames.LockerRoom);
                    break;
                default:
                    if (!string.IsNullOrEmpty(a.Detail)) _hud.Toast(a.Detail, 3f);
                    CloseCard();
                    break;
            }
        }

        private void CloseCard()
        {
            _hud.CloseCard();
            _talking = -1;
        }

        private void SavePosition()
        {
            var s = App.Career.city;
            s.district = _map.DistrictId;
            s.x = _me.Position.x;
            s.y = _me.Position.y;
            s.facing = _me.Facing;
            App.SaveCareer();
        }

        /// <summary>Through a door or down a road: arrive at the other side's spawn, behind the screen wipe.</summary>
        private void Travel(string district, string spawn)
        {
            if (_leaving) return;
            App.Career.city.Arrive(district, spawn);
            App.SaveCareer();
            Leave(SceneNames.City);
        }

        private void Leave(string scene)
        {
            if (_leaving) return;
            _leaving = true;
            _hud.CloseCard(false);
            _hud.SetControlsVisible(false);
            if (!SceneFlow.GoTo(scene)) _leaving = false; // a load was already running: stay put
        }

        // ------------------------------------------------------------------ menu

        private void OpenMenu()
        {
            if (_leaving) return;
            var s = App.Career.city;
            var resume = new CityActivity { Label = "RESUME", Action = CityAction.Info, Primary = true };
            var zoom = new CityActivity { Label = Loc.T("ZOOM") + ": " + (_zoom + 1) + "/" + CityCamera.TilesAcross.Length, Action = CityAction.Info };
            var battery = new CityActivity { Label = Loc.T("BATTERY SAVER") + ": " + Loc.T(s.fps30 ? "ON" : "OFF"), Detail = "30 frames a second in the city.", Action = CityAction.Info };
            var menu = new CityActivity { Label = "MAIN MENU", Detail = "Your spot in the city is saved.", Action = CityAction.Info };
            var list = new List<CityActivity> { resume, zoom, battery, menu };
            _hud.ShowCard("BALLER CITY", null, list, a =>
            {
                if (ReferenceEquals(a, zoom))
                {
                    _zoom = (_zoom + 1) % CityCamera.TilesAcross.Length;
                    _snapCamera = true;
                    OpenMenu();
                }
                else if (ReferenceEquals(a, battery))
                {
                    s.fps30 = !s.fps30;
                    if (s.fps30) Application.targetFrameRate = 30; else App.ApplyFrameRate();
                    App.SaveCareer();
                    OpenMenu();
                }
                else if (ReferenceEquals(a, menu))
                {
                    SavePosition();
                    Leave(SceneNames.MainMenu);
                }
                else CloseCard();
            }, null);
        }

        // ------------------------------------------------------------------ keys and pads (Mac, iPad keyboards, controllers)

        private static Vec2 KeyStick()
        {
#if ENABLE_INPUT_SYSTEM
            float x = 0f, y = 0f;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                var v = pad.leftStick.ReadValue();
                if (v.sqrMagnitude > 0.04f) { x += v.x; y += v.y; }
            }
            return new Vec2(x, y);
#else
            return Vec2.Zero;
#endif
        }

        private static bool SprintKey()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return (kb != null && kb.leftShiftKey.isPressed) || (pad != null && (pad.rightTrigger.isPressed || pad.buttonWest.isPressed));
#else
            return false;
#endif
        }

        private static bool InteractKey()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                   || (pad != null && pad.buttonSouth.wasPressedThisFrame);
#else
            return false;
#endif
        }

        private static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame));
#else
            return false;
#endif
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && !_leaving && _map != null) SavePosition();
        }

        private void OnDestroy()
        {
            PowerMonitor.Gameplay = false;
            if (App.Career?.city != null && App.Career.city.fps30) App.ApplyFrameRate();
            if (_hud != null) Destroy(_hud.gameObject);
        }
    }
}
