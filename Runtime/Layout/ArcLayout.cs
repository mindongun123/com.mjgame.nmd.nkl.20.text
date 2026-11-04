using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Bends the text along a circle arc: badges, banners, logos, text around a wheel (360°).
    /// Static: computed once per text change. The anchor of the arc is its top (or bottom when bent down),
    /// placed where the text was.
    /// <para>
    /// Size the arc either by <b>angle</b> (the text always covers that angle, whatever its length) or by
    /// <b>radius</b> (fixed circle: longer texts cover a bigger angle, several texts share the same curve).
    /// </para>
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Layout/Arc")]
    public sealed class ArcLayout : TextLayoutEffect, ISerializationCallbackReceiver
    {
        public enum Bend
        {
            /// <summary>∩, like a rainbow.</summary>
            Up,
            /// <summary>∪, like a smile.</summary>
            Down,
        }

        public enum Size
        {
            /// <summary>The text covers <see cref="Angle"/> degrees; the radius follows the text width.</summary>
            Angle,
            /// <summary>Fixed circle of <see cref="Radius"/>; the angle follows the text width.</summary>
            Radius,
        }

        [Tooltip("Up = ∩ (rainbow), Down = ∪ (smile).")]
        [SerializeField] Bend _direction = Bend.Up;
        [Tooltip("Angle: the text always covers the same angle. Radius: fixed circle, longer texts wrap further around it.")]
        [SerializeField] Size _sizeBy = Size.Angle;
        [Tooltip("Angle covered by the text, in degrees. 360 = full circle.")]
        [Range(0f, 360f)] [SerializeField] float _angle = 90f;
        [Tooltip("Circle radius in text units. Bigger = flatter.")]
        [Min(1f)] [SerializeField] float _radius = 300f;

        float _r;
        bool _down;
        Vector2 _apex;

        /// <summary>Last computed radius in text units (for the inspector).</summary>
        public float CurrentRadius => _r;

        /// <summary>Degrees covered by the text; negative = bend down. Setting it switches to <see cref="Size.Angle"/>.</summary>
        public float Angle
        {
            get => _direction == Bend.Down ? -_angle : _angle;
            set
            {
                _direction = value < 0f ? Bend.Down : Bend.Up;
                _angle = Mathf.Min(Mathf.Abs(value), 360f);
                _sizeBy = Size.Angle;
                Refresh();
            }
        }

        /// <summary>Circle radius in text units. Setting it switches to <see cref="Size.Radius"/>.</summary>
        public float Radius
        {
            get => _radius;
            set
            {
                _radius = Mathf.Max(1f, value);
                _sizeBy = Size.Radius;
                Refresh();
            }
        }

        public Bend Direction
        {
            get => _direction;
            set { _direction = value; Refresh(); }
        }

        public Size SizeBy
        {
            get => _sizeBy;
            set { _sizeBy = value; Refresh(); }
        }

        protected override void BeginLayout(in TextFrame frame)
        {
            _apex = new Vector2(frame.AnchorX, frame.Baseline);
            _down = _direction == Bend.Down;
            if (_sizeBy == Size.Radius)
            {
                _r = _radius * UnitScale;
            }
            else
            {
                float radians = _angle * Mathf.Deg2Rad;
                _r = radians > 1e-4f ? frame.Width / radians : 0f;
            }
        }

        protected override void Evaluate(float distance, out Vector2 point, out Vector2 direction) =>
            EvaluateLine(distance, 0f, out point, out direction);

        // Each line runs on its own concentric circle, so inner lines are not squeezed.
        protected override void EvaluateLine(float distance, float offset, out Vector2 point, out Vector2 direction)
        {
            if (_r <= 0f)
            {
                point = _apex + new Vector2(distance, offset);
                direction = Vector2.right;
                return;
            }

            if (_down)
            {
                // Center above the text: lower lines are on a bigger circle.
                Vector2 center = _apex + new Vector2(0f, _r);
                float r = Mathf.Max(1e-3f, _r - offset);
                float phi = distance / r;
                float sin = Mathf.Sin(phi), cos = Mathf.Cos(phi);
                point = center + new Vector2(r * sin, -r * cos);
                direction = new Vector2(cos, sin);
            }
            else
            {
                // Center below the text: lower lines are on a smaller circle.
                Vector2 center = _apex - new Vector2(0f, _r);
                float r = Mathf.Max(1e-3f, _r + offset);
                float phi = distance / r;
                float sin = Mathf.Sin(phi), cos = Mathf.Cos(phi);
                point = center + new Vector2(r * sin, r * cos);
                direction = new Vector2(cos, -sin);
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        // Older data stored "bend down" as a negative angle.
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (_angle >= 0f) return;
            _angle = -_angle;
            _direction = Bend.Down;
        }
    }
}
