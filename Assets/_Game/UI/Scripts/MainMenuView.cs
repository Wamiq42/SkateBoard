using System;
using UnityEngine;
using UnityEngine.UIElements;
using Mixtape.Core;
using Mixtape.Ads;
using Mixtape.Data;

namespace Mixtape.UITK
{
    /// <summary>
    /// UI Toolkit main-menu view (sandbox rebuild). Mirrors the behaviour of the
    /// legacy uGUI <c>MainMenuController</c> but drives a UIDocument instead.
    /// Lives in its own namespace and scene so it never collides with the Canvas UI.
    /// Every singleton access is null-guarded so the screen also runs standalone
    /// in the UIToolkitTesting scene (no Boot/GameManager required).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuView : MonoBehaviour
    {
        [Header("Links")]
        [Tooltip("Edit URLs here. If unassigned, the inline fallbacks below are used.")]
        public AppLinks links;
        [Header("Fallback URLs (used only when no AppLinks asset is assigned)")]
        public string rateUrl = "https://play.google.com/store";
        public string moreGamesUrl = "https://play.google.com/store";
        public string privacyUrl = "https://example.com/privacy";

        /// <summary>Nav hook: PLAY pressed. Assigned by <see cref="UIRouter"/>.</summary>
        public System.Action onPlay;

        private Label _coinLabel;
        private Button _soundOn;
        private Button _soundOff;
        private VisualElement _settingsPopup;
        private VisualElement _exitPopup;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;

            _coinLabel     = root.Q<Label>("coin-label");
            _settingsPopup = root.Q<VisualElement>("settings-popup");
            _exitPopup     = root.Q<VisualElement>("exit-popup");
            _soundOn       = root.Q<Button>("sound-on");
            _soundOff      = root.Q<Button>("sound-off");

            Bind(root, "play-btn",     Play);
            Bind(root, "settings-btn", OpenSettings);
            Bind(root, "rate-btn",     RateUs);
            Bind(root, "more-btn",     MoreGames);
            Bind(root, "privacy-btn",  Privacy);
            Bind(root, "exit-btn",     OpenExit);
            Bind(root, "watchad-btn",  WatchAdForCoins);

            Bind(root, "settings-save", CloseSettings);
            Bind(root, "sound-on",  () => SetSound(true));
            Bind(root, "sound-off", () => SetSound(false));
            Bind(root, "exit-yes",      ConfirmExit);
            Bind(root, "exit-no",       CloseExit);

            SetHidden(_settingsPopup, true);
            SetHidden(_exitPopup, true);
            RefreshCoins();
            RefreshToggles();
        }

        // OnEnable can run BEFORE GameManager.Awake on a cold scene load (Unity doesn't
        // order them), so the first RefreshCoins may miss the save. Start runs after all
        // Awakes, guaranteeing the real value lands.
        private void Start() => RefreshCoins();

        private static void Bind(VisualElement root, string name, Action cb)
        {
            var btn = root.Q<Button>(name);
            if (btn != null) btn.clicked += cb;
        }

        private static void SetHidden(VisualElement ve, bool hidden)
        {
            if (ve == null) return;
            ve.EnableInClassList("is-hidden", hidden);
        }

        private void RefreshCoins()
        {
            if (_coinLabel == null) return;
            int coins = GameManager.Instance != null ? GameManager.Instance.Data.coins : 0;
            _coinLabel.text = coins.ToString("N0");
        }

        private void RefreshToggles()
        {
            bool on = GameManager.Instance == null || GameManager.Instance.Data.soundOn;
            if (_soundOn != null)
            {
                _soundOn.EnableInClassList("seg--sel", on);
                _soundOn.EnableInClassList("seg--unsel", !on);
            }
            if (_soundOff != null)
            {
                _soundOff.EnableInClassList("seg--sel", !on);
                _soundOff.EnableInClassList("seg--unsel", on);
            }
        }

        // ---- actions (mirror MainMenuController) ----
        private void Play()
        {
            // UIRouter assigns onPlay (-> Character screen). In the standalone sandbox
            // (no router) this just logs.
            if (onPlay != null) onPlay();
            else Debug.Log("[MainMenuView] PLAY pressed (no UIRouter in sandbox)");
        }

        private void OpenSettings() => SetHidden(_settingsPopup, false);
        private void CloseSettings()
        {
            GameManager.Instance?.SaveData();
            SetHidden(_settingsPopup, true);
        }

        private void OpenExit() => SetHidden(_exitPopup, false);
        private void CloseExit() => SetHidden(_exitPopup, true);
        private void ConfirmExit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void SetSound(bool on)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.Data.soundOn = on;
                GameManager.Instance.SaveData();
            }
            RefreshToggles();
        }

        private void WatchAdForCoins()
        {
            AdManager.ShowRewarded("menu_free_coins", ok =>
            {
                if (ok) { GameManager.Instance?.AddCoins(10); RefreshCoins(); }
            });
        }

        private void RateUs() => Application.OpenURL(links != null ? links.rateUrl : rateUrl);
        private void MoreGames() => Application.OpenURL(links != null ? links.moreGamesUrl : moreGamesUrl);
        private void Privacy() => Application.OpenURL(links != null ? links.privacyUrl : privacyUrl);
    }
}
