using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>Smooth 3rd-person chase camera.</summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        [Tooltip("Offset behind/above the target, in the target's local space.")]
        public Vector3 localOffset = new Vector3(0f, 3.2f, -6f);
        public float positionSmooth = 8f;
        public float rotationSmooth = 8f;
        [Tooltip("How far ahead of the target the camera looks.")]
        public float lookAhead = 4f;
        public float lookHeight = 1.2f;

        private void LateUpdate()
        {
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
    }
}
