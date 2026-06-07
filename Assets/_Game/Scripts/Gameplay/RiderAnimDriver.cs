using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Drives the spawned character + board animators (and wheel spin) from the
    /// <see cref="PhysicsSkater"/> state. Lives on the rider root alongside RiderVisual and
    /// PhysicsSkater, so it works for BOTH the player and the AI with no per-scene wiring.
    /// RiderVisual adds this automatically when it builds the models.
    /// </summary>
    [RequireComponent(typeof(RiderVisual))]
    public class RiderAnimDriver : MonoBehaviour
    {
        [Tooltip("Wheel spin (deg/sec) per unit of skater speed.")]
        public float wheelDegPerSpeed = 45f;

        public string speedParam = "Speed";
        public string groundedParam = "Grounded";
        public string jumpTrigger = "Jump";
        public string boostParam = "Boost";
        public string pushTrigger = "Push";
        public string trickTrigger = "Trick";

        private RiderVisual _rider;
        private PhysicsSkater _ps;
        private Animator _anim;
        private Animator _board;
        private WheelSpinner _wheels;

        private int _sH, _gH, _jH, _bH, _pH, _tH;
        private bool _wasGrounded = true;
        private bool _wasMoving;
        private bool _trickPending;   // next takeoff animates as a kickflip (set by Trick())

        private void Awake()
        {
            _rider = GetComponent<RiderVisual>();
            _ps = GetComponent<PhysicsSkater>();
            _sH = Animator.StringToHash(speedParam);
            _gH = Animator.StringToHash(groundedParam);
            _jH = Animator.StringToHash(jumpTrigger);
            _bH = Animator.StringToHash(boostParam);
            _pH = Animator.StringToHash(pushTrigger);
            _tH = Animator.StringToHash(trickTrigger);
        }

        /// <summary>Trick jump: launches with the same jump as the jump button, but flags this
        /// takeoff so the board does a KICKFLIP instead of the normal ollie. Only from the ground
        /// (it consumes the jump). Driven by the HUD trick button + keyboard trick key.</summary>
        public void Trick()
        {
            if (_ps == null || !_ps.IsGrounded) return;
            _ps.JumpRequested = true;   // real jump (same height as the jump button)
            _trickPending = true;       // the next takeoff animates as a kickflip
        }

        private void Grab()
        {
            if (_rider == null) return;
            if (_anim == null) _anim = _rider.SpawnedAnimator;
            if (_board == null) _board = _rider.SpawnedBoardAnimator;
            if (_wheels == null) _wheels = _rider.SpawnedWheels;
        }

        private void Update()
        {
            Grab();
            if (_ps == null) return;

            float speed = _ps.Speed;
            bool grounded = _ps.IsGrounded;
            bool boosting = _ps.IsBoosting;

            if (_anim != null)
            {
                _anim.SetFloat(_sH, speed);
                _anim.SetBool(_gH, grounded);
                _anim.SetBool(_bH, boosting);
            }
            if (_board != null) _board.SetBool(_bH, boosting);

            // Takeoff = the moment we leave the ground (works for player input and AI alike).
            // The rider ollies either way; the board kickflips if this was a trick jump.
            if (_wasGrounded && !grounded)
            {
                if (_anim != null) _anim.SetTrigger(_jH);
                if (_board != null) _board.SetTrigger(_trickPending ? _tH : _jH);
                _trickPending = false;
            }
            _wasGrounded = grounded;

            // Kick-push when starting to roll from a near stop.
            bool moving = speed > 0.5f;
            if (moving && !_wasMoving && grounded && _anim != null) _anim.SetTrigger(_pH);
            _wasMoving = moving;

            if (_wheels != null) _wheels.SetSpeed(speed * wheelDegPerSpeed);
        }
    }
}
