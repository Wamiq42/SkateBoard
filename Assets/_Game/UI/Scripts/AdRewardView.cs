using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mixtape.UITK
{
    /// <summary>
    /// Ad-reward overlay (UI Toolkit). Standalone UIDocument; GET NOW triggers the
    /// rewarded ad at the call site (wired later). Null-guarded for the sandbox.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class AdRewardView : MonoBehaviour
    {
        public Action onGetNow;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            var get = root.Q<Button>("ar-getnow");
            if (get != null) get.clicked += () => { Debug.Log("[AdReward] GET NOW"); onGetNow?.Invoke(); };
        }
    }
}
