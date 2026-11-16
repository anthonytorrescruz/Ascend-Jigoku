using System.Collections;
using UnityEngine;
using AscendJigoku.UI;

namespace AscendJigoku.Gameplay
{
    // The tutorial's unwinnable finale: a hand the size of a house comes down on wherever you are standing.
    // It marks the ground first, then slams. It keeps coming until it connects.
    public sealed class TutorialHand : MonoBehaviour
    {
        private const float Width = 12, Height = 14;
        private SpriteRenderer hand, bar, column;
        private static Sprite handSprite;

        public static TutorialHand Create(Transform parent)
        {
            var go = new GameObject("Tutorial hand");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<TutorialHand>();
            h.hand = Part(go.transform, "Hand", HandSprite, Color.white, 6);
            h.bar = Part(go.transform, "Mark", ProceduralSprites.Square, new Color(MenuStyle.EmberDeep.r, MenuStyle.EmberDeep.g, MenuStyle.EmberDeep.b, 0.8f), 1);
            h.column = Part(go.transform, "Column", ProceduralSprites.Square, new Color(MenuStyle.EmberDeep.r, MenuStyle.EmberDeep.g, MenuStyle.EmberDeep.b, 0.1f), 0);
            h.hand.gameObject.SetActive(false); h.bar.gameObject.SetActive(false); h.column.gameObject.SetActive(false);
            return h;
        }

        private static SpriteRenderer Part(Transform parent, string name, Sprite sprite, Color color, int order)
        {
            var r = new GameObject(name).AddComponent<SpriteRenderer>();
            r.transform.SetParent(parent, false);
            r.sprite = sprite; r.color = color; r.sortingOrder = order;
            return r;
        }

        // Follows the player for `follow` seconds, locks for `warn`, then slams. Returns after the hand lifts away.
        public IEnumerator Strike(Transform target, float floorY, float follow, float warn)
        {
            float x = target.position.x;
            bar.gameObject.SetActive(true); column.gameObject.SetActive(true);
            bar.transform.localScale = new Vector3(Width, 0.3f, 1);
            column.transform.localScale = new Vector3(Width, 40, 1);
            for (float t = 0; t < follow + warn; t += Time.deltaTime)
            {
                if (t < follow) x = Mathf.MoveTowards(x, target.position.x, 22 * Time.deltaTime);
                bar.transform.position = new Vector3(x, floorY + 0.15f, 0);
                column.transform.position = new Vector3(x, floorY + 20, 0);
                float pulse = AscendJigoku.Settings.GameSettings.ReduceFlashing ? 0.8f : 0.55f + 0.45f * Mathf.Sin(t * 22);
                var c = bar.color; c.a = pulse; bar.color = c;
                yield return null;
            }
            hand.gameObject.SetActive(true);
            for (float t = 0; t < 0.14f; t += Time.deltaTime)
            {
                hand.transform.position = new Vector3(x, floorY + Mathf.Lerp(34, 0, t / 0.14f), 0);
                yield return null;
            }
            hand.transform.position = new Vector3(x, floorY, 0);
            bar.gameObject.SetActive(false); column.gameObject.SetActive(false);
            EncounterFeedback.Shake(0.8f);
            var health = target.GetComponent<PlayerHealth>();
            if (health != null && Mathf.Abs(target.position.x - x) < Width * 0.5f - 0.4f && target.position.y < floorY + Height)
                health.TakeFallDamage(99);
            yield return new WaitForSeconds(0.7f);
            for (float t = 0; t < 0.5f; t += Time.deltaTime)
            {
                hand.transform.position = new Vector3(x, floorY + t * 40, 0);
                yield return null;
            }
            hand.gameObject.SetActive(false);
        }

        // Dark knuckled hand seen from the front, fingers down, veins of ember in the skin. 96 x 112 px at 8 px per unit.
        private static Sprite HandSprite
        {
            get
            {
                if (handSprite != null) return handSprite;
                const int w = 96, h = 112;
                var tex = ProceduralSprites.NewTexture(w, h, "Tutorial hand");
                var px = new Color32[w * h];
                var skin = new Color32(0x3A, 0x15, 0x12, 255); var lit = new Color32(0x6E, 0x26, 0x1B, 255);
                var ink = new Color32(0x14, 0x07, 0x07, 255); var vein = new Color32(0xF0, 0x8A, 0x3C, 255);
                int[] tips = { 14, 4, 10, 24 }; // finger lengths: each finger ends this high above the bottom
                var rng = new System.Random(5);
                bool Inside(int x, int y)
                {
                    if (y >= 52 && x >= 10 && x < 86) return y < 108 || (y < 111 && x > 18 && x < 78);   // palm
                    if (x >= 86 && x < 96 && y >= 56 && y < 92) return x - 86 < (y - 56) * 0.4f + 2 || y > 80; // thumb
                    for (int f = 0; f < 4; f++)
                    {
                        int x0 = 10 + f * 19;
                        if (x >= x0 && x < x0 + 17 && y >= tips[f] && y < 52) return y > tips[f] + 2 || (x > x0 + 3 && x < x0 + 14);
                    }
                    return false;
                }
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (!Inside(x, y)) continue;
                        bool edge = !Inside(x - 1, y) || !Inside(x + 1, y) || !Inside(x, y - 1) || !Inside(x, y + 1);
                        Color32 c = edge ? ink : ((x + y / 3) % 17 < 3 ? lit : skin);
                        if (!edge && y % 17 == 0 && y < 52) c = ink;          // knuckle creases
                        if (!edge && rng.Next(100) < 2) c = vein;
                        px[y * w + x] = c;
                    }
                tex.SetPixels32(px); tex.Apply(false);
                handSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0), 8, 0, SpriteMeshType.FullRect);
                return handSprite;
            }
        }
    }
}
