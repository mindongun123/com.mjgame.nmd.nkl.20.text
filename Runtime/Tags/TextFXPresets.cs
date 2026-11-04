using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Built-in tags that work in any text with a <see cref="TextFX"/>, without adding an effect component:
    /// &lt;wave&gt;, &lt;shake&gt;, &lt;pulse&gt;, &lt;wobble&gt;, &lt;rainbow&gt;, &lt;flash&gt;, &lt;bounce&gt;, &lt;fade&gt;, &lt;gradient&gt;,
    /// plus every effect of the presets prefab (<see cref="ResourcePath"/>), which can also define new tags
    /// such as &lt;gold&gt; (a GradientEffect with Tag "gold").
    /// <para>
    /// The first time a text uses such a tag, TextFX adds a hidden, unsaved copy of the effect. An effect
    /// component with the same Tag on the text itself wins, to tune one item only.
    /// </para>
    /// </summary>
    public static class TextFXPresets
    {
        /// <summary>Resources path of the optional presets prefab: a GameObject holding configured effects.</summary>
        public const string ResourcePath = "NKL/TextFXPresets";

        internal readonly struct Entry
        {
            public readonly string Name;
            public readonly Type Type;
            public readonly TextEffect Template;

            public Entry(string name, Type type, TextEffect template)
            {
                Name = name;
                Type = type;
                Template = template;
            }
        }

        static readonly List<Entry> Entries = new List<Entry>();
        static readonly List<TextEffect> Scratch = new List<TextEffect>();
        static GameObject _prefab;
        static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Entries.Clear();
            _prefab = null;
            _loaded = false;
        }

        /// <summary>Uses <paramref name="prefab"/>'s effects as presets instead of the Resources one.</summary>
        public static void SetPresets(GameObject prefab)
        {
            _prefab = prefab;
            Reload();
        }

        /// <summary>Adds or replaces a built-in tag. <paramref name="template"/> (optional) provides its settings.</summary>
        public static void Register(string tag, Type effectType, TextEffect template = null)
        {
            if (string.IsNullOrEmpty(tag)) throw new ArgumentException("Tag is empty.", nameof(tag));
            if (effectType == null || !typeof(TextEffect).IsAssignableFrom(effectType) || effectType.IsAbstract)
                throw new ArgumentException("Not a concrete TextEffect type.", nameof(effectType));
            EnsureLoaded();
            Add(new Entry(tag.ToLowerInvariant(), effectType, template));
        }

        /// <summary>Reloads the presets (after editing the presets prefab).</summary>
        public static void Reload()
        {
            _loaded = false;
            EnsureLoaded();
        }

        internal static bool TryFind(string text, int start, int length, out Entry entry)
        {
            EnsureLoaded();
            for (int i = 0; i < Entries.Count; i++)
            {
                string name = Entries[i].Name;
                if (name.Length != length) continue;
                if (string.Compare(text, start, name, 0, length, StringComparison.OrdinalIgnoreCase) != 0) continue;
                entry = Entries[i];
                return true;
            }
            entry = default;
            return false;
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            Entries.Clear();
            Add(new Entry("wave", typeof(WaveEffect), null));
            Add(new Entry("shake", typeof(ShakeEffect), null));
            Add(new Entry("pulse", typeof(PulseEffect), null));
            Add(new Entry("wobble", typeof(WobbleEffect), null));
            Add(new Entry("rainbow", typeof(RainbowEffect), null));
            Add(new Entry("flash", typeof(ColorFlashEffect), null));
            Add(new Entry("bounce", typeof(BounceInEffect), null));
            Add(new Entry("fade", typeof(FadeInEffect), null));
            Add(new Entry("gradient", typeof(GradientEffect), null));

            GameObject prefab = _prefab != null ? _prefab : Resources.Load<GameObject>(ResourcePath);
            if (prefab == null) return;
            prefab.GetComponents(Scratch);
            for (int i = 0; i < Scratch.Count; i++)
            {
                TextEffect effect = Scratch[i];
                if (!string.IsNullOrEmpty(effect.TagName))
                    Add(new Entry(effect.TagName.ToLowerInvariant(), effect.GetType(), effect));
            }
            Scratch.Clear();
        }

        static void Add(Entry entry)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Name != entry.Name) continue;
                Entries[i] = entry;
                return;
            }
            Entries.Add(entry);
        }
    }
}
