using System;
using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.City
{
    // 17 Oct 2026 (Omari, phase 3): the living city (walkers, traffic, weather, court sounds), the shops and the MAP.
    public sealed partial class CityController
    {
        private CityWeatherKind _weather;
        private readonly List<CityNpcBrain> _walkerBrains = new List<CityNpcBrain>();
        private readonly List<CityActorView> _walkerViews = new List<CityActorView>();
        private List<CityLane> _lanes = new List<CityLane>();
        private List<CityCar> _cars = new List<CityCar>();
        private readonly List<SpriteRenderer> _carViews = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _particles = new List<SpriteRenderer>();
        private Vector2[] _particleVel = new Vector2[0];
        private float _soundAt;
        private CityPortal _cardPortal;

        private void StartLife(CareerSaveData career)
        {
            // People out walking (generated passers-by, not players you can call out).
            foreach (var w in CityWalkers.For(_map, _day, _block))
            {
                var brain = new CityNpcBrain(w.Anchor, StableHash.Of(w.Player.id), false, _map) { Radius = 7f };
                _walkerBrains.Add(brain);
                var kit = KitLook.Classic(w.Shirt, w.Shirt.Darken(0.2f), w.Shorts, null, TeamPattern.Solid, w.Shorts);
                _walkerViews.Add(CityActorView.Create(_world.transform, w.Player.id, w.Player, kit, _world.Shadow));
            }

            // Cars on the east-west roads.
            _lanes = CityTraffic.Lanes(_map);
            _cars = CityTraffic.Cars(_map, _lanes, _day);
            foreach (var c in _cars)
            {
                var go = new GameObject("Car");
                go.transform.SetParent(_world.transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = _world.PropSprite(new CityProp { Kind = CityPropKind.Car, W = 2, H = 1, Variant = c.Variant });
                r.flipX = _lanes[c.Lane].Dir < 0;
                _carViews.Add(r);
            }
            ApplyWeather();
        }

        /// <summary>Today's sky for this district (or clear, with MENU ▸ WEATHER off): the gloom and the particle pool.</summary>
        private void ApplyWeather()
        {
            var s = App.Career.city;
            _weather = CityWeather.For(_day, DateTime.Now.Month, _map.DistrictId, _map.Interior, s.weatherOff);
            _world.SetGloom(CityWeather.Gloom(_weather));
            _world.SetHour(_hour);
            foreach (var p in _particles) if (p != null) Destroy(p.gameObject);
            _particles.Clear();
            int n = CityWeather.Particles(_weather, _reduceMotion);
            _particleVel = new Vector2[n];
            if (n == 0) { RefreshClock(force: true); return; }
            Sprite dot = _weather == CityWeatherKind.Rain ? _world.Dot(1, 5, new RgbColor(170, 200, 235, 200))
                       : _weather == CityWeatherKind.Snow ? _world.Dot(2, 2, new RgbColor(245, 248, 252, 230))
                       : _world.Dot(2, 2, new RgbColor(150, 120, 60, 220));
            var rng = new SeededRandom((uint)(_day * 31 + 7));
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Weather");
                go.transform.SetParent(_world.transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = dot;
                r.sortingOrder = 30002;
                go.transform.localPosition = new Vector3(_me.Position.x + (rng.NextFloat() - 0.5f) * 24f, _me.Position.y + (rng.NextFloat() - 0.5f) * 40f, 0f);
                _particleVel[i] = _weather == CityWeatherKind.Rain ? new Vector2(-1.5f, -16f - rng.NextFloat() * 4f)
                                : _weather == CityWeatherKind.Snow ? new Vector2(-0.4f + rng.NextFloat() * 0.8f, -1.6f - rng.NextFloat())
                                : new Vector2(6f + rng.NextFloat() * 3f, -0.6f + rng.NextFloat() * 1.2f);
                _particles.Add(r);
            }
            RefreshClock(force: true);
        }

        private void UpdateLife(float dt)
        {
            for (int i = 0; i < _walkerBrains.Count; i++) _walkerBrains[i].Step(_map, Time.time, dt, _me.Position, false, _npcPositions);
            if (_cars.Count > 0) CityTraffic.Step(_cars, _lanes, _map.Width, _me.Position, dt);

            // Court sounds: a dribble or a swish now and then from the closest shooter in earshot.
            if (Time.time < _soundAt) return;
            _soundAt = Time.time + 0.5f + UnityEngine.Random.value * 0.6f;
            float best = 9f;
            for (int i = 0; i < _brains.Count; i++)
            {
                if (_brains[i].Activity != CityNpcActivity.Shoot) continue;
                float d = Vec2.Distance(_brains[i].Body.Position, _me.Position);
                if (d < best) best = d;
            }
            if (best >= 9f) return;
            float vol = Mathf.Lerp(0.35f, 0.04f, best / 9f);
            Audio.AudioManager.Play(UnityEngine.Random.value < 0.25f ? SfxId.Swish : SfxId.Bounce, vol, 0.95f + UnityEngine.Random.value * 0.1f);
        }

        private void DrawLife(float dt, float halfW, float halfH)
        {
            float cullX = halfW + 2f, cullY = halfH + 3f;
            for (int i = 0; i < _walkerBrains.Count; i++)
            {
                var b = _walkerBrains[i];
                var p = b.Body.Position;
                bool near = Mathf.Abs(p.x - _cam.x) < cullX && Mathf.Abs(p.y - _cam.y) < cullY;
                _walkerViews[i].SetVisible(near);
                if (near) _walkerViews[i].Sync(dt, p, b.Body.Facing, b.Walking && b.Body.Velocity.Magnitude > 0.2f, b.Body.Velocity.Magnitude, false, _reduceMotion);
            }
            for (int i = 0; i < _cars.Count; i++)
            {
                var c = _cars[i];
                var lane = _lanes[c.Lane];
                float x = lane.Dir > 0 ? c.X - 1f : c.X - 1f;
                _carViews[i].transform.localPosition = new Vector3(Mathf.Round(x * 16f) / 16f, lane.Y - 0.45f, 0f);
                _carViews[i].sortingOrder = CityWorld.Order(lane.Y - 0.45f);
            }
            // Weather particles fall across the view and wrap round it (no spawning, no garbage).
            if (_particles.Count == 0) return;
            float w = halfW * 2f + 2f, h = halfH * 2f + 4f;
            for (int i = 0; i < _particles.Count; i++)
            {
                var t = _particles[i].transform;
                var pos = (Vector2)t.localPosition + _particleVel[i] * dt;
                if (pos.y < _cam.y - h * 0.5f) pos.y += h;
                if (pos.y > _cam.y + h * 0.5f) pos.y -= h;
                if (pos.x < _cam.x - w * 0.5f) pos.x += w;
                if (pos.x > _cam.x + w * 0.5f) pos.x -= w;
                t.localPosition = new Vector3(pos.x, pos.y, 0f);
            }
        }

        // ------------------------------------------------------------------ the shops

        private void ShowPortalCard(CityPortal portal)
        {
            _cardPortal = portal;
            var career = App.Career;
            string line = portal.Target == "shop" ? Loc.T("Today's shelves. New stock every day.") + "  ·  " + career.signalPoints + " SP"
                        : portal.Target == "barber.chair" ? (CityBarber.PriceFor(career, 1) == 0 ? Loc.T("Your first cut is on the house.") : Loc.T("Cuts") + " " + CityBarber.CutPrice + " SP  ·  " + Loc.T("Colour") + " " + CityBarber.ColorPrice + " SP")
                                                            + "  ·  " + career.signalPoints + " SP"
                        : null;
            _hud.ShowCard(portal.Label, line, CityActivities.ForPortal(_map, portal, career, App.Catalog, _day), Pick, () => _cardPortal = null);
        }

        private void HandleShop(CityActivity a)
        {
            var career = App.Career;
            switch (a.Action)
            {
                case CityAction.Buy:
                {
                    var result = CityShop.Buy(career, App.Catalog, a.ItemId, _day);
                    _hud.Toast(result == CosmeticCheck.Ok ? Loc.T("BOUGHT AND PUT ON") : result == CosmeticCheck.NotEnoughPoints ? Loc.T("NOT ENOUGH SP") : Loc.T("CAN'T BUY THAT"), 1.8f);
                    if (result == CosmeticCheck.Ok) Audio.AudioManager.Play(SfxId.Fanfare, 0.5f);
                    break;
                }
                case CityAction.Equip:
                    if (CityShop.Equip(career, App.Catalog, a.ItemId)) _hud.Toast("PUT ON", 1.4f);
                    break;
                case CityAction.Haircut:
                    if (CityBarber.Apply(career, a.ItemId))
                    {
                        _hud.Toast("FRESH!", 1.6f);
                        Audio.AudioManager.Play(SfxId.Fanfare, 0.5f);
                        // Your new look, right away.
                        var me = PlayerCreator.BasePlayer(career, App.Catalog);
                        Destroy(_meView.gameObject);
                        _meView = CityActorView.Create(_world.transform, "You", me, Kit(App.Catalog.Team(DefaultContent.PlayerCrewId)), _world.Shadow);
                    }
                    break;
            }
            AnnounceMissions(CityMissions.Check(career));
            App.SaveCareer();
            if (_cardPortal != null) ShowPortalCard(_cardPortal);
        }

        // ------------------------------------------------------------------ the map

        private void OpenMap()
        {
            var s = App.Career.city;
            var entries = new List<CityHud.MapEntry>();
            int cols = 0, rows = 0;
            string here = CityAtlas.Outside(_map.DistrictId);
            foreach (var spot in CityAtlas.Spots)
            {
                var def = CityDistricts.Find(spot.id);
                entries.Add(new CityHud.MapEntry
                {
                    Id = spot.id, Name = def?.Name ?? spot.id, Col = spot.col, Row = spot.row,
                    Here = spot.id == here, Visited = s.seen.Contains(spot.id), Summary = CityAtlas.Summary(s, spot.id),
                });
                cols = Math.Max(cols, spot.col + 1);
                rows = Math.Max(rows, spot.row + 1);
            }
            int seen = entries.FindAll(e => e.Visited).Count;
            string footer = Loc.T("DISTRICTS") + " " + seen + "/" + entries.Count + "  ·  " + Loc.T("CREWS BEATEN") + " " + s.crewsBeaten.Count + "/" + CityCrews.All.Length
                            + (_map.Interior ? "\n" + Loc.T("You're inside:") + " " + CityDistricts.Find(_map.DistrictId)?.Name : "");
            _hud.ShowMap(entries, cols, rows, footer, to =>
            {
                if (!CityAtlas.Travel(App.Career.city, _map.DistrictId, to)) return;
                AnnounceMissions(CityMissions.Check(App.Career));
                App.SaveCareer();
                Leave(SceneNames.City);
            }, null);
        }
    }
}
