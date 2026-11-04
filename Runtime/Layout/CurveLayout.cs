using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Bends the text with an AnimationCurve drawn in the inspector (WordArt style): the curve goes from
    /// the left (0) to the right (1) of the text, its value times <see cref="Height"/> lifts the characters.
    /// Static: computed once per text change.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Layout/Curve")]
    public sealed class CurveLayout : TextLayoutEffect
    {
        [Tooltip("Vertical shape across the text: x 0..1 = left..right, y = lift (multiplied by Height).")]
        [SerializeField] AnimationCurve _curve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f), new Keyframe(0.5f, 1f, 0f, 0f), new Keyframe(1f, 0f, -2f, 0f));
        [Tooltip("Lift at curve value 1, in text units. Negative flips the curve.")]
        [SerializeField] float _height = 30f;

        float _minX;
        float _width;
        float _baseline;
        float _anchorX;
        float _scale;

        public AnimationCurve Curve
        {
            get => _curve;
            set { _curve = value; Refresh(); }
        }

        public float Height
        {
            get => _height;
            set { _height = value; Refresh(); }
        }

        protected override void BeginLayout(in TextFrame frame)
        {
            _minX = frame.MinX;
            _width = Mathf.Max(1e-4f, frame.Width);
            _baseline = frame.Baseline;
            _anchorX = frame.AnchorX;
            _scale = _height * UnitScale;
        }

        protected override void Evaluate(float distance, out Vector2 point, out Vector2 direction)
        {
            float x = _anchorX + distance;
            if (_curve == null || _curve.length == 0)
            {
                point = new Vector2(x, _baseline);
                direction = Vector2.right;
                return;
            }

            float u = (x - _minX) / _width;
            const float e = 0.01f;
            float y = _curve.Evaluate(u) * _scale;
            float slope = (_curve.Evaluate(u + e) - _curve.Evaluate(u - e)) / (2f * e) * _scale / _width;
            point = new Vector2(x, _baseline + y);
            direction = new Vector2(1f, slope);
        }
    }
}
