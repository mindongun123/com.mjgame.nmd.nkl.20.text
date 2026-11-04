using TMPro;
using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Characters cycle through the hue wheel (HSV, per character). For custom colors, direction,
    /// smooth per-vertex blending or presets use <see cref="GradientEffect"/>.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Effects/Rainbow")]
    public sealed class RainbowEffect : TextEffect
    {
        public override TMP_VertexDataUpdateFlags Modifies => TMP_VertexDataUpdateFlags.Colors32;
        [Tooltip("Hue cycles per second.")]
        public float speed = 0.5f;
        [Tooltip("Hue difference between neighbouring characters (0..1).")]
        public float spacing = 0.08f;
        [Range(0f, 1f)] public float saturation = 0.6f;
        [Range(0f, 1f)] public float brightness = 1f;
        [Tooltip("0 = keep the text's own colors/gradient, 1 = full rainbow.")]
        [Range(0f, 1f)] public float mix = 1f;

        public override bool IsLooping => speed != 0f;
        public override bool IsAnimated => speed != 0f;

        protected override bool Apply(TMP_TextInfo info)
        {
            int n = 0;
            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                // <rainbow=1> or <rainbow s=1 d=0.1 mix=0.5>
                TagArgs args = GetRangeArgs(r);
                float spd = args.GetFloat("s", "", speed);
                float dens = args.GetFloat("d", spacing);
                float amount = args.GetFloat("mix", mix);
                for (int c = range.x; c < range.y; c++, n++)
                {
                    if (!IsDrawable(info, c)) continue;
                    float hue = Mathf.Repeat(Time * spd + n * dens, 1f);
                    TintColor(info, c, Color.HSVToRGB(hue, saturation, brightness), amount);
                }
            }
            return true;
        }
    }
}
