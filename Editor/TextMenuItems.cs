using TMPro;
using UnityEditor;
using UnityEngine;

namespace com.nmd.nkl.text.editor
{
    /// <summary>GameObject menu entries that create TMP texts already wired with package components.</summary>
    static class TextMenuItems
    {
        const string Root = "GameObject/com.nmd.nkl/Text/";

        [MenuItem(Root + "Title (Bounce In)", false, 11)]
        static void CreateTitle(MenuCommand command) => Create(command, "Title", go =>
        {
            go.AddComponent<BounceInEffect>();
        });

        [MenuItem(Root + "Gradient Text", false, 12)]
        static void CreateGradient(MenuCommand command) => Create(command, "Gradient", go =>
        {
            go.AddComponent<GradientEffect>();
        });

        [MenuItem(Root + "Counter", false, 12)]
        static void CreateCounter(MenuCommand command) => Create(command, "Counter", go =>
        {
            go.AddComponent<TextCounter>();
        });

        [MenuItem(Root + "Typewriter", false, 13)]
        static void CreateTypewriter(MenuCommand command) => Create(command, "Typewriter", go =>
        {
            go.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.TopLeft;
            go.AddComponent<TypewriterEffect>();
        });

        static void Create(MenuCommand command, string name, System.Action<GameObject> setup)
        {
            var parent = command.context as GameObject;
            if (parent == null) parent = Selection.activeGameObject;

            var go = new GameObject(name, typeof(RectTransform));
            GameObjectUtility.SetParentAndAlign(go, parent);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = name;
            text.alignment = TextAlignmentOptions.Center;
            ((RectTransform)go.transform).sizeDelta = new Vector2(400f, 80f);
            setup(go);

            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Selection.activeGameObject = go;
        }
    }
}
