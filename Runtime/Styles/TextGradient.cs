using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Shareable gradient asset for <see cref="GradientEffect"/> (gold, fire, ice, rainbow...).
    /// Edit it once and every text using it follows.
    /// </summary>
    [CreateAssetMenu(menuName = "com.mjgame.nmd.nkl/Text/Text Gradient", fileName = "TextGradient")]
    public sealed class TextGradient : ScriptableObject
    {
        public Gradient gradient = GradientEffect.CreateRainbow();

        /// <summary>Raised when the asset is edited in the inspector (editor only).</summary>
        internal static event System.Action<TextGradient> Changed;

#if UNITY_EDITOR
        void OnValidate() => Changed?.Invoke(this);
#endif
    }
}
