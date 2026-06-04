using UnityEngine;
using UnityEngine.UI;
using Mixtape.Core;

namespace Mixtape.UI
{
    /// <summary>
    /// End-of-race screen. Shows a win (Level Complete) or lose panel, grants coins,
    /// and offers Restart / Home / Next. Built by the HUD builder.
    /// </summary>
    public class ResultsController : MonoBehaviour
    {
        [Header("Refs (set by builder)")]
        public GameObject root;
        public GameObject completePanel;
        public GameObject losePanel;
        public Text titleText;
        public Text rewardText;
        public GameObject[] stars;     // optional star images for 1st/2nd/3rd

        [Header("Rewards by place (1-based)")]
        public int[] coinRewards = { 100, 50, 25 };

        private CanvasGroup _group;

        private void Awake()
        {
            EnsureGroup();
            SetVisible(false);
        }

        private void EnsureGroup()
        {
            if (_group != null || root == null) return;
            _group = root.GetComponent<CanvasGroup>();
            if (_group == null) _group = root.AddComponent<CanvasGroup>();
        }

        private void SetVisible(bool v)
        {
            EnsureGroup();
            if (_group != null)
            {
                _group.alpha = v ? 1f : 0f;
                _group.blocksRaycasts = v;
                _group.interactable = v;
            }
        }

        public void Show(bool won, int place)
        {
            SetVisible(true);
            if (completePanel) completePanel.SetActive(won);
            if (losePanel) losePanel.SetActive(!won);

            int reward = 0;
            if (won)
            {
                int idx = Mathf.Clamp(place - 1, 0, coinRewards.Length - 1);
                reward = coinRewards[idx];
                GameManager.Instance?.AddCoins(reward);
            }

            if (titleText) titleText.text = won ? "LEVEL COMPLETE" : "YOU LOST";
            if (rewardText) rewardText.text = $"+{reward}";

            if (stars != null)
                for (int i = 0; i < stars.Length; i++)
                    if (stars[i]) stars[i].SetActive(won && i < (4 - Mathf.Clamp(place, 1, 3)));
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

        public void Next() => Restart(); // single track for now; hook next level later
    }
}
