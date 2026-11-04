using TMPro;
using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Characters pop in one after another with an overshoot. Plays once, then costs nothing.
    /// Tag args: d (char delay), t (char duration), h (or =, rise), o (overshoot).
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Effects/Bounce In")]
    public sealed class BounceInEffect : TextEffect
    {
        public override string DefaultTag => "bounce";

        [Tooltip("Delay between two characters, in seconds.")]
        public float charDelay = 0.04f;
        [Tooltip("Animation length of one character, in seconds.")]
        public float charDuration = 0.35f;
        [Tooltip("Strength of the overshoot (1.7 = classic ease-out-back).")]
        public float overshoot = 1.7f;
        [Tooltip("Characters start this far below their final position, in text units.")]
        public float rise = 20f;
        [Tooltip("Spaces and other invisible characters do not add a delay.")]
        public bool skipSpaces = true;

        protected override bool Apply(TMP_TextInfo info)
        {
            float end = 0f;
            int n = 0;
            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                // <bounce=30> or <bounce d=0.08 t=0.5 h=30 o=3>
                TagArgs args = GetRangeArgs(r);
                float delay = args.GetFloat("d", charDelay);
                float length = args.GetFloat("t", charDuration);
                float height = args.GetFloat("h", "", rise) * UnitScale;
                float back = args.GetFloat("o", overshoot);
                for (int c = range.x; c < range.y; c++)
                {
                    bool counts = !skipSpaces || info.characterInfo[c].isVisible;
                    float start = n * delay;
                    if (counts) n++;
                    end = Mathf.Max(end, start + length);
                    if (!IsDrawable(info, c)) continue;
                    float t = length > 0f ? Mathf.Clamp01((Time - start) / length) : 1f;
                    Scale(info, c, EaseOutBack(t, back));
                    Offset(info, c, new Vector3(0f, (t - 1f) * height, 0f));
                    MultiplyAlpha(info, c, Mathf.Clamp01(t * 3f));
                }
            }
            return Time < end;
        }

        static float EaseOutBack(float t, float overshoot)
        {
            float u = t - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }
    }
}
