using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace com.nmd.nkl.text
{
    /// <summary>
    /// Base class for per-character effects. Subclasses edit <see cref="TMP_TextInfo.meshInfo"/>
    /// in <see cref="Apply"/>; the vertices are reset to the generated mesh before every call.
    /// Iterate the affected characters with <see cref="RangeCount"/>/<see cref="GetRange"/>:
    /// the whole text, or only the parts inside this effect's tag, minus &lt;nofx&gt; parts.
    /// Per-range tag arguments (&lt;wave a=10&gt;) come from <see cref="GetRangeArgs"/>.
    /// </summary>
    [RequireComponent(typeof(TextFX))]
    public abstract class TextEffect : MonoBehaviour
    {
        [Tooltip("Empty = whole text. Otherwise only text inside <tag>...</tag>, e.g. \"wave\" for <wave>Hi</wave>.")]
        [SerializeField] string _tag = "";
        [SerializeField] bool _playOnEnable = true;

        readonly List<Vector2Int> _ranges = new List<Vector2Int>();
        readonly List<TagArgs> _rangeArgs = new List<TagArgs>();
        TextFX _fx;
        float _time;
        bool _playing;
        int _textVersion = -1;
        int _affectedCount;

        public string TagName => _tag;
        public bool IsPlaying => _playing && isActiveAndEnabled;

        /// <summary>True for effects that never end on their own (they keep the canvas rebatching).</summary>
        public virtual bool IsLooping => false;

        /// <summary>
        /// False for effects whose result does not change over time (e.g. a non-scrolling gradient).
        /// They are applied once and again only when the text is rebuilt: no per-frame cost.
        /// Static effects also show in edit mode when their component runs in edit mode.
        /// </summary>
        public virtual bool IsAnimated => true;

        public TextFX Fx => _fx != null ? _fx : (_fx = GetComponent<TextFX>());

        /// <summary>Seconds since <see cref="Play"/> (unscaled).</summary>
        protected float Time => _time;

        /// <summary>Number of affected ranges; each range is [x, y) in character indices.</summary>
        protected int RangeCount => _ranges.Count;
        protected Vector2Int GetRange(int index) => _ranges[index];

        /// <summary>Total number of affected characters (visible or not).</summary>
        protected int AffectedCount => _affectedCount;

        protected virtual void OnEnable()
        {
            Fx.Register(this);
            if (_playOnEnable && (Application.isPlaying || !IsAnimated)) Play();
        }

        protected virtual void OnDisable()
        {
            _playing = false;
            if (_fx == null) return;
            _fx.Unregister(this);
            _fx.Wake(); // one more tick restores the mesh without this effect
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            _textVersion = -1;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || Fx == null) return;
                Fx.Reparse();
                // Static effects in edit mode start/refresh here so inspector changes show immediately.
                if (isActiveAndEnabled && _playOnEnable && !Application.isPlaying && !IsAnimated && !_playing) Play();
                if (_playing) Fx.Wake();
            };
        }
#endif

        public void Play()
        {
            Fx.Register(this);
            _time = 0f;
            _playing = true;
            OnPlay();
            Fx.Wake();
        }

        public void Stop()
        {
            if (!_playing) return;
            _playing = false;
            Fx.Wake(); // one more tick restores the mesh without this effect
        }

        /// <summary>Called by <see cref="Play"/>.</summary>
        protected virtual void OnPlay() { }

        /// <summary>Called when the text mesh was regenerated and the ranges were rebuilt.</summary>
        protected virtual void OnTextRebuilt() { }

        protected void ResetTime() => _time = 0f;

        internal void Step(TMP_TextInfo info, float deltaTime, int textVersion)
        {
            if (textVersion != _textVersion)
            {
                _textVersion = textVersion;
                BuildRanges(info);
                OnTextRebuilt();
            }

            _time += deltaTime;
            if (!Apply(info)) _playing = false;
        }

        /// <summary>Edit the mesh of <paramref name="info"/>. Return false once the effect has finished.</summary>
        protected abstract bool Apply(TMP_TextInfo info);

        /// <summary>
        /// Short name used by &lt;nofx=...&gt;: the class name without "Effect", lower case
        /// ("gradient", "wave", "rainbow"...). The effect's tag name works too.
        /// </summary>
        public string EffectKey
        {
            get
            {
                if (_effectKey != null) return _effectKey;
                string name = GetType().Name;
                if (name.EndsWith("Effect", System.StringComparison.Ordinal)) name = name.Substring(0, name.Length - 6);
                return _effectKey = name.ToLowerInvariant();
            }
        }

        string _effectKey;

        /// <summary>
        /// Override to consume point tags (no closing tag needed), e.g. the typewriter's &lt;p=0.5&gt;.
        /// The tag is removed from the text and appears in <see cref="TextFX.Spans"/> with IsPoint.
        /// </summary>
        protected internal virtual bool ClaimsPointTag(string text, int start, int length) => false;

        /// <summary>Arguments of the tag that produced range <paramref name="index"/> (&lt;wave a=10&gt;).</summary>
        protected TagArgs GetRangeArgs(int index) => _rangeArgs[index];

        void BuildRanges(TMP_TextInfo info)
        {
            _ranges.Clear();
            _rangeArgs.Clear();
            _affectedCount = 0;
            int count = Mathf.Min(info.characterCount, info.characterInfo.Length);
            IReadOnlyList<TagSpan> spans = Fx.Spans;

            if (string.IsNullOrEmpty(_tag))
            {
                if (count > 0) AddRange(0, count, TagArgs.Empty);
            }
            else
            {
                for (int i = 0; i < spans.Count; i++)
                {
                    TagSpan span = spans[i];
                    if (span.IsPoint || !string.Equals(span.Name, _tag, System.StringComparison.OrdinalIgnoreCase)) continue;
                    TextFX.MapToCharacters(info, span.Start, span.End, out int first, out int last);
                    if (last > first) AddRange(first, last, TagArgs.Parse(span.Args));
                }
            }

            for (int i = 0; i < spans.Count; i++)
            {
                TagSpan span = spans[i];
                if (span.IsPoint || span.Name != TextFX.NoFxTag || !Excludes(span.Args)) continue;
                TextFX.MapToCharacters(info, span.Start, span.End, out int first, out int last);
                if (last > first) Subtract(first, last);
            }

            for (int i = 0; i < _ranges.Count; i++)
                _affectedCount += _ranges[i].y - _ranges[i].x;
        }

        void AddRange(int first, int last, TagArgs args)
        {
            _ranges.Add(new Vector2Int(first, last));
            _rangeArgs.Add(args);
        }

        /// <summary>Removes [first, last) from every range, splitting ranges when needed.</summary>
        void Subtract(int first, int last)
        {
            for (int i = _ranges.Count - 1; i >= 0; i--)
            {
                Vector2Int r = _ranges[i];
                if (last <= r.x || first >= r.y) continue;
                TagArgs args = _rangeArgs[i];
                _ranges.RemoveAt(i);
                _rangeArgs.RemoveAt(i);
                if (last < r.y)
                {
                    _ranges.Insert(i, new Vector2Int(last, r.y));
                    _rangeArgs.Insert(i, args);
                }
                if (first > r.x)
                {
                    _ranges.Insert(i, new Vector2Int(r.x, first));
                    _rangeArgs.Insert(i, args);
                }
            }
        }

        /// <summary>&lt;nofx&gt; excludes every effect; &lt;nofx=gradient,wave&gt; only the listed ones.</summary>
        bool Excludes(string args)
        {
            if (string.IsNullOrEmpty(args)) return true;
            string list = args[0] == '=' ? args.Substring(1) : args;
            foreach (string raw in list.Split(','))
            {
                string key = raw.Trim().Trim('"', '\'');
                if (key.Length == 0) continue;
                if (string.Equals(key, EffectKey, System.StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrEmpty(_tag) && string.Equals(key, _tag, System.StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>Character index in the text of the n-th affected character.</summary>
        protected int CharacterIndexAt(int order)
        {
            for (int r = 0; r < _ranges.Count; r++)
            {
                int length = _ranges[r].y - _ranges[r].x;
                if (order < length) return _ranges[r].x + order;
                order -= length;
            }
            return -1;
        }

        /// <summary>True for inline sprites (&lt;sprite=0&gt;, icons) as opposed to font glyphs.</summary>
        protected static bool IsSprite(TMP_TextInfo info, int charIndex) =>
            info.characterInfo[charIndex].elementType == TMP_TextElementType.Sprite;

        /// <summary>Visible and backed by the current mesh buffers; use it to skip characters in Apply.</summary>
        protected static bool IsDrawable(TMP_TextInfo info, int charIndex)
        {
            if (charIndex >= info.characterCount || charIndex >= info.characterInfo.Length) return false;
            TMP_CharacterInfo c = info.characterInfo[charIndex];
            if (!c.isVisible || c.materialReferenceIndex >= info.meshInfo.Length) return false;
            TMP_MeshInfo mesh = info.meshInfo[c.materialReferenceIndex];
            return mesh.vertices != null && mesh.colors32 != null &&
                   c.vertexIndex + 3 < Mathf.Min(mesh.vertices.Length, mesh.colors32.Length);
        }

        protected static void Offset(TMP_TextInfo info, int charIndex, Vector3 delta)
        {
            TMP_CharacterInfo c = info.characterInfo[charIndex];
            Vector3[] v = info.meshInfo[c.materialReferenceIndex].vertices;
            int i = c.vertexIndex;
            v[i] += delta;
            v[i + 1] += delta;
            v[i + 2] += delta;
            v[i + 3] += delta;
        }

        /// <summary>Scales a character around its own center.</summary>
        protected static void Scale(TMP_TextInfo info, int charIndex, float scale)
        {
            TMP_CharacterInfo c = info.characterInfo[charIndex];
            Vector3[] v = info.meshInfo[c.materialReferenceIndex].vertices;
            int i = c.vertexIndex;
            Vector3 center = (v[i] + v[i + 2]) * 0.5f;
            v[i] = center + (v[i] - center) * scale;
            v[i + 1] = center + (v[i + 1] - center) * scale;
            v[i + 2] = center + (v[i + 2] - center) * scale;
            v[i + 3] = center + (v[i + 3] - center) * scale;
        }

        /// <summary>Rotates a character around its own center, in degrees.</summary>
        protected static void Rotate(TMP_TextInfo info, int charIndex, float degrees)
        {
            TMP_CharacterInfo c = info.characterInfo[charIndex];
            Vector3[] v = info.meshInfo[c.materialReferenceIndex].vertices;
            int i = c.vertexIndex;
            Vector3 center = (v[i] + v[i + 2]) * 0.5f;
            Quaternion rotation = Quaternion.Euler(0f, 0f, degrees);
            v[i] = center + rotation * (v[i] - center);
            v[i + 1] = center + rotation * (v[i + 1] - center);
            v[i + 2] = center + rotation * (v[i + 2] - center);
            v[i + 3] = center + rotation * (v[i + 3] - center);
        }

        protected static void MultiplyAlpha(TMP_TextInfo info, int charIndex, float alpha)
        {
            TMP_CharacterInfo c = info.characterInfo[charIndex];
            Color32[] colors = info.meshInfo[c.materialReferenceIndex].colors32;
            int i = c.vertexIndex;
            for (int k = 0; k < 4; k++)
                colors[i + k].a = (byte)(colors[i + k].a * alpha);
        }

        /// <summary>Blends the RGB of a character towards <paramref name="color"/>, keeping its alpha.</summary>
        protected static void TintColor(TMP_TextInfo info, int charIndex, Color32 color, float amount)
        {
            TMP_CharacterInfo c = info.characterInfo[charIndex];
            Color32[] colors = info.meshInfo[c.materialReferenceIndex].colors32;
            int i = c.vertexIndex;
            for (int k = 0; k < 4; k++)
            {
                Color32 src = colors[i + k];
                Color32 tinted = Color32.Lerp(src, color, amount);
                tinted.a = src.a;
                colors[i + k] = tinted;
            }
        }
    }
}
