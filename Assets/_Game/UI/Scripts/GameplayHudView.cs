using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mixtape.UITK
{
    /// <summary>
    /// UI Toolkit in-race HUD (sandbox rebuild). Holds only the player control
    /// buttons (steer left/right, jump, double-jump, booster) and pause.
    /// Built UI-only for now: the buttons just log. They get wired to the real
    /// gameplay (InputService / PhysicsSkater / pause) once every UITK panel is
    /// built and a UITK screen router replaces the Canvas HUDController.
    /// Null-guarded so it runs standalone in the UIToolkitTesting scene.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameplayHudView : MonoBehaviour
    {
        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;

            // Steering and jumps are press-and-hold in-game; wiring will use
            // PointerDown/Up. For the UI-only pass a click stub is enough.
            Bind(root, "left-btn",     () => Debug.Log("[GameplayHudView] STEER LEFT (not wired yet)"));
            Bind(root, "right-btn",    () => Debug.Log("[GameplayHudView] STEER RIGHT (not wired yet)"));
            Bind(root, "jump-btn",     () => Debug.Log("[GameplayHudView] JUMP (not wired yet)"));
            Bind(root, "djump-btn",    () => Debug.Log("[GameplayHudView] DOUBLE JUMP (not wired yet)"));
            Bind(root, "booster-btn",  () => Debug.Log("[GameplayHudView] BOOSTER (green) (not wired yet)"));
            Bind(root, "booster2-btn", () => Debug.Log("[GameplayHudView] BOOSTER (white) (not wired yet)"));
            Bind(root, "pause-btn",    () => Debug.Log("[GameplayHudView] PAUSE (not wired yet)"));
        }

        private static void Bind(VisualElement root, string name, Action cb)
        {
            var btn = root.Q<Button>(name);
            if (btn != null) btn.clicked += cb;
        }
    }
}
