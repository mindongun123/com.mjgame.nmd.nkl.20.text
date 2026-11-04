using TMPro;
using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Flashes the text towards a color (e.g. red when the player cannot afford an item,
    /// white when a value increases). Plays once over <see cref="duration"/>, or loops when it is 0.
    /// Tag args: c (or =, color: "red", "#ff0000"), r (rate), d (char delay), a (strength).
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Effects/Color Flash")]
    public sealed class ColorFlashEffect : TextEffect
    {
        public override TMP_VertexDataUpdateFlags Modifies => TMP_VertexDataUpdateFlags.Colors32;
        public override string DefaultTag => "flash";

        public Color color = new Color(1f, 0.25f, 0.25f, 1f);
        [Range(0f, 1f)] public float strength = 1f;
        [Tooltip("Flashes per second.")]
        [Min(0.01f)] public float rate = 4f;
        [Tooltip("Seconds; 0 = loop forever.")]
        public float duration = 0.5f;
        [Tooltip("Delay between neighbouring characters, in seconds: makes the flash sweep across the text.")]
        public float charDelay;
        [Tooltip("Spaces and other invisible characters do not add a delay.")]
        public bool skipSpaces = true;

        public override bool IsLooping => duration <= 0f;

        protected override bool Apply(TMP_TextInfo info)
        {
            bool finite = duration > 0f;
            float end = 0f;
            int n = 0;
            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                // <flash=red> or <flash c=#ffffff r=8 d=0.05 a=0.5>
                TagArgs args = GetRangeArgs(r);
                Color target = args.GetColor("c", "", color);
                float speed = Mathf.Max(0.01f, args.GetFloat("r", rate));
                float delay = args.GetFloat("d", charDelay);
                float amount = Mathf.Clamp01(args.GetFloat("a", strength));
                for (int c = range.x; c < range.y; c++)
                {
                    bool counts = !skipSpaces || info.characterInfo[c].isVisible;
                    float start = n * delay;
                    if (counts) n++;
                    end = Mathf.Max(end, start + duration);
                    if (!IsDrawable(info, c)) continue;
                    float t = Time - start;
                    if (t < 0f || (finite && t > duration)) continue;
                    // 0 → 1 → 0 once per flash.
                    float flash = Mathf.Sin(t * speed * Mathf.PI);
                    TintColor(info, c, target, flash * flash * amount);
                }
            }
            return !finite || Time < end;
        }
    }
}
