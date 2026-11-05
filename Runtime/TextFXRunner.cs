using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Single update loop for the <see cref="TextFX"/> that are currently playing an effect.
    /// Idle texts are not in the list at all. UI texts use unscaled time and world texts follow
    /// Time.timeScale by default (<see cref="TextFXSettings"/>). In edit mode it ticks from
    /// EditorApplication.update for previews (always unscaled).
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class TextFXRunner : MonoBehaviour
    {
        const float MaxDeltaTime = 0.1f;

        static readonly ProfilerMarker TickMarker = new ProfilerMarker("MJGame.TextFX.Tick");
        static readonly List<TextFX> Active = new List<TextFX>();
        static TextFXRunner _instance;

        /// <summary>Number of texts in the update loop (for tests and diagnostics).</summary>
        internal static int ActiveCount => Active.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active.Clear();
            _instance = null;
        }

        // O(1) add/remove: each TextFX keeps its index in Active (100 items starting together stay cheap).
        internal static void Add(TextFX fx)
        {
            int i = fx.RunnerIndex;
            // A stale index (statics reset on entering play mode without domain reload) counts as absent.
            if (i < 0 || i >= Active.Count || !ReferenceEquals(Active[i], fx))
            {
                fx.RunnerIndex = Active.Count;
                Active.Add(fx);
            }
            EnsureInstance();
        }

        internal static void EnsureInstance()
        {
            if (!Application.isPlaying || _instance != null) return;

            // DontDestroyOnLoad (not DontSave) so the runner is cleaned up when play mode ends.
            var go = new GameObject("[MJGame TextFX]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TextFXRunner>();
        }

        internal static void Remove(TextFX fx)
        {
            int i = fx.RunnerIndex;
            if (i < 0 || i >= Active.Count || !ReferenceEquals(Active[i], fx)) return;
            RemoveAt(i);
        }

        /// <summary>Swap-remove. Ticks run backwards, so the moved (last) item was already ticked this frame.</summary>
        static void RemoveAt(int i)
        {
            TextFX removed = Active[i];
            int last = Active.Count - 1;
            if (i != last)
            {
                TextFX moved = Active[last];
                Active[i] = moved;
                if (!ReferenceEquals(moved, null)) moved.RunnerIndex = i;
            }
            Active.RemoveAt(last);
            if (!ReferenceEquals(removed, null)) removed.RunnerIndex = -1;
        }

        static bool TickAll(float deltaTime) => TickAll(deltaTime, deltaTime);

        /// <summary>Each text gets the scaled or unscaled delta depending on <see cref="TextFXSettings"/>.</summary>
        static bool TickAll(float unscaledDelta, float scaledDelta)
        {
            TextFXSettings settings = TextFXSettings.Instance;
            if (Active.Count == 0) return false;
            using (TickMarker.Auto())
            {
                // Backwards: a text that finishes removes itself from the list during its tick.
                for (int i = Active.Count - 1; i >= 0; i--)
                {
                    if (i >= Active.Count) continue;
                    TextFX fx = Active[i];
                    if (fx == null) RemoveAt(i);
                    else fx.Tick(settings.UsesTimeScale(fx.Text) ? scaledDelta : unscaledDelta);
                }
            }
            return true;
        }

        float _pendingUnscaled;
        float _pendingScaled;

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxDeltaTime);
            TextCounter.TickAll(Time.unscaledDeltaTime); // every frame: counters are cheap and only re-render on change
            _pendingUnscaled += dt;
            _pendingScaled += dt * Time.timeScale;
            int maxRate = TextFXSettings.Instance.MaxUpdatesPerSecond;
            // Small tolerance so a 60 fps frame time still triggers a 30/s tick every second frame.
            if (maxRate > 0 && _pendingUnscaled < 1f / maxRate - 0.002f) return;
            TickAll(Mathf.Min(_pendingUnscaled, MaxDeltaTime), Mathf.Min(_pendingScaled, MaxDeltaTime));
            _pendingUnscaled = 0f;
            _pendingScaled = 0f;
        }

#if UNITY_EDITOR
        static double _lastEditorTime;

        [UnityEditor.InitializeOnLoadMethod]
        static void InitEditor()
        {
            UnityEditor.EditorApplication.update -= EditorTick;
            UnityEditor.EditorApplication.update += EditorTick;
        }

        static void EditorTick()
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            float deltaTime = Mathf.Min((float)(now - _lastEditorTime), MaxDeltaTime);
            _lastEditorTime = now;
            if (Application.isPlaying) return;

            if (!TickAll(deltaTime)) return;
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }
}
