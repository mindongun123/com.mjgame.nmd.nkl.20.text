using UnityEditor;
using UnityEngine;

namespace com.mjgame.nmd.nkl.editor
{
    /// <summary>
    /// An effect added in the inspector starts with its built-in tag (Wave → "wave"), so it only animates
    /// &lt;wave&gt; parts and overrides the built-in &lt;wave&gt; for this text. Effects added from code keep an empty tag.
    /// </summary>
    [InitializeOnLoad]
    static class TextEffectDefaults
    {
        static TextEffectDefaults()
        {
            ObjectFactory.componentWasAdded -= OnComponentAdded;
            ObjectFactory.componentWasAdded += OnComponentAdded;
            // Edits to the presets prefab apply to texts that use a built-in tag from then on.
            UnityEditor.SceneManagement.PrefabStage.prefabSaved -= OnPrefabSaved;
            UnityEditor.SceneManagement.PrefabStage.prefabSaved += OnPrefabSaved;
        }

        static void OnPrefabSaved(GameObject prefab) => TextFXPresets.Reload();

        static void OnComponentAdded(Component component)
        {
            if (!(component is TextEffect effect) || !string.IsNullOrEmpty(effect.TagName) ||
                string.IsNullOrEmpty(effect.DefaultTag)) return;
            var so = new SerializedObject(effect);
            so.FindProperty("_tag").stringValue = effect.DefaultTag;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
