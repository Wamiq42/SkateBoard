using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// A sparse, ordered set of checkpoints down the course. NOT a rail — the player
    /// drives freely with physics. The route is used only for: AI steering targets,
    /// race-progress/ranking, and respawn-after-fall. Forgiving by design.
    ///
    /// Source of truth: if <see cref="spline"/> is assigned, the route follows that smooth
    /// spline (sampled into a dense polyline) — edit its knots in the Scene view to reshape
    /// the racing line with no code. Otherwise it falls back to the child checkpoint Transforms.
    /// </summary>
    public class RaceRoute : MonoBehaviour
    {
        [Tooltip("Optional. When assigned, the route follows this smooth spline (edit its knots in the " +
                 "Scene view) instead of the checkpoint Transforms below. Leave null to use checkpoints.")]
        public SplineContainer spline;

        [Tooltip("Which spline inside the container to follow: 0 = Level 1, 1 = Level 2, etc. " +
                 "Set automatically by LevelManager on load.")]
        public int splineIndex = 0;

        [Tooltip("Approx spacing (metres) between sampled points when a spline is used. Smaller = smoother.")]
        public float sampleSpacing = 3f;

        public List<Transform> checkpoints = new List<Transform>();

        // The live working set the route is measured against — either spline samples or checkpoints.
        private readonly List<Vector3> _pts = new List<Vector3>();
        private readonly List<float> _cum = new List<float>();
        private float _total;

        public int Count => _pts.Count;
        public float TotalLength => _total;

        private void Awake() => Build();

        public void Build()
        {
            _pts.Clear();

            bool builtFromSpline = false;
            if (spline != null && spline.Splines != null && spline.Splines.Count > 0)
            {
                int idx = Mathf.Clamp(splineIndex, 0, spline.Splines.Count - 1);
                if (spline.Splines[idx].Count >= 2)
                {
                    // Estimate length by coarse sampling (handles any spline index / container transform).
                    float len = 0f; Vector3 prev = EvalPos(idx, 0f);
                    int coarse = Mathf.Max(64, spline.Splines[idx].Count * 4);
                    for (int i = 1; i <= coarse; i++) { Vector3 p = EvalPos(idx, (float)i / coarse); len += Vector3.Distance(prev, p); prev = p; }
                    int samples = Mathf.Max(2, Mathf.CeilToInt(len / Mathf.Max(0.5f, sampleSpacing)));
                    for (int i = 0; i <= samples; i++) _pts.Add(EvalPos(idx, (float)i / samples));
                    builtFromSpline = _pts.Count >= 2;
                }
            }
            if (!builtFromSpline)
            {
                if (checkpoints == null || checkpoints.Count == 0)
                {
                    checkpoints = new List<Transform>();
                    foreach (Transform c in transform) checkpoints.Add(c);
                }
                foreach (Transform c in checkpoints)
                    if (c != null) _pts.Add(c.position);
            }

            _cum.Clear(); _total = 0f;
            if (_pts.Count == 0) return;
            _cum.Add(0f);
            for (int i = 1; i < _pts.Count; i++)
            {
                _total += Vector3.Distance(_pts[i - 1], _pts[i]);
                _cum.Add(_total);
            }
        }

        private Vector3 EvalPos(int idx, float t)
        {
            spline.Evaluate(idx, t, out float3 p, out _, out _);
            return (Vector3)p;
        }

        /// <summary>Switch which spline of the container drives the route, then rebuild.</summary>
        public void SetSplineIndex(int i)
        {
            splineIndex = i;
            Build();
        }

        public Vector3 Point(int i) => _pts[Mathf.Clamp(i, 0, _pts.Count - 1)];

        public Vector3 Forward(int i)
        {
            int a = Mathf.Clamp(i, 0, _pts.Count - 1);
            int b = Mathf.Min(a + 1, _pts.Count - 1);
            Vector3 f = _pts[b] - _pts[a];
            f.y = 0f;
            return f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
        }

        public int NearestIndex(Vector3 p)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < _pts.Count; i++)
            {
                float d = (_pts[i] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>Distance travelled along the route at the closest point to p (for ranking).</summary>
        public float Progress(Vector3 p)
        {
            if (_pts.Count == 0) return 0f;
            int i = NearestIndex(p);
            // project onto the segment toward the next point
            if (i < _pts.Count - 1)
            {
                Vector3 a = _pts[i], b = _pts[i + 1];
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
            for (int i = 0; i < _pts.Count - 1; i++)
            {
                if (_cum[i + 1] >= target)
                {
                    float segStart = _cum[i];
                    float t = (target - segStart) / Mathf.Max(0.01f, _cum[i + 1] - segStart);
                    return Vector3.Lerp(_pts[i], _pts[i + 1], t);
                }
            }
            return _pts.Count > 0 ? Point(_pts.Count - 1) : p;
        }

        public bool IsFinish(Vector3 p) => _pts.Count > 0 && Progress(p) >= _total - 4f;

        private void OnDrawGizmos()
        {
            // When the route has been built (play mode / after Build), draw the live working set.
            if (_pts.Count > 1)
            {
                Gizmos.color = Color.cyan;
                for (int i = 1; i < _pts.Count; i++) Gizmos.DrawLine(_pts[i - 1], _pts[i]);
                return;
            }
            // Edit-time preview of the source checkpoints (the spline draws its own gizmo).
            Gizmos.color = Color.green;
            if (checkpoints == null) return;
            for (int i = 0; i < checkpoints.Count; i++)
            {
                if (checkpoints[i] == null) continue;
                Gizmos.DrawWireSphere(checkpoints[i].position, 2f);
                if (i + 1 < checkpoints.Count && checkpoints[i + 1] != null)
                    Gizmos.DrawLine(checkpoints[i].position, checkpoints[i + 1].position);
            }
        }
    }
}
