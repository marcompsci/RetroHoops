using System;
using CallerRetroBall.Logic;
using CallerRetroBall.UI;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// Match HUD: pause button, both scores (the team with the ball is highlighted), game clock,
    /// and shot clock. Only rewrites text when a value actually changes (no per-frame allocs).
    /// </summary>
    public sealed class MatchHud : MonoBehaviour
    {
        public event Action PauseRequested;
        public event Action ResumeRequested;
        public event Action QuitRequested;
        public event Action RematchRequested;
        public event Action ContinueRequested;
        public event Action<PlayCall> PlayChosen;
        public event Action ReplayRequested;
        public event Action PlayOfTheGameRequested;
        /// <summary>Phase 40: play the HIGHLIGHTS reel, and share the result card.</summary>
        public event Action HighlightsRequested, ShareResultRequested;
        /// <summary>Make a GIF of the play of the game and share / save it.</summary>
        public event Action ShareRequested;
        public event Action PhotoRequested;
        /// <summary>14 Oct 2026: PHONE on the pause menu (music, texts, feed).</summary>
        public event Action PhoneRequested;
        /// <summary>16 Oct 2026: LEAVE COURT (walk off into Baller City). Shown only when the match controller turns it on.</summary>
        public event Action LeaveCourtRequested;
        /// <summary>16 Oct 2026 compliance gate (App Review 1.2): REPORT PLAYER and BLOCK PLAYER in LIVE games.</summary>
        public event Action ReportPlayerRequested, BlockPlayerRequested;
        private bool _playerSafety;

        private TextMeshProUGUI _teamA, _teamB, _scoreA, _scoreB, _clock, _shotClock, _toast;
        private GameObject _pausePanel;
        private GameObject _finalPanel;
        private TextMeshProUGUI _finalTitle;
        private TextMeshProUGUI _finalScore;
        private RectTransform _finalColumn;
        private GameObject _callMenu;
        private TextMeshProUGUI _info;
        private string _lastInfo = "";
        private int _lastScoreA = -1, _lastScoreB = -1, _lastClock = -1, _lastShot = -1, _lastOffense = -1;
        private float _toastUntil;
        private UnityEngine.UI.Image _micPanel;
        private TextMeshProUGUI _micText;
        private float _micUntil;
        private float _punchA = -10f, _punchB = -10f;

        /// <summary>Accessibility: no score bounce.</summary>
        public static bool ReduceMotion { get; set; }

        private RectTransform _bar;
        private bool _barLandscape;
        private bool _barSet;

        /// <summary>Landscape: a compact score bar in the middle (the stands and court show either side); portrait: full width.</summary>
        private void LayoutBar()
        {
            if (_bar == null) return;
            bool landscape = Screen.width > Screen.height;
            if (_barSet && landscape == _barLandscape) return;
            _barSet = true;
            _barLandscape = landscape;
            if (_callMenu != null)
            {
                // Five plays: the menu is tall, so in landscape it starts lower to fit under the score bar.
                var crt = (RectTransform)_callMenu.transform;
                crt.anchoredPosition = new Vector2(crt.anchoredPosition.x, landscape ? 120f : 520f);
            }
            if (landscape)
            {
                _bar.anchorMin = _bar.anchorMax = new Vector2(0.5f, 1f);
                _bar.sizeDelta = new Vector2(1100f, 130f);
            }
            else
            {
                _bar.anchorMin = new Vector2(0f, 1f);
                _bar.anchorMax = new Vector2(1f, 1f);
                _bar.sizeDelta = new Vector2(-32f, 150f);
            }
        }

        /// <summary>Left-handed layout: the CALL menu opens on the left, near the buttons.</summary>
        public void SetCallMenuLeft(bool left)
        {
            var rt = (RectTransform)_callMenu.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(left ? 0f : 1f, 0f);
            rt.pivot = new Vector2(left ? 0f : 1f, 0f);
            rt.anchoredPosition = new Vector2(left ? 24f : -24f, rt.anchoredPosition.y);
        }
        private const float PunchSeconds = 0.3f;

        public static MatchHud Create(TeamDef a, TeamDef b)
        {
            var canvas = UiKit.CreateScreenCanvas("MatchHudCanvas", 20);
            var hud = canvas.gameObject.AddComponent<MatchHud>();
            hud.Build(UiKit.SafeArea(canvas.transform), a, b);
            return hud;
        }

        private void Build(RectTransform safe, TeamDef a, TeamDef b)
        {
            var bar = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "ScoreBar");
            var rt = bar.rectTransform;
            _bar = rt;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-32f, 150f);
            rt.anchoredPosition = new Vector2(0f, -12f);

            var pause = UiKit.Button(rt, "II", () => PauseRequested?.Invoke(), ButtonStyle.Ghost, 110f, 44f);
            var prt = (RectTransform)pause.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0f, 0.5f);
            prt.sizeDelta = new Vector2(110f, 110f);
            prt.anchoredPosition = new Vector2(20f, 0f);

            _teamA = Text(rt, a.abbreviation, 40f, new Vector2(0.24f, 0.72f));
            _scoreA = Text(rt, "0", 64f, new Vector2(0.24f, 0.32f));
            _teamB = Text(rt, b.abbreviation, 40f, new Vector2(0.86f, 0.72f));
            _scoreB = Text(rt, "0", 64f, new Vector2(0.86f, 0.32f));
            _clock = Text(rt, "2:00", 56f, new Vector2(0.55f, 0.66f));
            _shotClock = Text(rt, "14", 36f, new Vector2(0.55f, 0.24f));
            _shotClock.color = Theme.Gold;

            // I8 MY LINE: your live box score under the score bar.
            _myLine = UiKit.Label(safe, "", 24f, Theme.Cyan, TextAlignmentOptions.Center, true, "MyLine");
            _myLine.rectTransform.anchorMin = _myLine.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _myLine.rectTransform.pivot = new Vector2(0.5f, 1f);
            _myLine.rectTransform.sizeDelta = new Vector2(1000f, 32f);
            _myLine.rectTransform.anchoredPosition = new Vector2(0f, -166f);
            _myLine.raycastTarget = false;

            _toast = UiKit.Label(safe, "", 44f, Theme.Cream, TextAlignmentOptions.Center, true, "Toast");
            UiKit.Place(_toast.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(1000f, 120f));
            // Long lines (coach tips, H-O-R-S-E calls) shrink and wrap instead of spilling off screen.
            _toast.enableAutoSizing = true;
            _toast.fontSizeMin = 26f;
            _toast.fontSizeMax = 44f;

            _info = UiKit.Label(safe, "", 36f, Theme.Gold, TextAlignmentOptions.Center, true, "Info");
            _info.rectTransform.anchorMin = _info.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _info.rectTransform.pivot = new Vector2(0.5f, 1f);
            _info.rectTransform.sizeDelta = new Vector2(1000f, 60f);
            _info.enableAutoSizing = true; // long lines (controls hint) shrink to fit
            _info.fontSizeMin = 20f;
            _info.fontSizeMax = 36f;
            _info.rectTransform.anchoredPosition = new Vector2(0f, -204f); // I8: the live stat line sits just under the score bar

            // Mic Tally's commentary ticker, under the score bar.
            _micPanel = UiKit.Panel(safe, new Color(0.04f, 0.04f, 0.09f, 0.78f), name: "MicTicker");
            var mrt = _micPanel.rectTransform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 1f);
            mrt.pivot = new Vector2(0.5f, 1f);
            mrt.sizeDelta = new Vector2(980f, 64f);
            mrt.anchoredPosition = new Vector2(0f, -236f);
            _micPanel.raycastTarget = false;
            _micText = UiKit.Label(mrt, "", 30f, Theme.Cream, TextAlignmentOptions.Center, true, "MicText");
            UiKit.Stretch(_micText.rectTransform, 10f);
            _micText.enableAutoSizing = true;
            _micText.fontSizeMin = 18f;
            _micText.fontSizeMax = 30f;
            _micPanel.gameObject.SetActive(false);

            BuildCallMenu(safe);
            BuildReplayUi(safe);
            BuildPausePanel(safe);
            BuildFinalPanel(safe);
        }

        private static TextMeshProUGUI Text(RectTransform parent, string text, float size, Vector2 anchor)
        {
            var t = UiKit.Label(parent, text, size, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Place(t.rectTransform, anchor, new Vector2(260f, size * 1.3f));
            return t;
        }

        private void BuildPausePanel(RectTransform safe)
        {
            var scrim = UiKit.Panel(safe.parent, Theme.Scrim, name: "PauseScrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true; // blocks touches to the game while paused
            _pausePanel = scrim.gameObject;

            var column = UiKit.Column(scrim.transform, 28f, null, "PauseMenu");
            UiKit.Band(column, 0.12f, 0.88f, 160f);
            column.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UiKit.Size(UiKit.ShadowLabel(column, "PAUSED", 90f, Theme.Cream, Theme.Pink, 8f).transform.parent.GetComponent<RectTransform>(), 140f);
            UiKit.Button(column, "RESUME", () => ResumeRequested?.Invoke(), ButtonStyle.Primary, 150f);
            UiKit.Button(column, "PHOTO MODE", () => PhotoRequested?.Invoke(), ButtonStyle.Secondary, 130f, 48f);
            UiKit.Button(column, "PHONE", () => PhoneRequested?.Invoke(), ButtonStyle.Secondary, 130f, 48f);
            _pauseColumn = column;
            _pauseQuit = UiKit.Button(column, "QUIT TO MENU", () => QuitRequested?.Invoke(), ButtonStyle.Ghost, 130f, 48f);
            _pausePanel.SetActive(false);
        }

        private RectTransform _pauseColumn;
        private UnityEngine.UI.Button _pauseQuit;
        private bool _leaveCourt;

        /// <summary>16 Oct 2026 (BALLER CITY): adds LEAVE COURT to the pause menu and the end screens.</summary>
        public void EnableLeaveCourt()
        {
            if (_leaveCourt || _pauseColumn == null) return;
            _leaveCourt = true;
            var b = UiKit.Button(_pauseColumn, "LEAVE COURT", () => LeaveCourtRequested?.Invoke(), ButtonStyle.Secondary, 130f, 48f);
            if (_pauseQuit != null) b.transform.SetSiblingIndex(_pauseQuit.transform.GetSiblingIndex());
        }

        private void LeaveCourtButton()
        {
            if (_leaveCourt) UiKit.Button(_finalColumn, "LEAVE COURT", () => LeaveCourtRequested?.Invoke(), ButtonStyle.Secondary, 110f, 44f);
            // Every end screen calls this, so LIVE games' REPORT / BLOCK sit here too.
            if (_playerSafety)
            {
                var row = UiKit.Row(_finalColumn, 16f, "PlayerSafety");
                UiKit.Size(row, 70f);
                UiKit.Button(row, "REPORT PLAYER", () => ReportPlayerRequested?.Invoke(), ButtonStyle.Ghost, 90f, 26f);
                UiKit.Button(row, "BLOCK PLAYER", () => BlockPlayerRequested?.Invoke(), ButtonStyle.Ghost, 90f, 26f);
            }
        }

        /// <summary>
        /// LIVE games (opponents Game Center found): REPORT PLAYER and BLOCK PLAYER on the pause menu and the end screens.
        /// </summary>
        public void EnablePlayerSafety()
        {
            if (_playerSafety || _pauseColumn == null) return;
            _playerSafety = true;
            var report = UiKit.Button(_pauseColumn, "REPORT PLAYER", () => ReportPlayerRequested?.Invoke(), ButtonStyle.Ghost, 110f, 36f);
            var block = UiKit.Button(_pauseColumn, "BLOCK PLAYER", () => BlockPlayerRequested?.Invoke(), ButtonStyle.Ghost, 110f, 36f);
            if (_pauseQuit != null)
            {
                int at = _pauseQuit.transform.GetSiblingIndex();
                report.transform.SetSiblingIndex(at);
                block.transform.SetSiblingIndex(at + 1);
            }
        }

        /// <summary>Hides the end screen (the walk-off plays over the court).</summary>
        public void HideFinal()
        {
            if (_finalPanel != null) _finalPanel.SetActive(false);
        }

        private GameObject _cash;

        /// <summary>17 Oct 2026: CASHED OUT: a big graffiti-style stamp over the end screen for a couple of seconds.</summary>
        public void CashOut(string amount, string sub)
        {
            if (_cash != null) Destroy(_cash);
            var canvas = UiKit.CreateScreenCanvas("CashOutCanvas", 65);
            _cash = canvas.gameObject;
            var holder = UiKit.NewRect("CashOut", canvas.transform);
            UiKit.Place(holder, new Vector2(0.5f, 0.62f), new Vector2(980f, 420f));
            holder.localRotation = Quaternion.Euler(0f, 0f, 6f);
            var plate = UiKit.Panel(holder, new Color(0.08f, 0.08f, 0.12f, 0.88f), name: "Plate");
            UiKit.Stretch(plate.rectTransform);
            var top = UiKit.ShadowLabel(holder, "CASHED OUT", 120f, new Color32(0xF7, 0x25, 0x85, 255), new Color32(0x4C, 0xC9, 0xF0, 255), 10f);
            var trt = (RectTransform)top.transform.parent;
            trt.anchorMin = new Vector2(0f, 0.5f); trt.anchorMax = new Vector2(1f, 1f); trt.offsetMin = trt.offsetMax = Vector2.zero;
            var money = UiKit.ShadowLabel(holder, amount, 110f, new Color32(0x7C, 0xE5, 0x77, 255), new Color32(0x14, 0x14, 0x1F, 255), 8f);
            var mrt = (RectTransform)money.transform.parent;
            mrt.anchorMin = new Vector2(0f, 0.12f); mrt.anchorMax = new Vector2(1f, 0.55f); mrt.offsetMin = mrt.offsetMax = Vector2.zero;
            if (!string.IsNullOrEmpty(sub))
            {
                var s = UiKit.Label(holder, sub, 40f, Theme.Gold, TextAlignmentOptions.Center, true, "Sub");
                s.rectTransform.anchorMin = new Vector2(0f, -0.12f); s.rectTransform.anchorMax = new Vector2(1f, 0.12f);
                s.rectTransform.offsetMin = s.rectTransform.offsetMax = Vector2.zero;
            }
            StartCoroutine(FadeCash(canvas.gameObject.AddComponent<CanvasGroup>()));
        }

        private System.Collections.IEnumerator FadeCash(CanvasGroup g)
        {
            g.blocksRaycasts = false;
            float start = Time.unscaledTime;
            while (g != null && Time.unscaledTime - start < 2.8f)
            {
                float t = Time.unscaledTime - start;
                g.alpha = t < 0.15f ? t / 0.15f : t > 2.2f ? 1f - (t - 2.2f) / 0.6f : 1f;
                yield return null;
            }
            if (g != null) Destroy(g.gameObject);
        }

        private GameObject _confirm;

        /// <summary>A yes/no card over everything (LEAVE COURT during a live game).</summary>
        public void Confirm(string title, string body, string yes, Action onYes, Action onNo)
        {
            if (_confirm != null) Destroy(_confirm);
            var canvas = UiKit.CreateScreenCanvas("ConfirmCanvas", 60);
            _confirm = canvas.gameObject;
            var scrim = UiKit.Panel(canvas.transform, new Color(0.04f, 0.04f, 0.09f, 0.88f), name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(canvas.transform);
            var column = UiKit.Column(safe, 26f, null, "Confirm");
            UiKit.Band(column, 0.3f, 0.72f, 120f);
            column.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UiKit.Size(UiKit.ShadowLabel(column, title, 76f, Theme.Cream, Theme.Pink, 6f).transform.parent.GetComponent<RectTransform>(), 120f);
            UiKit.Size(UiKit.Label(column, body, 38f, Theme.Cream, TextAlignmentOptions.Center, false), 150f);
            UiKit.Button(column, yes, () => { CloseConfirm(); onYes?.Invoke(); }, ButtonStyle.Primary, 140f);
            UiKit.Button(column, "CANCEL", () => { CloseConfirm(); onNo?.Invoke(); }, ButtonStyle.Ghost, 120f, 44f);
        }

        public bool ConfirmOpen => _confirm != null;

        private void CloseConfirm()
        {
            if (_confirm != null) Destroy(_confirm);
            _confirm = null;
        }

        private void OnDestroy()
        {
            CloseConfirm();
            if (_cash != null) Destroy(_cash);
        }

        private GameObject _replayButton;
        private float _replayButtonUntil;
        private GameObject _replayOverlay;

        private void BuildReplayUi(RectTransform safe)
        {
            var b = UiKit.Button(safe, "REPLAY", () =>
            {
                _replayButton.SetActive(false);
                ReplayRequested?.Invoke();
            }, ButtonStyle.Secondary, 90f, 34f);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.66f);
            rt.sizeDelta = new Vector2(320f, 90f);
            _replayButton = b.gameObject;
            _replayButton.SetActive(false);

            // Letterbox bars + label shown while a replay plays.
            var overlay = UiKit.NewRect("ReplayOverlay", safe.parent);
            UiKit.Stretch(overlay);
            var top = UiKit.Panel(overlay, Color.black, name: "Top");
            top.rectTransform.anchorMin = new Vector2(0f, 0.9f);
            top.rectTransform.anchorMax = Vector2.one;
            top.rectTransform.offsetMin = top.rectTransform.offsetMax = Vector2.zero;
            var bottom = UiKit.Panel(overlay, Color.black, name: "Bottom");
            bottom.rectTransform.anchorMin = Vector2.zero;
            bottom.rectTransform.anchorMax = new Vector2(1f, 0.1f);
            bottom.rectTransform.offsetMin = bottom.rectTransform.offsetMax = Vector2.zero;
            var label = UiKit.Label(top.transform, "REPLAY", 48f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Stretch(label.rectTransform);
            var tapHint = UiKit.Label(bottom.transform, "TAP TO SKIP", 30f, Theme.Muted, TextAlignmentOptions.Center, true);
            UiKit.Stretch(tapHint.rectTransform);
            _replayOverlay = overlay.gameObject;
            _replayOverlay.SetActive(false);
        }

        /// <summary>Offers a REPLAY button for a few seconds after a big play.</summary>
        public void OfferReplay(float seconds)
        {
            _replayButton.SetActive(true);
            _replayButtonUntil = Time.unscaledTime + seconds;
        }

        public void ShowReplayOverlay(bool visible)
        {
            _replayOverlay.SetActive(visible);
            if (visible) _replayButton.SetActive(false);
            _finalPanel.SetActive(!visible && _finalShownOnce);
        }

        private bool _finalShownOnce;

        private void BuildCallMenu(RectTransform safe)
        {
            var panel = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "CallMenu");
            panel.raycastTarget = true;
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(460f, 760f);
            rt.anchoredPosition = new Vector2(-24f, 520f);
            var column = UiKit.Column(rt, 14f, new RectOffset(20, 20, 20, 20));
            UiKit.Stretch(column);
            UiKit.Size(UiKit.Label(column, "CALL A PLAY", 36f, Theme.Gold, TextAlignmentOptions.Center, true), 56f);
            UiKit.Button(column, "PICK & ROLL", () => Choose(PlayCall.PickAndRoll), ButtonStyle.Secondary, 100f, 36f);
            UiKit.Button(column, "GIVE & GO", () => Choose(PlayCall.GiveAndGo), ButtonStyle.Secondary, 100f, 36f);
            UiKit.Button(column, "CLEAR OUT", () => Choose(PlayCall.ClearOut), ButtonStyle.Secondary, 100f, 36f);
            UiKit.Button(column, "BACKDOOR", () => Choose(PlayCall.Backdoor), ButtonStyle.Secondary, 100f, 36f);
            UiKit.Button(column, "POST UP", () => Choose(PlayCall.PostUp), ButtonStyle.Secondary, 100f, 36f);
            UiKit.Button(column, "CANCEL", () => ShowCallMenu(false), ButtonStyle.Ghost, 80f, 30f);
            _callMenu = panel.gameObject;
            _callMenu.SetActive(false);
        }

        private void Choose(PlayCall play)
        {
            ShowCallMenu(false);
            PlayChosen?.Invoke(play);
        }

        public bool CallMenuOpen => _callMenu != null && _callMenu.activeSelf;

        public void ShowCallMenu(bool visible)
        {
            if (_callMenu != null) _callMenu.SetActive(visible);
        }

        /// <summary>Small line under the score bar (drill timer, BOX OUT, objective).</summary>
        public void SetInfo(string text)
        {
            text = text ?? "";
            if (text == _lastInfo) return;
            _lastInfo = text;
            _info.text = Loc.T(text);
        }

        private void BuildFinalPanel(RectTransform safe)
        {
            var scrim = UiKit.Panel(safe.parent, Theme.Scrim, name: "FinalScrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            _finalPanel = scrim.gameObject;
            var inner = UiKit.SafeArea(scrim.transform);
            var holder = UiKit.NewRect("FinalHolder", inner);
            UiKit.Band(holder, 0.04f, 0.96f, 40f);
            _finalColumn = UiKit.ScrollColumn(holder, 18f, new RectOffset(0, 0, 20, 20));
            _finalPanel.SetActive(false);
        }

        private void ClearFinal()
        {
            for (int i = _finalColumn.childCount - 1; i >= 0; i--) Destroy(_finalColumn.GetChild(i).gameObject);
        }

        private void Title(string title, string subtitle)
        {
            _finalTitle = UiKit.ShadowLabel(_finalColumn, title, 96f, Theme.Cream, Theme.Pink, 8f);
            UiKit.Size(_finalTitle.transform.parent.GetComponent<RectTransform>(), 140f);
            _finalScore = UiKit.Label(_finalColumn, subtitle, 52f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Size(_finalScore, 80f);
        }

        /// <summary>Simple end card (no stats).</summary>
        public void ShowFinal(string title, string scoreLine)
        {
            ClearFinal();
            Title(title, scoreLine);
            if (ShareResultRequested != null)
                UiKit.Button(_finalColumn, "SHARE RESULT", () => ShareResultRequested?.Invoke(), ButtonStyle.Ghost, 100f, 36f);
            if (SaveTapeRequested != null)
                UiKit.Button(_finalColumn, "SAVE GAME TAPE", () => SaveTapeRequested?.Invoke(), ButtonStyle.Secondary, 100f, 36f);
            UiKit.Button(_finalColumn, "REMATCH", () => RematchRequested?.Invoke(), ButtonStyle.Primary, 140f);
            LeaveCourtButton();
            UiKit.Button(_finalColumn, "HOME", () => QuitRequested?.Invoke(), ButtonStyle.Ghost, 120f, 48f);
            Open();
        }

        /// <summary>
        /// Post-game: score, box score (PTS AST REB STL BLK FG%), player of the game, rewards, and
        /// Rematch / Continue / Home. <paramref name="continueLabel"/> null hides Continue.
        /// </summary>
        public void ShowPostGame(string title, MatchSummary s, RewardGrant grant, bool rewarded, string note, string continueLabel, bool allowRematch)
        {
            ClearFinal();
            Title(title, s.teamAName.ToUpperInvariant() + "  " + s.scoreA + " - " + s.scoreB + "  " + s.teamBName.ToUpperInvariant());

            if (!string.IsNullOrEmpty(GradeLine))
                UiKit.Size(UiKit.Label(_finalColumn, GradeLine, 34f, Theme.Gold, TextAlignmentOptions.Center, true), 56f);

            var pog = s.Line(s.playerOfTheGame);
            if (pog != null)
                UiKit.Size(UiKit.Label(_finalColumn, "PLAYER OF THE GAME  <color=#FFD166>" + pog.name.ToUpperInvariant() + "</color>  " +
                                       pog.stats.points + " PTS", 34f, Theme.Cream, TextAlignmentOptions.Center, true), 56f);

            TeamComparison(s);
            ShotChart(s);
            if (!string.IsNullOrEmpty(FairPlayLine))
                UiKit.Size(UiKit.Label(_finalColumn, FairPlayLine, 26f, Theme.Muted, TextAlignmentOptions.Center, true), 84f);
            BoxScore(s, 0);
            BoxScore(s, 1);

            if (rewarded && (grant.signalPoints > 0 || grant.fans > 0))
            {
                var rewards = UiKit.Label(_finalColumn, "", 48f, Theme.Cyan, TextAlignmentOptions.Center, true);
                UiKit.Size(rewards, 76f);
                StartCoroutine(CountUp(rewards, grant.signalPoints, grant.fans));
            }
            if (!string.IsNullOrEmpty(note))
                UiKit.Size(UiKit.Label(_finalColumn, note, 36f, Theme.Gold, TextAlignmentOptions.Center, true), 110f);

            if (PlayOfTheGameRequested != null && HasPlayOfTheGame)
            {
                UiKit.Button(_finalColumn, "PLAY OF THE GAME", () => PlayOfTheGameRequested?.Invoke(), ButtonStyle.Secondary, 110f, 40f);
                if (HighlightsRequested != null && HighlightCount >= 2)
                    UiKit.Button(_finalColumn, Logic.Loc.T("HIGHLIGHTS") + " (" + HighlightCount + ")", () => HighlightsRequested?.Invoke(), ButtonStyle.Secondary, 100f, 36f);
                if (ShareRequested != null)
                    UiKit.Button(_finalColumn, "SHARE HIGHLIGHT", () => ShareRequested?.Invoke(), ButtonStyle.Ghost, 100f, 36f);
            }
            if (SaveTapeRequested != null)
                UiKit.Button(_finalColumn, "SAVE GAME TAPE", () => SaveTapeRequested?.Invoke(), ButtonStyle.Ghost, 100f, 36f);
            if (continueLabel != null)
                UiKit.Button(_finalColumn, continueLabel, () => ContinueRequested?.Invoke(), ButtonStyle.Primary, 140f);
            if (allowRematch)
                UiKit.Button(_finalColumn, "REMATCH", () => RematchRequested?.Invoke(), continueLabel == null ? ButtonStyle.Primary : ButtonStyle.Secondary, 120f, 48f);
            LeaveCourtButton();
            UiKit.Button(_finalColumn, "HOME", () => QuitRequested?.Invoke(), ButtonStyle.Ghost, 110f, 44f);
            Open();
        }

        /// <summary>TEAM STATS: each row shows both teams' numbers with a two-colour bar split by who did more.</summary>
        private void TeamComparison(MatchSummary s)
        {
            UiKit.Size(UiKit.Label(_finalColumn, "TEAM STATS", 34f, Theme.Gold, TextAlignmentOptions.Center, true), 50f);
            var colA = s.humanTeam == 0 ? Theme.Cyan : Theme.Pink;
            var colB = s.humanTeam == 1 ? Theme.Cyan : Theme.Pink;
            foreach (var row in s.Comparison())
            {
                var line = UiKit.NewRect("Stat " + row.label, _finalColumn);
                UiKit.Size(line, 46f);
                var a = UiKit.Label(line, row.textA, 28f, Theme.Cream, TextAlignmentOptions.Left, true);
                a.rectTransform.anchorMin = new Vector2(0f, 0f); a.rectTransform.anchorMax = new Vector2(0.18f, 1f);
                a.rectTransform.offsetMin = a.rectTransform.offsetMax = Vector2.zero;
                var b = UiKit.Label(line, row.textB, 28f, Theme.Cream, TextAlignmentOptions.Right, true);
                b.rectTransform.anchorMin = new Vector2(0.82f, 0f); b.rectTransform.anchorMax = new Vector2(1f, 1f);
                b.rectTransform.offsetMin = b.rectTransform.offsetMax = Vector2.zero;
                float total = row.a + row.b;
                float split = total <= 0f ? 0.5f : row.a / total;
                var barA = UiKit.Panel(line, colA, name: "BarA");
                barA.rectTransform.anchorMin = new Vector2(0.2f, 0.62f); barA.rectTransform.anchorMax = new Vector2(0.2f + 0.6f * split, 0.92f);
                barA.rectTransform.offsetMin = barA.rectTransform.offsetMax = Vector2.zero;
                var barB = UiKit.Panel(line, colB, name: "BarB");
                barB.rectTransform.anchorMin = new Vector2(0.2f + 0.6f * split, 0.62f); barB.rectTransform.anchorMax = new Vector2(0.8f, 0.92f);
                barB.rectTransform.offsetMin = barB.rectTransform.offsetMax = Vector2.zero;
                var label = UiKit.Label(line, row.label, 22f, Theme.Muted, TextAlignmentOptions.Center, true);
                label.rectTransform.anchorMin = new Vector2(0.2f, 0f); label.rectTransform.anchorMax = new Vector2(0.8f, 0.6f);
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            }
        }

        /// <summary>Phase 35: your shot chart (or your team's when nobody was yours to play).</summary>
        private void ShotChart(MatchSummary s)
        {
            var mine = s.HumanLine;
            bool own = mine?.stats?.chart != null && ShotCharts.Attempts(mine.stats.chart) > 0;
            var chart = own ? mine.stats.chart : s.TeamTotals(s.humanTeam).chart;
            if (ShotCharts.Attempts(chart) == 0) return;
            string team = (s.humanTeam == 0 ? s.teamAName : s.teamBName) ?? "";
            string share = (own ? "MY SHOT CHART" : team.ToUpperInvariant()) + "  " + s.scoreA + "-" + s.scoreB;
            ShotChartView.Build(_finalColumn, own ? "YOUR SHOT CHART" : team.ToUpperInvariant() + " SHOT CHART", chart, 300f, share);
        }

        private void BoxScore(MatchSummary s, int team)
        {
            UiKit.Size(UiKit.Label(_finalColumn, (team == 0 ? s.teamAName : s.teamBName).ToUpperInvariant(), 34f,
                                   team == s.humanTeam ? Theme.Gold : Theme.Cream, TextAlignmentOptions.Left, true), 50f);
            UiKit.Size(UiKit.Label(_finalColumn, Row("", "PTS", "AST", "REB", "STL", "BLK", "FG%"), 28f, Theme.Muted, TextAlignmentOptions.Left, true), 40f);
            foreach (var line in s.lines)
            {
                if (line.team != team) continue;
                var st = line.stats;
                string fg = st.fieldGoalsAttempted == 0 ? "-" : Mathf.RoundToInt(st.FieldGoalPercentage * 100f).ToString();
                string name = (line.isHuman ? "» " : "") + line.name.ToUpperInvariant();
                UiKit.Size(UiKit.Label(_finalColumn, Row(name, st.points.ToString(), st.assists.ToString(), st.rebounds.ToString(),
                                                         st.steals.ToString(), st.blocks.ToString(), fg), 30f,
                                       line.isHuman ? Theme.Cyan : Theme.Cream, TextAlignmentOptions.Left, false), 44f);
            }
        }

        // Fixed columns via TMP <pos> tags (percent of the line width).
        private static string Row(string name, string a, string b, string c, string d, string e, string f) =>
            name + "<pos=44%>" + a + "<pos=53%>" + b + "<pos=62%>" + c + "<pos=71%>" + d + "<pos=80%>" + e + "<pos=90%>" + f;

        /// <summary>Ticks the reward line up from zero (presentation only; the career is already saved).</summary>
        private System.Collections.IEnumerator CountUp(TextMeshProUGUI label, int sp, int fans)
        {
            const float seconds = 0.9f;
            float start = Time.unscaledTime;
            while (true)
            {
                float u = Mathf.Clamp01((Time.unscaledTime - start) / seconds);
                float e = 1f - (1f - u) * (1f - u);
                label.text = "+" + Mathf.RoundToInt(sp * e) + " SP    +" + Mathf.RoundToInt(fans * e) + " FANS";
                if (u >= 1f) yield break;
                yield return null;
            }
        }

        /// <summary>Practice end card.</summary>
        public void ShowPracticeEnd(string title, string result, bool newBest, string againLabel = "RUN IT BACK")
        {
            ClearFinal();
            Title(title, result);
            if (newBest) UiKit.Size(UiKit.Label(_finalColumn, "NEW PERSONAL BEST!", 48f, Theme.Cyan, TextAlignmentOptions.Center, true), 76f);
            UiKit.Button(_finalColumn, againLabel, () => RematchRequested?.Invoke(), ButtonStyle.Primary, 140f);
            LeaveCourtButton();
            UiKit.Button(_finalColumn, "HOME", () => QuitRequested?.Invoke(), ButtonStyle.Ghost, 110f, 44f);
            Open();
        }

        /// <summary>Set by the match controller when there's a highlight to show on the post-game card.</summary>
        public bool HasPlayOfTheGame { get; set; }
        /// <summary>I2 FAIR PLAY: expected vs real makes for both sides (null = hide).</summary>
        public string FairPlayLine { get; set; }
        /// <summary>I10: your letter grade and why (null = hide).</summary>
        public string GradeLine { get; set; }
        /// <summary>Phase 40: plays in the HIGHLIGHTS reel (the button shows when there are 2 or more).</summary>
        public int HighlightCount { get; set; }
        /// <summary>Phase 31: SAVE GAME TAPE on the end screens (two-phone, Live and watched games).</summary>
        public event System.Action SaveTapeRequested;

        private void Open()
        {
            _finalShownOnce = true;
            if (_replayButton != null) _replayButton.SetActive(false);
            ShowCallMenu(false);
            _pausePanel.SetActive(false);
            _finalPanel.SetActive(true);
        }

        public void ShowPause(bool visible) => _pausePanel.SetActive(visible);

        /// <summary>Hides the whole HUD (photo mode) without changing what's open.</summary>
        public void SetHudVisible(bool visible)
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = visible;
        }

        /// <summary>Phase 36: hides the commentary ticker now (the replay theater jumped past what it said).</summary>
        public void HideMic()
        {
            if (_micPanel != null) _micPanel.gameObject.SetActive(false);
        }

        /// <summary>Brief centre-screen callout ("STEAL!", "BALL!").</summary>
        public void Toast(string text, float seconds = 1.2f)
        {
            _toast.text = Loc.T(text);
            _toastUntil = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            LayoutBar();
            if (_replayButton != null && _replayButton.activeSelf && Time.unscaledTime > _replayButtonUntil) _replayButton.SetActive(false);
            Punch(_scoreA, _punchA);
            Punch(_scoreB, _punchB);
            if (_micPanel != null && _micPanel.gameObject.activeSelf && Time.unscaledTime > _micUntil) _micPanel.gameObject.SetActive(false);
            UpdateIntro();
        }

        private GameObject _intro;
        private float _introUntil;

        /// <summary>
        /// Tip-off card: where tonight's game is, both teams and their starters. It fades on its own after a
        /// few seconds or on any tap, and never stops the game (the ball is still being checked).
        /// </summary>
        public void ShowIntro(string courtName, TeamDef a, TeamDef b, System.Collections.Generic.List<string> startersA, System.Collections.Generic.List<string> startersB, string tape = null)
        {
            if (_intro != null) Destroy(_intro);
            var canvas = UiKit.CreateScreenCanvas("IntroCanvas", 25);
            _intro = canvas.gameObject;
            var scrim = UiKit.Panel(canvas.transform, new Color(0.04f, 0.04f, 0.09f, 0.82f), name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var tap = scrim.gameObject.AddComponent<UnityEngine.UI.Button>();
            tap.transition = UnityEngine.UI.Selectable.Transition.None;
            tap.onClick.AddListener(() => _introUntil = 0f);
            var safe = UiKit.SafeArea(canvas.transform);
            var col = UiKit.Column(safe, 10f, new RectOffset(40, 40, 0, 0));
            UiKit.Band(col, 0.2f, 0.85f, 40f);
            col.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UiKit.Size(UiKit.Label(col, "TONIGHT AT " + courtName.ToUpperInvariant(), 34f, Theme.Muted, TextAlignmentOptions.Center, true), 50f);
            UiKit.Size(UiKit.Label(col, a.FullName.ToUpperInvariant(), 56f, ToColor(a.primary), TextAlignmentOptions.Center, true), 76f);
            UiKit.Size(UiKit.Label(col, string.Join("  ·  ", startersA), 28f, Theme.Cream, TextAlignmentOptions.Center, false), 44f);
            UiKit.Size(UiKit.Label(col, "VS", 44f, Theme.Gold, TextAlignmentOptions.Center, true), 66f);
            UiKit.Size(UiKit.Label(col, b.FullName.ToUpperInvariant(), 56f, ToColor(b.primary), TextAlignmentOptions.Center, true), 76f);
            UiKit.Size(UiKit.Label(col, string.Join("  ·  ", startersB), 28f, Theme.Cream, TextAlignmentOptions.Center, false), 44f);
            // I4 TALE OF THE TAPE: your series with them, their danger man and the key to the game.
            if (!string.IsNullOrEmpty(tape))
                UiKit.Size(UiKit.Label(col, tape, 28f, Theme.Cyan, TextAlignmentOptions.Center, true), 130f);
            _introUntil = Time.unscaledTime + (string.IsNullOrEmpty(tape) ? 3.2f : 4.4f);
        }

        private static Color ToColor(RgbColor c)
        {
            // Very dark team colours stay readable on the dark card.
            var col = (Color)new Color32(c.r, c.g, c.b, 255);
            return c.Luminance < 0.18f ? Color.Lerp(col, Color.white, 0.55f) : col;
        }

        private void UpdateIntro()
        {
            if (_intro == null) return;
            float left = _introUntil - Time.unscaledTime;
            var group = _intro.GetComponent<CanvasGroup>() ?? _intro.AddComponent<CanvasGroup>();
            group.alpha = Mathf.Clamp01(left / 0.4f);
            if (left <= 0f)
            {
                Destroy(_intro);
                _intro = null;
            }
        }

        /// <summary>Shows a commentary line ("MIC: ...") for a few seconds under the score bar.</summary>
        public void Commentary(string speaker, string line)
        {
            if (_micPanel == null || string.IsNullOrEmpty(line)) return;
            _micText.text = "<color=#4CC9F0>" + speaker + ":</color> " + Loc.T(line);
            _micUntil = Time.unscaledTime + 3f;
            _micPanel.gameObject.SetActive(true);
        }

        /// <summary>Score bounce: pops to 140 % and settles back over 0.3 s.</summary>
        private static void Punch(TextMeshProUGUI label, float since)
        {
            float t = ReduceMotion ? 1f : (Time.unscaledTime - since) / PunchSeconds;
            float scale = t >= 1f || t < 0f ? 1f : 1f + 0.4f * (1f - t) * (1f - t);
            label.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private TextMeshProUGUI _myLine;
        private string _myLineText = "";

        /// <summary>I8: the controlled player's live line ("8 PTS · 4/7 FG · 2 AST"); null or empty hides it.</summary>
        public void SetMyLine(string text)
        {
            text = text ?? "";
            if (_myLine == null || text == _myLineText) return;
            _myLineText = text;
            _myLine.text = text;
        }

        public void Sync(MatchSimulation m)
        {
            if (m.Score[0] != _lastScoreA)
            {
                if (_lastScoreA >= 0) _punchA = Time.unscaledTime;
                _lastScoreA = m.Score[0];
                _scoreA.text = _lastScoreA.ToString();
            }
            if (m.Score[1] != _lastScoreB)
            {
                if (_lastScoreB >= 0) _punchB = Time.unscaledTime;
                _lastScoreB = m.Score[1];
                _scoreB.text = _lastScoreB.ToString();
            }

            int clock = Mathf.CeilToInt(m.GameClock);
            if (clock != _lastClock)
            {
                _lastClock = clock;
                _clock.text = m.Setup.Rules.useGameClock ? (clock / 60) + ":" + (clock % 60).ToString("00") : "--:--";
            }

            int shot = Mathf.CeilToInt(m.ShotClock);
            if (shot != _lastShot)
            {
                _lastShot = shot;
                _shotClock.text = m.Setup.Rules.shotClockSeconds >= 99f ? "" : shot.ToString();
            }

            if (m.OffenseTeam != _lastOffense)
            {
                _lastOffense = m.OffenseTeam;
                _teamA.color = m.OffenseTeam == 0 ? Theme.Gold : Theme.Cream;
                _teamB.color = m.OffenseTeam == 1 ? Theme.Gold : Theme.Cream;
                _teamA.text = (m.OffenseTeam == 0 ? "• " : "") + m.Setup.TeamA.abbreviation;
                _teamB.text = (m.OffenseTeam == 1 ? "• " : "") + m.Setup.TeamB.abbreviation;
            }

            if (_toast.text.Length > 0 && Time.unscaledTime > _toastUntil) _toast.text = "";
        }

        // ------------------------------------------------------------------ 14 Oct 2026: quick chat (LIVE / two phones)

        private GameObject _chatPanel;
        private TextMeshProUGUI _chatBubble;
        private float _chatBubbleUntil;

        /// <summary>Adds a CHAT button under the pause button: preset messages to the other player.</summary>
        public void EnableQuickChat(Action<int> send)
        {
            if (_bar == null || _chatPanel != null) return;
            var safe = (RectTransform)_bar.parent;
            var chat = UiKit.Button(safe, "CHAT", () => _chatPanel.SetActive(!_chatPanel.activeSelf), ButtonStyle.Ghost, 80f, 30f);
            var crt = (RectTransform)chat.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.sizeDelta = new Vector2(170f, 84f);
            crt.anchoredPosition = new Vector2(28f, -176f);

            var panel = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "QuickChat");
            panel.raycastTarget = true;
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0f, 1f);
            prt.sizeDelta = new Vector2(760f, 470f);
            prt.anchoredPosition = new Vector2(28f, -270f);
            var grid = panel.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.padding = new RectOffset(20, 20, 20, 20);
            grid.cellSize = new Vector2(350f, 92f);
            grid.spacing = new Vector2(16f, 16f);
            for (int i = 0; i < QuickChat.Messages.Length; i++)
            {
                int id = i;
                UiKit.Button(panel.transform, QuickChat.Messages[i], () => { _chatPanel.SetActive(false); send?.Invoke(id); }, ButtonStyle.Secondary, 92f, 26f);
            }
            _chatPanel = panel.gameObject;
            _chatPanel.SetActive(false);

            _chatBubble = UiKit.Label(safe, "", 34f, Theme.Cream, TextAlignmentOptions.Right, true, "ChatBubble");
            var brt = _chatBubble.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(1f, 1f);
            brt.sizeDelta = new Vector2(820f, 70f);
            brt.anchoredPosition = new Vector2(-28f, -180f);
            UiKit.ApplyTextShadow(_chatBubble);
        }

        /// <summary>Shows a chat line under the score bar for a few seconds.</summary>
        public void ShowChat(string line)
        {
            if (_chatBubble == null) return;
            _chatBubble.text = line;
            _chatBubbleUntil = Time.unscaledTime + 3f;
            _chatBubble.gameObject.SetActive(true);
        }

        /// <summary>Hides the chat line when its time is up (call each frame).</summary>
        public void TickChat()
        {
            if (_chatBubble != null && _chatBubble.gameObject.activeSelf && Time.unscaledTime > _chatBubbleUntil)
                _chatBubble.gameObject.SetActive(false);
        }
    }
}
