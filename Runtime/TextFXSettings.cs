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

        [Tooltip("UI texts (TextMeshProUGUI) follow Time.timeScale: they stop when the game is paused. Off = unscaled time.")]
        public bool uiUseTimeScale;
        [Tooltip("World texts (TextMeshPro on objects) follow Time.timeScale: they stop when the game is paused.")]
        public bool worldUseTimeScale = true;

        [Tooltip("Multiplier of distances (wave amplitude, bounce rise, shake, fade slide) on UI texts.")]
        public float uiUnitScale = 1f;
        [Tooltip("Multiplier of distances on world texts. TMP builds world text 10x smaller than UI text " +
                 "at the same font size, so 0.1 makes the same settings look the same on both.")]
        public float worldUnitScale = 0.1f;
        [Tooltip("Max effect updates per second in play mode. 0 = every frame. 30 saves CPU/battery on mobile.")]
        [Min(0)] public int maxUpdatesPerSecond;

        [Tooltip("Strength of moving effects (wave, shake, wobble, pulse, bounce/fade movement). " +
                 "1 = full, 0 = no movement (colors and fades still play). For reduced-motion players.")]
        [Range(0f, 1f)] public float motionScale = 1f;

        [System.Serializable]
        public struct QualityOverride
        {
            [Tooltip("Index in Project Settings > Quality (0 = first level).")]
            public int qualityLevel;
            [Range(0f, 1f)] public float motionScale;
            [Tooltip("0 = every frame.")]
            [Min(0)] public int maxUpdatesPerSecond;
        }

        [Tooltip("Per quality level values, e.g. Low: motion 0.5 and 30 updates/s. Levels not listed use the values above.")]
        public QualityOverride[] qualityOverrides = new QualityOverride[0];

        static TextFXSettings _instance;
        static float _playerMotionScale = -1f;

        /// <summary>
        /// Player choice from the game's options menu (e.g. a "Reduce motion" toggle: 0 or 0.3).
        /// Wins over the asset and the quality overrides. Pass a negative value to clear it. Not saved.
        /// </summary>
        public static void SetPlayerMotionScale(float scale) => _playerMotionScale = scale < 0f ? -1f : Mathf.Clamp01(scale);

        /// <summary>Strength of moving effects now: player choice, else quality override, else <see cref="motionScale"/>.</summary>
        public float MotionScale
        {
            get
            {
                if (_playerMotionScale >= 0f) return _playerMotionScale;
                return TryGetQualityOverride(out QualityOverride o) ? o.motionScale : motionScale;
            }
        }

        /// <summary>Max updates per second now: quality override, else <see cref="maxUpdatesPerSecond"/>.</summary>
        public int MaxUpdatesPerSecond =>
            TryGetQualityOverride(out QualityOverride o) ? o.maxUpdatesPerSecond : maxUpdatesPerSecond;

        bool TryGetQualityOverride(out QualityOverride result)
        {
            if (qualityOverrides != null && qualityOverrides.Length > 0)
            {
                int level = QualitySettings.GetQualityLevel();
                for (int i = 0; i < qualityOverrides.Length; i++)
                {
                    if (qualityOverrides[i].qualityLevel != level) continue;
                    result = qualityOverrides[i];
                    return true;
                }
            }
            result = default;
            return false;
        }

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
        static void ResetStatics()
        {
            _instance = null;
            _playerMotionScale = -1f;
        }

        public static bool IsWorld(TMP_Text text) => !(text is TextMeshProUGUI);

        public bool UsesTimeScale(TMP_Text text) => IsWorld(text) ? worldUseTimeScale : uiUseTimeScale;

        public float UnitScale(TMP_Text text) => IsWorld(text) ? worldUnitScale : uiUnitScale;
    }
}
