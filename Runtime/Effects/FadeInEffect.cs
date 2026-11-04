using TMPro;
using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Characters fade in one after another, optionally sliding in. Plays once.
    /// Tag args: d (or =, char delay), t (char duration), x, y (slide).
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Effects/Fade In")]
    public sealed class FadeInEffect : TextEffect
    {
        public override string DefaultTag => "fade";

        [Tooltip("Delay between two characters, in seconds.")]
        public float charDelay = 0.03f;
        [Tooltip("Fade length of one character, in seconds.")]
        public float charDuration = 0.25f;
        [Tooltip("Characters start this far from their final position, in text units.")]
        public Vector2 slide = new Vector2(0f, -10f);
        [Tooltip("Spaces and other invisible characters do not add a delay.")]
        public bool skipSpaces = true;

        protected override bool Apply(TMP_TextInfo info)
        {
            float end = 0f;
            int n = 0;
            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                // <fade=0.05> or <fade d=0.05 t=0.5 x=0 y=-20>
                TagArgs args = GetRangeArgs(r);
                float delay = args.GetFloat("d", "", charDelay);
                float length = args.GetFloat("t", charDuration);
                Vector2 from = new Vector2(args.GetFloat("x", slide.x), args.GetFloat("y", slide.y)) * (UnitScale * MotionScale);
                for (int c = range.x; c < range.y; c++)
                {
                    bool counts = !skipSpaces || info.characterInfo[c].isVisible;
                    float start = n * delay;
                    if (counts) n++;
                    end = Mathf.Max(end, start + length);
                    if (!IsDrawable(info, c)) continue;
                    float t = length > 0f ? Mathf.Clamp01((Time - start) / length) : 1f;
                    float eased = 1f - (1f - t) * (1f - t);
                    Offset(info, c, (Vector3)(from * (1f - eased)));
                    MultiplyAlpha(info, c, t);
                }
            }
            return Time < end;
        }
    }
}
