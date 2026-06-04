using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// A hazard on the track (broken road, debris, lake edge). If a racer passes through
    /// it without being airborne above <see cref="clearHeight"/>, they get slowed — so the
    /// player must jump it. Put a trigger collider on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Obstacle : MonoBehaviour
    {
        [Tooltip("Racer must be jumping above this height (units) to clear the hazard.")]
        public float clearHeight = 1.0f;
        [Tooltip("Speed multiplier applied on a hit (0..1).")]
        [Range(0f, 1f)] public float slowMultiplier = 0.4f;
        public float slowDuration = 1.2f;
        [Tooltip("Optional VFX on hit.")]
        public GameObject hitEffect;

        private void Reset() { var c = GetComponent<Collider>(); if (c) c.isTrigger = true; }
        private void Awake() { var c = GetComponent<Collider>(); if (c) c.isTrigger = true; }

        private void OnTriggerEnter(Collider other)
        {
            var motor = other.GetComponentInParent<PhysicsSkater>();
            if (motor == null) return;
            if (!motor.IsGrounded) return; // airborne (jumping) clears it

            motor.ApplyPenalty(slowMultiplier, slowDuration);
            if (hitEffect) Instantiate(hitEffect, transform.position, Quaternion.identity);
        }
    }
}
