using TMPro;
using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Base of static layouts that place the characters along a path (arc, curve...).
    /// The layout is computed once per text rebuild and cached: with no animated effect the text costs
    /// nothing per frame. It runs before other effects (<see cref="Order"/>), so Wave, Shake... move the
    /// characters along the bent text.
    /// <para>
    /// Each character is placed by the middle of its baseline: its horizontal position in the text is
    /// the distance along the path, and it is rotated around that point to follow the path direction.
    /// Lower lines are pushed along the path normal.
    /// </para>
    /// </summary>
    public abstract class TextLayoutEffect : TextEffect
    {
        public enum SizeAlign
        {
            /// <summary>Characters grow upwards from the baseline.</summary>
            Bottom,
            /// <summary>Characters grow from their middle (half of the line height).</summary>
            Middle,
            /// <summary>Characters grow downwards from the top of the line.</summary>
            Top,
        }

        public enum Anchor
        {
            /// <summary>Text starts at the path anchor.</summary>
            Start,
            /// <summary>Text is centered on the path anchor.</summary>
            Center,
            /// <summary>Text ends at the path anchor.</summary>
            End,
        }

        [Tooltip("Which part of the text sits on the anchor of the path (e.g. the top of an arc).")]
        [SerializeField] Anchor _anchor = Anchor.Center;
        [Tooltip("Rotate each character to follow the path. Off = characters stay upright.")]
        [SerializeField] bool _rotateCharacters = true;

        [Tooltip("Scale characters by their position in the text (x 0..1 = left..right, y = scale), e.g. bigger in the middle.")]
        [SerializeField] bool _useSizeCurve;
        [SerializeField] AnimationCurve _sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0f), new Keyframe(0.5f, 1.4f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));
        [Tooltip("Which part of the characters lines up when they are scaled: Bottom = feet on the baseline, Middle = centers on one line, Top = tops on one line.")]
        [SerializeField] SizeAlign _sizeAlign = SizeAlign.Bottom;
        [Tooltip("Spread the characters by their new size so bigger ones do not overlap (and smaller ones close up).")]
        [SerializeField] bool _keepSpacing = true;

        // Per character: pivot before (middle of the baseline), pivot after, rotation, scale.
        // Not serialized: after a script reload they are empty and the layout is rebuilt on the next Apply.
        [System.NonSerialized] bool _built;
        [System.NonSerialized] Vector2[] _from = new Vector2[0];
        [System.NonSerialized] Vector2[] _to = new Vector2[0];
        [System.NonSerialized] Vector2[] _rotation = new Vector2[0]; // (cos, sin)
        [System.NonSerialized] float[] _scale = new float[0];
        [System.NonSerialized] float[] _x = new float[0];            // pivot x along the text after spacing
        [System.NonSerialized] bool[] _placed = new bool[0];

        public override TMP_VertexDataUpdateFlags Modifies => TMP_VertexDataUpdateFlags.Vertices;
        public override bool IsAnimated => false;
        public override int Order => -100;
        public override string DefaultTag => ""; // layouts apply to the whole text

        public Anchor TextAnchor
        {
            get => _anchor;
            set { _anchor = value; Invalidate(); }
        }

        public bool RotateCharacters
        {
            get => _rotateCharacters;
            set { _rotateCharacters = value; Invalidate(); }
        }

        /// <summary>Scale per character from <see cref="SizeCurve"/> (x 0..1 across the text).</summary>
        public bool UseSizeCurve
        {
            get => _useSizeCurve;
            set { _useSizeCurve = value; Invalidate(); }
        }

        public AnimationCurve SizeCurve
        {
            get => _sizeCurve;
            set { _sizeCurve = value; Invalidate(); }
        }

        /// <summary>Which part of the characters lines up when scaled by the size curve.</summary>
        public SizeAlign SizeAlignment
        {
            get => _sizeAlign;
            set { _sizeAlign = value; Invalidate(); }
        }

        /// <summary>Spread characters by their scaled width so they do not overlap.</summary>
        public bool KeepSpacing
        {
            get => _keepSpacing;
            set { _keepSpacing = value; Invalidate(); }
        }

        /// <summary>Recomputes the layout. Call it after changing fields of a subclass from code.</summary>
        public void Refresh() => Invalidate();

        /// <summary>Horizontal extent and first baseline of the affected characters, for subclasses.</summary>
        protected struct TextFrame
        {
            public float MinX;
            public float MaxX;
            public float Baseline;
            /// <summary>X of the text point that sits on the path anchor (see <see cref="Anchor"/>).</summary>
            public float AnchorX;
            public float Width => MaxX - MinX;
        }

        /// <summary>Prepare the path for this text (e.g. radius from the text width).</summary>
        protected virtual void BeginLayout(in TextFrame frame) { }

        /// <summary>
        /// Point and unit direction of the path at <paramref name="distance"/> from the path anchor
        /// (negative = before it), in the text's local space.
        /// </summary>
        protected abstract void Evaluate(float distance, out Vector2 point, out Vector2 direction);

        /// <summary>
        /// Same for a line of text <paramref name="offset"/> above the first line (negative = below).
        /// Default: the path point pushed along its normal. Override it when a parallel path has a
        /// different length (inner lines of an arc), so characters keep their spacing.
        /// </summary>
        protected virtual void EvaluateLine(float distance, float offset, out Vector2 point, out Vector2 direction)
        {
            Evaluate(distance, out point, out direction);
            if (offset == 0f) return;
            Vector2 dir = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector2.right;
            point += new Vector2(-dir.y, dir.x) * offset;
        }

        protected override void OnTextRebuilt() => Build(Fx.Text.textInfo);

        void Build(TMP_TextInfo info)
        {
            int count = Mathf.Min(info.characterCount, info.characterInfo.Length);
            if (_from.Length < count)
            {
                int size = Mathf.NextPowerOfTwo(count);
                _from = new Vector2[size];
                _to = new Vector2[size];
                _rotation = new Vector2[size];
                _scale = new float[size];
                _x = new float[size];
                _placed = new bool[size];
            }
            System.Array.Clear(_placed, 0, _placed.Length);
            _built = true;

            // 1. Which characters, their original frame (pen positions, so spaces count too).
            float minX = float.MaxValue, maxX = float.MinValue, baseline = 0f;
            bool any = false;
            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                for (int c = range.x; c < range.y && c < count; c++)
                {
                    ref readonly TMP_CharacterInfo ch = ref info.characterInfo[c];
                    if (!any) baseline = ch.baseLine;
                    any = true;
                    minX = Mathf.Min(minX, ch.origin);
                    maxX = Mathf.Max(maxX, ch.xAdvance);
                    _placed[c] = true;
                }
            }
            if (!any) return;

            // 2. Scale from the size curve, by original position.
            bool scaled = _useSizeCurve && _sizeCurve != null && _sizeCurve.length > 0;
            float width = Mathf.Max(1e-4f, maxX - minX);
            for (int c = 0; c < count; c++)
            {
                if (!_placed[c]) continue;
                ref readonly TMP_CharacterInfo ch = ref info.characterInfo[c];
                float mid = (ch.origin + ch.xAdvance) * 0.5f;
                _scale[c] = scaled ? Mathf.Max(0f, _sizeCurve.Evaluate((mid - minX) / width)) : 1f;
                _x[c] = mid;
            }

            // 3. Spread by scaled advance, line by line, keeping each line centered where it was.
            if (scaled && _keepSpacing) Respace(info, count);

            // 4. Frame after spacing, then the path.
            var frame = new TextFrame { MinX = float.MaxValue, MaxX = float.MinValue, Baseline = baseline };
            for (int c = 0; c < count; c++)
            {
                if (!_placed[c]) continue;
                ref readonly TMP_CharacterInfo ch = ref info.characterInfo[c];
                float half = (ch.xAdvance - ch.origin) * 0.5f * (_keepSpacing ? _scale[c] : 1f);
                frame.MinX = Mathf.Min(frame.MinX, _x[c] - half);
                frame.MaxX = Mathf.Max(frame.MaxX, _x[c] + half);
            }
            frame.AnchorX = _anchor == Anchor.Start ? frame.MinX : _anchor == Anchor.End ? frame.MaxX : (frame.MinX + frame.MaxX) * 0.5f;
            BeginLayout(frame);

            for (int c = 0; c < count; c++)
            {
                if (!_placed[c]) continue;
                ref readonly TMP_CharacterInfo ch = ref info.characterInfo[c];
                EvaluateLine(_x[c] - frame.AnchorX, ch.baseLine - frame.Baseline, out Vector2 point, out Vector2 dir);
                if (dir.sqrMagnitude < 1e-8f) dir = Vector2.right;
                dir.Normalize();
                // Scale/rotation pivot: on the baseline, or raised to the middle/top of the line. Moving the
                // pivot along the path normal keeps unscaled characters exactly where they were.
                float lift = _sizeAlign == SizeAlign.Bottom ? 0f
                    : _sizeAlign == SizeAlign.Middle ? (ch.ascender - ch.baseLine) * 0.5f
                    : ch.ascender - ch.baseLine;
                _from[c] = new Vector2((ch.origin + ch.xAdvance) * 0.5f, ch.baseLine + lift);
                _to[c] = point + new Vector2(-dir.y, dir.x) * lift;
                _rotation[c] = _rotateCharacters ? dir : Vector2.right;
            }
        }

        void Respace(TMP_TextInfo info, int count)
        {
            int c = 0;
            while (c < count)
            {
                if (!_placed[c]) { c++; continue; }
                int line = info.characterInfo[c].lineNumber;
                int first = c;
                float start = info.characterInfo[c].origin, end = start, cursor = start;
                for (; c < count && (!_placed[c] || info.characterInfo[c].lineNumber == line); c++)
                {
                    if (!_placed[c]) continue;
                    ref readonly TMP_CharacterInfo ch = ref info.characterInfo[c];
                    float advance = (ch.xAdvance - ch.origin) * _scale[c];
                    _x[c] = cursor + advance * 0.5f;
                    cursor += advance;
                    end = Mathf.Max(end, ch.xAdvance);
                }
                float shift = ((start + end) - (start + cursor)) * 0.5f; // keep the line center
                for (int k = first; k < c; k++)
                    if (_placed[k] && info.characterInfo[k].lineNumber == line) _x[k] += shift;
            }
        }

        protected override bool Apply(TMP_TextInfo info)
        {
            if (!_built || _placed.Length < info.characterCount) Build(info);
            int count = Mathf.Min(info.characterCount, _placed.Length);
            for (int c = 0; c < count; c++)
            {
                if (!_placed[c] || !IsDrawable(info, c)) continue;
                ref readonly TMP_CharacterInfo ch = ref info.characterInfo[c];
                Vector3[] v = info.meshInfo[ch.materialReferenceIndex].vertices;
                Vector2 from = _from[c], to = _to[c];
                float s = _scale[c];
                float cos = _rotation[c].x * s, sin = _rotation[c].y * s; // rotation and scale in one step
                for (int k = 0; k < 4; k++)
                {
                    int i = ch.vertexIndex + k;
                    float x = v[i].x - from.x, y = v[i].y - from.y;
                    v[i] = new Vector3(to.x + x * cos - y * sin, to.y + x * sin + y * cos, v[i].z);
                }
            }
            return true;
        }
    }
}