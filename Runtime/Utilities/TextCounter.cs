using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Animated number (coins, score, rewards, timers). Formats with <see cref="TextFormat"/> into a reused
    /// buffer and only re-renders when the shown value changes, so counting allocates no garbage.
    /// Idle counters have no Update: only counting ones are ticked, by the shared runner.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Text Counter")]
    public sealed class TextCounter : MonoBehaviour, ISerializationCallbackReceiver
    {
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (_legacyAbbreviateDecimals < 0) return;
            // Old counters held whole numbers, so their full-value decimals never showed: the short form's
            // setting is the one that mattered.
            decimals = Mathf.Max(decimals, _legacyAbbreviateDecimals);
            _legacyAbbreviateDecimals = -1;
        }

        public enum Display
        {
            /// <summary>Full value, never grouped: "1234567", "1058,6"</summary>
            Number,
            /// <summary>"1.2M"</summary>
            Abbreviated,
            /// <summary>Value is seconds: "02:05"</summary>
            Time,
        }

        // double so prices like 0.199 work; whole numbers stay exact up to 2^53 (9 million billion).
        [SerializeField] double _value;
        [Tooltip("Seconds to count from the old to the new value; 0 = jump.")]
        public float duration = 0.6f;
        public Display display = Display.Number;
        public string prefix = "";
        public string suffix = "";
        [Tooltip("Decimal mark, the only mark ever written (no thousands grouping). " +
                 "\",\": 1058,6 / 19999999999 / 1,9k. \".\": 1058.6 / 1.9k. Empty = \".\".")]
        public string separator = ".";
        [Tooltip("Max decimals, for full values (2 → 58.77) and the short form (2 → 1.99k, at most 3 there). " +
                 "Truncated, trailing zeros dropped (199, 2.5); a short form with 3 digits gets none (150k).")]
        [Range(0, 6)] public int decimals = 2;
        [Tooltip("Abbreviated: keep the full number below this value.")]
        public double abbreviateFrom = 10000;

        // Before 0.8 the short form had its own setting; its value moves into `decimals` when an old
        // counter is loaded (-1 = nothing to migrate).
        [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("abbreviateDecimals")]
        int _legacyAbbreviateDecimals = -1;

        /// <summary>Old name of <see cref="decimals"/>, which is now shared by both forms.</summary>
        [Obsolete("Use decimals: one setting for full and abbreviated values.")]
        public int abbreviateDecimals
        {
            get => decimals;
            set => decimals = value;
        }
        public TimeFormat timeFormat = TimeFormat.Auto;
        public UnityEvent onComplete = new UnityEvent();

        TMP_Text _text;
        char[] _buffer = new char[64];
        double _from;
        double _to;
        float _progress;
        bool _counting;

        public TMP_Text Text => _text != null ? _text : (_text = GetComponent<TMP_Text>());

        /// <summary>Displayed value. While counting this is the intermediate value.</summary>
        public double Value => _value;

        /// <summary>Value the counter is heading to.</summary>
        public double Target => _counting ? _to : _value;

        public bool IsCounting => _counting;

        static readonly List<TextCounter> Counting = new List<TextCounter>();
        [NonSerialized] int _index = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Counting.Clear();

        void OnEnable()
        {
            Render(_value);
            if (_counting) Track();
        }

        void OnDisable() => Untrack(); // a paused count resumes when enabled again

        bool IsTracked => _index >= 0 && _index < Counting.Count && ReferenceEquals(Counting[_index], this);

        void Track()
        {
            if (IsTracked) return;
            _index = Counting.Count;
            Counting.Add(this);
            TextFXRunner.EnsureInstance();
        }

        void Untrack()
        {
            if (IsTracked) RemoveAt(_index);
            _index = -1;
        }

        static void RemoveAt(int i)
        {
            int last = Counting.Count - 1;
            TextCounter moved = Counting[last];
            Counting[i] = moved;
            if (!ReferenceEquals(moved, null)) moved._index = i;
            Counting.RemoveAt(last);
        }

        /// <summary>Called once per frame by the runner. Backwards: finishing counters remove themselves.</summary>
        internal static void TickAll(float unscaledDelta)
        {
            for (int i = Counting.Count - 1; i >= 0; i--)
            {
                if (i >= Counting.Count) continue;
                TextCounter counter = Counting[i];
                if (counter == null) RemoveAt(i);
                else counter.Step(unscaledDelta);
            }
        }

        /// <summary>Jumps to a value without animation.</summary>
        public void SetValue(double value)
        {
            _counting = false;
            Untrack();
            _value = value;
            Render(value);
        }

        public void CountTo(double target)
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
            Track();
        }

        /// <summary>Adds to the target value; usable from UnityEvents (int argument).</summary>
        public void Add(int amount) => CountTo(Target + amount);

        public void Add(long amount) => CountTo(Target + amount);

        /// <summary>Adds to the target value; usable from UnityEvents (float argument, e.g. 0.05).</summary>
        public void Add(float amount) => CountTo(Target + amount);

        public void Add(double amount) => CountTo(Target + amount);

        /// <summary>Finishes the current count immediately.</summary>
        public void Complete()
        {
            if (!_counting) return;
            SetValue(_to);
            onComplete.Invoke();
        }

        void Step(float unscaledDelta)
        {
            if (!_counting) return;
            _progress += unscaledDelta / duration;
            if (_progress >= 1f)
            {
                Complete();
                return;
            }

            float eased = 1f - (1f - _progress) * (1f - _progress) * (1f - _progress);
            // Intermediate values keep the precision of the ends: 50800 → 70800 counts in whole numbers
            // (no "60234,5"), 0.5 → 2.75 in hundredths.
            int places = Math.Min(decimals, Math.Max(DecimalPlaces(_from), DecimalPlaces(_to)));
            double value = Math.Round(_from + (_to - _from) * eased, places, MidpointRounding.AwayFromZero);
            // Only re-render when the shown digits can change (the step of the last shown decimal).
            double step = Math.Abs(value) < AbbreviateLimit || display != Display.Abbreviated ? StepOf(decimals) : 0;
            if (step > 0 && Math.Floor(value / step) == Math.Floor(_value / step)) return;
            _value = value;
            Render(value);
        }

        double AbbreviateLimit => Math.Max(1000, abbreviateFrom);

        /// <summary>Decimals a value really has (0 for 50800, 2 for 2.75), at most 9.</summary>
        static int DecimalPlaces(double value)
        {
            double v = Math.Abs(value);
            for (int places = 0; places < 9; places++)
            {
                double scaled = v * Math.Pow(10, places);
                if (Math.Abs(scaled - Math.Round(scaled)) < 1e-6 * Math.Max(1.0, scaled)) return places;
            }
            return 9;
        }

        static double StepOf(int decimals) => Math.Pow(10, -Math.Max(0, Math.Min(decimals, 9)));

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) Render(_value);
            };
        }
#endif

        void Render(double value)
        {
            int needed = (prefix?.Length ?? 0) + (suffix?.Length ?? 0) + TextFormat.MaxNumberLength + 12;
            if (_buffer.Length < needed) _buffer = new char[needed * 2];
            char sep = string.IsNullOrEmpty(separator) ? '\0' : separator[0];

            int i = TextFormat.WriteString(_buffer, 0, prefix);
            switch (display)
            {
                case Display.Abbreviated:
                {
                    // One mark for everything: 0,199 / 199 / 1,9k. No thousands grouping, so it is never ambiguous.
                    char point = sep == '\0' ? '.' : sep;
                    i = Math.Abs(value) < AbbreviateLimit
                        ? TextFormat.WriteTrimmed(_buffer, i, value, decimals, point)
                        : TextFormat.WriteAbbreviated(_buffer, i, value, decimals, AbbreviateLimit, '\0', point);
                    break;
                }
                case Display.Time:
                    i = TextFormat.WriteTime(_buffer, i, value, timeFormat);
                    break;
                default:
                {
                    // The full value, never grouped: 1058,6 / 19999999999. Separator = decimal mark.
                    char point = sep == '\0' ? '.' : sep;
                    i = TextFormat.WriteTrimmed(_buffer, i, value, decimals, point);
                    break;
                }
            }
            i = TextFormat.WriteString(_buffer, i, suffix);
            TMPTextExtensions.SetChars(Text, _buffer, i);
        }
    }
}
