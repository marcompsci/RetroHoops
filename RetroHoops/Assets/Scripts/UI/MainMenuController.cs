using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>Main menu: career strip, title, five mode buttons, Quick Call and Practice pickers.</summary>
    public sealed partial class MainMenuController : ScreenBase
    {
        protected override string BackdropCourtId => "court.sunset_cage";
        protected override uint BackdropSeed => 7;
        protected override float ScrimAlpha => 0.15f;

        private GameObject _overlay;
        private Texture2D _logoTex;
        private RectTransform _logo;
        private Vector2 _logoBase;
        private float _idleSince;
        /// <summary>Seconds on the title screen with no input before the attract-mode demo starts.</summary>
        private const float AttractAfter = 30f;
        private static bool _tutorialOffered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _tutorialOffered = false;

        /// <summary>Starts the how-to-play tutorial (also reachable from Settings).</summary>
        /// <summary>
        /// 15 Oct 2026: first launch ► CREATE YOUR PLAYER, step by step (you, look, outfit, style of play). The same
        /// player is your Rise player; SKIP keeps the default and Rise ► CREATE MY PLAYER is always there.
        /// </summary>
        private static void OpenOnboardingMaker(System.Action then)
        {
            var c = App.Catalog;
            PlayerMaker.Open(RiseCreate.MySpec(App.Career, c), new PlayerMaker.Options
            {
                Title = "CREATE YOUR PLAYER",
                Mine = true,
                Outfit = true,
                Wizard = true,
                SkipLabel = "SKIP FOR NOW",
                SaveLabel = "CREATE PLAYER",
                Info = spec =>
                {
                    var arch = c.Archetypes.Find(a => (int)a.archetype == spec.Archetype) ?? c.Archetypes[0];
                    return Loc.T("STARTING OVR") + " " + arch.baseline.Offset(PlayerCreator.RatingOffset).Overall + "  ·  " + Loc.T("you'll play as him in RISE MODE");
                },
                OnSave = spec =>
                {
                    RiseCreate.ApplyMine(App.Career, App.Catalog, spec);
                    App.SaveCareer();
                    then?.Invoke();
                },
                OnClose = () => then?.Invoke(),
            });
        }

        public static void StartTutorial()
        {
            var request = MatchRequest.PracticeDefault();
            request.Mode = GameMode.Tutorial;
            App.PendingMatch = request;
            SceneFlow.GoTo(SceneNames.Game);
        }

        private void OnDestroy()
        {
            if (_logoTex != null) Destroy(_logoTex);
        }

        protected override void Build()
        {
            App.BackToCity = false; // back at the menu: screens opened from here come back here
            var career = App.Career;
            var strip = UiKit.Label(Body, career.nickname.ToUpperInvariant() + "   <color=#FFD166>" + career.signalPoints + " SP</color>   <color=#4CC9F0>" + career.fans + " FANS</color>",
                                    32f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Band(strip.rectTransform, 0.965f, 1f, 24f);

            // Pixel-art "RETRO HOOPS" logo (drawn in code), scaled with crisp pixels.
            var logoTex = _logoTex = Utilities.TextureFactory.ToTexture(Logic.PixelArt.TitleLogoGenerator.Generate(), "ui.title.logo");
            var holder = UiKit.NewRect("TitleLogo", Body);
            UiKit.Band(holder, 0.75f, 0.955f, 32f);
            var logo = UiKit.Picture(holder, logoTex, "Logo");
            UiKit.Stretch(logo.rectTransform);
            _logo = holder;
            _logoBase = holder.anchoredPosition;
            _idleSince = Time.unscaledTime;
            var fit = logo.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = logoTex.width / (float)logoTex.height;

            var column = UiKit.Column(Body, 20f, null, "Modes");
            UiKit.Band(column, 0.21f, 0.708f, 110f);
            column.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            // 16 Oct 2026 (Omari): BALLER CITY, right next to PLAY.
            var playRow = UiKit.Row(column, 20f, "PlayCity");
            UiKit.Size(playRow, 150f);
            UiKit.Button(playRow, "PLAY", ShowPlayMenu, ButtonStyle.Primary, 150f, 64f);
            UiKit.Button(playRow, "BALLER CITY", () => { App.BackToCity = false; SceneFlow.GoTo(SceneNames.City); }, ButtonStyle.Primary, 150f, 56f);

            // 15 Oct 2026 (Omari): THE STREETS, up top next to RISE MODE.
            var topRow = UiKit.Row(column, 20f, "StreetsRise");
            UiKit.Size(topRow, 124f);
            UiKit.Button(topRow, "THE STREETS", ShowStreets, ButtonStyle.Primary, 124f, 50f);
            UiKit.Button(topRow, "RISE MODE", () => SceneFlow.GoTo(SceneNames.Season), ButtonStyle.Secondary, 124f, 50f);
            // In season (October, December, Easter week, early July): a shortcut to that Holiday Game.
            var season = Holidays.InSeason(System.DateTime.Now);
            if (season != HolidayTheme.None)
                UiKit.Button(column, Holidays.Get(season).Name, () => ShowHolidays(season), ButtonStyle.Secondary, 110f, 44f);
            UiKit.Button(column, "PRACTICE LAB", ShowPractice, ButtonStyle.Secondary, 120f, 52f);
            UiKit.Button(column, "LOCKER ROOM", () => SceneFlow.GoTo(SceneNames.LockerRoom), ButtonStyle.Secondary, 120f, 52f);
            // 14 Oct 2026: SETTINGS and your player's PHONE (music, texts, feed) side by side.
            var lowRow = UiKit.Row(column, 20f, "SettingsPhone");
            UiKit.Size(lowRow, 104f);
            UiKit.Button(lowRow, "SETTINGS", () => SceneFlow.GoTo(SceneNames.Settings), ButtonStyle.Ghost, 104f, 46f);
            var phoneButton = UiKit.Button(lowRow, PhoneScreen.ButtonLabel(), () => PhoneScreen.Open(), ButtonStyle.Ghost, 104f, 46f);
            var phoneLabel = phoneButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            PhoneScreen.Closed += () => { if (phoneLabel != null) phoneLabel.text = Loc.T(PhoneScreen.ButtonLabel()); };
            PhoneMusic.Wake();
            // TestFlight copies only: BETA FEEDBACK (the report goes out through TestFlight's own feedback).
            if (Beta.IsTestFlight) UiKit.Button(column, "SEND FEEDBACK", BetaFeedbackScreen.SendFeedback, ButtonStyle.Ghost, 104f, 46f);

            BuildLogoStrip();
            BuildNextStep();

            // Phase 37: the game has an optional purchase (Retro Hoops Pro since Phase 41), so the footer only says "no ads" (App Review 2.3.1).
            var footer = UiKit.Label(Body, "v" + App.VersionAndBuild + "  ·  " + Loc.T("plays offline") + "  ·  " + Loc.T("no ads"), 28f, Theme.Muted);
            UiKit.Band(footer.rectTransform, 0.005f, 0.045f, 24f);

            // Phase 37: an App Store screenshot launch opens its screen and skips the first-launch dialogs.
            if (RunStoreShot()) return;

            if (App.OpenProOnMenu)
            {
                App.OpenProOnMenu = false;
                ShowPro(ShowPlayMenu);
            }
            else if (App.OpenClassicOnMenu)
            {
                App.OpenClassicOnMenu = false;
                ShowClassic();
            }
            else if (App.OpenKingOnMenu)
            {
                App.OpenKingOnMenu = false;
                ShowKing();
            }
            else if (App.OpenArcadeOnMenu)
            {
                App.OpenArcadeOnMenu = false;
                ShowArcade();
            }
            else if (App.OpenCupOnMenu)
            {
                App.OpenCupOnMenu = false;
                ShowCup();
            }
            else if (App.OpenFranchiseOnMenu)
            {
                App.OpenFranchiseOnMenu = false;
                FranchiseScreen.Open();
            }
            else if (App.OpenStoryOnMenu)
            {
                App.OpenStoryOnMenu = false;
                ShowStoryAfterGame();
            }
            else if (App.OpenLiveOnMenu)
            {
                App.OpenLiveOnMenu = false;
                ShowLive();
            }
            else if (App.OpenCouchOnMenu)
            {
                App.OpenCouchOnMenu = false;
                ShowCouchCup();
            }
            else if (App.OpenGauntletOnMenu)
            {
                App.OpenGauntletOnMenu = false;
                ShowGauntlet();
            }
            else if (App.OpenClutchOnMenu)
            {
                App.OpenClutchOnMenu = false;
                ShowClutch();
            }
            else if (App.OpenStreetsOnMenu)
            {
                App.OpenStreetsOnMenu = false;
                ShowStreets();
            }
            else if (App.OpenParkOnMenu)
            {
                App.OpenParkOnMenu = false;
                ShowPark();
            }
            else if (App.OpenCustomCupOnMenu)
            {
                App.OpenCustomCupOnMenu = false;
                ShowTournamentBuilder();
            }
            else if (App.OpenLegacyOnMenu)
            {
                App.OpenLegacyOnMenu = false;
                LegacyScreen.Open();
            }
            else if (App.OpenAllStar)
            {
                App.OpenAllStar = false;
                AllStarScreen.Open(false);
            }

            // TestFlight: the tester profile once, before anything else.
            if (Beta.IsTestFlight && !App.Career.beta.asked && _overlay == null)
            {
                BetaFeedbackScreen.OpenProfile();
                return;
            }

            // First launch: Coach Dee says hello, then the tutorial is offered (once per session until it's done).
            if (!App.Career.tutorialDone && App.Career.totals.games == 0 && !_tutorialOffered && _overlay == null)
            {
                _tutorialOffered = true;
                // Phase 39: JUST PLAY drops you straight into a winnable first game.
                void Offer() => UiControls.Dialog("NEW TO RETRO HOOPS?", "Learn the controls in about two minutes: move, shoot, pass, call plays, and defend. Or jump straight into a game.",
                                                  ("PLAY TUTORIAL", ButtonStyle.Primary, StartTutorial),
                                                  ("JUST PLAY", ButtonStyle.Secondary, StartFirstGame),
                                                  ("MAYBE LATER", ButtonStyle.Ghost, null));
                // 15 Oct 2026 (Omari): a new player creates their Rise player (look, outfit, style) before the tutorial.
                void Create()
                {
                    if (!Onboarding.OfferCreatePlayer(App.Career)) { Offer(); return; }
                    Tours.MarkSeen(App.Career, Tours.MakePlayer);
                    App.SaveCareer();
                    OpenOnboardingMaker(Offer);
                }
                if (!App.Career.storySeen.Contains(Story.Welcome)) StoryView.Show(Story.Beat(Story.Welcome, App.Career.nickname), Create);
                else Create();
            }

            if (App.CareerFromCloud)
            {
                App.CareerFromCloud = false;
                UiControls.Dialog("WELCOME BACK", "Your career was loaded from iCloud.", ("OK", ButtonStyle.Primary, null));
            }
            else if (CloudSync.CloudAhead && _overlay == null)
            {
                string from = string.IsNullOrEmpty(CloudSync.CloudDevice) ? "another device" : CloudSync.CloudDevice;
                UiControls.Dialog("NEWER CAREER IN ICLOUD",
                    "iCloud has a career that's further along (saved on " + from + "). Load it on this iPhone, or keep this one and replace the iCloud copy?",
                    ("LOAD FROM ICLOUD", ButtonStyle.Primary, () =>
                    {
                        CloudSync.LoadCloud();
                        SceneFlow.GoTo(SceneNames.MainMenu);
                    }),
                    ("KEEP THIS ONE", ButtonStyle.Ghost, CloudSync.KeepLocal));
            }

            // Phase 31: one "what's new" card for careers from before the update.
            Tours.SkipWhatsNewForNewPlayer(App.Career);
            if (Tours.ShowWhatsNew(App.Career) && _overlay == null && !CloudSync.CloudAhead && !App.CareerFromCloud) FirstVisit(Tours.WhatsNew);

            if (SaveStore.LastVerdict == SaveVerdict.Tampered && !_editNoticeShown)
            {
                _editNoticeShown = true;
                UiControls.Dialog("SAVE FILE CHANGED", "Your save file was changed outside Retro Hoops. Your career is still here (with its numbers checked), but its scores won't go to Game Center leaderboards any more. A copy of the changed file was kept.",
                                  ("OK", ButtonStyle.Primary, null));
            }

            // Phase 39: the first open of each day pays a small welcome-back bonus (not on the very first launch).
            if ((App.Career.totals?.games ?? 0) > 0 && _overlay == null)
            {
                int bonus = Onboarding.ClaimLogin(App.Career, App.Today);
                if (bonus > 0)
                {
                    App.SaveCareer();
                    UiControls.Dialog("WELCOME BACK", Loc.T("Day") + " " + App.Career.loginStreak + " " + Loc.T("in a row") + ":  +" + bonus + " SP"
                                      + (App.Career.loginStreak % Onboarding.LoginMaxDay == 0 ? "\n" + Loc.T("A full week! Bonus included.") : ""),
                                      ("NICE", ButtonStyle.Primary, null));
                }
            }

            if (App.CareerLoadStatus == LoadStatus.Recovered)
                UiControls.Dialog("SAVE RESET", "Your save file couldn't be read, so a fresh career was started. A backup of the old file was kept.",
                                  ("OK", ButtonStyle.Primary, null));
        }

        private static bool _editNoticeShown;

        /// <summary>Shows a mode's "first time here" card once (Phase 31).</summary>
        private static void FirstVisit(TourCard card)
        {
            if (App.Career == null || Tours.Seen(App.Career, card)) return;
            Tours.MarkSeen(App.Career, card);
            App.SaveCareer();
            UiControls.Dialog(card.Title, card.Body, ("GOT IT", ButtonStyle.Primary, null));
        }

        protected override void Update()
        {
            base.Update();
            StoreShotTick();
            PollLiveInvites();
            float now = Time.unscaledTime;
            // The title logo bobs in two-pixel steps, like an old cartridge title screen.
            if (_logo != null && App.Career != null && !App.Career.settings.reduceMotion)
                _logo.anchoredPosition = _logoBase + new Vector2(0f, Mathf.Round(Mathf.Sin(now * 2.2f) * 2f) * 4f);

            if (AnyInput()) _idleSince = now;
            bool idle = _overlay == null && GameObject.Find("DialogCanvas") == null && !SceneFlow.IsTransitioning;
            if (App.Career != null && App.Career.settings.attractMode && !StoreShots.Active && idle && now - _idleSince > AttractAfter)
            {
                _idleSince = now + 999f;
                StartDemo();
            }
        }

        private static bool AnyInput()
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer != null && (pointer.press.isPressed || pointer.delta.ReadValue().sqrMagnitude > 0.5f)) return true;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.anyKey.isPressed) return true;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.05f || pad.buttonSouth.isPressed || pad.startButton.isPressed)) return true;
#endif
            return false;
        }

        /// <summary>Attract mode: two random league teams play an AI-only demo game.</summary>
        private static void StartDemo()
        {
            var c = App.Catalog;
            var league = c.TeamsInTier(TeamTier.League);
            if (league.Count < 2) return;
            var rng = new SeededRandom((uint)System.Environment.TickCount | 1u);
            int a = rng.Range(0, league.Count);
            int b = (a + 1 + rng.Range(0, league.Count - 1)) % league.Count;
            App.PendingMatch = new MatchRequest
            {
                Mode = GameMode.Demo,
                HomeTeamId = league[a].id,
                AwayTeamId = league[b].id,
                CourtId = league[a].homeCourtId,
                RulesId = "rules.demo",
                DifficultyId = "difficulty.legend",
            };
            SceneFlow.GoTo(SceneNames.Game);
        }

        private void BuildLogoStrip()
        {
            var caption = UiKit.Label(Body, DefaultContent.LeagueName.ToUpperInvariant(), 30f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Band(caption.rectTransform, 0.165f, 0.2f, 24f);

            var row = UiKit.Row(Body, 14f, "LeagueLogos");
            UiKit.Band(row, 0.08f, 0.16f, 40f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            foreach (var team in App.Catalog.TeamsInTier(TeamTier.League))
            {
                var logo = UiKit.Picture(row, TextureFactory.TeamLogo(team), "Logo " + team.abbreviation);
                UiKit.Size(logo, 104f, 104f);
            }
        }

        // ------------------------------------------------------------------ overlays

        private RectTransform OpenOverlay(string title) => OpenOverlay(title, out _);

        /// <summary>
        /// Opens a modal panel. <paramref name="footer"/> is a bar pinned to the bottom of the panel
        /// for the main buttons, so they stay visible on any screen shape; everything else scrolls.
        /// </summary>
        private RectTransform OpenOverlay(string title, out RectTransform footer)
        {
            CloseOverlay();
            var canvas = UiKit.CreateScreenCanvas("Overlay", 30);
            _overlay = canvas.gameObject;
            var scrim = UiKit.Panel(canvas.transform, Theme.Scrim, name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(canvas.transform);
            var panel = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "Panel");
            UiKit.Band(panel.rectTransform, 0.04f, 0.96f, 48f);
            panel.raycastTarget = true;
            panel.gameObject.AddComponent<OverlayPop>();

            const float footerHeight = 170f;
            footer = UiKit.Row(panel.transform, 20f, "Footer");
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(-80f, footerHeight - 30f);
            footer.anchoredPosition = new Vector2(0f, 24f);

            var body = UiKit.NewRect("Body", panel.transform);
            UiKit.Stretch(body);
            body.offsetMin = new Vector2(0f, footerHeight);
            var column = UiKit.ScrollColumn(body, 20f, new RectOffset(40, 40, 30, 20));
            UiKit.Size(UiKit.ShadowLabel(column, title, 64f, Theme.Cream, Theme.Pink, 6f).transform.parent.GetComponent<RectTransform>(), 100f);
            return column;
        }

        private void CloseOverlay()
        {
            if (_overlay != null) Destroy(_overlay);
            _overlay = null;
        }

        protected override void OnBack()
        {
            if (_overlay != null) CloseOverlay();
        }

        private void ShowQuickCall()
        {
            var c = App.Catalog;
            var column = OpenOverlay("QUICK CALL", out var footer);
            var league = Secrets.OpponentTeams(c, App.Career.secrets);
            var mine = Secrets.PlayableTeams(c, App.Career.secrets);
            int myIndex = 0;
            int courtChoice = 0; // 0 = home court, then any unlocked hidden courts
            int oppIndex = 0;
            TextMeshProUGUI oppLabel = null; // assigned below; declared first so Refresh() can see it
            var opponents = new List<TeamDef>();

            UiKit.Size(UiKit.Label(column, "PICK YOUR TEAM", 36f, Theme.Gold, TextAlignmentOptions.Center, true), 60f);
            var teamRow = UiKit.Row(column, 20f, "Teams");
            UiKit.Size(teamRow, 250f);
            var cards = new List<Image>();
            for (int i = 0; i < mine.Count; i++)
            {
                int index = i;
                var team = mine[i];
                var card = UiKit.Button(teamRow, "", () => { myIndex = index; Refresh(); }, ButtonStyle.Secondary, 280f);
                cards.Add(card.GetComponent<Image>());
                var logo = UiKit.Picture(card.transform, TextureFactory.TeamLogo(team));
                UiKit.Place(logo.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(150f, 150f));
                var name = UiKit.Label(card.transform, team.FullName.ToUpperInvariant(), 30f, Theme.Cream, TextAlignmentOptions.Center, true);
                UiKit.Place(name.rectTransform, new Vector2(0.5f, 0.15f), new Vector2(360f, 60f));
            }

            oppLabel = UiKit.Label(column, "", 40f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(oppLabel, 70f);
            UiKit.Button(column, "CHANGE OPPONENT", () => { oppIndex = (oppIndex + 1) % opponents.Count; Refresh(); }, ButtonStyle.Ghost, 100f, 36f);

            // Play anywhere: home court, any street court, and hidden courts once unlocked.
            var courts = new List<string> { null };
            foreach (var court in c.Courts) if (court.circuit == CourtCircuit.Custom) courts.Add(court.id);
            foreach (var court in c.Courts) if (court.circuit == CourtCircuit.Blacktop || court.circuit == CourtCircuit.Holiday) courts.Add(court.id);
            foreach (var id in new[] { DefaultContent.SecretCourtId, DefaultContent.BossCourtId })
                if (Secrets.IsUnlocked(App.Career.secrets, id)) courts.Add(id);
            {
                var courtNames = courts.ConvertAll(id => id == null ? "HOME" : c.Court(id).displayName.ToUpperInvariant()).ToArray();
                UiControls.ChoiceRow(column, "COURT", courtNames, 0, i => courtChoice = i);
            }

            var difficulties = CityCrews.Pickable(c);
            int diffIndex = Mathf.Max(0, difficulties.FindIndex(d => d.id == App.Career.settings.difficultyId));
            var names = difficulties.ConvertAll(d => d.displayName.ToUpperInvariant()).ToArray();
            UiControls.ChoiceRow(column, "DIFFICULTY", names, diffIndex, i => diffIndex = i);

            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
            UiKit.Button(footer, "TIP OFF", () =>
            {
                var home = mine[myIndex];
                var away = opponents[oppIndex];
                App.Career.settings.difficultyId = difficulties[diffIndex].id;
                App.SaveCareer();
                App.PendingMatch = new MatchRequest
                {
                    Mode = GameMode.QuickCall,
                    HomeTeamId = home.id,
                    AwayTeamId = away.id,
                    CourtId = courts[courtChoice] ?? home.homeCourtId,
                    DifficultyId = difficulties[diffIndex].id,
                };
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Primary, 130f);
            UiKit.Size(UiKit.Label(column, "Touch: stick + SHOOT / PASS / DUNK / LAYUP / CALL\nKeyboard: WASD move · K shoot (hold) · J pass · U dunk · I layup · L steal · C call",
                                   28f, Theme.Muted), 90f);

            void Refresh()
            {
                for (int i = 0; i < cards.Count; i++) cards[i].color = i == myIndex ? Color.white : new Color(0.55f, 0.55f, 0.6f, 1f);
                opponents.Clear();
                foreach (var t in league) if (t.id != mine[myIndex].id) opponents.Add(t);
                oppIndex %= opponents.Count;
                oppLabel.text = "VS  " + opponents[oppIndex].FullName.ToUpperInvariant();
            }
            Refresh();
        }

        /// <summary>PLAY: every way to start a game.</summary>
        private void ShowPlayMenu()
        {
            _streetsStyle = false;
            var column = OpenOverlay("PLAY", out var footer);
            var career = App.Career;

            Section(column, "STORY");
            var story = career.story ?? new StorySaveData();
            Mode(column, "SUMMER STORY", story.finished ? "Sunburst champions. Replay any chapter."
                 : "Chapter " + System.Math.Min(StoryMode.Chapters, story.cleared + 1) + " of " + StoryMode.Chapters + ": " + StoryMode.Chapter(System.Math.Min(StoryMode.Chapters, story.cleared + 1)).Title.ToLowerInvariant()
                   + ". Nova, Big Sal, Mic Tally and the Velvet Hour.", ShowStoryMode, ButtonStyle.Primary);

            Section(column, "ONLINE");
            ProStore.Remember(); // settle the cached Pro answer (and grant its pass items the first time)
            Mode(column, "LIVE", Loc.T("Play people online, free. Rating") + " " + (career.live?.rating ?? LiveMode.StartRating) + "  ·  " + LiveMode.Tier(career.live?.rating ?? LiveMode.StartRating), ShowLive, ButtonStyle.Secondary);
            Mode(column, "RETRO HOOPS PRO", ProStore.Owned ? Loc.T("Unlocked. Thanks for supporting Retro Hoops!")
                : Loc.T("One-time unlock: FRANCHISE, Rise seasons 2+ and every Hoops Pass item.") + " " + ProStore.Price, () => ShowPro(ShowPlayMenu), ButtonStyle.Ghost);

            Section(column, "THE STREETS");
            Mode(column, "THE STREETS", "3 on 3 street ball to 21. Tricks, combos and SHOWSTOPPERS. Record " + (career.streets?.wins ?? 0) + "-" + (career.streets?.losses ?? 0) + ".",
                 ShowStreets, ButtonStyle.Primary);

            Section(column, "PLAY NOW");
            Mode(column, "QUICK CALL", "Pick a team and an opponent. One game. Score and it's still your ball.", ShowQuickCall, ButtonStyle.Primary);
            Mode(column, "FULL COURT", "5 on 5, both baskets, 2s and 3s. Four minutes.", ShowFullCourt, ButtonStyle.Secondary);
            Mode(column, "THE PARK", "Call out street legends, 1-on-1 to 4-on-4. Break ankles. Rep: " + Street.RepNames[Street.RepLevel(career.street.rep)], ShowPark, ButtonStyle.Secondary);
            Mode(column, "1-ON-1", "Just you and their best. First to 11.", ShowOneOnOne, ButtonStyle.Secondary);
            Mode(column, "CLUTCH", Loc.T("Late-game situations: the clock is running and the score is set. Stars:") + " "
                 + Clutch.TotalStars(career.clutch) + " / " + Clutch.MaxStars, ShowClutch, ButtonStyle.Secondary);
            Mode(column, "HOLIDAY GAMES", "Christmas, Halloween, Easter and Fourth of July courts.", () => ShowHolidays(), ButtonStyle.Secondary);

            Section(column, "CAREERS");
            var lg = career.legacy;
            Mode(column, "LEGACY", lg != null && lg.active ? Legacy.StageName(lg).ToLowerInvariant() + "  ·  age " + lg.age
                : "Your player's career: high school, college, the draft, the pros, the Hall of Fame.", () =>
            {
                CloseOverlay();
                LegacyScreen.Open();
            }, ButtonStyle.Secondary);
            var fr = career.franchise;
            // 8 Oct: FRANCHISE comes with Retro Hoops Pro; without it the row opens the Pro screen.
            bool frLocked = Pro.FranchiseLocked(ProStore.Owned);
            Mode(column, frLocked ? "FRANCHISE  ·  PRO" : "FRANCHISE", frLocked
                ? Loc.T("Be the GM: trades, free agency, the draft. Comes with Retro Hoops Pro.")
                : fr != null && fr.active
                ? "Year " + fr.year + "  ·  " + Franchise.PhaseName(fr.phase).ToLowerInvariant() + "  ·  titles " + fr.titles
                : "Be the GM: trades, free agency, the draft, season after season.", () =>
            {
                if (frLocked) { ShowPro(ShowPlayMenu); return; }
                CloseOverlay();
                FranchiseScreen.Open();
            }, ButtonStyle.Secondary);

            Section(column, "EVENTS");
            var today = DailyChallenges.For(App.Today, App.Catalog);
            bool done = DailyChallenges.CompletedToday(career.daily, App.Today);
            int streak = DailyChallenges.LiveStreak(career.daily, App.Today);
            Mode(column, "DAILY CHALLENGE", (done ? "Done for today √" : today.Describe()) + "  ·  streak " + streak, ShowDaily, ButtonStyle.Secondary);
            var g = career.gauntlet ?? (career.gauntlet = new GauntletSaveData());
            Mode(column, "SKILLS GAUNTLET", "4 drills back to back, new every day  ·  best " + g.best, ShowGauntlet, ButtonStyle.Secondary);
            career.weekly = career.weekly ?? new WeeklySaveData();
            Weekly.Sync(career.weekly, App.Today);
            int weeklyDone = career.weekly.done.FindAll(x => x).Count;
            Mode(column, "WEEKLY & HOOPS PASS", weeklyDone + "/" + Weekly.Goals + " weekly goals  ·  pass tier "
                 + (career.pass != null && career.pass.season == HoopsPass.SeasonOf(App.Today) ? HoopsPass.Tier(career.pass.xp) : 0) + "/" + HoopsPass.Tiers
                 + "  ·  free gear", ShowWeekly, ButtonStyle.Secondary);
            Mode(column, "ALL-STAR CONTESTS", "Dunk Contest, 3-Point Contest and the All-Star Game.", () =>
            {
                CloseOverlay();
                AllStarScreen.Open(false);
            }, ButtonStyle.Secondary);
            var arcade = career.secrets.arcade;
            Mode(column, "ARCADE LADDER", arcade.active
                ? "Stage " + (arcade.rung + 1) + " of " + ArcadeEngine.Rungs + "  ·  continues " + arcade.continues
                : "Six stages, three continues, one secret boss. Clears: " + arcade.clears, ShowArcade, ButtonStyle.Secondary);
            Mode(column, "KING OF THE COURT", "Beat league teams back to back until you lose. Best streak: " + career.king.best, ShowKing, ButtonStyle.Secondary);
            var cup = career.cup;
            Mode(column, "CALLER CUP", cup.Active ? "In progress  ·  titles " + cup.titles : "Eight-team knockout. Titles: " + cup.titles, ShowCup, ButtonStyle.Secondary);
            Mode(column, "FIRST CALL CLASSIC", "Four-team knockout. Titles won: " + career.classic.titles, ShowClassic, ButtonStyle.Secondary);
            Mode(column, "TOURNAMENT BUILDER", career.customCup.Active ? career.customCup.name + " in progress" : "Build a bracket: 4, 8 or 16 teams, 2-on-2 to Full Court.",
                 ShowTournamentBuilder, ButtonStyle.Secondary);

            Section(column, "WITH FRIENDS");
            Mode(column, "2 PLAYER", "Head to head on one iPhone, on two phones nearby, or a Couch Cup tournament for up to 8 friends.", ShowVersus, ButtonStyle.Secondary);
            Mode(column, "PARTY GAMES", "H-O-R-S-E, 21, Around the World, and the Shootout.", ShowParty, ButtonStyle.Secondary);
            Mode(column, "HOW TO PLAY", "Two-minute guided tutorial.", StartTutorial, ButtonStyle.Ghost);
            UiKit.Button(footer, "BACK", CloseOverlay, ButtonStyle.Ghost, 130f, 44f);
        }

        /// <summary>A small gold section heading in a menu list.</summary>
        private static void Section(Transform column, string title)
        {
            var label = UiKit.Label(column, title, 30f, Theme.Gold, TextAlignmentOptions.Left, true);
            UiKit.Size(label, 64f);
            label.alignment = TextAlignmentOptions.BottomLeft;
        }

        private static void Mode(Transform column, string name, string detail, System.Action onClick, ButtonStyle style)
        {
            UiKit.Button(column, name, onClick, style, 120f, 50f);
            UiKit.Size(UiKit.Label(column, detail, 28f, Theme.Muted), 44f);
        }

        /// <summary>King of the Court: short games against league teams in a row until you lose.</summary>
        private void ShowKing()
        {
            var c = App.Catalog;
            var career = App.Career;
            var k = career.king;
            var column = OpenOverlay("KING OF THE COURT", out var footer);
            UiKit.Size(UiKit.Label(column, "First to 11 or 90 seconds. Every win adds to your streak and pays a bonus. One loss ends the run.",
                                   30f, Theme.Cream), 100f);
            UiControls.Stat(column, "BEST STREAK", k.best.ToString());
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);

            var mine = Secrets.PlayableTeams(c, App.Career.secrets);
            var next = k.active && c.Team(k.homeTeamId) != null ? KingEngine.NextMatch(k, c, k.homeTeamId, career.settings.difficultyId) : null;
            if (next != null)
            {
                UiControls.Stat(column, "CURRENT STREAK", k.streak.ToString());
                UiKit.Size(UiKit.Label(column, c.Team(k.homeTeamId).FullName.ToUpperInvariant() + "\nVS  " +
                                       (c.Team(next.AwayTeamId)?.FullName ?? "?").ToUpperInvariant(), 38f, Theme.Gold, TextAlignmentOptions.Center, true), 110f);
                UiKit.Button(footer, "PLAY", () =>
                {
                    App.PendingMatch = next;
                    SceneFlow.GoTo(SceneNames.Game);
                }, ButtonStyle.Primary, 130f);
                return;
            }

            if (mine.Count == 0) mine = c.TeamsInTier(TeamTier.League);
            if (mine.Count == 0) return;
            int pick = 0;
            TextMeshProUGUI teamLabel = null;
            UiKit.Size(UiKit.Label(column, "YOUR TEAM", 32f, Theme.Muted, TextAlignmentOptions.Center, true), 50f);
            teamLabel = UiKit.Label(column, mine[pick].FullName.ToUpperInvariant(), 40f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Size(teamLabel, 64f);
            if (mine.Count > 1)
                UiKit.Button(column, "CHANGE TEAM", () =>
                {
                    pick = (pick + 1) % mine.Count;
                    teamLabel.text = mine[pick].FullName.ToUpperInvariant();
                }, ButtonStyle.Ghost, 90f, 34f);
            UiKit.Button(footer, k.runs == 0 ? "START" : "NEW RUN", () =>
            {
                KingEngine.Start(App.Career.king, App.Catalog, mine[pick].id);
                App.SaveCareer();
                ShowKing();
            }, ButtonStyle.Primary, 130f);
        }

        /// <summary>Arcade Ladder: six stages against tougher teams, then the secret boss.</summary>
        private void ShowArcade()
        {
            var c = App.Catalog;
            var a = App.Career.secrets.arcade;
            var column = OpenOverlay("ARCADE LADDER", out var footer);
            UiKit.Size(UiKit.Label(column, "Six games, each tougher than the last. Lose and use a continue to try the stage again. Lose with none left: GAME OVER.",
                                   30f, Theme.Cream), 110f);
            UiControls.Stat(column, "CLEARS", a.clears.ToString());
            UiControls.Stat(column, "BEST STAGE", a.bestRung + " / " + ArcadeEngine.Rungs);
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);

            var next = ArcadeEngine.NextMatch(a, c);
            if (next != null)
            {
                var ladder = ArcadeEngine.Ladder(c, a.homeTeamId);
                for (int i = 0; i < ladder.Count; i++)
                {
                    bool boss = ArcadeEngine.IsBossRung(i);
                    // The boss stays hidden until you've beaten it once.
                    string name = boss && a.clears == 0 ? "???" : c.Team(ladder[i]).FullName.ToUpperInvariant();
                    string mark = i < a.rung ? "<color=#8AFF80>CLEAR</color>  " : (i == a.rung ? "<color=#FFD166>NEXT</color>  " : "");
                    UiKit.Size(UiKit.Label(column, mark + (i + 1) + ".  " + name + (boss ? "  ·  BOSS" : ""), 32f,
                                           i == a.rung ? Theme.Gold : Theme.Cream, TextAlignmentOptions.Center, true), 50f);
                }
                UiControls.Stat(column, "CONTINUES", a.continues.ToString());
                UiKit.Button(footer, "PLAY", () =>
                {
                    App.PendingMatch = next;
                    SceneFlow.GoTo(SceneNames.Game);
                }, ButtonStyle.Primary, 130f);
                return;
            }

            var mine = Secrets.PlayableTeams(c, App.Career.secrets);
            mine.RemoveAll(t => t.id == DefaultContent.BossTeamId); // the boss can't climb its own ladder
            if (mine.Count == 0) return;
            int pick = 0;
            UiKit.Size(UiKit.Label(column, "YOUR TEAM", 32f, Theme.Muted, TextAlignmentOptions.Center, true), 50f);
            var teamLabel = UiKit.Label(column, mine[pick].FullName.ToUpperInvariant(), 40f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Size(teamLabel, 64f);
            if (mine.Count > 1)
                UiKit.Button(column, "CHANGE TEAM", () =>
                {
                    pick = (pick + 1) % mine.Count;
                    teamLabel.text = mine[pick].FullName.ToUpperInvariant();
                }, ButtonStyle.Ghost, 90f, 34f);
            UiKit.Button(footer, a.runs == 0 ? "START" : "NEW RUN", () =>
            {
                ArcadeEngine.Start(App.Career.secrets.arcade, mine[pick].id);
                App.SaveCareer();
                Audio.AudioManager.Play(SfxId.Coin, 0.8f);
                Audio.AudioManager.Voice("READY? TIP OFF!");
                ShowArcade();
            }, ButtonStyle.Primary, 130f);
        }

        /// <summary>Today's Daily Challenge: goal, matchup, streak.</summary>
        private void ShowDaily()
        {
            var c = App.Catalog;
            var career = App.Career;
            int day = App.Today;
            var d = DailyChallenges.For(day, c);
            var column = OpenOverlay("DAILY CHALLENGE", out var footer);
            UiKit.Size(UiKit.Label(column, d.Describe().ToUpperInvariant(), 48f, Theme.Gold, TextAlignmentOptions.Center, true), 80f);
            var home = c.Team(d.HomeTeamId);
            var opp = c.Team(d.OpponentId);
            UiKit.Size(UiKit.Label(column, (home?.FullName ?? "?").ToUpperInvariant() + "\nVS  " + (opp?.FullName ?? "?").ToUpperInvariant(),
                                   36f, Theme.Cream, TextAlignmentOptions.Center, true), 110f);
            var diff = c.Difficulty(d.DifficultyId);
            UiControls.Stat(column, "DIFFICULTY", (diff?.displayName ?? "?").ToUpperInvariant());
            int streak = DailyChallenges.LiveStreak(career.daily, day);
            UiControls.Stat(column, "STREAK", streak + "  (best " + career.daily.bestStreak + ")");
            bool done = DailyChallenges.CompletedToday(career.daily, day);
            UiKit.Size(UiKit.Label(column, done
                ? "Done for today! A new challenge arrives tomorrow. You can still play for fun."
                : "Complete it for +" + DailyChallenges.BonusFor(streak + 1) + " SP. Keep the streak going every day for a bigger bonus.",
                30f, done ? Theme.Cyan : Theme.Muted), 100f);
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
            UiKit.Button(footer, "PLAY", () =>
            {
                App.PendingMatch = d.ToRequest();
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Primary, 130f);
        }

        /// <summary>Local 2-player setup: each player picks a team.</summary>
        private void ShowVersus()
        {
            var c = App.Catalog;
            var league = Secrets.OpponentTeams(c, App.Career.secrets);
            var mineTeam = c.Team(CustomTeams.TeamId);
            if (mineTeam != null) league.Insert(0, mineTeam);
            int p1 = 0, p2 = 1;
            bool streets = _streetsStyle;
            var column = OpenOverlay(streets ? "STREETS: 2 PLAYER" : "2 PLAYER", out var footer);
            TextMeshProUGUI p1Label = null, p2Label = null;
            UiKit.Size(UiKit.Label(column, "PLAYER 1", 34f, Theme.Gold, TextAlignmentOptions.Center, true), 50f);
            p1Label = UiKit.Label(column, "", 40f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(p1Label, 60f);
            UiKit.Button(column, "CHANGE TEAM", () => { p1 = Next(p1, p2); Refresh(); }, ButtonStyle.Ghost, 90f, 34f);
            UiKit.Size(UiKit.Label(column, "PLAYER 2", 34f, Theme.Cyan, TextAlignmentOptions.Center, true), 50f);
            p2Label = UiKit.Label(column, "", 40f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(p2Label, 60f);
            UiKit.Button(column, "CHANGE TEAM", () => { p2 = Next(p2, p1); Refresh(); }, ButtonStyle.Ghost, 90f, 34f);
            UiKit.Button(column, "TWO PHONES  ·  EACH ON YOUR OWN", ShowLinkMenu, ButtonStyle.Secondary, 100f, 32f);
            UiKit.Button(column, "COUCH CUP  ·  TOURNAMENT FOR 2-8", ShowCouchCup, ButtonStyle.Secondary, 100f, 32f);
            UiKit.Button(column, "GAME TAPES  ·  WATCH AGAIN, SEND", ShowTapes, ButtonStyle.Secondary, 100f, 32f);
            UiKit.Size(UiKit.Label(column,
                "One iPhone: lay it flat between you. P1 plays from the bottom edge, P2 from the top.\n" +
                "Controllers: with two, P1 gets the first; with one, it's P2's (P1 uses touch).\n" +
                "Keyboard: P1 WASD · K J L C, P2 arrows · Num1 Num2 Num3 Num0.",
                26f, Theme.Muted), 170f);
            UiKit.Button(footer, "BACK", () => { if (streets) ShowStreets(); else ShowPlayMenu(); }, ButtonStyle.Ghost, 130f, 44f);
            UiKit.Button(footer, "TIP OFF", () =>
            {
                var home = league[p1];
                App.PendingMatch = new MatchRequest
                {
                    Mode = GameMode.Versus,
                    HomeTeamId = home.id,
                    AwayTeamId = league[p2].id,
                    CourtId = home.homeCourtId,
                    DifficultyId = App.Career.settings.difficultyId,
                };
                if (streets)
                {
                    var r = App.PendingMatch;
                    r.StreetsStyle = true;
                    r.StreetRules = true;
                    r.RulesId = Streets.RulesId;
                    r.TeamSize = Streets.TeamSize;
                    var courts = Streets.CourtList(c);
                    if (courts.Count > 0) r.CourtId = courts[(int)((uint)System.Environment.TickCount % (uint)courts.Count)].id;
                }
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Primary, 130f);

            int Next(int current, int other)
            {
                int n = (current + 1) % league.Count;
                if (n == other) n = (n + 1) % league.Count;
                return n;
            }

            void Refresh()
            {
                p1Label.text = league[p1].FullName.ToUpperInvariant();
                p2Label.text = league[p2].FullName.ToUpperInvariant();
            }
            Refresh();
        }

        /// <summary>First Call Classic: four-team knockout. Shows the bracket and plays the crew's next game.</summary>
        private void ShowClassic()
        {
            var c = App.Catalog;
            var career = App.Career;
            var t = career.classic;
            var column = OpenOverlay("FIRST CALL CLASSIC", out var footer);
            UiKit.Size(UiKit.Label(column, "Your First Callers vs three league teams. Win two in a row for the title.",
                                   32f, Theme.Cream), 100f);
            UiKit.Size(UiKit.Label(column, "TITLES WON: " + t.titles, 36f, Theme.Gold, TextAlignmentOptions.Center, true), 60f);

            if (t.seeds.Count == 4)
            {
                UiKit.Size(UiKit.Label(column, "EDITION " + t.edition, 30f, Theme.Muted, TextAlignmentOptions.Center, true), 44f);
                foreach (var g in t.games)
                {
                    string label = g.round == 2 ? "FINAL" : "SEMI";
                    string home = c.Team(g.homeId)?.abbreviation ?? "?";
                    string away = c.Team(g.awayId)?.abbreviation ?? "?";
                    string line = label + "   " + home + (g.played ? "  " + g.homeScore + " - " + g.awayScore + "  " : "  vs  ") + away;
                    bool mine = g.Involves(ClassicEngine.CrewId);
                    UiKit.Size(UiKit.Label(column, line, 40f, mine ? Theme.Gold : Theme.Cream, TextAlignmentOptions.Center, true), 60f);
                }
                if (t.finished)
                    UiKit.Size(UiKit.Label(column, "CHAMPION: " + (c.Team(t.championId)?.FullName ?? "?").ToUpperInvariant(),
                                           36f, Theme.Cyan, TextAlignmentOptions.Center, true), 60f);
            }

            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
            var next = ClassicEngine.NextMatch(t, c, career.settings.difficultyId);
            if (next != null)
            {
                var opp = c.Team(next.AwayTeamId);
                UiKit.Size(UiKit.Label(column, "NEXT: " + (next.Round == 2 ? "FINAL" : "SEMIFINAL") + " VS " +
                                               (opp?.FullName ?? "?").ToUpperInvariant(), 34f, Theme.Cream, TextAlignmentOptions.Center, true), 60f);
                UiKit.Button(footer, "PLAY", () =>
                {
                    App.PendingMatch = next;
                    SceneFlow.GoTo(SceneNames.Game);
                }, ButtonStyle.Primary, 130f);
            }
            else
            {
                UiKit.Button(footer, t.edition == 0 ? "ENTER" : "NEW CLASSIC", () =>
                {
                    ClassicEngine.Start(App.Career.classic, App.Catalog);
                    App.SaveCareer();
                    ShowClassic();
                }, ButtonStyle.Primary, 130f);
            }
        }

        /// <summary>1-on-1: pick your team and theirs; only the two leaders play.</summary>
        private void ShowOneOnOne()
        {
            var c = App.Catalog;
            var mine = Secrets.PlayableTeams(c, App.Career.secrets);
            var theirs = Secrets.OpponentTeams(c, App.Career.secrets);
            int a = 0, b = 0;
            var column = OpenOverlay("1-ON-1", out var footer);
            UiKit.Size(UiKit.Label(column, "Your player against their leader. Teammates sit out. First to 11 or two minutes. Make it, take it; out of bounds is the other player's ball.", 30f, Theme.Cream), 130f);
            UiKit.Size(UiKit.Label(column, "YOUR TEAM", 32f, Theme.Muted, TextAlignmentOptions.Center, true), 50f);
            TextMeshProUGUI mineLabel = null, theirLabel = null; // declared first so Refresh() can see them
            mineLabel = UiKit.Label(column, "", 40f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Size(mineLabel, 64f);
            UiKit.Button(column, "CHANGE TEAM", () => { a = (a + 1) % mine.Count; Refresh(); }, ButtonStyle.Ghost, 90f, 34f);
            theirLabel = UiKit.Label(column, "", 40f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(theirLabel, 64f);
            UiKit.Button(column, "CHANGE OPPONENT", () => { b = (b + 1) % theirs.Count; Refresh(); }, ButtonStyle.Ghost, 90f, 34f);
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
            UiKit.Button(footer, "TIP OFF", () =>
            {
                App.PendingMatch = new MatchRequest
                {
                    Mode = GameMode.OneOnOne,
                    HomeTeamId = mine[a].id,
                    AwayTeamId = theirs[b].id,
                    CourtId = mine[a].homeCourtId,
                    RulesId = "rules.oneonone",
                    DifficultyId = App.Career.settings.difficultyId,
                };
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Primary, 130f);

            void Refresh()
            {
                if (theirs[b].id == mine[a].id) b = (b + 1) % theirs.Count;
                var leader = c.Player(theirs[b].rosterPlayerIds[0]);
                mineLabel.text = mine[a].FullName.ToUpperInvariant();
                theirLabel.text = Loc.T("VS") + "  " + (leader != null ? leader.DisplayName.ToUpperInvariant() + "  ·  " : "") + theirs[b].abbreviation;
            }
            Refresh();
        }

        /// <summary>Full Court 5-on-5: pick both teams (three-player crews get two reserves).</summary>
        private void ShowFullCourt()
        {
            var c = App.Catalog;
            var mine = Secrets.PlayableTeams(c, App.Career.secrets);
            var theirs = Secrets.OpponentTeams(c, App.Career.secrets);
            int a = 0, b = 0;
            var column = OpenOverlay("FULL COURT", out var footer);
            UiKit.Size(UiKit.Label(column,
                "Five on five, end to end. Inside the arc is 2, outside is 3. After a basket the other team inbounds and brings it up. Four minutes; 20-second shot clock.",
                30f, Theme.Cream), 150f);
            UiKit.Size(UiKit.Label(column, "YOUR TEAM", 32f, Theme.Muted, TextAlignmentOptions.Center, true), 50f);
            TextMeshProUGUI mineLabel = null, theirLabel = null; // declared first so Refresh() can see them
            mineLabel = UiKit.Label(column, "", 40f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Size(mineLabel, 64f);
            UiKit.Button(column, "CHANGE TEAM", () => { a = (a + 1) % mine.Count; Refresh(); }, ButtonStyle.Ghost, 90f, 34f);
            theirLabel = UiKit.Label(column, "", 40f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(theirLabel, 64f);
            UiKit.Button(column, "CHANGE OPPONENT", () => { b = (b + 1) % theirs.Count; Refresh(); }, ButtonStyle.Ghost, 90f, 34f);
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
            UiKit.Button(footer, "TIP OFF", () =>
            {
                App.PendingMatch = new MatchRequest
                {
                    Mode = GameMode.FullCourt,
                    HomeTeamId = mine[a].id,
                    AwayTeamId = theirs[b].id,
                    CourtId = mine[a].homeCourtId,
                    RulesId = FullCourt.RulesId,
                    DifficultyId = App.Career.settings.difficultyId,
                };
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Primary, 130f);

            void Refresh()
            {
                if (theirs[b].id == mine[a].id) b = (b + 1) % theirs.Count;
                mineLabel.text = mine[a].FullName.ToUpperInvariant();
                theirLabel.text = Loc.T("VS") + "  " + theirs[b].FullName.ToUpperInvariant();
            }
            Refresh();
        }

        /// <summary>Holiday Games: a game on a decorated court (Quick Call rules), the one in season first.</summary>
        private void ShowHolidays(HolidayTheme highlight = HolidayTheme.None)
        {
            var c = App.Catalog;
            var mine = Secrets.PlayableTeams(c, App.Career.secrets);
            var theirs = Secrets.OpponentTeams(c, App.Career.secrets);
            int a = 0, b = new SeededRandom((uint)System.Environment.TickCount | 1u).Range(0, theirs.Count);
            if (highlight == HolidayTheme.None) highlight = Holidays.InSeason(System.DateTime.Now);
            var column = OpenOverlay("HOLIDAY GAMES", out var footer);
            TextMeshProUGUI mineLabel = null, theirLabel = null; // declared first so Refresh() can see them
            mineLabel = UiKit.Label(column, "", 36f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Size(mineLabel, 56f);
            UiKit.Button(column, "CHANGE TEAM", () => { a = (a + 1) % mine.Count; Refresh(); }, ButtonStyle.Ghost, 80f, 30f);
            theirLabel = UiKit.Label(column, "", 36f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(theirLabel, 56f);
            UiKit.Button(column, "CHANGE OPPONENT", () => { b = (b + 1) % theirs.Count; Refresh(); }, ButtonStyle.Ghost, 80f, 30f);
            var order = new List<HolidayGame>(Holidays.All);
            order.Sort((x, y) => (y.Theme == highlight ? 1 : 0) - (x.Theme == highlight ? 1 : 0));
            foreach (var h in order)
            {
                var game = h;
                Mode(column, game.Name, (game.Theme == highlight ? Loc.T("IN SEASON NOW") + "  ·  " : "") + Loc.T(game.Blurb), () =>
                {
                    App.PendingMatch = Holidays.Request(game.Theme, mine[a].id, theirs[b].id, App.Career.settings.difficultyId);
                    SceneFlow.GoTo(SceneNames.Game);
                }, game.Theme == highlight ? ButtonStyle.Primary : ButtonStyle.Secondary);
            }
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);

            void Refresh()
            {
                if (theirs[b].id == mine[a].id) b = (b + 1) % theirs.Count;
                mineLabel.text = mine[a].FullName.ToUpperInvariant();
                theirLabel.text = Loc.T("VS") + "  " + theirs[b].FullName.ToUpperInvariant();
            }
            Refresh();
        }

        /// <summary>Party games: quick shooting games, solo or pass-the-phone.</summary>
        private void ShowParty()
        {
            var column = OpenOverlay("PARTY GAMES", out var footer);
            var best = App.Career.practice;
            var league = App.Catalog.TeamsInTier(TeamTier.League);
            string RandomTeam() => league[new SeededRandom((uint)System.Environment.TickCount | 1u).Range(0, league.Count)].id;

            Mode(column, "H-O-R-S-E VS CPU", "Set a shot, make them match it. Spell HORSE and you lose. Wins: " + best.horseWins, () =>
                StartParty(DrillKind.Horse, "horse:cpu", RandomTeam()), ButtonStyle.Primary);
            Mode(column, "H-O-R-S-E VS FRIEND", "Pass the phone: P1 and P2 take turns with the same player.", () =>
                StartParty(DrillKind.Horse, "horse:friend", null), ButtonStyle.Secondary);
            Mode(column, "21", "1-on-1 to exactly 21. Make it, take it. Go over and you bust back to 13.", () =>
            {
                var mine = Secrets.PlayableTeams(App.Catalog, App.Career.secrets);
                var opp = league.Find(t => t.id != mine[0].id) ?? league[0];
                App.PendingMatch = new MatchRequest
                {
                    Mode = GameMode.OneOnOne,
                    HomeTeamId = mine[0].id,
                    AwayTeamId = opp.id,
                    CourtId = mine[0].homeCourtId,
                    RulesId = "rules.21",
                    DifficultyId = App.Career.settings.difficultyId,
                };
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Secondary);
            Mode(column, "AROUND THE WORLD", "Make one from each of 7 spots, corner to corner. Best: " +
                 (best.aroundWorldTime > 0f ? best.aroundWorldTime.ToString("0.0") + " s" : "—"), () =>
                StartParty(DrillKind.AroundTheWorld, null, null), ButtonStyle.Secondary);
            Mode(column, "SHOOTOUT", "Beat a CPU shooter's 3-point score. Wins: " + best.shootoutWins, () =>
                StartParty(DrillKind.Shootout, null, RandomTeam()), ButtonStyle.Secondary);
            Mode(column, "SHOOTOUT VS FRIEND", "Pass the phone: P1 sets a 60-second score, P2 tries to beat it.", () =>
            {
                App.PendingDuel = null;
                StartParty(DrillKind.Shootout, ShootoutDuel.ContextId, null);
            }, ButtonStyle.Secondary);
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);
        }

        private static void StartParty(DrillKind drill, string context, string opponentTeamId)
        {
            var request = MatchRequest.PracticeDefault();
            request.Drill = (int)drill;
            request.ContextId = context;
            request.AwayTeamId = opponentTeamId;
            request.DifficultyId = App.Career.settings.difficultyId;
            App.PendingMatch = request;
            SceneFlow.GoTo(SceneNames.Game);
        }

        /// <summary>The Caller Cup: an eight-team knockout bracket.</summary>
        private void ShowCup()
        {
            var c = App.Catalog;
            var career = App.Career;
            var cup = career.cup;
            var column = OpenOverlay("CALLER CUP", out var footer);
            UiKit.Size(UiKit.Label(column, "Eight teams, three rounds, one cup. You're the eighth seed. Lose once and you're out.", 30f, Theme.Cream), 90f);
            UiControls.Stat(column, "TITLES", cup.titles.ToString());
            UiKit.Button(footer, "BACK", ShowPlayMenu, ButtonStyle.Ghost, 130f, 44f);

            if (cup.bracket.Count == CupEngine.Teams)
            {
                for (int round = 1; round <= CupEngine.Rounds; round++)
                {
                    var games = cup.games.FindAll(g => g.round == round);
                    if (games.Count == 0) continue;
                    UiKit.Size(UiKit.Label(column, CupEngine.RoundName(round), 30f, Theme.Muted, TextAlignmentOptions.Center, true), 44f);
                    foreach (var g in games)
                    {
                        string home = c.Team(g.homeId)?.abbreviation ?? "?";
                        string away = c.Team(g.awayId)?.abbreviation ?? "?";
                        bool mine = g.Involves(cup.homeTeamId);
                        UiKit.Size(UiKit.Label(column, home + (g.played ? "  " + g.homeScore + " - " + g.awayScore + "  " : "  vs  ") + away,
                                               36f, mine ? Theme.Gold : Theme.Cream, TextAlignmentOptions.Center, true), 52f);
                    }
                }
                if (cup.finished)
                    UiKit.Size(UiKit.Label(column, "CHAMPION: " + (c.Team(cup.championId)?.FullName ?? "?").ToUpperInvariant(),
                                           36f, Theme.Cyan, TextAlignmentOptions.Center, true), 60f);
            }

            var next = CupEngine.NextMatch(cup, c, career.settings.difficultyId);
            if (next != null)
            {
                UiKit.Button(footer, "PLAY", () =>
                {
                    App.PendingMatch = next;
                    SceneFlow.GoTo(SceneNames.Game);
                }, ButtonStyle.Primary, 130f);
                return;
            }
            var mineTeams = Secrets.PlayableTeams(c, career.secrets);
            mineTeams.RemoveAll(t => t.tier == TeamTier.Secret);
            if (mineTeams.Count == 0) return;
            int pick = 0;
            var teamLabel = UiKit.Label(column, mineTeams[pick].FullName.ToUpperInvariant(), 40f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Size(teamLabel, 64f);
            if (mineTeams.Count > 1)
                UiKit.Button(column, "CHANGE TEAM", () =>
                {
                    pick = (pick + 1) % mineTeams.Count;
                    teamLabel.text = mineTeams[pick].FullName.ToUpperInvariant();
                }, ButtonStyle.Ghost, 90f, 34f);
            UiKit.Button(footer, cup.edition == 0 ? "ENTER" : "NEW CUP", () =>
            {
                CupEngine.Start(App.Career.cup, App.Catalog, mineTeams[pick].id);
                App.SaveCareer();
                ShowCup();
            }, ButtonStyle.Primary, 130f);
        }

        private void ShowPractice()
        {
            var column = OpenOverlay("PRACTICE LAB", out var footer);
            var best = App.Career.practice;
            Drill(column, "FREE SHOOT", "60 seconds. Best: " + best.freeShootMakes + " makes, streak " + best.freeShootStreak, 0);
            Drill(column, "PASSING TARGETS", "45 seconds. Best: " + best.passingScore + " targets", 1);
            Drill(column, "DRIBBLE LANE", "Weave the cones. Best: " + (best.dribbleLaneTime > 0f ? best.dribbleLaneTime.ToString("0.00") + " s" : "—"), 2);
            Drill(column, "3-POINT CONTEST", "60 seconds, arc shots only. Gold spot = money ball (2). Best: " + best.threePointBest, 3);
            Drill(column, "LOCKDOWN", "Defense: stop 6 possessions. Best: " + best.lockdownBest + " / 6", 4);
            Drill(column, "AROUND THE WORLD", "Make one from each of 7 spots. Best: " + (best.aroundWorldTime > 0f ? best.aroundWorldTime.ToString("0.0") + " s" : "—"), (int)DrillKind.AroundTheWorld);
            // Shootout: the 3-point contest head to head with a random league team's best shooter.
            UiKit.Button(column, "SHOOTOUT", () =>
            {
                var league = App.Catalog.TeamsInTier(TeamTier.League);
                var request = MatchRequest.PracticeDefault();
                request.Drill = (int)DrillKind.Shootout;
                request.AwayTeamId = league[new SeededRandom((uint)System.Environment.TickCount | 1u).Range(0, league.Count)].id;
                request.DifficultyId = App.Career.settings.difficultyId;
                App.PendingMatch = request;
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Secondary, 130f);
            UiKit.Size(UiKit.Label(column, "Beat a CPU shooter's 3-point score in 60 seconds. Wins: " + best.shootoutWins, 30f, Theme.Muted), 50f);
            UiKit.Button(footer, "BACK", CloseOverlay, ButtonStyle.Ghost, 130f, 44f);
        }

        private static void Drill(Transform column, string name, string detail, int drill)
        {
            UiKit.Button(column, name, () =>
            {
                var request = MatchRequest.PracticeDefault();
                request.Drill = drill;
                App.PendingMatch = request;
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Secondary, 130f);
            UiKit.Size(UiKit.Label(column, detail, 30f, Theme.Muted), 50f);
        }
    }
}
