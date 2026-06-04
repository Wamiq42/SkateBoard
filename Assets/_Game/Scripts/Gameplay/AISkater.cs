using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Drives a <see cref="PhysicsSkater"/> opponent: steers toward a look-ahead point on
    /// the <see cref="RaceRoute"/> and rubber-bands its speed toward the player so races
    /// stay close. Uses the same physics as the player, so it feels like a real racer.
    /// </summary>
    [RequireComponent(typeof(PhysicsSkater))]
    public class AISkater : MonoBehaviour
    {
        public RaceRoute route;
        public float lookahead = 14f;
        [Tooltip("Steering aggressiveness: degrees of error mapped to full steer.")]
        public float steerSharpness = 32f;

        [Header("Speed")]
        public float skill = 1f;
        public Transform player;
        public bool rubberBand = true;

        private PhysicsSkater _ps;
        private float _baseMax, _baseCruise;

        private void Awake()
        {
            _ps = GetComponent<PhysicsSkater>();
            _baseMax = _ps.maxSpeed; _baseCruise = _ps.baseSpeed;
        }

        public void Init(RaceRoute r, Transform playerT) { route = r; player = playerT; }

        private void Update()
        {
            if (route == null || route.Count == 0) return;

            Vector3 target = route.SteerTarget(transform.position, lookahead);
            Vector3 to = target - transform.position; to.y = 0f;
            Vector3 fwd = transform.forward; fwd.y = 0f;
            if (to.sqrMagnitude > 0.01f && fwd.sqrMagnitude > 0.01f)
            {
                float ang = Vector3.SignedAngle(fwd, to, Vector3.up);
                _ps.SteerInput = Mathf.Clamp(ang / steerSharpness, -1f, 1f);
            }

            float mul = skill;
            if (rubberBand && player != null && route != null)
            {
                float gap = route.Progress(player.position) - route.Progress(transform.position);
                if (gap > 8f) mul *= 1.10f;        // behind -> speed up
                else if (gap < -8f) mul *= 0.94f;  // far ahead -> ease
            }
            _ps.maxSpeed = _baseMax * mul;
            _ps.baseSpeed = _baseCruise * mul;
        }
    }
}
