using System;
using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// The run's scoreboard: trick/combo SCORE, best combo, tricks landed, distance, plus the
    /// finish-time STARS rating and COINS reward. A plain (serializable) class so its tunables
    /// show in the <see cref="RaceManager"/> Inspector and it's drivable/testable without a
    /// MonoBehaviour. RaceManager forwards the player's land/crash signals and ticks the combo.
    ///
    /// Scoring model (see HANDOFF "stars economy"):
    ///  - Each LANDED trick scores trickBase * currentCombo (chaining pays off steeply) and
    ///    refreshes a combo timer; when the timer lapses (only counts down on the ground) or the
    ///    skater crashes, the combo resets to 0 — the score already banked stays.
    ///  - At the finish, placement adds a flat score bonus, sets the 1-3 star rating (position is
    ///    the main driver, a high score earns the extra star), and the coin reward = a per-place
    ///    base + score * coinRate (so coins scale with how well you skated, and a loss still pays).
    /// </summary>
    [Serializable]
    public class RaceScore
    {
        [Header("Trick scoring")]
        [Tooltip("Points for the FIRST trick in a chain. The Nth chained trick scores trickBase * N.")]
        public int trickBase = 100;
        [Tooltip("Seconds (on the ground) to land another trick before the combo resets.")]
        public float comboWindow = 3f;

        [Header("Placement (index 0 = 1st place)")]
        [Tooltip("Flat score added at the finish for each place.")]
        public int[] placementScoreBonus = { 5000, 2000, 500 };
        [Tooltip("Base coin reward for each place (added to the score-scaled coins).")]
        public int[] placementCoins = { 100, 50, 25 };
        [Tooltip("Coins earned per point of final score.")]
        public float coinRate = 0.02f;

        [Header("Stars")]
        [Tooltip("Final score at/above which the run earns one extra star (on top of the placement base).")]
        public int starScoreThreshold = 8000;

        // ---- runtime state (read by the HUD + results screen) ----
        public int Score { get; private set; }
        public int CurrentCombo { get; private set; }
        public int BestCombo { get; private set; }
        public int TricksLanded { get; private set; }
        /// <summary>Distance travelled along the route, in metres.</summary>
        public float Distance { get; private set; }
        /// <summary>1-3 star rating, computed at the finish by <see cref="Finalize"/>.</summary>
        public int Stars { get; private set; }
        /// <summary>Coin reward, computed at the finish by <see cref="Finalize"/>.</summary>
        public int Coins { get; private set; }

        private float _comboTimer;

        public void ResetRun()
        {
            Score = 0; CurrentCombo = 0; BestCombo = 0; TricksLanded = 0;
            Distance = 0f; Stars = 0; Coins = 0; _comboTimer = 0f;
        }

        /// <summary>A trick jump landed cleanly — extend the combo and bank its points.</summary>
        public void OnTrickLanded()
        {
            TricksLanded++;
            CurrentCombo++;
            if (CurrentCombo > BestCombo) BestCombo = CurrentCombo;
            Score += trickBase * CurrentCombo;
            _comboTimer = comboWindow;
        }

        /// <summary>The skater crashed — bail the current combo (banked score is kept).</summary>
        public void OnCrash()
        {
            CurrentCombo = 0;
            _comboTimer = 0f;
        }

        /// <summary>Per-frame tick from RaceManager while the race is running.</summary>
        public void Tick(float dt, bool grounded, float distanceDelta)
        {
            if (distanceDelta > 0f) Distance += distanceDelta;
            // The combo window only burns down on the ground, so a long jump doesn't drop the chain.
            if (CurrentCombo > 0 && grounded)
            {
                _comboTimer -= dt;
                if (_comboTimer <= 0f) CurrentCombo = 0;
            }
        }

        /// <summary>Apply the finishing place: placement score bonus, star rating, coin reward.</summary>
        public void FinalizeForPlace(int place)
        {
            int bonusIdx = Mathf.Clamp(place - 1, 0, Mathf.Max(0, placementScoreBonus.Length - 1));
            if (placementScoreBonus.Length > 0) Score += placementScoreBonus[bonusIdx];

            // Stars: position is the main driver (win = 2 base, otherwise 1), a strong score adds one.
            int stars = place == 1 ? 2 : 1;
            if (Score >= starScoreThreshold) stars++;
            Stars = Mathf.Clamp(stars, 1, 3);

            int coinIdx = Mathf.Clamp(place - 1, 0, Mathf.Max(0, placementCoins.Length - 1));
            int baseCoins = placementCoins.Length > 0 ? placementCoins[coinIdx] : 0;
            Coins = baseCoins + Mathf.FloorToInt(Score * coinRate);
        }
    }
}
