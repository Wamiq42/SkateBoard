using System.Collections.Generic;
using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// One-time setup for the single combined race. The course used to be split into two
    /// levels (PATCH A + PATCH); it now runs as ONE race through both sections. On load this:
    ///   * activates every TRACK patch root,
    ///   * tells the RaceRoute to follow all of the container's splines as one continuous route,
    ///   * lines the racers up at the start.
    /// The opening cutscene plays as normal (it triggers the countdown on finish).
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Tooltip("The shared RaceRoute. It is put into combine-all-splines mode here.")]
        public RaceRoute route;

        [Tooltip("All TRACK patch roots (PATCH A, PATCH, ...). All are activated for the combined race.")]
        public GameObject[] levelRoots;

        [Header("Spawn")]
        [Tooltip("Move all racers to the start line on load.")]
        public bool repositionRacers = true;
        [Tooltip("Sideways gap (metres) between racers on the start line.")]
        public float laneSpacing = 3f;
        [Tooltip("Back-stagger (metres) per racer so the grid isn't a perfect row.")]
        public float startStagger = 1.2f;

        private void Awake()
        {
            if (levelRoots != null)
                foreach (var r in levelRoots) if (r != null) r.SetActive(true);

            if (route != null) route.Build();
        }

        private void Start()
        {
            if (repositionRacers) PlaceRacersAtStart();
        }

        private void PlaceRacersAtStart()
        {
            if (route == null || route.Count == 0) return;

            var rm = FindObjectOfType<RaceManager>(true);
            var list = new List<PhysicsSkater>();
            if (rm != null && rm.racers != null && rm.racers.Count > 0) list.AddRange(rm.racers);
            else list.AddRange(FindObjectsByType<PhysicsSkater>(FindObjectsSortMode.None));
            if (rm != null && rm.player != null && !list.Contains(rm.player)) list.Insert(0, rm.player);
            list.RemoveAll(x => x == null);
            if (list.Count == 0) return;

            Vector3 start = route.Point(0);
            Vector3 fwd = route.Forward(0);
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            int n = list.Count;
            int mask = ~(1 << 2); // exclude the skater "Ignore Raycast" layer

            for (int i = 0; i < n; i++)
            {
                float lateral = (i - (n - 1) * 0.5f) * laneSpacing;
                Vector3 p = start + right * lateral - fwd * (i * startStagger);
                if (Physics.Raycast(p + Vector3.up * 60f, Vector3.down, out RaycastHit h, 250f, mask, QueryTriggerInteraction.Ignore))
                    p.y = h.point.y;
                list[i].Teleport(p + Vector3.up * 0.1f, fwd);
            }
        }
    }
}
