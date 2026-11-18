using UnityEngine;

// Two tiny shared silhouette masks, generated once and reused by every pooled effect.
internal static class SpellShapes
{
    private static Sprite circle, crescent;
    public static Sprite Circle => circle != null ? circle : circle = Build(false);
    public static Sprite Crescent => crescent != null ? crescent : crescent = Build(true);

    private static Sprite Build(bool arc)
    {
        // Draws the shape into a resolution x resolution texture. The crescent is the strip between two curves; the circle is a disc.
        const int resolution = 128;
        var pixels = new Color32[resolution * resolution];
        for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                float coverage = 0;
                // Four samples soften the curve's edge without allocating a large texture.
                for (int sample = 0; sample < 4; sample++)
                {
                    float px = (x + .25f + (sample % 2) * .5f) / resolution - .5f;
                    float py = (y + .25f + (sample / 2) * .5f) / resolution - .5f;
                    if (arc)
                    {
                        float curve = Mathf.Sqrt(Mathf.Max(0, 1 - 4 * py * py));
                        if (px <= curve - .5f && px >= curve * .35f - .5f) coverage += .25f;
                    }
                    else if (px * px + py * py <= .25f) coverage += .25f;
                }
                pixels[y * resolution + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255));
            }
        string name = arc ? "Silver Crescent" : "Silver Healing Circle";
        var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false) {
            name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, resolution, resolution), Vector2.one * .5f, resolution, 0,
            SpriteMeshType.FullRect);
        sprite.name = name;
        return sprite;
    }
}
