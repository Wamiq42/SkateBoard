using UnityEngine;
using UnityEngine.UI;
using Mixtape.Core;
using Mixtape.Ads;

namespace Mixtape.UI
{
    /// <summary>Main menu logic: play, settings, exit, store/links, watch-ad for coins.</summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Refs (set by builder)")]
        public Text coinText;
        public GameObject settingsPanel;
        public GameObject exitPanel;

        [Header("Settings toggles")]
        public Image soundToggle;
        public Image musicToggle;
        public Sprite onSprite;
        public Sprite offSprite;

        [Header("Links")]
        public string rateUrl = "https://play.google.com/store";
        public string moreGamesUrl = "https://play.google.com/store";
        public string privacyUrl = "https://example.com/privacy";

        // OnEnable runs every time the menu panel is re-shown (e.g. returning from the
        // Character panel); Start guarantees a refresh once GameManager is ready on
        // first load. Both funnel through Refresh — re-running it is harmless.
        private void OnEnable() => Refresh();
        private void Start() => Refresh();

        private void Refresh()
        {
            RefreshCoins();
            if (settingsPanel) settingsPanel.SetActive(false);
            if (exitPanel) exitPanel.SetActive(false);
            RefreshToggles();
        }

        private void RefreshCoins()
        {
            if (coinText && GameManager.Instance != null)
                coinText.text = GameManager.Instance.Data.coins.ToString("N0");
        }

        public void Play() => ScreenManager.Go(ScreenManager.Screen.Character);

        public void OpenSettings() { if (settingsPanel) settingsPanel.SetActive(true); }
        public void CloseSettings()
        {
            GameManager.Instance?.SaveData();
            if (settingsPanel) settingsPanel.SetActive(false);
        }

        public void OpenExit() { if (exitPanel) exitPanel.SetActive(true); }
        public void CloseExit() { if (exitPanel) exitPanel.SetActive(false); }
        public void ConfirmExit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        public void ToggleSound()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.Data.soundOn = !GameManager.Instance.Data.soundOn;
            GameManager.Instance.SaveData();
            RefreshToggles();
        }

        public void ToggleMusic()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.Data.musicOn = !GameManager.Instance.Data.musicOn;
            GameManager.Instance.SaveData();
            AudioManager.Instance?.RefreshMusicState();
            RefreshToggles();
        }

        private void RefreshToggles()
        {
            if (GameManager.Instance == null) return;
            var d = GameManager.Instance.Data;
            if (soundToggle && onSprite && offSprite) soundToggle.sprite = d.soundOn ? onSprite : offSprite;
            if (musicToggle && onSprite && offSprite) musicToggle.sprite = d.musicOn ? onSprite : offSprite;
        }

        public void WatchAdForCoins()
        {
            AdManager.ShowRewarded("menu_free_coins", ok =>
            {
                if (ok) { GameManager.Instance?.AddCoins(10); RefreshCoins(); }
            });
        }

        public void RateUs() => Application.OpenURL(rateUrl);
        public void MoreGames() => Application.OpenURL(moreGamesUrl);
        public void Privacy() => Application.OpenURL(privacyUrl);
    }
}
