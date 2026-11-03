using System;

namespace com.nmd.nkl.text
{
    public enum TimeFormat
    {
        /// <summary>"1:02:05" from one hour, otherwise "02:05".</summary>
        Auto,
        /// <summary>"02:05"; minutes keep counting past 59 ("75:00").</summary>
        MinutesSeconds,
        /// <summary>"1:02:05"; hours keep counting past 23.</summary>
        HoursMinutesSeconds,
        /// <summary>Two largest units: "1d 3h", "3h 20m", "5m 10s", "9s".</summary>
        Compact,
    }

    /// <summary>Unit suffixes for <see cref="TimeFormat.Compact"/>; replace them for localization.</summary>
    public sealed class TimeLabels
    {
        public static readonly TimeLabels Default = new TimeLabels("d", "h", "m", "s");

        public readonly string Days;
        public readonly string Hours;
        public readonly string Minutes;
        public readonly string Seconds;

        public TimeLabels(string days, string hours, string minutes, string seconds)
        {
            Days = days ?? "";
            Hours = hours ?? "";
            Minutes = minutes ?? "";
            Seconds = seconds ?? "";
        }
    }

    /// <summary>
    /// Allocation-free formatting into a char buffer. Every Write method writes at
    /// <c>index</c> and returns the index after the last written char.
    /// The caller provides a buffer large enough; <see cref="MaxNumberLength"/> chars always
    /// fit one number. Use the <see cref="TMPTextExtensions"/> to format straight into a TMP text.
    /// </summary>
    public static class TextFormat
    {
        /// <summary>Upper bound of chars written by one number/time/percent write (no prefix/suffix).</summary>
        public const int MaxNumberLength = 40;

        /// <summary>Thousand-based suffixes. long.MaxValue is 9.2Qi.</summary>
        /// <summary>
        /// Short scale, one entry per power of 1000 (index 0 = no suffix). long reaches "Qi";
        /// the double overload goes up to "Vg" (10^63).
        /// </summary>
        static readonly string[] Units =
        {
            "", "k", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc",
            "Ud", "Dd", "Td", "Qad", "Qid", "Sxd", "Spd", "Ocd", "Nod", "Vg",
        };

        static readonly ulong[] Pow10 =
        {
            1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000,
        };

        public static int WriteString(char[] buffer, int index, string value)
        {
            if (string.IsNullOrEmpty(value)) return index;
            value.CopyTo(0, buffer, index, value.Length);
            return index + value.Length;
        }

        /// <summary>"1,234,567". Pass '\0' as separator for "1234567".</summary>
        public static int WriteNumber(char[] buffer, int index, long value, char separator = ',')
        {
            if (value < 0) buffer[index++] = '-';
            return WriteDigits(buffer, index, Magnitude(value), separator);
        }

        /// <summary>
        /// "15.3k", "1.25M", "3B". Values below <paramref name="from"/> are written like
        /// <see cref="WriteNumber"/>. Decimals are truncated (never shows more than the real value)
        /// and trailing zeros are dropped; three-digit wholes ("150k") get no decimals.
        /// Exact for every long (up to "9.2Qi"); use the double overload for idle-game sized values.
        /// </summary>
        public static int WriteAbbreviated(char[] buffer, int index, long value, int decimals = 1,
            long from = 1000, char separator = ',')
        {
            ulong magnitude = Magnitude(value);
            if (magnitude < (ulong)Math.Max(1000, from)) return WriteNumber(buffer, index, value, separator);
            if (value < 0) buffer[index++] = '-';

            int unit = 0;
            ulong divisor = 1;
            while (unit < Units.Length - 1 && magnitude / divisor >= 1000)
            {
                divisor *= 1000;
                unit++;
            }

            ulong whole = magnitude / divisor;
            // remainder / divisor < 1, so the double keeps enough precision for 3 digits.
            double fraction = (double)(magnitude % divisor) / divisor;
            return WriteScaled(buffer, index, whole, fraction, decimals, unit);
        }

        /// <summary>
        /// Same rules as the long overload, for values beyond long (idle games): up to "Vg" (10^63).
        /// Beyond that the suffix stays "Vg" and the whole part grows (clamped to ulong).
        /// NaN writes "0", infinity "∞". Precision is that of double (~15 significant digits).
        /// </summary>
        public static int WriteAbbreviated(char[] buffer, int index, double value, int decimals = 1,
            double from = 1000, char separator = ',')
        {
            if (double.IsNaN(value)) value = 0;
            if (value < 0) buffer[index++] = '-';
            if (double.IsInfinity(value))
            {
                buffer[index++] = '∞';
                return index;
            }

            double magnitude = Math.Abs(value);
            if (magnitude < Math.Max(1000, from))
                return WriteDigits(buffer, index, (ulong)Math.Floor(magnitude), separator);

            int unit = Math.Min((int)Math.Floor(Math.Log10(magnitude) / 3), Units.Length - 1);
            double scaled = magnitude / Math.Pow(1000, unit);
            // Log10 can land one unit off at exact powers of 1000.
            if (scaled >= 1000 && unit < Units.Length - 1)
            {
                unit++;
                scaled /= 1000;
            }
            else if (scaled < 1 && unit > 0)
            {
                unit--;
                scaled *= 1000;
            }

            // Tiny epsilon so 1.3 does not truncate to 1.2 because of binary rounding (1.29999...).
            scaled += scaled * 1e-12;
            double whole = Math.Floor(scaled);
            ulong wholeDigits = whole >= ulong.MaxValue ? ulong.MaxValue : (ulong)whole;
            return WriteScaled(buffer, index, wholeDigits, scaled - whole, decimals, unit);
        }

        static int WriteScaled(char[] buffer, int index, ulong whole, double fraction, int decimals, int unit)
        {
            index = WriteDigits(buffer, index, whole, '\0');

            decimals = Math.Max(0, Math.Min(decimals, 3));
            if (whole < 100 && decimals > 0)
            {
                ulong digits = (ulong)Math.Floor(fraction * Pow10[decimals]);
                if (digits >= Pow10[decimals]) digits = Pow10[decimals] - 1;
                while (decimals > 0 && digits % 10 == 0)
                {
                    digits /= 10;
                    decimals--;
                }
                if (decimals > 0)
                {
                    buffer[index++] = '.';
                    index = WritePadded(buffer, index, digits, decimals);
                }
            }

            return WriteString(buffer, index, Units[unit]);
        }

        /// <summary>Fixed decimals, rounded half away from zero: 3.14159 → "3.14".</summary>
        public static int WriteDecimal(char[] buffer, int index, double value, int decimals = 2, char separator = '\0')
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) value = 0;
            decimals = Math.Max(0, Math.Min(decimals, 9));
            double scaled = Math.Round(Math.Abs(value) * Pow10[decimals], MidpointRounding.AwayFromZero);
            if (scaled > long.MaxValue) scaled = long.MaxValue;
            ulong units = (ulong)scaled;
            ulong whole = units / Pow10[decimals];

            if (value < 0 && units > 0) buffer[index++] = '-';
            index = WriteDigits(buffer, index, whole, separator);
            if (decimals == 0) return index;
            buffer[index++] = '.';
            return WritePadded(buffer, index, units % Pow10[decimals], decimals);
        }

        /// <summary>0.756 → "76%" (decimals 0), "75.6%" (decimals 1).</summary>
        public static int WritePercent(char[] buffer, int index, double ratio, int decimals = 0)
        {
            index = WriteDecimal(buffer, index, ratio * 100.0, decimals);
            buffer[index++] = '%';
            return index;
        }

        /// <summary>"3/10", "1,250/5,000".</summary>
        public static int WriteFraction(char[] buffer, int index, long current, long max, char separator = ',')
        {
            index = WriteNumber(buffer, index, current, separator);
            buffer[index++] = '/';
            return WriteNumber(buffer, index, max, separator);
        }

        /// <param name="roundUp">True for countdowns: 0.2 s left shows "00:01" instead of "00:00".</param>
        public static int WriteTime(char[] buffer, int index, double seconds, TimeFormat format = TimeFormat.Auto,
            bool roundUp = false, TimeLabels labels = null)
        {
            if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
            double rounded = roundUp ? Math.Ceiling(seconds) : Math.Floor(seconds);
            ulong total = rounded >= ulong.MaxValue ? ulong.MaxValue : (ulong)rounded;

            ulong s = total % 60;
            ulong totalMinutes = total / 60;
            ulong m = totalMinutes % 60;
            ulong totalHours = totalMinutes / 60;

            switch (format)
            {
                case TimeFormat.MinutesSeconds:
                    index = WritePadded(buffer, index, totalMinutes, 2);
                    buffer[index++] = ':';
                    return WritePadded(buffer, index, s, 2);

                case TimeFormat.HoursMinutesSeconds:
                    return WriteHms(buffer, index, totalHours, m, s);

                case TimeFormat.Compact:
                    labels = labels ?? TimeLabels.Default;
                    ulong d = totalHours / 24;
                    ulong h = totalHours % 24;
                    if (d > 0) return WriteUnitPair(buffer, index, d, labels.Days, h, labels.Hours);
                    if (h > 0) return WriteUnitPair(buffer, index, h, labels.Hours, m, labels.Minutes);
                    if (m > 0) return WriteUnitPair(buffer, index, m, labels.Minutes, s, labels.Seconds);
                    index = WriteDigits(buffer, index, s, '\0');
                    return WriteString(buffer, index, labels.Seconds);

                default:
                    if (totalHours > 0) return WriteHms(buffer, index, totalHours, m, s);
                    index = WritePadded(buffer, index, m, 2);
                    buffer[index++] = ':';
                    return WritePadded(buffer, index, s, 2);
            }
        }

        /// <summary>Convenience for tests and editor code; allocates a string.</summary>
        public static string ToString(char[] buffer, int length) => new string(buffer, 0, length);

        static int WriteHms(char[] buffer, int index, ulong hours, ulong minutes, ulong seconds)
        {
            index = WriteDigits(buffer, index, hours, '\0');
            buffer[index++] = ':';
            index = WritePadded(buffer, index, minutes, 2);
            buffer[index++] = ':';
            return WritePadded(buffer, index, seconds, 2);
        }

        static int WriteUnitPair(char[] buffer, int index, ulong major, string majorLabel, ulong minor, string minorLabel)
        {
            index = WriteDigits(buffer, index, major, '\0');
            index = WriteString(buffer, index, majorLabel);
            if (minor == 0) return index;
            buffer[index++] = ' ';
            index = WriteDigits(buffer, index, minor, '\0');
            return WriteString(buffer, index, minorLabel);
        }

        /// <summary>Absolute value as ulong; safe for long.MinValue.</summary>
        static ulong Magnitude(long value) => value < 0 ? (ulong)(-(value + 1)) + 1 : (ulong)value;

        static int WriteDigits(char[] buffer, int index, ulong number, char separator)
        {
            bool separate = separator != '\0';
            int digits = 1;
            for (ulong n = number; n >= 10; n /= 10) digits++;
            int length = separate ? digits + (digits - 1) / 3 : digits;

            int pos = index + length - 1;
            int written = 0;
            do
            {
                if (separate && written > 0 && written % 3 == 0) buffer[pos--] = separator;
                buffer[pos--] = (char)('0' + (int)(number % 10));
                number /= 10;
                written++;
            } while (number > 0);
            return index + length;
        }

        /// <summary>Writes at least <paramref name="width"/> digits, left-padded with zeros.</summary>
        static int WritePadded(char[] buffer, int index, ulong number, int width)
        {
            int digits = 1;
            for (ulong n = number; n >= 10; n /= 10) digits++;
            for (int i = digits; i < width; i++) buffer[index++] = '0';
            return WriteDigits(buffer, index, number, '\0');
        }
    }
}
