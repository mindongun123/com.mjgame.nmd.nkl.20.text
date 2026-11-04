using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace com.mjgame.nmd.nkl.editor
{
    /// <summary>
    /// Live preview of every built-in tag and preset (Tools › com.mjgame.nmd.nkl › Text › Effect Gallery).
    /// Each row renders a hidden TextMeshPro in a preview scene; nothing is added to the open scene.
    /// Copy puts a ready-to-paste tag string on the clipboard; Apply writes it into the selected text.
    /// </summary>
    sealed class TextFXGalleryWindow : EditorWindow
    {
        const float RowHeight = 64f;
        const float RowSpacing = 4f;       // world units between preview texts
        const float ReplayEvery = 2.5f;    // seconds, for effects that play once

        sealed class Row
        {
            public string Tag;
            public string TypeName;
            public string Sample;
            public TextMeshPro Text;
            public TextFX Fx;
            public float Y;
        }

        readonly List<Row> _rows = new List<Row>();
        PreviewRenderUtility _preview;
        Vector2 _scroll;
        string _word = "Sample Text";
        double _lastReplay;
        double _lastRepaint;

        [MenuItem("Tools/com.mjgame.nmd.nkl/Text/Effect Gallery...", priority = 1)]
        static void Open() => GetWindow<TextFXGalleryWindow>("Text FX Gallery");

        void OnEnable()
        {
            minSize = new Vector2(420f, 300f);
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Cleanup();
        }

        void OnEditorUpdate()
        {
            if (_rows.Count == 0) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastReplay > ReplayEvery)
            {
                _lastReplay = now;
                foreach (Row row in _rows)
                    if (row.Fx != null && !row.Fx.IsPlaying) row.Fx.PlayAll();
            }
            if (now - _lastRepaint > 1.0 / 30.0)
            {
                _lastRepaint = now;
                Repaint();
            }
        }

        void Build()
        {
            Cleanup();
            _preview = new PreviewRenderUtility();
            Camera cam = _preview.camera;
            cam.orthographic = true;
            cam.orthographicSize = 1.5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);

            IReadOnlyList<TextFXPresets.Entry> entries = TextFXPresets.All;
            for (int i = 0; i < entries.Count; i++)
            {
                TextFXPresets.Entry entry = entries[i];
                var go = new GameObject("Gallery " + entry.Name) { hideFlags = HideFlags.HideAndDontSave };
                _preview.AddSingleGO(go);
                go.transform.position = new Vector3(0f, -i * RowSpacing, 0f);

                var text = go.AddComponent<TextMeshPro>();
                text.fontSize = 12f;
                text.color = new Color(0.72f, 0.75f, 0.82f); // not pure white, so <shine> and tints show
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = false;
                text.rectTransform.sizeDelta = new Vector2(20f, 3f);
                string sample = "<" + entry.Name + ">" + _word + "</" + entry.Name + ">";
                text.text = sample;

                var fx = go.AddComponent<TextFX>();
                text.ForceMeshUpdate();
                fx.PlayAll();

                _rows.Add(new Row
                {
                    Tag = entry.Name,
                    TypeName = ObjectNames.NicifyVariableName(entry.Type.Name),
                    Sample = sample,
                    Text = text,
                    Fx = fx,
                    Y = -i * RowSpacing,
                });
            }
            _lastReplay = EditorApplication.timeSinceStartup;
        }

        void Cleanup()
        {
            foreach (Row row in _rows)
                if (row.Text != null) DestroyImmediate(row.Text.gameObject);
            _rows.Clear();
            if (_preview != null) _preview.Cleanup();
            _preview = null;
        }

        void OnGUI()
        {
            if (_preview == null) Build();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                _word = EditorGUILayout.TextField(_word, EditorStyles.toolbarTextField, GUILayout.MinWidth(120f));
                if (EditorGUI.EndChangeCheck()) Build();
                if (GUILayout.Button("Replay", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    foreach (Row row in _rows) row.Fx.PlayAll();
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    TextFXPresets.Reload();
                    Build();
                }
            }

            EditorGUILayout.HelpBox("Copy puts the tag on the clipboard. Apply writes it into the selected text " +
                                    "(adds Text FX if needed). Presets come from Resources/TextFXPresets.", MessageType.None);

            TMP_Text selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<TMP_Text>() : null;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (Row row in _rows)
            {
                if (row.Fx == null) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(150f)))
                    {
                        EditorGUILayout.LabelField("<" + row.Tag + ">", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(row.TypeName, EditorStyles.miniLabel);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("Copy", EditorStyles.miniButtonLeft))
                            {
                                EditorGUIUtility.systemCopyBuffer = row.Sample;
                                ShowNotification(new GUIContent("Copied " + row.Sample), 1.0);
                            }
                            using (new EditorGUI.DisabledScope(selected == null))
                                if (GUILayout.Button(new GUIContent("Apply", "Write this tag into the selected TMP text"), EditorStyles.miniButtonRight))
                                    ApplyTo(selected, row.Sample);
                        }
                    }

                    Rect rect = GUILayoutUtility.GetRect(100f, RowHeight, GUILayout.ExpandWidth(true));
                    if (Event.current.type == EventType.Repaint) DrawPreview(rect, row);
                }
                GUILayout.Space(2f);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawPreview(Rect rect, Row row)
        {
            _preview.BeginPreview(rect, GUIStyle.none);
            Camera cam = _preview.camera;
            cam.transform.position = new Vector3(0f, row.Y, -10f);
            cam.transform.rotation = Quaternion.identity;
            // Fit the text width when the row is narrow.
            float aspect = rect.width / Mathf.Max(1f, rect.height);
            float textWidth = row.Text.textBounds.size.x + 2f;
            cam.orthographicSize = Mathf.Max(1.5f, textWidth / aspect * 0.5f);
            cam.Render();
            _preview.EndAndDrawPreview(rect);
        }

        static void ApplyTo(TMP_Text text, string sample)
        {
            Undo.RecordObject(text, "Apply Text FX Tag");
            text.text = sample;
            if (text.GetComponent<TextFX>() == null) Undo.AddComponent<TextFX>(text.gameObject);
            EditorUtility.SetDirty(text);
            TextFX fx = text.GetComponent<TextFX>();
            fx.Reparse();
            fx.PlayAll();
        }
    }
}
