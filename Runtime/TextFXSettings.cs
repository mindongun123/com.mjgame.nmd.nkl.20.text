using TMPro;
using UnityEngine;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Project-wide settings of the text effects, loaded from Resources/<see cref="ResourcePath"/>.
    /// Without the asset the defaults below are used. Edit it in Tools › com.mjgame.nmd.nkl › Text › Settings.
    /// </summary>
    public sealed class TextFXSettings : ScriptableObject
    {
        public const string ResourcePath = "TextFXSettings";

        [Header("Time")]
        [Tooltip("UI texts (TextMeshProUGUI) follow Time.timeScale: they stop when the game is paused. Off = unscaled time.")]
        public bool uiUseTimeScale;
        [Tooltip("World texts (TextMeshPro on objects) follow Time.timeScale: they stop when the game is paused.")]
        public bool worldUseTimeScale = true;

        [Header("Distance scale")]
        [Tooltip("Multiplier of distances (wave amplitude, bounce rise, shake, fade slide) on UI texts.")]
        public float uiUnitScale = 1f;
        [Tooltip("Multiplier of distances on world texts. TMP builds world text 10x smaller than UI text " +
                 "at the same font size, so 0.1 makes the same settings look the same on both.")]
        public float worldUnitScale = 0.1f;
        [Tooltip("Max effect updates per second in play mode. 0 = every frame. 30 saves CPU/battery on mobile.")]
        [Min(0)] public int maxUpdatesPerSecond;

        static TextFXSettings _instance;

        /// <summary>The settings asset, or defaults when the project has none.</summary>
        public static TextFXSettings Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<TextFXSettings>(ResourcePath);
                if (_instance == null)
                {
                    _instance = CreateInstance<TextFXSettings>();
                    _instance.hideFlags = HideFlags.HideAndDontSave;
                }
                return _instance;
            }
        }

        /// <summary>Uses <paramref name="settings"/> instead of the Resources asset (null = reload it).</summary>
        public static void Set(TextFXSettings settings) => _instance = settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        public static bool IsWorld(TMP_Text text) => !(text is TextMeshProUGUI);

        public bool UsesTimeScale(TMP_Text text) => IsWorld(text) ? worldUseTimeScale : uiUseTimeScale;

        public float UnitScale(TMP_Text text) => IsWorld(text) ? worldUnitScale : uiUnitScale;
    }
}
