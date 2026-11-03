using TMPro;
using UnityEngine;

namespace com.nmd.nkl.text
{
    /// <summary>Characters float up and down in a sine wave. Loops forever. Tag args: a (or =), s, d.</summary>
    [AddComponentMenu("com.nmd.nkl/Text/Effects/Wave")]
    public sealed class WaveEffect : TextEffect
    {
        [Tooltip("Height of the wave, in text units.")]
        public float amplitude = 6f;
        [Tooltip("Radians per second.")]
        public float speed = 6f;
        [Tooltip("Phase difference between neighbouring characters.")]
        public float spacing = 0.5f;

        public override bool IsLooping => true;

        protected override bool Apply(TMP_TextInfo info)
        {
            int n = 0;
            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                // <wave=10> or <wave a=10 s=2 d=0.3>
                TagArgs args = GetRangeArgs(r);
                float amp = args.GetFloat("a", "", amplitude);
                float spd = args.GetFloat("s", speed);
                float dens = args.GetFloat("d", spacing);
                for (int c = range.x; c < range.y; c++, n++)
                {
                    if (!IsDrawable(info, c)) continue;
                    float y = Mathf.Sin(Time * spd + n * dens) * amp;
                    Offset(info, c, new Vector3(0f, y, 0f));
                }
            }
            return true;
        }
    }
}
