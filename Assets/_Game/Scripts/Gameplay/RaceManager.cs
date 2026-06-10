using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mixtape.Core;
using Mixtape.Data;

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
        [Tooltip("Off by default: the in-scene opening intro calls StartRace() when it ends.")]
        public bool autoStart = false;
        public int countdownFrom = 3;

        [Header("Win Celebration")]
        public bool playWinCelebration = true;
        [Range(3f, 5f)] public float winCelebrationDuration = 5f;
        [Tooltip("Optional. If empty, RaceManager creates one at runtime.")]
        public RaceCelebrationDirector celebrationDirector;
        [Tooltip("Optional. If empty, the active CameraFollow is used.")]
        public CameraFollow celebrationCamera;

        [Header("Scoring")]
        [Tooltip("Trick/combo score, stars rating and coin reward for the player's run. Tunables live here.")]
        public RaceScore score = new RaceScore();
        /// <summary>The live run scoreboard (score/combo/tricks/distance, plus stars/coins at finish).</summary>
        public RaceScore Score => score;

        public event Action<int> CountdownTick;       // 3,2,1, then 0 = GO
        public event Action RaceStarted;
        public event Action<bool, int> RaceCelebrationStarted;  // fired immediately at finish before particles/camera delay
        public event Action<bool, int> RaceFinished;  // (playerWon, playerPlace)

        public bool IsRunning { get; private set; }
        public bool IsFinished { get; private set; }
        public int PlayerPlace { get; private set; } = 1;
        public int RacerCount => racers.Count;
        public float PlayerProgress =>
            (route != null && player != null) ? Mathf.Clamp01(route.Progress(player.transform.position) / Mathf.Max(1f, route.TotalLength)) : 0f;

        private float _prevProgress;
        private Coroutine _finishRoutine;

        private void Awake() => Instance = this;

        private IEnumerator Start()
        {
            if (route != null) route.Build();
            if (racers == null || racers.Count == 0)
                racers = new List<PhysicsSkater>(FindObjectsByType<PhysicsSkater>(FindObjectsSortMode.None));

            foreach (var r in racers) if (r != null) r.Active = false;

            // Cast the field: the player rides the live selection (RiderVisual.useSelected),
            // the AI take the characters the player did NOT pick, on random distinct boards.
            CastAIRiders();

            // Give every racer (player + AI) a 3D board loop. Done in code so newly-added
            // AI are covered without any scene wiring.
            foreach (var r in racers)
                if (r != null && r.GetComponent<SkaterAudio>() == null)
                    r.gameObject.AddComponent<SkaterAudio>();

            // Score tracking: listen to the player's clean-land + crash signals.
            score ??= new RaceScore();
            score.ResetRun();
            if (player != null)
            {
                player.Landed += OnPlayerLanded;
                player.Crashed += score.OnCrash;
            }

            if (autoStart) yield return StartCoroutine(CountdownAndGo());
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.Landed -= OnPlayerLanded;
                player.Crashed -= score.OnCrash;
            }
        }

        private void OnPlayerLanded(bool wasTrick)
        {
            if (IsRunning && wasTrick) score.OnTrickLanded();
        }

        /// <summary>
        /// Re-casts the AI riders so they never duplicate the player's selected character:
        /// each AI's RiderVisual gets one of the NON-selected characters from the database
        /// (the scene's forcedCharacter is just a fallback) plus a random, distinct board.
        /// Rebuilds the visual afterwards in case its own Start() already spawned the old cast.
        /// </summary>
        private void CastAIRiders()
        {
            var gm = GameManager.Instance;
            var db = gm != null ? gm.Database : null;
            if (db == null || db.CharacterCount == 0) return;

            int selected = Mathf.Clamp(gm.Data.selectedCharacter, 0, db.CharacterCount - 1);
            var others = new List<CharacterDef>();
            for (int i = 0; i < db.CharacterCount; i++)
                if (i != selected && db.GetCharacter(i) != null) others.Add(db.GetCharacter(i));
            if (others.Count == 0) return;

            // Shuffled board indices → each AI rides a random board, no two AI on the same one.
            var boardOrder = new List<int>();
            for (int i = 0; i < db.BoardCount; i++) boardOrder.Add(i);
            for (int i = boardOrder.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (boardOrder[i], boardOrder[j]) = (boardOrder[j], boardOrder[i]);
            }

            int next = 0;
            foreach (var r in racers)
            {
                if (r == null || r == player) continue;
                var visual = r.GetComponentInChildren<RiderVisual>(true);
                if (visual == null || visual.useSelected) continue;   // selection-driven riders keep theirs
                visual.forcedCharacter = others[next % others.Count];
                if (boardOrder.Count > 0) visual.forcedBoard = db.GetBoard(boardOrder[next % boardOrder.Count]);
                next++;
                visual.Build();
            }
        }

        /// <summary>Called by the opening intro when it finishes — begins the countdown.</summary>
        public void StartRace()
        {
            if (IsRunning || IsFinished) return;
            StartCoroutine(CountdownAndGo());
        }

        public IEnumerator CountdownAndGo()
        {
            IsFinished = false;
            // Crossfade cutscene/menu music into the race track as the countdown begins,
            // so the swap completes by "GO" (the crossfade fits inside the countdown).
            AudioManager.Instance?.PlayRaceMusic();
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
            if (route != null && player != null) _prevProgress = route.Progress(player.transform.position);
            RaceStarted?.Invoke();
        }

        private void Update()
        {
            if (!IsRunning || IsFinished) return;
            UpdateRanking();
            TickScore();
            if (route != null && player != null && route.IsFinish(player.transform.position))
                FinishRace();
        }

        private void TickScore()
        {
            if (player == null) return;
            float delta = 0f;
            if (route != null)
            {
                float p = route.Progress(player.transform.position);
                delta = Mathf.Max(0f, p - _prevProgress);   // forward-only; ignore respawn/backslide
                _prevProgress = p;
            }
            score.Tick(Time.deltaTime, player.IsGrounded, delta);
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
            if (_finishRoutine != null) return;

            IsRunning = false;
            IsFinished = true;
            UpdateRanking();
            bool won = PlayerPlace == 1;
            score.FinalizeForPlace(PlayerPlace);   // placement bonus + stars + coin reward
            StopAllRacers();
            RaceCelebrationStarted?.Invoke(won, PlayerPlace);
            _finishRoutine = StartCoroutine(FinishRaceSequence(won, PlayerPlace));
        }

        private IEnumerator FinishRaceSequence(bool won, int place)
        {
            if (won && playWinCelebration)
            {
                float duration = Mathf.Clamp(winCelebrationDuration, 3f, 5f);
                var director = EnsureCelebrationDirector();
                if (director != null)
                    duration = director.Play(racers, player != null ? player.transform : null, duration);

                var cam = EnsureCelebrationCamera();
                if (cam != null)
                    cam.BeginCelebrationOrbit(player != null ? player.transform : null, duration);

                yield return new WaitForSeconds(duration);
            }

            RaceFinished?.Invoke(won, place);
            _finishRoutine = null;
        }

        private void StopAllRacers()
        {
            if (racers == null || racers.Count == 0)
                racers = new List<PhysicsSkater>(FindObjectsByType<PhysicsSkater>(FindObjectsSortMode.None));

            foreach (var r in racers)
                if (r != null)
                    r.StopImmediately();
        }

        private RaceCelebrationDirector EnsureCelebrationDirector()
        {
            if (celebrationDirector != null) return celebrationDirector;
            celebrationDirector = GetComponent<RaceCelebrationDirector>();
            if (celebrationDirector == null) celebrationDirector = gameObject.AddComponent<RaceCelebrationDirector>();
            return celebrationDirector;
        }

        private CameraFollow EnsureCelebrationCamera()
        {
            if (celebrationCamera != null) return celebrationCamera;
            celebrationCamera = FindFirstObjectByType<CameraFollow>();
            return celebrationCamera;
        }
    }
}
