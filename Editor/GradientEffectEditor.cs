using UnityEditor;

namespace com.nmd.nkl.text.editor
{
    /// <summary>
    /// Shows only the gradient that is actually used: the preset's (editable in place) when a preset is
    /// assigned, otherwise the local one. Also hides the angle unless the direction needs it.
    /// </summary>
    [CustomEditor(typeof(GradientEffect))]
    [CanEditMultipleObjects]
    sealed class GradientEffectEditor : TextEffectEditor
    {
        protected override void DrawFields()
        {
            serializedObject.Update();
            SerializedProperty preset = serializedObject.FindProperty(nameof(GradientEffect.preset));
            SerializedProperty direction = serializedObject.FindProperty(nameof(GradientEffect.direction));

            SerializedProperty it = serializedObject.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
            {
                switch (it.name)
                {
                    case "m_Script":
                        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(it);
                        break;
                    case nameof(GradientEffect.gradient):
                        DrawGradient(it, preset);
                        break;
                    case nameof(GradientEffect.angle):
                        if (direction.hasMultipleDifferentValues ||
                            direction.enumValueIndex == (int)GradientEffect.Direction.Angle)
                            EditorGUILayout.PropertyField(it);
                        break;
                    default:
                        EditorGUILayout.PropertyField(it, true);
                        break;
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        static void DrawGradient(SerializedProperty local, SerializedProperty preset)
        {
            if (preset.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox("Selected effects use different presets.", MessageType.None);
                return;
            }

            var asset = preset.objectReferenceValue as TextGradient;
            if (asset == null)
            {
                EditorGUILayout.PropertyField(local);
                return;
            }

            // Edit the shared asset in place; every text using this preset follows.
            var so = new SerializedObject(asset);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty(nameof(TextGradient.gradient)),
                new UnityEngine.GUIContent("Preset Gradient"));
            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(asset);
            EditorGUILayout.HelpBox($"Editing the shared preset '{asset.name}': every text using it changes. " +
                                    "Clear Preset to use a gradient of this text only.", MessageType.None);
        }
    }
}
