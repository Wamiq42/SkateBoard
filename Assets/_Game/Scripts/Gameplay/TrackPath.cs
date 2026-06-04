using System.Collections.Generic;
using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// A polyline down the centre of the track. Racers move along it by distance and
    /// apply a lateral offset for steering. Build it by parenting empty "waypoint"
    /// GameObjects under this transform (in order, top of hill -> bottom), or by
    /// assigning <see cref="waypoints"/> explicitly.
    /// </summary>
    public class TrackPath : MonoBehaviour
    {
        [Tooltip("Ordered waypoints from start (top) to finish (bottom). If empty, direct children are used.")]
        public List<Transform> waypoints = new List<Transform>();

        [Tooltip("Half-width of the drivable road; steering is clamped to +/- this.")]
        public float roadHalfWidth = 3.5f;

        private readonly List<float> _cumulative = new List<float>();
        private float _totalLength;

        public float TotalLength => _totalLength;
        public float RoadHalfWidth => roadHalfWidth;

        private void Awake() => Rebuild();

        public void Rebuild()
        {
            if (waypoints == null || waypoints.Count == 0)
            {
                waypoints = new List<Transform>();
                foreach (Transform child in transform)
                    waypoints.Add(child);
            }

            _cumulative.Clear();
            _totalLength = 0f;
            if (waypoints.Count == 0) return;

            _cumulative.Add(0f);
            for (int i = 1; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null || waypoints[i - 1] == null) { _cumulative.Add(_totalLength); continue; }
                _totalLength += Vector3.Distance(waypoints[i - 1].position, waypoints[i].position);
                _cumulative.Add(_totalLength);
            }
        }

        public bool IsValid => waypoints != null && waypoints.Count >= 2 && _totalLength > 0.01f;

        /// <summary>Centre-line position at a given distance along the path.</summary>
        public Vector3 GetPosition(float distance)
        {
            if (waypoints == null || waypoints.Count == 0) return transform.position;
            if (waypoints.Count == 1) return waypoints[0].position;

            distance = Mathf.Clamp(distance, 0f, _totalLength);
            int seg = FindSegment(distance);
            float segStart = _cumulative[seg];
            float segLen = _cumulative[seg + 1] - segStart;
            float t = segLen > 0.0001f ? (distance - segStart) / segLen : 0f;
            return Vector3.Lerp(waypoints[seg].position, waypoints[seg + 1].position, t);
        }

        /// <summary>Normalised forward direction of the path at a given distance.</summary>
        public Vector3 GetDirection(float distance)
        {
            if (waypoints == null || waypoints.Count < 2) return transform.forward;
            distance = Mathf.Clamp(distance, 0f, _totalLength);
            int seg = FindSegment(distance);
            Vector3 dir = waypoints[seg + 1].position - waypoints[seg].position;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
        }

        /// <summary>Horizontal "right" vector used to apply lateral steering offset.</summary>
        public Vector3 GetRight(float distance)
        {
            Vector3 dir = GetDirection(distance);
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            return right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
        }

        /// <summary>Full world position including a clamped lateral offset.</summary>
        public Vector3 GetPositionWithOffset(float distance, float lateral)
        {
            lateral = Mathf.Clamp(lateral, -roadHalfWidth, roadHalfWidth);
            return GetPosition(distance) + GetRight(distance) * lateral;
        }

        private int FindSegment(float distance)
        {
            // Linear scan is fine for the modest waypoint counts a track uses.
            for (int i = 0; i < _cumulative.Count - 1; i++)
                if (distance <= _cumulative[i + 1]) return i;
            return Mathf.Max(0, waypoints.Count - 2);
        }

        private void OnDrawGizmos()
        {
            var pts = (waypoints != null && waypoints.Count > 0) ? waypoints : null;
            if (pts == null)
            {
                pts = new List<Transform>();
                foreach (Transform c in transform) pts.Add(c);
            }
            Gizmos.color = Color.cyan;
            for (int i = 0; i < pts.Count; i++)
            {
                if (pts[i] == null) continue;
                Gizmos.DrawSphere(pts[i].position, 0.4f);
                if (i + 1 < pts.Count && pts[i + 1] != null)
                    Gizmos.DrawLine(pts[i].position, pts[i + 1].position);
            }
        }
    }
}
