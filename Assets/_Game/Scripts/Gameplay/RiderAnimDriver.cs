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

        private RiderVisual _rider;
        private PhysicsSkater _ps;
        private Animator _anim;
        private Animator _board;
        private WheelSpinner _wheels;

        private int _sH, _gH, _jH, _bH, _pH;
        private bool _wasGrounded = true;
        private bool _wasMoving;

        private void Awake()
        {
            _rider = GetComponent<RiderVisual>();
            _ps = GetComponent<PhysicsSkater>();
            _sH = Animator.StringToHash(speedParam);
            _gH = Animator.StringToHash(groundedParam);
            _jH = Animator.StringToHash(jumpTrigger);
            _bH = Animator.StringToHash(boostParam);
            _pH = Animator.StringToHash(pushTrigger);
        }

        private void OnEnable()  { if (_ps != null) _ps.Jumped += OnJumped; }
        private void OnDisable() { if (_ps != null) _ps.Jumped -= OnJumped; }

        // The first jump is already animated by the grounded->airborne edge below; this fires
        // the pop again on the mid-air double-jump (no ground edge to catch it otherwise).
        private void OnJumped(int n)
        {
            if (n < 2) return;
            Grab();
            if (_anim  != null) _anim.SetTrigger(_jH);
            if (_board != null) _board.SetTrigger(_jH);
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

            // Jump = the moment we leave the ground (works for player input and AI alike).
            if (_wasGrounded && !grounded)
            {
                if (_anim != null) _anim.SetTrigger(_jH);
                if (_board != null) _board.SetTrigger(_jH);
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
