using UnityEditor;
using UnityEngine;

namespace com.nmd.nkl.text.editor
{
    /// <summary>Tools › com.nmd.nkl › Text › Auto Preview: play effects in edit mode without pressing Preview.</summary>
    static class AutoPreviewMenu
    {
        const string MenuPath = "Tools/com.nmd.nkl/Text/Auto Preview";

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            TextEffect.AutoPreview = !TextEffect.AutoPreview;
            if (Application.isPlaying) return;
            foreach (TextEffect effect in Object.FindObjectsOfType<TextEffect>())
            {
                if (!effect.IsAnimated || !effect.isActiveAndEnabled || effect is TypewriterEffect) continue;
                if (TextEffect.AutoPreview) { if (effect.PlayOnEnable) effect.Play(); }
                else effect.Stop();
            }
        }

        [MenuItem(MenuPath, true)]
        static bool Validate()
        {
            Menu.SetChecked(MenuPath, TextEffect.AutoPreview);
            return true;
        }
    }
}
