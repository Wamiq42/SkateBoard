using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Mixtape.Core;

namespace Mixtape.UITK
{
    /// <summary>
    /// UI Toolkit loading view. Shows the logo, coin pill and a bottom progress bar.
    ///
    /// In the real Loading scene set <see cref="autoLoadTarget"/> = true: it async-loads
    /// <see cref="SceneFlow.Target"/> (the Game scene) and drives the bar from the real
    /// <c>AsyncOperation.progress</c>, holding for <see cref="minDisplay"/> seconds for
    /// readability before activating. In the UIToolkitTesting sandbox leave it false and
    /// it just animates a simulated 0->100% loop so the screen can be previewed.
    /// Every singleton access is null-guarded.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LoadingView : MonoBehaviour
    {
        [Tooltip("True in the real Loading scene: async-loads SceneFlow.Target. False in the sandbox: fake preview sweep.")]
        public bool autoLoadTarget = false;
        [Tooltip("Minimum seconds the loading screen stays up before activating the next scene.")]
        public float minDisplay = 1.2f;

        [Header("Sandbox preview (autoLoadTarget = false)")]
        [Tooltip("Seconds for the simulated fill to sweep 0->100%.")]
        public float fakeDuration = 2.5f;
        [Tooltip("Loop the simulated fill so the sandbox preview keeps animating.")]
        public bool loopPreview = true;

        private UnityEngine.UIElements.Label _coinLabel;
        private UnityEngine.UIElements.Label _pctLabel;
        private UnityEngine.UIElements.VisualElement _fill;
        private float _t;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;

            _coinLabel = root.Q<UnityEngine.UIElements.Label>("coin-label");
            _pctLabel  = root.Q<UnityEngine.UIElements.Label>("load-pct");
            _fill      = root.Q<UnityEngine.UIElements.VisualElement>("load-fill");

            _t = 0f;
            RefreshCoins();
            SetProgress(0f);

            if (autoLoadTarget) StartCoroutine(LoadRoutine());
        }

        private void Update()
        {
            if (autoLoadTarget || fakeDuration <= 0f) return; // real load drives the bar itself
            _t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(_t / fakeDuration);
            SetProgress(p);
            if (p >= 1f && loopPreview) _t = 0f;
        }

        private IEnumerator LoadRoutine()
        {
            float shown = 0f;
            var op = SceneManager.LoadSceneAsync(SceneFlow.Target);
            op.allowSceneActivation = false;

            float display = 0f;
            while (!op.isDone)
            {
                shown += Time.unscaledDeltaTime;
                float real = Mathf.Clamp01(op.progress / 0.9f); // progress caps at 0.9 until activation
                display = Mathf.MoveTowards(display, real, Time.unscaledDeltaTime * 0.8f);
                SetProgress(display);

                if (display >= 0.999f && shown >= minDisplay)
                {
                    SetProgress(1f);
                    op.allowSceneActivation = true;
                }
                yield return null;
            }
        }

        /// <summary>Drive the bar + percentage from a normalised [0..1] progress.</summary>
        public void SetProgress(float p01)
        {
            p01 = Mathf.Clamp01(p01);
            if (_fill != null) _fill.style.width = UnityEngine.UIElements.Length.Percent(p01 * 100f);
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
