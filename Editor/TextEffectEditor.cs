using UnityEditor;
using UnityEngine;

namespace com.nmd.nkl.text.editor
{
    /// <summary>Adds Play / Stop (and Skip for the typewriter) to every effect, also usable in edit mode.</summary>
    [CustomEditor(typeof(TextEffect), true)]
    [CanEditMultipleObjects]
    class TextEffectEditor : UnityEditor.Editor
    {
        /// <summary>Draws the effect's own fields; override for a custom layout.</summary>
        protected virtual void DrawFields() => DrawDefaultInspector();

        public override void OnInspectorGUI()
        {
            DrawFields();
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Application.isPlaying ? "Play" : "Preview"))
                    foreach (Object t in targets) ((TextEffect)t).Play();
                if (GUILayout.Button("Stop"))
                    foreach (Object t in targets) ((TextEffect)t).Stop();
                if (target is TypewriterEffect && GUILayout.Button("Skip"))
                    foreach (Object t in targets) ((TypewriterEffect)t).Skip();
            }

            if (targets.Length == 1)
            {
                var effect = (TextEffect)target;
                string state = effect.IsPlaying ? "Playing" : "Stopped";
                if (effect.IsLooping) state += " (looping)";
                EditorGUILayout.LabelField("State", state);
                if (effect.IsPlaying) Repaint();
            }
        }
    }
}
