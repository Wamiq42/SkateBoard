using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mixtape.UITK
{
    /// <summary>
    /// Small UI Toolkit animation helpers shared across the menu views.
    /// </summary>
    public static class UIFx
    {
        /// <summary>
        /// Looping "breathing" scale pulse for call-to-action buttons (Play, Watch Ad, Select…).
        /// Eases scale 1 → <paramref name="peak"/> → 1 forever on a cosine curve. Runs on UNSCALED
        /// time so it keeps animating in menus and while the game is paused. Start it from a
        /// MonoBehaviour with <c>StartCoroutine(UIFx.Pulse(...))</c>; it stops automatically when the
        /// host GameObject is disabled. Driving the scale inline overrides the USS hover/press
        /// transition, which is the intended trade for a constantly-animating button.
        /// </summary>
        public static IEnumerator Pulse(VisualElement[] elems, float peak = 1.07f, float period = 0.85f)
        {
            if (elems == null || elems.Length == 0) yield break;
            float t = 0f;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                float k = 0.5f - 0.5f * Mathf.Cos((t / Mathf.Max(0.01f, period)) * Mathf.PI * 2f); // 0..1..0
                float s = Mathf.Lerp(1f, peak, k);
                var scale = new StyleScale(new Scale(new Vector3(s, s, 1f)));
                foreach (var e in elems)
                    if (e != null) e.style.scale = scale;
                yield return null;
            }
        }

        /// <summary>
        /// Ensures a full-screen black overlay exists as the top-most child of <paramref name="root"/>
        /// and returns it. Used for hard cut-to-black transitions (e.g. race finish → restart). The
        /// overlay starts hidden; drive it with <see cref="Fade"/>.
        /// </summary>
        public static VisualElement EnsureFadeOverlay(VisualElement root)
        {
            if (root == null) return null;
            var overlay = root.Q<VisualElement>("fade-overlay");
            if (overlay == null)
            {
                overlay = new VisualElement { name = "fade-overlay" };
                overlay.style.position = Position.Absolute;
                overlay.style.left = 0; overlay.style.top = 0;
                overlay.style.right = 0; overlay.style.bottom = 0;
                overlay.style.backgroundColor = Color.black;
                overlay.style.opacity = 0f;
                overlay.style.display = DisplayStyle.None;
                overlay.pickingMode = PickingMode.Ignore;
                root.Add(overlay);
            }
            overlay.BringToFront();   // always on top, even if added before later elements
            return overlay;
        }

        /// <summary>
        /// Fades an overlay's opacity from <paramref name="from"/> to <paramref name="to"/> over
        /// <paramref name="duration"/> seconds of UNSCALED time (so it works while the game is paused
        /// or time-scaled at finish). While the overlay is at all visible it swallows input; once it
        /// reaches full transparency it is hidden and stops blocking. Drives the overlay returned by
        /// <see cref="EnsureFadeOverlay"/>.
        /// </summary>
        public static IEnumerator Fade(VisualElement overlay, float from, float to, float duration)
        {
            if (overlay == null) yield break;
            overlay.style.display = DisplayStyle.Flex;
            overlay.pickingMode = PickingMode.Position;   // block taps for the whole transition
            overlay.style.opacity = from;
            float t = 0f;
            while (t < duration && duration > 0f)
            {
                t += Time.unscaledDeltaTime;
                overlay.style.opacity = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            overlay.style.opacity = to;
            if (to <= 0.001f)
            {
                overlay.style.display = DisplayStyle.None;   // fully clear → let gameplay receive input
                overlay.pickingMode = PickingMode.Ignore;
            }
        }
    }
}
