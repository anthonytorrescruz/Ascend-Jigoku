using UnityEngine;

namespace AscendJigoku.Gameplay
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ShopkeeperAnimation : MonoBehaviour
    {
        public Sprite[] frames;
        [Min(1)] public float framesPerSecond = 12.5f;
        private SpriteRenderer visual;
        private float started;
        private void Awake() { visual = GetComponent<SpriteRenderer>(); started = Time.time; }
        private void LateUpdate()
        {
            if (frames == null || frames.Length == 0) return;
            visual.sprite = frames[Mathf.FloorToInt((Time.time - started) * framesPerSecond) % frames.Length];
        }
    }
}
