using System;
using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.Utilities;
using UnityEngine;

namespace CallerRetroBall.City
{
    // 28 Oct 2026 (Omari, BALLER WORLD W1–W10): the comic, the guide, daily achievements, side jobs, Big Runs, the
    // world map and THE DRIVE, wired into the city. The rules are in Logic/City; this draws them and reacts.
    public sealed partial class CityController
    {
        private CityDrive _driveView;
        private float _guideAt, _walkMetres, _walkAt, _achAt, _errandStart;
        private bool _errandRunning;
        private GuideHint _hint;
        private readonly Dictionary<string, int> _giverIndex = new Dictionary<string, int>();
        private readonly Dictionary<string, SpriteRenderer> _giverMarks = new Dictionary<string, SpriteRenderer>();
        private readonly List<SpriteRenderer> _jobMarks = new List<SpriteRenderer>();
        private readonly List<UnityEngine.Object> _worldOwned = new List<UnityEngine.Object>();
        private readonly Queue<Action> _comicQueue = new Queue<Action>();
        private readonly HashSet<string> _talkedThisVisit = new HashSet<string>();
        private Sprite _markBang, _markGem, _markRing, _markQuestion;
        /// <summary>A line to show when the next city scene opens (after a drive).</summary>
        private static string _arrivalNote;

        private bool Driving => _driveView != null;

        private CityWorldSaveData World => App.Career.world ?? (App.Career.world = new CityWorldSaveData());

        // ------------------------------------------------------------------ start

        private void StartWorld(CareerSaveData career, ContentCatalog catalog)
        {
            var w = World;
            if (!w.started)
            {
                w.started = true;
                CityStory.StartStep(career);
            }
            _markBang = OwnedSprite(CityArtGenerator.Marker(0), "mark.bang", new Vector2(0.5f, 0f));
            _markGem = OwnedSprite(CityArtGenerator.Marker(1), "mark.gem", new Vector2(0.5f, 0f));
            _markRing = OwnedSprite(CityArtGenerator.Marker(2), "mark.ring", new Vector2(0.5f, 0.5f));
            _markQuestion = OwnedSprite(CityArtGenerator.Marker(3), "mark.q", new Vector2(0.5f, 0f));

            // A new region: say where you are.
            if (!_map.Interior && WorldRegions.NoteArrive(w, _map.DistrictId))
            {
                var r = WorldRegions.RegionOf(_map.DistrictId);
                _hud.Toast(Loc.T("WELCOME TO") + " " + Loc.T(r.Name) + "\n" + Loc.T(r.Tagline), 3.2f);
            }
            if (_arrivalNote != null) { _hud.Toast(_arrivalNote, 3f); _arrivalNote = null; }

            SpawnGivers();
            // An errand you'd started stops when you leave its district: talk to the giver to go again.
            var job = CitySideJobs.Active(w);
            if (job != null && job.Kind == SideJobKind.Errand) w.sideStage = 0;
            RefreshJobMarks();

            // The comic: pages from an issue finished in a game, then the cover of the issue you're on.
            if (w.pendingOutro > 0) QueueOutro(CityStory.Issue(w.pendingOutro));
            w.pendingOutro = 0;
            if (CityStory.NeedsIntro(w)) QueueIntro(CityStory.Current(w));
            CheckWorld();
        }

        private Sprite OwnedSprite(PixelCanvas c, string name, Vector2 pivot)
        {
            var tex = TextureFactory.ToTexture(c, name);
            _worldOwned.Add(tex);
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, CityWorld.Ppu, 0, SpriteMeshType.FullRect);
            _worldOwned.Add(s);
            return s;
        }

        private SpriteRenderer Mark(Sprite s, Vec2 at, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_world.transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s;
            r.sortingOrder = 30001;
            go.transform.localPosition = new Vector3(at.x, at.y, 0f);
            return r;
        }

        /// <summary>The side-job givers in this district, standing where they wait, with a "!" (or "?") over them.</summary>
        private void SpawnGivers()
        {
            foreach (var j in CitySideJobs.InDistrict(_map.DistrictId))
            {
                var p = CitySideJobs.Giver(j, out var kit);
                var npc = new CityNpc { Id = j.NpcId, Player = p, Team = null, Role = CityNpcRole.Local, Ref = j.Id, Haunts = new string[0] };
                var spot = CityMovement.Settle(_map, j.At);
                _giverIndex[j.Id] = _npcs.Count;
                _npcs.Add(npc);
                _brains.Add(new CityNpcBrain(spot, StableHash.Of(npc.Id), false, _map) { Radius = 0.8f });
                _npcViews.Add(CityActorView.Create(_world.transform, npc.Id, p, kit, _world.Shadow));
                _npcPositions.Add(spot);
                _giverMarks[j.Id] = Mark(_markBang, spot, "Mark " + j.Id);
            }
        }

        /// <summary>Markers for the job you've taken: things to find, the errand's next spot.</summary>
        private void RefreshJobMarks()
        {
            foreach (var m in _jobMarks) if (m != null) Destroy(m.gameObject);
            _jobMarks.Clear();
            var w = World;
            var job = CitySideJobs.Active(w);
            foreach (var kv in _giverMarks)
            {
                var j = CitySideJobs.Find(kv.Key);
                bool done = CitySideJobs.Done(w, j);
                bool open = CitySideJobs.Locked(App.Career, j) == null;
                kv.Value.enabled = !done && (open || job == j);
                kv.Value.sprite = job == j ? _markQuestion : _markBang;
            }
            if (job == null || job.District != _map.DistrictId) return;
            if (job.Kind == SideJobKind.Collect)
                for (int i = 0; i < job.Spots.Length; i++)
                    if (!w.sideFound.Contains(i)) _jobMarks.Add(Mark(_markGem, job.Spots[i], "Find " + i));
            if (job.Kind == SideJobKind.Errand && _errandRunning)
            {
                var t = CitySideJobs.ErrandTarget(job, w.sideStage);
                if (t != null) _jobMarks.Add(Mark(job.Spots.Length > 1 && w.sideStage == 0 ? _markGem : _markRing, t.Value, "Errand"));
            }
        }

        // ------------------------------------------------------------------ every frame

        private void UpdateWorld(float dt)
        {
            var career = App.Career;
            var w = World;
            // Walking counts (a metre a tile).
            _walkMetres += _me.Velocity.Magnitude * dt;
            if (Time.unscaledTime > _walkAt)
            {
                _walkAt = Time.unscaledTime + 1f;
                if (_walkMetres > 0.01f) CityAchievements.NoteWalk(w, _day, _walkMetres);
                _walkMetres = 0f;
            }
            if (Time.unscaledTime > _achAt)
            {
                _achAt = Time.unscaledTime + 3f;
                CheckWorld();
            }

            // The side job you've taken.
            var job = CitySideJobs.Active(w);
            if (job != null && job.District == _map.DistrictId)
            {
                if (job.Kind == SideJobKind.Errand && _errandRunning)
                {
                    float elapsed = Time.time - _errandStart;
                    _hud.SetTimer(Mathf.Max(0f, job.Target - elapsed).ToString("0.0"));
                    switch (CitySideJobs.StepErrand(w, job, _me.Position, elapsed))
                    {
                        case ErrandStep.PickedUp:
                            _hud.Toast(Loc.T("GOT IT! NOW BRING IT BACK"), 1.6f);
                            Audio.AudioManager.Play(SfxId.Coin, 0.7f);
                            RefreshJobMarks();
                            break;
                        case ErrandStep.Delivered:
                            _errandRunning = false;
                            _hud.SetTimer(null);
                            FinishJob();
                            break;
                        case ErrandStep.TimeUp:
                            _errandRunning = false;
                            w.sideStage = 0;
                            _hud.SetTimer(null);
                            _hud.Toast(Loc.T("TOO SLOW!") + "\n" + Loc.T("Talk to") + " " + job.Giver + " " + Loc.T("to try again."), 2.6f);
                            Audio.AudioManager.Play(SfxId.Error, 0.6f);
                            RefreshJobMarks();
                            App.SaveCareer();
                            break;
                    }
                }
                else if (job.Kind == SideJobKind.Collect)
                {
                    int found = CitySideJobs.StepCollect(w, job, _me.Position);
                    if (found >= 0)
                    {
                        Audio.AudioManager.Play(SfxId.Coin, 0.7f);
                        Haptics.Light();
                        RefreshJobMarks();
                        if (CitySideJobs.CollectDone(w, job)) FinishJob();
                        else { _hud.Toast(Loc.T("FOUND") + " " + w.sideFound.Count + "/" + job.Spots.Length, 1.2f); App.SaveCareer(); }
                    }
                }
            }

            // Comic pages wait until no card is open.
            if (_comicQueue.Count > 0 && !_hud.CardOpen) _comicQueue.Dequeue()();

            // The guide.
            if (Time.unscaledTime > _guideAt)
            {
                _guideAt = Time.unscaledTime + 0.5f;
                _hint = CityGuide.Hint(career, _map.DistrictId);
                _hud.SetTicker(_hint.Text, _hint.Route);
            }
        }

        private void LateUpdateGuide()
        {
            // The "!" rides over the giver's head as they shuffle about.
            foreach (var kv in _giverIndex)
                if (_giverMarks.TryGetValue(kv.Key, out var m) && m != null && kv.Value < _npcPositions.Count)
                {
                    var p = _npcPositions[kv.Value];
                    float bob = _reduceMotion ? 0f : Mathf.Sin(Time.time * 3f) * 0.08f;
                    m.transform.localPosition = new Vector3(p.x, p.y + 2.1f + bob, 0f);
                }
            Vec2? target = null;
            switch (_hint.Kind)
            {
                case GuideKind.Spot:
                case GuideKind.Exit:
                case GuideKind.Drive:
                    if (_hint.HasPoint) target = _hint.Point;
                    break;
                case GuideKind.People:
                {
                    float best = float.MaxValue;
                    for (int i = 0; i < _npcPositions.Count; i++)
                    {
                        float d = Vec2.Distance(_npcPositions[i], _me.Position);
                        if (d < best) { best = d; target = _npcPositions[i]; }
                    }
                    break;
                }
                case GuideKind.AnyCourt:
                {
                    float best = float.MaxValue;
                    foreach (var k in _map.Courts)
                    {
                        if (k.Use != CityCourtUse.Streets && k.Use != CityCourtUse.Park) continue;
                        var c = new Vec2(k.X + k.W * 0.5f, k.Y + 1.5f);
                        float d = Vec2.Distance(c, _me.Position);
                        if (d < best) { best = d; target = c; }
                    }
                    break;
                }
            }
            if (target == null || World.tracking == "off") { _hud.SetGuide(false, Vector2.zero, null); return; }
            var screen = _camera.WorldToScreenPoint(new Vector3(target.Value.x, target.Value.y + 0.5f, 0f));
            float dist = Vec2.Distance(target.Value, _me.Position);
            _hud.SetGuide(dist > 1.2f, new Vector2(screen.x, screen.y), Mathf.RoundToInt(dist) + " m");
        }

        // ------------------------------------------------------------------ achievements, the comic, badges

        /// <summary>Today's achievements, badges and the comic: pays and announces anything new.</summary>
        private void CheckWorld()
        {
            var career = App.Career;
            var ach = CityAchievements.Update(career, _day);
            var story = CityStory.Check(career);
            bool any = false;
            foreach (var a in ach.Done) { _hud.Toast(Loc.T("ACHIEVEMENT:") + " " + Loc.T(a.Text), 2.2f); any = true; }
            if (ach.Tier > 0)
            {
                _hud.Toast(Loc.T(CityAchievements.TierNames[ach.Tier - 1]) + " " + Loc.T("DAY!") + "  +" + StreetCash.Money(ach.Cash) + "  +" + ach.Sp + " SP"
                           + (ach.StreakBonus > 0 ? "\n" + Loc.T("STREAK BONUS") + " +" + ach.StreakBonus + " SP" : ""), 3f);
                Audio.AudioManager.Play(SfxId.Fanfare, 0.6f);
                any = true;
            }
            foreach (var b in ach.Badges) { _hud.Toast(Loc.T("BADGE:") + " " + Loc.T(b.Name), 2.6f); any = true; }
            foreach (var g in story.GoalsDone) { _hud.Toast(Loc.T("COMIC:") + " " + Loc.T(g.Text) + "  " + Loc.T("DONE"), 2.4f); any = true; }
            if (story.Finished != null)
            {
                World.pendingOutro = 0;
                QueueOutro(story.Finished);
                if (story.Opened != null) QueueIntro(story.Opened);
                any = true;
            }
            if (any)
            {
                _guideAt = 0f;
                RefreshJobMarks();
                App.SaveCareer();
            }
        }

        private void QueueIntro(StoryIssue issue)
        {
            if (issue == null) return;
            _comicQueue.Enqueue(() =>
            {
                CityStory.MarkIntroShown(World);
                App.SaveCareer();
                ShowPanels(CityStory.Header(issue), Loc.T(issue.Tagline), issue.Intro, issue.Region, () =>
                {
                    _hud.Toast(Loc.T("NEW OBJECTIVE") + "\n" + CityStory.Ticker(App.Career), 2.6f);
                    _guideAt = 0f;
                });
            });
        }

        private void QueueOutro(StoryIssue issue)
        {
            if (issue == null) return;
            _comicQueue.Enqueue(() =>
            {
                Audio.AudioManager.Play(SfxId.Fanfare, 0.7f);
                ShowPanels(CityStory.Header(issue) + "  " + Loc.T("COMPLETE"), "+" + StreetCash.Money(issue.Cash) + "  +" + issue.Sp + " SP", issue.Outro, issue.Region, null);
            });
        }

        /// <summary>Builds the panel art (backdrops and speakers from their sprite sheets) and shows the pages.</summary>
        private void ShowPanels(string header, string subtitle, ComicPanel[] panels, string region, Action done)
        {
            var career = App.Career;
            var owned = new List<UnityEngine.Object>();
            var sheets = new Dictionary<string, Texture2D>();
            var views = new List<ComicPanelView>();
            uint seed = StableHash.Of(header);
            foreach (var p in panels)
            {
                var v = new ComicPanelView { Caption = p.Caption == null ? null : Loc.T(p.Caption), Line = p.Line == null ? null : Loc.T(p.Line), Sfx = p.Sfx, Speaker = ComicCast.Name(p.Speaker) };
                var bg = TextureFactory.ToTexture(ComicArt.Panel(p.Scene, p.Region ?? region, seed++), "comic.bg");
                owned.Add(bg);
                v.Backdrop = bg;
                if (p.Speaker != null)
                {
                    if (!sheets.TryGetValue(p.Speaker, out var sheet))
                    {
                        var who = ComicCast.Resolve(p.Speaker, career, App.Catalog, out var kit);
                        if (who != null)
                        {
                            sheet = TextureFactory.ToTexture(CharacterSpriteGenerator.GenerateSheet(who.appearance, kit, who.jerseyNumber), "comic.who");
                            owned.Add(sheet);
                        }
                        sheets[p.Speaker] = sheet;
                    }
                    if (sheet != null) { v.Sheet = sheet; v.Frame = FrontFrame(sheet); }
                }
                views.Add(v);
            }
            _hud.ShowComic(header, subtitle, views, () =>
            {
                foreach (var o in owned) if (o != null) Destroy(o);
                done?.Invoke();
            });
        }

        private static Rect FrontFrame(Texture2D sheet)
        {
            CharacterSpriteGenerator.FrameOrigin(CharacterView.Front, 0, out int x, out int y);
            return new Rect(x / (float)sheet.width, y / (float)sheet.height,
                            CharacterSpriteGenerator.SheetFrameWidth / (float)sheet.width, CharacterSpriteGenerator.SheetFrameHeight / (float)sheet.height);
        }

        // ------------------------------------------------------------------ side jobs

        private bool TryTalkToGiver(CityNpc npc)
        {
            if (npc.Role != CityNpcRole.Local) return false;
            var j = CitySideJobs.Find(npc.Ref);
            if (j == null) return true;
            var career = App.Career;
            var w = World;
            var list = new List<CityActivity>();
            string line;
            if (CitySideJobs.Done(w, j))
            {
                line = Loc.T(j.Thanks);
                list.Add(new CityActivity { Label = "SEE YOU AROUND", Action = CityAction.Info });
                _hud.ShowCard(j.Giver, line, list, a => CloseCard(), () => _talking = -1);
                return true;
            }
            bool mine = CitySideJobs.Active(w) == j;
            string locked = CitySideJobs.Locked(career, j);
            line = Loc.T(j.Pitch) + "\n" + Loc.T("PAYS") + " " + StreetCash.Money(j.Cash) + "  +" + j.Sp + " SP";
            if (mine)
            {
                list.Add(new CityActivity { Label = j.Kind == SideJobKind.Errand ? Loc.T("GO! (START THE CLOCK)") : Loc.T("ON IT"), Action = CityAction.AcceptJob, ItemId = j.Id, Primary = true,
                                            Detail = CitySideJobs.Goal(j, w) });
                list.Add(new CityActivity { Label = Loc.T("DROP THIS JOB"), Action = CityAction.Info, ItemId = "drop:" + j.Id });
            }
            else
                list.Add(locked != null
                    ? new CityActivity { Label = Loc.T("TAKE THE JOB"), Locked = Loc.T(locked) }
                    : new CityActivity { Label = Loc.T("TAKE THE JOB"), Action = CityAction.AcceptJob, ItemId = j.Id, Primary = true,
                                         Detail = CitySideJobs.Goal(j, w) + (CitySideJobs.Active(w) != null ? "  ·  " + Loc.T("(drops the job you have)") : "") });
            // The pitch is a comic panel first, then the choice.
            var p = CitySideJobs.Giver(j, out var kit);
            var sheet = TextureFactory.ToTexture(CharacterSpriteGenerator.GenerateSheet(p.appearance, kit, p.jerseyNumber), "comic.giver");
            var bg = TextureFactory.ToTexture(ComicArt.Panel(ComicScene.Street, j.Region, StableHash.Of(j.Id)), "comic.giver.bg");
            var view = new ComicPanelView { Backdrop = bg, Sheet = sheet, Frame = FrontFrame(sheet), Speaker = j.Giver, Line = Loc.T(j.Pitch), Sfx = j.Sfx,
                                            Caption = Loc.T("SIDE JOB") + ": " + Loc.T(j.Title) };
            _hud.ShowComic(Loc.T("SIDE JOB"), Loc.T(j.Title), new List<ComicPanelView> { view }, () =>
            {
                Destroy(sheet);
                Destroy(bg);
                _hud.ShowCard(Loc.T(j.Title), line, list, PickJob, () => _talking = -1);
            });
            return true;
        }

        private void PickJob(CityActivity a)
        {
            var career = App.Career;
            var w = World;
            if (a.ItemId != null && a.ItemId.StartsWith("drop:", StringComparison.Ordinal))
            {
                CitySideJobs.Drop(w);
                _errandRunning = false;
                _hud.SetTimer(null);
                RefreshJobMarks();
                App.SaveCareer();
                CloseCard();
                _hud.Toast(Loc.T("JOB DROPPED"), 1.4f);
                return;
            }
            if (a.Action != CityAction.AcceptJob) { CloseCard(); return; }
            var j = CitySideJobs.Find(a.ItemId);
            if (j == null) return;
            if (CitySideJobs.Active(w) != j && !CitySideJobs.Accept(career, j)) { _hud.Toast(CitySideJobs.Locked(career, j) ?? "", 2f); return; }
            if (j.Kind == SideJobKind.Errand)
            {
                w.sideStage = 0;
                _errandRunning = true;
                _errandStart = Time.time;
            }
            RefreshJobMarks();
            _guideAt = 0f;
            App.SaveCareer();
            CloseCard();
            _hud.Toast(Loc.T("SIDE JOB") + ": " + CitySideJobs.Goal(j, w), 2.4f);
            Audio.AudioManager.Play(SfxId.Click, 0.6f);
        }

        private void FinishJob()
        {
            var career = App.Career;
            var j = CitySideJobs.Complete(career, _day);
            if (j == null) return;
            Audio.AudioManager.Play(SfxId.Fanfare, 0.7f);
            Haptics.Success();
            _hud.Toast(Loc.T("SIDE JOB DONE:") + " " + Loc.T(j.Title) + "\n+" + StreetCash.Money(j.Cash) + "  +" + j.Sp + " SP\n" + Loc.T(j.Thanks), 3.4f);
            RefreshJobMarks();
            CheckWorld();
            App.SaveCareer();
        }

        // ------------------------------------------------------------------ the world map and the drive

        private void OpenWorldMap()
        {
            var career = App.Career;
            var w = World;
            var here = WorldRegions.RegionOf(_map.DistrictId);
            var next = CityStory.Current(w)?.Region;
            var entries = new List<WorldMapEntry>();
            foreach (var r in WorldRegions.All)
            {
                bool open = WorldRegions.Open(career, r);
                int crews = WorldRegions.Crews(r).Count, beaten = WorldRegions.CrewsBeaten(career.city, r);
                int titles = CityBigRun.Titles(w, r.Id);
                entries.Add(new WorldMapEntry
                {
                    Id = r.Id, Name = Loc.T(r.Name), State = r.State, X = r.MapX, Y = r.MapY, Here = r.Id == here.Id, Open = open, Next = r.Id == next && r.Id != here.Id,
                    Status = r.Id == here.Id ? Loc.T("YOU ARE HERE") : open ? Loc.T("TAP TO DRIVE") : WorldRegions.LockedReason(career, r),
                    Detail = Loc.T("CREWS") + " " + beaten + "/" + crews + (titles > 0 ? "  ·  " + Loc.T(CityBigRun.KingTitle(r.Id)) : ""),
                });
            }
            string footer = Loc.T("REGIONS") + " " + w.regionsSeen.Count + "/" + WorldRegions.All.Length + "  ·  " + Loc.T("BIG RUN TITLES") + " " + CityBigRun.TotalTitles(w)
                            + "  ·  " + Loc.T("DRIVES") + " " + w.drives + (w.bestDrive > 0f ? "  ·  " + Loc.T("BEST") + " " + w.bestDrive.ToString("0.0") + " s" : "");
            _hud.ShowWorldMap(entries, footer, to => StartDrive(to), () => OpenMap(), null);
        }

        /// <summary>Gets in your ride and drives to a region (from anywhere outdoors).</summary>
        private void StartDrive(string region)
        {
            if (_leaving || Driving) return;
            var r = WorldRegions.Find(region);
            if (r == null) return;
            string locked = WorldRegions.LockedReason(App.Career, r);
            if (locked != null) { _hud.Toast(locked, 2.4f); return; }
            if (_map.Interior) { _hud.Toast(Loc.T("Head outside to your ride first."), 2f); return; }
            if (WorldRegions.RegionOf(_map.DistrictId).Id == r.Id) { _hud.Toast(Loc.T("YOU ARE HERE"), 1.4f); return; }
            SavePosition();
            _hud.CloseCard(false);
            _hud.gameObject.SetActive(false);
            _world.gameObject.SetActive(false);
            Audio.AudioManager.SetAmbience(false);
            Audio.AudioManager.Play(SfxId.TipOff, 0.6f);
            string from = WorldRegions.RegionOf(_map.DistrictId).Id;
            _driveView = CityDrive.Begin(_camera, from, r.Id, App.Career.settings, ArriveFromDrive);
        }

        private void ArriveFromDrive(HighwayDrive drive)
        {
            var career = App.Career;
            long paid = HighwayDrive.Finish(career, drive, _day);
            var (district, spawn) = HighwayDrive.Arrival(drive.To);
            career.city.Arrive(district, spawn);
            _arrivalNote = Loc.T("ARRIVED IN") + " " + drive.Time.ToString("0.0") + " s"
                           + (drive.Bumps == 0 ? "  ·  " + Loc.T("CLEAN DRIVE") : "")
                           + (paid > 0 ? "  +" + StreetCash.Money(paid) : "");
            CityAchievements.Update(career, _day);
            App.SaveCareer();
            _hud.gameObject.SetActive(true);
            Leave(SceneNames.City);
        }

        // ------------------------------------------------------------------ menus: the comic, achievements

        private void OpenComic()
        {
            var career = App.Career;
            var w = World;
            var list = new List<CityActivity>();
            var issue = CityStory.Current(w);
            if (issue == null)
                list.Add(new CityActivity { Label = Loc.T("ALL-WORLD"), Detail = Loc.T("You finished the comic. Every crew still wants a rematch."), Action = CityAction.Info, Primary = true });
            else
            {
                for (int i = 0; i < issue.Goals.Length; i++)
                {
                    var g = issue.Goals[i];
                    bool done = i < w.step;
                    var (now, target) = done ? (g.Target, g.Target) : i == w.step ? CityStory.Progress(career, g, true) : (0, g.Target);
                    list.Add(new CityActivity { Label = (done ? Loc.T("DONE") + ": " : i == w.step ? "► " : "") + Loc.T(g.Text) + (target > 1 ? "  " + now + "/" + target : ""), Action = CityAction.Info });
                }
                list.Add(new CityActivity { Label = Loc.T("READ THIS ISSUE"), Action = CityAction.Comic, ItemId = "read" });
            }
            bool tracking = w.tracking != "off";
            list.Add(new CityActivity { Label = Loc.T("GUIDE") + ": " + Loc.T(tracking ? "ON" : "OFF"), Detail = Loc.T("The arrow and the objective at the top."), Action = CityAction.Comic, ItemId = "guide" });
            if (CitySideJobs.Active(w) != null && w.tracking != null && w.tracking.StartsWith("side:", StringComparison.Ordinal))
                list.Add(new CityActivity { Label = Loc.T("TRACK THE COMIC"), Detail = Loc.T("Instead of your side job."), Action = CityAction.Comic, ItemId = "story" });
            else if (CitySideJobs.Active(w) != null)
                list.Add(new CityActivity { Label = Loc.T("TRACK MY SIDE JOB"), Detail = CitySideJobs.Goal(CitySideJobs.Active(w), w), Action = CityAction.Comic, ItemId = "side" });
            string header = issue == null ? Loc.T("THE COMIC") : CityStory.Header(issue);
            string line = issue == null ? null : Loc.T(issue.Tagline) + "\n" + Loc.T("PAYS") + " " + StreetCash.Money(issue.Cash) + "  +" + issue.Sp + " SP  ·  "
                                                 + Loc.T("ISSUE") + " " + issue.Number + "/" + CityStory.Count;
            _hud.ShowCard(header, line, list, a =>
            {
                switch (a.ItemId)
                {
                    case "read":
                        CloseCard();
                        if (issue != null) ShowPanels(CityStory.Header(issue), Loc.T(issue.Tagline), issue.Intro, issue.Region, null);
                        break;
                    case "guide":
                        w.tracking = tracking ? "off" : "";
                        _guideAt = 0f;
                        App.SaveCareer();
                        OpenComic();
                        break;
                    case "story":
                        w.tracking = "story";
                        _guideAt = 0f;
                        App.SaveCareer();
                        OpenComic();
                        break;
                    case "side":
                        if (CitySideJobs.Active(w) != null) w.tracking = "side:" + w.sideActive;
                        _guideAt = 0f;
                        App.SaveCareer();
                        OpenComic();
                        break;
                    default: OpenMenu(); break;
                }
            }, null);
        }

        private void OpenAchievements()
        {
            var w = World;
            CityAchievements.Roll(w, _day);
            var defs = CityAchievements.For(_day);
            var list = new List<CityActivity>();
            for (int i = 0; i < defs.Count; i++)
            {
                int now = Math.Min(defs[i].Target, CityAchievements.Value(w, defs[i].Kind));
                bool done = now >= defs[i].Target;
                list.Add(new CityActivity { Label = (done ? Loc.T("DONE") + ": " : "") + Loc.T(defs[i].Text) + "  " + now + "/" + defs[i].Target, Action = CityAction.Info });
            }
            var badges = new CityActivity { Label = Loc.T("BADGES") + " " + w.badges.Count + "/" + CityAchievements.Badges.Length, Action = CityAction.Comic, ItemId = "badges" };
            list.Add(badges);
            int done6 = CityAchievements.DoneCount(w, _day);
            string tiers = "";
            for (int t = 0; t < CityAchievements.TierAt.Length; t++)
                tiers += (t > 0 ? "  ·  " : "") + (w.achTierPaid > t ? Loc.T("DONE") + " " : "") + Loc.T(CityAchievements.TierNames[t]) + " " + CityAchievements.TierAt[t] + ": " + StreetCash.Money(CityAchievements.TierCash[t]);
            string line = Loc.T("Six a day. New ones tomorrow.") + "  " + done6 + "/" + CityAchievements.PerDay + "\n" + tiers + "\n"
                          + Loc.T("STREAK") + " " + CityAchievements.StreakNow(w, _day) + " (" + Loc.T("BEST") + " " + w.achBestStreak + ")  ·  " + Loc.T("GOLD DAYS") + " " + w.achGoldDays;
            _hud.ShowCard(Loc.T("DAILY ACHIEVEMENTS"), line, list, a =>
            {
                if (ReferenceEquals(a, badges)) OpenBadges();
                else OpenMenu();
            }, null);
        }

        private void OpenBadges()
        {
            var w = World;
            var list = new List<CityActivity>();
            foreach (var b in CityAchievements.Badges)
            {
                bool got = w.badges.Contains(b.Id);
                list.Add(new CityActivity { Label = (got ? Loc.T("DONE") + ": " : "") + Loc.T(b.Name), Detail = Loc.T(b.Detail) + "  ·  +" + b.Sp + " SP" + (b.Cash > 0 ? "  +" + StreetCash.Money(b.Cash) : ""), Action = CityAction.Info });
            }
            _hud.ShowCard(Loc.T("BADGES"), w.badges.Count + "/" + CityAchievements.Badges.Length, list, a => OpenAchievements(), null);
        }

        private void DestroyWorldOwned()
        {
            foreach (var o in _worldOwned) if (o != null) Destroy(o);
            _worldOwned.Clear();
            if (_driveView != null) Destroy(_driveView.gameObject);
        }
    }
}
