using System.Collections.Generic;
using AscendJigoku.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace AscendJigoku.Gameplay
{
    // Game-feel layer: screen shake, hit-stop, the low-health/damage vignette and enemy death bursts.
    // CombatFeedback calls the On* hooks; everything respects the accessibility settings.
    // Tuning lives in the constants below until we decide whether it deserves an asset.
    [DefaultExecutionOrder(-900)]
    public sealed class EncounterFeedback : MonoBehaviour
    {
        // Distinctive value so a stale hit-stop restored by a menu can be recognised and repaired.
        private const float HitStopScale = 0.0437f;
        private const float MaxShakeUnits = 0.42f;
        private const float ShakeDecayPerSecond = 2.4f;

        private static EncounterFeedback instance;
        public static Vector3 CameraOffset { get; private set; }

        private float trauma, hitStopUntil, vignettePulse, nextPlayerSearch;
        private bool hitStopping;
        private PlayerHealth player;
        private RawImage vignette;

        private sealed class Spark { public Transform Transform; public SpriteRenderer Renderer; public Vector2 Velocity; public float Age, Life, Size; public Color Color; }
        private readonly List<Spark> sparks = new List<Spark>();
        private readonly Stack<Spark> sparePool = new Stack<Spark>();

        private static EncounterFeedback Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("Encounter Feedback");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<EncounterFeedback>();
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() => _ = Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; CameraOffset = Vector3.zero; }

        // ---------- Hooks ----------

        public static void OnPlayerHit(bool lethal)
        {
            Shake(lethal ? 0.75f : 0.55f);
            HitStop(lethal ? 0.12f : 0.07f);
            Instance.vignettePulse = 1f;
        }

        public static void OnEnemyHit(CombatFeedback target, bool lethal)
        {
            Shake(lethal ? 0.3f : 0.16f);
            HitStop(lethal ? 0.08f : 0.045f);
            if (lethal && target != null) Instance.Burst(target);
        }

        public static void OnParry() { Shake(0.35f); HitStop(0.09f); }
        public static void OnPogo() { Shake(0.1f); HitStop(0.03f); }

        // ---------- Primitives ----------

        public static void Shake(float amount)
        {
            if (GameSettings.ScreenShake <= 0 || amount <= 0) return;
            var self = Instance;
            self.trauma = Mathf.Clamp01(self.trauma + amount);
        }

        public static void HitStop(float seconds)
        {
            if (!GameSettings.HitStop || seconds <= 0 || Time.timeScale <= 0) return;
            var self = Instance;
            // Only freeze normal play; never override a menu or someone else's slow-motion.
            if (!self.hitStopping && !Mathf.Approximately(Time.timeScale, GameSettings.GameplayTimeScale)) return;
            self.hitStopping = true;
            self.hitStopUntil = Mathf.Max(self.hitStopUntil, Time.unscaledTime + seconds);
            Time.timeScale = HitStopScale;
        }

        private static bool IsHitStopScale(float scale) => Mathf.Abs(scale - HitStopScale) < 0.0001f;

        // ---------- Frame ----------

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            BuildVignette();
        }

        private void Update()
        {
            if (hitStopping && Time.unscaledTime >= hitStopUntil)
            {
                hitStopping = false;
                if (IsHitStopScale(Time.timeScale)) Time.timeScale = GameSettings.GameplayTimeScale;
            }
            else if (!hitStopping && IsHitStopScale(Time.timeScale)) Time.timeScale = GameSettings.GameplayTimeScale;

            trauma = Mathf.MoveTowards(trauma, 0, Time.unscaledDeltaTime * ShakeDecayPerSecond);
            if (Time.timeScale <= 0 || trauma <= 0 || GameSettings.ScreenShake <= 0) CameraOffset = Vector3.zero;
            else
            {
                float strength = trauma * trauma * GameSettings.ScreenShake * MaxShakeUnits;
                float t = Time.unscaledTime * 30f;
                CameraOffset = new Vector3((Mathf.PerlinNoise(t, 0.37f) * 2 - 1) * strength, (Mathf.PerlinNoise(0.71f, t) * 2 - 1) * strength, 0);
            }

            UpdateVignette();
            UpdateSparks(Time.deltaTime);
        }

        // ---------- Vignette ----------

        private void BuildVignette()
        {
            var canvasObject = new GameObject("Damage Vignette", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -50; // under every menu, over the world
            var imageObject = new GameObject("Vignette", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            vignette = imageObject.GetComponent<RawImage>();
            vignette.texture = ProceduralSprites.Vignette;
            vignette.raycastTarget = false;
            vignette.color = new Color(0.55f, 0.02f, 0.02f, 0);
        }

        private void UpdateVignette()
        {
            if (vignette == null) return;
            if (player == null && Time.unscaledTime >= nextPlayerSearch)
            {
                nextPlayerSearch = Time.unscaledTime + 0.5f;
                player = FindAnyObjectByType<PlayerHealth>();
            }
            vignettePulse = Mathf.MoveTowards(vignettePulse, 0, Time.unscaledDeltaTime * (GameSettings.ReduceFlashing ? 1.1f : 1.8f));
            float alpha = 0;
            if (player != null && GameSettings.DamageVignette)
            {
                int hp = player.CurrentHealth, max = Mathf.Max(1, player.MaxHealth);
                bool low = hp > 0 && (hp == 1 || hp <= max / 3);
                float breathing = GameSettings.ReduceFlashing ? 0 : 0.08f * Mathf.Sin(Time.unscaledTime * 3.2f);
                float baseline = hp <= 0 ? 0.85f : low ? 0.5f + breathing : 0;
                float pulse = vignettePulse * (GameSettings.ReduceFlashing ? 0.4f : 0.7f);
                alpha = Mathf.Clamp01(Mathf.Max(baseline, pulse));
            }
            var color = vignette.color; color.a = alpha; vignette.color = color;
            vignette.enabled = alpha > 0.001f;
        }

        // ---------- Death burst ----------

        private void Burst(CombatFeedback target)
        {
            var body = target.GetComponent<SpriteRenderer>();
            if (body == null) body = target.GetComponentInChildren<SpriteRenderer>();
            Vector3 center = body != null ? body.bounds.center : target.transform.position;
            Color tint = body != null ? body.color : Color.white;
            int order = body != null ? body.sortingOrder + 2 : 5;
            int layer = body != null ? body.sortingLayerID : 0;
            int count = GameSettings.ReduceFlashing ? 8 : 14;
            for (int i = 0; i < count; i++)
            {
                var spark = sparePool.Count > 0 ? sparePool.Pop() : NewSpark();
                float angle = Random.Range(0f, Mathf.PI * 2);
                float speed = Random.Range(3f, 8.5f);
                spark.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.8f + 0.45f) * speed;
                spark.Age = 0; spark.Life = Random.Range(0.35f, 0.65f); spark.Size = Random.Range(0.12f, 0.26f);
                spark.Color = i % 3 == 0 ? new Color(1f, 0.55f, 0.15f) : Color.Lerp(tint, Color.white, 0.25f);
                spark.Transform.position = center + (Vector3)(Random.insideUnitCircle * 0.25f);
                spark.Renderer.sortingLayerID = layer; spark.Renderer.sortingOrder = order;
                spark.Transform.gameObject.SetActive(true);
                sparks.Add(spark);
            }
        }

        private Spark NewSpark()
        {
            var go = new GameObject("Spark", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = ProceduralSprites.Square;
            return new Spark { Transform = go.transform, Renderer = renderer };
        }

        private void UpdateSparks(float dt)
        {
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var spark = sparks[i];
                spark.Age += dt;
                if (spark.Age >= spark.Life || spark.Transform == null)
                {
                    if (spark.Transform != null) { spark.Transform.gameObject.SetActive(false); sparePool.Push(spark); }
                    sparks.RemoveAt(i);
                    continue;
                }
                spark.Velocity += Vector2.down * (14f * dt);
                spark.Velocity *= 1f - Mathf.Clamp01(2.5f * dt);
                spark.Transform.position += (Vector3)(spark.Velocity * dt);
                float k = 1 - spark.Age / spark.Life;
                spark.Transform.localScale = Vector3.one * (spark.Size * (0.4f + 0.6f * k));
                var c = spark.Color; c.a = k; spark.Renderer.color = c;
            }
        }
    }
}
