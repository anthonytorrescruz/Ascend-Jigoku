using System.Collections;
using UnityEngine;
using AscendJigoku.UI;

namespace AscendJigoku.Gameplay
{
    // The tutorial's rooms, built from the pillar's own platform prefab and rock skin, far to the side of the pillar.
    // Positions are local: x runs east along the corridor, y = 0 is the floor top.
    public sealed class TutorialLevel
    {
        public readonly Transform Root;
        public readonly Vector3 Origin;
        private readonly GameObject platform;
        private int counter;
        private Sprite spikeSprite, poleSprite;

        public TutorialLevel(VerticalLevelGenerator generator, Vector3 origin)
        {
            Origin = origin;
            platform = generator.Settings.platformPrefab;
            Root = new GameObject("Tutorial Level").transform;
            Root.position = origin;
        }

        public Vector3 At(float x, float y = 0) => Origin + new Vector3(x, y, 0);

        public GameObject Block(float x0, float x1, float top, float bottom)
        {
            var go = Object.Instantiate(platform, Root);
            go.name = "Tutorial rock";
            go.transform.position = At((x0 + x1) * 0.5f, (top + bottom) * 0.5f);
            go.transform.localScale = new Vector3(x1 - x0, top - bottom, 1);
            var render = go.GetComponent<SpriteRenderer>();
            if (render != null)
            {
                render.color = PillarEnvironment.TintRock(new Color(0.35f, 0.3f, 0.32f), 0);
                PillarEnvironment.GlowAt(0, out Color glow, out float density);
                if (PillarEnvironment.PixelArtTerrain) TerrainSkin.Apply(render, 7919 * ++counter, true, glow, density, 1);
            }
            return go;
        }

        public TutorialSeal Seal(float x, string label) => TutorialSeal.Create(Root, At(x, 7), 14, label);

        // Row of upward spikes, kill zone for a pit floor.
        public GameObject SpikeBed(float x0, float x1, float y)
        {
            var go = new GameObject("Tutorial spikes");
            go.transform.SetParent(Root, false);
            go.transform.position = At((x0 + x1) * 0.5f, y);
            var render = go.AddComponent<SpriteRenderer>();
            render.sprite = Spikes;
            render.drawMode = SpriteDrawMode.Tiled;
            render.size = new Vector2(x1 - x0, 1);
            render.sortingOrder = 3;
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true; box.size = new Vector2(x1 - x0, 0.8f); box.offset = new Vector2(0, -0.1f);
            go.AddComponent<DamageZone>();
            return go;
        }

        // A pole with a spiked head that hurts to land on and can be pogoed.
        public GameObject SpikePost(float x, float topY)
        {
            var go = new GameObject("Tutorial spike post");
            go.transform.SetParent(Root, false);
            go.transform.position = At(x, topY);
            var head = new GameObject("Head").AddComponent<SpriteRenderer>();
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0, 0.5f, 0);
            head.sprite = Spikes; head.drawMode = SpriteDrawMode.Tiled; head.size = new Vector2(3, 1); head.sortingOrder = 3;
            var pole = new GameObject("Pole").AddComponent<SpriteRenderer>();
            pole.transform.SetParent(go.transform, false);
            pole.transform.localPosition = new Vector3(0, -2.5f, 0);
            pole.sprite = ProceduralSprites.Square; pole.color = new Color(0.16f, 0.1f, 0.09f);
            pole.transform.localScale = new Vector3(0.7f, 5f, 1); pole.sortingOrder = 2;
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true; box.size = new Vector2(3, 1.4f); box.offset = new Vector2(0, 0.4f);
            go.AddComponent<DamageZone>();
            go.AddComponent<Pogoable>();
            return go;
        }

        private Sprite Spikes
        {
            get
            {
                if (spikeSprite != null) return spikeSprite;
                var tex = ProceduralSprites.NewTexture(16, 16, "Tutorial spikes");
                var px = new Color32[256];
                var steel = new Color32(0xB9, 0xA9, 0x9C, 255); var edge = new Color32(0x3A, 0x1E, 0x1A, 255); var tip = new Color32(0xF2, 0xE4, 0xCC, 255);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        float half = (15 - y) * 0.5f + 0.5f; // tooth narrows toward the top
                        float d = Mathf.Abs(x - 7.5f);
                        if (d > half) continue;
                        px[y * 16 + x] = d > half - 1.1f ? edge : (y > 11 ? tip : steel);
                    }
                tex.SetPixels32(px); tex.Apply(false);
                spikeSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16, 0, SpriteMeshType.FullRect);
                return spikeSprite;
            }
        }
    }

    // Glowing barrier that holds the player until a lesson is done, then burns away.
    public sealed class TutorialSeal : MonoBehaviour
    {
        private SpriteRenderer core, glow;
        private bool dissolving;

        public static TutorialSeal Create(Transform parent, Vector3 center, float height, string label)
        {
            var go = new GameObject("Tutorial seal - " + label);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.6f, height);
            var seal = go.AddComponent<TutorialSeal>();
            seal.glow = Bar(go.transform, "Glow", 2.4f, height, new Color(MenuStyle.EmberDeep.r, MenuStyle.EmberDeep.g, MenuStyle.EmberDeep.b, 0.18f), 3);
            seal.core = Bar(go.transform, "Core", 0.35f, height, MenuStyle.Ember, 4);
            return seal;
        }

        private static SpriteRenderer Bar(Transform parent, string name, float width, float height, Color color, int order)
        {
            var bar = new GameObject(name).AddComponent<SpriteRenderer>();
            bar.transform.SetParent(parent, false);
            bar.sprite = ProceduralSprites.Square; bar.color = color; bar.sortingOrder = order;
            bar.transform.localScale = new Vector3(width, height, 1);
            return bar;
        }

        private void Update()
        {
            if (dissolving) return;
            float pulse = AscendJigoku.Settings.GameSettings.ReduceFlashing ? 0.85f : 0.7f + 0.3f * Mathf.Sin(Time.time * 5f);
            var c = core.color; c.a = pulse; core.color = c;
        }

        public void Dissolve()
        {
            if (dissolving) return;
            dissolving = true;
            GetComponent<Collider2D>().enabled = false;
            StartCoroutine(Burn());
        }

        private IEnumerator Burn()
        {
            for (float t = 0; t < 0.5f; t += Time.deltaTime)
            {
                float k = 1 - t / 0.5f;
                var c = core.color; c.a = k; core.color = c;
                var g = glow.color; g.a = 0.18f * k; glow.color = g;
                core.transform.localScale = new Vector3(0.35f * (1 + (1 - k) * 3), core.transform.localScale.y, 1);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
