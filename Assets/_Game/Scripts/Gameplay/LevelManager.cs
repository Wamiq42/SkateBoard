using System.Collections.Generic;
using UnityEngine;
using Mixtape.Core;
using Mixtape.UI;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// On Game-scene load, reads which level the player picked (GameManager.SelectedLevel)
    /// and configures the scene for it:
    ///   * activates that level's TRACK patch, deactivates the others,
    ///   * points the shared RaceRoute at that level's spline (by index),
    ///   * spawns all racers (player + AI) at that level's start line,
    ///   * for any level past Level 1, skips the opening cutscene and goes straight to the countdown.
    ///
    /// Levels are sequential sections of the same world. Spline index N corresponds to
    /// level N (0 = Level 1, 1 = Level 2). Patch roots are assigned per level in the inspector.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Tooltip("The shared RaceRoute whose splineIndex we set per level.")]
        public RaceRoute route;

        [Tooltip("One TRACK patch root per level, in order: element 0 = Level 1 (PATCH A), " +
                 "element 1 = Level 2 (PATCH). The active level's root is enabled, the rest disabled.")]
        public GameObject[] levelRoots;

        [Tooltip("Used only when there is no GameManager (e.g. playing the Game scene directly " +
                 "in the editor). Set to 1 to test Level 2 without going through the menu.")]
        public int editorTestLevel = 0;

        [Header("Spawn")]
        [Tooltip("Move all racers to the active level's start line on load.")]
        public bool repositionRacers = true;
        [Tooltip("Sideways gap (metres) between racers on the start line.")]
        public float laneSpacing = 3f;
        [Tooltip("Back-stagger (metres) per racer so the grid isn't a perfect row.")]
        public float startStagger = 1.2f;

        public int ActiveLevel { get; private set; }

        private void Awake()
        {
            int lvl = GameManager.Instance != null ? GameManager.Instance.SelectedLevel : editorTestLevel;
            Apply(lvl);
            GateIntro(lvl);
        }

        private void Start()
        {
            if (repositionRacers) PlaceRacersAtStart();
        }

        /// <summary>Activate the chosen level's patch and route spline; disable the others.</summary>
        public void Apply(int lvl)
        {
            if (levelRoots != null && levelRoots.Length > 0)
                lvl = Mathf.Clamp(lvl, 0, levelRoots.Length - 1);
            ActiveLevel = lvl;

            if (levelRoots != null)
                for (int i = 0; i < levelRoots.Length; i++)
                    if (levelRoots[i] != null) levelRoots[i].SetActive(i == lvl);

            if (route != null)
            {
                route.splineIndex = lvl;
                route.Build();
            }
        }

        /// <summary>Level 1 plays the apartment cutscene; later levels skip straight to the countdown.</summary>
        private void GateIntro(int lvl)
        {
            if (lvl == 0) return; // Level 1 keeps the intro (it calls RaceManager.StartRace on finish).

            var cut = FindObjectOfType<CutsceneController>(true);
            if (cut != null)
            {
                if (cut.introStage) cut.introStage.SetActive(false);
                if (cut.introCamera) cut.introCamera.SetActive(false);
                if (cut.raceCamera) cut.raceCamera.SetActive(true);
                cut.gameObject.SetActive(false); // stop the dialogue overlay from playing
            }

            // No cutscene to trigger the start, so let RaceManager run the countdown itself.
            var rm = FindObjectOfType<RaceManager>(true);
            if (rm != null) rm.autoStart = true;
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
