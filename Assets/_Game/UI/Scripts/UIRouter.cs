using UnityEngine;
using Mixtape.Core;

namespace Mixtape.UITK
{
    /// <summary>
    /// Front-end screen router for the UI Toolkit rebuild. Replaces the legacy uGUI
    /// <c>ScreenManager</c>: the Menu / Character / Board screens live as sibling
    /// <see cref="UnityEngine.UIElements.UIDocument"/> GameObjects in the MainMenu scene
    /// and are toggled active one at a time (navigation is SetActive, not scene loads).
    ///
    /// It also owns the navigation graph by assigning each view's nav hooks:
    ///   Menu PLAY            -> Character
    ///   Character SELECT     -> Board   (SELECT confirms the character and advances)
    ///   Character BACK       -> Menu
    ///   Board NEXT           -> Game    (through the Loading scene)
    ///   Board BACK           -> Character
    /// Hooks are assigned in Awake (GetComponent works on inactive GameObjects) and the
    /// view button bindings invoke them lazily, so assignment order doesn't matter.
    /// </summary>
    public class UIRouter : MonoBehaviour
    {
        public enum Screen { Menu, Character, Board }

        public static UIRouter Instance { get; private set; }

        [Header("Screen UIDocument GameObjects")]
        public GameObject menuUI;
        public GameObject characterUI;
        public GameObject boardUI;

        private void Awake()
        {
            Instance = this;

            var menu = menuUI != null ? menuUI.GetComponent<MainMenuView>() : null;
            var chr  = characterUI != null ? characterUI.GetComponent<CharacterSelectView>() : null;
            var brd  = boardUI != null ? boardUI.GetComponent<BoardSelectView>() : null;

            if (menu != null) menu.onPlay = () => Show(Screen.Character);
            if (chr != null)
            {
                chr.onProceed = () => Show(Screen.Board);
                chr.onBack    = () => Show(Screen.Menu);
            }
            if (brd != null)
            {
                brd.onNext = () => SceneFlow.LoadVia(SceneFlow.Game);
                brd.onBack = () => Show(Screen.Character);
            }
        }

        private void Start() => Show(Screen.Menu);

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Show(Screen screen)
        {
            if (menuUI != null)      menuUI.SetActive(screen == Screen.Menu);
            if (characterUI != null) characterUI.SetActive(screen == Screen.Character);
            if (boardUI != null)     boardUI.SetActive(screen == Screen.Board);
        }

        /// <summary>Null-safe static entry point.</summary>
        public static void Go(Screen screen) => Instance?.Show(screen);
    }
}
