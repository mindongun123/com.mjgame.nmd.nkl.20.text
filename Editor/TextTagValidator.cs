using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.mjgame.nmd.nkl.editor
{
    /// <summary>
    /// Finds tags in a text that neither TMP nor this package understands (typos such as &lt;wavee&gt;).
    /// TMP prints unknown tags as plain text, so they are easy to miss in game.
    /// </summary>
    static class TextTagValidator
    {
        // TMP rich text tags (TMP 3.x). Unknown tags are printed as text by TMP.
        static readonly HashSet<string> TmpTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "align", "allcaps", "alpha", "b", "br", "color", "cspace", "font", "font-weight", "fontweight",
            "gradient", "i", "indent", "line-height", "line-indent", "link", "lowercase", "margin", "margin-left",
            "margin-right", "mark", "material", "mspace", "nobr", "noparse", "page", "pos", "rotate", "s", "size",
            "smallcaps", "space", "sprite", "strikethrough", "style", "sub", "sup", "u", "uppercase", "voffset",
            "width", "zwj", "zwsp", "nbsp", "shy", "cr", "lf", "action", "class", "table", "tr", "th", "td",
        };

        static readonly string[] TypewriterTags = { "p", "pause", "speed", "ev", "event" };

        public struct Issue
        {
            public string Tag;
            public string Hint;
        }

        /// <summary>Unknown tags of <paramref name="text"/>, each reported once.</summary>
        public static List<Issue> Find(string text, GameObject owner)
        {
            var issues = new List<Issue>();
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0) return issues;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool hasTypewriter = owner != null && owner.GetComponent<TypewriterEffect>() != null;
            TextEffect[] effects = owner != null ? owner.GetComponents<TextEffect>() : Array.Empty<TextEffect>();

            int i = 0;
            bool noparse = false;
            while (i < text.Length)
            {
                int open = text.IndexOf('<', i);
                if (open < 0) break;
                int close = text.IndexOf('>', open + 1);
                if (close < 0) break;
                i = close + 1;

                int start = open + 1;
                bool closing = start < close && text[start] == '/';
                if (closing) start++;
                int end = start;
                while (end < close && text[end] != ' ' && text[end] != '=') end++;
                if (end == start) continue;
                string name = text.Substring(start, end - start);

                if (string.Equals(name, "noparse", StringComparison.OrdinalIgnoreCase)) { noparse = !closing; continue; }
                if (noparse) continue;
                if (name[0] == '#' || name[0] == '"') continue; // <#ff0000> color shorthand
                if (IsKnown(name, effects)) continue;
                if (!seen.Add(name)) continue;

                string hint = null;
                if (Array.IndexOf(TypewriterTags, name.ToLowerInvariant()) >= 0 && !hasTypewriter)
                    hint = "only works on a text with a Typewriter effect";
                else
                {
                    string close1 = Closest(name, effects);
                    if (close1 != null) hint = "did you mean <" + close1 + ">?";
                }
                if (Array.IndexOf(TypewriterTags, name.ToLowerInvariant()) >= 0 && hasTypewriter) continue;
                issues.Add(new Issue { Tag = (closing ? "</" : "<") + name + ">", Hint = hint });
            }
            return issues;
        }

        static bool IsKnown(string name, TextEffect[] effects)
        {
            if (TmpTags.Contains(name)) return true;
            if (string.Equals(name, TextFX.NoFxTag, StringComparison.OrdinalIgnoreCase)) return true;
            if (TextFXPresets.Contains(name)) return true;
            foreach (TextEffect effect in effects)
                if (effect != null && string.Equals(effect.TagName, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Nearest known tag by edit distance (at most 2 edits), for the "did you mean" hint.
        static string Closest(string name, TextEffect[] effects)
        {
            string best = null;
            int bestDistance = 3;
            void Try(string candidate)
            {
                if (string.IsNullOrEmpty(candidate)) return;
                int d = Distance(name.ToLowerInvariant(), candidate.ToLowerInvariant());
                if (d < bestDistance) { bestDistance = d; best = candidate; }
            }
            foreach (TextFXPresets.Entry entry in TextFXPresets.All) Try(entry.Name);
            foreach (TextEffect effect in effects) if (effect != null) Try(effect.TagName);
            foreach (string tag in TmpTags) if (tag.Length > 2) Try(tag);
            Try(TextFX.NoFxTag);
            return best;
        }

        static int Distance(string a, string b)
        {
            var row = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) row[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                int prev = row[0];
                row[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int temp = row[j];
                    row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), prev + (a[i - 1] == b[j - 1] ? 0 : 1));
                    prev = temp;
                }
            }
            return row[b.Length];
        }
    }
}
