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
            window.minSize = new Vector2(380f, 300f);
        }

        void OnEnable() => FindAsset();

        void FindAsset()
        {
            _asset = Resources.Load<TextFXSettings>(TextFXSettings.ResourcePath);
            _so = _asset != null ? new SerializedObject(_asset) : null;
        }

        void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool preview = EditorGUILayout.Toggle(
                new GUIContent("Auto Preview", "Effects with Play On Enable also play in edit mode (Typewriter excluded). Saved per user."),
                TextEffect.AutoPreview);
            if (EditorGUI.EndChangeCheck()) AutoPreviewMenu.Set(preview);

            EditorGUILayout.Space();
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
            EditorGUILayout.LabelField("Time", EditorStyles.boldLabel);
            Field("uiUseTimeScale", "UI uses Time.timeScale", "On = UI texts stop when the game is paused.");
            Field("worldUseTimeScale", "World uses Time.timeScale", "On = world texts (TextMeshPro on objects) stop when the game is paused.");
            Field("maxUpdatesPerSecond", "Max updates / second", "0 = every frame. 30 halves the CPU cost of looping effects on 60 fps mobile games.");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Distance scale", EditorStyles.boldLabel);
            Field("uiUnitScale", "UI ×", "Multiplier of wave amplitude, bounce rise, shake, fade slide on UI texts.");
            Field("worldUnitScale", "World ×", "TMP builds world text 10x smaller than UI text: 0.1 makes the same settings look alike.");
            _so.ApplyModifiedProperties();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Asset", _asset, typeof(TextFXSettings), false);

            EditorGUILayout.Space();
            if (GUILayout.Button("Reset to Defaults")) ResetToDefaults();
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
            EditorGUILayout.PropertyField(_so.FindProperty(property), new GUIContent(label, tooltip));

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
