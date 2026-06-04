using UnityEngine;
using Mixtape.InputCtrl;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Player skater. Translates <see cref="InputService"/> into commands for the
    /// shared <see cref="RacerMotor"/>. Also exposes simple Animator hooks (parameters
    /// are set if an Animator exists) so imported animations can be wired in later.
    /// </summary>
    [RequireComponent(typeof(RacerMotor))]
    public class SkaterController : MonoBehaviour
    {
        [Header("Boost (UI / Shift)")]
        public float boostMultiplier = 1.6f;
        public float boostRefresh = 0.15f;

        [Header("Animator (optional, wire clips later)")]
        public Animator animator;
        public string speedParam = "Speed";
        public string groundedParam = "Grounded";
        public string jumpTrigger = "Jump";

        private RacerMotor _motor;
        private int _speedHash, _groundedHash, _jumpHash;
        private bool _hasAnimator;

        private void Awake()
        {
            _motor = GetComponent<RacerMotor>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _hasAnimator = animator != null;
            if (_hasAnimator)
            {
                _speedHash = Animator.StringToHash(speedParam);
                _groundedHash = Animator.StringToHash(groundedParam);
                _jumpHash = Animator.StringToHash(jumpTrigger);
            }
        }

        private void Update()
        {
            var input = InputService.Instance;
            if (input == null || _motor.path == null) return;

            // Steering: map axis to a target offset across the road.
            _motor.targetLateral = input.Steer * _motor.path.RoadHalfWidth;

            // Jump / double-jump.
            if (input.JumpPressedThisFrame)
            {
                _motor.Jump();
                if (_hasAnimator) animator.SetTrigger(_jumpHash);
            }

            // Speed boost while held.
            if (input.BoostHeld)
                _motor.ApplyBoost(boostMultiplier, boostRefresh);

            if (_hasAnimator)
            {
                animator.SetFloat(_speedHash, _motor.CurrentSpeed);
                animator.SetBool(_groundedHash, _motor.IsGrounded);
            }
        }
    }
}
