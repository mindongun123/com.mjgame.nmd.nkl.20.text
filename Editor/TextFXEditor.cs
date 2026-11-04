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

            if (effects.Length == 0)
            {
                EditorGUILayout.HelpBox("Write a built-in tag in the text (<wave>, <gold>...) or add an effect from " +
                                        "Add Component > com.mjgame.nmd.nkl > Text > Effects.", MessageType.Info);
                DrawAddButtons(fx, effects);
                DrawRemoveButton(fx);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Application.isPlaying ? "Play All" : "Preview All")) fx.PlayAll();
                if (GUILayout.Button("Stop All")) fx.StopAll();
            }

            foreach (TextEffect effect in effects)
            {
                string tag = string.IsNullOrEmpty(effect.TagName) ? "whole text" : "<" + effect.TagName + ">";
                string state = effect.IsPlaying ? "playing" : "stopped";
                if (effect.IsAuto) state += " (built-in tag)";
                EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(effect.GetType().Name), tag + ", " + state);
            }
            if (fx.IsPlaying) Repaint();

            DrawCanvasWarning(fx, effects);
            DrawAddButtons(fx, effects);
            DrawRemoveButton(fx);
        }

        static readonly (string Label, System.Type Type)[] AddableEffects =
        {
            ("Wave", typeof(WaveEffect)), ("Shake", typeof(ShakeEffect)), ("Pulse", typeof(PulseEffect)),
            ("Wobble", typeof(WobbleEffect)), ("Rainbow", typeof(RainbowEffect)), ("Flash", typeof(ColorFlashEffect)),
            ("Bounce", typeof(BounceInEffect)), ("Fade", typeof(FadeInEffect)), ("Gradient", typeof(GradientEffect)),
            ("Typewriter", typeof(TypewriterEffect)),
        };

        // Adds an editable effect with its built-in tag: <wave> in this text then uses its values.
        static void DrawAddButtons(TextFX fx, TextEffect[] effects)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Add effect (overrides the built-in tag on this text)", EditorStyles.miniBoldLabel);
            const int perRow = 3;
            for (int i = 0; i < AddableEffects.Length; i += perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = i; j < i + perRow && j < AddableEffects.Length; j++)
                    {
                        (string label, System.Type type) = AddableEffects[j];
                        using (new EditorGUI.DisabledScope(HasManual(effects, type)))
                            if (GUILayout.Button("Add " + label)) AddEffect(fx, type);
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
            EditorGUILayout.Space();
            if (!GUILayout.Button("Remove Text FX and its effects")) return;
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
