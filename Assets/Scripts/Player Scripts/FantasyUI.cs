using UnityEngine;
using UnityEngine.UI;

// Procedural gilded HUD art, generated once so the HUD needs no imported sprites.
public static class FantasyUI
{
    public static readonly Color Gold = new(0.88f, 0.7f, 0.38f);
    public static readonly Color Parchment = new(0.96f, 0.89f, 0.72f);
    public static readonly Color Muted = new(0.74f, 0.63f, 0.47f);
    public static readonly Color Inset = new(0.06f, 0.04f, 0.03f, 0.95f);
    private static readonly Color DarkGold = new(0.5f, 0.34f, 0.14f);
    private static readonly Color BrightGold = new(1f, 0.88f, 0.56f);
    private static Sprite frame, socket, glow, divider;

    public static Sprite Frame => frame != null ? frame : frame = Create("Gilded frame", 64, 64, new Vector4(22, 22, 22, 22), FramePixel);
    public static Sprite Socket => socket != null ? socket : socket = Create("Gem socket", 64, 64, Vector4.zero, SocketPixel);
    public static Sprite Glow => glow != null ? glow : glow = Create("Soft glow", 64, 64, Vector4.zero, GlowPixel);
    public static Sprite Divider => divider != null ? divider : divider = Create("Gilded divider", 128, 16, Vector4.zero, DividerPixel);

    public static Image Panel(RectTransform rect)
    {
        var image = rect.GetComponent<Image>();
        if (image == null) image = rect.gameObject.AddComponent<Image>();
        image.sprite = Frame;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static Color FramePixel(float x, float y, int width, int height)
    {
        float ex = Mathf.Min(x, width - 1 - x) + 0.5f, ey = Mathf.Min(y, height - 1 - y) + 0.5f;
        // Chamfered corners read as a carved plaque rather than an app card.
        float d = Mathf.Min(Mathf.Min(ex, ey), (ex + ey - 7f) * 0.7071f);
        if (d < 0f) return Color.clear;
        Color gold = Color.Lerp(DarkGold, BrightGold, y / (height - 1f));
        Color pixel = d < 1f ? new Color(0.02f, 0.01f, 0.005f, d)
            : d < 3.2f ? gold
            : d < 5f ? new Color(0.04f, 0.025f, 0.015f)
            : d < 6f ? Color.Lerp(gold, Color.black, 0.3f)
            // Opaque: in linear colour space even 5% of a bright sky tints this dark fill blue.
            : Color.Lerp(new Color(0.035f, 0.024f, 0.017f), new Color(0.13f, 0.09f, 0.06f), Mathf.Clamp01((d - 6f) / 12f));
        float stud = Mathf.Clamp01(3.6f - Mathf.Abs(ex - 11f) - Mathf.Abs(ey - 11f));
        return Color.Lerp(pixel, Color.Lerp(gold, Color.white, 0.2f), stud);
    }

    private static Color SocketPixel(float x, float y, int width, int height)
    {
        float r = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
        Color gold = Color.Lerp(DarkGold, BrightGold, y / (height - 1f));
        Color pixel = r > 30.5f ? new Color(0.02f, 0.01f, 0.005f, Mathf.Clamp01(31.8f - r))
            : r > 27f ? gold
            : r > 25.5f ? new Color(0.04f, 0.025f, 0.015f)
            : Color.Lerp(new Color(0.16f, 0.11f, 0.08f), new Color(0.03f, 0.02f, 0.015f), r / 25.5f);
        return pixel;
    }

    private static Color GlowPixel(float x, float y, int width, int height)
    {
        float r = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
        float a = Mathf.Clamp01(1f - r);
        return new Color(1f, 1f, 1f, a * a);
    }

    private static Color DividerPixel(float x, float y, int width, int height)
    {
        float cx = Mathf.Abs(x - 63.5f), cy = Mathf.Abs(y - 7.5f);
        float line = Mathf.Clamp01(1.6f - cy) * Mathf.Sin(Mathf.PI * x / (width - 1f));
        float gem = Mathf.Clamp01(6.5f - cx - cy);
        Color gold = Color.Lerp(DarkGold, BrightGold, 0.6f + 0.4f * Mathf.Clamp01(1f - cy / 6f));
        return new Color(gold.r, gold.g, gold.b, Mathf.Max(line, gem));
    }

    private static Sprite Create(string name, int width, int height, Vector4 border, System.Func<float, float, int, int, Color> pixel)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = pixel(x, y, width, height);
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
