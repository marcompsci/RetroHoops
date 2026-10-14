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
    /// Locker Room ► KIT: design your team's Home, Away and Alt kits — jersey cut, collar, stripes, chest,
    /// pattern; shorts length, stripe and waistband; shoe height, sole, laces and stripe; any colour from
    /// the 64-colour palette or RGB sliders. A live preview loops your player through idle, run and jump
    /// poses. RANDOMIZE, UNDO, presets from your shop items, share codes / QR and a trading card.
    /// </summary>
    public sealed class KitStudio
    {
        private const int MaxUndo = 40;

        private readonly RectTransform _content;
        private readonly Action _rebuild;
        private readonly KitData[] _drafts = new KitData[Kits.SlotCount];
        private readonly List<(int slot, KitData kit)> _undo = new List<(int, KitData)>();
        private int _slot;
        private bool _dirty;
        private Texture2D _sheetTex;
        private KitPreview _preview;
        private TextMeshProUGUI _status;

        /// <param name="rebuild">Redraws the whole tab (after undo, randomize, slot change...).</param>
        public KitStudio(RectTransform content, Action rebuild)
        {
            _content = content;
            _rebuild = rebuild;
            var career = App.Career;
            EnsureSlots(career);
            for (int i = 0; i < Kits.SlotCount; i++) _drafts[i] = career.kits.slots[i].Clone();
            _slot = career.kits.wear;
        }

        public bool HasUnsavedChanges => _dirty;

        /// <summary>A shared kit (deep link or code) loads into the ALT slot, ready to tweak and save.</summary>
        public void LoadShared(KitData kit)
        {
            PushUndo();
            kit.name = "SHARED";
            _drafts[2] = kit;
            _slot = 2;
            _dirty = true;
        }

        public void Dispose()
        {
            if (_sheetTex != null) UnityEngine.Object.Destroy(_sheetTex);
        }

        private KitData Draft => _drafts[_slot];

        private static void EnsureSlots(CareerSaveData career) => RiseOutfit.EnsureKits(career, App.Catalog);

        // ------------------------------------------------------------------ build

        public void Build()
        {
            var career = App.Career;
            var k = Draft;

            // Preview stage.
            var stage = UiKit.Panel(_content, Color.white, Theme.PanelSprite(), true, "KitStage");
            UiKit.Size(stage, 470f);
            var pic = UiKit.Picture(stage.transform, null, "Player");
            UiKit.Place(pic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(16f * 16f, 24f * 16f));
            _preview = KitPreview.Attach(pic);
            var title = UiKit.Label(stage.transform, Kits.SlotNames[_slot] + " KIT  ·  " + k.name, 32f, Theme.Gold, TextAlignmentOptions.Left, true);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 0.93f), new Vector2(900f, 50f));
            _status = UiKit.Label(stage.transform, "", 26f, Theme.Cyan, TextAlignmentOptions.Center, true);
            UiKit.Place(_status.rectTransform, new Vector2(0.5f, 0.06f), new Vector2(900f, 40f));
            if (_dirty) _status.text = Loc.T("UNSAVED CHANGES");
            RefreshPreview();

            // Slots.
            var slots = UiKit.Row(_content, 14f, "Slots");
            UiKit.Size(slots, 100f);
            for (int i = 0; i < Kits.SlotCount; i++)
            {
                int index = i;
                var b = UiKit.Button(slots, Kits.SlotNames[i], () => { _slot = index; _rebuild(); },
                                     i == _slot ? ButtonStyle.Primary : ButtonStyle.Secondary, 90f, 32f);
            }
            UiControls.ChoiceRow(_content, "WEAR IN GAMES", Kits.SlotNames, career.kits.wear, i =>
            {
                career.kits.wear = i;
                App.SaveCareer();
            });
            UiControls.ToggleRow(_content, "AWAY KIT ON CLASH", career.kits.autoAway, v =>
            {
                career.kits.autoAway = v;
                App.SaveCareer();
            });

            var tools = UiKit.Row(_content, 14f, "Tools");
            UiKit.Size(tools, 100f);
            UiKit.Button(tools, "RANDOMIZE", () =>
            {
                PushUndo();
                string name = Draft.name;
                _drafts[_slot] = Kits.Randomize((uint)Environment.TickCount | 1u, App.Career, name);
                Changed(rebuild: true);
                Haptics.Light();
            }, ButtonStyle.Secondary, 90f, 30f);
            UiKit.Button(tools, "UNDO", Undo, _undo.Count > 0 ? ButtonStyle.Secondary : ButtonStyle.Ghost, 90f, 30f);
            UiKit.Button(tools, "RESET", () =>
            {
                PushUndo();
                var fresh = TeamDefaults()[_slot];
                fresh.name = Draft.name;
                _drafts[_slot] = fresh;
                Changed(rebuild: true);
            }, ButtonStyle.Ghost, 90f, 30f);

            Header("NAME");
            UiControls.TextField(_content, k.name, 10, v =>
            {
                PushUndo();
                Draft.name = CustomTeams.Clean(v, 10, Kits.SlotNames[_slot]).ToUpperInvariant();
                Changed(rebuild: true);
            });

            Header("JERSEY");
            Swatch("JERSEY", () => Draft.jersey, v => Draft.jersey = v);
            Swatch("TRIM", () => Draft.trim, v => Draft.trim = v);
            Swatch("ACCENT", () => Draft.accent, v => Draft.accent = v);
            Style("CUT", Kits.PartCut, Kits.CutNames, () => Draft.cut, v => Draft.cut = v);
            Style("COLLAR", Kits.PartCollar, Kits.CollarNames, () => Draft.collar, v => Draft.collar = v);
            Style("SIDE STRIPES", Kits.PartSides, Kits.SideNames, () => Draft.sides, v => Draft.sides = v);
            Style("CHEST", Kits.PartChest, Kits.ChestNames, () => Draft.chest, v => Draft.chest = v);
            Style("PATTERN", Kits.PartPattern, Kits.PatternNames, () => Draft.pattern, v => Draft.pattern = v);

            Header("SHORTS");
            Swatch("SHORTS", () => Draft.shorts, v => Draft.shorts = v);
            Swatch("SHORTS TRIM", () => Draft.shortsTrim, v => Draft.shortsTrim = v);
            Style("LENGTH", Kits.PartLength, Kits.LengthNames, () => Draft.length, v => Draft.length = v);
            Toggle("SIDE STRIPE", () => Draft.shortsStripe, v => Draft.shortsStripe = v);
            Toggle("WAISTBAND", () => Draft.waistband, v => Draft.waistband = v);

            Header("SHOES");
            Swatch("SHOES", () => Draft.shoe, v => Draft.shoe = v);
            Swatch("SOLE", () => Draft.sole, v => Draft.sole = v);
            Swatch("LACES", () => Draft.laces, v => Draft.laces = v);
            Swatch("STRIPE", () => Draft.shoeStripe, v => Draft.shoeStripe = v);
            Toggle("SHOE STRIPE", () => Draft.shoeStripeOn, v => Draft.shoeStripeOn = v);
            Style("HEIGHT", Kits.PartTop, Kits.TopNames, () => Draft.top, v => Draft.top = v);

            Presets(career);

            Header("SHARE");
            var share = UiKit.Row(_content, 14f, "Share");
            UiKit.Size(share, 100f);
            UiKit.Button(share, "SHARE KIT", () => KitShare.ShowCode(Draft), ButtonStyle.Secondary, 90f, 30f);
            UiKit.Button(share, "ENTER CODE", () => KitShare.EnterCode(kit =>
            {
                PushUndo();
                kit.name = Draft.name;
                _drafts[_slot] = kit;
                Changed(rebuild: true);
                Audio.AudioManager.Play(SfxId.Coin, 0.7f);
            }), ButtonStyle.Secondary, 90f, 30f);
            UiKit.Button(share, "TRADING CARD", () => KitShare.ShowCard(Draft), ButtonStyle.Secondary, 90f, 30f);

            UiKit.Button(_content, "SAVE KITS", Save, ButtonStyle.Primary, 130f);
            UiKit.Size(UiKit.Label(_content,
                "Your player and crew wear your kit in every game with your team. Some styles unlock as you play.",
                28f, Theme.Muted), 80f);
        }

        // ------------------------------------------------------------------ rows

        private void Header(string text) =>
            UiKit.Size(UiKit.Label(_content, text, 38f, Theme.Gold, TextAlignmentOptions.Left, true), 64f);

        /// <summary>Colour row: a chip in the current colour that opens the picker.</summary>
        private void Swatch(string label, Func<int> get, Action<int> set)
        {
            var row = UiKit.NewRect("Colour " + label, _content);
            UiKit.Size(row, 100f);
            var text = UiKit.Label(row, label, 36f, Theme.Cream, TextAlignmentOptions.Left, true);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(0.45f, 1f);
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            var chip = UiKit.Panel(row, ColorPicker.ToColor(Kits.Unpack(get())), name: "Chip");
            chip.raycastTarget = true;
            var crt = chip.rectTransform;
            crt.anchorMin = new Vector2(0.5f, 0.12f);
            crt.anchorMax = new Vector2(1f, 0.88f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            var hex = UiKit.Label(crt, Kits.Unpack(get()).ToHex(), 28f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Stretch(hex.rectTransform);
            void Paint()
            {
                var col = Kits.Unpack(get());
                chip.color = ColorPicker.ToColor(col);
                hex.text = col.ToHex();
                hex.color = col.Luminance > 0.5 ? Theme.Ink : Theme.Cream;
            }
            Paint();
            var button = chip.gameObject.AddComponent<Button>();
            button.targetGraphic = chip;
            button.onClick.AddListener(() =>
            {
                Audio.AudioManager.Click();
                int before = get();
                ColorPicker.Open(label, Kits.Unpack(before),
                    preview: c => { set(Kits.Pack(c)); RefreshPreview(); Paint(); },
                    done: c =>
                    {
                        set(before);
                        if (Kits.Pack(c) == before) return;
                        PushUndo();
                        set(Kits.Pack(c));
                        Changed(rebuild: false);
                        Paint();
                    });
            });
        }

        /// <summary>Style row that cycles through unlocked options and lists how to unlock the rest.</summary>
        private void Style(string label, string part, string[] names, Func<int> get, Action<int> set)
        {
            var career = App.Career;
            var unlocked = new List<int>();
            var locked = new List<string>();
            for (int i = 0; i < names.Length; i++)
            {
                if (Kits.IsUnlocked(part, i, career)) unlocked.Add(i);
                else locked.Add(names[i] + ": " + Loc.T(Kits.HintFor(part, i)));
            }
            int current = Math.Max(0, unlocked.IndexOf(get()));
            UiControls.ChoiceRow(_content, label, unlocked.ConvertAll(i => names[i]).ToArray(), current, choice =>
            {
                PushUndo();
                set(unlocked[choice]);
                Changed(rebuild: false);
            });
            if (locked.Count > 0)
                UiKit.Size(UiKit.Label(_content, Loc.T("LOCKED") + "  " + string.Join("  ·  ", locked), 24f, Theme.Muted), 34f * locked.Count + 10f);
        }

        private void Toggle(string label, Func<bool> get, Action<bool> set)
        {
            UiControls.ToggleRow(_content, label, get(), v =>
            {
                PushUndo();
                set(v);
                Changed(rebuild: false);
            });
        }

        /// <summary>Your shop jerseys and shoes as one-tap colour presets, plus your team colours.</summary>
        private void Presets(CareerSaveData career)
        {
            var c = App.Catalog;
            Header("PRESETS");
            var row = UiKit.Row(_content, 12f, "Presets");
            UiKit.Size(row, 90f);
            UiKit.Button(row, "TEAM COLORS", () =>
            {
                PushUndo();
                var fresh = TeamDefaults()[_slot];
                var d = Draft;
                d.jersey = fresh.jersey; d.trim = fresh.trim; d.accent = fresh.accent; d.shorts = fresh.shorts; d.shortsTrim = fresh.shortsTrim;
                Changed(rebuild: true);
            }, ButtonStyle.Ghost, 80f, 26f);
            foreach (var cos in c.Cosmetics)
            {
                if (!career.ownedCosmetics.Contains(cos.id) && !cos.unlockedByDefault) continue;
                if (cos.slot != CosmeticSlot.JerseyPalette && cos.slot != CosmeticSlot.Shoes) continue;
                var item = cos;
                var b = UiKit.Button(_content, (item.slot == CosmeticSlot.Shoes ? "SHOES: " : "JERSEY: ") + item.displayName.ToUpperInvariant(), () =>
                {
                    PushUndo();
                    if (item.slot == CosmeticSlot.Shoes)
                    {
                        Draft.shoe = Kits.Pack(item.colorA);
                        Draft.shoeStripe = Kits.Pack(item.colorB);
                        Draft.laces = Kits.Pack(item.colorB);
                    }
                    else
                    {
                        Draft.jersey = Kits.Pack(item.colorA);
                        Draft.trim = Kits.Pack(item.colorB);
                        Draft.shortsTrim = Kits.Pack(item.colorB);
                    }
                    Changed(rebuild: true);
                }, ButtonStyle.Ghost, 80f, 28f);
                b.GetComponent<Image>().color = ColorPicker.ToColor(RgbColor.Lerp(item.colorA, RgbColor.White, 0.55f));
            }
        }

        // ------------------------------------------------------------------ state

        private List<KitData> TeamDefaults()
        {
            var tmp = new KitSaveData();
            var c = App.Catalog;
            var team = c.Team(CustomTeams.TeamId) ?? c.Team(DefaultContent.PlayerCrewId);
            Kits.Ensure(tmp, team.primary, team.secondary, team.accent, team.customKit ? team.shorts : (RgbColor?)null,
                        team.customKit ? team.shoes : (RgbColor?)null);
            return tmp.slots;
        }

        private void PushUndo()
        {
            _undo.Add((_slot, Draft.Clone()));
            if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
        }

        private void Undo()
        {
            if (_undo.Count == 0) return;
            var (slot, kit) = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            _slot = slot;
            _drafts[_slot] = kit;
            Changed(rebuild: true);
        }

        private void Changed(bool rebuild)
        {
            _dirty = true;
            Kits.Clamp(Draft);
            if (rebuild) _rebuild();
            else RefreshPreview();
            if (_status != null) _status.text = Loc.T("UNSAVED CHANGES");
        }

        private void Save()
        {
            var career = App.Career;
            for (int i = 0; i < Kits.SlotCount; i++)
            {
                Kits.Clamp(_drafts[i]);
                career.kits.slots[i] = _drafts[i].Clone();
            }
            career.kits.designed = true;
            App.SaveCareer();
            _dirty = false;
            Haptics.Success();
            Audio.AudioManager.Play(SfxId.Fanfare, 0.6f);
            if (_status != null) _status.text = Loc.T("KITS SAVED");
        }

        private void RefreshPreview()
        {
            if (_preview == null) return;
            var me = PlayerCreator.BasePlayer(App.Career, App.Catalog);
            var sheet = CharacterSpriteGenerator.GenerateSheet(me.appearance, Kits.Look(Draft));
            var old = _sheetTex;
            _sheetTex = TextureFactory.ToTexture(sheet, "ui.kit.sheet");
            _preview.SetSheet(_sheetTex);
            if (old != null) UnityEngine.Object.Destroy(old);
        }
    }
}
