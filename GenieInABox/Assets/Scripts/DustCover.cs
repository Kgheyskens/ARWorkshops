using System.Collections;
using UnityEngine;

// Fase 1: stof wegblazen van de doos met de microfoon.
public class DustCover : MonoBehaviour
{
    [Header("Stof")]
    public int textureWidth = 160;
    [Tooltip("Hoe snel het stof verdwijnt bij hard blazen")]
    public float eraseSpeed = 2.2f;
    [Tooltip("Straal van de blaasplek (meter) bij zacht en hard blazen")]
    public float minBrush = 0.025f, maxBrush = 0.05f;
    [Tooltip("Vanaf dit percentage waait de rest vanzelf weg")]
    [Range(0.5f, 1f)] public float autoClearAt = 0.9f;
    public Color dustColor = new Color(0.62f, 0.56f, 0.48f);

    // Mag er geblazen worden?
    public bool Active { get; set; }
    // 0 = stoffig, 1 = schoon.
    public float Cleared { get; private set; }
    // Richtpunt op de doos?
    public bool AimOnBox { get; private set; }
    public bool Finished { get; private set; }

    private GenieBoxRig rig;
    private MicBlow mic;
    private SoundFx sfx;
    private Camera cam;

    private int texW, texH;
    private float[] mask;          // 1 = stof, 0 = weg
    private Color32[] basePixels;  // kleur + dekking van het stof
    private Color32[] pixels;
    private Texture2D tex;
    private GameObject dustQuad, reticle;
    private Material reticleMat;
    private ParticleSystem dustCloud;
    private float maskSum;
    private bool dirty;

    private void Start()
    {
        rig = GetComponentInParent<GenieBoxRig>();
        mic = FindFirstObjectByType<MicBlow>();
        sfx = FindFirstObjectByType<SoundFx>();
        cam = Camera.main;
        Build();
        ResetDust();
    }

    private void Build()
    {
        texW = textureWidth;
        texH = Mathf.RoundToInt(textureWidth * rig.Length / rig.Width);
        mask = new float[texW * texH];
        pixels = new Color32[texW * texH];
        basePixels = PaintDust(texW, texH);

        tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Dust" };
        var mat = Gfx.NewUnlit(Gfx.Blend.Alpha, tex);
        dustQuad = Gfx.Quad(transform, rig.FrontPoint(0.5f, 0.5f, 0.0045f), Gfx.FaceUp, new Vector2(rig.Width, rig.Length), mat, "DustLayer");

        reticleMat = Gfx.NewUnlit(Gfx.Blend.Additive, MakeRing(64), 20);
        reticle = Gfx.Quad(transform, Vector3.zero, Gfx.FaceUp, Vector2.one * 0.06f, reticleMat, "Reticle");
        reticle.SetActive(false);

        // Stofwolk: zachte vlekjes die opzij wegwaaien, afremmen (luchtweerstand), uitdijen en
        // langzaam weer wat zakken - zoals echt stof, niet als rook die omhoog stijgt.
        dustCloud = Gfx.Particles("DustCloud", transform, rig.transform, Gfx.SoftDot, Gfx.Blend.Alpha,
                                  1.8f, 0.03f, 0.1f, new Color(0.7f, 0.64f, 0.55f, 0.6f), 0.012f, 1200);
        var lv = dustCloud.limitVelocityOverLifetime;
        lv.enabled = true;
        lv.drag = new ParticleSystem.MinMaxCurve(2.2f);
        lv.multiplyDragByParticleSize = false;
        lv.multiplyDragByParticleVelocity = false;
        var sol = dustCloud.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
    }

    public void ResetDust()
    {
        for (int i = 0; i < mask.Length; i++) mask[i] = 1f;
        maskSum = mask.Length;
        Cleared = 0f;
        Finished = false;
        dustQuad.SetActive(true);
        var r = dustQuad.GetComponent<Renderer>();
        r.sharedMaterial.color = Color.white;
        dirty = true;
    }

    // Gemiddeld stof rond uv.
    public float CoverAt(Vector2 uv, float radiusUV = 0.04f)
    {
        int cx = Mathf.RoundToInt(uv.x * (texW - 1)), cy = Mathf.RoundToInt(uv.y * (texH - 1));
        int r = Mathf.Max(1, Mathf.RoundToInt(radiusUV * texW));
        float sum = 0f; int n = 0;
        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= texW || y >= texH) continue;
                sum += mask[y * texW + x]; n++;
            }
        return n > 0 ? sum / n : 0f;
    }

    private void Update()
    {
        if (cam == null) cam = Camera.main;
        AimOnBox = false;

        if (Active && !Finished && cam != null)
        {
            float strength = mic != null ? mic.Strength : 0f;
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (rig.RayToFront(ray, out Vector3 hit))
            {
                Vector2 uv = rig.FrontUV(hit);
                AimOnBox = uv.x > -0.05f && uv.x < 1.05f && uv.y > -0.05f && uv.y < 1.05f;
                float brush = Mathf.Lerp(minBrush, maxBrush, strength);

                reticle.SetActive(AimOnBox);
                if (AimOnBox)
                {
                    reticle.transform.localPosition = new Vector3(hit.x, rig.TopY + 0.006f, hit.z);
                    float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f);
                    reticle.transform.localScale = Vector3.one * brush * 2.2f * pulse;
                    reticleMat.color = Color.Lerp(new Color(1f, 0.9f, 0.6f, 0.5f), new Color(1f, 1f, 1f, 1f), strength);
                }

                if (AimOnBox && strength > 0.05f)
                {
                    float removed = Erase(uv, brush / rig.Width, strength * eraseSpeed * Time.deltaTime);
                    EmitDust(hit, strength, removed);
                }
            }
            else reticle.SetActive(false);

            if (sfx != null) sfx.SetWind(AimOnBox ? strength : strength * 0.4f);

            if (Cleared >= autoClearAt) StartCoroutine(BlowAwayRest());
        }
        else if (reticle.activeSelf) reticle.SetActive(false);

        if (dirty)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = basePixels[i];
                c.a = (byte)(c.a * mask[i]);
                pixels[i] = c;
            }
            tex.SetPixels32(pixels);
            tex.Apply(false);
            dirty = false;
        }
    }

    // Gumt stof weg rond uv; geeft weggehaalde hoeveelheid terug.
    private float Erase(Vector2 uv, float radiusU, float amount)
    {
        int cx = Mathf.RoundToInt(uv.x * (texW - 1)), cy = Mathf.RoundToInt(uv.y * (texH - 1));
        int r = Mathf.Max(2, Mathf.RoundToInt(radiusU * texW));
        float removed = 0f;
        for (int y = cy - r; y <= cy + r; y++)
        {
            if (y < 0 || y >= texH) continue;
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || x >= texW) continue;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                if (d > 1f) continue;
                int i = y * texW + x;
                float before = mask[i];
                // Wat ruis in de rand zodat het er "stoffig" en niet als een perfecte cirkel uitziet
                float falloff = (1f - d * d) * (0.6f + 0.4f * Mathf.PerlinNoise(x * 0.3f + Time.time, y * 0.3f));
                mask[i] = Mathf.Max(0f, before - amount * falloff);
                removed += before - mask[i];
            }
        }
        if (removed > 0f)
        {
            maskSum -= removed;
            Cleared = 1f - maskSum / mask.Length;
            dirty = true;
        }
        return removed;
    }

    private float emitCarry;

    private void EmitDust(Vector3 local, float strength, float removed)
    {
        emitCarry += removed * 0.6f + strength * 0.5f;
        int count = Mathf.Min(20, Mathf.FloorToInt(emitCarry));
        emitCarry -= count;
        if (count <= 0) return;

        Vector3 dir = BlowDirection();
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        Vector3 origin = rig.transform.TransformPoint(local + Vector3.up * 0.004f);
        for (int i = 0; i < count; i++)
        {
            // vooral VAN JE AF langs het oppervlak, met een waaier opzij en amper omhoog
            Vector3 v = dir * Random.Range(0.25f, 0.45f + 0.35f * strength)
                      + side * Random.Range(-0.18f, 0.18f)
                      + Vector3.up * Random.Range(0.005f, 0.03f);
            Gfx.Burst(dustCloud, origin, rig.transform.TransformVector(v), 1, 0.01f);
        }
    }

    // Blaasrichting: kijkrichting van de gsm in het doosvlak.
    private Vector3 BlowDirection()
    {
        Vector3 f = rig.transform.InverseTransformDirection(cam.transform.forward);
        f.y = 0f;
        if (f.magnitude < 0.3f)
        {
            Vector3 up = rig.transform.InverseTransformDirection(cam.transform.up);
            up.y = 0f;
            if (up.magnitude > f.magnitude) f = up;
        }
        return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
    }

    // Laatste stof waait weg.
    private IEnumerator BlowAwayRest()
    {
        Finished = true;
        if (sfx != null) { sfx.Play("whoosh", 0.8f); sfx.SetWind(0f); }
        float[] start = (float[])mask.Clone();
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
        {
            for (int i = 0; i < mask.Length; i++) mask[i] = start[i] * (1f - t);
            // stofwolkjes over het hele oppervlak
            Vector3 p = rig.FrontPoint(Random.value, Random.value, 0.005f);
            Vector3 gust = (cam != null ? BlowDirection() : Vector3.forward) * 0.4f + Vector3.up * 0.02f;
            Gfx.Burst(dustCloud, rig.transform.TransformPoint(p), rig.transform.TransformVector(gust), 3, 0.06f);
            dirty = true;
            yield return null;
        }
        for (int i = 0; i < mask.Length; i++) mask[i] = 0f;
        maskSum = 0f;
        Cleared = 1f;
        dirty = true;
        yield return null;
        dustQuad.SetActive(false);
    }

    // ------------------------------------------------------------------ Textuur tekenen

    // Stoftextuur met ruis en spinnenwebben.
    private Color32[] PaintDust(int w, int h)
    {
        var px = new Color32[w * h];
        float seed = Random.Range(0f, 100f);
        var web = new float[w * h];
        // Spinnenwebben in twee of drie hoeken
        DrawWeb(web, w, h, 0, 0, 1, 1);
        DrawWeb(web, w, h, w - 1, h - 1, -1, -1);
        if (Random.value < 0.6f) DrawWeb(web, w, h, w - 1, 0, -1, 1);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float n = Fbm(x * 0.05f + seed, y * 0.05f + seed);
                float clumps = Mathf.Pow(Mathf.PerlinNoise(x * 0.18f + seed * 2f, y * 0.18f), 4f) * 1.8f;
                float shade = 0.75f + 0.35f * n - clumps * 0.25f;
                Color c = dustColor * shade;
                float alpha = Mathf.Clamp01(0.8f + 0.18f * n + clumps * 0.2f);
                float wv = web[y * w + x];
                c = Color.Lerp(c, new Color(0.92f, 0.92f, 0.9f), wv * 0.8f);
                alpha = Mathf.Max(alpha, wv);
                // De rand van de doos iets minder stoffig
                float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                alpha *= Mathf.Clamp01(edge / 3f + 0.3f);
                px[y * w + x] = new Color(c.r, c.g, c.b, alpha);
            }
        return px;
    }

    private static float Fbm(float x, float y)
    {
        float sum = 0f, amp = 0.5f, f = 1f;
        for (int i = 0; i < 4; i++) { sum += (Mathf.PerlinNoise(x * f, y * f) - 0.5f) * amp; amp *= 0.5f; f *= 2.1f; }
        return sum * 2f;
    }

    private static void DrawWeb(float[] web, int w, int h, int cx, int cy, int sx, int sy)
    {
        int spokes = 6;
        float radius = Mathf.Min(w, h) * 0.45f;
        var ends = new Vector2[spokes];
        for (int s = 0; s < spokes; s++)
        {
            float a = Mathf.Lerp(4f, 86f, s / (float)(spokes - 1)) * Mathf.Deg2Rad;
            ends[s] = new Vector2(Mathf.Cos(a) * sx, Mathf.Sin(a) * sy);
            Line(web, w, h, new Vector2(cx, cy), new Vector2(cx, cy) + ends[s] * radius * Random.Range(0.8f, 1f), 0.75f);
        }
        for (int ring = 1; ring <= 5; ring++)
        {
            float r = radius * ring / 5.5f;
            for (int s = 0; s < spokes - 1; s++)
            {
                Vector2 a = new Vector2(cx, cy) + ends[s] * r, b = new Vector2(cx, cy) + ends[s + 1] * r;
                // een beetje doorhangen naar de hoek toe
                Vector2 mid = (a + b) * 0.5f + (new Vector2(cx, cy) - (a + b) * 0.5f).normalized * r * 0.08f;
                Line(web, w, h, a, mid, 0.55f);
                Line(web, w, h, mid, b, 0.55f);
            }
        }
    }

    private static void Line(float[] buf, int w, int h, Vector2 a, Vector2 b, float strength)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f);
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)Mathf.Max(1, steps));
            int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
            if (x < 0 || y < 0 || x >= w || y >= h) continue;
            buf[y * w + x] = Mathf.Max(buf[y * w + x], strength);
        }
    }

    private static Texture2D MakeRing(int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Ring" };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) * 14f) + Mathf.Clamp01(1f - d * 8f) * 0.8f;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }
}
