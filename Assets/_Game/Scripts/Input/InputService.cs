using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mixtape.InputCtrl
{
    /// <summary>
    /// Single source of truth for gameplay input. Merges editor keyboard input with
    /// virtual buttons driven by on-screen UI (touch). Gameplay code reads only from
    /// here, so it never cares whether input came from a finger or a key.
    ///
    /// UI buttons call SetSteer / PressJump / SetBoostHeld; keyboard is polled here.
    /// </summary>
    public class InputService : MonoBehaviour
    {
        public static InputService Instance { get; private set; }

        // Virtual (UI) state.
        private float _uiSteer;        // -1 left, +1 right
        private bool _uiBoostHeld;
        private bool _jumpQueued;
        private bool _trickQueued;

        /// <summary>Combined steer axis in [-1, 1]. Keyboard overrides when pressed.</summary>
        public float Steer
        {
            get
            {
                float kb = KeyboardSteer();
                return Mathf.Abs(kb) > 0.01f ? kb : _uiSteer;
            }
        }

        /// <summary>True for one frame when a jump was requested (UI tap or key down).</summary>
        public bool JumpPressedThisFrame { get; private set; }

        /// <summary>True for one frame when a trick was requested (UI tap or key down).</summary>
        public bool TrickPressedThisFrame { get; private set; }

        /// <summary>True while a speed boost input is held.</summary>
        public bool BoostHeld => _uiBoostHeld || KeyboardBoost();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            JumpPressedThisFrame = _jumpQueued || KeyboardJumpDown();
            _jumpQueued = false;
            TrickPressedThisFrame = _trickQueued || KeyboardTrickDown();
            _trickQueued = false;
        }

        // ---- Called by UI buttons ----
        public void SetSteer(float value) => _uiSteer = Mathf.Clamp(value, -1f, 1f);
        public void SteerLeftDown() => _uiSteer = -1f;
        public void SteerRightDown() => _uiSteer = 1f;
        public void SteerRelease() => _uiSteer = 0f;
        public void PressJump() => _jumpQueued = true;
        public void PressTrick() => _trickQueued = true;
        public void SetBoostHeld(bool held) => _uiBoostHeld = held;

        // ---- Keyboard (editor / desktop testing) ----
        private static float KeyboardSteer()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return 0f;
            float v = 0f;
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) v -= 1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) v += 1f;
            return v;
#else
            float v = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) v -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) v += 1f;
            return v;
#endif
        }

        private static bool KeyboardJumpDown()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
#endif
        }

        private static bool KeyboardTrickDown()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && (kb.fKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.DownArrow);
#endif
        }

        private static bool KeyboardBoost()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb.leftShiftKey.isPressed;
#else
            return Input.GetKey(KeyCode.LeftShift);
#endif
        }
    }
}
