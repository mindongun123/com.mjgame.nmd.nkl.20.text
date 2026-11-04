using TMPro;
using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Colors the text with a multi-key <see cref="Gradient"/> (up to 8 colors + alpha), along a
    /// direction, per vertex (smooth) or per character (blocky), optionally scrolling.
    /// Without scrolling it is static: applied once, then free until the text changes, and visible in edit mode.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Effects/Gradient")]
    public sealed class GradientEffect : TextEffect
    {
        public enum Direction
        {
            /// <summary>Left to right.</summary>
            Horizontal,
            /// <summary>Top to bottom.</summary>
            Vertical,
            /// <summary>Along <see cref="angle"/> (0° = left to right, 90° = bottom to top).</summary>
            Angle,
            /// <summary>From the center outwards.</summary>
            Radial,
            /// <summary>By character order (first to last), independent of layout.</summary>
            Characters,
        }

        public enum Coloring
        {
            /// <summary>Each vertex gets its own color: smooth transitions inside a character.</summary>
            Smooth,
            /// <summary>One color per character, sampled at its center.</summary>
            PerCharacter,
        }

        public enum Wrap
        {
            Repeat,
            PingPong,
            Clamp,
        }

        public enum Blend
        {
            /// <summary>Gradient color replaces the text color.</summary>
            Replace,
            /// <summary>Gradient color multiplies the text color (tints existing colors/gradients).</summary>
            Multiply,
            /// <summary>Gradient color is added (glow/highlight).</summary>
            Add,
        }

        [Tooltip("Shared gradient asset. Leave empty to use the local gradient below.")]
        public TextGradient preset;
        public Gradient gradient = CreateRainbow();

        [Header("Layout")]
        public Direction direction = Direction.Horizontal;
        [Range(0f, 360f)] public float angle = 45f;
        public Coloring coloring = Coloring.Smooth;
        [Tooltip("How many times the gradient fits across the text.")]
        [Min(0.01f)] public float repeat = 1f;
        [Tooltip("Shifts the gradient (0..1).")]
        public float offset;
        public Wrap wrap = Wrap.Repeat;

        [Header("Animation")]
        [Tooltip("Gradient lengths per second; negative scrolls the other way. 0 = static (no per-frame cost).")]
        public float scrollSpeed;

        [Header("Blending")]
        public Blend blend = Blend.Replace;
        [Range(0f, 1f)] public float amount = 1f;
        [Tooltip("Multiply the text alpha by the gradient alpha.")]
        public bool useGradientAlpha;

        /// <summary>Samples of the gradient; reading it is much cheaper than Gradient.Evaluate per vertex.</summary>
        const int LutSize = 128;
        readonly Color[] _lut = new Color[LutSize];
        Gradient _lutSource;
        bool _lutDirty = true;
        bool _lutFixed;

        public Gradient ActiveGradient => preset != null && preset.gradient != null ? preset.gradient : gradient;

        /// <summary>
        /// Call after changing the gradient's keys from code (assigning a new gradient or preset is detected
        /// automatically). Inspector edits refresh it on their own.
        /// </summary>
        public void RefreshGradient()
        {
            _lutDirty = true;
            if (IsPlaying) Fx.Wake();
        }

        protected override void OnPlay() => _lutDirty = true;
        protected override void OnTextRebuilt() => _lutDirty = true;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            _lutDirty = true;
            base.OnValidate();
        }
#endif

        void EnsureLut(Gradient g)
        {
            if (!_lutDirty && ReferenceEquals(g, _lutSource)) return;
            for (int i = 0; i < LutSize; i++) _lut[i] = g.Evaluate(i / (LutSize - 1f));
            _lutSource = g;
            _lutFixed = g.mode == GradientMode.Fixed; // hard steps: sample, do not blend
            _lutDirty = false;
        }

        public override bool IsLooping => scrollSpeed != 0f;
        public override bool IsAnimated => scrollSpeed != 0f;

        protected override void OnEnable()
        {
            base.OnEnable();
#if UNITY_EDITOR
            TextGradient.Changed += OnPresetChanged;
#endif
        }

        protected override void OnDisable()
        {
#if UNITY_EDITOR
            TextGradient.Changed -= OnPresetChanged;
#endif
            base.OnDisable();
        }

#if UNITY_EDITOR
        void OnPresetChanged(TextGradient changed)
        {
            if (changed == preset) RefreshGradient();
        }
#endif

        /// <summary>Red → yellow → green → cyan → blue → magenta → red.</summary>
        public static Gradient CreateRainbow()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.25f, 0.25f), 0f),
                    new GradientColorKey(new Color(1f, 0.9f, 0.2f), 1f / 6f),
                    new GradientColorKey(new Color(0.3f, 1f, 0.35f), 2f / 6f),
                    new GradientColorKey(new Color(0.25f, 0.95f, 1f), 3f / 6f),
                    new GradientColorKey(new Color(0.3f, 0.45f, 1f), 4f / 6f),
                    new GradientColorKey(new Color(1f, 0.35f, 1f), 5f / 6f),
                    new GradientColorKey(new Color(1f, 0.25f, 0.25f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        protected override bool Apply(TMP_TextInfo info)
        {
            Gradient g = ActiveGradient;
            if (g == null || AffectedCount == 0) return true;
            EnsureLut(g);

            Vector2 axis = Axis();
            GetBounds(info, axis, out float min, out float max, out Vector2 center, out float radius);
            float span = max - min;
            float shift = offset + Time * scrollSpeed;

            int n = 0;
            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                for (int c = range.x; c < range.y; c++, n++)
                {
                    if (!IsDrawable(info, c)) continue;
                    TMP_CharacterInfo ch = info.characterInfo[c];
                    Vector3[] v = info.meshInfo[ch.materialReferenceIndex].vertices;
                    Color32[] colors = info.meshInfo[ch.materialReferenceIndex].colors32;
                    int i = ch.vertexIndex;

                    if (direction == Direction.Characters)
                    {
                        float t = AffectedCount > 1 ? (float)n / (AffectedCount - 1) : 0f;
                        Color color = Sample(t, shift);
                        for (int k = 0; k < 4; k++) colors[i + k] = Combine(colors[i + k], color);
                        continue;
                    }

                    if (coloring == Coloring.PerCharacter)
                    {
                        Vector2 mid = (v[i] + v[i + 2]) * 0.5f;
                        Color color = Sample(Position(mid, axis, min, span, center, radius), shift);
                        for (int k = 0; k < 4; k++) colors[i + k] = Combine(colors[i + k], color);
                        continue;
                    }

                    for (int k = 0; k < 4; k++)
                    {
                        Color color = Sample(Position(v[i + k], axis, min, span, center, radius), shift);
                        colors[i + k] = Combine(colors[i + k], color);
                    }
                }
            }
            return true;
        }

        Vector2 Axis()
        {
            switch (direction)
            {
                case Direction.Vertical: return Vector2.down; // top → bottom, like TMP's top/bottom gradient
                case Direction.Angle:
                    float rad = angle * Mathf.Deg2Rad;
                    return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                default: return Vector2.right;
            }
        }

        /// <summary>Extent of the affected visible characters along the axis (or around the center for radial).</summary>
        void GetBounds(TMP_TextInfo info, Vector2 axis, out float min, out float max, out Vector2 center, out float radius)
        {
            min = float.MaxValue;
            max = float.MinValue;
            Vector2 lo = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 hi = new Vector2(float.MinValue, float.MinValue);

            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                for (int c = range.x; c < range.y; c++)
                {
                    if (!IsDrawable(info, c)) continue;
                    TMP_CharacterInfo ch = info.characterInfo[c];
                    Vector3[] v = info.meshInfo[ch.materialReferenceIndex].vertices;
                    for (int k = 0; k < 4; k++)
                    {
                        Vector2 p = v[ch.vertexIndex + k];
                        float d = Vector2.Dot(p, axis);
                        if (d < min) min = d;
                        if (d > max) max = d;
                        lo = Vector2.Min(lo, p);
                        hi = Vector2.Max(hi, p);
                    }
                }
            }

            if (min > max)
            {
                min = max = 0f;
                lo = hi = Vector2.zero;
            }
            center = (lo + hi) * 0.5f;
            radius = Mathf.Max(0.0001f, (hi - lo).magnitude * 0.5f);
        }

        float Position(Vector2 p, Vector2 axis, float min, float span, Vector2 center, float radius)
        {
            if (direction == Direction.Radial) return (p - center).magnitude / radius;
            return span > 0.0001f ? (Vector2.Dot(p, axis) - min) / span : 0f;
        }

        Color Sample(float t, float shift)
        {
            t = t * repeat + shift;
            switch (wrap)
            {
                case Wrap.PingPong: t = Mathf.PingPong(t, 1f); break;
                case Wrap.Clamp: t = Mathf.Clamp01(t); break;
                default: t = Mathf.Repeat(t, 1f); break;
            }
            float x = t * (LutSize - 1);
            if (_lutFixed) return _lut[Mathf.Clamp(Mathf.RoundToInt(x), 0, LutSize - 1)];
            int i = Mathf.Min((int)x, LutSize - 2);
            return Color.LerpUnclamped(_lut[i], _lut[i + 1], x - i);
        }

        Color32 Combine(Color32 source, Color color)
        {
            Color src = source;
            Color mixed;
            switch (blend)
            {
                case Blend.Multiply: mixed = src * color; break;
                case Blend.Add: mixed = src + color; break;
                default: mixed = color; break;
            }

            Color result = Color.Lerp(src, mixed, amount);
            result.a = useGradientAlpha ? src.a * Mathf.Lerp(1f, color.a, amount) : src.a;
            return result;
        }
    }
}
