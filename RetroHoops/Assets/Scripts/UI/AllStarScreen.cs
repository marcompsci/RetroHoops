using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// All-Star Weekend, drawn over the current screen: the hub (Dunk Contest, 3-Point Contest,
    /// All-Star Game) and the Dunk Contest itself — pick a dunk, enter its combo before the clock runs
    /// out, time the slam on the meter, watch it, and get five judges' cards. Rules in <see cref="AllStar"/>.
    /// </summary>
    public sealed class AllStarScreen : MonoBehaviour
    {
        private enum State { Hub, Pick, Combo, Slam, Show, Judges, Done }

        private State _state;
        private bool _rise;
        private RectTransform _root;
        private RectTransform _content;
        private Canvas _canvas;
        private readonly List<Object> _owned = new List<Object>();

        // Dunk attempt
        private DunkMove _move;
        private int _attempt;
        private int _entered;
        private float _comboStart;
        private float _execution;
        private float _slamStart;
        private float _meter;
        private RawImage[] _comboIcons;
        private Image _timerFill;
        private RectTransform _meterCursor;
        private RawImage _dunker;
        private RawImage _ball;
        private RectTransform _rim;
        private TextMeshProUGUI _callout;
        private Texture2D _sheet;
        private Texture2D[] _arrows;
        private float _showStart;
        private bool _made;
        private float _timing;
        private DunkScore _score;
        private TextMeshProUGUI[] _cards;
        private float _cardsStart;

        private const float MeterPeriod = 1.1f;
        private const float GreenHalf = 0.09f;
        private const float ShowSeconds = 2.3f;

        private static ContestSaveData Contest => App.Career.allStar.contest;

        /// <summary>Opens the hub. <paramref name="rise"/>: the Rise Mode weekend (pays out and is marked done).</summary>
        public static AllStarScreen Open(bool rise)
        {
            var existing = FindAnyObjectByType<AllStarScreen>();
            if (existing != null) Destroy(existing.gameObject);
            var go = new GameObject("AllStarScreen");
            var screen = go.AddComponent<AllStarScreen>();
            screen._rise = rise;
            screen.Build();
            return screen;
        }

        private void Build()
        {
            _canvas = UiKit.CreateScreenCanvas("AllStarCanvas", 36);
            _canvas.transform.SetParent(transform, false);
            var bg = UiKit.Panel(_canvas.transform, Theme.Ink, name: "Backdrop");
            UiKit.Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            _root = UiKit.SafeArea(_canvas.transform);
            _arrows = new Texture2D[4];
            for (int i = 0; i < 4; i++) _arrows[i] = Own(TextureFactory.ToTexture(ArrowArt((DunkInput)i), "ui.dunk.arrow" + i));
            // A dunk contest in progress opens straight into it.
            if (Contest.kind == "dunk") ShowPick();
            else if (Contest.kind == "three") ShowThree();
            else ShowHub();
        }

        private T Own<T>(T o) where T : Object { _owned.Add(o); return o; }

        private void OnDestroy()
        {
            foreach (var o in _owned) if (o != null) Destroy(o);
        }

        private void Close() => Destroy(gameObject);

        private void Clear()
        {
            for (int i = _root.childCount - 1; i >= 0; i--) Destroy(_root.GetChild(i).gameObject);
            _comboIcons = null;
            _timerFill = null;
            _meterCursor = null;
            _dunker = null;
            _cards = null;
        }

        private RectTransform Scroll(string title, out RectTransform footer)
        {
            Clear();
            var head = UiKit.ShadowLabel(_root, title, 60f, Theme.Cream, Theme.Pink, 6f);
            UiKit.Band((RectTransform)head.transform.parent, 0.91f, 0.99f, 24f);
            footer = UiKit.Row(_root, 20f, "Footer");
            UiKit.Band(footer, 0.01f, 0.08f, 40f);
            var holder = UiKit.NewRect("Body", _root);
            UiKit.Band(holder, 0.09f, 0.9f, 0f);
            _content = UiKit.ScrollColumn(holder, 16f, new RectOffset(40, 40, 10, 40));
            return _content;
        }

        private TextMeshProUGUI Text(Transform parent, string text, float size, Color color, float height, bool bold = false)
        {
            var l = UiKit.Label(parent, text, size, color, TextAlignmentOptions.Center, bold);
            l.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Size(l, height);
            return l;
        }

        // ------------------------------------------------------------------ hub

        private void ShowHub()
        {
            _state = State.Hub;
            var career = App.Career;
            var a = career.allStar;
            var col = Scroll(_rise ? "ALL-STAR WEEKEND" : "ALL-STAR CONTESTS", out var footer);
            var weekend = _rise ? AllStar.Weekend(career) : null;
            Text(col, _rise
                ? "Halfway through the season the league's best get together. You're in all three events. Win a contest for +" + AllStar.ContestTitleBonus + " SP."
                : "The Dunk Contest and the 3-Point Contest against the league's stars, and the All-Star Game. Win a contest for +" + AllStar.ContestTitleBonus + " SP.",
                30f, Theme.Cream, 130f);

            string Done(bool d) => d ? "  ·  <color=#4CC9F0>" + Loc.T("DONE") + "</color>" : "";
            bool dunkLive = Contest.kind == "dunk", threeLive = Contest.kind == "three";
            UiKit.Button(col, dunkLive ? "DUNK CONTEST (CONTINUE)" : "DUNK CONTEST", () => StartContest("dunk"), ButtonStyle.Primary, 120f, 44f);
            Text(col, Loc.T("Pick a dunk, enter its combo, time the slam. Five judges, 50 points a dunk.") + Done(weekend != null && weekend.dunkDone)
                 + "  ·  " + Loc.T("titles") + " " + a.dunkTitles, 26f, Theme.Muted, 70f);
            UiKit.Button(col, threeLive ? "3-POINT CONTEST (CONTINUE)" : "3-POINT CONTEST", () => StartContest("three"), ButtonStyle.Primary, 120f, 44f);
            Text(col, Loc.T("60 seconds against the league's best shooters, then a final.") + Done(weekend != null && weekend.threeDone)
                 + "  ·  " + Loc.T("titles") + " " + a.threeTitles, 26f, Theme.Muted, 70f);
            UiKit.Button(col, "ALL-STAR GAME", () =>
            {
                App.PendingMatch = AllStar.GameRequest(App.Catalog, App.Career, App.Career.settings.difficultyId);
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Primary, 120f, 44f);
            Text(col, Loc.T("Team Sunrise vs Team Moonlight, Full Court. You start.") + Done(weekend != null && weekend.gameDone)
                 + "  ·  " + Loc.T("record") + " " + a.allStarWins + "-" + (a.allStarGames - a.allStarWins), 26f, Theme.Muted, 70f);
            if (a.bestDunk > 0) UiControls.Stat(col, "BEST DUNK ROUND", a.bestDunk + " / 100");
            UiKit.Button(footer, "BACK", Close, ButtonStyle.Ghost, 120f, 44f);
        }

        private void StartContest(string kind)
        {
            var career = App.Career;
            if (Contest.Active && Contest.kind != kind)
            {
                UiControls.Dialog("LEAVE THE " + AllStar.Title(Contest.kind) + "?", "Only one contest at a time. Starting this one ends the other.",
                    ("START", ButtonStyle.Primary, () => { career.allStar.contest = new ContestSaveData(); StartContest(kind); }),
                    ("CANCEL", ButtonStyle.Ghost, null));
                return;
            }
            if (!Contest.Active)
                career.allStar.contest = AllStar.Start(App.Catalog, kind, career.nickname.ToUpperInvariant(), (uint)System.Environment.TickCount | 1u,
                                                       _rise && AllStar.WeekendOpen(career), career.settings.difficultyId);
            App.SaveCareer();
            if (kind == "dunk") ShowPick();
            else ShowThree();
        }

        // ------------------------------------------------------------------ 3-point contest

        private void ShowThree()
        {
            var s = Contest;
            var col = Scroll("3-POINT CONTEST", out var footer);
            Standings(col, s);
            if (s.round >= 3)
            {
                Finish(col, footer, s);
                return;
            }
            Text(col, (s.round == 1 ? Loc.T("ROUND ONE") : Loc.T("THE FINAL")) + "\n" + Loc.T("Score") + " " + AllStar.Target(s) + " "
                 + (s.round == 1 ? Loc.T("to make the final.") : Loc.T("to win it.")), 34f, Theme.Gold, 110f, true);
            Text(col, "60 seconds of threes. Shots from the gold spot are money balls: 2 points.", 28f, Theme.Muted, 80f);
            UiKit.Button(footer, "BACK", ShowHub, ButtonStyle.Ghost, 120f, 44f);
            UiKit.Button(footer, "SHOOT", () =>
            {
                App.PendingMatch = AllStar.ThreeRound(s, App.Career.settings.difficultyId);
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Primary, 120f, 44f);
        }

        private void Standings(Transform col, ContestSaveData s)
        {
            var order = new List<ContestEntrant>(s.field);
            order.Sort((x, y) => Score(y).CompareTo(Score(x)));
            foreach (var e in order)
            {
                bool fin = AllStar.InFinal(s, e);
                string line = e.name.ToUpperInvariant() + "    " + (e.r1 >= 0 ? e.r1.ToString() : "—") + (s.round >= 2 && fin ? "   " + Loc.T("FINAL") + " " + (e.r2 >= 0 ? e.r2.ToString() : "—") : "");
                Text(col, line, 30f, e.you ? Theme.Gold : (s.round >= 2 && !fin ? Theme.Muted : Theme.Cream), 48f, e.you);
            }
            int Score(ContestEntrant e) => (e.r2 >= 0 ? 1000 + e.r2 : 0) + e.r1;
        }

        private void Finish(Transform col, Transform footer, ContestSaveData s)
        {
            _state = State.Done;
            Text(col, (s.youWon ? Loc.T("YOU WIN THE") : s.championName.ToUpperInvariant() + " " + Loc.T("WINS THE")) + " " + Loc.T(AllStar.Title(s.kind)),
                 44f, s.youWon ? Theme.Gold : Theme.Cream, 130f, true);
            int bonus = AllStar.Settle(App.Career, s);
            if (bonus > 0) Text(col, "+" + bonus + " SP", 40f, Theme.Cyan, 60f, true);
            Audio.AudioManager.Play(s.youWon ? SfxId.Fanfare : SfxId.CrowdGroan, 0.8f);
            App.SaveCareer();
            App.ReportGameCenter();
            UiKit.Button(footer, "DONE", ShowHub, ButtonStyle.Primary, 120f, 44f);
        }

        // ------------------------------------------------------------------ dunk contest: pick

        private void ShowPick()
        {
            var s = Contest;
            _state = State.Pick;
            var col = Scroll("DUNK CONTEST", out var footer);
            Standings(col, s);
            if (s.round >= 3)
            {
                Finish(col, footer, s);
                return;
            }
            var me = AllStar.You(s);
            Text(col, (s.round == 1 ? Loc.T("ROUND ONE") : Loc.T("THE FINAL")) + " · " + Loc.T("DUNK") + " " + (s.yourDunks + 1) + "/" + AllStar.DunksPerRound
                      + (s.yourDunks > 0 ? "  ·  " + Loc.T("so far") + " " + s.yourRoundTotal : "") + "\n" + Loc.T("Two dunks totalling") + " " + AllStar.Target(s) + " "
                      + (s.round == 1 ? Loc.T("make the final.") : Loc.T("win it.")), 32f, Theme.Gold, 110f, true);
            Text(col, "Harder dunks score more but have longer combos. Judges mark down repeats. Two tries per dunk.", 26f, Theme.Muted, 70f);
            foreach (var m in AllStar.Moves)
            {
                var move = m;
                int used = me.used.FindAll(n => n == m.Name).Count;
                var row = UiKit.Row(col, 10f, "Dunk");
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                UiKit.Size(row, 96f);
                var label = UiKit.Label(row, "<b>" + m.Name + "</b>\n<size=22><color=#8D99AE>" + Loc.T("DIFFICULTY") + " " + new string('|', m.Difficulty)
                                        + "  ·  " + m.Combo.Length + " " + Loc.T("moves") + (used > 0 ? "  ·  <color=#F72585>" + Loc.T("USED") + "</color>" : "") + "</color></size>",
                                        28f, Theme.Cream, TextAlignmentOptions.Left);
                UiKit.Size(label).flexibleWidth = 1f;
                var b = UiKit.Button(row, "GO", () => BeginDunk(move), ButtonStyle.Primary, 86f, 30f);
                UiKit.Size(b, 86f, 150f);
            }
            UiKit.Button(footer, "BACK", ShowHub, ButtonStyle.Ghost, 120f, 44f);
        }

        // ------------------------------------------------------------------ dunk: stage

        private void BuildStage(string title)
        {
            Clear();
            var head = UiKit.ShadowLabel(_root, title, 52f, Theme.Cream, Theme.Pink, 6f);
            UiKit.Band((RectTransform)head.transform.parent, 0.92f, 0.99f, 24f);

            var stage = UiKit.Panel(_root, new Color32(0x22, 0x22, 0x3A, 255), name: "Stage");
            UiKit.Band(stage.rectTransform, 0.5f, 0.91f, 24f);
            var floor = UiKit.Panel(stage.transform, new Color32(0xC8, 0x8A, 0x4E, 255), name: "Floor");
            floor.rectTransform.anchorMin = new Vector2(0f, 0f);
            floor.rectTransform.anchorMax = new Vector2(1f, 0.18f);
            floor.rectTransform.offsetMin = floor.rectTransform.offsetMax = Vector2.zero;
            // Crowd: rows of pixel heads behind the floor.
            var crowd = UiKit.Picture(stage.transform, Own(TextureFactory.ToTexture(CrowdArt(), "ui.dunk.crowd")), "Crowd");
            crowd.rectTransform.anchorMin = new Vector2(0f, 0.18f);
            crowd.rectTransform.anchorMax = new Vector2(1f, 0.42f);
            crowd.rectTransform.offsetMin = crowd.rectTransform.offsetMax = Vector2.zero;

            // Hoop: pole, backboard, rim, net.
            var pole = UiKit.Panel(stage.transform, new Color32(0x58, 0x58, 0x68, 255), name: "Pole");
            Place(pole.rectTransform, 0.9f, 0.18f, 18f, 0f, 0.62f);
            var board = UiKit.Panel(stage.transform, new Color32(0xF4, 0xF1, 0xDE, 255), name: "Board");
            Place(board.rectTransform, 0.84f, 0.62f, 22f, 150f, 0f);
            var rim = UiKit.Panel(stage.transform, new Color32(0xFF, 0x6B, 0x1A, 255), name: "Rim");
            Place(rim.rectTransform, 0.775f, 0.66f, 100f, 12f, 0f);
            _rim = rim.rectTransform;
            var net = UiKit.Panel(stage.transform, new Color(1f, 1f, 1f, 0.6f), name: "Net");
            Place(net.rectTransform, 0.775f, 0.58f, 70f, 60f, 0f);

            _dunker = UiKit.Picture(stage.transform, _sheet, "Dunker");
            _dunker.rectTransform.anchorMin = _dunker.rectTransform.anchorMax = Vector2.zero;
            _dunker.rectTransform.pivot = new Vector2(0.5f, 0f);
            _dunker.rectTransform.sizeDelta = new Vector2(CharacterSpriteGenerator.FrameWidth * 9f, CharacterSpriteGenerator.FrameHeight * 9f);
            _ball = UiKit.Picture(stage.transform, Own(TextureFactory.ToTexture(PropSpriteGenerator.Ball(), "ui.dunk.ball")), "Ball");
            _ball.rectTransform.anchorMin = _ball.rectTransform.anchorMax = Vector2.zero;
            _ball.rectTransform.sizeDelta = new Vector2(54f, 54f);
            _callout = UiKit.Label(stage.transform, "", 64f, Theme.Gold, TextAlignmentOptions.Center, true);
            _callout.rectTransform.anchorMin = new Vector2(0f, 0.7f);
            _callout.rectTransform.anchorMax = new Vector2(0.7f, 0.98f);
            _callout.rectTransform.offsetMin = _callout.rectTransform.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            Pose(0.15f, 0f, CharacterView.Side, 0, false, 0f);
            BallAt(0.17f, 0.3f);
        }

        private static void Place(RectTransform rt, float x, float y, float w, float h, float hFrac)
        {
            rt.anchorMin = new Vector2(x, y);
            rt.anchorMax = new Vector2(x, hFrac > 0f ? y + hFrac : y);
            rt.pivot = new Vector2(0.5f, hFrac > 0f ? 0f : 0.5f);
            rt.sizeDelta = new Vector2(w, hFrac > 0f ? 0f : h);
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>Puts the dunker at (x, height above the floor) in stage fractions with a pose and spin.</summary>
        private void Pose(float x, float lift, CharacterView view, int frame, bool flip, float degrees)
        {
            if (_dunker == null || _dunker.texture == null) return;
            var parent = (RectTransform)_dunker.rectTransform.parent;
            var size = parent.rect.size;
            _dunker.rectTransform.anchoredPosition = new Vector2(x * size.x, size.y * (0.18f + lift));
            _dunker.rectTransform.localEulerAngles = new Vector3(0f, 0f, degrees);
            float w = (float)CharacterSpriteGenerator.SheetFrameWidth / _dunker.texture.width;
            float h = (float)CharacterSpriteGenerator.SheetFrameHeight / _dunker.texture.height;
            CharacterSpriteGenerator.FrameOrigin(view, frame, out int fx, out int fy);
            float u = (float)fx / _dunker.texture.width, v = (float)fy / _dunker.texture.height;
            _dunker.uvRect = flip ? new Rect(u + w, v, -w, h) : new Rect(u, v, w, h);
        }

        private void BallAt(float x, float y)
        {
            var size = ((RectTransform)_ball.rectTransform.parent).rect.size;
            _ball.rectTransform.anchoredPosition = new Vector2(x * size.x, y * size.y);
        }

        // ------------------------------------------------------------------ dunk: attempt

        private void BeginDunk(DunkMove move)
        {
            _move = move;
            _attempt = 1;
            if (_sheet == null)
            {
                var c = App.Catalog;
                var me = PlayerCreator.ForMatch(App.Career, c);
                var crew = c.Team(DefaultContent.PlayerCrewId);
                var kit = Kits.ForMatch(App.Career.kits, RgbColor.White, ColorFilter.Off, out _);
                var canvas = kit != null ? CharacterSpriteGenerator.GenerateSheet(me.appearance, Kits.Look(kit))
                                         : CharacterSpriteGenerator.GenerateSheet(me.appearance, crew.primary, crew.secondary, crew.accent);
                _sheet = Own(TextureFactory.ToTexture(canvas, "ui.dunk.sheet"));
            }
            StartCombo();
        }

        private void StartCombo()
        {
            _state = State.Combo;
            BuildStage(_move.Name + (_attempt == 2 ? "  ·  " + Loc.T("TRY 2") : ""));
            _entered = 0;
            _comboStart = Time.unscaledTime;
            _callout.text = Loc.T("ENTER THE COMBO!");

            var row = UiKit.Row(_root, 12f, "Combo");
            UiKit.Band(row, 0.42f, 0.49f, 60f);
            _comboIcons = new RawImage[_move.Combo.Length];
            for (int i = 0; i < _move.Combo.Length; i++)
            {
                _comboIcons[i] = UiKit.Picture(row, _arrows[(int)_move.Combo[i]], "Step");
                _comboIcons[i].color = new Color(0.55f, 0.55f, 0.65f, 1f);
                _comboIcons[i].gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            }
            var bar = UiKit.Panel(_root, Theme.InkLight, name: "Timer");
            UiKit.Band(bar.rectTransform, 0.395f, 0.41f, 60f);
            _timerFill = UiKit.Panel(bar.transform, Theme.Cyan, name: "Fill");
            UiKit.Stretch(_timerFill.rectTransform);
            _timerFill.rectTransform.pivot = new Vector2(0f, 0.5f);

            // Pad: four arrows in a cross.
            var pad = UiKit.NewRect("Pad", _root);
            UiKit.Band(pad, 0.04f, 0.37f, 40f);
            foreach (DunkInput d in new[] { DunkInput.Up, DunkInput.Left, DunkInput.Right, DunkInput.Down })
            {
                var input = d;
                var b = UiKit.Button(pad, "", () => Press(input), ButtonStyle.Secondary, 200f, 10f);
                var rt = (RectTransform)b.transform;
                Vector2 at = d == DunkInput.Up ? new Vector2(0.5f, 0.82f) : d == DunkInput.Down ? new Vector2(0.5f, 0.18f)
                           : d == DunkInput.Left ? new Vector2(0.24f, 0.5f) : new Vector2(0.76f, 0.5f);
                UiKit.Place(rt, at, new Vector2(230f, 200f));
                var icon = UiKit.Picture(b.transform, _arrows[(int)d], "Arrow");
                UiKit.Stretch(icon.rectTransform, 40f);
                icon.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            }
        }

        private void Press(DunkInput input)
        {
            if (_state != State.Combo) return;
            if (_move.Combo[_entered] == input)
            {
                _comboIcons[_entered].color = Theme.Gold;
                _entered++;
                Audio.AudioManager.Play(SfxId.Squeak, 0.5f, 1f + 0.08f * _entered);
                if (_entered >= _move.Combo.Length)
                {
                    float left = 1f - (Time.unscaledTime - _comboStart) / AllStar.ComboSeconds(_move);
                    _execution = Mathf.Clamp01(0.35f + 0.75f * left);
                    StartSlam();
                }
            }
            else
            {
                _comboIcons[_entered].color = Theme.Pink;
                Blown(Loc.T("WRONG MOVE"));
            }
        }

        private void Blown(string why)
        {
            Audio.AudioManager.Play(SfxId.Error, 0.7f);
            Haptics.Light();
            if (_attempt == 1)
            {
                _state = State.Pick;
                UiControls.Dialog(why, "One more try at the " + _move.Name + ".", ("TRY AGAIN", ButtonStyle.Primary, () =>
                {
                    _attempt = 2;
                    StartCombo();
                }));
                return;
            }
            _made = false;
            _timing = 0f;
            _execution = 0f;
            StartShow();
        }

        private void StartSlam()
        {
            _state = State.Slam;
            _slamStart = Time.unscaledTime;
            _callout.text = Loc.T("SLAM IT!");
            var pad = _root.Find("Pad");
            if (pad != null) Destroy(pad.gameObject);
            var meter = UiKit.Panel(_root, Theme.InkLight, name: "Meter");
            UiKit.Band(meter.rectTransform, 0.3f, 0.36f, 80f);
            var green = UiKit.Panel(meter.transform, new Color32(0x3A, 0xD1, 0x6B, 255), name: "Green");
            green.rectTransform.anchorMin = new Vector2(0.5f - GreenHalf, 0f);
            green.rectTransform.anchorMax = new Vector2(0.5f + GreenHalf, 1f);
            green.rectTransform.offsetMin = green.rectTransform.offsetMax = Vector2.zero;
            var cursor = UiKit.Panel(meter.transform, Theme.Cream, name: "Cursor");
            cursor.rectTransform.anchorMin = new Vector2(0f, -0.2f);
            cursor.rectTransform.anchorMax = new Vector2(0f, 1.2f);
            cursor.rectTransform.sizeDelta = new Vector2(16f, 0f);
            _meterCursor = cursor.rectTransform;
            var jam = UiKit.Button(_root, "JAM!", Jam, ButtonStyle.Primary, 200f, 64f);
            UiKit.Band((RectTransform)jam.transform, 0.08f, 0.26f, 160f);
        }

        private void Jam()
        {
            if (_state != State.Slam) return;
            _timing = AllStar.SlamTiming(_meter);
            _made = true;
            StartShow();
        }

        // ------------------------------------------------------------------ dunk: show and judges

        private void StartShow()
        {
            _state = State.Show;
            _showStart = Time.unscaledTime;
            _callout.text = "";
            foreach (var n in new[] { "Meter", "Pad", "Combo", "Timer" })
            {
                var t = _root.Find(n);
                if (t != null) Destroy(t.gameObject);
            }
            foreach (var b in _root.GetComponentsInChildren<Button>()) if (b.name.Contains("JAM")) Destroy(b.gameObject);
        }

        private void UpdateShow(float t)
        {
            bool reverse = _move.Reverse;
            float rimX = 0.775f, rimY = 0.66f;
            if (t < 0.7f)
            {
                // Run-up (from the far side on a reverse).
                float k = t / 0.7f;
                float x = reverse ? Mathf.Lerp(1.05f, 0.95f, k) : Mathf.Lerp(0.1f, 0.5f, k);
                int frame = CharacterSpriteGenerator.IdleFrames + (int)(t / 0.1f) % CharacterSpriteGenerator.RunFrames;
                Pose(x, 0f, CharacterView.Side, frame, reverse, 0f);
                BallAt(x + (reverse ? -0.02f : 0.02f), 0.3f + 0.04f * Mathf.Abs(Mathf.Sin(t * 18f)));
            }
            else if (t < 1.35f)
            {
                // Takeoff to the rim, spinning for the turns.
                float k = (t - 0.7f) / 0.65f;
                float startX = reverse ? 0.95f : 0.5f;
                float x = Mathf.Lerp(startX, rimX - (reverse ? -0.03f : 0.06f), k);
                float lift = Mathf.Sin(k * Mathf.PI * 0.5f) * (rimY - 0.18f - 0.12f);
                Pose(x, lift, CharacterView.Front, CharacterSpriteGenerator.ShootFrame, reverse, -360f * _move.Turns * k);
                BallAt(x, 0.18f + lift + 0.26f);
            }
            else if (t < 1.75f)
            {
                float k = (t - 1.35f) / 0.4f;
                if (_made)
                {
                    // Through the rim; hang on it.
                    if (k < 0.2f && _callout.text.Length == 0)
                    {
                        _callout.text = _timing > 0.8f ? Loc.T("SLAM!") : Loc.T("JAM!");
                        Audio.AudioManager.Play(SfxId.Backboard, 1f);
                        Audio.AudioManager.Play(SfxId.CrowdCheer, 0.9f);
                        Haptics.Heavy();
                    }
                    BallAt(rimX, Mathf.Lerp(rimY + 0.02f, 0.2f, k));
                    _rim.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(k * 30f) * 6f * (1f - k));
                    Pose(rimX - 0.05f, rimY - 0.18f - 0.14f, CharacterView.Front, CharacterSpriteGenerator.ShootFrame, reverse, 0f);
                }
                else
                {
                    if (k < 0.2f && _callout.text.Length == 0)
                    {
                        _callout.text = Loc.T("BLOWN!");
                        Audio.AudioManager.Play(SfxId.Rim, 1f);
                        Audio.AudioManager.Play(SfxId.CrowdGroan, 0.8f);
                    }
                    BallAt(Mathf.Lerp(rimX, 0.45f, k), rimY + 0.15f * Mathf.Sin(k * Mathf.PI));
                    Pose(rimX - 0.1f, (rimY - 0.3f) * (1f - k), CharacterView.Front, CharacterSpriteGenerator.ShootFrame, reverse, 0f);
                }
            }
            else
            {
                float k = Mathf.Clamp01((t - 1.75f) / 0.35f);
                Pose(rimX - 0.08f, (rimY - 0.32f) * (1f - k), CharacterView.Front, 0, reverse, 0f);
                _rim.localEulerAngles = Vector3.zero;
            }
        }

        private void ShowJudges()
        {
            _state = State.Judges;
            var s = Contest;
            _score = AllStar.RecordDunk(s, App.Catalog, _move, _made, _execution, _timing);
            App.SaveCareer();
            var row = UiKit.Row(_root, 14f, "Judges");
            UiKit.Band(row, 0.26f, 0.46f, 40f);
            _cards = new TextMeshProUGUI[AllStar.JudgeCount];
            for (int j = 0; j < AllStar.JudgeCount; j++)
            {
                var card = UiKit.Panel(row, Color.white, Theme.PanelSprite(), true, "Card");
                var col = UiKit.Column(card.transform, 4f, new RectOffset(4, 4, 10, 10));
                UiKit.Stretch(col);
                Text(col, AllStar.JudgeNames[j], 18f, Theme.Muted, 50f, true);
                _cards[j] = Text(col, "", 72f, Theme.Gold, 100f, true);
            }
            _cardsStart = Time.unscaledTime;
            var total = UiKit.Label(_root, "", 44f, Theme.Cream, TextAlignmentOptions.Center, true, "Total");
            UiKit.Band(total.rectTransform, 0.17f, 0.25f, 40f);
            var next = UiKit.Button(_root, "NEXT", () => ShowPick(), ButtonStyle.Primary, 120f, 44f);
            UiKit.Band((RectTransform)next.transform, 0.03f, 0.12f, 200f);
            next.gameObject.SetActive(false);
        }

        private void UpdateJudges()
        {
            if (_cards == null || _score == null) return;
            float t = Time.unscaledTime - _cardsStart;
            int shown = Mathf.Min(AllStar.JudgeCount, (int)(t / 0.4f));
            for (int j = 0; j < AllStar.JudgeCount; j++)
            {
                if (j < shown && _cards[j].text.Length == 0)
                {
                    _cards[j].text = _score.Judges[j].ToString();
                    _cards[j].color = _score.Judges[j] == 10 ? Theme.Cyan : (_score.Judges[j] <= 6 ? Theme.Pink : Theme.Gold);
                    Audio.AudioManager.Play(SfxId.Click, 0.8f, 0.9f + 0.05f * _score.Judges[j]);
                }
            }
            if (shown >= AllStar.JudgeCount)
            {
                var total = _root.Find("Total");
                if (total != null)
                {
                    var label = total.GetComponent<TextMeshProUGUI>();
                    if (label.text.Length == 0)
                    {
                        label.text = Loc.T("TOTAL") + "  " + _score.Total + " / 50" + (_score.Total == 50 ? "  ·  " + Loc.T("PERFECT!") : "");
                        if (_score.Total >= 48) Audio.AudioManager.Play(SfxId.Fanfare, 0.7f);
                    }
                }
                foreach (var b in _root.GetComponentsInChildren<Button>(true)) if (b.name.Contains("NEXT")) b.gameObject.SetActive(true);
            }
        }

        // ------------------------------------------------------------------ loop

        private void Update()
        {
            switch (_state)
            {
                case State.Combo:
                {
                    float left = 1f - (Time.unscaledTime - _comboStart) / AllStar.ComboSeconds(_move);
                    if (_timerFill != null) _timerFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(left), 1f, 1f);
                    if (left <= 0f) Blown(Loc.T("TOO SLOW"));
                    else if (KeyDown(out var key)) Press(key);
                    break;
                }
                case State.Slam:
                {
                    float p = (Time.unscaledTime - _slamStart) / MeterPeriod;
                    _meter = Mathf.PingPong(p * 2f, 1f);
                    if (_meterCursor != null)
                    {
                        _meterCursor.anchorMin = new Vector2(_meter, -0.2f);
                        _meterCursor.anchorMax = new Vector2(_meter, 1.2f);
                    }
                    if (JamPressed()) Jam();
                    break;
                }
                case State.Show:
                {
                    float t = Time.unscaledTime - _showStart;
                    UpdateShow(t);
                    if (t >= ShowSeconds) ShowJudges();
                    break;
                }
                case State.Judges:
                    UpdateJudges();
                    break;
                case State.Hub:
                case State.Pick:
                case State.Done:
                    if (BackPressed()) { if (_state == State.Hub) Close(); else ShowHub(); }
                    break;
            }
        }

        private static bool KeyDown(out DunkInput input)
        {
            input = DunkInput.Up;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if ((kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)) || (pad != null && pad.dpad.up.wasPressedThisFrame)) { input = DunkInput.Up; return true; }
            if ((kb != null && (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)) || (pad != null && pad.dpad.down.wasPressedThisFrame)) { input = DunkInput.Down; return true; }
            if ((kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (pad != null && pad.dpad.left.wasPressedThisFrame)) { input = DunkInput.Left; return true; }
            if ((kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (pad != null && pad.dpad.right.wasPressedThisFrame)) { input = DunkInput.Right; return true; }
#endif
            return false;
        }

        private static bool JamPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            return (kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
#else
            return false;
#endif
        }

        private static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
#else
            return false;
#endif
        }

        // ------------------------------------------------------------------ pixel art

        /// <summary>A chunky 9×9 arrow pointing the way of the input.</summary>
        private static PixelCanvas ArrowArt(DunkInput dir)
        {
            var c = new PixelCanvas(9, 9);
            var fill = RgbColor.White;
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 9; x++)
                {
                    // Up arrow in canvas space (y = 0 is the bottom row): head on rows 4..8, shaft on 0..4.
                    bool head = y >= 4 && System.Math.Abs(x - 4) <= 8 - y;
                    bool shaft = y <= 4 && x >= 3 && x <= 5;
                    if (!head && !shaft) continue;
                    int px = x, py = y;
                    switch (dir)
                    {
                        case DunkInput.Down: py = 8 - y; break;
                        case DunkInput.Right: px = y; py = x; break;
                        case DunkInput.Left: px = 8 - y; py = x; break;
                    }
                    c.Set(px, py, fill);
                }
            return c;
        }

        /// <summary>Rows of crowd heads in random team colours.</summary>
        private static PixelCanvas CrowdArt()
        {
            var c = new PixelCanvas(96, 18);
            var rng = new SeededRandom(17);
            RgbColor[] shirts = { RgbColor.FromHex("#E63946"), RgbColor.FromHex("#4CC9F0"), RgbColor.FromHex("#FFD166"), RgbColor.FromHex("#7B2CBF"), RgbColor.FromHex("#2A9D4F") };
            for (int row = 0; row < 3; row++)
                for (int x = row % 2; x < 96; x += 3)
                {
                    int baseY = 1 + row * 6;
                    var shirt = shirts[rng.Range(0, shirts.Length)];
                    var skin = CharacterSpriteGenerator.SkinTones[rng.Range(0, CharacterSpriteGenerator.SkinTones.Length)];
                    c.Set(x, baseY, shirt);
                    c.Set(x + 1, baseY, shirt);
                    c.Set(x, baseY + 1, shirt);
                    c.Set(x + 1, baseY + 1, shirt);
                    c.Set(x, baseY + 2, skin);
                    c.Set(x + 1, baseY + 2, skin);
                }
            return c;
        }
    }
}
