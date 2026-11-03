using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace com.nmd.nkl.text
{
    /// <summary>
    /// Single listener on TMP's global TEXT_CHANGED event, routing each change only to the
    /// <see cref="TextFX"/> that watch that text. Without it every awake TextFX would be called
    /// for every text change in the game.
    /// </summary>
    internal static class TextChangeDispatcher
    {
        static readonly Dictionary<int, List<TextFX>> Watchers = new Dictionary<int, List<TextFX>>();
        static readonly Stack<List<TextFX>> Pool = new Stack<List<TextFX>>();
        static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            if (_subscribed) TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            _subscribed = false;
            Watchers.Clear();
        }

        /// <summary>Number of watched texts (for tests and diagnostics).</summary>
        internal static int WatchedCount => Watchers.Count;

        internal static void Watch(Object text, TextFX fx)
        {
            if (text == null) return;
            int id = text.GetInstanceID();
            if (!Watchers.TryGetValue(id, out List<TextFX> list))
            {
                list = Pool.Count > 0 ? Pool.Pop() : new List<TextFX>(1);
                Watchers.Add(id, list);
            }
            if (!list.Contains(fx)) list.Add(fx);

            if (_subscribed) return;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            _subscribed = true;
        }

        internal static void Unwatch(Object text, TextFX fx)
        {
            if (ReferenceEquals(text, null)) return;
            int id = text.GetInstanceID();
            if (!Watchers.TryGetValue(id, out List<TextFX> list)) return;
            list.Remove(fx);
            if (list.Count > 0) return;

            Watchers.Remove(id);
            Pool.Push(list);
            if (Watchers.Count > 0 || !_subscribed) return;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            _subscribed = false;
        }

        static void OnTextChanged(Object obj)
        {
            if (obj == null || !Watchers.TryGetValue(obj.GetInstanceID(), out List<TextFX> list)) return;
            // Backwards: a TextFX may stop watching while handling the change.
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i < list.Count) list[i].OnTextChanged(obj);
            }
        }
    }
}
