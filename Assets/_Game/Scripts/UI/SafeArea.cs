using UnityEngine;

namespace Mixtape.UI
{
    /// <summary>
    /// Resizes a RectTransform to the device safe area so UI isn't hidden by notches /
    /// rounded corners on iOS/Android. Put on a full-screen child of the Canvas and
    /// parent interactive UI under it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform _rt;
        private Rect _last;
        private ScreenOrientation _lastOrient;

        private void Awake() => _rt = GetComponent<RectTransform>();
        private void OnEnable() => Apply();
        private void Update()
        {
            if (_last != Screen.safeArea || _lastOrient != Screen.orientation) Apply();
        }

        private void Apply()
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            var sa = Screen.safeArea;
            _last = sa; _lastOrient = Screen.orientation;

            Vector2 min = sa.position;
            Vector2 max = sa.position + sa.size;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;

            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
