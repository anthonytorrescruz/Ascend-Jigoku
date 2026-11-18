using UnityEngine;

// Short-lived flash rectangle for weapon abilities.
public sealed class WeaponFx : MonoBehaviour
{
    private static Sprite sprite;
    private SpriteRenderer render;
    private Vector2 from, to;
    private Color color;
    private float life, age; // seconds

    // Spawns a rectangle at center that grows from size 'from' to 'to' while fading out over 'life' seconds.
    public static void Burst(Vector2 center, Vector2 from, Vector2 to, Color color, float life, int order)
    {
        if (sprite == null) sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
        var go = new GameObject("Weapon Fx");
        go.transform.position = center;
        var fx = go.AddComponent<WeaponFx>();
        fx.render = go.AddComponent<SpriteRenderer>();
        fx.render.sprite = sprite;
        fx.render.sortingOrder = order;
        fx.from = from;
        fx.to = to;
        fx.color = color;
        fx.life = life;
        if (AscendJigoku.Settings.GameSettings.ReduceFlashing) fx.color.a *= 0.4f;
        fx.Apply(0);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= life)
        {
            Destroy(gameObject);
            return;
        }
        Apply(age / life);
    }

    private void Apply(float t)
    {
        Vector2 size = Vector2.Lerp(from, to, 1 - (1 - t) * (1 - t));
        transform.localScale = new Vector3(size.x, size.y, 1);
        render.color = new Color(color.r, color.g, color.b, color.a * (1 - t));
    }
}
