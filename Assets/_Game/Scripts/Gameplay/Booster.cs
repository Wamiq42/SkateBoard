using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Speed pad / pickup. When a racer enters its trigger, applies a temporary speed
    /// boost to that racer's motor. Put a trigger collider on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Booster : MonoBehaviour
    {
        public float multiplier = 1.8f;
        public float duration = 2f;
        [Tooltip("If set, hide this object for a while after pickup instead of staying active.")]
        public bool consumable = true;
        public float respawnDelay = 5f;

        [Tooltip("Optional VFX/SFX to spawn on pickup.")]
        public GameObject pickupEffect;

        private Collider _col;
        private MeshRenderer[] _renderers;

        private void Awake()
        {
            _col = GetComponent<Collider>();
            _col.isTrigger = true;
            _renderers = GetComponentsInChildren<MeshRenderer>();
        }

        private void OnTriggerEnter(Collider other)
        {
            var motor = other.GetComponentInParent<PhysicsSkater>();
            if (motor == null) return;

            motor.ApplyBoost(multiplier, duration);

            if (pickupEffect != null)
                Instantiate(pickupEffect, transform.position, Quaternion.identity);

            if (consumable)
            {
                SetVisible(false);
                _col.enabled = false;
                Invoke(nameof(Respawn), respawnDelay);
            }
        }

        private void Respawn()
        {
            SetVisible(true);
            _col.enabled = true;
        }

        private void SetVisible(bool v)
        {
            foreach (var r in _renderers) if (r) r.enabled = v;
        }
    }
}
