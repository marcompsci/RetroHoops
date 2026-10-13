using System;
using System.Collections;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Pixel-art story scene: the speaker's portrait, their name, and a typewriter text box.
    /// Tap (or press any button) to finish the line or go to the next one; SKIP ends the scene.
    /// Built in code like every other screen.
    /// </summary>
    public sealed class StoryView : MonoBehaviour
    {
        private const float CharsPerSecond = 45f;

        private StoryBeat _beat;
        private Action _onDone;
        private int _line = -1;
        private float _lineStart;
        private TextMeshProUGUI _name, _text;
        private UnityEngine.UI.RawImage _portrait;
        private RectTransform _portraitRt;
        private readonly Texture2D[] _faces = new Texture2D[System.Enum.GetValues(typeof(StorySpeaker)).Length + 1];
        private float _openedAt;

        /// <summary>Plays <paramref name="beat"/> and marks it seen; <paramref name="onDone"/> runs afterwards.</summary>
        public static void Show(StoryBeat beat, Action onDone)
        {
            if (beat == null || beat.Lines.Count == 0)
            {
                onDone?.Invoke();
                return;
            }
            var canvas = UiKit.CreateScreenCanvas("StoryCanvas", 60);
            var view = canvas.gameObject.AddComponent<StoryView>();
            view._beat = beat;
            view._onDone = onDone;
            view.Build(canvas);
            view.Next();
        }

        private void Build(Canvas canvas)
        {
            var scrim = UiKit.Panel(canvas.transform, new Color(0.04f, 0.04f, 0.09f, 0.88f), name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var tap = scrim.gameObject.AddComponent<UnityEngine.UI.Button>();
            tap.transition = UnityEngine.UI.Selectable.Transition.None;
            tap.onClick.AddListener(Advance);

            var safe = UiKit.SafeArea(canvas.transform);

            _portrait = UiKit.Picture(safe, null, "Portrait");
            _portraitRt = _portrait.rectTransform;
            _portraitRt.anchorMin = _portraitRt.anchorMax = new Vector2(0.5f, 0.62f);
            _portraitRt.sizeDelta = new Vector2(16 * 26f, 24 * 26f);

            var box = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "TextBox");
            UiKit.Band(box.rectTransform, 0.06f, 0.33f, 40f);
            _name = UiKit.Label(box.transform, "", 48f, Theme.Gold, TextAlignmentOptions.TopLeft, true, "Name");
            UiKit.Stretch(_name.rectTransform, 36f);
            _text = UiKit.Label(box.transform, "", 40f, Theme.Cream, TextAlignmentOptions.TopLeft, false, "Text");
            UiKit.Stretch(_text.rectTransform, 36f);
            _text.rectTransform.offsetMax = new Vector2(-36f, -100f);
            _text.characterSpacing = 1f;

            var hint = UiKit.Label(box.transform, "TAP ►", 30f, Theme.Muted, TextAlignmentOptions.BottomRight, true, "Hint");
            UiKit.Stretch(hint.rectTransform, 30f);

            var skip = UiKit.Button(safe, "SKIP", Finish, ButtonStyle.Ghost, 90f, 34f);
            var srt = (RectTransform)skip.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 1f);
            srt.sizeDelta = new Vector2(200f, 90f);
            srt.anchoredPosition = new Vector2(-32f, -32f);
            _openedAt = Time.unscaledTime;
        }

        private void Update()
        {
            if (_line < 0 || _line >= _beat.Lines.Count) return;
            string full = LineText(_beat.Lines[_line]);
            int shown = Mathf.Min(full.Length, (int)((Time.unscaledTime - _lineStart) * CharsPerSecond));
            if (_text.maxVisibleCharacters != shown) _text.maxVisibleCharacters = shown;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) Advance();
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad != null && pad.buttonSouth.wasPressedThisFrame) Advance();
#endif
        }

        private string LineText(StoryLine l) => l.Text;

        private void Advance()
        {
            if (Time.unscaledTime - _openedAt < 0.25f) return; // ignore the tap that opened the scene
            if (_line >= 0 && _line < _beat.Lines.Count)
            {
                string full = LineText(_beat.Lines[_line]);
                if (_text.maxVisibleCharacters < full.Length)
                {
                    _lineStart = -999f; // finish the line first
                    return;
                }
            }
            Next();
        }

        private void Next()
        {
            _line++;
            if (_line >= _beat.Lines.Count)
            {
                Finish();
                return;
            }
            var l = _beat.Lines[_line];
            _name.text = SpeakerName(l.Speaker);
            var cast = StoryMode.Character(l.Speaker);
            bool rival = l.Speaker == StorySpeaker.Rival || l.Speaker == StorySpeaker.Rival2 || l.Speaker == StorySpeaker.Rival3 || l.Speaker == StorySpeaker.Rival4 || l.Speaker == StorySpeaker.Rival5 || l.Speaker == StorySpeaker.Rival6 || l.Speaker == StorySpeaker.Rival7 || l.Speaker == StorySpeaker.Rival8 || l.Speaker == StorySpeaker.Rival9 || l.Speaker == StorySpeaker.Rival10 || l.Speaker == StorySpeaker.Rival11 || l.Speaker == StorySpeaker.Rival12
                         || (cast != null && cast.Right);
            _name.color = cast != null ? ToColor(RgbColor.FromHex(cast.Color))
                        : l.Speaker == StorySpeaker.Rival ? Theme.Cyan
                        : l.Speaker == StorySpeaker.Rival2 ? (Color)new Color32(0xFF, 0x8C, 0x42, 255)
                        : l.Speaker == StorySpeaker.Rival3 ? (Color)new Color32(0x0E, 0xA5, 0xE9, 255)
                        : l.Speaker == StorySpeaker.Rival4 ? (Color)new Color32(0xE1, 0x1D, 0x48, 255)
                        : l.Speaker == StorySpeaker.Rival5 ? (Color)new Color32(0xC9, 0x18, 0x4A, 255)
                        : l.Speaker == StorySpeaker.Rival6 ? (Color)new Color32(0xFF, 0xD6, 0x0A, 255)
                        : l.Speaker == StorySpeaker.Rival7 ? (Color)new Color32(0xFB, 0x56, 0x07, 255)
                        : l.Speaker == StorySpeaker.Rival8 ? (Color)new Color32(0xFC, 0xBF, 0x49, 255)
                        : l.Speaker == StorySpeaker.Rival9 ? (Color)new Color32(0xF7, 0x25, 0x85, 255)
                        : l.Speaker == StorySpeaker.Rival10 ? (Color)new Color32(0x81, 0xB2, 0x9A, 255)
                        : l.Speaker == StorySpeaker.Rival11 ? (Color)new Color32(0xFF, 0xB7, 0x03, 255)
                        : l.Speaker == StorySpeaker.Rival12 ? (Color)new Color32(0x52, 0xB7, 0x88, 255)
                        : (l.Speaker == StorySpeaker.You ? Theme.Pink : Theme.Gold);
            _text.text = l.Text;
            _text.maxVisibleCharacters = 0;
            _lineStart = Time.unscaledTime;
            _portrait.texture = Face(l.Speaker);
            // The rival stands on the right, everyone else on the left.
            _portraitRt.anchorMin = _portraitRt.anchorMax = new Vector2(rival ? 0.68f : 0.32f, 0.62f);
            _portrait.uvRect = rival ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
            Audio.AudioManager.Click();
        }

        private static Color ToColor(RgbColor c) => new Color32(c.r, c.g, c.b, 255);

        private static string SpeakerName(StorySpeaker s)
        {
            var cast = StoryMode.Character(s);
            if (cast != null) return cast.Name;
            switch (s)
            {
                case StorySpeaker.Coach: return Story.CoachName;
                case StorySpeaker.Rival: return Story.RivalName;
                case StorySpeaker.Rival2: return Story.Rival2Name;
                case StorySpeaker.Rival3: return Story.Rival3Name;
                case StorySpeaker.Rival4: return Story.Rival4Name;
                case StorySpeaker.Rival5: return Story.Rival5Name;
                case StorySpeaker.Rival6: return Story.Rival6Name;
                case StorySpeaker.Rival7: return Story.Rival7Name;
                case StorySpeaker.Rival8: return Story.Rival8Name;
                case StorySpeaker.Rival9: return Story.Rival9Name;
                case StorySpeaker.Rival10: return Story.Rival10Name;
                case StorySpeaker.Rival11: return Story.Rival11Name;
                case StorySpeaker.Rival12: return Story.Rival12Name;
                default: return (App.Career?.nickname ?? "ROOK").ToUpperInvariant();
            }
        }

        private Texture2D Face(StorySpeaker s)
        {
            int i = Mathf.Clamp((int)s, 0, _faces.Length - 1);
            if (_faces[i] != null) return _faces[i];
            var c = App.Catalog;
            var crew = c.Team(DefaultContent.PlayerCrewId);
            AppearanceDef look;
            RgbColor jersey, trim, accent;
            var cast = StoryMode.Character(s);
            switch (cast != null ? (StorySpeaker)(-1) : s)
            {
                case (StorySpeaker)(-1):
                    look = cast.Look;
                    jersey = RgbColor.FromHex(cast.Jersey);
                    trim = RgbColor.FromHex(cast.Trim);
                    accent = RgbColor.FromHex(cast.Accent);
                    break;
                case StorySpeaker.Coach:
                    // Coach Dee: an original character in a gold coach's top.
                    look = new AppearanceDef(3, 4, 0, BodyType.Standard, 1);
                    jersey = RgbColor.FromHex("#FFD166");
                    trim = RgbColor.FromHex("#1A1A2E");
                    accent = crew.accent;
                    break;
                case StorySpeaker.Rival:
                    var rival = c.Team(DefaultContent.RivalCrewId);
                    look = c.Player(DefaultContent.RivalLeaderId)?.appearance ?? new AppearanceDef(1, 3, 4, BodyType.Slim, 2);
                    jersey = rival.primary;
                    trim = rival.secondary;
                    accent = rival.accent;
                    break;
                case StorySpeaker.Rival12:
                    var putt = c.Team(DefaultContent.Rival12CrewId);
                    look = c.Player(DefaultContent.Rival12LeaderId)?.appearance ?? new AppearanceDef(1, 4, 2, BodyType.Standard, 1);
                    jersey = putt.primary;
                    trim = putt.secondary;
                    accent = putt.accent;
                    break;
                case StorySpeaker.Rival11:
                    var kites = c.Team(DefaultContent.Rival11CrewId);
                    look = c.Player(DefaultContent.Rival11LeaderId)?.appearance ?? new AppearanceDef(2, 3, 1, BodyType.Slim, 2);
                    jersey = kites.primary;
                    trim = kites.secondary;
                    accent = kites.accent;
                    break;
                case StorySpeaker.Rival10:
                    var wash = c.Team(DefaultContent.Rival10CrewId);
                    look = c.Player(DefaultContent.Rival10LeaderId)?.appearance ?? new AppearanceDef(4, 2, 2, BodyType.Standard, 1);
                    jersey = wash.primary;
                    trim = wash.secondary;
                    accent = wash.accent;
                    break;
                case StorySpeaker.Rival9:
                    var royals = c.Team(DefaultContent.Rival9CrewId);
                    look = c.Player(DefaultContent.Rival9LeaderId)?.appearance ?? new AppearanceDef(2, 1, 3, BodyType.Slim, 1);
                    jersey = royals.primary;
                    trim = royals.secondary;
                    accent = royals.accent;
                    break;
                case StorySpeaker.Rival8:
                    var lanterns = c.Team(DefaultContent.Rival8CrewId);
                    look = c.Player(DefaultContent.Rival8LeaderId)?.appearance ?? new AppearanceDef(3, 2, 1, BodyType.Slim, 2);
                    jersey = lanterns.primary;
                    trim = lanterns.secondary;
                    accent = lanterns.accent;
                    break;
                case StorySpeaker.Rival7:
                    var couriers = c.Team(DefaultContent.Rival7CrewId);
                    look = c.Player(DefaultContent.Rival7LeaderId)?.appearance ?? new AppearanceDef(5, 3, 0, BodyType.Slim, 1);
                    jersey = couriers.primary;
                    trim = couriers.secondary;
                    accent = couriers.accent;
                    break;
                case StorySpeaker.Rival6:
                    var keepers = c.Team(DefaultContent.Rival6CrewId);
                    look = c.Player(DefaultContent.Rival6LeaderId)?.appearance ?? new AppearanceDef(4, 1, 0, BodyType.Slim, 1);
                    jersey = keepers.primary;
                    trim = keepers.secondary;
                    accent = keepers.accent;
                    break;
                case StorySpeaker.Rival5:
                    var cassette = c.Team(DefaultContent.Rival5CrewId);
                    look = c.Player(DefaultContent.Rival5LeaderId)?.appearance ?? new AppearanceDef(2, 2, 1, BodyType.Standard, 1);
                    jersey = cassette.primary;
                    trim = cassette.secondary;
                    accent = cassette.accent;
                    break;
                case StorySpeaker.Rival4:
                    var cranes = c.Team(DefaultContent.Rival4CrewId);
                    look = c.Player(DefaultContent.Rival4LeaderId)?.appearance ?? new AppearanceDef(1, 3, 2, BodyType.Slim, 1);
                    jersey = cranes.primary;
                    trim = cranes.secondary;
                    accent = cranes.accent;
                    break;
                case StorySpeaker.Rival3:
                    var tide = c.Team(DefaultContent.Rival3CrewId);
                    look = c.Player(DefaultContent.Rival3LeaderId)?.appearance ?? new AppearanceDef(2, 1, 1, BodyType.Standard, 1);
                    jersey = tide.primary;
                    trim = tide.secondary;
                    accent = tide.accent;
                    break;
                case StorySpeaker.Rival2:
                    var syndicate = c.Team(DefaultContent.Rival2CrewId);
                    look = c.Player(DefaultContent.Rival2LeaderId)?.appearance ?? new AppearanceDef(3, 2, 0, BodyType.Slim, 1);
                    jersey = syndicate.primary;
                    trim = syndicate.secondary;
                    accent = syndicate.accent;
                    break;
                default:
                    look = PlayerCreator.BasePlayer(App.Career, c).appearance;
                    jersey = crew.primary;
                    trim = crew.secondary;
                    accent = crew.accent;
                    break;
            }
            var sheet = CharacterSpriteGenerator.GenerateSheet(look, jersey, trim, accent);
            CharacterSpriteGenerator.FrameOrigin(CharacterView.Front, 0, out int fx, out int fy);
            var frame = new PixelCanvas(CharacterSpriteGenerator.SheetFrameWidth, CharacterSpriteGenerator.SheetFrameHeight);
            for (int y = 0; y < frame.Height; y++)
                for (int x = 0; x < frame.Width; x++)
                    frame.Pixels[y * frame.Width + x] = sheet.Get(fx + x, fy + y);
            _faces[i] = TextureFactory.ToTexture(frame, "story.face." + s);
            return _faces[i];
        }

        private void Finish()
        {
            if (_beat != null) Story.MarkSeen(App.Career, _beat.Id);
            App.SaveCareer();
            var done = _onDone;
            _onDone = null;
            Destroy(gameObject);
            done?.Invoke();
        }

        private void OnDestroy()
        {
            foreach (var t in _faces) if (t != null) Destroy(t);
        }
    }
}
