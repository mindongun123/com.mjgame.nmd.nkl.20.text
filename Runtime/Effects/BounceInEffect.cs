using TMPro;
using UnityEngine;

namespace com.nmd.nkl.text
{
    /// <summary>Characters pop in one after another with an overshoot. Plays once, then costs nothing.</summary>
    [AddComponentMenu("com.nmd.nkl/Text/Effects/Bounce In")]
    public sealed class BounceInEffect : TextEffect
    {
        [Tooltip("Delay between two characters, in seconds.")]
        public float charDelay = 0.04f;
        [Tooltip("Animation length of one character, in seconds.")]
        public float charDuration = 0.35f;
        [Tooltip("Strength of the overshoot (1.7 = classic ease-out-back).")]
        public float overshoot = 1.7f;
        [Tooltip("Characters start this far below their final position, in text units.")]
        public float rise = 20f;

        protected override bool Apply(TMP_TextInfo info)
        {
            int n = 0;
            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                for (int c = range.x; c < range.y; c++, n++)
                {
                    if (!IsDrawable(info, c)) continue;
                    float t = charDuration > 0f ? Mathf.Clamp01((Time - n * charDelay) / charDuration) : 1f;
                    Scale(info, c, EaseOutBack(t));
                    Offset(info, c, new Vector3(0f, (t - 1f) * rise, 0f));
                    MultiplyAlpha(info, c, Mathf.Clamp01(t * 3f));
                }
            }
            return Time < (AffectedCount - 1) * charDelay + charDuration;
        }

        float EaseOutBack(float t)
        {
            float u = t - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }
    }
}
