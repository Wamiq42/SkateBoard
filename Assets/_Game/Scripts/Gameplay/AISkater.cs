using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Drives a <see cref="PhysicsSkater"/> opponent. Steers toward a look-ahead point on
    /// the <see cref="RaceRoute"/> — but offset sideways by smooth noise so it follows the
    /// course on a relaxed, parallel line rather than fighting for the exact centre. A
    /// forward sensor lets it jump hazards it can clear and swerve around things it can't,
    /// while leaving boosters alone. Rubber-bands its speed toward the player so races stay
    /// close. Uses the same physics as the player, so it feels like a real racer.
    /// </summary>
    [RequireComponent(typeof(PhysicsSkater))]
    public class AISkater : MonoBehaviour
    {
        public RaceRoute route;
        public float lookahead = 14f;
        [Tooltip("Steering aggressiveness: degrees of error mapped to full steer.")]
        public float steerSharpness = 32f;

        [Header("Natural line")]
        [Tooltip("How far (metres) the racer drifts off the centre line. Keep below the road half-width " +
                 "so it stays on the road but doesn't look glued to the middle.")]
        public float laneOffset = 1.8f;
        [Tooltip("How quickly the sideways drift wanders. Lower = lazier, more natural sway.")]
        public float wanderFrequency = 0.25f;

        [Header("Speed")]
        public float skill = 1f;
        public Transform player;
        public bool rubberBand = true;

        [Header("Obstacle avoidance")]
        [Tooltip("Master switch for the forward sensor (jump hazards / swerve walls).")]
        public bool avoidObstacles = true;
        [Tooltip("Base forward look distance; extended by current speed.")]
        public float senseDistance = 6f;
        [Tooltip("Seconds of travel added to the sensor reach (faster = sees further ahead).")]
        public float senseLead = 0.35f;
        [Tooltip("Thickness of the forward probe.")]
        public float senseRadius = 0.6f;
        [Tooltip("How hard it swerves around things it can't jump (0..1 of full steer).")]
        [Range(0f, 1f)] public float avoidStrength = 0.9f;
        [Tooltip("Seconds before impact to pop a jump, timed so the apex lines up with the hazard.")]
        public float jumpLead = 0.32f;
        [Tooltip("Extra height margin required before an obstacle counts as jump-clearable.")]
        public float jumpClearMargin = 0.25f;

        [Tooltip("Draw the steer target (yellow), forward sensor (cyan), and avoid/jump reactions in the Scene view.")]
        public bool debugGizmos = false;

        private PhysicsSkater _ps;
        private float _baseMax, _baseCruise;
        private float _noiseSeed;
        private static readonly RaycastHit[] _hits = new RaycastHit[8];

        // Cached for the debug gizmo only.
        private Vector3 _dbgTarget, _dbgSenseEnd;
        private bool _dbgJump, _dbgAvoid;

        private void Awake()
        {
            _ps = GetComponent<PhysicsSkater>();
            _baseMax = _ps.maxSpeed; _baseCruise = _ps.baseSpeed;
            // Per-racer seed so the two opponents pick different lines and don't converge.
            _noiseSeed = (GetInstanceID() & 0xFFFF) * 0.017f;
        }

        public void Init(RaceRoute r, Transform playerT) { route = r; player = playerT; }

        private void Update()
        {
            if (route == null || route.Count == 0) return;

            // --- Natural racing line: centre target nudged sideways by smooth noise ---
            int idx = route.NearestIndex(transform.position);
            Vector3 routeFwd = route.Forward(idx);
            Vector3 routeRight = Vector3.Cross(Vector3.up, routeFwd).normalized;

            float drift = (Mathf.PerlinNoise(_noiseSeed, Time.time * wanderFrequency) * 2f - 1f) * laneOffset;
            Vector3 target = route.SteerTarget(transform.position, lookahead) + routeRight * drift;
            _dbgTarget = target;

            Vector3 to = target - transform.position; to.y = 0f;
            Vector3 fwd = transform.forward; fwd.y = 0f;
            float steer = 0f;
            if (to.sqrMagnitude > 0.01f && fwd.sqrMagnitude > 0.01f)
            {
                float ang = Vector3.SignedAngle(fwd, to, Vector3.up);
                steer = Mathf.Clamp(ang / steerSharpness, -1f, 1f);
            }

            // --- Avoidance: jump clearable hazards, swerve around walls, ignore boosters ---
            if (avoidObstacles && fwd.sqrMagnitude > 0.01f)
                steer += Sense(fwd.normalized);

            _ps.SteerInput = Mathf.Clamp(steer, -1f, 1f);

            // --- Speed: base skill, optionally rubber-banded toward the player ---
            float mul = skill;
            if (rubberBand && player != null)
            {
                float gap = route.Progress(player.position) - route.Progress(transform.position);
                if (gap > 8f) mul *= 1.10f;        // behind -> speed up
                else if (gap < -8f) mul *= 0.94f;  // far ahead -> ease
            }
            _ps.maxSpeed = _baseMax * mul;
            _ps.baseSpeed = _baseCruise * mul;
        }

        /// <summary>
        /// Forward probe. Reacts ONLY to the track's hazards/pickups (the <see cref="Obstacle"/> /
        /// <see cref="Booster"/> trigger boxes under "Interactions") — never to the road mesh or other
        /// geometry, so the AI doesn't fight the track. Requests a jump for hazards it can clear
        /// (double-jumping only when a single hop won't reach), returns an extra steer (-1..1) to
        /// swerve around hazards too tall to jump, and leaves boosters alone.
        /// </summary>
        private float Sense(Vector3 fwd)
        {
            float speed = _ps.Speed;
            float dist = senseDistance + speed * senseLead;
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            _dbgSenseEnd = origin + fwd * dist; _dbgJump = false; _dbgAvoid = false;

            // QueryTriggerInteraction.Collide so the trigger-collider hazards register. We then
            // keep only Obstacle hits and discard everything else (track, props, boosters, slopes).
            int n = Physics.SphereCastNonAlloc(origin, senseRadius, fwd, _hits, dist,
                                               Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (n == 0) return 0f;

            // Reach of one jump and (roughly) a double jump, used to decide jump vs swerve.
            float apex = _ps.jumpSpeed * _ps.jumpSpeed / (2f * Mathf.Max(1f, _ps.gravity));
            float doubleApex = apex * 2f;
            float jumpDist = Mathf.Clamp(speed * jumpLead, 1.5f, dist);

            float nearestBlock = float.MaxValue;
            float avoid = 0f;

            for (int i = 0; i < n; i++)
            {
                RaycastHit h = _hits[i];
                if (h.collider == null) continue;

                // Boosters are good — never swerve or jump to dodge them.
                if (h.collider.GetComponentInParent<Booster>() != null) continue;

                // Only react to real hazards. The road mesh and every other collider are ignored,
                // so the AI follows its line instead of swerving off the track geometry.
                if (h.collider.GetComponentInParent<Obstacle>() == null) continue;

                // Height is measured against the hazard's OWN box (not the giant track bounds).
                float topAbove = h.collider.bounds.max.y - transform.position.y;
                bool clearable = topAbove <= doubleApex - jumpClearMargin;

                if (clearable)
                {
                    if (h.distance <= jumpDist)
                    {
                        bool needsDouble = topAbove > apex - jumpClearMargin;
                        if (_ps.IsGrounded) { _ps.JumpRequested = true; _dbgJump = true; }                       // first hop
                        else if (needsDouble && _ps.Velocity.y > 0f) { _ps.JumpRequested = true; _dbgJump = true; } // double for tall
                    }
                }
                else if (h.distance < nearestBlock)
                {
                    // Too tall to jump — swerve toward the side it isn't on.
                    nearestBlock = h.distance;
                    float side = Vector3.Dot(h.point - transform.position, right);
                    avoid = (side >= 0f ? -1f : 1f) * avoidStrength;
                    _dbgAvoid = true;
                }
            }

            return avoid;
        }

        private void OnDrawGizmos()
        {
            if (!debugGizmos || !Application.isPlaying) return;

            // Steer target (the relaxed, off-centre line point it's aiming at).
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _dbgTarget);
            Gizmos.DrawWireSphere(_dbgTarget, 0.4f);

            // Forward sensor: cyan = clear, magenta = avoiding a wall, green = jumping.
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Gizmos.color = _dbgJump ? Color.green : (_dbgAvoid ? Color.magenta : Color.cyan);
            Gizmos.DrawLine(origin, _dbgSenseEnd);
            Gizmos.DrawWireSphere(_dbgSenseEnd, senseRadius);
        }
    }
}
