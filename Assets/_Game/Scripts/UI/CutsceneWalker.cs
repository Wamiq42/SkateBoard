using UnityEngine;

namespace Mixtape.UI
{
    /// <summary>
    /// Paces a cutscene character between a looping set of waypoint Transforms while their
    /// Animator plays an in-place walk loop, turning smoothly to face the direction of
    /// travel. Pairs with the CutsceneWalk controller so the friend who is "walking
    /// around" during the intro actually moves through the apartment. Pauses briefly at
    /// each waypoint so it reads as someone wandering and thinking, not marching.
    /// Drop empty GameObjects in the scene and drag them into <see cref="waypoints"/> —
    /// move them in the editor to re-route the path.
    /// </summary>
    [DisallowMultipleComponent]
    public class CutsceneWalker : MonoBehaviour
    {
        [Tooltip("Waypoint Transforms walked between, in order, looping back to the first. Place these as empty GameObjects in the scene; only their position is used.")]
        public Transform[] waypoints;

        [Tooltip("Walk speed in metres/second. Keep it gentle so it matches the walk-cycle cadence.")]
        public float speed = 0.7f;
        [Tooltip("How fast the character turns to face the direction of travel (deg/sec).")]
        public float turnSpeed = 220f;
        [Tooltip("How close counts as 'arrived' at a waypoint.")]
        public float arriveDistance = 0.08f;
        [Tooltip("Seconds to pause at each waypoint before moving on.")]
        public float pauseAtWaypoint = 1.1f;
        [Tooltip("Use unscaled time so the pacing is unaffected by any time-scale changes.")]
        public bool useUnscaledTime = true;

        [Header("Animator")]
        [Tooltip("Bool parameter the walk Animator uses: true => walk, false => idle (set while paused at a waypoint).")]
        public string movingParam = "Moving";

        private int _target;
        private float _pauseTimer;
        private Animator _anim;
        private int _movingHash;
        private bool _hasMovingParam;

        private void Awake()
        {
            _anim = GetComponent<Animator>();
            _movingHash = Animator.StringToHash(movingParam);
            CacheParam();
        }

        private void CacheParam()
        {
            _hasMovingParam = false;
            if (_anim == null) return;
            foreach (var p in _anim.parameters)
                if (p.type == AnimatorControllerParameterType.Bool && p.nameHash == _movingHash)
                { _hasMovingParam = true; break; }
        }

        private void SetMoving(bool moving)
        {
            if (_anim != null && _hasMovingParam) _anim.SetBool(_movingHash, moving);
        }

        private void OnEnable()
        {
            // Snap onto the nearest leg so re-enabling never teleports the character.
            _pauseTimer = 0f;
            _target = NearestWaypoint();
        }

        private int CountValid()
        {
            if (waypoints == null) return 0;
            int n = 0;
            for (int i = 0; i < waypoints.Length; i++) if (waypoints[i] != null) n++;
            return n;
        }

        private int NearestWaypoint()
        {
            int best = 0; float bestSqr = float.MaxValue;
            if (waypoints == null) return 0;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                float d = (waypoints[i].position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = i; }
            }
            return best;
        }

        private void Update()
        {
            if (CountValid() < 2) { SetMoving(false); return; }

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            if (_pauseTimer > 0f)
            {
                SetMoving(false);   // idle while paused at a waypoint
                _pauseTimer -= dt;
                return;
            }

            // Skip past any empty slots so the loop never stalls on a null.
            if (waypoints[_target] == null) { _target = (_target + 1) % waypoints.Length; return; }

            Vector3 goal = waypoints[_target].position;
            Vector3 to = goal - transform.position; to.y = 0f;

            if (to.magnitude <= arriveDistance)
            {
                _target = (_target + 1) % waypoints.Length;
                _pauseTimer = pauseAtWaypoint;
                SetMoving(false);   // begin idling at the waypoint
                return;
            }

            SetMoving(true);        // pacing => walk
            Vector3 dir = to.normalized;
            // Turn toward travel direction, then step forward.
            Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);
            transform.position += dir * speed * dt;
        }
    }
}
