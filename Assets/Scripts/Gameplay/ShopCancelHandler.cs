using UnityEngine;
using UnityEngine.EventSystems;
namespace AscendJigoku.Gameplay
{
    public sealed class ShopCancelHandler : MonoBehaviour, ICancelHandler
    {
        public ShopMenu shop;
        public void OnCancel(BaseEventData data) { shop.Close(); data.Use(); }
    }
}
