using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Typing sound for a <see cref="TypewriterEffect"/>: plays a clip when characters are revealed.
    /// A minimum interval keeps fast typing from stacking sounds, <see cref="TypewriterEffect.Skip"/> is
    /// silent, and random clips/pitch avoid a "machine gun" feel. No allocation per character.
    /// </summary>
    [RequireComponent(typeof(TypewriterEffect))]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Typewriter Sound")]
    public sealed class TypewriterSound : MonoBehaviour
    {
        [Tooltip("Empty = an AudioSource on this object (one is added if missing).")]
        public AudioSource source;
        [Tooltip("One is picked per sound; with several, the same clip is never played twice in a row.")]
        public AudioClip[] clips = new AudioClip[0];
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random pitch between x and y for each sound.")]
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
        [Tooltip("Play on every n-th sounding character (2 = every other one).")]
        [Min(1)] public int everyNthCharacter = 1;
        [Tooltip("Minimum seconds between two sounds (unscaled time).")]
        [Min(0f)] public float minInterval = 0.04f;
        public bool skipSpaces = true;
        public bool skipPunctuation;
        [Tooltip("No sound for the characters revealed at once by Skip.")]
        public bool muteOnSkip = true;

        /// <summary>Global mute, e.g. from the game's sound settings.</summary>
        public static bool Muted;

        TypewriterEffect _typer;
        int _count;
        int _lastClip = -1;
        float _lastTime = float.NegativeInfinity;
        int _lastFrame = -1;
        int _frameReveals;

        void Reset() => source = GetComponent<AudioSource>();

        void OnEnable()
        {
            if (_typer == null) _typer = GetComponent<TypewriterEffect>();
            _typer.onCharacterRevealed.AddListener(OnRevealed);
        }

        void OnDisable()
        {
            if (_typer != null) _typer.onCharacterRevealed.RemoveListener(OnRevealed);
        }

        AudioSource Source
        {
            get
            {
                if (source != null) return source;
                if (!TryGetComponent(out source))
                {
                    source = gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                }
                return source;
            }
        }

        void OnRevealed(int character)
        {
            // Normal typing reveals at most a couple of characters per frame; Skip reveals the rest at once.
            int frame = Time.frameCount;
            if (frame != _lastFrame)
            {
                _lastFrame = frame;
                _frameReveals = 0;
            }
            _frameReveals++;
            if (muteOnSkip && _frameReveals > 2) return;

            if (Muted || clips == null || clips.Length == 0 || !Application.isPlaying) return;
            TMPro.TMP_TextInfo info = _typer.Fx.Text.textInfo;
            if (info == null || character < 0 || character >= info.characterCount) return;
            char ch = info.characterInfo[character].character;
            if (skipSpaces && char.IsWhiteSpace(ch)) return;
            if (skipPunctuation && char.IsPunctuation(ch)) return;
            if (++_count % everyNthCharacter != 0) return;

            float now = Time.unscaledTime;
            if (now - _lastTime < minInterval) return;
            _lastTime = now;

            int index = 0;
            if (clips.Length > 1)
            {
                index = Random.Range(0, clips.Length - 1);
                if (_lastClip >= 0 && index >= _lastClip) index++; // skips the previous clip
            }
            _lastClip = index;
            AudioClip clip = clips[index];
            if (clip == null) return;

            AudioSource s = Source;
            s.pitch = Random.Range(pitchRange.x, pitchRange.y);
            s.PlayOneShot(clip, volume);
        }
    }
}
