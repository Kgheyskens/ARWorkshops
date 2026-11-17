using System.Collections.Generic;
using UnityEngine;

// Hulpfuncties: materialen, vormen, texturen, deeltjes.
public static class Gfx
{
    public enum Blend { Alpha, Additive }

    private static Material toon, mask;
    private static readonly Dictionary<Blend, Material> unlit = new Dictionary<Blend, Material>();
    private static Texture2D softDot, sparkle;
    private static MaterialPropertyBlock mpb;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int GlowId = Shader.PropertyToID("_Glow");
    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int VertexColorId = Shader.PropertyToID("_UseVertexColor");

    // ------------------------------------------------------------------ Materialen

    private static Shader Find(string name)
    {
        Shader s = Shader.Find(name);
        if (s == null) Debug.LogError($"[Gfx] Shader '{name}' niet gevonden. Staan de .shader-bestanden in een map 'Resources'?");
        return s;
    }

    // Gedeeld toon-materiaal.
    public static Material Toon => toon != null ? toon : (toon = new Material(Find("GenieBox/Toon")) { name = "GenieToon" });

    // Depth mask-materiaal.
    public static Material DepthMask => mask != null ? mask : (mask = new Material(Find("GenieBox/DepthMask")) { name = "GenieDepthMask" });

    // Gedeeld onverlicht materiaal.
    public static Material Unlit(Blend blend)
    {
        if (unlit.TryGetValue(blend, out Material m) && m != null) return m;
        m = NewUnlit(blend, null);
        unlit[blend] = m;
        return m;
    }

    // Nieuw onverlicht materiaal.
    public static Material NewUnlit(Blend blend, Texture tex, int queueOffset = 0)
    {
        var m = new Material(Find("GenieBox/Unlit")) { name = "GenieUnlit_" + blend };
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", blend == Blend.Additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.renderQueue = 3000 + queueOffset;
        if (tex != null) m.SetTexture(MainTexId, tex);
        return m;
    }

    // Nieuw toon-materiaal met textuur.
    public static Material NewToon(Texture tex)
    {
        var m = new Material(Toon);
        if (tex != null) m.SetTexture(MainTexId, tex);
        return m;
    }

    // ------------------------------------------------------------------ Kleur per object

    public static void SetColor(Renderer r, Color color, float glow = 0f, Color? glowColor = null)
    {
        if (r == null) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        mpb.SetColor(ColorId, color);
        mpb.SetFloat(GlowId, glow);
        mpb.SetColor(GlowColorId, glowColor ?? color);
        r.SetPropertyBlock(mpb);
    }

    public static void UseVertexColors(Renderer r)
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        mpb.SetFloat(VertexColorId, 1f);
        r.SetPropertyBlock(mpb);
    }

    // ------------------------------------------------------------------ Prefabs maken (editor)

    // Bake-modus: echte materialen i.p.v. property blocks.
    public static bool BakeMode;
    public static readonly Dictionary<string, Material> BakedMaterials = new Dictionary<string, Material>();

    private static Material BakeMaterial(Color color, float glow, bool vertexColors = false)
    {
        string key = ColorUtility.ToHtmlStringRGB(color) + "_" + Mathf.RoundToInt(glow * 100f) + (vertexColors ? "_vc" : "");
        if (BakedMaterials.TryGetValue(key, out Material m) && m != null) return m;
        m = new Material(Find("GenieBox/Toon")) { name = "Toon_" + key };
        m.SetColor(ColorId, color);
        m.SetColor(GlowColorId, color);
        m.SetFloat(GlowId, glow);
        m.SetFloat(VertexColorId, vertexColors ? 1f : 0f);
        BakedMaterials[key] = m;
        return m;
    }

    private static void DestroySafe(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Object.Destroy(o);
        else Object.DestroyImmediate(o);
    }

    // ------------------------------------------------------------------ Vormen

    // Primitieve vorm met kleur.
    public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color, float glow = 0f, string name = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        if (!string.IsNullOrEmpty(name)) go.name = name;
        DestroySafe(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        var r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (BakeMode) r.sharedMaterial = BakeMaterial(color, glow); // voor prefabs: echte materialen
        else { r.sharedMaterial = Toon; SetColor(r, color, glow); }
        return go;
    }

    // Object met eigen mesh.
    public static GameObject MeshObj(Mesh mesh, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale,
                                     Color color, float glow = 0f, string name = "Mesh", bool vertexColors = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (BakeMode) r.sharedMaterial = BakeMaterial(color, glow, vertexColors);
        else
        {
            r.sharedMaterial = Toon;
            SetColor(r, color, glow);
            if (vertexColors) UseVertexColors(r);
        }
        return go;
    }

    // Onverlichte quad.
    public static GameObject Quad(Transform parent, Vector3 localPos, Quaternion localRot, Vector2 size, Material mat, string name = "Quad")
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        DestroySafe(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    // Lege container.
    public static Transform Group(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    // Rotatie om een quad plat te leggen.
    public static Quaternion FaceUp => Quaternion.Euler(90f, 0f, 0f);

    // ------------------------------------------------------------------ Texturen

    // Zachte vlek.
    public static Texture2D SoftDot => softDot != null ? softDot : (softDot = MakeDot(64, 2.2f));

    // Sterretje.
    public static Texture2D Sparkle => sparkle != null ? sparkle : (sparkle = MakeSparkle(64));

    private static Texture2D ring;

    // Dunne ring.
    public static Texture2D Ring
    {
        get
        {
            if (ring != null) return ring;
            int size = 128;
            ring = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Ring" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) * 9f) + Mathf.Clamp01(1f - d) * 0.25f;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            ring.SetPixels32(px);
            ring.Apply();
            return ring;
        }
    }

    private static Texture2D MakeDot(int size, float falloff)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftDot" };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), falloff);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private static Texture2D MakeSparkle(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Sparkle" };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) / size * 2f - 1f), dy = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                float star = Mathf.Clamp01(1f - (dx * dy * 18f + Mathf.Sqrt(dx * dx + dy * dy) * 0.9f));
                float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 3f);
                float a = Mathf.Clamp01(star * star + core);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // ------------------------------------------------------------------ Deeltjes

    // Deeltjessysteem dat meebeweegt met 'space'.
    public static ParticleSystem Particles(string name, Transform parent, Transform space, Texture2D tex, Blend blend,
                                           float lifetime, float size, float speed, Color color, float gravity = 0f, int max = 400)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = true; // speelt ook als het object pas later actief wordt (emissie staat op 0)
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.maxParticles = max;
        main.simulationSpace = space != null ? ParticleSystemSimulationSpace.Custom : ParticleSystemSimulationSpace.Local;
        if (space != null) main.customSimulationSpace = space;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.005f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = NewUnlit(blend, tex, 10);
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
        return ps;
    }

    // Deeltjes uitstoten op een plek.
    public static void Burst(ParticleSystem ps, Vector3 worldPos, Vector3 worldVelocity, int count, float spread, Color? color = null, float sizeMul = 1f)
    {
        if (ps == null) return;
        var main = ps.main;
        float baseSize = main.startSize.constantMax;
        float life = main.startLifetime.constantMax;
        // Positie en snelheid omzetten naar de simulatieruimte van het systeem
        Transform space = main.simulationSpace == ParticleSystemSimulationSpace.Custom ? main.customSimulationSpace : ps.transform;
        for (int i = 0; i < count; i++)
        {
            var p = new ParticleSystem.EmitParams();
            Vector3 v = worldVelocity + Random.insideUnitSphere * spread;
            p.position = space != null ? space.InverseTransformPoint(worldPos) : worldPos;
            p.velocity = space != null ? space.InverseTransformVector(v) : v;
            p.startSize = baseSize * Random.Range(0.5f, 1f) * sizeMul;
            p.startLifetime = life * Random.Range(0.6f, 1f);
            if (color.HasValue) p.startColor = color.Value;
            p.applyShapeToPosition = false;
            ps.Emit(p, 1);
        }
    }
}
