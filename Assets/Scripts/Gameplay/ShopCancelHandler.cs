using UnityEngine;
using UnityEngine.EventSystems;

namespace AscendJigoku.Gameplay
{
    // Closes the shop when the UI Cancel action (Esc / B) fires on one of its buttons.
    public sealed class ShopCancelHandler : MonoBehaviour, ICancelHandler
    {
        public ShopMenu shop;
        public void OnCancel(BaseEventData data)
        {
            shop.Close();
            data.Use();
        }
    }
}
