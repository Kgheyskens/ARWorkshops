using UnityEngine;

// UI-sprites in code.
public static class UIStyle
{
    private static Sprite rounded, roundedOutline, ring, circle;
    private static Texture2D scanlines, vignette;

    // Afgerond vierkant (sliced).
    public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded(128, 32f, 0f));

    // Rand van afgerond vierkant.
    public static Sprite RoundedOutline => roundedOutline != null ? roundedOutline : (roundedOutline = MakeRounded(128, 32f, 4f));

    // Dunne ring.
    public static Sprite Ring => ring != null ? ring : (ring = MakeRing(256, 3f));

    // Gevulde cirkel.
    public static Sprite Circle => circle != null ? circle : (circle = MakeRing(64, 0f));

    public static Texture2D Scanlines => scanlines != null ? scanlines : (scanlines = MakeScanlines());
    public static Texture2D Vignette => vignette != null ? vignette : (vignette = MakeVignette(128));

    // Verticaal verloop.
    public static Texture2D Gradient(Color bottom, Color top)
    {
        var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 64; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
        tex.Apply();
        return tex;
    }

    // ------------------------------------------------------------------ Generatoren

    private static Sprite MakeRounded(int size, float radius, float outline)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Signed distance tot de rand van een afgerond vierkant (negatief = binnen)
                float half = size / 2f;
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                float alpha = Mathf.Clamp01(0.5f - d); // zachte (anti-aliased) rand
                if (outline > 0f) alpha *= Mathf.Clamp01(d + outline + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        float b = radius;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
    }

    private static Sprite MakeRing(int size, float thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        float r = size / 2f - 1f;
        var c = new Vector2(size / 2f, size / 2f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a = thickness <= 0f
                    ? Mathf.Clamp01(r - d + 0.5f)
                    : Mathf.Clamp01(thickness * 0.5f - Mathf.Abs(d - (r - thickness)) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Texture2D MakeScanlines()
    {
        var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.35f));
        tex.SetPixel(0, 1, new Color(0f, 0f, 0f, 0f));
        tex.SetPixel(0, 2, new Color(0f, 0f, 0f, 0f));
        tex.SetPixel(0, 3, new Color(0f, 0f, 0f, 0f));
        tex.Apply();
        return tex;
    }

    private static Texture2D MakeVignette(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var c = new Vector2(size / 2f, size / 2f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / (size * 0.5f);
                float a = Mathf.SmoothStep(0f, 0.75f, Mathf.Clamp01((d - 0.55f) / 0.6f));
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        tex.Apply();
        return tex;
    }
}
