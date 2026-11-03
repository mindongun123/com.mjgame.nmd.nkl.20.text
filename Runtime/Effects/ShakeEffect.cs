using TMPro;
using UnityEngine;

namespace com.nmd.nkl.text
{
    /// <summary>Characters jitter randomly. Loops, or stops after <see cref="duration"/>. Tag args: a (or =), r.</summary>
    [AddComponentMenu("com.nmd.nkl/Text/Effects/Shake")]
    public sealed class ShakeEffect : TextEffect
    {
        [Tooltip("Max offset, in text units.")]
        public float amplitude = 3f;
        [Tooltip("New random positions per second.")]
        public float rate = 20f;
        [Tooltip("Seconds; 0 = loop forever.")]
        public float duration;

        public override bool IsLooping => duration <= 0f;

        protected override bool Apply(TMP_TextInfo info)
        {
            if (duration > 0f && Time >= duration) return false;

            int n = 0;
            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                // <shake=5> or <shake a=5 r=30>
                TagArgs args = GetRangeArgs(r);
                float amp = args.GetFloat("a", "", amplitude);
                int step = (int)(Time * args.GetFloat("r", rate));
                for (int c = range.x; c < range.y; c++, n++)
                {
                    if (!IsDrawable(info, c)) continue;
                    uint h = Hash(n, step);
                    float x = ((h & 0xFFFF) / 65535f * 2f - 1f) * amp;
                    float y = ((h >> 16) / 65535f * 2f - 1f) * amp;
                    Offset(info, c, new Vector3(x, y, 0f));
                }
            }
            return true;
        }

        static uint Hash(int a, int b)
        {
            uint h = (uint)a * 374761393u + (uint)b * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }
}
