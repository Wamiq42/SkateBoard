using UnityEngine;
using Mixtape.InputCtrl;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Arcade physics skateboard. A Rigidbody driven with ground-snapping and
    /// surface-normal alignment, so it hugs the undulating road and tilts up/down hills.
    /// Auto-rolls forward; gravity feeds speed on descents; the rider really steers,
    /// can drift wide, clip barriers, and jump. Shared by player (input) and AI (target).
    ///
    /// Set the GameObject's layer to "Ignore Raycast" so the ground probe skips itself,
    /// or assign a groundMask that excludes the skater.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PhysicsSkater : MonoBehaviour
    {
        [Header("Drive")]
        [Tooltip("Cruising speed on flat ground.")]
        public float baseSpeed = 16f;
        public float maxSpeed = 34f;
        [Tooltip("How quickly speed eases toward its target.")]
        public float accel = 14f;
        [Tooltip("Extra target speed per unit of downhill steepness (0..1).")]
        public float downhillGain = 22f;
        [Tooltip("Speed scrubbed per unit of uphill steepness.")]
        public float uphillDrag = 16f;

        [Header("Steering")]
        [Tooltip("Yaw degrees/second at full steer (at speed).")]
        public float steerRate = 95f;
        [Tooltip("Steering is scaled by speed up to this value (no spinning when slow).")]
        public float steerSpeedRef = 12f;

        [Header("Air / jump")]
        public float gravity = 26f;
        public float jumpSpeed = 9f;
        public int maxJumps = 2;
        [Tooltip("Limited steering authority while airborne (0..1).")]
        public float airControl = 0.35f;

        [Header("Ground follow")]
        public LayerMask groundMask = ~0;
        [Tooltip("Ride height of the pivot above the road surface.")]
        public float hoverHeight = 0.08f;
        [Tooltip("How far below the pivot we still snap to / consider grounded (magnetizes to the road).")]
        public float groundProbe = 6f;
        [Tooltip("How fast the board tilts to match the ground slope.")]
        public float alignSpeed = 12f;

        public bool IsGrounded { get; private set; }
        public float Speed => _speed;
        public Vector3 Velocity => _rb != null ? _rb.linearVelocity : Vector3.zero;

        private Rigidbody _rb;
        private float _speed;
        private Vector3 _heading = Vector3.forward;     // horizontal facing
        private Vector3 _groundNormal = Vector3.up;
        private int _jumpsUsed;
        private float _airVelY;
        private float _boostMul = 1f, _boostTimer;
        private float _penaltyMul = 1f, _penaltyTimer;

        // External control (player sets from input; AI sets directly).
        public float SteerInput;     // -1..1
        public bool JumpRequested;
        public bool Active = true;   // race gate (false during countdown)

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _heading = transform.forward;
            _heading.y = 0f; _heading.Normalize();
            if (_heading.sqrMagnitude < 0.01f) _heading = Vector3.forward;
        }

        public void ApplyBoost(float mul, float dur) { _boostMul = Mathf.Max(_boostMul, mul); _boostTimer = Mathf.Max(_boostTimer, dur); }
        public void ApplyPenalty(float mul, float dur) { _penaltyMul = Mathf.Min(_penaltyMul, Mathf.Clamp01(mul)); _penaltyTimer = Mathf.Max(_penaltyTimer, dur); }

        public void Jump()
        {
            if (_jumpsUsed >= maxJumps) return;
            if (IsGrounded) _jumpsUsed = 0;
            _airVelY = jumpSpeed;
            _jumpsUsed++;
            IsGrounded = false;
        }

        public void Teleport(Vector3 pos, Vector3 forward)
        {
            _heading = new Vector3(forward.x, 0, forward.z).normalized;
            if (_heading.sqrMagnitude < 0.01f) _heading = Vector3.forward;
            _rb.position = pos;
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(_heading, Vector3.up);
            _rb.linearVelocity = Vector3.zero;
            _speed = 0f; _airVelY = 0f; _jumpsUsed = 0;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // timers
            if (_boostTimer > 0f) { _boostTimer -= dt; if (_boostTimer <= 0f) _boostMul = 1f; }
            if (_penaltyTimer > 0f) { _penaltyTimer -= dt; if (_penaltyTimer <= 0f) _penaltyMul = 1f; }

            // --- Ground probe ---
            Vector3 origin = _rb.position + Vector3.up * 0.6f;
            bool hitGround = Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 0.6f + hoverHeight + groundProbe, groundMask, QueryTriggerInteraction.Ignore);
            float groundY = hitGround ? hit.point.y : _rb.position.y;
            _groundNormal = hitGround ? Vector3.Slerp(_groundNormal, hit.normal, alignSpeed * dt) : Vector3.up;

            // Magnetized: grounded whenever road is within probe range below and not
            // mid-jump. Only a real jump (or a true cliff edge with no ground) leaves it.
            bool nearGround = hitGround && _airVelY <= 0.01f;
            IsGrounded = nearGround;

            // --- Steering ---
            float steer = Mathf.Clamp(SteerInput, -1f, 1f);
            float speedFactor = Mathf.Clamp01(_speed / steerSpeedRef);
            float authority = IsGrounded ? 1f : airControl;
            float yaw = steer * steerRate * speedFactor * authority * dt;
            _heading = Quaternion.AngleAxis(yaw, Vector3.up) * _heading;
            _heading.y = 0f; _heading.Normalize();

            // forward along the ground plane (tilts with slope)
            Vector3 fwdOnPlane = Vector3.ProjectOnPlane(_heading, _groundNormal).normalized;
            if (fwdOnPlane.sqrMagnitude < 0.01f) fwdOnPlane = _heading;

            // --- Speed (auto-roll + gravity on slope) ---
            if (Active)
            {
                float slope = -fwdOnPlane.y;                 // >0 downhill, <0 uphill
                float target = baseSpeed + Mathf.Max(0f, slope) * downhillGain;
                target = Mathf.Min(target, maxSpeed) * _boostMul * _penaltyMul;
                float a = accel + Mathf.Max(0f, -slope) * uphillDrag;   // brake harder uphill
                _speed = Mathf.MoveTowards(_speed, target, a * dt);
            }
            else
            {
                _speed = Mathf.MoveTowards(_speed, 0f, accel * dt);
            }

            // --- Jump request ---
            if (JumpRequested) { Jump(); JumpRequested = false; }

            // --- Assemble velocity ---
            Vector3 vel = fwdOnPlane * _speed;
            if (IsGrounded)
            {
                // snap to ride height
                float targetY = groundY + hoverHeight;
                float vy = (targetY - _rb.position.y) / dt;
                vel.y = Mathf.Clamp(vy, -40f, 18f);
                _airVelY = 0f;
            }
            else
            {
                _airVelY -= gravity * dt;
                vel.y = _airVelY;
                // landing
                if (hitGround && _rb.position.y + _airVelY * dt <= groundY + hoverHeight) { _jumpsUsed = 0; }
            }

            _rb.linearVelocity = vel;

            // --- Orientation: face heading, tilt to slope ---
            Vector3 up = IsGrounded ? _groundNormal : Vector3.up;
            Quaternion targetRot = Quaternion.LookRotation(fwdOnPlane, up);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, alignSpeed * dt));
        }
    }
}
