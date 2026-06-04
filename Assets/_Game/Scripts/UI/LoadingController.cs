using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Mixtape.Core;

namespace Mixtape.UI
{
    /// <summary>Async-loads <see cref="SceneFlow.Target"/> while animating a progress bar.</summary>
    public class LoadingController : MonoBehaviour
    {
        [Header("Refs (set by builder)")]
        public Image barFill;       // Image type = Filled (Horizontal)
        public Text percentText;
        public Text dotsText;

        [Tooltip("Minimum time the loading screen stays up, for readability.")]
        public float minDisplay = 1.2f;

        private void Start() => StartCoroutine(LoadRoutine());

        private IEnumerator LoadRoutine()
        {
            float shown = 0f;
            var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(SceneFlow.Target);
            op.allowSceneActivation = false;

            float display = 0f;
            while (!op.isDone)
            {
                shown += Time.deltaTime;
                float real = Mathf.Clamp01(op.progress / 0.9f);
                display = Mathf.MoveTowards(display, real, Time.deltaTime * 0.8f);
                SetProgress(display);

                if (display >= 0.999f && shown >= minDisplay)
                {
                    SetProgress(1f);
                    op.allowSceneActivation = true;
                }
                yield return null;
            }
        }

        private void SetProgress(float p)
        {
            if (barFill) barFill.fillAmount = p;
            if (percentText) percentText.text = Mathf.RoundToInt(p * 100f) + "%";
            if (dotsText)
            {
                int d = (int)(Time.time * 3f) % 4;
                dotsText.text = new string('.', d);
            }
        }
    }
}
