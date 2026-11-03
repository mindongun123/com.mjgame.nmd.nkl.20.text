using TMPro;
using UnityEngine;

namespace com.nmd.nkl.text
{
    /// <summary>Characters fade in one after another, optionally sliding in. Plays once.</summary>
    [ExecuteAlways]
    [AddComponentMenu("com.nmd.nkl/Text/Effects/Fade In")]
    public sealed class FadeInEffect : TextEffect
    {
        public override string DefaultTag => "fade";

        [Tooltip("Delay between two characters, in seconds.")]
        public float charDelay = 0.03f;
        [Tooltip("Fade length of one character, in seconds.")]
        public float charDuration = 0.25f;
        [Tooltip("Characters start this far from their final position, in text units.")]
        public Vector2 slide = new Vector2(0f, -10f);

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
                    float eased = 1f - (1f - t) * (1f - t);
                    Offset(info, c, (Vector3)(slide * (1f - eased)));
                    MultiplyAlpha(info, c, t);
                }
            }
            return Time < (AffectedCount - 1) * charDelay + charDuration;
        }
    }
}
