using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mixtape.UITK
{
    /// <summary>
    /// Objective overlay (UI Toolkit). Standalone UIDocument shown e.g. at level
    /// start. The panel body is intentionally empty — objective text/content is
    /// filled in by the integration layer later. OK confirms/closes.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ObjectiveView : MonoBehaviour
    {
        public Action onOk;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            var ok = root.Q<Button>("obj-ok");
            if (ok != null) ok.clicked += () => { Debug.Log("[Objective] OK"); onOk?.Invoke(); };
        }
    }
}
