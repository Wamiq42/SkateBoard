using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Safety net for the physics skater: if it falls off the world or wanders too far
    /// off the route for too long, it respawns at the last checkpoint facing down-course.
    /// This is what makes free physics safe — a bad line costs time, not the whole run.
    /// </summary>
    [RequireComponent(typeof(PhysicsSkater))]
    public class SkaterRespawn : MonoBehaviour
    {
        public RaceRoute route;
        [Tooltip("Below this world Y, respawn immediately (fell off the map).")]
        public float fallY = -250f;
        [Tooltip("Horizontal distance from the route considered 'off track'.")]
        public float offDistance = 28f;
        [Tooltip("Seconds off-track (and not grounded on route) before respawning.")]
        public float offGrace = 2.5f;
        [Tooltip("Seconds nearly stationary while racing (e.g. wedged on traffic) before nudging ahead.")]
        public float stuckGrace = 2f;

        private PhysicsSkater _ps;
        private float _offTimer;
        private float _stuckTimer;

        private void Awake() => _ps = GetComponent<PhysicsSkater>();

        private void FixedUpdate()
        {
            if (route == null || route.Count == 0) return;

            if (transform.position.y < fallY) { Respawn(0); return; }

            int i = route.NearestIndex(transform.position);
            Vector3 d = route.Point(i) - transform.position; d.y = 0f;
            bool farOff = d.magnitude > offDistance;

            if (farOff && !_ps.IsGrounded) _offTimer += Time.fixedDeltaTime;
            else if (_ps.IsGrounded) _offTimer = 0f;
            if (_offTimer > offGrace) { Respawn(0); return; }

            // anti-stuck: wedged on traffic / a wall while racing
            if (_ps.Active)
            {
                Vector3 hv = _ps.Velocity; hv.y = 0f;
                if (hv.magnitude < 1.5f) _stuckTimer += Time.fixedDeltaTime;
                else _stuckTimer = 0f;
                if (_stuckTimer > stuckGrace) { Respawn(1); _stuckTimer = 0f; }
            }
        }

        /// <summary>Respawn relative to the nearest checkpoint (offset: -1 = back one, +1 = ahead one).</summary>
        public void Respawn(int offset)
        {
            int i = Mathf.Clamp(route.NearestIndex(transform.position) + offset, 0, route.Count - 1);
            _ps.Teleport(route.Point(i) + Vector3.up * 0.6f, route.Forward(i));
            _offTimer = 0f;
        }
    }
}
