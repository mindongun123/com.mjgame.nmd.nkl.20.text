using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.nmd.nkl.text
{
    /// <summary>
    /// Drives the <see cref="TextEffect"/> components on the same GameObject.
    /// Effects only edit vertex positions/colors of the already generated TMP mesh,
    /// so the text is never re-laid-out per frame and no material is instantiated.
    /// It is also the text's preprocessor: package tags (&lt;wave&gt;...&lt;/wave&gt;, &lt;nofx&gt;,
    /// &lt;p=0.5&gt;...) are removed from the text and their positions recorded as <see cref="Spans"/>.
    /// Unlike TMP links they can nest and carry arguments.
    /// <para>
    /// A TextFX with no playing effect is asleep: it is not in the update loop and does not
    /// listen to text-change events, so idle texts (e.g. hundreds of shop items) cost nothing.
    /// When only static effects play (see <see cref="TextEffect.IsAnimated"/>), it leaves the
    /// update loop too and only re-applies them when TMP rebuilds the text.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("com.nmd.nkl/Text/Text FX")]
    public sealed class TextFX : MonoBehaviour, ITextPreprocessor
    {
        /// <summary>Excludes its content from effects: &lt;nofx&gt; (all) or &lt;nofx=gradient,wave&gt;.</summary>
        public const string NoFxTag = "nofx";

        readonly List<TextEffect> _effects = new List<TextEffect>();
        readonly List<TagSpan> _spans = new List<TagSpan>();
        readonly List<int> _openSpans = new List<int>();
        readonly List<TextEffect> _scratch = new List<TextEffect>();
        readonly List<Follower> _followers = new List<Follower>();
        readonly MeshCache _source = new MeshCache();
        readonly StringBuilder _builder = new StringBuilder();
        TMP_Text _text;
        bool _listening;
        bool _inRunner;
        bool _sourceDirty = true;
        bool _modified;
        bool _ticking;
        int _textVersion;

        public TMP_Text Text => _text != null ? _text : (_text = GetComponent<TMP_Text>());

        /// <summary>True while at least one effect on this text is playing.</summary>
        public bool IsPlaying
        {
            get
            {
                for (int i = 0; i < _effects.Count; i++)
                    if (_effects[i] != null && _effects[i].IsPlaying) return true;
                return false;
            }
        }

        /// <summary>Package tags of the last preprocessed text, in order of their opening position.</summary>
        public IReadOnlyList<TagSpan> Spans => _spans;

        /// <summary>
        /// First and last+1 character index whose source position lies in [start, end).
        /// For a point (start == end) both are the first character at or after it.
        /// </summary>
        public static void MapToCharacters(TMP_TextInfo info, int start, int end, out int first, out int last)
        {
            int count = Mathf.Min(info.characterCount, info.characterInfo.Length);
            first = count;
            for (int c = 0; c < count; c++)
            {
                if (info.characterInfo[c].index < start) continue;
                first = c;
                break;
            }
            last = first;
            if (end <= start) return;
            while (last < count && info.characterInfo[last].index < end) last++;
        }

        /// <summary>True while effects are applied: animated (in the update loop) or static (on rebuild).</summary>
        public bool IsAwake => _listening;

        /// <summary>True while this text is ticked every frame.</summary>
        public bool IsInUpdateLoop => _inRunner;

        bool IsAnimating
        {
            get
            {
                for (int i = 0; i < _effects.Count; i++)
                    if (_effects[i] != null && _effects[i].IsPlaying && _effects[i].IsAnimated) return true;
                return false;
            }
        }

        public void PlayAll()
        {
            GetComponents(_scratch);
            for (int i = 0; i < _scratch.Count; i++)
                if (_scratch[i].enabled) _scratch[i].Play();
        }

        /// <summary>Stops all effects; the text goes back to normal on the next update.</summary>
        public void StopAll()
        {
            for (int i = 0; i < _effects.Count; i++)
                if (_effects[i] != null) _effects[i].Stop();
        }

        /// <summary>
        /// Stops all effects and restores the mesh immediately. Use it when a pooled/recycled
        /// item gets new content, before calling <see cref="PlayAll"/> again.
        /// </summary>
        public void ResetAll()
        {
            StopAll();
            RestoreNow();
            Sleep();
        }

        /// <summary>
        /// Makes another text copy this text's effect offsets every frame (used by <see cref="TextExtrude"/>).
        /// The follower must have the same text and layout settings.
        /// </summary>
        public void AddFollower(TMP_Text follower)
        {
            if (follower == null || follower == Text) return;
            for (int i = 0; i < _followers.Count; i++)
                if (_followers[i].Text == follower) return;
            _followers.Add(new Follower { Text = follower });
            if (_listening) TextChangeDispatcher.Watch(follower, this);
        }

        public void RemoveFollower(TMP_Text follower)
        {
            for (int i = _followers.Count - 1; i >= 0; i--)
                if (_followers[i].Text == follower) _followers.RemoveAt(i);
            TextChangeDispatcher.Unwatch(follower, this);
        }

        /// <summary>Re-parses the text so tags are found again (after a tag name changed).</summary>
        public void Reparse()
        {
            if (Text != null && Text.isActiveAndEnabled) Text.ForceMeshUpdate(false, true);
        }

        internal void Register(TextEffect effect)
        {
            if (!_effects.Contains(effect)) _effects.Add(effect);
        }

        internal void Unregister(TextEffect effect) => _effects.Remove(effect);

        /// <summary>
        /// Schedules a tick: called when an effect starts, stops or changes. The tick then decides
        /// whether the text stays in the update loop.
        /// </summary>
        internal void Wake()
        {
            if (!isActiveAndEnabled) return;
            if (!_listening)
            {
                _listening = true;
                // While asleep the mesh is untouched, so whatever TMP holds now is the source.
                _sourceDirty = true;
                TextChangeDispatcher.Watch(Text, this);
                for (int i = 0; i < _followers.Count; i++)
                {
                    _followers[i].Dirty = true;
                    TextChangeDispatcher.Watch(_followers[i].Text, this);
                }
            }
            if (!_inRunner)
            {
                _inRunner = true;
                TextFXRunner.Add(this);
            }
        }

        void Sleep()
        {
            if (_listening)
            {
                _listening = false;
                TextChangeDispatcher.Unwatch(_text, this);
                for (int i = 0; i < _followers.Count; i++) TextChangeDispatcher.Unwatch(_followers[i].Text, this);
            }
            LeaveUpdateLoop();
        }

        void LeaveUpdateLoop()
        {
            if (!_inRunner) return;
            _inRunner = false;
            TextFXRunner.Remove(this);
        }

        void OnEnable()
        {
            // The preprocessor is not serialized: assign it after loads/reloads, then reparse once so the
            // tags get stripped. Re-enabling (pooled items) keeps it and needs no reparse.
            bool assigned = Text.textPreprocessor == null;
            if (assigned) Text.textPreprocessor = this;
            if (TryGetComponent(out TextExtrude extrude) && extrude.Layer != null) AddFollower(extrude.Layer);
            if (assigned && HasEffectTags()) Reparse();
            if (IsPlaying) Wake();
        }

        void OnDisable()
        {
            RestoreNow();
            Sleep();
        }

        void OnDestroy()
        {
            if (_text == null || !ReferenceEquals(_text.textPreprocessor, this)) return;
            _text.textPreprocessor = null;
            Reparse();
        }

        void RestoreNow()
        {
            if (!_modified) return;
            _modified = false;

            TMP_TextInfo info = Text.textInfo;
            if (_sourceDirty || info == null)
            {
                Text.ForceMeshUpdate();
            }
            else
            {
                _source.Restore(info);
                Text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
            }

            for (int i = 0; i < _followers.Count; i++)
            {
                Follower follower = _followers[i];
                if (follower.Text == null) continue;
                if (follower.Dirty || follower.Text.textInfo == null)
                {
                    follower.Text.ForceMeshUpdate();
                    continue;
                }
                follower.Source.Restore(follower.Text.textInfo);
                follower.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
            }
        }

        /// <summary>Called by <see cref="TextChangeDispatcher"/> for this text and its followers only.</summary>
        internal void OnTextChanged(Object obj)
        {
            if (obj == Text)
            {
                _sourceDirty = true;
                _modified = false;
            }
            else
            {
                bool isFollower = false;
                for (int i = 0; i < _followers.Count; i++)
                {
                    if (_followers[i].Text != obj) continue;
                    _followers[i].Dirty = true;
                    isFollower = true;
                }
                if (!isFollower) return;
            }

            // Re-apply in the same frame so new text never flashes without its effects.
            if (!_ticking && IsPlaying) Tick(0f);
        }

        /// <returns>True while this text still needs ticking.</returns>
        internal bool Tick(float deltaTime)
        {
            bool playing = IsPlaying;
            if (!playing && !_modified)
            {
                Sleep();
                return false;
            }

            // TMP is about to regenerate (or is half way, e.g. while a scene opens): its buffers may not
            // match the uploaded mesh. Wait; the rebuild raises TEXT_CHANGED and effects re-apply then.
            if (!isActiveAndEnabled || !Text.isActiveAndEnabled || Text.havePropertiesChanged) return playing;

            TMP_TextInfo info = Text.textInfo;
            if (info == null || info.meshInfo == null || info.characterInfo == null ||
                info.characterInfo.Length < info.characterCount) return playing;

            _ticking = true;
            try
            {
                // Safety net: TMP can rebuild the mesh without raising TEXT_CHANGED (e.g. after a
                // domain reload or a canvas rebuild). A different character count or a reallocated
                // vertex buffer means the cached source is stale.
                if (!_sourceDirty && !_source.Matches(info)) _sourceDirty = true;

                if (_sourceDirty)
                {
                    _source.Capture(info);
                    _sourceDirty = false;
                    _textVersion++;
                }
                _source.Restore(info);

                for (int i = 0; i < _effects.Count; i++)
                {
                    TextEffect effect = _effects[i];
                    if (effect != null && effect.IsPlaying) effect.Step(info, deltaTime, _textVersion);
                }

                Text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
                for (int i = 0; i < _followers.Count; i++) ApplyToFollower(_followers[i], info);
            }
            finally
            {
                _ticking = false;
            }

            // When nothing plays, this tick uploaded the untouched source: the mesh is clean again.
            _modified = playing;
            if (!playing) Sleep();
            else if (!IsAnimating) LeaveUpdateLoop(); // static effects stay applied until the next rebuild
            return playing;
        }

        void ApplyToFollower(Follower follower, TMP_TextInfo info)
        {
            if (follower.Text == null || !follower.Text.isActiveAndEnabled || follower.Text.havePropertiesChanged) return;
            TMP_TextInfo followerInfo = follower.Text.textInfo;
            if (followerInfo == null || followerInfo.meshInfo == null) return;
            if (follower.Dirty || !follower.Source.Matches(followerInfo))
            {
                follower.Source.Capture(followerInfo);
                follower.Dirty = false;
            }

            int meshCount = Mathf.Min(Mathf.Min(followerInfo.meshInfo.Length, info.meshInfo.Length),
                Mathf.Min(follower.Source.Count, _source.Count));
            for (int m = 0; m < meshCount; m++)
            {
                int count = Mathf.Min(follower.Source.VertexCounts[m], _source.VertexCounts[m]);
                Vector3[] dstV = followerInfo.meshInfo[m].vertices;
                Color32[] dstC = followerInfo.meshInfo[m].colors32;
                Vector3[] curV = info.meshInfo[m].vertices;
                Color32[] curC = info.meshInfo[m].colors32;
                Vector3[] srcV = _source.Vertices[m];
                Color32[] srcC = _source.Colors[m];
                Vector3[] fSrcV = follower.Source.Vertices[m];
                Color32[] fSrcC = follower.Source.Colors[m];

                for (int v = 0; v < count; v++)
                {
                    dstV[v] = fSrcV[v] + (curV[v] - srcV[v]);
                    Color32 c = fSrcC[v];
                    if (srcC[v].a > 0) c.a = (byte)(c.a * curC[v].a / srcC[v].a);
                    dstC[v] = c;
                }
            }
            follower.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        // Any '<' may be a package tag (<nofx> works without an effect tag), so reparse whenever present.
        bool HasEffectTags()
        {
            string raw = Text.text;
            return !string.IsNullOrEmpty(raw) && raw.IndexOf('<') >= 0;
        }

        enum TagKind
        {
            None,
            Range,
            Point,
        }

        /// <summary>
        /// Removes package tags and records them in <see cref="Spans"/>; other tags are left for TMP.
        /// Range tags (effect tags, &lt;nofx&gt;) may nest; a closing tag closes the latest open one of
        /// the same name, and unclosed ones run to the end.
        /// </summary>
        string ITextPreprocessor.PreprocessText(string text)
        {
            _spans.Clear();
            _openSpans.Clear();
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0) return text;
            GetComponents(_scratch);

            _builder.Clear();
            bool changed = false;
            int i = 0;
            while (i < text.Length)
            {
                char ch = text[i];
                if (ch == '<')
                {
                    int end = text.IndexOf('>', i + 1);
                    if (end > i + 1)
                    {
                        bool closing = text[i + 1] == '/';
                        int nameStart = closing ? i + 2 : i + 1;
                        int nameEnd = nameStart;
                        while (nameEnd < end && text[nameEnd] != ' ' && text[nameEnd] != '=') nameEnd++;

                        TagKind kind = Classify(text, nameStart, nameEnd - nameStart, out string name);
                        if (kind != TagKind.None)
                        {
                            int position = _builder.Length;
                            string args = nameEnd < end ? text.Substring(nameEnd, end - nameEnd).Trim() : "";
                            if (kind == TagKind.Point)
                                _spans.Add(new TagSpan(closing ? "/" + name : name, args, position, position, true));
                            else if (closing)
                                CloseSpan(name, position);
                            else
                            {
                                _openSpans.Add(_spans.Count);
                                _spans.Add(new TagSpan(name, args, position, -1, false));
                            }
                            changed = true;
                            i = end + 1;
                            continue;
                        }
                    }
                }
                _builder.Append(ch);
                i++;
            }

            for (int s = 0; s < _openSpans.Count; s++)
                _spans[_openSpans[s]] = _spans[_openSpans[s]].WithEnd(_builder.Length);
            _openSpans.Clear();
            return changed ? _builder.ToString() : text;
        }

        void CloseSpan(string name, int position)
        {
            for (int s = _openSpans.Count - 1; s >= 0; s--)
            {
                int index = _openSpans[s];
                if (!string.Equals(_spans[index].Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                _spans[index] = _spans[index].WithEnd(position);
                _openSpans.RemoveAt(s);
                return;
            }
        }

        TagKind Classify(string text, int start, int length, out string name)
        {
            name = null;
            if (length <= 0) return TagKind.None;

            if (length == NoFxTag.Length &&
                string.Compare(text, start, NoFxTag, 0, length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                name = NoFxTag;
                return TagKind.Range;
            }

            for (int i = 0; i < _scratch.Count; i++)
            {
                string tag = _scratch[i].TagName;
                if (string.IsNullOrEmpty(tag) || tag.Length != length) continue;
                if (string.Compare(text, start, tag, 0, length, StringComparison.OrdinalIgnoreCase) != 0) continue;
                name = tag;
                return TagKind.Range;
            }

            for (int i = 0; i < _scratch.Count; i++)
            {
                if (!_scratch[i].ClaimsPointTag(text, start, length)) continue;
                name = text.Substring(start, length).ToLowerInvariant();
                return TagKind.Point;
            }
            return TagKind.None;
        }

        sealed class Follower
        {
            public TMP_Text Text;
            public readonly MeshCache Source = new MeshCache();
            public bool Dirty = true;
        }

        /// <summary>Copy of the used part of each TMP sub-mesh.</summary>
        sealed class MeshCache
        {
            public Vector3[][] Vertices = Array.Empty<Vector3[]>();
            public Color32[][] Colors = Array.Empty<Color32[]>();
            public int[] VertexCounts = Array.Empty<int>();
            Vector3[] _buffer0;
            int _characterCount = -1;

            public int Count => VertexCounts.Length;

            /// <summary>True if <paramref name="info"/> is still the mesh this cache was captured from.</summary>
            public bool Matches(TMP_TextInfo info)
            {
                if (info.characterCount != _characterCount || info.meshInfo.Length != VertexCounts.Length) return false;
                return info.meshInfo.Length == 0 || ReferenceEquals(info.meshInfo[0].vertices, _buffer0);
            }

            public void Capture(TMP_TextInfo info)
            {
                _characterCount = info.characterCount;
                _buffer0 = info.meshInfo.Length > 0 ? info.meshInfo[0].vertices : null;
                int meshCount = info.meshInfo.Length;
                if (VertexCounts.Length != meshCount)
                {
                    Array.Resize(ref Vertices, meshCount);
                    Array.Resize(ref Colors, meshCount);
                    Array.Resize(ref VertexCounts, meshCount);
                }

                for (int m = 0; m < meshCount; m++)
                {
                    TMP_MeshInfo mesh = info.meshInfo[m];
                    int count = mesh.vertices == null || mesh.colors32 == null
                        ? 0
                        : Mathf.Min(mesh.vertexCount, Mathf.Min(mesh.vertices.Length, mesh.colors32.Length));
                    VertexCounts[m] = count;
                    if (count == 0) continue;
                    Ensure(ref Vertices[m], count);
                    Ensure(ref Colors[m], count);
                    Array.Copy(mesh.vertices, Vertices[m], count);
                    Array.Copy(mesh.colors32, Colors[m], count);
                }
            }

            public void Restore(TMP_TextInfo info)
            {
                int meshCount = Mathf.Min(info.meshInfo.Length, VertexCounts.Length);
                for (int m = 0; m < meshCount; m++)
                {
                    int count = VertexCounts[m];
                    if (count == 0) continue;
                    Vector3[] vertices = info.meshInfo[m].vertices;
                    Color32[] colors = info.meshInfo[m].colors32;
                    if (vertices == null || colors == null) continue;
                    count = Mathf.Min(count, Mathf.Min(vertices.Length, colors.Length));
                    Array.Copy(Vertices[m], vertices, count);
                    Array.Copy(Colors[m], colors, count);
                }
            }

            static void Ensure<T>(ref T[] array, int size)
            {
                if (array == null || array.Length < size) array = new T[Mathf.NextPowerOfTwo(size)];
            }
        }
    }
}
