using UnityEngine;

namespace AscendJigoku.Gameplay
{
    // Implement this adapter when the real currency/loot system exists.
    public abstract class CurrencyWallet : MonoBehaviour
    {
        public abstract int Balance { get; }
        public abstract string CurrencyName { get; }
        public abstract bool TrySpend(int amount);
    }
}
