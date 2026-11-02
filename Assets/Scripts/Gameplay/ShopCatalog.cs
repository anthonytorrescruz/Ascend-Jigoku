using System;
using UnityEngine;

namespace AscendJigoku.Gameplay
{
    public enum UpgradeKind { MaxHealth, Damage, ExtraJump }
    [Serializable]
    public sealed class ShopOffer
    {
        public UpgradeKind kind;
        public string title;
        [TextArea] public string description;
        [Min(0)] public int basePrice = 100;
        [Min(1)] public float priceMultiplier = 1.5f;
        [Range(1, 8)] public int maxPurchases = 3;
        [Min(1)] public int amount = 1;
        public int Price(int purchased) => Mathf.Clamp(Mathf.CeilToInt(basePrice * Mathf.Pow(priceMultiplier, purchased)), 0, 1000000);
    }
    [CreateAssetMenu(menuName = "Ascend Jigoku/Shop Catalog")]
    public sealed class ShopCatalog : ScriptableObject
    {
        public string greeting = "Choose an upgrade for the climb.";
        public ShopOffer[] offers = {
            new ShopOffer { kind = UpgradeKind.MaxHealth, title = "Vitality", description = "+1 maximum health and one health restored.", basePrice = 100 },
            new ShopOffer { kind = UpgradeKind.Damage, title = "Sharpen", description = "+1 melee damage. Your ranged attack follows melee damage.", basePrice = 150 },
            new ShopOffer { kind = UpgradeKind.ExtraJump, title = "Air Step", description = "+1 air jump: three jumps total, including the ground jump.", basePrice = 250, maxPurchases = 1 }
        };
        public AudioClip purchaseSound;
        public AudioClip openSound;
        [Range(0, 1)] public float openSoundVolume = 0.4f;
        [Range(0.5f, 1.5f)] public float openSoundPitch = 0.85f;
    }
}
