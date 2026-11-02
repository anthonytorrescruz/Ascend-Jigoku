using UnityEngine;
using UnityEngine.EventSystems;

namespace AscendJigoku.Gameplay
{
    public sealed class MenuBackHandler : MonoBehaviour, ICancelHandler
    {
        public AnthonySession session;
        public void OnCancel(BaseEventData eventData) { session?.Back(); eventData.Use(); }
    }
}
