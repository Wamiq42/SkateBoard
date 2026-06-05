using UnityEngine;

namespace Mixtape.UI
{
    /// <summary>
    /// Toggles between the front-end screens that now live as panels inside the single
    /// MainMenu scene (Menu / Character select / Board select). Replaces the old
    /// per-screen scenes — navigation is panel SetActive instead of scene loads.
    /// Re-activating a panel fires its controller's OnEnable, which re-initialises it.
    /// </summary>
    public class ScreenManager : MonoBehaviour
    {
        public enum Screen { Menu, Character, Board }

        public static ScreenManager Instance { get; private set; }

        [Header("Panels")]
        public GameObject menuPanel;
        public GameObject characterPanel;
        public GameObject boardPanel;

        private void Awake() => Instance = this;

        private void Start() => Show(Screen.Menu);

        public void Show(Screen screen)
        {
            if (menuPanel) menuPanel.SetActive(screen == Screen.Menu);
            if (characterPanel) characterPanel.SetActive(screen == Screen.Character);
            if (boardPanel) boardPanel.SetActive(screen == Screen.Board);
        }

        /// <summary>Null-safe static entry point for controllers and UnityEvents.</summary>
        public static void Go(Screen screen) => Instance?.Show(screen);
    }
}
