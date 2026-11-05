using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.mjgame.nmd.nkl
{
    /// <summary>
    /// Thick "3D" title: keeps a solid-colored copy of this text, offset downwards and drawn behind it.
    /// The copy follows text, font and layout changes automatically, and the effects of a
    /// <see cref="TextFX"/> on this text. Use the context menu "Create Extrude Layer" to set it up.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [ExecuteAlways]
    [AddComponentMenu("com.mjgame.nmd.nkl/Text/Text Extrude")]
    public sealed class TextExtrude : MonoBehaviour, ITextChangeListener
    {
        [SerializeField] TMP_Text _layer;
        [Tooltip("Offset of the layer, in UI units. World texts multiply it by the World distance scale of TextFXSettings.")]
        public Vector2 offset = new Vector2(0f, -9f);
        [Tooltip("World texts only: how far behind the text the layer sits (local Z), so it never draws over the text.")]
        public float worldDepth = 0.01f;
        public Color color = new Color(0.42f, 0.15f, 0.02f, 1f);

        TMP_Text _text;

        TMP_Text Text => _text != null ? _text : (_text = GetComponent<TMP_Text>());

        public TMP_Text Layer => _layer;

        void OnEnable()
        {
            TextChangeDispatcher.Watch(Text, this);
            Sync();
            LinkEffects();
        }

        void OnDisable()
        {
            TextChangeDispatcher.Unwatch(Text, this);
            if (_layer != null && TryGetComponent(out TextFX fx)) fx.RemoveFollower(_layer);
        }

        void LinkEffects()
        {
            if (_layer != null && TryGetComponent(out TextFX fx)) fx.AddFollower(_layer);
        }

        void ITextChangeListener.OnTextChanged(Object obj)
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
                // TextMeshPro (world) has a RectTransform too: copy the frame for both kinds.
                dst.anchorMin = src.anchorMin;
                dst.anchorMax = src.anchorMax;
                dst.pivot = src.pivot;
                dst.sizeDelta = src.sizeDelta;
            }

            if (!TextFXSettings.IsWorld(Text))
            {
                ((RectTransform)_layer.transform).anchoredPosition = ((RectTransform)transform).anchoredPosition + offset;
            }
            else
            {
                // World text: distances follow the World scale like the effects do, and the layer is pushed
                // back in Z and drawn first: sibling order does not decide draw order for 3D meshes.
                Vector3 shift = (Vector3)(offset * TextFXSettings.Instance.UnitScale(Text)) + new Vector3(0f, 0f, worldDepth);
                _layer.transform.localPosition = transform.localPosition + transform.localRotation * Vector3.Scale(shift, transform.localScale);
                if (Text is TextMeshPro world && _layer is TextMeshPro layer)
                {
                    layer.sortingLayerID = world.sortingLayerID;
                    layer.sortingOrder = world.sortingOrder - 1;
                }
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
