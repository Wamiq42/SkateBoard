using System.Linq;
using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// One-shot roadside car hazard. The car waits until the racer is approaching,
    /// then reverses from its parked/shoulder position into the road and remains as
    /// a solid obstacle.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RoadsideReverseCarObstacle : MonoBehaviour
    {
        public enum ModelUpAxis
        {
            LocalY,
            LocalZ
        }

        [Header("Trigger")]
        public Transform player;
        public RaceRoute route;
        public float triggerProgressLead = 62f;
        public float fallbackDistance = 45f;
        public bool triggerOnlyWhenAhead = true;

        [Header("Movement")]
        public Transform roadTarget;
        public float moveDuration = 2.1f;
        public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public bool stayEnabledAfterMove;

        [Header("Ground Fit")]
        public bool snapToGround = true;
        public bool alignToGround = true;
        public ModelUpAxis modelUpAxis = ModelUpAxis.LocalZ;
        public LayerMask groundMask = ~0;
        public float groundRayHeight = 10f;
        public float groundRayDistance = 40f;
        public float groundOffset = 0.03f;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private float _carProgress;
        private float _moveTimer;
        private bool _triggered;
        private bool _complete;
        private bool _hasCarProgress;
        private Collider[] _ownColliders;

        private void Awake()
        {
            _ownColliders = GetComponentsInChildren<Collider>(true);
            CaptureStart();
            SnapTransformToGround();
        }

        private void OnValidate()
        {
            triggerProgressLead = Mathf.Max(1f, triggerProgressLead);
            fallbackDistance = Mathf.Max(1f, fallbackDistance);
            moveDuration = Mathf.Max(0.1f, moveDuration);
            groundRayHeight = Mathf.Max(0.1f, groundRayHeight);
            groundRayDistance = Mathf.Max(0.1f, groundRayDistance);
            groundOffset = Mathf.Max(0f, groundOffset);
        }

        private void Update()
        {
            if (!_triggered)
            {
                if (ShouldTrigger())
                    BeginMove();
                return;
            }

            if (_complete)
                return;

            _moveTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_moveTimer / Mathf.Max(0.01f, moveDuration));
            float eased = motionCurve != null ? motionCurve.Evaluate(t) : t;

            Vector3 position = Vector3.Lerp(_startPosition, _targetPosition, eased);
            Quaternion rotation = Quaternion.Slerp(_startRotation, _targetRotation, eased);
            FitPoseToGround(ref position, ref rotation);
            transform.SetPositionAndRotation(position, rotation);

            if (t >= 1f)
            {
                _complete = true;
                transform.SetPositionAndRotation(position, rotation);
                enabled = stayEnabledAfterMove;
            }
        }

        public void Configure(Transform playerTransform, RaceRoute raceRoute, Transform target, float leadDistance, float duration)
        {
            player = playerTransform;
            route = raceRoute;
            roadTarget = target;
            triggerProgressLead = Mathf.Max(1f, leadDistance);
            moveDuration = Mathf.Max(0.1f, duration);
            CaptureStart();
        }

        private void CaptureStart()
        {
            _startPosition = transform.position;
            _startRotation = transform.rotation;
            ResolveTarget();
            _hasCarProgress = route != null && route.Count > 0;
            _carProgress = _hasCarProgress ? route.Progress(transform.position) : 0f;
        }

        private void ResolveTarget()
        {
            if (roadTarget != null)
            {
                _targetPosition = roadTarget.position;
                _targetRotation = roadTarget.rotation;
                return;
            }

            _targetPosition = transform.position + transform.right * 7f;
            _targetRotation = transform.rotation;
        }

        private bool ShouldTrigger()
        {
            if (player == null)
                return false;

            if (route != null && route.Count > 0)
            {
                if (!_hasCarProgress)
                {
                    _carProgress = route.Progress(transform.position);
                    _hasCarProgress = true;
                }

                float playerProgress = route.Progress(player.position);
                float delta = _carProgress - playerProgress;
                if (triggerOnlyWhenAhead)
                    return delta >= 0f && delta <= triggerProgressLead;
                return Mathf.Abs(delta) <= triggerProgressLead;
            }

            return Vector3.Distance(player.position, transform.position) <= fallbackDistance;
        }

        private void BeginMove()
        {
            ResolveTarget();
            _triggered = true;
            _moveTimer = 0f;
            _startPosition = transform.position;
            _startRotation = transform.rotation;
        }

        private void SnapTransformToGround()
        {
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            FitPoseToGround(ref position, ref rotation);
            transform.SetPositionAndRotation(position, rotation);
            CaptureStart();
        }

        private void FitPoseToGround(ref Vector3 position, ref Quaternion rotation)
        {
            if (!snapToGround || !TryFindGround(position, out RaycastHit hit))
                return;

            if (alignToGround)
            {
                Vector3 modelUp = rotation * LocalModelUp;
                if (modelUp.sqrMagnitude > 0.0001f)
                    rotation = Quaternion.FromToRotation(modelUp.normalized, hit.normal) * rotation;
            }

            float bottom = GetColliderBottomAlong(position, rotation, hit.normal);
            float wantedBottom = Vector3.Dot(hit.point, hit.normal) + groundOffset;
            position += hit.normal * (wantedBottom - bottom);
        }

        private bool TryFindGround(Vector3 nearPosition, out RaycastHit groundHit)
        {
            Vector3 origin = nearPosition + Vector3.up * groundRayHeight;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits.OrderBy(h => h.distance))
            {
                if (hit.collider == null)
                    continue;
                if (_ownColliders != null && _ownColliders.Contains(hit.collider))
                    continue;
                groundHit = hit;
                return true;
            }

            groundHit = default;
            return false;
        }

        private float GetColliderBottomAlong(Vector3 position, Quaternion rotation, Vector3 normal)
        {
            var box = GetComponent<BoxCollider>();
            if (box == null)
                return Vector3.Dot(position, normal);

            Vector3 scale = transform.lossyScale;
            Vector3 center = Vector3.Scale(box.center, scale);
            Vector3 extents = Vector3.Scale(box.size * 0.5f, scale);
            float min = float.PositiveInfinity;

            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 local = center + Vector3.Scale(extents, new Vector3(x, y, z));
                Vector3 world = position + rotation * local;
                min = Mathf.Min(min, Vector3.Dot(world, normal));
            }

            return min;
        }

        private Vector3 LocalModelUp => modelUpAxis == ModelUpAxis.LocalZ ? Vector3.forward : Vector3.up;
    }
}
