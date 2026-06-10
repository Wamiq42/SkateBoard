using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mixtape.Core
{
    /// <summary>
    /// Audio hub. Persists across scenes (DontDestroyOnLoad). Owns the music tracks and
    /// the looping board SFX clip, and crossfades between music tracks.
    ///
    /// Music model: the <see cref="menuMusic"/> track plays on the menu AND the in-scene
    /// cutscene (same clip, so it continues seamlessly into the Game scene). When the race
    /// countdown begins, it crossfades into <see cref="raceMusic"/>. Returning to the menu
    /// (or reloading) crossfades back.
    ///
    /// Respects the sound/music toggles in PlayerData.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Music clips")]
        [Tooltip("Menu + opening-cutscene track. One clip for both so it carries over seamlessly.")]
        [SerializeField] private AudioClip menuMusic;
        [Tooltip("Race track. Crossfaded in as the countdown starts.")]
        [SerializeField] private AudioClip raceMusic;

        [Header("SFX clips")]
        [Tooltip("Looping board-on-road sound. Played 3D on each racer by SkaterAudio.")]
        [SerializeField] private AudioClip skateboardLoop;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.4f;
        [Tooltip("Seconds to crossfade between music tracks (fits inside the race countdown).")]
        [SerializeField] private float crossfadeSeconds = 2.5f;

        [Header("Sources (auto-created if empty)")]
        [SerializeField] private AudioSource musicA;
        [SerializeField] private AudioSource musicB;
        [SerializeField] private AudioSource sfxSource;

        /// <summary>The looping board SFX clip, fetched by per-skater <c>SkaterAudio</c>.</summary>
        public AudioClip SkateboardLoop => skateboardLoop;
        public bool SoundOn => GameManager.Instance == null || GameManager.Instance.Data.soundOn;
        public bool MusicOn => GameManager.Instance == null || GameManager.Instance.Data.musicOn;

        private AudioSource _activeMusic;   // current foreground source
        private AudioClip _currentClip;     // intended track (independent of the MusicOn toggle)
        private Coroutine _fade;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            musicA = EnsureSource(musicA, loop: true);
            musicB = EnsureSource(musicB, loop: true);
            sfxSource = EnsureSource(sfxSource, loop: false);
            _activeMusic = musicA;
        }

        private AudioSource EnsureSource(AudioSource s, bool loop)
        {
            if (s == null) s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f; // 2D
            return s;
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        // sceneLoaded does not fire for the scene already open when we boot, so kick it here.
        private void Start() => PlayMenuMusic();

        private void OnSceneLoaded(Scene s, LoadSceneMode m) => PlayMenuMusic();

        /// <summary>Menu + cutscene ambient track. No-op if it is already the current track.</summary>
        public void PlayMenuMusic() => Crossfade(menuMusic);

        /// <summary>Race track — crossfaded in over the countdown.</summary>
        public void PlayRaceMusic() => Crossfade(raceMusic);

        private void Crossfade(AudioClip clip)
        {
            if (clip == null || clip == _currentClip) return; // idempotent: don't restart the same track
            _currentClip = clip;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(CrossfadeRoutine(clip));
        }

        private IEnumerator CrossfadeRoutine(AudioClip clip)
        {
            AudioSource from = _activeMusic;
            AudioSource to = (from == musicA) ? musicB : musicA;

            to.clip = clip;
            to.volume = 0f;
            if (MusicOn) to.Play();
            _activeMusic = to;

            float dur = Mathf.Max(0.01f, crossfadeSeconds);
            float fromStart = from.volume;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                to.volume = (MusicOn ? musicVolume : 0f) * k;
                from.volume = fromStart * (1f - k);
                yield return null;
            }
            to.volume = MusicOn ? musicVolume : 0f;
            from.volume = 0f;
            from.Stop();
            from.clip = null;
            _fade = null;
        }

        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null || !SoundOn) return;
            sfxSource.PlayOneShot(clip, volume);
        }

        /// <summary>Back-compat: set and play a music track immediately (no crossfade).</summary>
        public void PlayMusic(AudioClip clip, float volume = 0.6f)
        {
            if (clip == null) return;
            if (_fade != null) { StopCoroutine(_fade); _fade = null; }
            musicVolume = volume;
            _currentClip = clip;
            _activeMusic.clip = clip;
            _activeMusic.volume = volume;
            if (MusicOn) _activeMusic.Play();
        }

        /// <summary>Pause/resume the music to match the PlayerData music toggle.</summary>
        public void RefreshMusicState()
        {
            if (_activeMusic == null) return;
            if (MusicOn)
            {
                if (_activeMusic.clip == null && _currentClip != null) _activeMusic.clip = _currentClip;
                _activeMusic.volume = musicVolume;
                if (_activeMusic.clip != null && !_activeMusic.isPlaying) _activeMusic.Play();
            }
            else if (_activeMusic.isPlaying)
            {
                _activeMusic.Pause();
            }
        }
    }
}
