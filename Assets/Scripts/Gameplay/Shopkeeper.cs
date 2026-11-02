using System.Collections.Generic;
using UnityEngine;

namespace AscendJigoku.Gameplay
{
    public sealed class Shopkeeper : MonoBehaviour
    {
        public string displayName = "Checkpoint Merchant";
        public ShopCatalog catalog;
        [Min(0.5f)] public float interactionRadius = 2.5f;
        private static readonly List<Shopkeeper> actors = new List<Shopkeeper>();
        public static IReadOnlyList<Shopkeeper> Actors => actors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActors() => actors.Clear();
        private void OnEnable() { if (!actors.Contains(this)) actors.Add(this); }
        private void OnDisable() => actors.Remove(this);
    }
}
