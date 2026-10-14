using System;
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
    /// A full-screen CREATE A PLAYER overlay (10 Oct 2026, Rise Mode).
    /// 15 Oct 2026 (Omari): redesigned to be more interactive.
    /// - A big live preview stays on screen. It shows your player in the clothes he really wears in Rise: your crew's
    ///   kit from the Kit Studio, or the crew colours with your equipped jersey and shoes.
    /// - The preview moves (idle, run, jumper). The ◄ ► buttons turn him to the front, side and back, where his
    ///   number is printed.
    /// - The steps are YOU (name, number), LOOK (swatches for skin and hair, build and height), OUTFIT (kit, jersey,
    ///   shorts, shoes and their colours) and GAME (style of play).
    /// - <see cref="Options.Wizard"/> walks through the steps with BACK / NEXT. The first-launch onboarding uses it.
    /// The caller decides what SAVE does (your Rise player or a created teammate); the rules live in
    /// <see cref="RiseCreate"/> and <see cref="RiseOutfit"/>.
    /// </summary>
    public sealed class PlayerMaker
    {
        private static readonly string[] HairColorNames = { "BLACK", "DARK BROWN", "BROWN", "BLONDE", "RED" };
        private static readonly string[] BodyNames = { "SLIM", "STANDARD", "BROAD" };
        private static readonly string[] HeightNames = { "SHORT", "AVERAGE", "TALL" };

        public struct Options
        {
            public string Title;
            /// <summary>Your own player: one name (your nickname), no last name.</summary>
            public bool Mine;
            public string SaveLabel;
            /// <summary>Shown under the style (e.g. the OVR he starts at). Gets the current spec.</summary>
            public Func<CreatedPlayerSpec, string> Info;
            public Action<CreatedPlayerSpec> OnSave;
            /// <summary>Optional REMOVE button (editing a created teammate).</summary>
            public Action OnRemove;
            public Action OnClose;
            /// <summary>Show the OUTFIT step and save the kit changes with the player (your own player).</summary>
            public bool Outfit;
            /// <summary>Step-by-step with BACK / NEXT (onboarding). The first step's left button says <see cref="SkipLabel"/>.</summary>
            public bool Wizard;
            public string SkipLabel;
        }

        private enum Step { You = 0, Look = 1, Outfit = 2, Game = 3 }

        private readonly GameObject _root;
        private readonly CreatedPlayerSpec _s;
        private readonly Options _o;
        private readonly KitSaveData _kits;
        private readonly List<Step> _steps = new List<Step>();
        private int _step;

        private RawImage _preview;
        private KitPreview _anim;
        private Texture2D _tex;
        private TextMeshProUGUI _nameTag, _styleTag, _viewTag, _moveLabel;
        private RectTransform _tabs, _footer, _body;
        private int _view = -1; // -1 = moving; 0 front, 1 side, 2 back, 3 other side

        private static readonly (CharacterView view, bool flip, string name)[] Views =
        {
            (CharacterView.Front, false, "FRONT"), (CharacterView.Side, false, "SIDE"),
            (CharacterView.Back, false, "BACK"), (CharacterView.Side, true, "SIDE"),
        };

        public static PlayerMaker Open(CreatedPlayerSpec spec, Options o) => new PlayerMaker(spec, o);

        private PlayerMaker(CreatedPlayerSpec spec, Options o)
        {
            _s = spec;
            _o = o;
            // The kits: a copy to edit (your player), or the saved ones to dress a teammate the same way.
            _kits = RiseOutfit.Draft(App.Career, App.Catalog);
            if (!_kits.designed) _kits.wear = 0; // until a kit is designed the crew plays in the HOME colours
            _steps.Add(Step.You);
            _steps.Add(Step.Look);
            if (o.Outfit) _steps.Add(Step.Outfit);
            _steps.Add(Step.Game);

            var canvas = UiKit.CreateScreenCanvas("PlayerMaker", 40);
            _root = canvas.gameObject;
            var scrim = UiKit.Panel(canvas.transform, Theme.Scrim, name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(canvas.transform);
            var panel = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "Panel");
            UiKit.Band(panel.rectTransform, 0.02f, 0.98f, 30f);
            panel.raycastTarget = true;
            var p = panel.rectTransform;

            var title = UiKit.ShadowLabel(p, o.Title ?? "CREATE A PLAYER", 54f, Theme.Cream, Theme.Pink, 6f);
            Band((RectTransform)title.transform.parent, 0.945f, 0.995f);

            BuildStage(p);

            _tabs = UiKit.Row(p, 10f, "Steps");
            Band(_tabs, 0.565f, 0.61f, 36f);

            _footer = UiKit.Row(p, 20f, "Footer");
            _footer.anchorMin = new Vector2(0f, 0f);
            _footer.anchorMax = new Vector2(1f, 0f);
            _footer.pivot = new Vector2(0.5f, 0f);
            _footer.sizeDelta = new Vector2(-72f, 130f);
            _footer.anchoredPosition = new Vector2(0f, 24f);

            var body = UiKit.NewRect("Body", p);
            UiKit.Stretch(body);
            body.anchorMin = new Vector2(0f, 0f);
            body.anchorMax = new Vector2(1f, 0.555f);
            body.offsetMin = new Vector2(0f, 168f);
            body.offsetMax = Vector2.zero;
            _body = UiKit.ScrollColumn(body, 14f, new RectOffset(36, 36, 14, 30));

            ShowStep(0);
        }

        /// <summary>
        /// Stretches across the panel between two heights (fractions), so the stage, steps and list share any
        /// screen (iPhone, iPad, bigger text) instead of running off it.
        /// </summary>
        private static void Band(RectTransform rt, float yMin, float yMax, float side = 30f)
        {
            rt.anchorMin = new Vector2(0f, yMin);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(side, 0f);
            rt.offsetMax = new Vector2(-side, 0f);
        }

        // ------------------------------------------------------------------ the preview stage

        private void BuildStage(RectTransform panel)
        {
            var stage = UiKit.Panel(panel, new Color32(0x12, 0x12, 0x24, 255), Theme.PanelSprite(), true, "Stage");
            Band(stage.rectTransform, 0.62f, 0.94f, 36f);

            // A strip of hardwood and a spotlight so he stands somewhere.
            var floor = UiKit.Panel(stage.transform, new Color32(0x6B, 0x45, 0x2A, 255), null, false, "Floor");
            floor.rectTransform.anchorMin = new Vector2(0f, 0f);
            floor.rectTransform.anchorMax = new Vector2(1f, 0.2f);
            floor.rectTransform.offsetMin = new Vector2(8f, 8f);
            floor.rectTransform.offsetMax = new Vector2(-8f, 0f);
            var line = UiKit.Panel(stage.transform, new Color32(0xF4, 0xF1, 0xDE, 160), null, false, "Line");
            line.rectTransform.anchorMin = new Vector2(0f, 0.2f);
            line.rectTransform.anchorMax = new Vector2(1f, 0.2f);
            line.rectTransform.offsetMin = new Vector2(8f, -3f);
            line.rectTransform.offsetMax = new Vector2(-8f, 3f);
            var spot = UiKit.Panel(stage.transform, new Color32(0xFF, 0xD1, 0x66, 28), null, false, "Spot");
            spot.rectTransform.anchorMin = new Vector2(0.33f, 0.04f);
            spot.rectTransform.anchorMax = new Vector2(0.67f, 0.96f);
            spot.rectTransform.offsetMin = spot.rectTransform.offsetMax = Vector2.zero;

            _preview = UiKit.Picture(stage.transform, null, "Player");
            // As tall as the stage allows (feet on the floor line), the sprite's own shape.
            _preview.rectTransform.anchorMin = new Vector2(0.5f, 0.1f);
            _preview.rectTransform.anchorMax = new Vector2(0.5f, 0.9f);
            _preview.rectTransform.pivot = new Vector2(0.5f, 0f);
            _preview.rectTransform.offsetMin = _preview.rectTransform.offsetMax = Vector2.zero;
            var fit = _preview.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            fit.aspectRatio = (float)CharacterSpriteGenerator.SheetFrameWidth / CharacterSpriteGenerator.SheetFrameHeight;
            _anim = KitPreview.Attach(_preview);

            _nameTag = UiKit.Label(stage.transform, "", 40f, Theme.Cream, TextAlignmentOptions.Left, true, "Name");
            Corner(_nameTag.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -18f), new Vector2(620f, 56f));
            _styleTag = UiKit.Label(stage.transform, "", 26f, Theme.Cyan, TextAlignmentOptions.Left, true, "Style");
            Corner(_styleTag.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -74f), new Vector2(620f, 40f));
            _viewTag = UiKit.Label(stage.transform, "", 24f, Theme.Gold, TextAlignmentOptions.Right, true, "View");
            Corner(_viewTag.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -22f), new Vector2(260f, 40f));

            // Turn him around.
            var left = UiKit.Button(stage.transform, "◄", () => Turn(-1), ButtonStyle.Secondary, 110f, 40f);
            Corner((RectTransform)left.transform, new Vector2(0f, 0.5f), new Vector2(24f, 20f), new Vector2(110f, 110f));
            var right = UiKit.Button(stage.transform, "►", () => Turn(1), ButtonStyle.Secondary, 110f, 40f);
            Corner((RectTransform)right.transform, new Vector2(1f, 0.5f), new Vector2(-24f, 20f), new Vector2(110f, 110f));
            var moves = UiKit.Button(stage.transform, "MOVES", ToggleMoves, ButtonStyle.Ghost, 80f, 26f);
            Corner((RectTransform)moves.transform, new Vector2(1f, 0f), new Vector2(-24f, 22f), new Vector2(210f, 80f));
            _moveLabel = moves.GetComponentInChildren<TextMeshProUGUI>();
        }

        private static void Corner(RectTransform rt, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }

        private void Turn(int dir)
        {
            _view = _view < 0 ? (dir > 0 ? 1 : 3) : (_view + dir + Views.Length) % Views.Length;
            HoldView();
            Haptics.Light();
        }

        private void HoldView()
        {
            var v = Views[_view];
            _anim.Hold(v.view, v.flip);
            _viewTag.text = Loc.T(v.name);
            if (_moveLabel != null) _moveLabel.text = Loc.T("MOVES");
        }

        private void ToggleMoves()
        {
            if (_view >= 0)
            {
                _view = -1;
                _anim.Play();
                _viewTag.text = Loc.T("IN MOTION");
                _moveLabel.text = Loc.T("STAND");
            }
            else
            {
                _view = 0;
                HoldView();
            }
        }

        /// <summary>Turns him to show what just changed (his number on the back, his shoes from the side...).</summary>
        private void Show(CharacterView view, bool flip = false)
        {
            for (int i = 0; i < Views.Length; i++)
                if (Views[i].view == view && Views[i].flip == flip) { _view = i; break; }
            HoldView();
        }

        // ------------------------------------------------------------------ steps

        private void ShowStep(int index)
        {
            _step = Mathf.Clamp(index, 0, _steps.Count - 1);
            BuildTabs();
            BuildFooter();
            RebuildBody();
            Refresh();
            if (_view < 0) { _anim.Play(); _viewTag.text = Loc.T("IN MOTION"); _moveLabel.text = Loc.T("STAND"); }
        }

        private static string StepName(Step s) => s == Step.You ? "YOU" : s == Step.Look ? "LOOK" : s == Step.Outfit ? "OUTFIT" : "GAME";

        private void BuildTabs()
        {
            Clear(_tabs);
            for (int i = 0; i < _steps.Count; i++)
            {
                int index = i;
                string label = (_o.Wizard ? (i + 1) + " " : "") + StepName(_steps[i]);
                UiKit.Button(_tabs, label, () => ShowStep(index), i == _step ? ButtonStyle.Primary : ButtonStyle.Secondary, 90f, 28f);
            }
        }

        private void BuildFooter()
        {
            Clear(_footer);
            bool last = _step == _steps.Count - 1;
            string save = string.IsNullOrEmpty(_o.SaveLabel) ? "SAVE" : _o.SaveLabel;
            if (_o.Wizard)
            {
                if (_step == 0) UiKit.Button(_footer, string.IsNullOrEmpty(_o.SkipLabel) ? "SKIP" : _o.SkipLabel, Close, ButtonStyle.Ghost, 120f);
                else UiKit.Button(_footer, "BACK", () => ShowStep(_step - 1), ButtonStyle.Ghost, 120f);
                if (last) UiKit.Button(_footer, save, Save, ButtonStyle.Primary, 120f);
                else UiKit.Button(_footer, "NEXT", () => ShowStep(_step + 1), ButtonStyle.Primary, 120f);
                return;
            }
            UiKit.Button(_footer, "CANCEL", Close, ButtonStyle.Ghost, 120f);
            UiKit.Button(_footer, save, Save, ButtonStyle.Primary, 120f);
        }

        private void RebuildBody()
        {
            Clear(_body);
            switch (_steps[_step])
            {
                case Step.You: BuildYou(); break;
                case Step.Look: BuildLook(); break;
                case Step.Outfit: BuildOutfit(); break;
                default: BuildGame(); break;
            }
        }

        private static void Clear(RectTransform rt)
        {
            for (int i = rt.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(rt.GetChild(i).gameObject);
        }

        private void Header(string text) => UiKit.Size(UiKit.Label(_body, text, 32f, Theme.Cyan, TextAlignmentOptions.Left, true), 50f);

        private void Note(string text, float height = 60f) =>
            UiKit.Size(UiKit.Label(_body, text, 24f, Theme.Muted, TextAlignmentOptions.Left, false), height).GetComponent<TextMeshProUGUI>();

        // ------------------------------------------------------------------ YOU

        private void BuildYou()
        {
            var s = _s;
            Header("NAME");
            if (_o.Mine)
            {
                var f = UiControls.TextField(_body, s.First, Franchise.MaxNameLength, v => { s.First = v; Refresh(); }, "Nickname");
                f.onValueChanged.AddListener(v => { s.First = v; RefreshTags(); });
            }
            else
            {
                var f1 = UiControls.TextField(_body, s.First, Franchise.MaxNameLength, v => { s.First = v; Refresh(); }, "First name");
                f1.onValueChanged.AddListener(v => { s.First = v; RefreshTags(); });
                var f2 = UiControls.TextField(_body, s.Last, Franchise.MaxNameLength, v => { s.Last = v; Refresh(); }, "Last name");
                f2.onValueChanged.AddListener(v => { s.Last = v; RefreshTags(); });
            }
            Header("NUMBER");
            NumberStepper();
            Note("It's printed on the back of your jersey. Tap ◄ ► on the preview to turn him around.");
        }

        private void NumberStepper()
        {
            var row = UiKit.Row(_body, 12f, "Stepper NUMBER");
            UiKit.Size(row, 110f);
            TextMeshProUGUI value = null;
            void StepBy(int delta)
            {
                _s.Number = (_s.Number + delta + 100) % 100;
                value.text = "#" + _s.Number;
                Refresh();
                Show(CharacterView.Back);
            }
            UiKit.Button(row, "-10", () => StepBy(-10), ButtonStyle.Ghost, 100f, 32f);
            UiKit.Button(row, "-1", () => StepBy(-1), ButtonStyle.Ghost, 100f, 32f);
            value = UiKit.Label(row, "#" + _s.Number, 56f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Size(value, -1f, 200f);
            UiKit.Button(row, "+1", () => StepBy(1), ButtonStyle.Ghost, 100f, 32f);
            UiKit.Button(row, "+10", () => StepBy(10), ButtonStyle.Ghost, 100f, 32f);
        }

        // ------------------------------------------------------------------ LOOK

        private void BuildLook()
        {
            var s = _s;
            Header("SKIN TONE");
            Swatches(CharacterSpriteGenerator.SkinTones, Wrap(s.Skin, PlayerCreator.SkinToneCount), i => { s.Skin = i; LookChanged(); });
            Header("HAIR");
            Stepper("STYLE", () => Loc.T("STYLE") + " " + (Wrap(s.Hair, PlayerCreator.HairStyleCount) + 1) + " / " + PlayerCreator.HairStyleCount,
                    d => { s.Hair = Wrap(s.Hair + d, PlayerCreator.HairStyleCount); LookChanged(); });
            Swatches(CharacterSpriteGenerator.HairColors, Wrap(s.HairColor, PlayerCreator.HairColorCount), i => { s.HairColor = i; LookChanged(); });
            Note(Loc.T("HAIR COLOUR") + ": " + Loc.T(HairColorNames[Wrap(s.HairColor, HairColorNames.Length)]), 40f);
            Header("BUILD");
            Segments(BodyNames, Wrap(s.Body, PlayerCreator.BodyCount), i => { s.Body = i; LookChanged(); });
            Header("HEIGHT");
            Segments(HeightNames, Wrap(s.Height, PlayerCreator.HeightCount), i => { s.Height = i; LookChanged(); });
            UiKit.Button(_body, "RANDOM LOOK", () =>
            {
                var r = new System.Random();
                s.Skin = r.Next(PlayerCreator.SkinToneCount);
                s.Hair = r.Next(PlayerCreator.HairStyleCount);
                s.HairColor = r.Next(PlayerCreator.HairColorCount);
                s.Body = r.Next(PlayerCreator.BodyCount);
                s.Height = r.Next(PlayerCreator.HeightCount);
                LookChanged();
            }, ButtonStyle.Secondary, 100f, 32f);
        }

        private void LookChanged()
        {
            Haptics.Light();
            RebuildBody();
            Refresh();
            if (_view < 0) Show(CharacterView.Front);
        }

        // ------------------------------------------------------------------ OUTFIT

        private KitData Kit => RiseOutfit.Worn(_kits);

        private void BuildOutfit()
        {
            var d = App.Career;
            var k = Kit;
            if (k == null) { Note("Your kit isn't ready yet."); return; }
            Note("What your crew wears in Rise (and every game with your team). The full Kit Studio is in the Locker Room ► KIT.", 90f);

            Header("KIT");
            Segments(Kits.SlotNames, _kits.wear, i => { _kits.wear = i; OutfitChanged(CharacterView.Front); });

            Header("JERSEY");
            StyleStepper(Kits.PartCut, Kits.CutNames, () => Kit.cut, v => Kit.cut = v, CharacterView.Front);
            StyleStepper(Kits.PartCollar, Kits.CollarNames, () => Kit.collar, v => Kit.collar = v, CharacterView.Front);
            Swatches(RiseOutfit.Swatches, Pick(Kit.jersey), i => { Kit.jersey = Kits.Pack(RiseOutfit.Swatches[i]); OutfitChanged(CharacterView.Front); });
            Note(Loc.T("TRIM"), 36f);
            Swatches(RiseOutfit.Swatches, Pick(Kit.trim), i =>
            {
                Kit.trim = Kits.Pack(RiseOutfit.Swatches[i]);
                Kit.shortsTrim = Kit.trim;
                OutfitChanged(CharacterView.Front);
            });

            Header("SHORTS");
            StyleStepper(Kits.PartLength, Kits.LengthNames, () => Kit.length, v => Kit.length = v, CharacterView.Side);
            Swatches(RiseOutfit.Swatches, Pick(Kit.shorts), i => { Kit.shorts = Kits.Pack(RiseOutfit.Swatches[i]); OutfitChanged(CharacterView.Front); });

            Header("SHOES");
            StyleStepper(Kits.PartTop, Kits.TopNames, () => Kit.top, v => Kit.top = v, CharacterView.Side);
            Swatches(RiseOutfit.Swatches, Pick(Kit.shoe), i =>
            {
                Kit.shoe = Kits.Pack(RiseOutfit.Swatches[i]);
                Kit.laces = Kit.shoe;
                OutfitChanged(CharacterView.Side);
            });

            var tools = UiKit.Row(_body, 14f, "Outfit tools");
            UiKit.Size(tools, 100f);
            UiKit.Button(tools, "RANDOM OUTFIT", () =>
            {
                string name = Kit.name;
                _kits.slots[Mod(_kits.wear, _kits.slots.Count)] = Kits.Randomize((uint)Environment.TickCount | 1u, d, name);
                OutfitChanged(CharacterView.Front);
            }, ButtonStyle.Secondary, 100f, 28f);
            UiKit.Button(tools, "CREW COLOURS", () =>
            {
                RiseOutfit.ResetSlot(_kits, Mod(_kits.wear, _kits.slots.Count), d, App.Catalog);
                OutfitChanged(CharacterView.Front);
            }, ButtonStyle.Ghost, 100f, 28f);

            // What's still locked, so there's something to play for.
            string hint = LockedHint(d);
            if (hint != null) Note(hint, 70f);
        }

        private static int Pick(int packed)
        {
            for (int i = 0; i < RiseOutfit.Swatches.Length; i++)
                if (Kits.Pack(RiseOutfit.Swatches[i]) == (packed & 0x7FFF)) return i;
            return -1;
        }

        private static string LockedHint(CareerSaveData d)
        {
            foreach (var u in Kits.Unlocks)
            {
                if (u.Part != Kits.PartCut && u.Part != Kits.PartTop && u.Part != Kits.PartLength) continue;
                if (Kits.IsUnlocked(u.Part, u.Value, d)) continue;
                string[] names = u.Part == Kits.PartCut ? Kits.CutNames : u.Part == Kits.PartTop ? Kits.TopNames : Kits.LengthNames;
                return Loc.T("LOCKED") + ": " + Loc.T(names[u.Value]) + "  ·  " + Loc.T(u.Hint);
            }
            return null;
        }

        private void StyleStepper(string part, string[] names, Func<int> get, Action<int> set, CharacterView view)
        {
            var d = App.Career;
            bool more = RiseOutfit.UnlockedCount(part, names.Length, d) > 1;
            Stepper(part, () => Loc.T(PartName(part)) + ":  " + Loc.T(names[Mod(get(), names.Length)]), dir =>
            {
                if (!more) return;
                set(RiseOutfit.NextStyle(part, get(), names.Length, d, dir));
                OutfitChanged(view);
            });
        }

        private static string PartName(string part) =>
            part == Kits.PartCut ? "CUT" : part == Kits.PartCollar ? "COLLAR" : part == Kits.PartLength ? "LENGTH" : part == Kits.PartTop ? "TOP" : part.ToUpperInvariant();

        private void OutfitChanged(CharacterView view)
        {
            Haptics.Light();
            RebuildBody();
            Refresh();
            Show(view);
        }

        // ------------------------------------------------------------------ GAME

        private void BuildGame()
        {
            var c = App.Catalog;
            var s = _s;
            Header("STYLE OF PLAY");
            var styles = c.Archetypes;
            int ai = Mathf.Max(0, styles.FindIndex(a => (int)a.archetype == s.Archetype));
            s.Archetype = (int)styles[ai].archetype;
            for (int i = 0; i < styles.Count; i++)
            {
                var a = styles[i];
                bool on = i == ai;
                var card = UiKit.Panel(_body, on ? new Color32(0x3A, 0x1D, 0x4A, 255) : new Color32(0x1A, 0x1A, 0x2E, 255), Theme.PanelSprite(), true, "Style " + a.id);
                UiKit.Size(card, 150f);
                card.raycastTarget = true;
                var b = card.gameObject.AddComponent<Button>();
                b.targetGraphic = card;
                int index = i;
                b.onClick.AddListener(() =>
                {
                    Audio.AudioManager.Click();
                    Haptics.Light();
                    s.Archetype = (int)styles[index].archetype;
                    RebuildBody();
                    Refresh();
                });
                var name = UiKit.Label(card.transform, (on ? "► " : "") + a.displayName.ToUpperInvariant(), 32f, on ? Theme.Gold : Theme.Cream, TextAlignmentOptions.Left, true);
                name.rectTransform.anchorMin = new Vector2(0f, 0.55f);
                name.rectTransform.anchorMax = new Vector2(1f, 0.95f);
                name.rectTransform.offsetMin = new Vector2(28f, 0f);
                name.rectTransform.offsetMax = new Vector2(-28f, 0f);
                var desc = UiKit.Label(card.transform, a.description, 22f, Theme.Muted, TextAlignmentOptions.Left, false);
                desc.rectTransform.anchorMin = new Vector2(0f, 0.05f);
                desc.rectTransform.anchorMax = new Vector2(1f, 0.55f);
                desc.rectTransform.offsetMin = new Vector2(28f, 0f);
                desc.rectTransform.offsetMax = new Vector2(-28f, 0f);
                desc.textWrappingMode = TextWrappingModes.Normal;
                desc.enableAutoSizing = true;
                desc.fontSizeMax = 22f;
                desc.fontSizeMin = 14f;
            }
            string extra = _o.Info != null ? _o.Info(s) : "";
            if (!string.IsNullOrEmpty(extra))
                UiKit.Size(UiKit.Label(_body, extra, 26f, Theme.Cyan, TextAlignmentOptions.Center, true), 70f);

            if (_o.OnRemove != null)
                UiKit.Button(_body, "REMOVE", () =>
                    UiControls.Dialog("REMOVE PLAYER?", "He leaves your crew for good. An original First Caller takes his spot.",
                        ("REMOVE", ButtonStyle.Primary, () => { var r = _o.OnRemove; Destroy(); r(); }),
                        ("CANCEL", ButtonStyle.Ghost, null)),
                    ButtonStyle.Ghost, 100f, 36f);
        }

        // ------------------------------------------------------------------ controls

        /// <summary>A row of colour squares; the picked one has a gold frame.</summary>
        private void Swatches(RgbColor[] colors, int selected, Action<int> pick)
        {
            const int perRow = 6;
            for (int start = 0; start < colors.Length; start += perRow)
            {
                var row = UiKit.Row(_body, 16f, "Swatches");
                UiKit.Size(row, 96f);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                for (int i = start; i < Math.Min(colors.Length, start + perRow); i++)
                {
                    var col = colors[i];
                    bool on = i == selected;
                    var frame = UiKit.Panel(row, on ? Theme.Gold : new Color32(0x2A, 0x2A, 0x44, 255), null, false, "Swatch " + i);
                    UiKit.Size(frame, 96f, 120f);
                    frame.raycastTarget = true;
                    var chip = UiKit.Panel(frame.transform, new Color32(col.r, col.g, col.b, 255), null, false, "Colour");
                    UiKit.Stretch(chip.rectTransform, on ? 10f : 6f);
                    var b = frame.gameObject.AddComponent<Button>();
                    b.targetGraphic = frame;
                    int index = i;
                    b.onClick.AddListener(() => { Audio.AudioManager.Click(); pick(index); });
                }
            }
        }

        /// <summary>Buttons side by side; the picked one is lit.</summary>
        private void Segments(string[] names, int selected, Action<int> pick)
        {
            var row = UiKit.Row(_body, 12f, "Segments");
            UiKit.Size(row, 96f);
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                UiKit.Button(row, names[i], () => { if (index != selected) pick(index); }, i == selected ? ButtonStyle.Primary : ButtonStyle.Secondary, 96f, 28f);
            }
        }

        /// <summary>◄ VALUE ► row.</summary>
        private void Stepper(string name, Func<string> text, Action<int> step)
        {
            var row = UiKit.Row(_body, 12f, "Stepper " + name);
            UiKit.Size(row, 100f);
            var l = UiKit.Button(row, "◄", () => step(-1), ButtonStyle.Ghost, 100f, 34f);
            UiKit.Size(l, 100f, 120f);
            var value = UiKit.Label(row, text(), 30f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(value, -1f, 560f);
            var r = UiKit.Button(row, "►", () => step(1), ButtonStyle.Ghost, 100f, 34f);
            UiKit.Size(r, 100f, 120f);
        }

        // ------------------------------------------------------------------ preview

        /// <summary>Redraws the player in his outfit and the tags.</summary>
        private void Refresh()
        {
            var s = _s;
            var look = new AppearanceDef(Wrap(s.Skin, PlayerCreator.SkinToneCount), Wrap(s.Hair, PlayerCreator.HairStyleCount),
                                         Wrap(s.HairColor, PlayerCreator.HairColorCount), (BodyType)Wrap(s.Body, PlayerCreator.BodyCount),
                                         Wrap(s.Height, PlayerCreator.HeightCount));
            var kit = Kit;
            var dress = kit != null ? Kits.Look(kit) : KitLook.Classic(new RgbColor(0xF7, 0x25, 0x85), new RgbColor(0x4C, 0xC9, 0xF0), new RgbColor(0x1A, 0x1A, 0x2E), null, TeamPattern.Solid, null);
            var sheet = CharacterSpriteGenerator.GenerateSheet(look, dress, Mathf.Clamp(s.Number, 0, 99));
            var old = _tex;
            _tex = TextureFactory.ToTexture(sheet, "ui.playermaker.sheet");
            _anim.SetSheet(_tex);
            if (old != null) UnityEngine.Object.Destroy(old);
            RefreshTags();
        }

        private void RefreshTags()
        {
            var c = App.Catalog;
            var s = _s;
            string name = ((s.First ?? "") + (_o.Mine ? "" : " " + (s.Last ?? ""))).Trim();
            if (name.Length == 0) name = _o.Mine ? "Rook" : "Rookie";
            _nameTag.text = name.ToUpperInvariant() + "  <color=#FFD166>#" + s.Number + "</color>";
            var arch = c.Archetypes.Find(a => (int)a.archetype == s.Archetype) ?? c.Archetypes[0];
            string kitName = Kit != null ? "  ·  " + Loc.T(Kits.SlotNames[Mod(_kits.wear, Kits.SlotCount)]) + " " + Loc.T("KIT") : "";
            _styleTag.text = arch.displayName.ToUpperInvariant() + kitName;
        }

        private void Save()
        {
            var save = _o.OnSave;
            var spec = _s;
            if (_o.Outfit && App.Career != null) RiseOutfit.Apply(App.Career, _kits);
            Destroy();
            Haptics.Success();
            save?.Invoke(spec);
        }

        private void Close()
        {
            var close = _o.OnClose;
            Destroy();
            close?.Invoke();
        }

        private void Destroy()
        {
            if (_tex != null) UnityEngine.Object.Destroy(_tex);
            _tex = null;
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }

        private static int Wrap(int v, int n) => n <= 0 ? 0 : ((v % n) + n) % n;
        private static int Mod(int v, int n) => Wrap(v, n);
    }
}
