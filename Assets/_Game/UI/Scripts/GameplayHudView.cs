using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using Mixtape.Core;
using Mixtape.Gameplay;
using Mixtape.InputCtrl;
using Mixtape.Ads;

namespace Mixtape.UITK
{
    /// <summary>
    /// In-race HUD + coordinator (UI Toolkit). Replaces the legacy uGUI <c>HUDController</c>.
    /// Drives the on-screen controls into <see cref="InputService"/>, runs the pause / objective /
    /// level-complete popups (each its own UIDocument), shows a transient 3-2-1-GO countdown and a
    /// small position/rank pill, and hands off to Level Complete on finish.
    ///
    /// Mechanics note (see HANDOFF.md "to add"): the booster button maps to the existing boost.
    /// The former double-jump button now triggers a TRICK JUMP (a normal jump + board
    /// kickflip) — plain double-jump was removed (player maxJumps = 1). Every singleton + ref is
    /// null-guarded so the overlay still runs standalone in the UIToolkitTesting sandbox.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameplayHudView : MonoBehaviour
    {
        [Header("Other in-race UI documents (GameObjects with their *View)")]
        public GameObject pauseUI;          // PauseView
        public GameObject objectiveUI;      // ObjectiveView
        public GameObject levelCompleteUI;  // LevelCompleteView
        public GameObject adRewardUI;       // AdRewardView (optional; trigger TBD)
        public GameObject resumeUI;         // ResumeView (shown when the player falls off the track)

        [Header("Flow")]
        [Tooltip("Show the Objective popup (time frozen) the moment the race starts. Off by default until the objective has real content; the popup is still wired and can be shown via ShowObjective().")]
        public bool showObjectiveAtStart = false;

        [Header("Finish → restart transition")]
        [Tooltip("Beat to hold on the final frame after the race ends before the screen cuts to black.")]
        public float finishHold = 0.5f;
        [Tooltip("Duration of the cut-to-black at finish and the fade-back-in on the restarted run.")]
        public float fadeDuration = 0.5f;

        /// <summary>Set just before a fade-restart so the freshly loaded HUD opens fully black and fades in.</summary>
        public static bool FadeInOnStart;

        private InputService _input;
        private RaceManager _race;
        private VisualElement _root;

        private VisualElement _rankPill;
        private Label _rankLabel;
        private Label _countdown;
        private VisualElement _scorePill;
        private Label _scoreLabel;
        private Label _comboLabel;
        private Button _pauseButton;
        private VisualElement[] _controls;
        private bool _objectiveShown;
        private bool _paused;
        private bool _subscribed;
        private Coroutine _countdownCo;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            _root = root;

            // Restart-from-finish: open the HUD fully black so the very first painted frame is
            // black (no flash of the start line), then Start() fades it back in.
            if (FadeInOnStart)
            {
                var overlay = UIFx.EnsureFadeOverlay(root);
                overlay.style.display = DisplayStyle.Flex;
                overlay.pickingMode = UnityEngine.UIElements.PickingMode.Position;
                overlay.style.opacity = 1f;
            }

            _rankPill  = root.Q<VisualElement>("rank-pill");
            _rankLabel = root.Q<Label>("rank-label");
            _countdown = root.Q<Label>("countdown");
            _scorePill  = root.Q<VisualElement>("score-pill");
            _scoreLabel = root.Q<Label>("score-label");
            _comboLabel = root.Q<Label>("combo-label");
            _pauseButton = root.Q<Button>("pause-btn");

            // ---- steering: press-and-hold via pointer events ----
            HoldSteer(root.Q<Button>("left-btn"),  -1f);
            HoldSteer(root.Q<Button>("right-btn"), +1f);

            // ---- jump (tap) ----
            Click(root.Q<Button>("jump-btn"), () => _input?.PressJump());

            // ---- boost: hold the existing speed boost ----
            HoldBoost(root.Q<Button>("booster2-btn"));

            // ---- trick jump: jumps (like the jump button) + board kickflip (was double-jump) ----
            Click(root.Q<Button>("djump-btn"), DoTrick);

            // ---- pause (available once the race/countdown begins; hidden during cutscenes) ----
            Click(_pauseButton, TogglePause);

            // the steer/jump/boost controls only do something once the race is live
            // (the skater is gated inactive until then), so keep them hidden during the
            // intro + countdown and reveal them on GO — mirrors the old HUD.
            _controls = new VisualElement[]
            {
                root.Q<Button>("left-btn"), root.Q<Button>("right-btn"),
                root.Q<Button>("jump-btn"), root.Q<Button>("djump-btn"),
                root.Q<Button>("booster2-btn"),
            };

            // popups start hidden; wire their hooks
            SetActive(pauseUI, false);
            SetActive(objectiveUI, false);
            SetActive(levelCompleteUI, false);
            SetActive(adRewardUI, false);
            SetActive(resumeUI, false);
            WirePopups();

            // rank + score hidden until the race is running
            SetHidden(_rankPill, true);
            SetHidden(_scorePill, true);
            SetHidden(_countdown, true);
        }

        // Singletons + event subscription happen in Start so they're guaranteed past
        // InputService/RaceManager Awake (OnEnable can run before another object's Awake).
        private void Start()
        {
            _input = InputService.Instance;
            _race  = RaceManager.Instance;
            if (_race != null && !_subscribed)
            {
                _race.CountdownTick += OnCountdown;
                _race.RaceStarted   += OnRaceStarted;
                _race.RaceCelebrationStarted += OnRaceCelebrationStarted;
                _race.RaceFinished  += OnRaceFinished;
                if (_race.player != null) _race.player.FellOff += OnPlayerFell;
                _subscribed = true;
                // hide the controls and pause until the race countdown starts (real game).
                // In the standalone sandbox (no RaceManager) leave them visible.
                SetControlsVisible(false);
                SetPauseVisible(false);
            }

            // Restarted run: fade the opening black overlay back in to reveal the start line.
            if (FadeInOnStart)
            {
                FadeInOnStart = false;
                StartCoroutine(UIFx.Fade(UIFx.EnsureFadeOverlay(_root), 1f, 0f, fadeDuration));
            }
        }

        private void SetControlsVisible(bool v)
        {
            if (_controls == null) return;
            foreach (var c in _controls)
                if (c != null) c.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetPauseVisible(bool v)
        {
            if (_pauseButton != null) _pauseButton.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnDisable()
        {
            if (_subscribed && _race != null)
            {
                _race.CountdownTick -= OnCountdown;
                _race.RaceStarted   -= OnRaceStarted;
                _race.RaceCelebrationStarted -= OnRaceCelebrationStarted;
                _race.RaceFinished  -= OnRaceFinished;
                if (_race.player != null) _race.player.FellOff -= OnPlayerFell;
                _subscribed = false;
            }
        }

        private void Update()
        {
            if (_race == null || !_race.IsRunning) return;
            if (_rankLabel != null)
                _rankLabel.text = $"{_race.PlayerPlace}/{_race.RacerCount}";

            var s = _race.Score;
            if (s != null)
            {
                if (_scoreLabel != null) _scoreLabel.text = s.Score.ToString("N0");
                if (_comboLabel != null)
                {
                    bool combo = s.CurrentCombo > 1;   // show the multiplier only once chaining
                    SetHidden(_comboLabel, !combo);
                    if (combo) _comboLabel.text = $"COMBO x{s.CurrentCombo}";
                }
            }
        }

        // ---------- control wiring ----------
        private float _steerHeld;   // direction currently held via the HUD buttons (0 = none)

        private void HoldSteer(Button b, float dir)
        {
            if (b == null) return;
            // TrickleDown on BOTH down and up: a Button's built-in Clickable consumes pointer
            // events in the bubble phase (StopImmediatePropagation), so a bubble-phase PointerUp
            // never fires with a mouse — steer stayed latched until the cursor LEFT the button,
            // which made mouse steering feel mushier than the A/D keys (instant release).
            b.RegisterCallback<PointerDownEvent>(_ =>
            {
                _steerHeld = dir;
                if (dir < 0) _input?.SteerLeftDown(); else _input?.SteerRightDown();
            }, TrickleDown.TrickleDown);
            b.RegisterCallback<PointerUpEvent>(_ => ReleaseSteer(dir), TrickleDown.TrickleDown);
            // backstops: finger slid off, OS cancelled the touch, or the Clickable lost capture
            b.RegisterCallback<PointerLeaveEvent>(_ => ReleaseSteer(dir));
            b.RegisterCallback<PointerCancelEvent>(_ => ReleaseSteer(dir), TrickleDown.TrickleDown);
            b.RegisterCallback<PointerCaptureOutEvent>(_ => ReleaseSteer(dir));
        }

        private void ReleaseSteer(float dir)
        {
            // only release if we're the side currently steering (so the other button isn't
            // cancelled). Tracked locally — InputService.Steer is keyboard-overridden, so it
            // can't be trusted to tell which HUD button is held.
            if (_steerHeld != dir) return;
            _steerHeld = 0f;
            _input?.SteerRelease();
        }

        private void HoldBoost(Button b)
        {
            if (b == null) return;
            // TrickleDown on down AND up so the Button's Clickable can't swallow either (see HoldSteer).
            b.RegisterCallback<PointerDownEvent>(_ => _input?.SetBoostHeld(true), TrickleDown.TrickleDown);
            b.RegisterCallback<PointerUpEvent>(_ => _input?.SetBoostHeld(false), TrickleDown.TrickleDown);
            b.RegisterCallback<PointerLeaveEvent>(_ => _input?.SetBoostHeld(false));
            b.RegisterCallback<PointerCancelEvent>(_ => _input?.SetBoostHeld(false), TrickleDown.TrickleDown);
            b.RegisterCallback<PointerCaptureOutEvent>(_ => _input?.SetBoostHeld(false));
        }

        // ---------- countdown / race events ----------
        private void OnCountdown(int n)
        {
            if (_countdown == null) return;
            SetPauseVisible(true);
            // Stop only the previous countdown pop — StopAllCoroutines here would also kill the
            // fade-in that runs on a restarted run (the countdown fires on the same first frame).
            if (_countdownCo != null) StopCoroutine(_countdownCo);
            _countdownCo = StartCoroutine(CountdownPop(n));
        }

        private IEnumerator CountdownPop(int n)
        {
            SetHidden(_countdown, false);
            _countdown.text = n > 0 ? n.ToString() : "GO!";
            _countdown.style.scale = new StyleScale(new Scale(Vector3.one * 1.4f));
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.Lerp(1.4f, 1f, t / 0.35f);
                _countdown.style.scale = new StyleScale(new Scale(new Vector3(s, s, 1f)));
                yield return null;
            }
            if (n == 0)
            {
                yield return new WaitForSecondsRealtime(0.4f);
                SetHidden(_countdown, true);
            }
        }

        private void OnRaceStarted()
        {
            SetHidden(_rankPill, false);
            SetHidden(_scorePill, false);
            SetPauseVisible(true);
            SetControlsVisible(true);
            if (showObjectiveAtStart && objectiveUI != null && !_objectiveShown)
            {
                _objectiveShown = true;
                Time.timeScale = 0f;            // freeze until the player acknowledges
                SetActive(objectiveUI, true);
            }
        }

        private void OnRaceCelebrationStarted(bool won, int place)
        {
            Time.timeScale = 1f;
            SetControlsVisible(false);
            SetPauseVisible(false);
            SetHidden(_scorePill, true);
            SetHidden(_rankPill, true);
            SetHidden(_countdown, true);
        }

        private void OnRaceFinished(bool won, int place)
        {
            Time.timeScale = 1f;
            SetControlsVisible(false);
            SetPauseVisible(false);
            SetHidden(_scorePill, true);
            SetHidden(_rankPill, true);
            SetHidden(_countdown, true);

            // The run still counts: placement bonus + stars + coin reward were computed in
            // RaceManager.FinishRace(); pay the coins out even though no results panel is shown.
            var s = _race != null ? _race.Score : null;
            int reward = s != null ? s.Coins : 0;
            if (reward > 0) GameManager.Instance?.AddCoins(reward);

            // No Level Complete panel: cut the whole screen to black, then restart at the start
            // line — the freshly loaded HUD opens black and fades back in (see FadeInOnStart).
            StartCoroutine(FadeOutAndRestart());
        }

        private IEnumerator FadeOutAndRestart()
        {
            if (finishHold > 0f) yield return new WaitForSecondsRealtime(finishHold);

            yield return UIFx.Fade(UIFx.EnsureFadeOverlay(_root), 0f, 1f, fadeDuration);

            // Restart the Game scene directly (no Loading screen): the load freeze is hidden behind
            // the black overlay, and the next HUD opens black + fades in for a seamless cut.
            FadeInOnStart = true;
            Mixtape.UI.CutsceneController.SkipNextIntro = true;   // skip the opening intro on the restart
            SceneFlow.LoadDirect(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        // ---------- pause ----------
        private void TogglePause()
        {
            if (_paused) Resume();
            else
            {
                _paused = true;
                Time.timeScale = 0f;
                SetActive(pauseUI, true);
            }
        }

        private void Resume()
        {
            _paused = false;
            SetActive(pauseUI, false);
            Time.timeScale = 1f;
        }

        // ---------- fell off the track ----------
        private void OnPlayerFell()
        {
            // freeze the run and offer Resume(ad) / Restart / Home
            Time.timeScale = 0f;
            SetControlsVisible(false);
            SetPauseVisible(false);
            SetActive(resumeUI, true);
        }

        // Watch-ad → respawn at the fall point and keep racing.
        private void ResumeFromFall()
        {
            var p = _race != null ? _race.player : null;
            if (p != null)
            {
                var respawn = p.GetComponent<SkaterRespawn>();
                if (respawn != null) respawn.Respawn(0);   // back onto the nearest checkpoint
            }
            SetActive(resumeUI, false);
            SetControlsVisible(true);
            SetPauseVisible(true);
            Time.timeScale = 1f;
        }

        private void WirePopups()
        {
            var pause = pauseUI != null ? pauseUI.GetComponent<PauseView>() : null;
            if (pause != null)
            {
                pause.onResume  = Resume;
                pause.onRestart = () => { Time.timeScale = 1f; GameManager.Instance?.ReloadCurrent(); };
                pause.onHome    = () => { Time.timeScale = 1f; GameManager.Instance?.LoadMainMenu(); };
            }

            var obj = objectiveUI != null ? objectiveUI.GetComponent<ObjectiveView>() : null;
            if (obj != null)
                obj.onOk = () => { SetActive(objectiveUI, false); Time.timeScale = 1f; };

            var ad = adRewardUI != null ? adRewardUI.GetComponent<AdRewardView>() : null;
            if (ad != null)
                ad.onGetNow = () => AdManager.ShowRewarded("hud_reward", ok =>
                {
                    if (ok && _race != null && _race.player != null) _race.player.ApplyBoost(2.2f, 4f);
                    SetActive(adRewardUI, false);
                });

            var resume = resumeUI != null ? resumeUI.GetComponent<ResumeView>() : null;
            if (resume != null)
            {
                // watch ad → continue from the fall point; only resume if the ad actually completed
                resume.onResumeAd = () => AdManager.ShowRewarded("fall_resume", ok => { if (ok) ResumeFromFall(); });
                // restart the race from the start, skipping the cutscene
                resume.onRestart = () =>
                {
                    Time.timeScale = 1f;
                    Mixtape.UI.CutsceneController.SkipNextIntro = true;
                    GameManager.Instance?.ReloadCurrent();
                };
                resume.onHome = () => { Time.timeScale = 1f; GameManager.Instance?.LoadMainMenu(); };
            }
        }

        // Trigger the player's trick jump (jump + board kickflip). Null-guarded for the sandbox.
        private void DoTrick()
        {
            var p = _race != null ? _race.player : null;
            if (p == null) return;
            p.GetComponent<RiderAnimDriver>()?.Trick();
        }

        /// <summary>Public hook so a future trigger can show the Objective popup (freezes time until OK).</summary>
        public void ShowObjective() { if (objectiveUI != null) { Time.timeScale = 0f; SetActive(objectiveUI, true); } }

        /// <summary>Public hook so a future trigger can offer the rewarded-ad popup mid-game.</summary>
        public void ShowAdReward() => SetActive(adRewardUI, true);

        // ---------- helpers ----------
        private static void Click(Button b, Action cb) { if (b != null) b.clicked += cb; }
        private static void SetActive(GameObject go, bool on) { if (go != null && go.activeSelf != on) go.SetActive(on); }
        private static void SetHidden(VisualElement ve, bool hidden) { if (ve != null) ve.EnableInClassList("is-hidden", hidden); }
    }
}
