using System;
using TMPro;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Allocation-free setters for common game UI values. They format into a shared buffer and call
    /// <see cref="TMP_Text.SetText(char[], int, int)"/>, which copies the chars: no string is created.
    /// Prefix/suffix may contain rich text, e.g. "&lt;sprite name=coin&gt; ".
    /// Main thread only (like TMP itself).
    /// </summary>
    public static class TMPTextExtensions
    {
        static char[] _buffer = new char[128];

        /// <summary>"1,234,567".</summary>
        public static void SetNumber(this TMP_Text text, long value, string prefix = null, string suffix = null,
            char separator = ',')
        {
            int i = Begin(prefix, suffix);
            i = TextFormat.WriteNumber(_buffer, i, value, separator);
            End(text, i, suffix);
        }

        /// <summary>"15.3K", "1.2M". Below <paramref name="from"/> it writes the full number.</summary>
        public static void SetAbbreviated(this TMP_Text text, long value, int decimals = 1, long from = 1000,
            string prefix = null, string suffix = null, char separator = ',', char decimalPoint = '.')
        {
            int i = Begin(prefix, suffix);
            i = TextFormat.WriteAbbreviated(_buffer, i, value, decimals, from, separator, decimalPoint);
            End(text, i, suffix);
        }

        /// <summary>Idle-game sized values: "1.5Sx", "12Vg".</summary>
        public static void SetAbbreviated(this TMP_Text text, double value, int decimals = 1, double from = 1000,
            string prefix = null, string suffix = null, char separator = ',', char decimalPoint = '.')
        {
            int i = Begin(prefix, suffix);
            i = TextFormat.WriteAbbreviated(_buffer, i, value, decimals, from, separator, decimalPoint);
            End(text, i, suffix);
        }

        /// <summary>"3.14" ("3,14" with decimalPoint ',').</summary>
        public static void SetDecimal(this TMP_Text text, double value, int decimals = 2, string prefix = null,
            string suffix = null, char decimalPoint = '.')
        {
            int i = Begin(prefix, suffix);
            i = TextFormat.WriteDecimal(_buffer, i, value, decimals, '\0', decimalPoint);
            End(text, i, suffix);
        }

        /// <summary>Ratio 0..1 → "76%".</summary>
        public static void SetPercent(this TMP_Text text, double ratio, int decimals = 0, string prefix = null,
            string suffix = null, char decimalPoint = '.')
        {
            int i = Begin(prefix, suffix);
            i = TextFormat.WritePercent(_buffer, i, ratio, decimals, decimalPoint);
            End(text, i, suffix);
        }

        /// <summary>"3/10".</summary>
        public static void SetFraction(this TMP_Text text, long current, long max, string prefix = null,
            string suffix = null, char separator = ',')
        {
            int i = Begin(prefix, suffix, TextFormat.MaxNumberLength * 2);
            i = TextFormat.WriteFraction(_buffer, i, current, max, separator);
            End(text, i, suffix);
        }

        /// <summary>"02:05", "1:02:05" or "1d 3h". Use <paramref name="roundUp"/> for countdowns.</summary>
        public static void SetTime(this TMP_Text text, double seconds, TimeFormat format = TimeFormat.Auto,
            bool roundUp = false, string prefix = null, string suffix = null, TimeLabels labels = null)
        {
            int extra = labels == null ? 0 : labels.Days.Length + labels.Hours.Length + labels.Minutes.Length +
                                               labels.Seconds.Length;
            int i = Begin(prefix, suffix, TextFormat.MaxNumberLength + extra);
            i = TextFormat.WriteTime(_buffer, i, seconds, format, roundUp, labels);
            End(text, i, suffix);
        }

        static int Begin(string prefix, string suffix, int body = TextFormat.MaxNumberLength)
        {
            int needed = (prefix?.Length ?? 0) + (suffix?.Length ?? 0) + body;
            if (_buffer.Length < needed) _buffer = new char[needed * 2];
            return TextFormat.WriteString(_buffer, 0, prefix);
        }

        static void End(TMP_Text text, int index, string suffix)
        {
            index = TextFormat.WriteString(_buffer, index, suffix);
            SetChars(text, _buffer, index);
        }

        /// <summary>
        /// <see cref="TMP_Text.SetText(char[], int, int)"/> that still honours the text preprocessor.
        /// TMP 3.0 runs <see cref="TMP_Text.textPreprocessor"/> only for string input, so package tags
        /// (&lt;wave&gt;) in a char array would be shown as plain text. When the chars contain '&lt;' and the text
        /// has a preprocessor (e.g. <see cref="TextFX"/>), one string is created per call; texts without tags stay allocation-free.
        /// </summary>
        internal static void SetChars(TMP_Text text, char[] buffer, int length)
        {
            if (text.textPreprocessor != null && Array.IndexOf(buffer, '<', 0, length) >= 0)
            {
                // Do not read text.text to compare: after a char array set, TMP builds that string too.
                text.text = new string(buffer, 0, length);
                return;
            }
            // No tags: the previous text's spans must not be applied to the new content.
            if (text.textPreprocessor is TextFX fx) fx.ClearSpans();
            text.SetText(buffer, 0, length);
        }
    }
}
