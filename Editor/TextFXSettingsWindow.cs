using System.IO;
using UnityEditor;
using UnityEngine;

namespace com.mjgame.nmd.nkl.editor
{
    /// <summary>Tools › com.mjgame.nmd.nkl › Text › Settings: the important project-wide settings in one place.</summary>
    sealed class TextFXSettingsWindow : EditorWindow
    {
        const string DefaultAssetPath = "Assets/Resources/" + TextFXSettings.ResourcePath + ".asset";

        TextFXSettings _asset;
        SerializedObject _so;

        [MenuItem("Tools/com.mjgame.nmd.nkl/Text/Settings...", priority = 0)]
        static void Open()
        {
            var window = GetWindow<TextFXSettingsWindow>(true, "MJGame Text Settings", true);
            window.minSize = new Vector2(420f, 360f);
        }

        void OnEnable() => FindAsset();

        void FindAsset()
        {
            _asset = Resources.Load<TextFXSettings>(TextFXSettings.ResourcePath);
            _so = _asset != null ? new SerializedObject(_asset) : null;
        }

        static readonly int[] RateValues = { 0, 60, 30, 20, 15 };
        static readonly string[] RateNames = { "Every frame", "60 per second", "30 per second", "20 per second", "15 per second" };

        Vector2 _scroll;
        GUIStyle _hint;

        GUIStyle Hint => _hint ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true };

        void OnGUI()
        {
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Max(170f, position.width * 0.42f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            try
            {
                DrawContent();
            }
            finally
            {
                EditorGUILayout.EndScrollView();
                EditorGUIUtility.labelWidth = labelWidth;
            }
        }

        void DrawContent()
        {
            GUILayout.Space(6f);
            Section("Editor", "Settings of this computer only.", () =>
            {
                EditorGUI.BeginChangeCheck();
                bool preview = EditorGUILayout.Toggle(
                    new GUIContent("Auto Preview", "Effects with Play On Enable also play in edit mode (Typewriter excluded). Saved per user."),
                    TextEffect.AutoPreview);
                if (EditorGUI.EndChangeCheck()) AutoPreviewMenu.Set(preview);
            });

            if (_so == null || _asset == null)
            {
                EditorGUILayout.HelpBox(
                    "The settings asset is missing (it ships in the package's Runtime/Resources): the defaults are used " +
                    "(UI unscaled time ×1, World timeScale ×0.1).\nCreate it again to change them for the whole project, builds included.",
                    MessageType.Info);
                if (GUILayout.Button("Create Settings Asset")) CreateAsset();
                return;
            }

            _so.Update();

            Section("Pause", "Texts that stop when the game sets Time.timeScale = 0.", () =>
            {
                Field("uiUseTimeScale", "UI texts", "TextMeshProUGUI. Off = keeps animating in pause menus.");
                Field("worldUseTimeScale", "World texts (3D)", "TextMeshPro on objects: damage numbers, names...");
            });

            Section("Performance", "How often looping effects (wave, rainbow, shine...) are redrawn in play mode. " +
                                   "30 per second looks smooth for text and halves their CPU cost in a 60 fps game. " +
                                   "Keep <b>Every frame</b> if the game already runs at 30 fps.", () =>
            {
                RatePopup(_so.FindProperty("maxUpdatesPerSecond"), new GUIContent("Update rate"));
            });

            Section("Distance scale", "Multiplier of distances (wave height, shake, bounce rise, fade slide). " +
                                      "TMP builds 3D text 10× smaller than UI text, so World is usually 0.1.", () =>
            {
                Field("uiUnitScale", "UI texts ×", null);
                Field("worldUnitScale", "World texts ×", null);
            });

            Section("Motion", "Strength of moving effects. 0 = no movement, colors and fades still play. " +
                              "For a <b>Reduce motion</b> option in game call TextFXSettings.SetPlayerMotionScale().", () =>
            {
                Field("motionScale", "Strength", null);
            });

            Section("Per quality level", "Different values for some levels of Project Settings › Quality, e.g. Low: motion 0.5, 30 per second. " +
                                         "Levels not listed use the values above.", DrawQualityOverrides);

            _so.ApplyModifiedProperties();

            GUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(_asset, typeof(TextFXSettings), false);
                if (GUILayout.Button("Reset to Defaults", GUILayout.Width(130f))) ResetToDefaults();
            }
            GUILayout.Space(6f);
        }

        void Section(string title, string hint, System.Action body)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                if (!string.IsNullOrEmpty(hint)) GUILayout.Label(hint, Hint);
                GUILayout.Space(2f);
                body();
                GUILayout.Space(2f);
            }
            GUILayout.Space(4f);
        }

        static void RatePopup(SerializedProperty property, GUIContent label)
        {
            int value = property.intValue;
            int index = System.Array.IndexOf(RateValues, value);
            string[] names = RateNames;
            int[] values = RateValues;
            if (index < 0) // a custom value set from code: keep it selectable
            {
                names = new string[RateNames.Length + 1];
                values = new int[RateValues.Length + 1];
                RateNames.CopyTo(names, 0);
                RateValues.CopyTo(values, 0);
                names[names.Length - 1] = value + " per second";
                values[values.Length - 1] = value;
            }
            property.intValue = EditorGUILayout.IntPopup(label.text, value, names, values);
        }

        void DrawQualityOverrides()
        {
            SerializedProperty list = _so.FindProperty("qualityOverrides");
            string[] levels = QualitySettings.names;
            int current = QualitySettings.GetQualityLevel();

            if (list.arraySize == 0)
                EditorGUILayout.LabelField("None: every quality level uses the values above.", EditorStyles.miniLabel);

            int remove = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty item = list.GetArrayElementAtIndex(i);
                SerializedProperty level = item.FindPropertyRelative("qualityLevel");
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var options = new string[levels.Length];
                        for (int l = 0; l < levels.Length; l++) options[l] = levels[l] + (l == current ? "  (current)" : "");
                        level.intValue = EditorGUILayout.Popup(level.intValue, options);
                        if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(60f))) remove = i;
                    }
                    EditorGUILayout.PropertyField(item.FindPropertyRelative("motionScale"), new GUIContent("Motion strength"));
                    RatePopup(item.FindPropertyRelative("maxUpdatesPerSecond"), new GUIContent("Update rate"));
                }
            }
            if (remove >= 0) list.DeleteArrayElementAtIndex(remove);

            if (GUILayout.Button("Add quality level"))
            {
                int index = list.arraySize;
                list.InsertArrayElementAtIndex(index);
                SerializedProperty item = list.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("qualityLevel").intValue = 0;
                item.FindPropertyRelative("motionScale").floatValue = 1f;
                item.FindPropertyRelative("maxUpdatesPerSecond").intValue = 30;
            }
        }
        void ResetToDefaults()
        {
            AutoPreviewMenu.Set(true);
            Undo.RecordObject(_asset, "Reset Text Settings");
            var defaults = CreateInstance<TextFXSettings>();
            EditorUtility.CopySerialized(defaults, _asset);
            DestroyImmediate(defaults);
            _asset.name = "TextFXSettings"; // CopySerialized also copies the name
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
            _so = new SerializedObject(_asset);
        }

        void Field(string property, string label, string tooltip) =>
            EditorGUILayout.PropertyField(_so.FindProperty(property), new GUIContent(label, tooltip ?? _so.FindProperty(property).tooltip));

        /// <summary>
        /// Next to the package's other resources (Runtime/Resources/...), unless the package is read-only
        /// (installed from a registry or git): then in the project's Assets/Resources.
        /// </summary>
        static string NewAssetPath()
        {
            var instance = CreateInstance<TextFXSettings>();
            string script = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(instance));
            DestroyImmediate(instance);
            if (string.IsNullOrEmpty(script)) return DefaultAssetPath;

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(script);
            bool readOnly = package != null &&
                            package.source != UnityEditor.PackageManager.PackageSource.Embedded &&
                            package.source != UnityEditor.PackageManager.PackageSource.Local;
            if (readOnly) return DefaultAssetPath;
            return Path.GetDirectoryName(script).Replace('\\', '/') + "/Resources/" + TextFXSettings.ResourcePath + ".asset";
        }

        void CreateAsset()
        {
            string path = NewAssetPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(CreateInstance<TextFXSettings>(), path);
            AssetDatabase.SaveAssets();
            TextFXSettings.Set(null); // the runtime picks up the new asset
            FindAsset();
            EditorGUIUtility.PingObject(_asset);
        }
    }
}
