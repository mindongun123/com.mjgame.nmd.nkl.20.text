using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Animated number (coins, score, rewards, timers). Formats with <see cref="TextFormat"/> into a reused
    /// buffer and only re-renders when the shown value changes, so counting allocates no garbage.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Text Counter")]
    public sealed class TextCounter : MonoBehaviour
    {
        public enum Display
        {
            /// <summary>"1,234,567"</summary>
            Number,
            /// <summary>"1.2M"</summary>
            Abbreviated,
            /// <summary>Value is seconds: "02:05"</summary>
            Time,
        }

        [SerializeField] long _value;
        [Tooltip("Seconds to count from the old to the new value; 0 = jump.")]
        public float duration = 0.6f;
        public Display display = Display.Number;
        public string prefix = "";
        public string suffix = "";
        [Tooltip("Thousands separator; leave empty for none.")]
        public string separator = ",";
        [Tooltip("Abbreviated: keep the full number below this value.")]
        public long abbreviateFrom = 10000;
        [Range(0, 3)] public int abbreviateDecimals = 1;
        public TimeFormat timeFormat = TimeFormat.Auto;
        public UnityEvent onComplete = new UnityEvent();

        TMP_Text _text;
        char[] _buffer = new char[64];
        long _from;
        long _to;
        float _progress;
        bool _counting;

        public TMP_Text Text => _text != null ? _text : (_text = GetComponent<TMP_Text>());

        /// <summary>Displayed value. While counting this is the intermediate value.</summary>
        public long Value => _value;

        /// <summary>Value the counter is heading to.</summary>
        public long Target => _counting ? _to : _value;

        public bool IsCounting => _counting;

        void OnEnable() => Render(_value);

        /// <summary>Jumps to a value without animation.</summary>
        public void SetValue(long value)
        {
            _counting = false;
            _value = value;
            Render(value);
        }

        public void CountTo(long target)
        {
            if (duration <= 0f || !isActiveAndEnabled)
            {
                SetValue(target);
                onComplete.Invoke();
                return;
            }
            _from = _value;
            _to = target;
            _progress = 0f;
            _counting = true;
        }

        /// <summary>Adds to the target value; usable from UnityEvents (int argument).</summary>
        public void Add(int amount) => CountTo(Target + amount);

        public void Add(long amount) => CountTo(Target + amount);

        /// <summary>Finishes the current count immediately.</summary>
        public void Complete()
        {
            if (!_counting) return;
            SetValue(_to);
            onComplete.Invoke();
        }

        void Update()
        {
            if (!_counting) return;
            _progress += Time.unscaledDeltaTime / duration;
            if (_progress >= 1f)
            {
                Complete();
                return;
            }

            float eased = 1f - (1f - _progress) * (1f - _progress) * (1f - _progress);
            long value = _from + (long)Math.Round((_to - _from) * (double)eased);
            if (value == _value) return;
            _value = value;
            Render(value);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) Render(_value);
            };
        }
#endif

        void Render(long value)
        {
            int needed = (prefix?.Length ?? 0) + (suffix?.Length ?? 0) + TextFormat.MaxNumberLength;
            if (_buffer.Length < needed) _buffer = new char[needed * 2];
            char sep = string.IsNullOrEmpty(separator) ? '\0' : separator[0];

            int i = TextFormat.WriteString(_buffer, 0, prefix);
            switch (display)
            {
                case Display.Abbreviated:
                    i = TextFormat.WriteAbbreviated(_buffer, i, value, abbreviateDecimals, abbreviateFrom, sep);
                    break;
                case Display.Time:
                    i = TextFormat.WriteTime(_buffer, i, value, timeFormat);
                    break;
                default:
                    i = TextFormat.WriteNumber(_buffer, i, value, sep);
                    break;
            }
            i = TextFormat.WriteString(_buffer, i, suffix);
            Text.SetText(_buffer, 0, i);
        }
    }
}
