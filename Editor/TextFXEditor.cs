using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace com.mjgame.nmd.nkl.editor
{
    /// <summary>
    /// Play/Stop for all effects, and a warning when looping effects would keep a big canvas rebatching.
    /// </summary>
    [CustomEditor(typeof(TextFX))]
    sealed class TextFXEditor : UnityEditor.Editor
    {
        const int CrowdedCanvasGraphics = 20;

        public override void OnInspectorGUI()
        {
            var fx = (TextFX)target;
            TextEffect[] effects = fx.GetComponents<TextEffect>();
            DrawTagWarnings(fx);

            if (effects.Length == 0)
            {
                EditorGUILayout.HelpBox("No effect yet. Write a built-in tag in the text (<wave>, <gold>, <shine>...) " +
                                        "or add one below.", MessageType.Info);
            }
            else
            {
                DrawEffectList(fx, effects);
                if (fx.IsInUpdateLoop) Repaint(); // only while something animates
                DrawCanvasWarning(fx, effects);
            }

            GUILayout.Space(4f);
            DrawAddButtons(fx, effects);
            GUILayout.Space(4f);
            DrawRemoveButton(fx);
        }

        static GUIStyle _stateStyle;
        static GUIStyle StateStyle => _stateStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };

        void DrawEffectList(TextFX fx, TextEffect[] effects)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Effects on this text", EditorStyles.boldLabel);
                    if (GUILayout.Button(Application.isPlaying ? "Play All" : "Preview All", EditorStyles.miniButtonLeft, GUILayout.Width(80f))) fx.PlayAll();
                    if (GUILayout.Button("Stop All", EditorStyles.miniButtonRight, GUILayout.Width(64f))) fx.StopAll();
                }

                foreach (TextEffect effect in effects)
                {
                    string name = ObjectNames.NicifyVariableName(effect.GetType().Name);
                    string target = string.IsNullOrEmpty(effect.TagName) ? "whole text" : "<" + effect.TagName + ">";
                    string state = effect is TextLayoutEffect ? "layout"
                        : !effect.IsPlaying ? "stopped"
                        : effect.IsAnimated ? "playing" : "applied";
                    if (effect.IsAuto) state = "built-in tag · " + state;

                    Rect row = EditorGUILayout.GetControlRect();
                    float w = row.width;
                    GUI.Label(new Rect(row.x, row.y, w * 0.42f, row.height), name);
                    GUI.Label(new Rect(row.x + w * 0.42f, row.y, w * 0.28f, row.height), target, EditorStyles.miniLabel);
                    using (new EditorGUI.DisabledScope(!effect.IsPlaying))
                        GUI.Label(new Rect(row.x + w * 0.70f, row.y, w * 0.30f, row.height), state, StateStyle);
                }
            }
        }

        static readonly (string Group, (string Label, System.Type Type)[] Items)[] AddableEffects =
        {
            ("Motion", new[] { ("Wave", typeof(WaveEffect)), ("Shake", typeof(ShakeEffect)), ("Pulse", typeof(PulseEffect)), ("Wobble", typeof(WobbleEffect)) }),
            ("Color", new[] { ("Gradient", typeof(GradientEffect)), ("Rainbow", typeof(RainbowEffect)), ("Flash", typeof(ColorFlashEffect)), ("Shine", typeof(ShineEffect)) }),
            ("Appear", new[] { ("Bounce In", typeof(BounceInEffect)), ("Fade In", typeof(FadeInEffect)), ("Typewriter", typeof(TypewriterEffect)), ("Fade Out", typeof(FadeOutEffect)) }),
            ("Layout", new[] { ("Arc", typeof(ArcLayout)), ("Curve", typeof(CurveLayout)), ("Line", typeof(LineLayout)) }),
        };

        // Adds an editable effect with its built-in tag: <wave> in this text then uses its values.
        static void DrawAddButtons(TextFX fx, TextEffect[] effects)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(new GUIContent("Add effect",
                    "An effect added here overrides the built-in tag of the same name on this text only."), EditorStyles.boldLabel);

                const int columns = 4;
                foreach (var (group, items) in AddableEffects)
                {
                    Rect line = EditorGUILayout.GetControlRect();
                    float labelWidth = 58f;
                    GUI.Label(new Rect(line.x, line.y, labelWidth, line.height), group, EditorStyles.miniBoldLabel);
                    float cell = (line.width - labelWidth) / columns;
                    for (int i = 0; i < items.Length; i++)
                    {
                        (string label, System.Type type) = items[i];
                        var rect = new Rect(line.x + labelWidth + i * cell, line.y, cell - 2f, line.height);
                        bool exists = HasManual(effects, type);
                        using (new EditorGUI.DisabledScope(exists))
                        {
                            var content = new GUIContent(label, exists ? "Already on this text." : "Add " + label);
                            if (GUI.Button(rect, content, EditorStyles.miniButton)) AddEffect(fx, type);
                        }
                    }
                }
            }
        }
        static bool HasManual(TextEffect[] effects, System.Type type)
        {
            foreach (TextEffect effect in effects)
                if (!effect.IsAuto && effect.GetType() == type) return true;
            return false;
        }

        static void AddEffect(TextFX fx, System.Type type)
        {
            var effect = (TextEffect)Undo.AddComponent(fx.gameObject, type);
            var so = new SerializedObject(effect);
            so.FindProperty("_tag").stringValue = effect.DefaultTag;
            so.ApplyModifiedPropertiesWithoutUndo();

            // The hidden copy for the same tag is no longer needed.
            foreach (TextEffect other in fx.GetComponents<TextEffect>())
                if (other.IsAuto && string.Equals(other.TagName, effect.TagName, System.StringComparison.OrdinalIgnoreCase))
                    Object.DestroyImmediate(other);
            fx.Reparse();
            GUIUtility.ExitGUI();
        }

        // Unity refuses to remove TextFX while effects require it, including the hidden built-in tag ones.
        static void DrawRemoveButton(TextFX fx)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (!GUILayout.Button(new GUIContent("Remove Text FX", "Removes Text FX and all its effects, including hidden built-in tag ones."), EditorStyles.miniButton, GUILayout.Width(120f))) return;
            }
            RemoveWithEffects(fx);
            GUIUtility.ExitGUI();
        }

        [MenuItem("CONTEXT/TextFX/Remove Text FX and its effects")]
        static void RemoveFromContextMenu(MenuCommand command) => RemoveWithEffects((TextFX)command.context);

        static void RemoveWithEffects(TextFX fx)
        {
            int group = Undo.GetCurrentGroup();
            foreach (TextEffect effect in fx.GetComponents<TextEffect>())
            {
                if (effect.IsAuto) Object.DestroyImmediate(effect); // never saved, nothing to undo
                else Undo.DestroyObjectImmediate(effect);
            }
            Undo.DestroyObjectImmediate(fx);
            Undo.CollapseUndoOperations(group);
        }

        // Validation result cached per text: only recomputed when the string or the effects change.
        string _checkedText;
        int _checkedEffects = -1;
        System.Collections.Generic.List<TextTagValidator.Issue> _issues;

        void DrawTagWarnings(TextFX fx)
        {
            if (fx.Text == null) return;
            string text = fx.Text.text;
            int effects = fx.GetComponents<TextEffect>().Length;
            if (_issues == null || !ReferenceEquals(text, _checkedText) || effects != _checkedEffects)
            {
                _issues = TextTagValidator.Find(text, fx.gameObject);
                _checkedText = text;
                _checkedEffects = effects;
            }
            var issues = _issues;
            if (issues.Count == 0) return;
            var message = new System.Text.StringBuilder("Unknown tags, TMP will print them as text:");
            foreach (var issue in issues)
            {
                message.Append("\n  ").Append(issue.Tag);
                if (issue.Hint != null) message.Append("  (").Append(issue.Hint).Append(")");
            }
            EditorGUILayout.HelpBox(message.ToString(), MessageType.Warning);
            EditorGUILayout.Space();
        }

        static void DrawCanvasWarning(TextFX fx, TextEffect[] effects)
        {
            bool looping = false;
            foreach (TextEffect effect in effects)
                looping |= effect.enabled && effect.IsLooping;
            if (!looping) return;

            Canvas canvas = fx.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.gameObject == fx.gameObject) return;

            int graphics = canvas.GetComponentsInChildren<Graphic>(true).Length;
            if (graphics < CrowdedCanvasGraphics) return;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                $"A looping effect updates the mesh every frame, which rebatches the whole canvas '{canvas.name}' ({graphics} graphics). " +
                "Give this text its own sub-canvas so only it is rebatched.",
                MessageType.Warning);
            if (GUILayout.Button("Add sub-Canvas"))
                Undo.AddComponent<Canvas>(fx.gameObject);
        }
    }
}
