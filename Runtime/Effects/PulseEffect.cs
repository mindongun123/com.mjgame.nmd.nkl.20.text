using TMPro;
using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>Characters grow and shrink rhythmically. Loops forever. Tag args: a (or =), s, d.</summary>
    [ExecuteAlways]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Effects/Pulse")]
    public sealed class PulseEffect : TextEffect
    {
        [Tooltip("Extra scale at the peak (0.15 = 115%).")]
        public float amount = 0.15f;
        [Tooltip("Radians per second.")]
        public float speed = 5f;
        [Tooltip("Phase difference between neighbouring characters; 0 = whole text pulses together.")]
        public float spacing = 0.3f;

        public override bool IsLooping => true;

        protected override bool Apply(TMP_TextInfo info)
        {
            int n = 0;
            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                // <pulse=0.3> or <pulse a=0.3 s=8 d=0>
                TagArgs args = GetRangeArgs(r);
                float amt = args.GetFloat("a", "", amount);
                float spd = args.GetFloat("s", speed);
                float dens = args.GetFloat("d", spacing);
                for (int c = range.x; c < range.y; c++, n++)
                {
                    if (!IsDrawable(info, c)) continue;
                    float wave = Mathf.Sin(Time * spd - n * dens) * 0.5f + 0.5f;
                    Scale(info, c, 1f + wave * amt);
                }
            }
            return true;
        }
    }
}
