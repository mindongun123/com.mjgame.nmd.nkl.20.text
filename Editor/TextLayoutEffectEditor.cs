using UnityEditor;

namespace com.mjgame.nmd.nkl.editor
{
    /// <summary>Layouts (Curve, Line, custom ones): own fields, then the size curve shown only when enabled.</summary>
    [CustomEditor(typeof(TextLayoutEffect), true)]
    [CanEditMultipleObjects]
    class TextLayoutEffectEditor : TextEffectEditor
    {
        static readonly string[] SizeFields = { "_useSizeCurve", "_sizeCurve", "_sizeAlign", "_keepSpacing" };

        protected override void DrawFields()
        {
            serializedObject.Update();
            SerializedProperty it = serializedObject.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
            {
                if (it.name == TagProperty || System.Array.IndexOf(SizeFields, it.name) >= 0) continue;
                using (new EditorGUI.DisabledScope(it.name == "m_Script")) EditorGUILayout.PropertyField(it, true);
            }
            ArcLayoutEditor.DrawSizeCurve(serializedObject);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
