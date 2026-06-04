using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Owns the race lifecycle for the physics racers: countdown (gates each skater's
    /// Active flag), live ranking by route progress, and finish/win-lose. UI subscribes
    /// to the events. Progress is measured against the sparse <see cref="RaceRoute"/>.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        [Header("Setup")]
        public RaceRoute route;
        public PhysicsSkater player;
        [Tooltip("All racers (player + AI). If empty, found automatically.")]
        public List<PhysicsSkater> racers = new List<PhysicsSkater>();

        [Header("Flow")]
        public bool autoStart = true;
        public int countdownFrom = 3;

        public event Action<int> CountdownTick;       // 3,2,1, then 0 = GO
        public event Action RaceStarted;
        public event Action<bool, int> RaceFinished;  // (playerWon, playerPlace)

        public bool IsRunning { get; private set; }
        public bool IsFinished { get; private set; }
        public int PlayerPlace { get; private set; } = 1;
        public int RacerCount => racers.Count;
        public float PlayerProgress =>
            (route != null && player != null) ? Mathf.Clamp01(route.Progress(player.transform.position) / Mathf.Max(1f, route.TotalLength)) : 0f;

        private void Awake() => Instance = this;

        private IEnumerator Start()
        {
            if (route != null) route.Build();
            if (racers == null || racers.Count == 0)
                racers = new List<PhysicsSkater>(FindObjectsByType<PhysicsSkater>(FindObjectsSortMode.None));

            foreach (var r in racers) if (r != null) r.Active = false;

            if (autoStart) yield return StartCoroutine(CountdownAndGo());
        }

        public IEnumerator CountdownAndGo()
        {
            IsFinished = false;
            for (int n = countdownFrom; n > 0; n--)
            {
                CountdownTick?.Invoke(n);
                yield return new WaitForSeconds(1f);
            }
            CountdownTick?.Invoke(0);
            BeginRace();
        }

        public void BeginRace()
        {
            foreach (var r in racers) if (r != null) r.Active = true;
            IsRunning = true;
            RaceStarted?.Invoke();
        }

        private void Update()
        {
            if (!IsRunning || IsFinished) return;
            UpdateRanking();
            if (route != null && player != null && route.IsFinish(player.transform.position))
                FinishRace();
        }

        private void UpdateRanking()
        {
            if (player == null || route == null) return;
            float pp = route.Progress(player.transform.position);
            int place = 1;
            foreach (var r in racers)
            {
                if (r == null || r == player) continue;
                if (route.Progress(r.transform.position) > pp) place++;
            }
            PlayerPlace = place;
        }

        private void FinishRace()
        {
            IsRunning = false;
            IsFinished = true;
            UpdateRanking();
            bool won = PlayerPlace == 1;
            foreach (var r in racers) if (r != null && r != player) r.Active = false;
            RaceFinished?.Invoke(won, PlayerPlace);
        }
    }
}
