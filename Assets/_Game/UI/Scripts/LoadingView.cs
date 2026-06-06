using UnityEngine;
using UnityEngine.UIElements;
using Mixtape.Core;

namespace Mixtape.UITK
{
    /// <summary>
    /// UI Toolkit loading view (sandbox rebuild). Shows the logo, coin pill and a
    /// bottom progress bar. In the sandbox it just animates a simulated 0->100%
    /// loop so the screen can be previewed; the real Loading scene async-loads the
    /// Game scene, so once this UITK screen is wired in, drive <see cref="SetProgress"/>
    /// from the <c>AsyncOperation.progress</c> instead of the fake timer.
    /// Every singleton access is null-guarded so it runs standalone in the
    /// UIToolkitTesting scene (no Boot/GameManager required).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LoadingView : MonoBehaviour
    {
        [Tooltip("Seconds for the simulated fill to sweep 0->100% (preview only).")]
        public float fakeDuration = 2.5f;
        [Tooltip("Loop the simulated fill so the sandbox preview keeps animating.")]
        public bool loopPreview = true;

        private Label _coinLabel;
        private Label _pctLabel;
        private VisualElement _fill;
        private float _t;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;

            _coinLabel = root.Q<Label>("coin-label");
            _pctLabel  = root.Q<Label>("load-pct");
            _fill      = root.Q<VisualElement>("load-fill");

            _t = 0f;
            RefreshCoins();
            SetProgress(0f);
        }

        private void Update()
        {
            if (fakeDuration <= 0f) return;
            _t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(_t / fakeDuration);
            SetProgress(p);
            if (p >= 1f && loopPreview) _t = 0f;
        }

        /// <summary>Drive the bar + percentage from a normalised [0..1] progress.</summary>
        public void SetProgress(float p01)
        {
            p01 = Mathf.Clamp01(p01);
            if (_fill != null) _fill.style.width = Length.Percent(p01 * 100f);
            if (_pctLabel != null) _pctLabel.text = Mathf.RoundToInt(p01 * 100f) + "%";
        }

        private void RefreshCoins()
        {
            if (_coinLabel == null) return;
            int coins = GameManager.Instance != null ? GameManager.Instance.Data.coins : 7000;
            _coinLabel.text = coins.ToString("N0");
        }
    }
}
