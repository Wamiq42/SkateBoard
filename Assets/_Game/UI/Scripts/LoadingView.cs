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
            Debug.Log($"[Loading] showing, target='{SceneFlow.Target}'");

            // Guarantee the loading UI paints BEFORE the heavy load begins: kicking off
            // LoadSceneAsync on the screen's very first frame can stall the main thread
            // (editor especially) before anything was ever drawn.
            yield return null;
            yield return null;

            var op = SceneManager.LoadSceneAsync(SceneFlow.Target);
            op.allowSceneActivation = false;

            // The bar is TIME-driven (a fixed minDisplay-seconds sweep), not progress-driven:
            // real async progress finishes near-instantly in the editor, which made the screen
            // activate after 1-2 rendered frames. The sweep only advances on frames the player
            // actually sees (dt clamped — load stalls deliver multi-second deltas), and holds
            // at 95% if the real load is genuinely slower than the sweep.
            float shown = 0f;
            int frames = 0;
            while (true)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                shown += dt;
                frames++;

                bool ready = op.progress >= 0.9f;               // async progress caps at 0.9 until activation
                float sweep = Mathf.Clamp01(shown / Mathf.Max(0.1f, minDisplay));
                SetProgress(ready ? sweep : Mathf.Min(sweep, 0.95f));

                if (sweep >= 1f && ready) break;
                yield return null;
            }

            SetProgress(1f);
            Debug.Log($"[Loading] held {shown:0.00}s over {frames} rendered frames, activating '{SceneFlow.Target}'");
            op.allowSceneActivation = true;
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
