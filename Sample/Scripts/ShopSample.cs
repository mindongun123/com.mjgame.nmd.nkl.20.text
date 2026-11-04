using TMPro;
using UnityEngine;

namespace com.mjgame.nmd.nkl.sample
{
    /// <summary>
    /// Shop with many items: shows how to use the package on big lists.
    /// - Names/prices: plain TMP (font/material presets), formatted with the zero-GC extensions.
    /// - Every item has a BounceIn effect that plays once when the shop opens, then sleeps.
    /// - Only "HOT" badges loop (Pulse + Rainbow), each on its own sub-canvas.
    /// - Coins use a TextCounter; a countdown uses SetTime only when the second changes.
    /// </summary>
    public sealed class ShopSample : MonoBehaviour
    {
        [SerializeField] ShopSampleItem _template;
        [SerializeField] Transform _content;
        [SerializeField] TextCounter _coins;
        [SerializeField] TMP_Text _timer;
        [SerializeField] int _itemCount = 100;
        [SerializeField] int _hotEvery = 10;
        [SerializeField] float _saleSeconds = 3725f;

        static readonly string[] Names = { "Sword", "Shield", "Potion", "Helmet", "Boots", "Ring", "Bow", "Staff" };

        ShopSampleItem[] _items;
        int _shownSecond = -1;

        void Start()
        {
            _template.gameObject.SetActive(false);
            _items = new ShopSampleItem[_itemCount];
            for (int i = 0; i < _itemCount; i++)
            {
                ShopSampleItem item = Instantiate(_template, _content);
                item.name = "Item " + i;
                long price = 50L * (i + 1) * (i + 1);
                item.Setup(this, Names[i % Names.Length], i + 1, price, _hotEvery > 0 && i % _hotEvery == 0);
                item.gameObject.SetActive(true);
                _items[i] = item;
            }
            _coins.SetValue(25000);
        }

        void Update()
        {
            _saleSeconds -= Time.unscaledDeltaTime;
            // Re-render only when the visible value changes: setting text is what costs, not the check.
            int second = Mathf.CeilToInt(Mathf.Max(0f, _saleSeconds));
            if (second == _shownSecond) return;
            _shownSecond = second;
            _timer.SetTime(second, TimeFormat.Auto, false, "Sale ends in ");
        }

        /// <summary>Replays the open animation of every item (e.g. when the shop popup opens).</summary>
        public void Open()
        {
            foreach (ShopSampleItem item in _items) item.PlayOpen();
        }

        public bool TryBuy(long price)
        {
            if (_coins.Target < price) return false;
            _coins.Add(-price);
            return true;
        }

        public void AddCoins(int amount) => _coins.Add(amount);
    }
}
