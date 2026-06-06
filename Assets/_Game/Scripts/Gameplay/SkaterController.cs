using UnityEngine;
using Mixtape.InputCtrl;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Player skater. Translates <see cref="InputService"/> into commands for the shared
    /// <see cref="RacerMotor"/> and drives the spawned character + board animators
    /// (Speed / Grounded / Jump / Boost / Push) plus the wheel spin. Animators are produced
    /// by <see cref="RiderVisual"/> at runtime, so refs are grabbed lazily.
    /// </summary>
    [RequireComponent(typeof(RacerMotor))]
    public class SkaterController : MonoBehaviour
    {
        [Header("Boost (UI / Shift)")]
        public float boostMultiplier = 1.6f;
        public float boostRefresh = 0.15f;

        [Header("Animator parameters")]
        public string speedParam = "Speed";
        public string groundedParam = "Grounded";
        public string jumpTrigger = "Jump";
        public string boostParam = "Boost";
        public string pushTrigger = "Push";

        [Header("Wheels")]
        [Tooltip("Wheel spin (deg/sec) per unit of rider speed.")]
        public float wheelDegPerSpeed = 120f;

        private RacerMotor _motor;
        private RiderVisual _rider;
        private Animator _animator;
        private Animator _boardAnimator;
        private WheelSpinner _wheels;

        private int _speedHash, _groundedHash, _jumpHash, _boostHash, _pushHash;
        private bool _wasMoving;

        private void Awake()
        {
            _motor = GetComponent<RacerMotor>();
            _rider = GetComponentInChildren<RiderVisual>();
            _speedHash = Animator.StringToHash(speedParam);
            _groundedHash = Animator.StringToHash(groundedParam);
            _jumpHash = Animator.StringToHash(jumpTrigger);
            _boostHash = Animator.StringToHash(boostParam);
            _pushHash = Animator.StringToHash(pushTrigger);
        }

        // RiderVisual builds in Start(), so the spawned animators may not exist yet in Awake.
        private void GrabRefs()
        {
            if (_rider == null) { _rider = GetComponentInChildren<RiderVisual>(); if (_rider == null) return; }
            if (_animator == null) _animator = _rider.SpawnedAnimator;
            if (_boardAnimator == null) _boardAnimator = _rider.SpawnedBoardAnimator;
            if (_wheels == null) _wheels = _rider.SpawnedWheels;
        }

        private void Update()
        {
            GrabRefs();

            var input = InputService.Instance;
            if (input == null || _motor.path == null) return;

            // Steering: map axis to a target offset across the road.
            _motor.targetLateral = input.Steer * _motor.path.RoadHalfWidth;

            // Jump / double-jump.
            if (input.JumpPressedThisFrame)
            {
                _motor.Jump();
                if (_animator != null) _animator.SetTrigger(_jumpHash);
                if (_boardAnimator != null) _boardAnimator.SetTrigger(_jumpHash);
            }

            // Speed boost while held.
            bool boosting = input.BoostHeld;
            if (boosting) _motor.ApplyBoost(boostMultiplier, boostRefresh);

            if (_animator != null)
            {
                _animator.SetFloat(_speedHash, _motor.CurrentSpeed);
                _animator.SetBool(_groundedHash, _motor.IsGrounded);
                _animator.SetBool(_boostHash, boosting);
            }
            if (_boardAnimator != null) _boardAnimator.SetBool(_boostHash, boosting);

            // Kick-push when starting to roll from a near stop.
            bool moving = _motor.CurrentSpeed > 0.5f;
            if (moving && !_wasMoving && _motor.IsGrounded && _animator != null)
                _animator.SetTrigger(_pushHash);
            _wasMoving = moving;

            // Spin the wheels with speed.
            if (_wheels != null) _wheels.SetSpeed(_motor.CurrentSpeed * wheelDegPerSpeed);
        }
    }
}
