using TMPro;
using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// A band of light sweeps across the text (a "Claim" button shine). Vertex colors only: no material
    /// is created and the text keeps batching. Loops with a pause between sweeps, or plays once.
    /// Tag args: c (or =, color), w (width), t (sweep seconds), i (pause), a (intensity).
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Effects/Shine")]
    public sealed class ShineEffect : TextEffect
    {
        public override TMP_VertexDataUpdateFlags Modifies => TMP_VertexDataUpdateFlags.Colors32;

        [Tooltip("Color of the light band.")]
        public Color color = Color.white;
        [Tooltip("How much the band replaces the text color at its center.")]
        [Range(0f, 1f)] public float intensity = 0.85f;
        [Tooltip("Width of the band, relative to the text width.")]
        [Min(0.01f)] public float width = 0.3f;
        [Tooltip("Seconds for one sweep from left to right.")]
        [Min(0.01f)] public float duration = 0.6f;
        [Tooltip("Pause between two sweeps, in seconds.")]
        [Min(0f)] public float interval = 1.5f;
        [Tooltip("Slant of the band: horizontal shift per unit of height (0 = vertical band).")]
        public float slant = 0.35f;
        [Tooltip("Off = one sweep, then the effect ends.")]
        public bool loop = true;

        public override bool IsLooping => loop;

        protected override bool Apply(TMP_TextInfo info)
        {
            float longest = 0f;
            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                // <shine=#ffd700> or <shine c=yellow w=0.5 t=1 i=2 a=1>
                TagArgs args = GetRangeArgs(r);
                Color32 band = args.GetColor("c", "", color);
                float amount = args.GetFloat("a", intensity);
                float rangeDuration = args.GetFloat("t", duration);
                float rangeCycle = rangeDuration + args.GetFloat("i", interval);
                longest = Mathf.Max(longest, rangeDuration);
                // Between two sweeps the text keeps its own colors.
                float rt = loop ? Mathf.Repeat(Time, rangeCycle) : Time;
                if (rt >= rangeDuration) continue;

                // Band position along "x + slant * y", measured over this range only.
                float min = float.MaxValue, max = float.MinValue;
                for (int c = range.x; c < range.y; c++)
                {
                    if (!IsDrawable(info, c)) continue;
                    ref readonly TMP_CharacterInfo ch = ref info.characterInfo[c];
                    Vector3[] v = info.meshInfo[ch.materialReferenceIndex].vertices;
                    for (int k = 0; k < 4; k++)
                    {
                        float p = v[ch.vertexIndex + k].x + slant * v[ch.vertexIndex + k].y;
                        if (p < min) min = p;
                        if (p > max) max = p;
                    }
                }
                if (min > max) continue;

                float half = Mathf.Max(1e-4f, args.GetFloat("w", width) * (max - min) * 0.5f);
                float center = Mathf.Lerp(min - half, max + half, rt / rangeDuration);
                for (int c = range.x; c < range.y; c++)
                {
                    if (!IsDrawable(info, c)) continue;
                    ref readonly TMP_CharacterInfo ch = ref info.characterInfo[c];
                    Vector3[] v = info.meshInfo[ch.materialReferenceIndex].vertices;
                    Color32[] colors = info.meshInfo[ch.materialReferenceIndex].colors32;
                    for (int k = 0; k < 4; k++)
                    {
                        int i = ch.vertexIndex + k;
                        float d = Mathf.Abs(v[i].x + slant * v[i].y - center) / half;
                        if (d >= 1f) continue;
                        float s = 1f - d;
                        s = s * s * (3f - 2f * s) * amount; // smooth edges
                        Color32 src = colors[i];
                        Color32 lit = Color32.Lerp(src, band, s);
                        lit.a = src.a;
                        colors[i] = lit;
                    }
                }
            }
            return loop || Time < longest;
        }
    }
}
