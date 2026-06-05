using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Mixtape.Core;
using Mixtape.Gameplay;
using Mixtape.InputCtrl;
using Mixtape.Ads;

namespace Mixtape.UI
{
    /// <summary>
    /// In-race HUD: countdown, coins, position, the touch controls, pause, and the
    /// hand-off to the results screen on finish. Buttons are wired by the HUD builder.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Refs (set by builder)")]
        public Text countdownText;
        public Text coinText;
        public Text positionText;
        public GameObject pausePanel;
        public GameObject controlsRoot;
        public ResultsController results;

        private InputService _input;
        private RaceManager _race;

        private void Start()
        {
            _input = InputService.Instance;
            _race = RaceManager.Instance;
            if (pausePanel) pausePanel.SetActive(false);
            if (controlsRoot) controlsRoot.SetActive(false); // revealed when the race starts

            if (_race != null)
            {
                _race.CountdownTick += OnCountdown;
                _race.RaceStarted += OnRaceStarted;
                _race.RaceFinished += OnFinished;
            }
            UpdateCoins();
        }

        private void OnDestroy()
        {
            if (_race != null)
            {
                _race.CountdownTick -= OnCountdown;
                _race.RaceStarted -= OnRaceStarted;
                _race.RaceFinished -= OnFinished;
            }
        }

        private void Update()
        {
            if (_race != null && positionText != null)
                positionText.text = $"{_race.PlayerPlace}/{_race.RacerCount}";
        }

        private void UpdateCoins()
        {
            if (coinText != null && GameManager.Instance != null)
                coinText.text = GameManager.Instance.Data.coins.ToString("N0");
        }

        private void OnCountdown(int n)
        {
            if (countdownText == null) return;
            StopAllCoroutines();
            StartCoroutine(ShowCountdown(n));
        }

        private IEnumerator ShowCountdown(int n)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = n > 0 ? n.ToString() : "GO!";
            countdownText.transform.localScale = Vector3.one * 1.4f;
            float t = 0f;
            while (t < 0.8f) { t += Time.unscaledDeltaTime; countdownText.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, t / 0.3f); yield return null; }
            if (n == 0) countdownText.gameObject.SetActive(false);
        }

        private void OnRaceStarted()
        {
            if (controlsRoot) controlsRoot.SetActive(true);
        }

        private void OnFinished(bool won, int place)
        {
            if (controlsRoot) controlsRoot.SetActive(false);
            if (results != null) results.Show(won, place);
        }

        // ---- Control button hooks ----
        public void SteerLeftDown() => _input?.SteerLeftDown();
        public void SteerLeftUp() { if (_input != null && _input.Steer < 0) _input.SteerRelease(); }
        public void SteerRightDown() => _input?.SteerRightDown();
        public void SteerRightUp() { if (_input != null && _input.Steer > 0) _input.SteerRelease(); }
        public void SteerReleaseAny() => _input?.SteerRelease();
        public void Jump() => _input?.PressJump();
        public void BoostDown() => _input?.SetBoostHeld(true);
        public void BoostUp() => _input?.SetBoostHeld(false);

        public void AdBoost()
        {
            AdManager.ShowRewarded("speed_boost", ok =>
            {
                if (ok && _race != null && _race.player != null)
                    _race.player.ApplyBoost(2.2f, 4f);
            });
        }

        public void TogglePause()
        {
            bool show = pausePanel != null && !pausePanel.activeSelf;
            if (pausePanel) pausePanel.SetActive(show);
            Time.timeScale = show ? 0f : 1f;
        }

        public void Resume()
        {
            if (pausePanel) pausePanel.SetActive(false);
            Time.timeScale = 1f;
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            GameManager.Instance?.ReloadCurrent();
        }

        public void Home()
        {
            Time.timeScale = 1f;
            GameManager.Instance?.LoadMainMenu();
        }
    }
}
