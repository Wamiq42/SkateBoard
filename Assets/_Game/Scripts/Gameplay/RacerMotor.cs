using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Kinematic movement along a <see cref="TrackPath"/>. Drives forward automatically;
    /// steering is a target lateral offset; jumping is a simple vertical arc layered on
    /// top of the path position. Shared by the player and AI so they race on equal terms.
    /// </summary>
    public class RacerMotor : MonoBehaviour
    {
        [Header("Path")]
        public TrackPath path;

        [Header("Speed")]
        [Tooltip("Base cruising speed (units/sec).")]
        public float baseSpeed = 14f;
        [Tooltip("Per-racer multiplier (board tuning / AI variance).")]
        public float speedMultiplier = 1f;
        [Tooltip("How fast speed eases toward its target.")]
        public float acceleration = 8f;

        [Header("Steering")]
        [Tooltip("Lateral movement speed (units/sec) toward the target offset.")]
        public float strafeSpeed = 8f;
        [Tooltip("Visual bank/lean angle at full steer.")]
        public float leanAngle = 18f;

        [Header("Jump")]
        public float jumpForce = 8f;
        public float gravity = 24f;
        public int maxJumps = 2;

        [Header("Runtime (read-only)")]
        public float distance;          // progress along path
        public float lateral;           // current lateral offset
        public float targetLateral;     // desired lateral offset
        public float height;            // vertical offset above path (jump)

        private float _speed;
        private float _speedBoostMul = 1f;
        private float _boostTimer;
        private float _penaltyMul = 1f;
        private float _penaltyTimer;
        private float _verticalVel;
        private int _jumpsUsed;
        private bool _running;

        public bool IsGrounded => height <= 0.001f && _verticalVel <= 0f;
        public bool IsFinished => path != null && path.IsValid && distance >= path.TotalLength - 0.05f;
        public float Distance => distance;
        public float NormalizedProgress => (path != null && path.TotalLength > 0f) ? Mathf.Clamp01(distance / path.TotalLength) : 0f;
        public float CurrentSpeed => _speed * _speedBoostMul * _penaltyMul;

        public void SetRunning(bool running) => _running = running;

        public void ResetToStart(float startDistance = 0f, float startLateral = 0f)
        {
            distance = startDistance;
            lateral = startLateral;
            targetLateral = startLateral;
            height = 0f;
            _verticalVel = 0f;
            _jumpsUsed = 0;
            _speed = 0f;
            _speedBoostMul = 1f;
            _boostTimer = 0f;
            _penaltyMul = 1f;
            _penaltyTimer = 0f;
            Apply(instant: true);
        }

        public void Jump()
        {
            if (_jumpsUsed >= maxJumps) return;
            if (_jumpsUsed == 0 && !IsGrounded) _jumpsUsed = 1; // walked off an edge counts as first
            _verticalVel = jumpForce;
            _jumpsUsed++;
        }

        /// <summary>Apply a temporary speed multiplier (boosters / UI boost).</summary>
        public void ApplyBoost(float multiplier, float duration)
        {
            _speedBoostMul = Mathf.Max(_speedBoostMul, multiplier);
            _boostTimer = Mathf.Max(_boostTimer, duration);
        }

        /// <summary>Temporarily slow the racer (e.g. hitting an un-jumped obstacle).</summary>
        public void ApplyPenalty(float multiplier, float duration)
        {
            _penaltyMul = Mathf.Min(_penaltyMul, Mathf.Clamp01(multiplier));
            _penaltyTimer = Mathf.Max(_penaltyTimer, duration);
        }

        private void Update() => Tick(Time.deltaTime);

        /// <summary>Advance the motor by dt. Public so races can be simulated/tested deterministically.</summary>
        public void Tick(float dt)
        {
            if (path == null || !path.IsValid) return;

            if (_running && !IsFinished)
            {
                float targetSpeed = baseSpeed * speedMultiplier;
                _speed = Mathf.MoveTowards(_speed, targetSpeed, acceleration * dt);

                if (_boostTimer > 0f)
                {
                    _boostTimer -= dt;
                    if (_boostTimer <= 0f) _speedBoostMul = 1f;
                }

                if (_penaltyTimer > 0f)
                {
                    _penaltyTimer -= dt;
                    if (_penaltyTimer <= 0f) _penaltyMul = 1f;
                }

                distance += CurrentSpeed * dt;
                distance = Mathf.Min(distance, path.TotalLength);
            }

            // Lateral easing toward target (clamped to road).
            float clampedTarget = Mathf.Clamp(targetLateral, -path.RoadHalfWidth, path.RoadHalfWidth);
            lateral = Mathf.MoveTowards(lateral, clampedTarget, strafeSpeed * dt);

            // Vertical (jump) integration.
            if (height > 0f || _verticalVel > 0f)
            {
                _verticalVel -= gravity * dt;
                height += _verticalVel * dt;
                if (height <= 0f)
                {
                    height = 0f;
                    _verticalVel = 0f;
                    _jumpsUsed = 0;
                }
            }

            Apply(instant: false);
        }

        private void Apply(bool instant)
        {
            Vector3 basePos = path.GetPositionWithOffset(distance, lateral);
            transform.position = basePos + Vector3.up * height;

            Vector3 dir = path.GetDirection(distance);
            if (dir.sqrMagnitude > 0.0001f)
            {
                float steerNorm = path.RoadHalfWidth > 0.01f ? (lateral / path.RoadHalfWidth) : 0f;
                Quaternion face = Quaternion.LookRotation(dir, Vector3.up);
                Quaternion lean = Quaternion.AngleAxis(steerNorm * leanAngle, dir);
                transform.rotation = instant ? face : Quaternion.Slerp(transform.rotation, lean * face, 12f * Time.deltaTime);
            }
        }
    }
}
