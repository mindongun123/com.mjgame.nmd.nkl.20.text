using UnityEditor;

namespace com.mjgame.nmd.nkl.editor
{
    /// <summary>Shows only the size field in use (Angle or Radius) and the resulting radius.</summary>
    [CustomEditor(typeof(ArcLayout))]
    [CanEditMultipleObjects]
    sealed class ArcLayoutEditor : TextEffectEditor
    {
        protected override void DrawFields()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_playOnEnable"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_anchor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_rotateCharacters"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_direction"));

            SerializedProperty sizeBy = serializedObject.FindProperty("_sizeBy");
            EditorGUILayout.PropertyField(sizeBy);
            bool byRadius = !sizeBy.hasMultipleDifferentValues && sizeBy.enumValueIndex == (int)ArcLayout.Size.Radius;
            EditorGUILayout.PropertyField(serializedObject.FindProperty(byRadius ? "_radius" : "_angle"));
            DrawSizeCurve(serializedObject);
            serializedObject.ApplyModifiedProperties();

            if (targets.Length == 1)
            {
                var arc = (ArcLayout)target;
                string info = byRadius
                    ? "Fixed circle: longer texts wrap further around it. Bigger radius = flatter."
                    : $"The text always covers this angle. Radius now ≈ {arc.CurrentRadius:0}.";
                EditorGUILayout.HelpBox(info, MessageType.None);
            }
        }

        /// <summary>Size curve fields shared by every layout; the curve shows only when enabled.</summary>
        internal static void DrawSizeCurve(SerializedObject so)
        {
            SerializedProperty use = so.FindProperty("_useSizeCurve");
            EditorGUILayout.PropertyField(use, new UnityEngine.GUIContent("Size Curve", use.tooltip));
            if (!use.boolValue && !use.hasMultipleDifferentValues) return;
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(so.FindProperty("_sizeCurve"), new UnityEngine.GUIContent("Curve", "x 0..1 = left..right of the text, y = scale (1 = normal)."));
                EditorGUILayout.PropertyField(so.FindProperty("_sizeAlign"), new UnityEngine.GUIContent("Align", so.FindProperty("_sizeAlign").tooltip));
                EditorGUILayout.PropertyField(so.FindProperty("_keepSpacing"));
            }
        }
    }
}
