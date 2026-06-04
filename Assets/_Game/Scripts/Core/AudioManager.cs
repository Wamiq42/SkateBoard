using UnityEngine;

namespace Mixtape.Core
{
    /// <summary>
    /// Lightweight audio hub. Clips are assigned later; calls no-op safely until then.
    /// Respects the sound/music toggles in PlayerData.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (musicSource == null) { musicSource = gameObject.AddComponent<AudioSource>(); musicSource.loop = true; musicSource.playOnAwake = false; }
            if (sfxSource == null) { sfxSource = gameObject.AddComponent<AudioSource>(); sfxSource.playOnAwake = false; }
        }

        public bool SoundOn => GameManager.Instance == null || GameManager.Instance.Data.soundOn;
        public bool MusicOn => GameManager.Instance == null || GameManager.Instance.Data.musicOn;

        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null || !SoundOn) return;
            sfxSource.PlayOneShot(clip, volume);
        }

        public void PlayMusic(AudioClip clip, float volume = 0.6f)
        {
            if (clip == null) return;
            musicSource.clip = clip; musicSource.volume = volume;
            if (MusicOn) musicSource.Play();
        }

        public void RefreshMusicState()
        {
            if (musicSource == null) return;
            if (MusicOn && musicSource.clip != null && !musicSource.isPlaying) musicSource.Play();
            else if (!MusicOn && musicSource.isPlaying) musicSource.Pause();
        }
    }
}
