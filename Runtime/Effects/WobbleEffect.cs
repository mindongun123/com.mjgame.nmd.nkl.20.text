using TMPro;
using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>Characters rock left and right around their center. Loops forever. Tag args: a (or =), s, d.</summary>
    [ExecuteAlways]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Effects/Wobble")]
    public sealed class WobbleEffect : TextEffect
    {
        public override TMP_VertexDataUpdateFlags Modifies => TMP_VertexDataUpdateFlags.Vertices;
        [Tooltip("Max rotation, in degrees.")]
        public float angle = 10f;
        [Tooltip("Radians per second.")]
        public float speed = 6f;
        [Tooltip("Phase difference between neighbouring characters.")]
        public float spacing = 0.8f;

        public override bool IsLooping => true;

        protected override bool Apply(TMP_TextInfo info)
        {
            int n = 0;
            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                // <wobble=20> or <wobble a=20 s=4 d=1>
                TagArgs args = GetRangeArgs(r);
                float ang = args.GetFloat("a", "", angle) * MotionScale;
                float spd = args.GetFloat("s", speed);
                float dens = args.GetFloat("d", spacing);
                for (int c = range.x; c < range.y; c++, n++)
                {
                    if (!IsDrawable(info, c)) continue;
                    Rotate(info, c, Mathf.Sin(Time * spd + n * dens) * ang);
                }
            }
            return true;
        }
    }
}
