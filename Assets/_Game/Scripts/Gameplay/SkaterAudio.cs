using UnityEngine;
using Mixtape.Core;

namespace Mixtape.Gameplay
{
    /// <summary>
    /// 3D looping skateboard-on-road sound for a single skater (player or AI). The loop
    /// runs continuously but its volume/pitch follow the board's speed, and it fades to
    /// silent when the board is airborne, stopped, or before the race is live. Self-builds
    /// a child AudioSource and pulls the clip from <see cref="AudioManager"/>, so it can be
    /// added to any racer at runtime with no wiring.
    /// </summary>
    [RequireComponent(typeof(PhysicsSkater))]
    public class SkaterAudio : MonoBehaviour
    {
        [Tooltip("Speed (units/s) below which the board is treated as not rolling.")]
        public float minRollSpeed = 2f;
        [Range(0f, 1f)] public float maxVolume = 0.35f;
        [Tooltip("Volume floor while just barely rolling (ramps up to maxVolume at cruise).")]
        [Range(0f, 1f)] public float minVolume = 0.1f;
        [Tooltip("3D rolloff: distance at which the board sound fades out.")]
        public float maxDistance = 35f;
        [Tooltip("Pitch when at cruise speed (1 = unaltered). Gives a sense of pace.")]
        public float pitchAtCruise = 1.1f;
        [Tooltip("How quickly the volume eases toward its target.")]
        public float volumeLerp = 8f;

        private PhysicsSkater _skater;
        private AudioSource _src;

        private void Awake() => _skater = GetComponent<PhysicsSkater>();

        private void Start()
        {
            var clip = AudioManager.Instance != null ? AudioManager.Instance.SkateboardLoop : null;

            var go = new GameObject("SkateLoop");
            go.transform.SetParent(transform, false);
            _src = go.AddComponent<AudioSource>();
            _src.clip = clip;
            _src.loop = true;
            _src.playOnAwake = false;
            _src.spatialBlend = 1f;                 // full 3D so you hear rivals around you
            _src.rolloffMode = AudioRolloffMode.Linear;
            _src.minDistance = 3f;
            _src.maxDistance = maxDistance;
            _src.dopplerLevel = 0f;                 // racers move fast; doppler would warble the loop
            _src.volume = 0f;
            if (clip != null) _src.Play();          // play silent; volume gates audibility (no start/stop pops)
        }

        private void Update()
        {
            if (_src == null) return;

            bool soundOn = AudioManager.Instance == null || AudioManager.Instance.SoundOn;
            // Only roll while the race is actually live. The player keeps Active=true and
            // coasts past the finish line, so gate on RaceManager state too — otherwise the
            // loop would keep playing after the race completes.
            var race = RaceManager.Instance;
            bool raceLive = race == null || (race.IsRunning && !race.IsFinished);
            bool rolling = soundOn && raceLive && _skater.Active && _skater.IsGrounded && _skater.Speed > minRollSpeed;

            float cruise = Mathf.Max(1f, _skater.baseSpeed);
            float speed01 = Mathf.Clamp01(_skater.Speed / cruise);

            float target = rolling ? Mathf.Lerp(minVolume, maxVolume, speed01) : 0f;
            _src.volume = Mathf.MoveTowards(_src.volume, target, volumeLerp * Time.deltaTime);
            _src.pitch = Mathf.Lerp(0.9f, pitchAtCruise, speed01);
        }
    }
}
