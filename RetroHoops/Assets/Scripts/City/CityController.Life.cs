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
        private SpriteRenderer _fog; // I33: the Bay's fog, a soft grey veil over the view
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
            if (_fog != null) { Destroy(_fog.gameObject); _fog = null; }
            if (_weather == CityWeatherKind.Fog)
            {
                var fogGo = new GameObject("Fog");
                fogGo.transform.SetParent(_world.transform, false);
                _fog = fogGo.AddComponent<SpriteRenderer>();
                _fog.sprite = _world.Dot(1, 1, new RgbColor(214, 222, 232, 255));
                _fog.color = new Color(1f, 1f, 1f, 0.32f);
                _fog.sortingOrder = 30000;
                fogGo.transform.localScale = new Vector3(16f * 60f, 16f * 90f, 1f);
            }
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
            if (_fog != null) _fog.transform.localPosition = new Vector3(_cam.x, _cam.y, 0f);
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
                    if (result == CosmeticCheck.Ok) { Audio.AudioManager.Play(SfxId.Fanfare, 0.5f); RefreshMe(); }
                    break;
                }
                case CityAction.BuyWithCash:
                {
                    var result = CityShop.BuyWithCash(career, App.Catalog, a.ItemId, _day);
                    _hud.Toast(result == CosmeticCheck.Ok ? Loc.T("PAID IN CASH") : result == CosmeticCheck.NotEnoughPoints ? Loc.T("NOT ENOUGH CASH") : Loc.T("CAN'T BUY THAT"), 1.8f);
                    if (result == CosmeticCheck.Ok) { Audio.AudioManager.Play(SfxId.Fanfare, 0.5f); RefreshMe(); }
                    break;
                }
                case CityAction.RegionBuy:
                {
                    // I30 REGION STORES.
                    var result = RegionStore.Buy(career, App.Catalog, a.ItemId);
                    _hud.Toast(result == CosmeticCheck.Ok ? Loc.T("PAID IN CASH") : result == CosmeticCheck.NotEnoughPoints ? Loc.T("NOT ENOUGH CASH") : Loc.T("CAN'T BUY THAT"), 1.8f);
                    if (result == CosmeticCheck.Ok) { Audio.AudioManager.Play(SfxId.Fanfare, 0.5f); RefreshMe(); }
                    break;
                }
                case CityAction.Equip:
                    if (CityShop.Equip(career, App.Catalog, a.ItemId)) { _hud.Toast("PUT ON", 1.4f); RefreshMe(); }
                    break;
                case CityAction.Haircut:
                    if (CityBarber.Apply(career, a.ItemId))
                    {
                        _hud.Toast("FRESH!", 1.6f);
                        Audio.AudioManager.Play(SfxId.Fanfare, 0.5f);
                        RefreshMe(); // your new look, right away
                    }
                    break;
            }
            AnnounceMissions(CityMissions.Check(career));
            App.SaveCareer();
            if (_cardPortal != null) ShowPortalCard(_cardPortal);
        }

        // ------------------------------------------------------------------ THE STREETS: cash and milestones

        private void OpenStreetsBoard()
        {
            var s = App.Career.streetCash ?? (App.Career.streetCash = new StreetCashSaveData());
            var list = new List<CityActivity>();
            foreach (var m in StreetCash.Milestones)
            {
                bool done = s.achievements.Contains(m.Id);
                list.Add(new CityActivity
                {
                    Label = (done ? Loc.T("DONE") + ": " : "") + (m.Secret && !done ? "???" : Loc.T(m.Name)) + "  +" + m.Sp + " SP",
                    Detail = m.Secret && !done ? Loc.T("A secret. Play big to find out.") : Loc.T(m.Detail),
                    Action = CityAction.Info,
                });
            }
            string line = Loc.T("The street courts are meant for legends. Think you can hang?") + "\n"
                          + Loc.T("BANK") + " " + StreetCash.Money(s.cash) + "  ·  " + Loc.T("EARNED") + " " + StreetCash.Money(s.earned)
                          + "  ·  " + Loc.T("STREAK") + " " + s.streak + " (" + Loc.T("BEST") + " " + s.bestStreak + ")\n"
                          + Loc.T("Win: $5,000. Three in a row: $10,000 a win.");
            _hud.ShowCard("CASHED OUT", line, list, a => { }, null);
        }

        // ------------------------------------------------------------------ 17 Oct 2026 (phase 6): call-outs and City Challenges

        /// <summary>Today's call-out: the crew's leader is waiting by where you came in.</summary>
        private void SpawnCallout(CareerSaveData career, ContentCatalog catalog)
        {
            if (_map.Interior) return;
            var crew = CityCallout.For(career, _map.DistrictId, _day);
            if (crew == null) return;
            var team = CityCrews.Register(catalog, crew);
            var leader = team.rosterPlayerIds.Count > 0 ? catalog.Player(team.rosterPlayerIds[0]) : null;
            if (leader == null) return;
            var npc = new CityNpc { Id = "callout." + crew.Id, Player = leader, Team = team, Role = CityNpcRole.Crew, Ref = crew.Id, Callout = true, Haunts = new string[0] };
            var spot = CityMovement.Settle(_map, _me.Position + new Vec2(1.6f, 0.4f));
            _npcs.Add(npc);
            _brains.Add(new CityNpcBrain(spot, StableHash.Of(npc.Id + _day), false, _map) { Radius = 1.2f });
            _npcViews.Add(CityActorView.Create(_world.transform, npc.Id, leader, Kit(team), _world.Shadow));
            _npcPositions.Add(spot);
            _hud.Toast(leader.firstName + " (" + crew.Name + ") " + Loc.T("is calling you out!"), 2.6f);
        }

        private void OpenDaily()
        {
            var s = App.Career.bigTime ?? (App.Career.bigTime = new CityBigTimeSaveData());
            var defs = CityDaily.For(_day);
            var progress = CityDaily.Progress(s, _day);
            var list = new List<CityActivity>();
            for (int i = 0; i < defs.Count; i++)
                list.Add(new CityActivity
                {
                    Label = (progress[i] >= defs[i].Target ? Loc.T("DONE") + ": " : "") + defs[i].Text + "  " + Math.Min(progress[i], defs[i].Target) + "/" + defs[i].Target,
                    Detail = "+" + StreetCash.Money(CityDaily.EachPay), Action = CityAction.Info,
                });
            string line = Loc.T("New challenges every day. Games you start in the city count.") + "\n"
                          + (s.dailyPaid ? Loc.T("All three done today!") : Loc.T("All three:") + " +" + StreetCash.Money(CityDaily.SetBonus))
                          + "  ·  " + Loc.T("SETS") + " " + s.dailySets;
            _hud.ShowCard("CITY CHALLENGES", line, list, a => { }, null);
        }

        // ------------------------------------------------------------------ the map

        /// <summary>28 Oct 2026: MAP opens the zoomable world map on the region you're in.</summary>
        private void OpenMap() => OpenAtlas(WorldRegions.RegionOf(_map.DistrictId).Id);
    }
}
