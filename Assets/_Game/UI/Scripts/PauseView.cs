using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mixtape.UITK
{
    /// <summary>
    /// Pause overlay (UI Toolkit). Standalone UIDocument shown over gameplay.
    /// Button handlers are hooks the game integration wires later; for now they
    /// log and invoke optional callbacks. Null-guarded so it runs in the sandbox.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PauseView : MonoBehaviour
    {
        public Action onResume, onRestart, onHome;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            Bind(root, "pause-resume",  () => { Debug.Log("[Pause] RESUME");  onResume?.Invoke(); });
            Bind(root, "pause-restart", () => { Debug.Log("[Pause] RESTART"); onRestart?.Invoke(); });
            Bind(root, "pause-home",    () => { Debug.Log("[Pause] HOME");    onHome?.Invoke(); });
        }

        private static void Bind(VisualElement root, string name, Action cb)
        {
            var b = root.Q<Button>(name);
            if (b != null) b.clicked += cb;
        }
    }
}
