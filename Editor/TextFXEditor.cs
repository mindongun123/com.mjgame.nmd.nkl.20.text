using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace com.nmd.nkl.text.editor
{
    /// <summary>
    /// Play/Stop for all effects, and a warning when looping effects would keep a big canvas rebatching.
    /// </summary>
    [CustomEditor(typeof(TextFX))]
    sealed class TextFXEditor : UnityEditor.Editor
    {
        const int CrowdedCanvasGraphics = 20;

        public override void OnInspectorGUI()
        {
            var fx = (TextFX)target;
            TextEffect[] effects = fx.GetComponents<TextEffect>();

            if (effects.Length == 0)
            {
                EditorGUILayout.HelpBox("Add an effect from Add Component > com.nmd.nkl > Text > Effects.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Application.isPlaying ? "Play All" : "Preview All")) fx.PlayAll();
                if (GUILayout.Button("Stop All")) fx.StopAll();
            }

            foreach (TextEffect effect in effects)
            {
                string tag = string.IsNullOrEmpty(effect.TagName) ? "whole text" : "<" + effect.TagName + ">";
                string state = effect.IsPlaying ? "playing" : "stopped";
                EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(effect.GetType().Name), tag + ", " + state);
            }
            if (fx.IsPlaying) Repaint();

            DrawCanvasWarning(fx, effects);
        }

        static void DrawCanvasWarning(TextFX fx, TextEffect[] effects)
        {
            bool looping = false;
            foreach (TextEffect effect in effects)
                looping |= effect.enabled && effect.IsLooping;
            if (!looping) return;

            Canvas canvas = fx.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.gameObject == fx.gameObject) return;

            int graphics = canvas.GetComponentsInChildren<Graphic>(true).Length;
            if (graphics < CrowdedCanvasGraphics) return;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                $"A looping effect updates the mesh every frame, which rebatches the whole canvas '{canvas.name}' ({graphics} graphics). " +
                "Give this text its own sub-canvas so only it is rebatched.",
                MessageType.Warning);
            if (GUILayout.Button("Add sub-Canvas"))
                Undo.AddComponent<Canvas>(fx.gameObject);
        }
    }
}
