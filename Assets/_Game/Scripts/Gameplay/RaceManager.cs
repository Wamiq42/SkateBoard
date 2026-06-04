using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Owns the race lifecycle: countdown, starting all racers together, live position
    /// ranking, and finish/win-lose detection. UI subscribes to the events.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        [Header("Setup")]
        public TrackPath path;
        public RacerMotor player;
        [Tooltip("All racers (player + AI). If empty, found automatically in the scene.")]
        public List<RacerMotor> racers = new List<RacerMotor>();

        [Header("Flow")]
        public bool autoStart = true;
        public int countdownFrom = 3;

        // Events for UI / audio.
        public event Action<int> CountdownTick;     // 3,2,1  (0 => "GO")
        public event Action RaceStarted;
        public event Action<bool, int> RaceFinished; // (playerWon, playerPlace 1-based)

        public bool IsRunning { get; private set; }
        public bool IsFinished { get; private set; }
        public int PlayerPlace { get; private set; } = 1;
        public int RacerCount => racers.Count;
        public float PlayerProgress => player != null ? player.NormalizedProgress : 0f;

        private void Awake()
        {
            Instance = this;
        }

        private IEnumerator Start()
        {
            if (racers == null || racers.Count == 0)
                racers = new List<RacerMotor>(FindObjectsByType<RacerMotor>(FindObjectsSortMode.None));

            foreach (var r in racers)
            {
                if (r == null) continue;
                if (r.path == null) r.path = path;
                r.SetRunning(false);
            }

            // Let AI know who the player is.
            foreach (var ai in FindObjectsByType<AIRacer>(FindObjectsSortMode.None))
                ai.Init(player);

            if (autoStart)
                yield return StartCoroutine(CountdownAndGo());
        }

        public IEnumerator CountdownAndGo()
        {
            IsFinished = false;
            for (int n = countdownFrom; n > 0; n--)
            {
                CountdownTick?.Invoke(n);
                yield return new WaitForSeconds(1f);
            }
            CountdownTick?.Invoke(0); // GO
            BeginRace();
        }

        public void BeginRace()
        {
            foreach (var r in racers)
                if (r != null) r.SetRunning(true);
            IsRunning = true;
            RaceStarted?.Invoke();
        }

        private void Update()
        {
            if (!IsRunning || IsFinished) return;

            UpdateRanking();

            if (player != null && player.IsFinished)
                FinishRace();
        }

        private void UpdateRanking()
        {
            if (player == null) return;
            int place = 1;
            foreach (var r in racers)
            {
                if (r == null || r == player) continue;
                if (r.Distance > player.Distance) place++;
            }
            PlayerPlace = place;
        }

        private void FinishRace()
        {
            IsRunning = false;
            IsFinished = true;
            UpdateRanking();
            bool won = PlayerPlace == 1;

            // Freeze opponents for the results moment.
            foreach (var r in racers)
                if (r != null && r != player) r.SetRunning(false);

            RaceFinished?.Invoke(won, PlayerPlace);
        }
    }
}
