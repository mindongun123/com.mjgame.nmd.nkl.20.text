using UnityEngine;
using UnityEngine.UI;

namespace com.mjgame.nmd.nkl.sample
{
    /// <summary>
    /// Switches between the pages of the sample scene (effect demos, shop).
    /// Hidden pages are deactivated, so their texts leave the TextFX update loop.
    /// </summary>
    public sealed class SampleTabs : MonoBehaviour
    {
        [System.Serializable]
        public struct Page
        {
            public Button button;
            public GameObject[] content;
        }

        [SerializeField] Page[] _pages;
        [SerializeField] int _startPage;
        [SerializeField] Color _selected = new Color(1f, 0.78f, 0.25f);
        [SerializeField] Color _normal = Color.white;

        void Awake()
        {
            // Load presets and settings now, so the first tag does not hitch (do this on a loading screen).
            TextFXPresets.Prewarm();
            for (int i = 0; i < _pages.Length; i++)
            {
                int index = i;
                if (_pages[i].button != null) _pages[i].button.onClick.AddListener(() => Show(index));
            }
            Show(_startPage);
        }

        public void Show(int index)
        {
            for (int i = 0; i < _pages.Length; i++)
            {
                bool on = i == index;
                foreach (GameObject go in _pages[i].content)
                    if (go != null) go.SetActive(on);
                if (_pages[i].button != null && _pages[i].button.targetGraphic != null)
                    _pages[i].button.targetGraphic.color = on ? _selected : _normal;
            }
        }
    }
}
