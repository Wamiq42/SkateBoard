using System.Collections.Generic;
using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// A sparse, ordered set of checkpoints down the course. NOT a rail — the player
    /// drives freely with physics. The route is used only for: AI steering targets,
    /// race-progress/ranking, and respawn-after-fall. Forgiving by design.
    /// </summary>
    public class RaceRoute : MonoBehaviour
    {
        public List<Transform> checkpoints = new List<Transform>();

        private readonly List<float> _cum = new List<float>();
        private float _total;

        public int Count => checkpoints.Count;
        public float TotalLength => _total;

        private void Awake() => Build();

        public void Build()
        {
            if (checkpoints == null || checkpoints.Count == 0)
            {
                checkpoints = new List<Transform>();
                foreach (Transform c in transform) checkpoints.Add(c);
            }
            _cum.Clear(); _total = 0f;
            if (Count == 0) return;
            _cum.Add(0f);
            for (int i = 1; i < Count; i++)
            {
                _total += Vector3.Distance(checkpoints[i - 1].position, checkpoints[i].position);
                _cum.Add(_total);
            }
        }

        public Vector3 Point(int i) => checkpoints[Mathf.Clamp(i, 0, Count - 1)].position;

        public Vector3 Forward(int i)
        {
            int a = Mathf.Clamp(i, 0, Count - 1);
            int b = Mathf.Min(a + 1, Count - 1);
            Vector3 f = checkpoints[b].position - checkpoints[a].position;
            f.y = 0f;
            return f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
        }

        public int NearestIndex(Vector3 p)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < Count; i++)
            {
                float d = (checkpoints[i].position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>Distance travelled along the route at the closest point to p (for ranking).</summary>
        public float Progress(Vector3 p)
        {
            if (Count == 0) return 0f;
            int i = NearestIndex(p);
            // project onto the segment toward the next checkpoint
            if (i < Count - 1)
            {
                Vector3 a = checkpoints[i].position, b = checkpoints[i + 1].position;
                Vector3 ab = b - a; float len = ab.magnitude;
                if (len > 0.01f)
                {
                    float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / (len * len));
                    return _cum[i] + t * len;
                }
            }
            return _cum[i];
        }

        /// <summary>A point ~lookahead metres ahead along the route from p — the AI steer target.</summary>
        public Vector3 SteerTarget(Vector3 p, float lookahead)
        {
            float target = Progress(p) + lookahead;
            for (int i = 0; i < Count - 1; i++)
            {
                if (_cum[i + 1] >= target)
                {
                    float segStart = _cum[i];
                    float t = (target - segStart) / Mathf.Max(0.01f, _cum[i + 1] - segStart);
                    return Vector3.Lerp(checkpoints[i].position, checkpoints[i + 1].position, t);
                }
            }
            return Point(Count - 1);
        }

        public bool IsFinish(Vector3 p) => Count > 0 && Progress(p) >= _total - 4f;

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            var list = (checkpoints != null && checkpoints.Count > 0) ? checkpoints : null;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                Gizmos.DrawWireSphere(list[i].position, 2f);
                if (i + 1 < list.Count && list[i + 1] != null) Gizmos.DrawLine(list[i].position, list[i + 1].position);
            }
        }
    }
}
