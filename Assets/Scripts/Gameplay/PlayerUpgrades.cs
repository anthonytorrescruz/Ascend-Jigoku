using UnityEngine;

namespace AscendJigoku.Gameplay
{
    [RequireComponent(typeof(PlayerHealth), typeof(PlayerCombat), typeof(PlayerController))]
    public sealed class PlayerUpgrades : MonoBehaviour
    {
        private readonly int[] purchases = new int[3];
        public int Count(UpgradeKind kind) => purchases[(int)kind];
        public bool IsMaxed(ShopOffer offer)
        {
            if (offer == null) return true;
            if (Count(offer.kind) >= Mathf.Max(1, offer.maxPurchases)) return true;
            return offer.kind == UpgradeKind.MaxHealth ? GetComponent<PlayerHealth>().MaxHealth >= 99
                : offer.kind == UpgradeKind.Damage ? GetComponent<PlayerCombat>().AttackDamage >= 99
                : GetComponent<PlayerController>().MaxJumps >= 8;
        }
        public bool TryBuy(ShopOffer offer, CurrencyWallet wallet, out string result)
        {
            result = "Shop is unavailable.";
            if (offer == null || wallet == null || !isActiveAndEnabled) return false;
            if (GetComponent<PlayerHealth>().CurrentHealth <= 0) { result = "You cannot buy while defeated."; return false; }
            if (IsMaxed(offer)) { result = "Upgrade limit reached."; return false; }
            int cost = offer.Price(Count(offer.kind));
            if (!wallet.TrySpend(cost)) { result = "Not enough " + wallet.CurrencyName.ToLowerInvariant() + "."; return false; }
            int amount = Mathf.Max(1, offer.amount);
            switch (offer.kind)
            {
                case UpgradeKind.MaxHealth: GetComponent<PlayerHealth>().IncreaseMaxHealth(amount); break;
                case UpgradeKind.Damage: GetComponent<PlayerCombat>().IncreaseAttackDamage(amount); break;
                case UpgradeKind.ExtraJump: GetComponent<PlayerController>().IncreaseMaxJumps(amount); break;
            }
            purchases[(int)offer.kind]++; result = offer.title + " purchased."; return true;
        }
    }
}
