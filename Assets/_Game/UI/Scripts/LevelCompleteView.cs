using System;
using UnityEngine;
using UnityEngine.UIElements;
using Mixtape.Core;

namespace Mixtape.UITK
{
    /// <summary>
    /// Level Complete results screen (UI Toolkit). Shows score/combo/tricks/distance/coins
    /// and Main Menu / Restart / Next. The race stats aren't in the data model yet, so the
    /// game calls <see cref="SetResults"/> to populate them; the UXML defaults to the mock
    /// values for preview. Null-guarded for the sandbox.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LevelCompleteView : MonoBehaviour
    {
        public Action onMenu, onRestart, onNext;

        private Label _score, _combo, _tricks, _distance, _coins;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;

            _score    = root.Q<Label>("val-score");
            _combo    = root.Q<Label>("val-combo");
            _tricks   = root.Q<Label>("val-tricks");
            _distance = root.Q<Label>("val-distance");
            _coins    = root.Q<Label>("val-coins");

            Bind(root, "lc-menu",    () => { Debug.Log("[LevelComplete] MAIN MENU"); onMenu?.Invoke();    GameManager.Instance?.LoadMainMenu(); });
            Bind(root, "lc-restart", () => { Debug.Log("[LevelComplete] RESTART");   onRestart?.Invoke(); GameManager.Instance?.ReloadCurrent(); });
            Bind(root, "lc-next",    () => { Debug.Log("[LevelComplete] NEXT");      onNext?.Invoke(); });
        }

        /// <summary>Populate the results (called by the game at race end).</summary>
        public void SetResults(int score, int bestCombo, int tricksLanded, float distanceKm, int coinsEarned)
        {
            if (_score != null)    _score.text    = score.ToString("N0");
            if (_combo != null)    _combo.text    = "X" + bestCombo;
            if (_tricks != null)   _tricks.text   = tricksLanded.ToString();
            if (_distance != null) _distance.text = distanceKm.ToString("0.00") + " KM";
            if (_coins != null)    _coins.text    = coinsEarned.ToString("N0");
        }

        private static void Bind(VisualElement root, string name, Action cb)
        {
            var b = root.Q<Button>(name);
            if (b != null) b.clicked += cb;
        }
    }
}
