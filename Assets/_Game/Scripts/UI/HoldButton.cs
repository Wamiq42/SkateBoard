using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace Mixtape.UI
{
    /// <summary>
    /// A button that reports press-and-hold (pointer down / up) rather than just click.
    /// Used for steering and boost-hold controls. Works for touch and mouse.
    /// </summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public UnityEvent onDown = new UnityEvent();
        public UnityEvent onUp = new UnityEvent();

        public bool IsHeld { get; private set; }

        public void OnPointerDown(PointerEventData e)
        {
            IsHeld = true;
            onDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData e) => Release();
        public void OnPointerExit(PointerEventData e) { if (IsHeld) Release(); }

        private void Release()
        {
            if (!IsHeld) return;
            IsHeld = false;
            onUp?.Invoke();
        }

        private void OnDisable() { if (IsHeld) Release(); }
    }
}
