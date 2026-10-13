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
    /// Franchise ► ROSTER ► CREATE A PLAYER (10 Oct 2026): name, number, style of play, look and starting level, with a
    /// live preview in your club's colours. Every rule lives in <see cref="Franchise"/> (FranchiseCreate.cs).
    /// Changing a choice updates the preview in place, so the list doesn't jump back to the top.
    /// </summary>
    public sealed partial class FranchiseScreen
    {
        private CreatedPlayerSpec _create;
        private int _editId = -1;
        private RawImage _preview;
        private Texture2D _previewTex;
        private TextMeshProUGUI _previewNumber, _createInfo;

        private static readonly string[] HairColorNames = { "BLACK", "DARK BROWN", "BROWN", "BLONDE", "RED" };
        private static readonly string[] BodyNames = { "SLIM", "STANDARD", "BROAD" };
        private static readonly string[] HeightNames = { "SHORT", "AVERAGE", "TALL" };

        private void OnDestroy()
        {
            if (_previewTex != null) Destroy(_previewTex);
        }

        /// <summary>ROSTER: the CREATE A PLAYER button (or why you can't right now).</summary>
        private void CreateEntry(FranchiseSaveData f)
        {
            string why = Franchise.CannotCreate(f);
            Big("CREATE A PLAYER", () =>
            {
                if (why != null) { Toast("CREATE A PLAYER", why); return; }
                OpenCreate(null);
            }, ButtonStyle.Secondary);
            Line(why ?? "Build your own player and sign him to your club (up to three).",
                 Theme.Muted, 24f, 60f, TextAlignmentOptions.Center);
        }

        /// <summary>ROSTER: your created players, with EDIT.</summary>
        private void CreatedList(FranchiseSaveData f)
        {
            var mine = Franchise.Roster(f, f.you).FindAll(p => p.created);
            if (mine.Count == 0) return;
            Heading("YOUR CREATED PLAYERS");
            foreach (var p in mine)
            {
                var player = p;
                Row(Card(p), Theme.Cream, 110f, ("EDIT", ButtonStyle.Secondary, () => OpenCreate(player)));
            }
        }

        private void OpenCreate(FrPlayer editing)
        {
            _editId = editing != null ? editing.id : -1;
            _create = editing != null ? Franchise.SpecOf(editing) : NewSpec();
            Refresh();
        }

        private CreatedPlayerSpec NewSpec()
        {
            var s = Franchise.SpecFromCareer(App.Career, C);
            s.First = "";
            s.Last = "";
            return s;
        }

        private void CloseCreate()
        {
            _create = null;
            _editId = -1;
            _tab = Tab.Roster;
            Refresh();
        }

        private void BuildCreate()
        {
            var f = F;
            var s = _create;
            bool editing = _editId >= 0;

            // Live preview in your club's colours.
            var stage = UiKit.Panel(_content, Color.white, Theme.PanelSprite(), true, "Preview");
            UiKit.Size(stage, 320f);
            _preview = UiKit.Picture(stage.transform, null, "Player");
            _previewNumber = UiKit.Label(stage.transform, "", 48f, Theme.Gold, TextAlignmentOptions.Right, true);
            UiKit.Place(_previewNumber.rectTransform, new Vector2(0.85f, 0.75f), new Vector2(200f, 70f));
            UpdatePreview();

            Heading("NAME");
            UiControls.TextField(_content, s.First, Franchise.MaxNameLength, v => { s.First = v; UpdatePreview(); }, "First name");
            UiControls.TextField(_content, s.Last, Franchise.MaxNameLength, v => { s.Last = v; UpdatePreview(); }, "Last name");
            NumberStepper(s);

            Heading("LOOK");
            UiControls.ChoiceRow(_content, "SKIN TONE", Numbered(PlayerCreator.SkinToneCount), s.Skin, i => { s.Skin = i; UpdatePreview(); });
            UiControls.ChoiceRow(_content, "HAIR", Numbered(PlayerCreator.HairStyleCount), s.Hair, i => { s.Hair = i; UpdatePreview(); });
            UiControls.ChoiceRow(_content, "HAIR COLOUR", HairColorNames, s.HairColor, i => { s.HairColor = i; UpdatePreview(); });
            UiControls.ChoiceRow(_content, "BUILD", BodyNames, s.Body, i => { s.Body = i; UpdatePreview(); });
            UiControls.ChoiceRow(_content, "HEIGHT", HeightNames, s.Height, i => { s.Height = i; UpdatePreview(); });

            if (!editing)
            {
                Heading("GAME");
                var styles = C.Archetypes;
                int ai = Mathf.Max(0, styles.FindIndex(a => (int)a.archetype == s.Archetype));
                UiControls.ChoiceRow(_content, "STYLE OF PLAY", styles.ConvertAll(a => a.displayName.ToUpperInvariant()).ToArray(), ai,
                    i => { s.Archetype = (int)styles[i].archetype; UpdatePreview(); });
                var levels = System.Array.ConvertAll(Franchise.CreateLevels, l => l.Name);
                UiControls.ChoiceRow(_content, "STARTING LEVEL", levels, s.Level, i => { s.Level = i; UpdatePreview(); });
                _createInfo = Line("", Theme.Cyan, 26f, 120f, TextAlignmentOptions.Center, true);
                UpdatePreview();
                Big("USE MY LOCKER ROOM PLAYER", () =>
                {
                    int level = s.Level;
                    _create = Franchise.SpecFromCareer(App.Career, C);
                    _create.Level = level;
                    Refresh();
                }, ButtonStyle.Ghost);
            }
            else
            {
                Line("Name, number and look can change. Ratings grow with games, age and training like everyone else's.", Theme.Muted, 24f, 70f, TextAlignmentOptions.Center);
            }

            Big(editing ? "SAVE CHANGES" : "SIGN HIM", () =>
            {
                if (editing)
                {
                    Franchise.EditCreated(f, _editId, s);
                    _create = null;
                    _editId = -1;
                    _tab = Tab.Roster;
                    Save();
                    return;
                }
                string why = Franchise.CannotCreate(f, s.Level);
                if (why != null) { Toast("CAN'T SIGN HIM", why); return; }
                var p = Franchise.CreatePlayer(f, C, s);
                _create = null;
                _tab = Tab.Roster;
                Haptics.Success();
                Save();
                Toast("WELCOME, " + p.Name.ToUpperInvariant(),
                      "He's at the end of your bench. Move him UP to #1 on the ROSTER to control him in games.");
            });
            Big("CANCEL", CloseCreate, ButtonStyle.Ghost);
        }

        private void NumberStepper(CreatedPlayerSpec s)
        {
            var row = UiKit.Row(_content, 12f, "Stepper NUMBER");
            UiKit.Size(row, 100f);
            var text = UiKit.Label(row, "NUMBER", 36f, Theme.Cream, TextAlignmentOptions.Left, true);
            UiKit.Size(text, -1f, 260f);
            TextMeshProUGUI value = null;
            void Step(int delta)
            {
                s.Number = (s.Number + delta + 100) % 100;
                value.text = "#" + s.Number;
                UpdatePreview();
            }
            UiKit.Button(row, "-10", () => Step(-10), ButtonStyle.Ghost, 90f, 32f);
            UiKit.Button(row, "-1", () => Step(-1), ButtonStyle.Ghost, 90f, 32f);
            value = UiKit.Label(row, "#" + s.Number, 40f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Size(value, -1f, 140f);
            UiKit.Button(row, "+1", () => Step(1), ButtonStyle.Ghost, 90f, 32f);
            UiKit.Button(row, "+10", () => Step(10), ButtonStyle.Ghost, 90f, 32f);
        }

        /// <summary>Redraws the preview sprite, the number and the ratings/cost line from the current choices.</summary>
        private void UpdatePreview()
        {
            var s = _create;
            var f = F;
            if (s == null || _preview == null) return;
            var team = Team(f.you);
            var look = new AppearanceDef(Wrap(s.Skin, PlayerCreator.SkinToneCount), Wrap(s.Hair, PlayerCreator.HairStyleCount),
                                         Wrap(s.HairColor, PlayerCreator.HairColorCount), (BodyType)Wrap(s.Body, PlayerCreator.BodyCount),
                                         Wrap(s.Height, PlayerCreator.HeightCount));
            var sheet = team != null
                ? CharacterSpriteGenerator.GenerateSheet(look, team.primary, team.secondary, team.accent, null, team.pattern)
                : CharacterSpriteGenerator.GenerateSheet(look, new RgbColor(0x2B, 0x2D, 0x42), new RgbColor(0xFF, 0xD1, 0x66), new RgbColor(0xEF, 0x47, 0x6F));
            CharacterSpriteGenerator.FrameOrigin(CharacterView.Front, 0, out int fx, out int fy);
            var frame = new PixelCanvas(CharacterSpriteGenerator.SheetFrameWidth, CharacterSpriteGenerator.SheetFrameHeight);
            for (int y = 0; y < frame.Height; y++)
                for (int x = 0; x < frame.Width; x++)
                    frame.Pixels[y * frame.Width + x] = sheet.Get(fx + x, fy + y);
            if (_previewTex != null) Destroy(_previewTex);
            _previewTex = TextureFactory.ToTexture(frame, "ui.franchise.create.preview");
            _preview.texture = _previewTex;
            UiKit.Place(_preview.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(frame.Width * 11f / CharacterSpriteGenerator.ArtScale, frame.Height * 11f / CharacterSpriteGenerator.ArtScale));
            _previewNumber.text = "#" + s.Number;

            if (_createInfo != null)
            {
                var level = Franchise.CreateLevels[Mathf.Clamp(s.Level, 0, Franchise.CreateLevels.Length - 1)];
                var ratings = Franchise.CreatedRatings(C, s.Archetype, s.Level);
                string why = Franchise.CannotCreate(f, s.Level);
                _createInfo.text = Loc.T("OVR") + " " + ratings.Overall + "  ·  " + Loc.T("AGE") + " " + level.Age + "  ·  " + Franchise.Money(Franchise.CreateSalary(s.Level)) + " × 3"
                                   + (level.Minimum ? "  ·  " + Loc.T("minimum deal") : "")
                                   + (why != null ? "\n<color=#EF476F>" + Loc.T(why) + "</color>" : "");
            }
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
