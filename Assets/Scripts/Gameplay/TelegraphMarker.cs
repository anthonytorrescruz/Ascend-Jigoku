using AscendJigoku.Settings;
using UnityEngine;

namespace AscendJigoku.Gameplay
{
    // "Stronger attack warnings" accessibility option: a pulsing "!" over an enemy for the length of its windup.
    // Added on demand by CombatFeedback; does nothing while the option is off.
    [DisallowMultipleComponent]
    public sealed class TelegraphMarker : MonoBehaviour
    {
        private SpriteRenderer marker, body;
        private float startsAt, endsAt;

        public static void Show(GameObject owner, float seconds)
        {
            if (!GameSettings.StrongTelegraphs || owner == null || seconds <= 0) return;
            var telegraph = owner.GetComponent<TelegraphMarker>();
            if (telegraph == null) telegraph = owner.AddComponent<TelegraphMarker>();
            telegraph.Begin(seconds);
        }

        public static void Hide(GameObject owner)
        {
            if (owner == null) return;
            var telegraph = owner.GetComponent<TelegraphMarker>();
            if (telegraph != null) telegraph.endsAt = 0;
        }

        private void Begin(float seconds)
        {
            if (marker == null)
            {
                body = GetComponent<SpriteRenderer>();
                if (body == null) body = GetComponentInChildren<SpriteRenderer>();
                var go = new GameObject("Attack warning", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                marker = go.GetComponent<SpriteRenderer>();
                marker.sprite = ProceduralSprites.Exclamation;
                if (body != null) { marker.sortingLayerID = body.sortingLayerID; marker.sortingOrder = body.sortingOrder + 5; }
            }
            startsAt = Time.time;
            endsAt = Time.time + seconds;
        }

        private void LateUpdate()
        {
            if (marker == null) return;
            bool visible = Time.time < endsAt && GameSettings.StrongTelegraphs;
            if (marker.enabled != visible) marker.enabled = visible;
            if (!visible) return;

            float progress = Mathf.InverseLerp(startsAt, endsAt, Time.time);
            Vector3 top = body != null ? new Vector3(body.bounds.center.x, body.bounds.max.y, 0) : transform.position + Vector3.up;
            var t = marker.transform;
            t.position = top + new Vector3(0, 0.25f, 0);
            // Counter parent flips/scale so the glyph stays upright and the same size on every enemy.
            Vector3 parent = transform.lossyScale;
            float pulse = GameSettings.ReduceFlashing ? 1f : 1f + 0.18f * Mathf.Sin(progress * Mathf.PI * 6f);
            float size = Mathf.Lerp(0.9f, 1.25f, progress) * pulse;
            t.localScale = new Vector3(size / SafeAbs(parent.x), size / SafeAbs(parent.y), 1);
            t.rotation = Quaternion.identity;
            marker.color = Color.Lerp(new Color(1f, 0.8f, 0.2f), new Color(1f, 0.15f, 0.1f), progress);
        }

        private static float SafeAbs(float value) => Mathf.Max(0.0001f, Mathf.Abs(value));

        private void OnDisable() { endsAt = 0; if (marker != null) marker.enabled = false; }
    }
}
