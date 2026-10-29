using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// LockerRoomScene: four tabs over the local career.
    /// PLAYER: nickname and trained ratings. TRAINING: attribute upgrades (cost, level, and why a
    /// purchase is blocked). STYLE: cosmetics shop and equip by slot. CAREER: totals and practice bests.
    /// Every rule is in <see cref="Career"/>; this screen only calls it and saves.
    /// </summary>
    public sealed class LockerRoomController : ScreenBase
    {
        protected override string ScreenTitle => "LOCKER ROOM";
        protected override string BackdropCourtId => "court.pier_nine";

        private enum Tab { Player = 0, Create = 1, Team = 2, Kit = 3, Court = 4, Training = 5, Style = 6, Stats = 7, Trophies = 8 }

        private static readonly string[] TabNames = { "PLAYER", "CREATE", "TEAM", "KIT", "COURT", "TRAIN", "STYLE", "STATS", "TROPHY" };
        private CourtStudio _courtStudio;
        private KitStudio _kitStudio;
        private CustomTeamData _teamDraft;
        private Texture2D _teamPreviewTex;

        private CustomPlayerData _draft;
        private Texture2D _previewTex;

        private void OnDestroy()
        {
            if (_previewTex != null) Destroy(_previewTex);
            if (_teamPreviewTex != null) Destroy(_teamPreviewTex);
            _kitStudio?.Dispose();
            _courtStudio?.Dispose();
        }

        private Tab _tab;
        private RectTransform _content;
        private TextMeshProUGUI _wallet;
        private UnityEngine.UI.Image[] _tabImages;

        protected override void Build()
        {
            _wallet = UiKit.Label(Body, "", 36f, Theme.Cream, TextAlignmentOptions.Center, true, "Wallet");
            UiKit.Band(_wallet.rectTransform, 0.94f, 1f, 24f);

            // Two rows of tabs (5 + 4) so every name fits on one line ("PLAYER", not "PLAY/ER").
            var tabsTop = UiKit.Row(Body, 10f, "Tabs");
            UiKit.Band(tabsTop, 0.906f, 0.937f, 32f);
            var tabsBottom = UiKit.Row(Body, 10f, "Tabs2");
            UiKit.Band(tabsBottom, 0.873f, 0.904f, 32f);
            _tabImages = new UnityEngine.UI.Image[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                var t = (Tab)i;
                var b = UiKit.Button(i < 5 ? tabsTop : tabsBottom, TabNames[i], () => Show(t), ButtonStyle.Secondary, 56f, 28f);
                _tabImages[i] = b.GetComponent<UnityEngine.UI.Image>();
            }

            var holder = UiKit.NewRect("TabBody", Body);
            UiKit.Band(holder, 0f, 0.865f, 0f);
            _content = UiKit.ScrollColumn(holder, 16f, new RectOffset(48, 48, 12, 48));
            // A kit shared by a friend (scanned QR / link) opens straight in the Kit Studio.
            if (App.PendingKit != null)
            {
                _kitStudio = new KitStudio(_content, RebuildKeepingScroll);
                _kitStudio.LoadShared(App.PendingKit);
                App.PendingKit = null;
                Show(Tab.Kit);
                Audio.AudioManager.Play(SfxId.Coin, 0.7f);
                return;
            }
            Show(Tab.Player);
        }

        /// <summary>Rebuilds the tab without jumping back to the top of the list.</summary>
        private void RebuildKeepingScroll()
        {
            var scroll = _content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            float pos = scroll != null ? scroll.verticalNormalizedPosition : 1f;
            Refresh();
            if (scroll != null) StartCoroutine(RestoreScroll(scroll, pos));
        }

        // The old rows are destroyed at the end of the frame, so put the scroll back once they're gone.
        private System.Collections.IEnumerator RestoreScroll(UnityEngine.UI.ScrollRect scroll, float pos)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (scroll != null) scroll.verticalNormalizedPosition = pos;
        }

        private void Show(Tab tab)
        {
            _tab = tab;
            for (int i = 0; i < _tabImages.Length; i++)
                _tabImages[i].color = i == (int)tab ? Color.white : new Color(0.55f, 0.55f, 0.6f, 1f);
            Refresh();
        }

        private void Refresh()
        {
            var career = App.Career;
            _wallet.text = "<color=#FFD166>" + career.signalPoints + " SP</color>    <color=#4CC9F0>" + career.fans + " FANS</color>";
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
            switch (_tab)
            {
                case Tab.Player: BuildPlayer(); break;
                case Tab.Create: BuildCreate(); break;
                case Tab.Team: BuildTeam(); break;
                case Tab.Kit:
                    if (_kitStudio == null) _kitStudio = new KitStudio(_content, RebuildKeepingScroll);
                    _kitStudio.Build();
                    break;
                case Tab.Court:
                    if (_courtStudio == null) _courtStudio = new CourtStudio(_content, RebuildKeepingScroll);
                    _courtStudio.Build();
                    break;
                case Tab.Training: BuildTraining(); break;
                case Tab.Style: BuildStyle(); break;
                case Tab.Trophies: BuildTrophies(); break;
                default: BuildCareer(); break;
            }
        }

        // ------------------------------------------------------------------ player

        private void BuildPlayer()
        {
            var c = App.Catalog;
            var career = App.Career;
            var rook = c.Player(DefaultContent.RookPlayerId);
            if (rook == null)
            {
                UiKit.Size(UiKit.Label(_content, "Rook's profile is missing from content.", 40f, Theme.Pink), 100f);
                return;
            }
            var me = PlayerCreator.BasePlayer(career, c);
            var archetype = c.ArchetypeById(me.archetypeId);
            var ratings = Career.EffectiveRatings(me.attributes, career, c);

            UiKit.Size(UiKit.Label(_content, "NICKNAME", 30f, Theme.Muted, TextAlignmentOptions.Left, true), 40f);
            UiControls.TextField(_content, career.nickname, 14, value =>
            {
                career.nickname = Career.CleanNickname(value);
                App.SaveCareer();
            });

            UiKit.Size(UiKit.Label(_content, (archetype != null ? archetype.displayName.ToUpperInvariant() : "ROOKIE") +
                                             "  ·  #" + me.jerseyNumber + "  ·  OVR " + ratings.Overall,
                                   44f, Theme.Gold, TextAlignmentOptions.Center, true), 70f);
            if (archetype != null) UiKit.Size(UiKit.Label(_content, archetype.description, 30f, Theme.Muted), 90f);

            for (int i = 0; i < RatingScale.AttributeCount; i++)
            {
                var type = (AttributeType)i;
                int trained = ratings.Get(type) - me.attributes.Get(type);
                AttributeBar(_content, type.ToString().ToUpperInvariant() + (trained > 0 ? " <color=#4CC9F0>+" + trained + "</color>" : ""),
                             ratings.Get(type), archetype != null && archetype.strengths.Contains(type));
            }
        }

        private static void AttributeBar(Transform parent, string label, int rating, bool strength)
        {
            var row = UiKit.NewRect("Attr", parent);
            UiKit.Size(row, 72f);

            var name = UiKit.Label(row, label, 32f, strength ? Theme.Gold : Theme.Cream, TextAlignmentOptions.Left, true);
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(0.42f, 1f);
            name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;

            var track = UiKit.Panel(row, Theme.InkLight, name: "Track");
            track.rectTransform.anchorMin = new Vector2(0.42f, 0.3f);
            track.rectTransform.anchorMax = new Vector2(0.86f, 0.7f);
            track.rectTransform.offsetMin = track.rectTransform.offsetMax = Vector2.zero;

            var fill = UiKit.Panel(track.transform, strength ? Theme.Gold : Theme.Cyan, name: "Fill");
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(RatingScale.Normalized(rating), 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            var value = UiKit.Label(row, rating.ToString(), 34f, Theme.Cream, TextAlignmentOptions.Right, true);
            value.rectTransform.anchorMin = new Vector2(0.86f, 0f);
            value.rectTransform.anchorMax = new Vector2(1f, 1f);
            value.rectTransform.offsetMin = value.rectTransform.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ create a player

        private static readonly string[] HairColorNames = { "BLACK", "DARK BROWN", "BROWN", "BLONDE", "RED" };
        private static readonly string[] BodyNames = { "SLIM", "STANDARD", "BROAD" };
        private static readonly string[] HeightNames = { "SHORT", "AVERAGE", "TALL" };

        private void BuildCreate()
        {
            var c = App.Catalog;
            var career = App.Career;
            if (_draft == null)
            {
                var from = career.customPlayer ?? PlayerCreator.FromRook(c);
                _draft = new CustomPlayerData
                {
                    created = from.created, skinTone = from.skinTone, hairStyle = from.hairStyle, hairColor = from.hairColor,
                    body = from.body, heightTier = from.heightTier, jerseyNumber = from.jerseyNumber, archetypeId = from.archetypeId,
                };
            }
            var d = _draft;
            PlayerCreator.Clamp(d, c);

            // Live preview: front-facing idle frame in your crew colours (or your equipped jersey palette).
            var crew = c.Team(DefaultContent.PlayerCrewId);
            var jersey = c.Find(c.Cosmetics, career.equippedJersey);
            var shoes = c.Find(c.Cosmetics, career.equippedShoes);
            var look = new AppearanceDef(d.skinTone, d.hairStyle, d.hairColor, (BodyType)d.body, d.heightTier);
            var sheet = CharacterSpriteGenerator.GenerateSheet(look, jersey?.colorA ?? crew.primary, jersey?.colorB ?? crew.secondary,
                                                               crew.accent, shoes?.colorA, TeamPattern.Solid);
            CharacterSpriteGenerator.FrameOrigin(CharacterView.Front, 0, out int fx, out int fy);
            var frame = new PixelCanvas(CharacterSpriteGenerator.SheetFrameWidth, CharacterSpriteGenerator.SheetFrameHeight);
            for (int y = 0; y < frame.Height; y++)
                for (int x = 0; x < frame.Width; x++)
                    frame.Pixels[y * frame.Width + x] = sheet.Get(fx + x, fy + y);
            if (_previewTex != null) Destroy(_previewTex);
            _previewTex = TextureFactory.ToTexture(frame, "ui.create.preview");
            var stage = UiKit.Panel(_content, Color.white, Theme.PanelSprite(), true, "Preview");
            UiKit.Size(stage, 320f);
            var pic = UiKit.Picture(stage.transform, _previewTex, "Player");
            UiKit.Place(pic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(frame.Width * 11f / CharacterSpriteGenerator.ArtScale, frame.Height * 11f / CharacterSpriteGenerator.ArtScale));
            var number = UiKit.Label(stage.transform, "#" + d.jerseyNumber, 48f, Theme.Gold, TextAlignmentOptions.Right, true);
            UiKit.Place(number.rectTransform, new Vector2(0.85f, 0.75f), new Vector2(200f, 70f));

            Choice("SKIN TONE", Numbered(PlayerCreator.SkinToneCount), d.skinTone, i => d.skinTone = i);
            Choice("HAIR", Numbered(PlayerCreator.HairStyleCount), d.hairStyle, i => d.hairStyle = i);
            Choice("HAIR COLOUR", HairColorNames, d.hairColor, i => d.hairColor = i);
            Choice("BUILD", BodyNames, d.body, i => d.body = i);
            Choice("HEIGHT", HeightNames, d.heightTier, i => d.heightTier = i);
            Stepper("NUMBER", d.jerseyNumber, v => d.jerseyNumber = v);

            var archetypes = c.Archetypes;
            int ai = Mathf.Max(0, archetypes.FindIndex(a => a.id == d.archetypeId));
            Choice("STYLE OF PLAY", archetypes.ConvertAll(a => a.displayName.ToUpperInvariant()).ToArray(), ai, i => d.archetypeId = archetypes[i].id);
            var arch = archetypes[ai];
            UiKit.Size(UiKit.Label(_content, arch.description, 30f, Theme.Muted), 90f);
            var ratings = arch.baseline.Offset(PlayerCreator.RatingOffset);
            UiKit.Size(UiKit.Label(_content, "STARTING OVR " + ratings.Overall + "  ·  your training upgrades carry over", 30f, Theme.Cyan, TextAlignmentOptions.Center, true), 50f);

            UiKit.Button(_content, career.customPlayer.created ? "SAVE CHANGES" : "CREATE PLAYER", () =>
            {
                d.created = true;
                App.Career.customPlayer = d;
                _draft = null;
                App.SaveCareer();
                Core.Haptics.Success();
                Refresh();
            }, ButtonStyle.Primary, 130f);
            if (career.customPlayer.created)
                UiKit.Button(_content, "GO BACK TO ROOK", () =>
                    UiControls.Dialog("USE ROOK?", "Your created player is set aside and Rook takes the court again. Your nickname, stats, and training stay.",
                        ("USE ROOK", ButtonStyle.Primary, () =>
                        {
                            App.Career.customPlayer.created = false;
                            _draft = null;
                            App.SaveCareer();
                            Refresh();
                        }),
                        ("CANCEL", ButtonStyle.Ghost, null)),
                    ButtonStyle.Ghost, 100f, 36f);
            UiKit.Size(UiKit.Label(_content, "Your player stars in Rise Mode, the First Call Classic, Practice, and How to Play.", 28f, Theme.Muted), 70f);
        }

        // ------------------------------------------------------------------ create a team

        /// <summary>Create-a-team: name, colours, kit (jersey, shorts, shoes, pattern), logo, home court.</summary>
        private void BuildTeam()
        {
            var c = App.Catalog;
            var career = App.Career;
            if (_teamDraft == null)
            {
                var from = career.customTeam ?? new CustomTeamData();
                _teamDraft = new CustomTeamData
                {
                    created = from.created, city = from.city, nickname = from.nickname, abbreviation = from.abbreviation,
                    primary = from.primary, secondary = from.secondary, accent = from.accent, shorts = from.shorts, shoes = from.shoes,
                    pattern = from.pattern, logoShape = from.logoShape, logoMotif = from.logoMotif, homeCourtId = from.homeCourtId,
                    useInRise = from.useInRise,
                };
            }
            var d = _teamDraft;
            CustomTeams.Clamp(d, c);

            // Live preview: your player in the kit, next to the team logo.
            var me = PlayerCreator.BasePlayer(career, c);
            var sheet = CharacterSpriteGenerator.GenerateSheet(me.appearance, CustomTeams.Color(d.primary), CustomTeams.Color(d.secondary),
                                                               CustomTeams.Color(d.accent), CustomTeams.Color(d.shoes), (TeamPattern)d.pattern,
                                                               CustomTeams.Color(d.shorts));
            CharacterSpriteGenerator.FrameOrigin(CharacterView.Front, 0, out int fx, out int fy);
            var frame = new PixelCanvas(CharacterSpriteGenerator.SheetFrameWidth, CharacterSpriteGenerator.SheetFrameHeight);
            for (int y = 0; y < frame.Height; y++)
                for (int x = 0; x < frame.Width; x++)
                    frame.Pixels[y * frame.Width + x] = sheet.Get(fx + x, fy + y);
            if (_teamPreviewTex != null) Destroy(_teamPreviewTex);
            _teamPreviewTex = TextureFactory.ToTexture(frame, "ui.team.preview");
            var stage = UiKit.Panel(_content, Color.white, Theme.PanelSprite(), true, "TeamPreview");
            UiKit.Size(stage, 340f);
            var pic = UiKit.Picture(stage.transform, _teamPreviewTex, "Player");
            UiKit.Place(pic.rectTransform, new Vector2(0.3f, 0.5f), new Vector2(frame.Width * 11f / CharacterSpriteGenerator.ArtScale, frame.Height * 11f / CharacterSpriteGenerator.ArtScale));
            var look = new TeamDef
            {
                id = "preview", logoShape = (LogoShape)d.logoShape, logoMotif = (LogoMotif)d.logoMotif,
                primary = CustomTeams.Color(d.primary), secondary = CustomTeams.Color(d.secondary), accent = CustomTeams.Color(d.accent),
            };
            var logo = UiKit.Picture(stage.transform, TextureFactory.TeamLogo(look), "Logo");
            UiKit.Place(logo.rectTransform, new Vector2(0.72f, 0.58f), new Vector2(200f, 200f));
            string full = string.IsNullOrEmpty(d.city) ? d.nickname : d.city + " " + d.nickname;
            var name = UiKit.Label(stage.transform, full.ToUpperInvariant() + "\n<color=#8D99AE>" + d.abbreviation + "</color>", 34f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Place(name.rectTransform, new Vector2(0.72f, 0.16f), new Vector2(460f, 100f));

            UiKit.Size(UiKit.Label(_content, "TEAM NAME", 30f, Theme.Muted, TextAlignmentOptions.Left, true), 40f);
            UiControls.TextField(_content, d.nickname, CustomTeams.MaxNameLength, v => { d.nickname = v; Refresh(); });
            UiKit.Size(UiKit.Label(_content, "CITY (OPTIONAL)", 30f, Theme.Muted, TextAlignmentOptions.Left, true), 40f);
            UiControls.TextField(_content, d.city, CustomTeams.MaxCityLength, v => { d.city = v; Refresh(); });
            UiKit.Size(UiKit.Label(_content, "SHORT NAME (SCOREBOARD)", 30f, Theme.Muted, TextAlignmentOptions.Left, true), 40f);
            UiControls.TextField(_content, d.abbreviation, CustomTeams.MaxAbbreviation, v => { d.abbreviation = v; Refresh(); });

            Choice("JERSEY", CustomTeams.PaletteNames, d.primary, i => d.primary = i);
            Choice("TRIM", CustomTeams.PaletteNames, d.secondary, i => d.secondary = i);
            Choice("ACCENT", CustomTeams.PaletteNames, d.accent, i => d.accent = i);
            Choice("SHORTS", CustomTeams.PaletteNames, d.shorts, i => d.shorts = i);
            Choice("SHOES", CustomTeams.PaletteNames, d.shoes, i => d.shoes = i);
            Choice("JERSEY PATTERN", CustomTeams.PatternNames, d.pattern, i => d.pattern = i);
            Choice("LOGO SHAPE", CustomTeams.ShapeNames, d.logoShape, i => d.logoShape = i);
            Choice("LOGO ICON", CustomTeams.MotifNames, d.logoMotif, i => d.logoMotif = i);
            var courts = CustomTeams.HomeCourts(c);
            int ci = Mathf.Max(0, courts.FindIndex(x => x.id == d.homeCourtId));
            Choice("HOME COURT", courts.ConvertAll(x => x.displayName.ToUpperInvariant()).ToArray(), ci, i => d.homeCourtId = courts[i].id);
            UiControls.ToggleRow(_content, "WEAR IT IN RISE MODE", d.useInRise, v => d.useInRise = v);

            UiKit.Button(_content, career.customTeam.created ? "SAVE CHANGES" : "CREATE TEAM", () =>
            {
                d.created = true;
                App.Career.customTeam = d;
                CustomTeams.Apply(App.Catalog, d);
                _teamDraft = null;
                App.SaveCareer();
                Core.Haptics.Success();
                Audio.AudioManager.Play(SfxId.Fanfare, 0.6f);
                Refresh();
            }, ButtonStyle.Primary, 130f);
            UiKit.Size(UiKit.Label(_content,
                "Your team is your player and your crew in your colours. Pick it in Quick Call, King of the Court, the Arcade Ladder, and 2 Player.",
                28f, Theme.Muted), 90f);
        }

        private static string[] Numbered(int count)
        {
            var names = new string[count];
            for (int i = 0; i < count; i++) names[i] = (i + 1).ToString();
            return names;
        }

        /// <summary>A choice row that redraws the tab (and the preview) after each change.</summary>
        private void Choice(string label, string[] options, int index, System.Action<int> set)
        {
            UiControls.ChoiceRow(_content, label, options, index, i =>
            {
                set(i);
                Refresh();
            });
        }

        private void Stepper(string label, int value, System.Action<int> set)
        {
            var row = UiKit.Row(_content, 12f, "Stepper " + label);
            UiKit.Size(row, 100f);
            var text = UiKit.Label(row, label, 36f, Theme.Cream, TextAlignmentOptions.Left, true);
            UiKit.Size(text, -1f, 300f);
            void Step(int delta)
            {
                set((value + delta + PlayerCreator.MaxJersey + 1) % (PlayerCreator.MaxJersey + 1));
                Refresh();
            }
            UiKit.Button(row, "-10", () => Step(-10), ButtonStyle.Ghost, 90f, 32f);
            UiKit.Button(row, "-1", () => Step(-1), ButtonStyle.Ghost, 90f, 32f);
            UiKit.Size(UiKit.Label(row, value.ToString(), 44f, Theme.Gold, TextAlignmentOptions.Center, true), -1f, 120f);
            UiKit.Button(row, "+1", () => Step(1), ButtonStyle.Ghost, 90f, 32f);
            UiKit.Button(row, "+10", () => Step(10), ButtonStyle.Ghost, 90f, 32f);
        }

        // ------------------------------------------------------------------ training

        private void BuildTraining()
        {
            var c = App.Catalog;
            var career = App.Career;
            var rook = c.Player(DefaultContent.RookPlayerId);
            if (rook == null) return;

            UiKit.Size(UiKit.Label(_content, "Spend Signal Points to train. Each upgrade needs " +
                                             "a game played since your last one.", 30f, Theme.Muted), 90f);
            foreach (var u in c.Upgrades)
            {
                int level = career.UpgradeLevel(u.id);
                var check = Career.CanBuy(career, u, PlayerCreator.BaseRatings(career, c));
                string cost = level >= u.maxLevel ? "MAX" : Career.UpgradeCost(u, level) + " SP";

                var card = UiKit.Panel(_content, Color.white, Theme.PanelSprite(), true, "Upgrade " + u.id);
                UiKit.Size(card, 190f);
                var text = UiKit.Label(card.transform,
                    u.displayName.ToUpperInvariant() + "  <color=#8D99AE>LV " + level + "/" + u.maxLevel + "</color>\n" +
                    "<size=75%><color=#8D99AE>" + u.description + "</color></size>\n" +
                    "<size=75%>" + Reason(check) + "</size>",
                    34f, Theme.Cream, TextAlignmentOptions.Left, true);
                UiKit.Stretch(text.rectTransform);
                text.rectTransform.offsetMin = new Vector2(28f, 10f);
                text.rectTransform.offsetMax = new Vector2(-260f, -10f);

                var upgrade = u;
                var buy = UiKit.Button(card.transform, cost, () =>
                {
                    if (Career.Buy(App.Career, upgrade, PlayerCreator.BaseRatings(App.Career, App.Catalog)) == UpgradeCheck.Ok)
                    {
                        App.SaveCareer();
                        Haptics.Success();
                    }
                    Refresh();
                }, check == UpgradeCheck.Ok ? ButtonStyle.Primary : ButtonStyle.Ghost, 110f, 34f);
                var brt = (RectTransform)buy.transform;
                brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
                brt.pivot = new Vector2(1f, 0.5f);
                brt.sizeDelta = new Vector2(220f, 110f);
                brt.anchoredPosition = new Vector2(-20f, 0f);
                buy.interactable = check == UpgradeCheck.Ok;
            }
        }

        private static string Reason(UpgradeCheck check)
        {
            switch (check)
            {
                case UpgradeCheck.Ok: return "<color=#4CC9F0>Ready to train</color>";
                case UpgradeCheck.MaxLevel: return "<color=#FFD166>Fully trained</color>";
                case UpgradeCheck.AtAttributeCap: return "<color=#FFD166>At this upgrade's rating cap</color>";
                case UpgradeCheck.NotEnoughPoints: return "<color=#F72585>Not enough Signal Points</color>";
                case UpgradeCheck.NeedsTraining: return "<color=#F72585>Play a game first (training time)</color>";
                default: return "";
            }
        }

        // ------------------------------------------------------------------ style

        private void BuildStyle()
        {
            var c = App.Catalog;
            var career = App.Career;
            var slots = new[] { CosmeticSlot.JerseyPalette, CosmeticSlot.Shoes, CosmeticSlot.Gear, CosmeticSlot.CourtBanner, CosmeticSlot.Celebration, CosmeticSlot.DribbleMove, CosmeticSlot.DunkPackage };
            string[] slotNames = { "JERSEY PALETTE", "SHOES", "GEAR", "COURT BANNER", "CELEBRATION", "DRIBBLE MOVE", "DUNK PACKAGE" };
            if (career.kits.designed)
                UiKit.Size(UiKit.Label(_content, "Your KIT tab design is what your team wears. Jerseys and shoes you own here show up there as presets.",
                                       28f, Theme.Cyan), 80f);
            for (int s = 0; s < slots.Length; s++)
            {
                var items = c.Cosmetics.FindAll(x => x.slot == slots[s]);
                if (items.Count == 0) continue;
                UiKit.Size(UiKit.Label(_content, slotNames[s], 36f, Theme.Gold, TextAlignmentOptions.Left, true), 56f);
                foreach (var item in items) CosmeticRow(item, career);
            }
            if (slots.Length > 0)
                UiKit.Size(UiKit.Label(_content, "Celebrations, dribble moves and dunk packages change how you look, not your ratings.",
                                       28f, Theme.Muted), 80f);
        }

        private static int PassTierOf(string id)
        {
            foreach (var set in HoopsPass.GearSets)
                for (int i = 0; i < set.Length; i++)
                    if (set[i] == id) return (i + 1) * 5;
            return 0;
        }

        private void CosmeticRow(CosmeticDef item, CareerSaveData career)
        {
            bool owned = career.ownedCosmetics.Contains(item.id);
            bool equipped = career.Equipped(item.slot) == item.id;
            var check = Career.CanBuy(career, item);

            var card = UiKit.Panel(_content, Color.white, Theme.PanelSprite(), true, "Cosmetic " + item.id);
            UiKit.Size(card, 130f);

            var swatchA = UiKit.Panel(card.transform, item.colorA.ToColor(), name: "SwatchA");
            UiKit.Place(swatchA.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 70f));
            swatchA.rectTransform.anchoredPosition = new Vector2(46f, 0f);
            var swatchB = UiKit.Panel(card.transform, item.colorB.ToColor(), name: "SwatchB");
            UiKit.Place(swatchB.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 70f));
            swatchB.rectTransform.anchoredPosition = new Vector2(94f, 0f);

            string status = owned ? (equipped ? "<color=#4CC9F0>EQUIPPED</color>" : "<color=#8D99AE>OWNED</color>")
                          : check == CosmeticCheck.PassOnly && item.id == LiveSeason.StarBannerId ? "<color=#9B4DFF>LIVE SEASON: FINISH ALL-STAR</color>"
                          : check == CosmeticCheck.PassOnly ? "<color=#9B4DFF>HOOPS PASS TIER " + PassTierOf(item.id) + "</color>"
                          : check == CosmeticCheck.StoreOnly ? "<color=#2EC4B6>" + Loc.T("SOLD IN") + " " + RegionStore.Name(item.store) + "</color>"
                          : check == CosmeticCheck.NeedsFans ? "<color=#F72585>Needs " + item.fansRequired + " fans</color>"
                          : "<color=#FFD166>" + item.cost + " SP</color>";
            var text = UiKit.Label(card.transform, item.displayName.ToUpperInvariant() + "\n<size=75%>" + status + "</size>",
                                   32f, Theme.Cream, TextAlignmentOptions.Left, true);
            UiKit.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(140f, 6f);
            text.rectTransform.offsetMax = new Vector2(-230f, -6f);

            if (equipped) return;
            string label = owned ? "EQUIP" : "BUY";
            bool usable = owned || check == CosmeticCheck.Ok;
            var cosmetic = item;
            var button = UiKit.Button(card.transform, label, () =>
            {
                var data = App.Career;
                bool changed = data.ownedCosmetics.Contains(cosmetic.id)
                    ? Career.Equip(data, cosmetic)
                    : Career.Buy(data, cosmetic) == CosmeticCheck.Ok;
                if (changed) App.SaveCareer();
                Refresh();
            }, usable ? ButtonStyle.Primary : ButtonStyle.Ghost, 90f, 32f);
            button.interactable = usable;
            var brt = (RectTransform)button.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.sizeDelta = new Vector2(200f, 90f);
            brt.anchoredPosition = new Vector2(-18f, 0f);
        }

        // ------------------------------------------------------------------ career

        private void BuildCareer()
        {
            var career = App.Career;
            var t = career.totals;
            string fg = t.fieldGoalsAttempted == 0 ? "-" : Mathf.RoundToInt(100f * t.fieldGoalsMade / t.fieldGoalsAttempted) + "%";
            Line("GAMES", t.games + "  (" + t.wins + "-" + t.losses + ")");
            Line("POINTS", t.points.ToString());
            Line("ASSISTS", t.assists.ToString());
            Line("REBOUNDS", t.rebounds.ToString());
            Line("STEALS", t.steals.ToString());
            Line("BLOCKS", t.blocks.ToString());
            Line("FIELD GOALS", t.fieldGoalsMade + "/" + t.fieldGoalsAttempted + "  " + fg);
            Line("GREEN RELEASES", t.greens.ToString());
            Line("CHAMPIONSHIPS", t.championships.ToString());
            Line("CLASSIC TITLES", career.classic.titles.ToString());
            // Phase 35: every shot of your career, by spot.
            if (ShotCharts.Attempts(career.shotChart) > 0)
                ShotChartView.Build(_content, "CAREER SHOT CHART", career.shotChart, 320f, "MY CAREER SHOT CHART");
            // Phase 36: SPOT SPECIALIST ranks.
            Header("SPOT SPECIALIST");
            UiKit.Size(UiKit.Label(_content, "Make shots from an area, better than usual, to rank up. Each rank adds a little to your shots from there (not in two-phone or Live games).",
                                   24f, Theme.Muted, TextAlignmentOptions.Left, false), 70f);
            for (int g = 0; g < Specialist.Groups; g++)
                UiKit.Size(UiKit.Label(_content, Specialist.Line(career.shotChart, (SpotGroup)g), 26f,
                                       Specialist.TierOf(career.shotChart, (SpotGroup)g) == SpecialistTier.Gold ? Theme.Gold : Theme.Cream,
                                       TextAlignmentOptions.Left, true), 44f);

            Header("RECORDS (ONE GAME)");
            var r = career.records;
            Line("POINTS", r.points.ToString());
            Line("ASSISTS", r.assists.ToString());
            Line("REBOUNDS", r.rebounds.ToString());
            Line("STEALS", r.steals.ToString());
            Line("BLOCKS", r.blocks.ToString());
            Line("GREENS", r.greens.ToString());
            Line("BIGGEST WIN", r.biggestWin > 0 ? "+" + r.biggestWin : "-");
            Line("WIN STREAK", r.winStreak + "  (best " + r.bestWinStreak + ")");

            // I3 LEGACY: the nights themselves, not just the numbers.
            Header("MOMENTS");
            if (career.moments == null || career.moments.Count == 0)
                UiKit.Size(UiKit.Label(_content, "Hit a game-winner, drop 20, go perfect from the floor: your big nights land here.", 26f, Theme.Muted, TextAlignmentOptions.Left, false), 70f);
            else
            {
                Line("GAME WINNERS", (MomentsBook.Count(career, MomentKind.GameWinner) + MomentsBook.Count(career, MomentKind.BuzzerBeater)).ToString()
                     + "  (" + MomentsBook.Count(career, MomentKind.BuzzerBeater) + " " + Logic.Loc.T("AT THE BUZZER") + ")");
                int shownM = 0;
                foreach (var m in career.moments)
                {
                    if (shownM++ >= 15) break;
                    var oppM = App.Catalog.Team(m.opponentId);
                    UiKit.Size(UiKit.Label(_content, MomentsBook.Line(m, oppM?.abbreviation), 26f,
                                           m.kind == MomentKind.BuzzerBeater || m.kind == MomentKind.GameWinner ? Theme.Gold : Theme.Cream,
                                           TextAlignmentOptions.Left, true), 42f);
                }
            }

            if (career.seasons.Count > 0)
            {
                Header("RISE SEASONS");
                foreach (var e in career.seasons)
                {
                    string name = e.season == 0 ? "CIRCUIT" : "SEASON " + e.season;
                    string ppg = e.games > 0 ? (e.points / (float)e.games).ToString("0.0") + " PPG" : "-";
                    Line(name, e.wins + "-" + e.losses + "  ·  " + ppg + (string.IsNullOrEmpty(e.result) ? "" : "  ·  " + e.result.ToUpperInvariant()));
                }
            }

            if (career.history.Count > 0)
            {
                Header("RECENT GAMES");
                int shown = 0;
                foreach (var h in career.history)
                {
                    if (shown++ >= 10) break;
                    var opp = App.Catalog.Team(h.opponentId);
                    string result = (h.Won ? "<color=#4CC9F0>W</color> " : "<color=#F72585>L</color> ") + h.scoreFor + "-" + h.scoreAgainst;
                    Line(ModeName(h.mode) + " vs " + (opp?.abbreviation ?? "?"), result + "  ·  " + h.points + " PTS " + h.assists + " AST " + h.rebounds + " REB");
                }
            }

            Header("PRACTICE BESTS");
            var p = career.practice;
            Line("FREE SHOOT", p.freeShootMakes + " makes · streak " + p.freeShootStreak);
            Line("PASSING TARGETS", p.passingScore.ToString());
            Line("DRIBBLE LANE", p.dribbleLaneTime > 0f ? p.dribbleLaneTime.ToString("0.00") + " s" : "-");
            Line("3-POINT CONTEST", p.threePointBest.ToString());
            Line("LOCKDOWN", p.lockdownBest + " / 6");

            Header("ARCADE");
            Line("KING STREAK", career.king.best.ToString());
            Line("LADDER CLEARS", career.secrets.arcade.clears.ToString());
            Line("CALLER CUP TITLES", career.cup.titles.ToString());
            Line("SHOOTOUT WINS", career.practice.shootoutWins.ToString());
            Line("ALLEY-OOPS", career.totals.alleyOops.ToString());
            Line("HEAT CHECKS", career.totals.heatUps.ToString());
            Line("BEST STAGE", career.secrets.arcade.bestRung + " / " + ArcadeEngine.Rungs);
        }

        /// <summary>Trophy room: titles won and every badge (earned in gold, locked ones show how to earn them).</summary>
        private void BuildTrophies()
        {
            var career = App.Career;
            Header("TITLES");
            Line("GOLD SIGNAL CUP", career.totals.championships.ToString());
            Line("FIRST CALL CLASSIC", career.classic.titles.ToString());
            Line("VS NEON STATIC", career.rival.wins + "-" + career.rival.losses);
            Line("CIRCUIT", career.rise.stage != RiseStage.Circuit || career.rise.seasonsPlayed > 0 ? "CLEARED" : career.rise.circuitBeaten.Count + "/" + RiseEngine.CircuitOrder.Length);

            Line("ARCADE LADDER", career.secrets.arcade.clears.ToString());
            Line("KING OF THE COURT", "BEST " + career.king.best);

            // Secret codes: found ones show their symbols; earned hints say how to find the rest.
            var sec = career.secrets;
            Header("SECRET CODES  " + sec.codesFound.Count + "/" + Secrets.All.Count);
            foreach (var code in Secrets.All)
            {
                bool found = Secrets.Found(sec, code.Id);
                bool hint = sec.hintsRevealed.Contains(code.Id);
                string value = found ? "<color=#FFD166>" + code.SequenceText + "</color>"
                             : hint ? code.SequenceText
                             : "<color=#8D99AE>" + Loc.T(code.HintCondition) + "</color>";
                Line(found || hint ? Loc.T(code.Name) : "???", value);
            }
            UiKit.Size(UiKit.Label(_content, "Enter codes in Settings ► SECRETS.", 28f, Theme.Muted), 44f);

            Header("BADGES  " + Badges.EarnedCount(career) + "/" + Badges.All.Count);
            foreach (var b in Badges.All)
            {
                bool earned = Badges.IsEarned(b, career);
                var card = UiKit.Panel(_content, Color.white, Theme.PanelSprite(), true, "Badge " + b.Id);
                UiKit.Size(card, 110f);
                var medal = UiKit.Panel(card.transform, Color.white, Theme.DiscSprite(earned ? Theme.Gold : Theme.InkLight, Theme.Shadow), false, "Medal");
                medal.rectTransform.anchorMin = medal.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                medal.rectTransform.pivot = new Vector2(0f, 0.5f);
                medal.rectTransform.sizeDelta = new Vector2(80f, 80f);
                medal.rectTransform.anchoredPosition = new Vector2(20f, 0f);
                string title = Loc.T(b.Title);
                var text = UiKit.Label(card.transform, (earned ? title : "<color=#8D99AE>" + title + "</color>") +
                                       "\n<size=70%><color=#8D99AE>" + Loc.T(b.Description) + "</color></size>",
                                       34f, earned ? Theme.Gold : Theme.Cream, TextAlignmentOptions.Left, true);
                UiKit.Stretch(text.rectTransform);
                text.rectTransform.offsetMin = new Vector2(120f, 6f);
                text.rectTransform.offsetMax = new Vector2(-20f, -6f);
            }
        }

        private void Header(string text) =>
            UiKit.Size(UiKit.Label(_content, text, 36f, Theme.Gold, TextAlignmentOptions.Left, true), 60f);

        private static string ModeName(GameMode m)
        {
            switch (m)
            {
                case GameMode.Rise: return "RISE";
                case GameMode.Tournament: return "CLASSIC";
                case GameMode.Daily: return "DAILY";
                case GameMode.Rival: return "RIVAL";
                case GameMode.King: return "KING";
                case GameMode.Arcade: return "ARCADE";
                case GameMode.OneOnOne: return "1-ON-1";
                case GameMode.Cup: return "CUP";
                case GameMode.Clutch: return "CLUTCH";
                case GameMode.Streets: return "STREETS";
                case GameMode.Street: return "PARK";
                default: return "QUICK";
            }
        }

        private void Line(string label, string value)
        {
            UiKit.Size(UiKit.Label(_content, "<color=#8D99AE>" + label + "</color><pos=55%>" + value, 34f, Theme.Cream,
                                   TextAlignmentOptions.Left, true), 52f);
        }
    }
}
