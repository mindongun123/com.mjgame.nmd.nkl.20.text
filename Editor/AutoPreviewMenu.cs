using UnityEngine;

namespace com.mjgame.nmd.nkl.editor
{
    /// <summary>Auto Preview (toggled in the Settings window): play effects in edit mode without pressing Preview.</summary>
    static class AutoPreviewMenu
    {
        /// <summary>Changes the setting and starts/stops the effects already in the open scenes.</summary>
        internal static void Set(bool on)
        {
            TextEffect.AutoPreview = on;
            if (Application.isPlaying) return;
            foreach (TextEffect effect in Object.FindObjectsOfType<TextEffect>())
            {
                if (!effect.IsAnimated || !effect.isActiveAndEnabled || effect is TypewriterEffect) continue;
                if (on) { if (effect.PlayOnEnable) effect.Play(); }
                else effect.Stop();
            }
        }
    }
}
