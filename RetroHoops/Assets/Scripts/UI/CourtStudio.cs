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
    /// Locker Room ► COURT: the Court Builder. Three court slots; floor style and colour, lines, paint,
    /// what's behind the baseline, sky, crowd and a centre-court logo, with a live preview drawn by the
    /// real court generator. SAVE puts the court in Quick Call and the home-court list; PLAY HERE starts
    /// a Quick Call on it.
    /// </summary>
    public sealed class CourtStudio
    {
        private readonly RectTransform _content;
        private readonly Action _rebuild;
        private readonly CustomCourtData[] _drafts = new CustomCourtData[CourtBuilder.Slots];
        private int _slot;
        private bool _dirty;
        private Texture2D _previewTex;
        private RawImage _preview;

        public CourtStudio(RectTransform content, Action rebuild)
        {
            _content = content;
            _rebuild = rebuild;
            var courts = App.Career.courts = CourtBuilder.Ensure(App.Career.courts);
            for (int i = 0; i < CourtBuilder.Slots; i++) _drafts[i] = courts[i].Clone();
        }

        public void Dispose()
        {
            if (_previewTex != null) UnityEngine.Object.Destroy(_previewTex);
        }

        private CustomCourtData D => _drafts[_slot];

        public void Build()
        {
            // I145: eight slots in two rows; slots 4-8 come with COURT DESIGNER PLUS.
            var career = App.Career;
            int open = Shop.CourtSlots(career);
            RectTransform slots = null;
            for (int i = 0; i < CourtBuilder.Slots; i++)
            {
                if (i % 4 == 0)
                {
                    slots = UiKit.Row(_content, 10f, "Slots" + i);
                    UiKit.Size(slots, 80f);
                }
                int slot = i;
                var d = _drafts[i];
                bool locked = i >= open && !d.built;
                UiKit.Button(slots, locked ? Loc.T("PLUS") + " " + (i + 1) : (d.built ? d.name.ToUpperInvariant() : Loc.T("EMPTY") + " " + (i + 1)), () =>
                {
                    if (locked) { NeedsPlus(); return; }
                    _slot = slot;
                    _rebuild();
                }, i == _slot ? ButtonStyle.Primary : ButtonStyle.Ghost, 70f, 22f);
            }
            if (!Shop.DesignerPlusOwned(career))
                UiKit.Size(UiKit.Label(_content, Loc.T("COURT DESIGNER PLUS adds 5 more court slots, the CHECKER, SUNBURST and PARQUET floors, 6 more skies and every centre-court logo."),
                                       24f, Theme.Gold), 70f);

            var frame = UiKit.Panel(_content, new Color32(0x0B, 0x0B, 0x16, 255), name: "PreviewFrame");
            UiKit.Size(frame, 640f);
            _preview = UiKit.Picture(frame.transform, null, "Preview");
            UiKit.Stretch(_preview.rectTransform, 10f);
            _preview.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            Redraw();

            UiKit.Size(UiKit.Label(_content, "NAME", 30f, Theme.Muted, TextAlignmentOptions.Left, true), 40f);
            UiControls.TextField(_content, D.name, CourtBuilder.MaxName, v => { D.name = CustomTeams.Clean(v, CourtBuilder.MaxName, "My Court"); Changed(); });

            UiControls.ChoiceRow(_content, "FLOOR", PlusNames(CourtBuilder.FloorNames, Shop.FreeFloors), D.floorStyle, i =>
            {
                if (!Shop.FloorOpen(App.Career, i)) { NeedsPlus(); return; }
                D.floorStyle = i;
                Changed();
            });
            ColorRow("FLOOR COLOUR", D.floor, v => D.floor = v);
            ColorRow("LINES", D.lines, v => D.lines = v);
            ColorRow("PAINT", D.paint, v => D.paint = v);
            UiControls.ChoiceRow(_content, "BEHIND", CourtBuilder.StandNames, D.stands, i => { D.stands = i; Changed(); });
            UiControls.ChoiceRow(_content, "CROWD", CourtBuilder.CrowdNames, D.crowd, i => { D.crowd = i; Changed(); });
            var skies = Array.ConvertAll(CourtBuilder.Skies, s => s.Name);
            UiControls.ChoiceRow(_content, "SKY", PlusNames(skies, Shop.FreeSkies), D.sky, i =>
            {
                if (!Shop.SkyOpen(App.Career, i)) { NeedsPlus(); return; }
                D.sky = i;
                Changed();
            });
            // I145: every motif the logo generator draws (the season ones with Plus).
            var motifs = Enum.GetNames(typeof(LogoMotif));
            var logos = new string[motifs.Length + 1];
            logos[0] = "NONE";
            for (int i = 0; i < motifs.Length; i++) logos[i + 1] = motifs[i].ToUpperInvariant() + (i >= Shop.FreeMotifs ? " +" : "");
            UiControls.ChoiceRow(_content, "CENTRE LOGO", logos, D.logoMotif + 1, i =>
            {
                if (i - 1 >= 0 && !Shop.MotifOpen(App.Career, i - 1)) { NeedsPlus(); return; }
                D.logoMotif = i - 1;
                Changed();
            });
            UiControls.ChoiceRow(_content, "LOGO SHAPE", CustomTeams.ShapeNames, D.logoShape, i => { D.logoShape = i; Changed(); });
            ColorRow("LOGO COLOUR", D.logoColor, v => D.logoColor = v);

            UiKit.Button(_content, "RANDOMIZE", () =>
            {
                bool built = D.built;
                string name = D.name;
                _drafts[_slot] = CourtBuilder.Randomize((uint)Environment.TickCount | 1u, name);
                _drafts[_slot].built = built;
                _dirty = true;
                _rebuild();
            }, ButtonStyle.Secondary, 100f, 36f);
            UiKit.Button(_content, _dirty || !D.built ? "SAVE COURT" : "SAVED", Save, ButtonStyle.Primary, 110f, 40f);
            if (D.built)
            {
                UiKit.Button(_content, "PLAY HERE", () =>
                {
                    if (_dirty) Save();
                    var req = CourtBuilder.PlayHere(App.Catalog, App.Career, _slot);
                    if (req == null) return;
                    App.PendingMatch = req;
                    SceneFlow.GoTo(SceneNames.Game);
                }, ButtonStyle.Secondary, 100f, 36f);
                UiKit.Button(_content, "DELETE COURT", () => UiControls.Dialog("DELETE " + D.name.ToUpperInvariant() + "?",
                    "It leaves Quick Call. If it's your team's home court, your team goes back to a street court.",
                    ("DELETE", ButtonStyle.Primary, () =>
                    {
                        _drafts[_slot] = new CustomCourtData { name = "Court " + (_slot + 1) };
                        _dirty = false;
                        Commit();
                        _rebuild();
                    }),
                    ("KEEP", ButtonStyle.Ghost, null)), ButtonStyle.Ghost, 90f, 32f);
            }
            UiKit.Size(UiKit.Label(_content, "Saved courts show up in Quick Call's court list and can be your team's home court (Locker Room ► TEAM).",
                                   26f, Theme.Muted), 80f);
        }

        /// <summary>Option names with a "+" on the ones that come with COURT DESIGNER PLUS.</summary>
        private static string[] PlusNames(string[] names, int free)
        {
            var list = (string[])names.Clone();
            for (int i = free; i < list.Length; i++) list[i] += " +";
            return list;
        }

        /// <summary>I145: a Plus option was picked without Plus: say so, and offer the SHOP.</summary>
        private void NeedsPlus()
        {
            UiControls.Dialog(Loc.T("COURT DESIGNER PLUS"), Loc.T(Shop.Product(Shop.DesignerPlus).Blurb),
                ("SHOP", ButtonStyle.Primary, () => { App.OpenShopOnMenu = true; SceneFlow.GoTo(SceneNames.MainMenu); }),
                ("NOT NOW", ButtonStyle.Ghost, () => _rebuild()));
        }

        private void ColorRow(string label, int value, Action<int> set)
        {
            var row = UiKit.Row(_content, 16f, "Colour " + label);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            UiKit.Size(row, 90f);
            var text = UiKit.Label(row, label, 32f, Theme.Cream, TextAlignmentOptions.Left, true);
            UiKit.Size(text).flexibleWidth = 1f;
            var color = Kits.Unpack(value);
            var swatch = UiKit.Panel(row, ColorPicker.ToColor(color), name: "Swatch");
            UiKit.Size(swatch, 70f, 120f);
            var b = UiKit.Button(row, "CHANGE", () => ColorPicker.Open(label, color,
                preview => { set(Kits.Pack(preview)); swatch.color = ColorPicker.ToColor(preview); Redraw(); },
                done => { set(Kits.Pack(done)); Changed(); }), ButtonStyle.Secondary, 80f, 26f);
            UiKit.Size(b, 80f, 200f);
        }

        private void Changed()
        {
            _dirty = true;
            _rebuild();
        }

        private void Redraw()
        {
            if (_preview == null) return;
            if (_previewTex != null) UnityEngine.Object.Destroy(_previewTex);
            var def = CourtBuilder.Build(D.Clone(), _slot);
            _previewTex = TextureFactory.ToTexture(CourtGenerator.Generate(def, CourtGeometry.Default, 11), "ui.court.preview");
            _preview.texture = _previewTex;
            var fit = _preview.GetComponent<AspectRatioFitter>();
            if (fit != null) fit.aspectRatio = _previewTex.width / (float)_previewTex.height;
        }

        private void Save()
        {
            D.built = true;
            _dirty = false;
            Commit();
            Audio.AudioManager.Play(SfxId.Coin, 0.7f);
            _rebuild();
        }

        private void Commit()
        {
            var career = App.Career;
            for (int i = 0; i < CourtBuilder.Slots; i++)
                if (i == _slot) career.courts[i] = _drafts[i].Clone();
            CourtBuilder.Apply(App.Catalog, career.courts);
            CustomTeams.Apply(App.Catalog, career.customTeam);
            App.SaveCareer();
        }
    }
}
