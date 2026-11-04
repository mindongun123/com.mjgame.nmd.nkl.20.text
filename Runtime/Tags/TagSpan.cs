using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// A package tag found by the <see cref="TextFX"/> preprocessor and removed from the text.
    /// <see cref="Start"/>/<see cref="End"/> are indices in the preprocessed string, which is what
    /// <c>TMP_CharacterInfo.index</c> refers to. Point tags (&lt;p=0.5&gt;) have Start == End.
    /// </summary>
    public readonly struct TagSpan
    {
        public readonly string Name;
        /// <summary>Raw arguments after the name, e.g. "=10" or "a=10 s=2".</summary>
        public readonly string Args;
        public readonly int Start;
        public readonly int End;
        public readonly bool IsPoint;

        public TagSpan(string name, string args, int start, int end, bool isPoint)
        {
            Name = name;
            Args = args;
            Start = start;
            End = end;
            IsPoint = isPoint;
        }

        internal TagSpan WithEnd(int end) => new TagSpan(Name, Args, Start, end, IsPoint);
    }

    /// <summary>
    /// Parsed tag arguments. Supports a main value (&lt;wave=10&gt;) and named values
    /// (&lt;wave a=10 s=2&gt;, quotes optional). Parsed once when the text is rebuilt.
    /// </summary>
    public sealed class TagArgs
    {
        public static readonly TagArgs Empty = new TagArgs(Array.Empty<string>(), Array.Empty<string>());

        readonly string[] _keys;
        readonly string[] _values;
        // Parsed once here, so effects can read args every frame without string parsing.
        readonly float[] _floats;
        readonly bool[] _hasFloat;
        readonly Color[] _colors;
        readonly bool[] _hasColor;

        TagArgs(string[] keys, string[] values)
        {
            _keys = keys;
            _values = values;
            int n = keys.Length;
            _floats = new float[n];
            _hasFloat = new bool[n];
            _colors = new Color[n];
            _hasColor = new bool[n];
            for (int i = 0; i < n; i++)
            {
                _hasFloat[i] = float.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _floats[i]);
                _hasColor[i] = ColorUtility.TryParseHtmlString(values[i], out _colors[i]);
            }
        }

        int IndexOf(string key)
        {
            for (int i = 0; i < _keys.Length; i++)
                if (string.Equals(_keys[i], key, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        public int Count => _keys.Length;

        public static TagArgs Parse(string args)
        {
            if (string.IsNullOrEmpty(args)) return Empty;
            var keys = new List<string>();
            var values = new List<string>();

            int i = 0;
            while (i < args.Length)
            {
                while (i < args.Length && (args[i] == ' ' || args[i] == ',')) i++;
                if (i >= args.Length) break;

                int keyStart = i;
                while (i < args.Length && args[i] != '=' && args[i] != ' ') i++;
                string key = args.Substring(keyStart, i - keyStart);

                string value = "";
                if (i < args.Length && args[i] == '=')
                {
                    i++;
                    bool quoted = i < args.Length && (args[i] == '"' || args[i] == '\'');
                    char quote = quoted ? args[i++] : ' ';
                    int valueStart = i;
                    while (i < args.Length && (quoted ? args[i] != quote : args[i] != ' ')) i++;
                    value = args.Substring(valueStart, i - valueStart);
                    if (quoted && i < args.Length) i++;
                }

                keys.Add(key);
                values.Add(value);
            }
            return keys.Count == 0 ? Empty : new TagArgs(keys.ToArray(), values.ToArray());
        }

        /// <summary>Value of <paramref name="key"/>; "" is the main value (&lt;tag=value&gt;).</summary>
        public bool TryGet(string key, out string value)
        {
            for (int i = 0; i < _keys.Length; i++)
            {
                if (!string.Equals(_keys[i], key, StringComparison.OrdinalIgnoreCase)) continue;
                value = _values[i];
                return true;
            }
            value = null;
            return false;
        }

        public float GetFloat(string key, float fallback)
        {
            int i = IndexOf(key);
            return i >= 0 && _hasFloat[i] ? _floats[i] : fallback;
        }

        /// <summary>First of the keys that is present, else <paramref name="fallback"/>.</summary>
        public float GetFloat(string key, string alias, float fallback) =>
            GetFloat(key, GetFloat(alias, fallback));

        /// <summary>HTML color ("red", "#ff0000", "#f00c") of <paramref name="key"/>, else <paramref name="fallback"/>.</summary>
        public Color GetColor(string key, Color fallback)
        {
            int i = IndexOf(key);
            return i >= 0 && _hasColor[i] ? _colors[i] : fallback;
        }

        /// <summary>First of the keys that is present, else <paramref name="fallback"/>.</summary>
        public Color GetColor(string key, string alias, Color fallback) =>
            GetColor(key, GetColor(alias, fallback));
    }
}
