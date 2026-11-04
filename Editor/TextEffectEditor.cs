using UnityEditor;
using UnityEngine;

namespace com.mjgame.nmd.nkl.editor
{
    /// <summary>Adds Play / Stop (and Skip for the typewriter) to every effect, also usable in edit mode.</summary>
    [CustomEditor(typeof(TextEffect), true)]
    [CanEditMultipleObjects]
    class TextEffectEditor : UnityEditor.Editor
    {
        protected const string TagProperty = "_tag";

        /// <summary>Draws the effect's own fields (the tag is drawn above them); override for a custom layout.</summary>
        protected virtual void DrawFields()
        {
            serializedObject.Update();
            SerializedProperty it = serializedObject.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
            {
                if (it.name == TagProperty) continue;
                using (new EditorGUI.DisabledScope(it.name == "m_Script")) EditorGUILayout.PropertyField(it, true);
            }
            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            DrawTag();
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

        static readonly GUIContent TagLabel = new GUIContent("Tag",
            "Empty = whole text. Otherwise text inside <tag>...</tag> gets this effect. Using a built-in tag name " +
            "(wave, shake, gold...) overrides that tag for this text only.");

        void DrawTag()
        {
            serializedObject.Update();
            SerializedProperty tag = serializedObject.FindProperty(TagProperty);
            EditorGUILayout.PropertyField(tag, TagLabel);
            if (!tag.hasMultipleDifferentValues)
            {
                string hint = string.IsNullOrEmpty(tag.stringValue)
                    ? "Applies to the whole text."
                    : $"Applies to <{tag.stringValue}>...</{tag.stringValue}> in the text.";
                EditorGUILayout.HelpBox(hint, MessageType.None);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
