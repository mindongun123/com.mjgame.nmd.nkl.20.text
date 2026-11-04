using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Single update loop for the <see cref="TextFX"/> that are currently playing an effect.
    /// Idle texts are not in the list at all. Uses unscaled time so popups keep animating while
    /// the game is paused. In edit mode it ticks from EditorApplication.update for previews.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class TextFXRunner : MonoBehaviour
    {
        const float MaxDeltaTime = 0.1f;

        static readonly ProfilerMarker TickMarker = new ProfilerMarker("NKL.TextFX.Tick");
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

        internal static void Add(TextFX fx)
        {
            if (!Active.Contains(fx)) Active.Add(fx);
            if (!Application.isPlaying || _instance != null) return;

            // DontDestroyOnLoad (not DontSave) so the runner is cleaned up when play mode ends.
            var go = new GameObject("[NKL TextFX]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<TextFXRunner>();
        }

        internal static void Remove(TextFX fx) => Active.Remove(fx);

        static bool TickAll(float deltaTime)
        {
            if (Active.Count == 0) return false;
            using (TickMarker.Auto())
            {
                // Backwards: a text that finishes removes itself from the list during its tick.
                for (int i = Active.Count - 1; i >= 0; i--)
                {
                    if (i >= Active.Count) continue;
                    TextFX fx = Active[i];
                    if (fx == null) Active.RemoveAt(i);
                    else fx.Tick(deltaTime);
                }
            }
            return true;
        }

        void LateUpdate() => TickAll(Mathf.Min(Time.unscaledDeltaTime, MaxDeltaTime));

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
