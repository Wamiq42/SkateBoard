using System;
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
        [Tooltip("Steady CRUISE speed the time-ramp settles at (on flat ground).")]
        public float baseSpeed = 26f;
        public float maxSpeed = 40f;
        [Tooltip("How quickly speed eases toward its target.")]
        public float accel = 14f;
        [Tooltip("Extra target speed per unit of downhill steepness (0..1).")]
        public float downhillGain = 12f;
        [Tooltip("Speed scrubbed per unit of uphill steepness.")]
        public float uphillDrag = 12f;

        [Header("Time ramp (Subway-Surfers feel)")]
        [Tooltip("Speed climbs from startSpeed up to baseSpeed over rampTime, then holds. Collisions knock it back so you rebuild.")]
        public bool useTimeRamp = true;
        [Tooltip("Speed the moment the race starts, before the ramp builds.")]
        public float startSpeed = 12f;
        [Tooltip("Seconds to climb from startSpeed to baseSpeed (the steady cruise speed).")]
        public float rampTime = 18f;
        [Tooltip("Fraction of built-up ramp kept after a collision (0.6 = lose 40% of your momentum).")]
        [Range(0f, 1f)] public float rampKeepOnHit = 0.6f;

        [Header("Steering")]
        [Tooltip("Yaw degrees/second at full steer (at speed).")]
        public float steerRate = 120f;
        [Tooltip("Steering is scaled by speed up to this value (no spinning when slow).")]
        public float steerSpeedRef = 12f;
        [Tooltip("How fast steering eases IN toward full lock (input units/sec). The touch buttons " +
                 "are digital (0 or 1), so without this the board snaps to full yaw rate in one tick. " +
                 "~4 = heavy carve, ~12 = twitchy. 0 = no smoothing (instant).")]
        public float steerRampUp = 6f;
        [Tooltip("How fast steering returns to centre on release (input units/sec). " +
                 "Usually a touch quicker than ramp-up so the board straightens promptly.")]
        public float steerRampDown = 9f;

        [Header("Air / jump")]
        public float gravity = 22f;
        public float jumpSpeed = 10.5f;
        public int maxJumps = 2;
        [Tooltip("Limited steering authority while airborne (0..1).")]
        public float airControl = 0.4f;
        [Tooltip("Vertical slack above ride height within which a *descending* jump re-lands. " +
                 "Kept small so road-magnetization can't grab the skater at the apex (that was eating the arc). " +
                 "Smaller = floatier / cleaner parabola; larger = lands sooner.")]
        public float landSnap = 0.2f;

        [Header("Ground follow")]
        public LayerMask groundMask = ~0;
        [Tooltip("Ride height of the pivot above the road surface.")]
        public float hoverHeight = 0.08f;
        [Tooltip("How far below the pivot we still snap to / consider grounded (magnetizes to the road).")]
        public float groundProbe = 6f;
        [Tooltip("How fast the board tilts to match the ground slope.")]
        public float alignSpeed = 12f;

        [Header("Finish Stop")]
        [Tooltip("How quickly the skater brakes after crossing the finish line.")]
        public float finishBrake = 24f;

        [Header("Road edge guide (soft boundary)")]
        [Tooltip("Keeps the skater on the road WITHOUT invisible walls: free in the middle, " +
                 "steered gently back inside the soft band near the edge, hard-clamped at the limit. " +
                 "Measured laterally from the RaceRoute line.")]
        public bool edgeGuide = true;
        [Tooltip("The route the corridor follows. Auto-found in the scene when left empty.")]
        public RaceRoute route;
        [Tooltip("Half-width of the playable corridor (metres from the route line to the edge).")]
        public float roadHalfWidth = 4.5f;
        [Tooltip("Width of the guidance band inside the edge. Entering it eases you back; 0 = walls only.")]
        public float edgeSoftZone = 2f;
        [Tooltip("Max corrective steer (deg/s) at the very edge. Higher = firmer push back.")]
        public float edgeSteer = 240f;
        [Tooltip("If the skater ends up this far BELOW the route line (fell off a bridge/ledge), " +
                 "teleport back onto the route and keep racing. 0 = off.")]
        public float fallResetDepth = 8f;

        [Header("Fall reporting")]
        [Tooltip("When set (player only), falling off the route raises FellOff instead of auto-teleporting back, so a Resume popup can take over. AI leave this off and self-recover.")]
        public bool reportFall = false;
        /// <summary>Raised once when this skater falls off the route and <see cref="reportFall"/> is set.</summary>
        public event Action FellOff;
        private bool _fellReported;

        public bool IsGrounded { get; private set; }
        public float Speed => _speed;

        // ---- Scoring hooks (read/driven by RaceManager's RaceScore) ----
        /// <summary>Tags the CURRENT jump as a trick (set by RiderAnimDriver.Trick()).
        /// Carried into the Landed event so a clean land scores; cleared on land or crash.</summary>
        public bool TrickArmed;
        /// <summary>Fired the frame a jump re-lands. Arg = was this a trick jump (TrickArmed at touchdown).</summary>
        public event Action<bool> Landed;
        /// <summary>Fired when a collision penalty is applied (bails any in-progress trick/combo).</summary>
        public event Action Crashed;
        public Vector3 Velocity => _rb != null ? _rb.linearVelocity : Vector3.zero;
        public bool IsBoosting => _boostTimer > 0f;
        /// <summary>Vertical velocity (units/s); + up, - down. Handy for tuning telemetry.</summary>
        public float VerticalSpeed => _airVelY;
        /// <summary>Jumps consumed since the last landing (0 grounded, 1 after one jump, etc.).</summary>
        public int JumpsUsed => _jumpsUsed;
        /// <summary>How far along the speed ramp we are (0 = startSpeed, 1 = full cruise). Telemetry.</summary>
        public float RampProgress => rampTime > 0.01f ? Mathf.Clamp01(_rampT / rampTime) : 1f;

        private Rigidbody _rb;
        private float _speed;
        private float _steer;       // smoothed steer (eases toward SteerInput)
        private Vector3 _heading = Vector3.forward;     // horizontal facing
        private Vector3 _groundNormal = Vector3.up;
        private int _jumpsUsed;
        private float _airVelY;
        private bool _airborne;     // true from a jump until we re-land (ignores road magnetization)
        private float _rampT;       // elapsed time on the speed ramp (0..rampTime)
        private float _boostMul = 1f, _boostTimer;
        private float _penaltyMul = 1f, _penaltyTimer;
        private bool _finishStopping;

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
            if (route == null) route = FindFirstObjectByType<RaceRoute>();
        }

        public void ApplyBoost(float mul, float dur) { _boostMul = Mathf.Max(_boostMul, mul); _boostTimer = Mathf.Max(_boostTimer, dur); }
        public void ApplyPenalty(float mul, float dur)
        {
            _penaltyMul = Mathf.Min(_penaltyMul, Mathf.Clamp01(mul));
            _penaltyTimer = Mathf.Max(_penaltyTimer, dur);
            _rampT *= Mathf.Clamp01(rampKeepOnHit);   // collision scrubs built-up momentum
            TrickArmed = false;                        // a crash bails the in-progress trick
            Crashed?.Invoke();
        }

        public void Jump()
        {
            if (_jumpsUsed >= maxJumps) return;
            if (IsGrounded) _jumpsUsed = 0;
            _airVelY = jumpSpeed;
            _jumpsUsed++;
            _airborne = true;
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
            _speed = 0f; _steer = 0f; _airVelY = 0f; _jumpsUsed = 0; _airborne = false; _finishStopping = false;
            _fellReported = false;   // re-arm fall reporting after a respawn/resume
        }

        /// <summary>Begin a controlled finish-line brake used while the celebration camera takes over.</summary>
        public void StopImmediately()
        {
            Active = false;
            SteerInput = 0f;
            _steer = 0f;
            JumpRequested = false;
            TrickArmed = false;
            _finishStopping = true;
            _boostMul = 1f;
            _boostTimer = 0f;
            _penaltyMul = 1f;
            _penaltyTimer = 0f;
            if (_rb == null) return;
            _rb.angularVelocity = Vector3.zero;
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

            // Height of the pivot above the road right now (∞ if no road in range).
            float heightAboveGround = hitGround ? _rb.position.y - groundY : float.PositiveInfinity;

            if (_airborne)
            {
                // Mid-jump: IGNORE road magnetization so the whole parabola plays out.
                // (Previously the 6-unit probe re-grounded us at the apex, which ate the
                // descent and made jumps "instantly drop".) Only re-land once we're actually
                // descending AND close to the surface.
                if (hitGround && _airVelY <= 0f && heightAboveGround <= hoverHeight + landSnap)
                {
                    _airborne = false;
                    _jumpsUsed = 0;
                    IsGrounded = true;
                    Landed?.Invoke(TrickArmed);   // score a clean trick land; clear the tag
                    TrickArmed = false;
                }
                else IsGrounded = false;
            }
            else
            {
                // Rolling: magnetized to the road within probe range (hugs crests/dips).
                IsGrounded = hitGround;
            }

            // --- Fall reset ---
            // Fell off a bridge/ledge into the void below the track: pop back onto the route
            // (the route line is the source of truth for where the road surface is).
            if (fallResetDepth > 0f && Active && route != null && route.Count > 1)
            {
                Vector3 rc = route.ClosestPoint(_rb.position, out Vector3 routeDir);
                if (rc.y - _rb.position.y > fallResetDepth)
                {
                    // Player: hand the fall to the Resume popup instead of auto-recovering.
                    if (reportFall)
                    {
                        if (!_fellReported) { _fellReported = true; FellOff?.Invoke(); }
                        return;
                    }
                    Teleport(rc + Vector3.up * 0.5f, routeDir);
                    return;
                }
            }

            // --- Steering ---
            // Ease the (digital) input toward its target so the turn carves in/out instead of
            // snapping to full yaw rate the frame a button goes down.
            float steerTarget = Mathf.Clamp(SteerInput, -1f, 1f);
            bool releasing = Mathf.Abs(steerTarget) < 0.01f || steerTarget * _steer < 0f;
            float steerRamp = releasing ? steerRampDown : steerRampUp;
            _steer = steerRamp > 0f ? Mathf.MoveTowards(_steer, steerTarget, steerRamp * dt) : steerTarget;
            float steer = _steer;
            float speedFactor = Mathf.Clamp01(_speed / steerSpeedRef);
            float authority = IsGrounded ? 1f : airControl;
            float yaw = steer * steerRate * speedFactor * authority * dt;
            _heading = Quaternion.AngleAxis(yaw, Vector3.up) * _heading;
            _heading.y = 0f; _heading.Normalize();

            // --- Road edge guide (soft boundary) ---
            // Replaces the old invisible side-barrier colliders: measure lateral offset from the
            // route line; inside the road do nothing, in the soft band steer back progressively,
            // at the hard limit clamp position and strip any outward heading (slide, don't bounce).
            if (edgeGuide && Active && route != null && route.Count > 1)
            {
                Vector3 c = route.ClosestPoint(_rb.position, out Vector3 routeFwd);
                Vector3 right = Vector3.Cross(Vector3.up, routeFwd).normalized;
                float lat = Vector3.Dot(_rb.position - c, right);
                float abs = Mathf.Abs(lat);
                float softStart = Mathf.Max(0.25f, roadHalfWidth - edgeSoftZone);
                if (abs > softStart)
                {
                    float side = Mathf.Sign(lat);
                    float k = Mathf.InverseLerp(softStart, roadHalfWidth, abs);   // 0..1 across the band
                    // ease the heading toward "route forward, leaning back inside"
                    Vector3 desired = (routeFwd - right * (side * k)).normalized;
                    _heading = Vector3.RotateTowards(_heading, desired, edgeSteer * k * dt * Mathf.Deg2Rad, 0f);
                    _heading.y = 0f; _heading.Normalize();

                    if (abs > roadHalfWidth)
                    {
                        // hard limit: clamp back to the corridor edge…
                        _rb.position -= right * (side * (abs - roadHalfWidth));
                        // …and remove any outward component so we glide along the edge.
                        float outward = Vector3.Dot(_heading, right) * side;
                        if (outward > 0f)
                        {
                            _heading -= right * (side * outward);
                            if (_heading.sqrMagnitude < 0.01f) _heading = routeFwd;
                            _heading.Normalize();
                        }
                    }
                }
            }

            // forward along the ground plane (tilts with slope)
            Vector3 fwdOnPlane = Vector3.ProjectOnPlane(_heading, _groundNormal).normalized;
            if (fwdOnPlane.sqrMagnitude < 0.01f) fwdOnPlane = _heading;

            // --- Speed (auto-roll + gravity on slope) ---
            if (Active)
            {
                _finishStopping = false;

                // Ramp the cruise speed up over time, then hold (Subway-Surfers feel).
                // Clamp the start to baseSpeed so a misconfigured startSpeed > baseSpeed can never
                // make this ramp DOWN (i.e. "starts fast then slows" — only ever ramp up or hold).
                if (useTimeRamp && rampTime > 0.01f) _rampT = Mathf.Min(_rampT + dt, rampTime);
                float rampFrom = Mathf.Min(startSpeed, baseSpeed);
                float cruise = useTimeRamp ? Mathf.Lerp(rampFrom, baseSpeed, _rampT / Mathf.Max(0.01f, rampTime)) : baseSpeed;

                float slope = -fwdOnPlane.y;                 // >0 downhill, <0 uphill
                float target = cruise + Mathf.Max(0f, slope) * downhillGain;
                target = Mathf.Min(target, maxSpeed) * _boostMul * _penaltyMul;
                float a = accel + Mathf.Max(0f, -slope) * uphillDrag;   // brake harder uphill
                _speed = Mathf.MoveTowards(_speed, target, a * dt);
            }
            else
            {
                float brake = _finishStopping ? Mathf.Max(accel, finishBrake) : accel;
                _speed = Mathf.MoveTowards(_speed, 0f, brake * dt);
            }

            // --- Jump request ---
            if (JumpRequested)
            {
                if (Active) Jump();
                JumpRequested = false;
            }

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
                // airborne (jump) or free-falling off a cliff: integrate gravity.
                // Landing is decided next frame by the _airborne / magnetization logic above.
                _airVelY -= gravity * dt;
                vel.y = _airVelY;
            }

            _rb.linearVelocity = vel;

            // --- Orientation: face heading, tilt to slope ---
            Vector3 up = IsGrounded ? _groundNormal : Vector3.up;
            Quaternion targetRot = Quaternion.LookRotation(fwdOnPlane, up);
            _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, targetRot, alignSpeed * dt));
        }
    }
}
