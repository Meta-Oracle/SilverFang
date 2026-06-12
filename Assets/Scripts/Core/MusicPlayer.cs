using UnityEngine;

namespace SilverFang.Core
{
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip[] playlist;
        [SerializeField, Range(0f, 1f)] private float volume = 0.55f;
        [SerializeField] private bool loopPlaylist = true;
        [SerializeField] private bool playOnAwake = true;
        [SerializeField] private bool persistAcrossScenes = true;
        [SerializeField] private float fadeInSeconds = 1.25f;

        private static MusicPlayer instance;

        private AudioSource source;
        private int index;
        private float fadeTimer;
        private float trackTimer;

        private void Awake()
        {
            if (persistAcrossScenes && instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (persistAcrossScenes)
                DontDestroyOnLoad(gameObject);

            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = playlist != null && playlist.Length <= 1;
            source.volume = fadeInSeconds > 0f ? 0f : volume;
            source.ignoreListenerPause = true;

            if (playOnAwake)
                Play(0);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            if (source == null) return;

            if (fadeInSeconds > 0f && source.isPlaying && source.volume < volume)
            {
                fadeTimer += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(0f, volume, Mathf.Clamp01(fadeTimer / fadeInSeconds));
            }

            if (source.clip == null || source.loop) return;

            trackTimer += Time.unscaledDeltaTime;
            if (trackTimer >= source.clip.length || !source.isPlaying)
                PlayNext();
        }

        public void Play(int trackIndex)
        {
            if (playlist == null || playlist.Length == 0) return;

            index = Mathf.Clamp(trackIndex, 0, playlist.Length - 1);
            var clip = playlist[index];
            if (clip == null) return;

            source.clip = clip;
            source.loop = playlist.Length == 1 && loopPlaylist;
            source.volume = fadeInSeconds > 0f ? 0f : volume;
            fadeTimer = 0f;
            trackTimer = 0f;
            source.Play();
        }

        public void PlayNext()
        {
            if (playlist == null || playlist.Length == 0) return;

            int next = index + 1;
            if (next >= playlist.Length)
            {
                if (!loopPlaylist)
                {
                    source.Stop();
                    return;
                }
                next = 0;
            }

            Play(next);
        }
    }
}
