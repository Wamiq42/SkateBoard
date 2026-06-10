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
    /// Mechanics note (see HANDOFF.md "to add"): both booster buttons still map to the existing
    /// boost. The former double-jump button now triggers a TRICK JUMP (a normal jump + board
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

        [Header("Flow")]
        [Tooltip("Show the Objective popup (time frozen) the moment the race starts. Off by default until the objective has real content; the popup is still wired and can be shown via ShowObjective().")]
        public bool showObjectiveAtStart = false;

        private InputService _input;
        private RaceManager _race;

        private VisualElement _rankPill;
        private Label _rankLabel;
        private Label _countdown;
        private VisualElement _scorePill;
        private Label _scoreLabel;
        private Label _comboLabel;
        private VisualElement[] _controls;
        private bool _objectiveShown;
        private bool _paused;
        private bool _subscribed;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;

            _rankPill  = root.Q<VisualElement>("rank-pill");
            _rankLabel = root.Q<Label>("rank-label");
            _countdown = root.Q<Label>("countdown");
            _scorePill  = root.Q<VisualElement>("score-pill");
            _scoreLabel = root.Q<Label>("score-label");
            _comboLabel = root.Q<Label>("combo-label");

            // ---- steering: press-and-hold via pointer events ----
            HoldSteer(root.Q<Button>("left-btn"),  -1f);
            HoldSteer(root.Q<Button>("right-btn"), +1f);

            // ---- jump (tap) ----
            Click(root.Q<Button>("jump-btn"), () => _input?.PressJump());

            // ---- boost: both boosters hold the existing speed boost ----
            HoldBoost(root.Q<Button>("booster-btn"));
            HoldBoost(root.Q<Button>("booster2-btn"));

            // ---- trick jump: jumps (like the jump button) + board kickflip (was double-jump) ----
            Click(root.Q<Button>("djump-btn"), DoTrick);

            // ---- pause (always available, even during the intro) ----
            Click(root.Q<Button>("pause-btn"), TogglePause);

            // the steer/jump/boost controls only do something once the race is live
            // (the skater is gated inactive until then), so keep them hidden during the
            // intro + countdown and reveal them on GO — mirrors the old HUD.
            _controls = new VisualElement[]
            {
                root.Q<Button>("left-btn"), root.Q<Button>("right-btn"),
                root.Q<Button>("jump-btn"), root.Q<Button>("djump-btn"),
                root.Q<Button>("booster-btn"), root.Q<Button>("booster2-btn"),
            };

            // popups start hidden; wire their hooks
            SetActive(pauseUI, false);
            SetActive(objectiveUI, false);
            SetActive(levelCompleteUI, false);
            SetActive(adRewardUI, false);
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
                _race.RaceFinished  += OnRaceFinished;
                _subscribed = true;
                // hide the controls until the race actually starts (real game).
                // In the standalone sandbox (no RaceManager) leave them visible.
                SetControlsVisible(false);
            }
        }

        private void SetControlsVisible(bool v)
        {
            if (_controls == null) return;
            foreach (var c in _controls)
                if (c != null) c.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnDisable()
        {
            if (_subscribed && _race != null)
            {
                _race.CountdownTick -= OnCountdown;
                _race.RaceStarted   -= OnRaceStarted;
                _race.RaceFinished  -= OnRaceFinished;
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
        private void HoldSteer(Button b, float dir)
        {
            if (b == null) return;
            // TrickleDown: a Button's built-in Clickable consumes PointerDown in the bubble
            // phase (StopImmediatePropagation), so we must catch it on the way DOWN.
            b.RegisterCallback<PointerDownEvent>(_ => { if (dir < 0) _input?.SteerLeftDown(); else _input?.SteerRightDown(); }, TrickleDown.TrickleDown);
            b.RegisterCallback<PointerUpEvent>(_ => ReleaseSteer(dir));
            b.RegisterCallback<PointerLeaveEvent>(_ => ReleaseSteer(dir));
        }

        private void ReleaseSteer(float dir)
        {
            if (_input == null) return;
            // only release if we're the side currently steering (so the other button isn't cancelled)
            if (dir < 0 && _input.Steer < 0) _input.SteerRelease();
            else if (dir > 0 && _input.Steer > 0) _input.SteerRelease();
        }

        private void HoldBoost(Button b)
        {
            if (b == null) return;
            // TrickleDown so the Button's Clickable doesn't swallow PointerDown (see HoldSteer).
            b.RegisterCallback<PointerDownEvent>(_ => _input?.SetBoostHeld(true), TrickleDown.TrickleDown);
            b.RegisterCallback<PointerUpEvent>(_ => _input?.SetBoostHeld(false));
            b.RegisterCallback<PointerLeaveEvent>(_ => _input?.SetBoostHeld(false));
        }

        // ---------- countdown / race events ----------
        private void OnCountdown(int n)
        {
            if (_countdown == null) return;
            StopAllCoroutines();
            StartCoroutine(CountdownPop(n));
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
            SetControlsVisible(true);
            if (showObjectiveAtStart && objectiveUI != null && !_objectiveShown)
            {
                _objectiveShown = true;
                Time.timeScale = 0f;            // freeze until the player acknowledges
                SetActive(objectiveUI, true);
            }
        }

        private void OnRaceFinished(bool won, int place)
        {
            Time.timeScale = 1f;
            SetControlsVisible(false);
            SetHidden(_scorePill, true);

            // Real run stats from RaceScore: placement bonus + stars + coin reward were computed in
            // RaceManager.FinishRace(). Coins scale with score and are paid on every placement.
            var s = _race != null ? _race.Score : null;
            int reward = s != null ? s.Coins : 0;
            if (reward > 0) GameManager.Instance?.AddCoins(reward);

            SetActive(levelCompleteUI, true);
            var lc = levelCompleteUI != null ? levelCompleteUI.GetComponent<LevelCompleteView>() : null;
            if (s != null)
                lc?.SetResults(won, s.Stars, s.Score, s.BestCombo, s.TricksLanded, s.Distance / 1000f, reward);
            else
                lc?.SetResults(won, won ? 3 : 1, 0, 0, 0, 0f, reward);
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
