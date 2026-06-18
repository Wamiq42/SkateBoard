using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mixtape.UITK
{
    /// <summary>
    /// "You fell!" overlay (UI Toolkit). Shown when the player drops off the track. Three
    /// choices: watch a rewarded ad to RESUME from the fall point, RESTART the race from the
    /// start, or go HOME. Handlers are hooks wired by <see cref="GameplayHudView"/>.
    /// Null-guarded so it also runs standalone in the sandbox.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ResumeView : MonoBehaviour
    {
        /// <summary>Watch-ad → respawn at the fall point and keep racing.</summary>
        public Action onResumeAd;
        /// <summary>Restart the whole race (no cutscene).</summary>
        public Action onRestart;
        /// <summary>Back to the main menu.</summary>
        public Action onHome;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            Bind(root, "resume-ad",      () => { Debug.Log("[Resume] WATCH-AD RESUME"); onResumeAd?.Invoke(); });
            Bind(root, "resume-restart", () => { Debug.Log("[Resume] RESTART");         onRestart?.Invoke(); });
            Bind(root, "resume-home",    () => { Debug.Log("[Resume] HOME");            onHome?.Invoke(); });
        }

        private static void Bind(VisualElement root, string name, Action cb)
        {
            var b = root.Q<Button>(name);
            if (b != null) b.clicked += cb;
        }
    }
}
