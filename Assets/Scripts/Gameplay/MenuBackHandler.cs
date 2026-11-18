using UnityEngine;
using UnityEngine.EventSystems;

namespace AscendJigoku.Gameplay
{
    // Routes the UI Cancel action (Esc / B) on a menu button to AnthonySession.Back().
    public sealed class MenuBackHandler : MonoBehaviour, ICancelHandler
    {
        public AnthonySession session;
        public void OnCancel(BaseEventData eventData)
        {
            session?.Back();
            eventData.Use();
        }
    }
}
