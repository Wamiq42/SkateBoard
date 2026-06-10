using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>Smooth 3rd-person chase camera.</summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        [Tooltip("Offset behind/above the target, in the target's local space.")]
        public Vector3 localOffset = new Vector3(0f, 3.4f, -6.5f);
        public float positionSmooth = 7f;
        public float rotationSmooth = 7f;
        [Tooltip("How far ahead of the target the camera looks.")]
        public float lookAhead = 5f;
        public float lookHeight = 1.3f;

        [Header("Finish Celebration Orbit")]
        public float celebrationOrbitSpeed = 34f;
        public float celebrationOrbitHeight = 2.6f;
        public float celebrationOrbitDistance = 6.2f;

        private bool _celebrating;
        private Transform _celebrationTarget;
        private float _celebrationTimer;
        private float _celebrationDuration;
        private float _celebrationAngle;

        private void LateUpdate()
        {
            if (_celebrating)
            {
                UpdateCelebrationOrbit();
                return;
            }

            if (target == null) return;

            Vector3 desired = target.TransformPoint(localOffset);
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-positionSmooth * Time.deltaTime));

            Vector3 lookPoint = target.position + target.forward * lookAhead + Vector3.up * lookHeight;
            Quaternion desiredRot = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime));
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.TransformPoint(localOffset);
            Vector3 lookPoint = target.position + target.forward * lookAhead + Vector3.up * lookHeight;
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        }

        public void BeginCelebrationOrbit(Transform focus, float duration)
        {
            _celebrationTarget = focus != null ? focus : target;
            if (_celebrationTarget == null) return;

            Vector3 flat = transform.position - _celebrationTarget.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) flat = -_celebrationTarget.forward;

            _celebrationAngle = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            _celebrationDuration = Mathf.Max(0.1f, duration);
            _celebrationTimer = 0f;
            _celebrating = true;
        }

        private void UpdateCelebrationOrbit()
        {
            if (_celebrationTarget == null)
            {
                _celebrating = false;
                return;
            }

            _celebrationTimer += Time.deltaTime;
            _celebrationAngle += celebrationOrbitSpeed * Time.deltaTime;

            Vector3 focus = _celebrationTarget.position + Vector3.up * lookHeight;
            Vector3 orbit = Quaternion.Euler(0f, _celebrationAngle, 0f) * Vector3.forward * celebrationOrbitDistance;
            Vector3 desired = _celebrationTarget.position + orbit + Vector3.up * celebrationOrbitHeight;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-positionSmooth * Time.deltaTime));

            Quaternion desiredRot = Quaternion.LookRotation(focus - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, 1f - Mathf.Exp(-rotationSmooth * Time.deltaTime));

            if (_celebrationTimer >= _celebrationDuration)
                _celebrating = false;
        }
    }
}
