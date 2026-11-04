using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace com.mjgame.nmd.nkl.sample
{
    /// <summary>One shop cell. Effects are added in the template; this only fills text and triggers them.</summary>
    public sealed class ShopSampleItem : MonoBehaviour
    {
        [SerializeField] TMP_Text _name;
        [SerializeField] TMP_Text _price;
        [SerializeField] GameObject _hotBadge;
        [SerializeField] Button _button;
        [SerializeField] BounceInEffect _openEffect;
        [SerializeField] ShakeEffect _denyEffect;
        [SerializeField] BounceInEffect _boughtEffect;

        ShopSample _shop;
        long _price0;

        public void Setup(ShopSample shop, string itemName, int level, long price, bool hot)
        {
            _shop = shop;
            _price0 = price;
            _name.SetText(itemName);
            _price.SetAbbreviated(price, 1, 10000, "$ ");
            _hotBadge.SetActive(hot);
            _button.onClick.AddListener(Buy);
        }

        public void PlayOpen() => _openEffect.Play();

        void Buy()
        {
            if (_shop.TryBuy(_price0))
            {
                _boughtEffect.Play();
                _price.SetText("Owned");
                _button.interactable = false;
            }
            else
            {
                _denyEffect.Play();
            }
        }
    }
}
