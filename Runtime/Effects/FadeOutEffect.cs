using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Characters disappear one after another (closing a dialogue, removing a label), optionally sliding
    /// and shrinking. When finished the text stays hidden at no per-frame cost: it leaves the update loop
    /// and is only re-applied if the text is rebuilt. Call <see cref="TextEffect.Stop"/> to show it again,
    /// or <see cref="TextEffect.Play"/> to hide it again. Not started on enable by default.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Effects/Fade Out")]
    public sealed class FadeOutEffect : TextEffect
    {
        public enum Order
        {
            /// <summary>First character first.</summary>
            Forward,
            /// <summary>Last character first ("un-typing").</summary>
            Backward,
            /// <summary>Every character at the same time.</summary>
            AllAtOnce,
        }

        // Whole text: hiding only a tagged part is rarely wanted.
        public override string DefaultTag => "";
        protected override bool DefaultPlayOnEnable => false;
        // Previewing in edit mode would hide the text in the scene view.
        protected override bool PreviewsInEditMode => false;
        // Once hidden nothing changes over time, so the text leaves the update loop.
        public override bool IsAnimated => !_done;

        public Order order = Order.Forward;
        [Tooltip("Delay between two characters, in seconds (ignored for All At Once).")]
        public float charDelay = 0.02f;
        [Tooltip("Fade length of one character, in seconds.")]
        public float charDuration = 0.2f;
        [Tooltip("Characters move this far while fading, in text units.")]
        public Vector2 slide = new Vector2(0f, 10f);
        [Tooltip("Size of a character at the end of its fade; 1 = no shrinking.")]
        [Range(0f, 2f)] public float endScale = 1f;
        [Tooltip("Spaces and other invisible characters do not add a delay.")]
        public bool skipSpaces = true;

        [Tooltip("Raised once when every character is hidden.")]
        public UnityEvent onComplete = new UnityEvent();

        bool _done;
        bool _completeRaised;

        /// <summary>True once every character is hidden (until the next Play or Stop).</summary>
        public bool IsHidden => _done && IsPlaying;

        /// <summary>Hides the text immediately, without the animation (onComplete is still raised).</summary>
        public void HideNow()
        {
            Play();
            _done = true;
        }

        protected override void OnPlay()
        {
            _done = false;
            _completeRaised = false;
        }

        protected override bool Apply(TMP_TextInfo info)
        {
            // Counted first so Backward can number the characters from the end.
            int total = 0;
            if (order == Order.Backward)
                for (int r = 0; r < RangeCount; r++)
                {
                    Vector2Int range = GetRange(r);
                    for (int c = range.x; c < range.y; c++)
                        if (!skipSpaces || info.characterInfo[c].isVisible) total++;
                }

            Vector3 move = (Vector3)(slide * (UnitScale * MotionScale));
            float shrink = Mathf.Lerp(1f, endScale, MotionScale);
            float end = 0f;
            int n = 0;
            for (int r = 0; r < RangeCount; r++)
            {
                Vector2Int range = GetRange(r);
                for (int c = range.x; c < range.y; c++)
                {
                    bool counts = !skipSpaces || info.characterInfo[c].isVisible;
                    int slot = order == Order.AllAtOnce ? 0 : order == Order.Backward ? Mathf.Max(0, total - 1 - n) : n;
                    if (counts) n++;
                    float start = slot * charDelay;
                    end = Mathf.Max(end, start + charDuration);
                    if (!IsDrawable(info, c)) continue;

                    float t = _done ? 1f
                        : charDuration > 0f ? Mathf.Clamp01((Time - start) / charDuration)
                        : Time >= start ? 1f : 0f;
                    if (t <= 0f) continue;
                    float eased = t * t;
                    if (move != Vector3.zero) Offset(info, c, move * eased);
                    if (shrink != 1f) Scale(info, c, Mathf.Lerp(1f, shrink, eased));
                    MultiplyAlpha(info, c, 1f - t);
                }
            }

            if (!_done && Time >= end) _done = true;
            if (_done && !_completeRaised)
            {
                _completeRaised = true;
                onComplete.Invoke();
            }
            return true; // stays applied (hidden) until Stop or Play
        }
    }
}
