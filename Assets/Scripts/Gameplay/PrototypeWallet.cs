using UnityEngine;

namespace AscendJigoku.Gameplay
{
    public sealed class PrototypeWallet : CurrencyWallet
    {
        [Tooltip("Editor-tweakable test balance. No enemy drops or real currency economy are implemented.")]
        [Min(0)] public int testCoins = 500;
        public override int Balance => Mathf.Max(0, testCoins);
        public override string CurrencyName => "Test coins (placeholder)";
        public override bool TrySpend(int amount)
        {
            if (amount < 0 || amount > Balance) return false;
            testCoins -= amount; return true;
        }
    }
}
