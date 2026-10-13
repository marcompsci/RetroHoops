using System;
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
    /// A full-screen CREATE A PLAYER overlay (10 Oct 2026, Rise Mode): name, number, look and style of play with a live
    /// preview in your crew's colours. The caller decides what SAVE does (your Rise player or a created teammate);
    /// the rules live in <see cref="RiseCreate"/>.
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
        }

        private readonly GameObject _root;
        private readonly CreatedPlayerSpec _s;
        private readonly Options _o;
        private RawImage _preview;
        private Texture2D _tex;
        private TextMeshProUGUI _number, _info;

        public static PlayerMaker Open(CreatedPlayerSpec spec, Options o) => new PlayerMaker(spec, o);

        private PlayerMaker(CreatedPlayerSpec spec, Options o)
        {
            _s = spec;
            _o = o;
            var canvas = UiKit.CreateScreenCanvas("PlayerMaker", 40);
            _root = canvas.gameObject;
            var scrim = UiKit.Panel(canvas.transform, Theme.Scrim, name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(canvas.transform);
            var panel = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "Panel");
            UiKit.Band(panel.rectTransform, 0.03f, 0.97f, 40f);
            panel.raycastTarget = true;

            var footer = UiKit.Row(panel.transform, 20f, "Footer");
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(-80f, 130f);
            footer.anchoredPosition = new Vector2(0f, 24f);
            UiKit.Button(footer, "CANCEL", Close, ButtonStyle.Ghost, 120f);
            UiKit.Button(footer, string.IsNullOrEmpty(o.SaveLabel) ? "SAVE" : o.SaveLabel, Save, ButtonStyle.Primary, 120f);

            var body = UiKit.NewRect("Body", panel.transform);
            UiKit.Stretch(body);
            body.offsetMin = new Vector2(0f, 170f);
            var col = UiKit.ScrollColumn(body, 14f, new RectOffset(36, 36, 30, 20));
            Build(col);
        }

        private void Build(RectTransform col)
        {
            var c = App.Catalog;
            var s = _s;
            UiKit.Size(UiKit.ShadowLabel(col, _o.Title ?? "CREATE A PLAYER", 56f, Theme.Cream, Theme.Pink, 6f).transform.parent.GetComponent<RectTransform>(), 86f);

            var stage = UiKit.Panel(col, Color.white, Theme.PanelSprite(), true, "Preview");
            UiKit.Size(stage, 320f);
            _preview = UiKit.Picture(stage.transform, null, "Player");
            _number = UiKit.Label(stage.transform, "", 48f, Theme.Gold, TextAlignmentOptions.Right, true);
            UiKit.Place(_number.rectTransform, new Vector2(0.85f, 0.75f), new Vector2(200f, 70f));

            UiKit.Size(UiKit.Label(col, "NAME", 36f, Theme.Cyan, TextAlignmentOptions.Left, true), 56f);
            if (_o.Mine)
            {
                UiControls.TextField(col, s.First, Franchise.MaxNameLength, v => s.First = v, "Nickname");
            }
            else
            {
                UiControls.TextField(col, s.First, Franchise.MaxNameLength, v => s.First = v, "First name");
                UiControls.TextField(col, s.Last, Franchise.MaxNameLength, v => s.Last = v, "Last name");
            }
            NumberStepper(col);

            UiKit.Size(UiKit.Label(col, "LOOK", 36f, Theme.Cyan, TextAlignmentOptions.Left, true), 56f);
            UiControls.ChoiceRow(col, "SKIN TONE", Numbered(PlayerCreator.SkinToneCount), s.Skin, i => { s.Skin = i; Update(); });
            UiControls.ChoiceRow(col, "HAIR", Numbered(PlayerCreator.HairStyleCount), s.Hair, i => { s.Hair = i; Update(); });
            UiControls.ChoiceRow(col, "HAIR COLOUR", HairColorNames, s.HairColor, i => { s.HairColor = i; Update(); });
            UiControls.ChoiceRow(col, "BUILD", BodyNames, s.Body, i => { s.Body = i; Update(); });
            UiControls.ChoiceRow(col, "HEIGHT", HeightNames, s.Height, i => { s.Height = i; Update(); });

            UiKit.Size(UiKit.Label(col, "GAME", 36f, Theme.Cyan, TextAlignmentOptions.Left, true), 56f);
            var styles = c.Archetypes;
            int ai = Mathf.Max(0, styles.FindIndex(a => (int)a.archetype == s.Archetype));
            s.Archetype = (int)styles[ai].archetype;
            UiControls.ChoiceRow(col, "STYLE OF PLAY", styles.ConvertAll(a => a.displayName.ToUpperInvariant()).ToArray(), ai,
                i => { s.Archetype = (int)styles[i].archetype; Update(); });
            _info = UiKit.Label(col, "", 28f, Theme.Muted, TextAlignmentOptions.Center, false);
            UiKit.Size(_info, 150f);

            if (_o.OnRemove != null)
                UiKit.Button(col, "REMOVE", () =>
                    UiControls.Dialog("REMOVE PLAYER?", "He leaves your crew for good. An original First Caller takes his spot.",
                        ("REMOVE", ButtonStyle.Primary, () => { var r = _o.OnRemove; Destroy(); r(); }),
                        ("CANCEL", ButtonStyle.Ghost, null)),
                    ButtonStyle.Ghost, 100f, 36f);
            Update();
        }

        private void NumberStepper(RectTransform col)
        {
            var row = UiKit.Row(col, 12f, "Stepper NUMBER");
            UiKit.Size(row, 100f);
            var text = UiKit.Label(row, "NUMBER", 36f, Theme.Cream, TextAlignmentOptions.Left, true);
            UiKit.Size(text, -1f, 220f);
            TextMeshProUGUI value = null;
            void Step(int delta)
            {
                _s.Number = (_s.Number + delta + 100) % 100;
                value.text = "#" + _s.Number;
                Update();
            }
            UiKit.Button(row, "-10", () => Step(-10), ButtonStyle.Ghost, 90f, 32f);
            UiKit.Button(row, "-1", () => Step(-1), ButtonStyle.Ghost, 90f, 32f);
            value = UiKit.Label(row, "#" + _s.Number, 40f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Size(value, -1f, 130f);
            UiKit.Button(row, "+1", () => Step(1), ButtonStyle.Ghost, 90f, 32f);
            UiKit.Button(row, "+10", () => Step(10), ButtonStyle.Ghost, 90f, 32f);
        }

        /// <summary>Redraws the preview, the number and the info line.</summary>
        private void Update()
        {
            var c = App.Catalog;
            var s = _s;
            var crew = c.Team(DefaultContent.PlayerCrewId);
            var look = new AppearanceDef(Wrap(s.Skin, PlayerCreator.SkinToneCount), Wrap(s.Hair, PlayerCreator.HairStyleCount),
                                         Wrap(s.HairColor, PlayerCreator.HairColorCount), (BodyType)Wrap(s.Body, PlayerCreator.BodyCount),
                                         Wrap(s.Height, PlayerCreator.HeightCount));
            var sheet = crew != null
                ? CharacterSpriteGenerator.GenerateSheet(look, crew.primary, crew.secondary, crew.accent, null, crew.pattern)
                : CharacterSpriteGenerator.GenerateSheet(look, new RgbColor(0xF7, 0x25, 0x85), new RgbColor(0x4C, 0xC9, 0xF0), new RgbColor(0x1A, 0x1A, 0x2E));
            CharacterSpriteGenerator.FrameOrigin(CharacterView.Front, 0, out int fx, out int fy);
            var frame = new PixelCanvas(CharacterSpriteGenerator.SheetFrameWidth, CharacterSpriteGenerator.SheetFrameHeight);
            for (int y = 0; y < frame.Height; y++)
                for (int x = 0; x < frame.Width; x++)
                    frame.Pixels[y * frame.Width + x] = sheet.Get(fx + x, fy + y);
            if (_tex != null) UnityEngine.Object.Destroy(_tex);
            _tex = TextureFactory.ToTexture(frame, "ui.playermaker.preview");
            _preview.texture = _tex;
            UiKit.Place(_preview.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(frame.Width * 11f / CharacterSpriteGenerator.ArtScale, frame.Height * 11f / CharacterSpriteGenerator.ArtScale));
            _number.text = "#" + s.Number;
            if (_info != null)
            {
                var arch = c.Archetypes.Find(a => (int)a.archetype == s.Archetype) ?? c.Archetypes[0];
                string extra = _o.Info != null ? _o.Info(s) : "";
                _info.text = Loc.T(arch.description) + (string.IsNullOrEmpty(extra) ? "" : "\n<color=#4CC9F0>" + extra + "</color>");
            }
        }

        private void Save()
        {
            var save = _o.OnSave;
            var spec = _s;
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

        private static string[] Numbered(int count)
        {
            var a = new string[count];
            for (int i = 0; i < count; i++) a[i] = (i + 1).ToString();
            return a;
        }

        private static int Wrap(int v, int n) => n <= 0 ? 0 : ((v % n) + n) % n;
    }
}
