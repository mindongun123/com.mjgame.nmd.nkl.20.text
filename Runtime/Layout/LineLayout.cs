using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Straight path, optionally tilted. Mostly used with the size curve (bigger in the middle, growing
    /// to the right...) on text that should stay straight. Static: computed once per text change.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Layout/Line")]
    public sealed class LineLayout : TextLayoutEffect
    {
        [Tooltip("Tilt of the line in degrees, around the anchor. Positive = rises to the right.")]
        [Range(-89f, 89f)] [SerializeField] float _tilt;

        Vector2 _origin;
        Vector2 _dir;

        public float Tilt
        {
            get => _tilt;
            set { _tilt = Mathf.Clamp(value, -89f, 89f); Refresh(); }
        }

        protected override void BeginLayout(in TextFrame frame)
        {
            _origin = new Vector2(frame.AnchorX, frame.Baseline);
            float rad = _tilt * Mathf.Deg2Rad;
            _dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        protected override void Evaluate(float distance, out Vector2 point, out Vector2 direction)
        {
            point = _origin + _dir * distance;
            direction = _dir;
        }
    }
}
