using TMPro;
using UnityEngine;

namespace com.nmd.nkl.text
{
    /// <summary>
    /// Flashes the text towards a color (e.g. red when the player cannot afford an item,
    /// white when a value increases). Plays once over <see cref="duration"/>, or loops when it is 0.
    /// </summary>
    [AddComponentMenu("com.nmd.nkl/Text/Effects/Color Flash")]
    public sealed class ColorFlashEffect : TextEffect
    {
        public Color color = new Color(1f, 0.25f, 0.25f, 1f);
        [Range(0f, 1f)] public float strength = 1f;
        [Tooltip("Flashes per second.")]
        [Min(0.01f)] public float rate = 4f;
        [Tooltip("Seconds; 0 = loop forever.")]
        public float duration = 0.5f;
        [Tooltip("Delay between neighbouring characters, in seconds: makes the flash sweep across the text.")]
        public float charDelay;

        public override bool IsLooping => duration <= 0f;

        protected override bool Apply(TMP_TextInfo info)
        {
            bool finite = duration > 0f;
            float end = finite ? duration + Mathf.Max(0, AffectedCount - 1) * charDelay : 0f;
            if (finite && Time >= end) return false;

            int n = 0;
            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                for (int c = range.x; c < range.y; c++, n++)
                {
                    if (!IsDrawable(info, c)) continue;
                    float t = Time - n * charDelay;
                    if (t < 0f || (finite && t > duration)) continue;
                    // 0 → 1 → 0 once per flash.
                    float flash = Mathf.Sin(t * rate * Mathf.PI);
                    TintColor(info, c, color, flash * flash * strength);
                }
            }
            return true;
        }
    }
}
