using UnityEngine;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// Computer-controlled opponent. Uses the same <see cref="RacerMotor"/> as the player
    /// for fairness, with mild lateral wander, a per-racer speed bias, and light
    /// rubber-banding so races stay close and fun.
    /// </summary>
    [RequireComponent(typeof(RacerMotor))]
    public class AIRacer : MonoBehaviour
    {
        [Header("Personality")]
        [Tooltip("Base speed bias vs the player (1 = same as motor base).")]
        public float skill = 1f;
        [Tooltip("How far it drifts side to side (0..1 of road width).")]
        [Range(0f, 1f)] public float wander = 0.6f;
        public float wanderFrequency = 0.4f;

        [Header("Rubber banding")]
        public bool rubberBand = true;
        public float catchUpMul = 1.08f;
        public float easeOffMul = 0.95f;

        private RacerMotor _motor;
        private RacerMotor _player;
        private float _noiseSeed;

        private void Awake()
        {
            _motor = GetComponent<RacerMotor>();
            _noiseSeed = (GetInstanceID() & 0xFFFF) * 0.013f;
        }

        public void Init(RacerMotor player) => _player = player;

        private void Update()
        {
            if (_motor.path == null) return;

            // Lateral wander via smooth noise.
            float n = Mathf.PerlinNoise(_noiseSeed, Time.time * wanderFrequency) * 2f - 1f;
            _motor.targetLateral = n * wander * _motor.path.RoadHalfWidth;

            // Speed: base skill, optionally rubber-banded toward the player.
            float mul = skill;
            if (rubberBand && _player != null)
            {
                float gap = _player.Distance - _motor.Distance;
                if (gap > 6f) mul *= catchUpMul;       // falling behind -> speed up
                else if (gap < -6f) mul *= easeOffMul; // far ahead -> ease off
            }
            _motor.speedMultiplier = mul;
        }
    }
}
