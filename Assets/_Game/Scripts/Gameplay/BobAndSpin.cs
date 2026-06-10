using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Simple pickup idle animation: bobs up/down on a sine wave while
    /// spinning around the Y axis. Used by booster arrows and similar props.
    /// </summary>
    public class BobAndSpin : MonoBehaviour
    {
        [Header("Bob (up/down)")]
        [Tooltip("How far above/below the rest position the object travels, in meters.")]
        public float bobAmplitude = 0.25f;
        [Tooltip("Full up-down cycles per second.")]
        public float bobFrequency = 0.8f;

        [Header("Spin")]
        [Tooltip("Rotation speed around the Y axis, in degrees per second.")]
        public float spinSpeed = 90f;

        [Tooltip("Offset each instance's bob phase by its position so arrows don't move in sync.")]
        public bool desyncPhase = true;

        private Vector3 _restLocalPos;
        private float _phase;

        private void Awake()
        {
            _restLocalPos = transform.localPosition;
            if (desyncPhase)
                _phase = (transform.position.x + transform.position.z) * 0.7f;
        }

        private void Update()
        {
            float bob = Mathf.Sin((Time.time + _phase) * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            transform.localPosition = _restLocalPos + Vector3.up * bob;
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }

        private void OnDisable()
        {
            transform.localPosition = _restLocalPos;
        }
    }
}
