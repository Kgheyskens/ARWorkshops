using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Wens: vliegend tapijt.
public class MagicCarpet : MonoBehaviour
{
    public const float Length = 0.5f, Width = 0.32f;
    private const int NX = 20, NZ = 32; // rasterpunten (breedte x lengte)

    private Mesh cloth;
    private Vector3[] baseVerts, verts;
    private readonly List<Transform> tassels = new List<Transform>();
    private readonly List<int> tasselVert = new List<int>();
    private Transform rider, riderArmL, riderArmR, riderHead;
    private ParticleSystem glitter;
    private Transform fxRoot;
    private Camera cam;
    private Vector3 worldUp = Vector3.up, lastPos;
    private float rippleSpeed = 7f, rippleAmp = 0.012f, roll;

    // ================================================================== Model

    // Bouwt het tapijtmodel.
    public static Transform BuildModel(Transform parent)
    {
        Transform root = Gfx.Group("MagicCarpet", parent);
        var mesh = BuildClothMesh();
        var mat = Gfx.NewUnlit(Gfx.Blend.Alpha, PatternTexture(), 0);
        mat.name = "Carpet";
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetFloat("_ZWrite", 1f);
        mat.renderQueue = 2000;
        var go = new GameObject("Cloth");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        // kwastjes: 7 aan de voorkant, 7 aan de achterkant
        Color gold = new Color(1f, 0.8f, 0.3f);
        for (int end = 0; end < 2; end++)
            for (int i = 0; i < 7; i++)
            {
                var t = Gfx.Group($"Tassel{end}_{i}", root);
                Gfx.Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 0.009f, gold, 0.8f, "Knot");
                Gfx.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0f, 0.018f), new Vector3(0.006f, 0.014f, 0.006f), gold, 0.6f, "Threads")
                   .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                t.localPosition = TasselRest(end, i);
                t.localRotation = Quaternion.Euler(0f, end == 0 ? 0f : 180f, 0f);
            }
        return root;
    }

    private static Vector3 TasselRest(int end, int i)
    {
        float x = Mathf.Lerp(-Width * 0.42f, Width * 0.42f, i / 6f);
        return new Vector3(x, 0f, end == 0 ? Length * 0.5f : -Length * 0.5f);
    }

    private static Mesh BuildClothMesh()
    {
        var v = new Vector3[NX * NZ];
        var uv = new Vector2[NX * NZ];
        for (int z = 0; z < NZ; z++)
            for (int x = 0; x < NX; x++)
            {
                float u = z / (float)(NZ - 1), w = x / (float)(NX - 1);
                v[z * NX + x] = new Vector3((w - 0.5f) * Width, 0f, (u - 0.5f) * Length);
                uv[z * NX + x] = new Vector2(u, w);
            }
        var tri = new List<int>();
        for (int z = 0; z < NZ - 1; z++)
            for (int x = 0; x < NX - 1; x++)
            {
                int a = z * NX + x, b = a + 1, c = a + NX, d = c + 1;
                tri.AddRange(new[] { a, c, b, b, c, d });
            }
        var m = new Mesh { name = "CarpetCloth" };
        m.vertices = v;
        m.uv = uv;
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // Tapijtpatroon (u = lengte, v = breedte).
    public static Texture2D PatternTexture()
    {
        const int W = 320, H = 204;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "CarpetPattern", wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[W * H];
        Color indigo = new Color(0.16f, 0.08f, 0.38f), indigoLight = new Color(0.27f, 0.16f, 0.52f);
        Color crimson = new Color(0.62f, 0.08f, 0.16f), gold = new Color(1f, 0.78f, 0.28f), darkGold = new Color(0.7f, 0.48f, 0.12f);
        Color turq = new Color(0.2f, 0.75f, 0.72f);
        for (int py = 0; py < H; py++)
            for (int pxi = 0; pxi < W; pxi++)
            {
                float u = (pxi + 0.5f) / W, v = (py + 0.5f) / H;
                float x = (u - 0.5f) * Length, y = (v - 0.5f) * Width; // meter, vanuit het midden
                float d = Mathf.Min(Mathf.Min(u * Length, (1f - u) * Length), Mathf.Min(v * Width, (1f - v) * Width));
                Color c;
                if (d < 0.004f) c = darkGold;
                else if (d < 0.032f)
                {
                    // zigzag langs de rand
                    float s = (u * Length < 0.032f || (1f - u) * Length < 0.032f) ? y : x;
                    float zig = Mathf.Abs(Mathf.Repeat(s / 0.02f, 2f) - 1f) * 0.016f + 0.01f;
                    c = Mathf.Abs(d - zig) < 0.0024f ? gold : crimson;
                }
                else if (d < 0.036f) c = gold;
                else if (d < 0.040f) c = indigo;
                else if (d < 0.043f) c = turq;
                else
                {
                    // veld met ruitjes
                    float a = (x + y) / 0.045f, b = (x - y) / 0.045f;
                    float fa = a - Mathf.Floor(a), fb = b - Mathf.Floor(b);
                    c = fa < 0.07f || fb < 0.07f ? indigoLight : indigo;
                    if (Mathf.Abs(fa - 0.5f) < 0.09f && Mathf.Abs(fb - 0.5f) < 0.09f) c = turq * 0.85f;
                    // hoekornamenten
                    float cx = Length * 0.5f - 0.043f, cy = Width * 0.5f - 0.043f;
                    float cd = new Vector2(Mathf.Abs(x) - cx, Mathf.Abs(y) - cy).magnitude;
                    if (cd < 0.04f) c = cd > 0.033f ? gold : cd > 0.026f ? crimson : cd > 0.02f ? gold : indigo;
                    // ster in het midden
                    float r = Mathf.Sqrt(x * x + y * y), th = Mathf.Atan2(y, x);
                    float star = 0.052f + 0.034f * Mathf.Pow(0.5f + 0.5f * Mathf.Cos(8f * th), 3f);
                    if (r < star + 0.005f)
                    {
                        c = r > star ? darkGold : gold;
                        if (r < star - 0.008f) c = crimson;
                        if (Mathf.Abs(x) + Mathf.Abs(y) < 0.036f) c = turq;
                        if (r < 0.012f) c = gold;
                    }
                    else if (r < star + 0.012f && r > star + 0.008f) c = turq;
                }
                // een beetje stofstructuur (geweven draad)
                float weave = 0.93f + 0.07f * Mathf.Sin(pxi * 1.7f) * Mathf.Sin(py * 1.9f);
                c *= weave;
                c.a = 1f;
                px[py * W + pxi] = c;
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // ================================================================== Animatie

    // Maakt een tapijt met de geest erop.
    public static MagicCarpet Spawn(GameObject prefab, Genie genie)
    {
        Transform root = prefab != null ? Instantiate(prefab).transform : BuildModel(null);
        root.name = "MagicCarpet";
        var c = root.gameObject.AddComponent<MagicCarpet>();
        c.Init(genie);
        return c;
    }

    private void Init(Genie genie)
    {
        cam = Camera.main;
        // eigen kopie van de mesh, want die gaat golven
        var mf = transform.Find("Cloth").GetComponent<MeshFilter>();
        cloth = Instantiate(mf.sharedMesh);
        cloth.MarkDynamic();
        mf.sharedMesh = cloth;
        baseVerts = cloth.vertices;
        verts = new Vector3[baseVerts.Length];

        for (int end = 0; end < 2; end++)
            for (int i = 0; i < 7; i++)
            {
                var t = transform.Find($"Tassel{end}_{i}");
                if (t == null) continue;
                tassels.Add(t);
                // dichtstbijzijnde rasterpunt aan de rand
                Vector3 rest = TasselRest(end, i);
                int best = 0; float bd = float.MaxValue;
                for (int k = 0; k < baseVerts.Length; k++)
                {
                    float dd = (baseVerts[k] - rest).sqrMagnitude;
                    if (dd < bd) { bd = dd; best = k; }
                }
                tasselVert.Add(best);
            }

        // de geest zit erop (een kopie van zijn model), een beetje naar achter
        if (genie != null)
        {
            rider = genie.CreateRiderCopy(transform);
            rider.localPosition = new Vector3(0f, 0.004f, -0.04f);
            rider.localRotation = Quaternion.identity;
            foreach (var t in rider.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "ArmL") riderArmL = t;
                else if (t.name == "ArmR") riderArmR = t;
                else if (t.name == "Head") riderHead = t;
            }
        }

        fxRoot = new GameObject("CarpetFx").transform;
        glitter = Gfx.Particles("CarpetGlitter", fxRoot, null, Gfx.Sparkle, Gfx.Blend.Additive, 1.2f, 0.022f, 0.03f, new Color(1f, 0.85f, 0.45f), 0.04f, 2500);
        lastPos = transform.position;
    }

    private void Update()
    {
        if (cloth == null) return;
        float t = Time.time;
        // golvend doek: golf van voor naar achter, randen hangen iets door, voorkant krult op
        for (int k = 0; k < baseVerts.Length; k++)
        {
            Vector3 b = baseVerts[k];
            float zf = b.z / Length + 0.5f;          // 0 = achter, 1 = voor
            float xf = Mathf.Abs(b.x) / (Width * 0.5f);
            float wave = Mathf.Sin(b.z * 22f - t * rippleSpeed) * rippleAmp * (0.35f + 0.65f * (1f - zf));
            wave += Mathf.Sin(b.x * 30f + t * 4f) * 0.002f;
            float curl = zf > 0.85f ? Mathf.Pow((zf - 0.85f) / 0.15f, 2f) * 0.022f : 0f;
            float droop = -Mathf.Pow(xf, 3f) * 0.008f;
            verts[k] = new Vector3(b.x, wave + curl + droop, b.z);
        }
        cloth.vertices = verts;
        cloth.RecalculateBounds();

        // kwastjes volgen de rand en wapperen
        for (int i = 0; i < tassels.Count; i++)
        {
            Vector3 p = verts[tasselVert[i]];
            tassels[i].localPosition = p;
            bool front = i < 7;
            tassels[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 6f + i) * 15f + (front ? 25f : 15f), front ? 0f : 180f, Mathf.Sin(t * 4f + i * 0.7f) * 8f);
        }

        // de geest: zwaait, houdt zich vast en kijkt naar jou
        if (riderArmR != null) riderArmR.localRotation = Quaternion.Euler(0f, 0f, 130f + Mathf.Sin(t * 10f) * 25f);
        if (riderArmL != null) riderArmL.localRotation = Quaternion.Euler(10f, 0f, -35f);
        if (riderHead != null && cam != null)
        {
            Vector3 toCam = rider.InverseTransformDirection(cam.transform.position - riderHead.position);
            float yaw = Mathf.Clamp(Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg, -80f, 80f);
            float pitch = Mathf.Clamp(-Mathf.Atan2(toCam.y, new Vector2(toCam.x, toCam.z).magnitude) * Mathf.Rad2Deg, -35f, 30f);
            riderHead.localRotation = Quaternion.Slerp(riderHead.localRotation, Quaternion.Euler(pitch, yaw, 0f), Time.deltaTime * 6f);
        }

        // glinsterspoor achter het tapijt
        Vector3 vel = Time.deltaTime > 0f ? (transform.position - lastPos) / Time.deltaTime : Vector3.zero;
        lastPos = transform.position;
        float sc = transform.lossyScale.x;
        for (int i = 0; i < 2; i++)
        {
            Vector3 back = transform.TransformPoint(new Vector3(Random.Range(-Width, Width) * 0.45f, 0f, -Length * 0.5f));
            Gfx.Burst(glitter, back, -vel * 0.1f, 1, 0.02f * sc, Random.value < 0.3f ? new Color(0.5f, 0.9f, 1f) : (Color?)null, sc);
        }
    }

    // De vlucht rond de kamer.
    public IEnumerator Fly(Transform box, Vector3 up, SoundFx sfx)
    {
        worldUp = up;
        if (cam == null) cam = Camera.main;
        Vector3 boxTop = box.position + up * 0.05f;
        Vector3 toCam = Vector3.ProjectOnPlane(cam.transform.position - boxTop, up);
        Vector3 f0 = toCam.sqrMagnitude > 0.001f ? toCam.normalized : Vector3.ProjectOnPlane(box.forward, up).normalized;
        transform.position = boxTop;
        transform.rotation = Quaternion.LookRotation(f0, up);
        transform.localScale = Vector3.one * 0.05f;

        // Opstijgen: het tapijt ontrolt (groeit) en zweeft omhoog
        if (sfx != null) { sfx.Play("sparkle", 1f, 0.8f); sfx.Play("whoosh", 0.7f, 1.2f); }
        for (int i = 0; i < 60; i++) Gfx.Burst(glitter, boxTop, Random.onUnitSphere * 0.3f + up * 0.2f, 1, 0.05f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.8f)
        {
            float e = 1f - Mathf.Pow(1f - t, 3f);
            transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.8f, e);
            transform.position = boxTop + up * Mathf.Lerp(0f, 0.3f, e);
            yield return null;
        }
        if (sfx != null) sfx.Play("whistle", 0.6f, 0.8f);

        // Rondes rond jou en de doos
        Vector3 camPos = cam.transform.position;
        Vector3 center = Vector3.Lerp(box.position, camPos, 0.35f);
        center += up * (Vector3.Dot(camPos - center, up) * 0.5f);
        float radius = Mathf.Clamp(Vector3.ProjectOnPlane(camPos - box.position, up).magnitude + 0.45f, 0.75f, 1.4f);
        Vector3 ax = f0, az = Vector3.Cross(up, f0);
        float startAng = Mathf.Atan2(Vector3.Dot(transform.position - center, az), Vector3.Dot(transform.position - center, ax));
        Vector3 from = transform.position, prev = from;
        float duration = 12f, blendIn = 1.4f, total = Mathf.PI * 4f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float ang = startAng + total * (t / duration);
            Vector3 target = center + (ax * Mathf.Cos(ang) + az * Mathf.Sin(ang)) * radius + up * (Mathf.Sin(ang * 2f) * 0.15f);
            Vector3 pos = t < blendIn ? Vector3.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / blendIn)) : target;
            transform.position = pos;
            transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(t / 2f));
            Orient(pos - prev, center, 0f);
            prev = pos;
            yield return null;
        }

        // Rolbeweging vlak langs je hoofd ("Wiiiieee!")
        if (sfx != null) sfx.Play("whoosh", 1f, 0.7f);
        Vector3 p0 = transform.position, camNow = cam.transform.position;
        Vector3 side = Vector3.Cross(up, Vector3.ProjectOnPlane(camNow - p0, up).normalized);
        Vector3 p1 = camNow + side * 0.32f + up * 0.02f;
        Vector3 p2 = camNow + (camNow - p0).normalized * 0.9f + up * 0.2f;
        prev = p0;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.8f)
        {
            Vector3 pos = Bezier(p0, p1, p2, t);
            transform.position = pos;
            roll = Mathf.SmoothStep(0f, 360f, Mathf.InverseLerp(0.25f, 0.85f, t));
            Orient(pos - prev, camNow, roll);
            prev = pos;
            yield return null;
        }
        roll = 0f;

        // Terug naar de doos en landen
        Vector3 q0 = transform.position, q2 = box.position + up * 0.035f;
        Vector3 q1 = Vector3.Lerp(q0, q2, 0.5f) + up * 0.4f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 2.4f)
        {
            Vector3 pos = Bezier(q0, q1, q2, Mathf.SmoothStep(0f, 1f, t));
            transform.position = pos;
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, t);
            rippleAmp = Mathf.Lerp(0.012f, 0.004f, t);
            Orient(pos - prev, q2, 0f);
            prev = pos;
            yield return null;
        }
        Vector3 look = Vector3.ProjectOnPlane(cam.transform.position - transform.position, up);
        if (look.sqrMagnitude > 0.001f)
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look, up), t);
                yield return null;
            }
        yield return new WaitForSeconds(0.4f);
    }

    // Tapijt rolt op en verdwijnt.
    public IEnumerator Vanish(SoundFx sfx)
    {
        if (rider != null) rider.gameObject.SetActive(false);
        if (sfx != null) sfx.Play("poof", 0.8f, 1.1f);
        Vector3 s0 = transform.localScale;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            transform.localScale = new Vector3(s0.x * (1f - t), s0.y, s0.z * (1f - t * t));
            if (Random.value < 0.8f) Gfx.Burst(glitter, transform.position, worldUp * 0.1f, 3, 0.1f);
            yield return null;
        }
        Remove();
    }

    public void Remove()
    {
        if (fxRoot != null) Destroy(fxRoot.gameObject);
        if (cloth != null) Destroy(cloth);
        Destroy(gameObject);
    }

    // Oriëntatie tijdens het vliegen.
    private void Orient(Vector3 delta, Vector3 turnCenter, float extraRoll)
    {
        if (delta.sqrMagnitude < 1e-8f) return;
        Vector3 fwd = Vector3.ProjectOnPlane(delta, worldUp);
        if (fwd.sqrMagnitude < 1e-8f) return;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(worldUp, fwd);
        float side = Vector3.Dot((turnCenter - transform.position).normalized, right);
        Quaternion target = Quaternion.LookRotation(fwd, worldUp) * Quaternion.Euler(0f, 0f, -side * 22f - extraRoll);
        transform.rotation = extraRoll != 0f ? target : Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 5f);
    }

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }
}
