using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.nmd.nkl.text
{
    /// <summary>
    /// Thick "3D" title: keeps a solid-colored copy of this text, offset downwards and drawn behind it.
    /// The copy follows text, font and layout changes automatically, and the effects of a
    /// <see cref="TextFX"/> on this text. Use the context menu "Create Extrude Layer" to set it up.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [ExecuteAlways]
    [AddComponentMenu("com.nmd.nkl/Text/Text Extrude")]
    public sealed class TextExtrude : MonoBehaviour
    {
        [SerializeField] TMP_Text _layer;
        public Vector2 offset = new Vector2(0f, -9f);
        public Color color = new Color(0.42f, 0.15f, 0.02f, 1f);

        TMP_Text _text;

        TMP_Text Text => _text != null ? _text : (_text = GetComponent<TMP_Text>());

        public TMP_Text Layer => _layer;

        void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            Sync();
            LinkEffects();
        }

        void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            if (_layer != null && TryGetComponent(out TextFX fx)) fx.RemoveFollower(_layer);
        }

        void LinkEffects()
        {
            if (_layer != null && TryGetComponent(out TextFX fx)) fx.AddFollower(_layer);
        }

        void OnTextChanged(Object obj)
        {
            if (obj == Text) Sync();
        }

        void LateUpdate()
        {
            if (!transform.hasChanged) return;
            transform.hasChanged = false;
            SyncTransform();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) Sync();
            };
        }
#endif

        public void Sync()
        {
            if (_layer == null) return;
            TMP_Text t = Text;

            // Same preprocessor so effect tags (<wave>...) are stripped on the layer too.
            _layer.textPreprocessor = t.textPreprocessor;
            if (_layer.text != t.text) _layer.text = t.text;
            if (_layer.font != t.font) _layer.font = t.font;
            if (_layer.fontSharedMaterial != t.fontSharedMaterial) _layer.fontSharedMaterial = t.fontSharedMaterial;
            _layer.fontSize = t.fontSize;
            _layer.enableAutoSizing = t.enableAutoSizing;
            _layer.fontSizeMin = t.fontSizeMin;
            _layer.fontSizeMax = t.fontSizeMax;
            _layer.fontStyle = t.fontStyle;
            _layer.alignment = t.alignment;
            _layer.characterSpacing = t.characterSpacing;
            _layer.wordSpacing = t.wordSpacing;
            _layer.lineSpacing = t.lineSpacing;
            _layer.enableWordWrapping = t.enableWordWrapping;
            _layer.overflowMode = t.overflowMode;
            _layer.enableVertexGradient = false;
            _layer.color = color;
            _layer.raycastTarget = false;
            SyncTransform();
        }

        void SyncTransform()
        {
            if (_layer == null) return;
            if (transform is RectTransform src && _layer.transform is RectTransform dst)
            {
                dst.anchorMin = src.anchorMin;
                dst.anchorMax = src.anchorMax;
                dst.pivot = src.pivot;
                dst.sizeDelta = src.sizeDelta;
                dst.anchoredPosition = src.anchoredPosition + offset;
            }
            else
            {
                _layer.transform.localPosition = transform.localPosition + (Vector3)offset;
            }
            _layer.transform.localRotation = transform.localRotation;
            _layer.transform.localScale = transform.localScale;
        }

        [ContextMenu("Create Extrude Layer")]
        void CreateLayer()
        {
            if (_layer != null) return;

            // Sibling placed right before this text so it renders behind it.
            var go = new GameObject(name + "_Extrude", typeof(RectTransform)) { layer = gameObject.layer };
            go.transform.SetParent(transform.parent, false);
            go.transform.SetSiblingIndex(transform.GetSiblingIndex());
            _layer = Text is TextMeshProUGUI ? go.AddComponent<TextMeshProUGUI>() : go.AddComponent<TextMeshPro>();
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Extrude Layer");
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Sync();
            LinkEffects();
        }
    }
}
