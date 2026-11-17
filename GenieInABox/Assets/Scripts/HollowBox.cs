using System.Collections;
using UnityEngine;

// Luik met onmogelijk diepe binnenkant (depth mask).
public class HollowBox : MonoBehaviour
{
    [Header("Opening")]
    [Tooltip("Rand rond het luik (meter)")]
    public float frameMargin = 0.018f;
    [Tooltip("Diepte van de binnenkant (meter) - mag dieper dan de echte doos!")]
    public float interiorDepth = 0.075f;
    [Tooltip("Openingshoek van het luik")]
    public float openAngle = 112f;

    [Header("Kleuren")]
    public Color wallTop = new Color(0.45f, 0.3f, 0.6f);
    public Color wallBottom = new Color(0.08f, 0.04f, 0.14f);
    public Color trimColor = new Color(1f, 0.78f, 0.3f);
    public Color cardboard = new Color(0.72f, 0.6f, 0.47f);

    public Transform Interior { get; private set; }
    public Transform HatchPivot { get; private set; }
    public float FloorLocalY => -interiorDepth;          // in Interior-ruimte
    public Vector2 WindowSize { get; private set; }
    public bool IsOpen { get; private set; }

    // Positie van het sleutelgat.
    public Vector3 KeyholeOnHatch { get; private set; }
    public Vector3 KeyholeWorld => HatchPivot.TransformPoint(KeyholeOnHatch);

    private GenieBoxRig rig;
    private SoundFx sfx;
    private GameObject lid, lockPlate, occluders, beams;
    private ParticleSystem sparkles;
    private Material beamMat;

    private void Start()
    {
        rig = GetComponentInParent<GenieBoxRig>();
        sfx = FindFirstObjectByType<SoundFx>();
        Build();
        ResetBox();
    }

    private void Build()
    {
        float W = rig.Width - 2f * frameMargin, L = rig.Length - 2f * frameMargin;
        WindowSize = new Vector2(W, L);
        float top = rig.TopY;

        // ---------------- Binnenkant (wanden met kleurverloop + gouden rand)
        Interior = Gfx.Group("Interior", transform);
        Interior.localPosition = new Vector3(0f, top, 0f);
        var walls = new GameObject("Walls");
        walls.transform.SetParent(Interior, false);
        walls.AddComponent<MeshFilter>().sharedMesh = BuildWallMesh(W - 0.002f, L - 0.002f, interiorDepth); // 1 mm binnen de occluders (geen geflikker)
        var wr = walls.AddComponent<MeshRenderer>();
        wr.sharedMaterial = Gfx.Toon;
        Gfx.SetColor(wr, Color.white, 0f);
        Gfx.UseVertexColors(wr);

        Gfx.Prim(PrimitiveType.Cube, Interior, new Vector3(0f, -interiorDepth - 0.001f, 0f), new Vector3(W, 0.002f, L), wallBottom * 1.3f, 0f, "Floor");

        float t = 0.004f;
        Gfx.Prim(PrimitiveType.Cube, Interior, new Vector3(-W / 2f + t / 2f, -0.002f, 0f), new Vector3(t, 0.004f, L), trimColor, 0.6f, "TrimL");
        Gfx.Prim(PrimitiveType.Cube, Interior, new Vector3(W / 2f - t / 2f, -0.002f, 0f), new Vector3(t, 0.004f, L), trimColor, 0.6f, "TrimR");
        Gfx.Prim(PrimitiveType.Cube, Interior, new Vector3(0f, -0.002f, -L / 2f + t / 2f), new Vector3(W, 0.004f, t), trimColor, 0.6f, "TrimB");
        Gfx.Prim(PrimitiveType.Cube, Interior, new Vector3(0f, -0.002f, L / 2f - t / 2f), new Vector3(W, 0.004f, t), trimColor, 0.6f, "TrimF");

        // ---------------- Onzichtbare occluders
        occluders = new GameObject("Occluders");
        occluders.transform.SetParent(transform, false);
        BuildOccluders(occluders.transform, W, L);

        // ---------------- Luik (met scharnier aan de bovenkant van de opdruk = +Z)
        HatchPivot = Gfx.Group("HatchPivot", transform);
        HatchPivot.localPosition = new Vector3(0f, top, L / 2f);

        lid = new GameObject("Lid");
        lid.transform.SetParent(HatchPivot, false);
        // Bovenkant: de echte opdruk van je doos (Resources/BoxFront), precies het stukje van het luik
        Texture2D front = Resources.Load<Texture2D>("BoxFront");
        var lidMat = Gfx.NewToon(front);
        lidMat.SetFloat("_Ambient", 0.85f);
        lidMat.mainTextureScale = new Vector2(W / rig.Width, L / rig.Length);
        lidMat.mainTextureOffset = new Vector2(frameMargin / rig.Width, frameMargin / rig.Length);
        var printQuad = Gfx.Quad(lid.transform, new Vector3(0f, 0.0012f, -L / 2f), Gfx.FaceUp, new Vector2(W, L), lidMat, "Print");
        Gfx.SetColor(printQuad.GetComponent<Renderer>(), front != null ? Color.white : new Color(0.85f, 0.2f, 0.15f));
        Gfx.Prim(PrimitiveType.Cube, lid.transform, new Vector3(0f, -0.0002f, -L / 2f), new Vector3(W, 0.002f, L), cardboard, 0f, "Cardboard");

        // ---------------- Slotplaat met sleutelgat, 3 cm van de voorste rand
        lockPlate = new GameObject("LockPlate");
        lockPlate.transform.SetParent(HatchPivot, false);
        Vector3 plateCenter = new Vector3(0f, 0.0024f, -L + 0.032f);
        Gfx.Prim(PrimitiveType.Cylinder, lockPlate.transform, plateCenter, new Vector3(0.034f, 0.0012f, 0.034f), trimColor, 0.35f, "Plate");
        // Sleutelgat: rond gaatje (voor de schacht) met een smalle sleuf richting de voorrand (-Z) voor de tanden
        Gfx.Prim(PrimitiveType.Cylinder, lockPlate.transform, plateCenter + new Vector3(0f, 0.0012f, 0f), new Vector3(0.0075f, 0.0002f, 0.0075f), Color.black, 0f, "HoleRound");
        Gfx.Prim(PrimitiveType.Cube, lockPlate.transform, plateCenter + new Vector3(0f, 0.0012f, -0.0045f), new Vector3(0.0028f, 0.0004f, 0.009f), Color.black, 0f, "HoleSlot");
        KeyholeOnHatch = plateCenter + new Vector3(0f, 0.0013f, 0f);

        // ---------------- Lichtbundels + glinsters bij het openen
        beams = new GameObject("LightBeams");
        beams.transform.SetParent(transform, false);
        beams.transform.localPosition = new Vector3(0f, top, 0f);
        beamMat = Gfx.NewUnlit(Gfx.Blend.Additive, BeamTexture(), 5);
        for (int i = 0; i < 3; i++)
        {
            var q = Gfx.Quad(beams.transform, new Vector3(0f, 0.12f, 0f), Quaternion.Euler(0f, i * 60f, 0f), new Vector2(Mathf.Min(W, L) * 0.9f, 0.24f), beamMat, "Beam" + i);
            q.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        }
        sparkles = Gfx.Particles("Sparkles", transform, rig.transform, Gfx.Sparkle, Gfx.Blend.Additive, 2f, 0.018f, 0.12f, new Color(1f, 0.9f, 0.5f), -0.05f, 300);
    }

    // Luik dicht.
    public void ResetBox()
    {
        StopAllCoroutines();
        IsOpen = false;
        HatchPivot.localRotation = Quaternion.identity;
        lid.SetActive(false);
        lockPlate.SetActive(false);
        Interior.gameObject.SetActive(false);
        beams.SetActive(false);
    }

    public void ShowLock(bool show)
    {
        lockPlate.SetActive(show);
        if (show && sfx != null) sfx.Play("sparkle", 0.6f);
        if (show) Gfx.Burst(sparkles, KeyholeWorld, rig.transform.up * 0.08f, 15, 0.08f);
    }

    // Luik open.
    public IEnumerator Open()
    {
        lid.SetActive(true);
        Interior.gameObject.SetActive(true);
        if (sfx != null) sfx.Play("rumble", 0.7f);

        // Eerst even rammelen (er wil iets naar buiten!)
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            HatchPivot.localRotation = Quaternion.Euler(Mathf.Abs(Mathf.Sin(t * 45f)) * 4f * (1f - t / 0.6f), 0f, 0f);
            yield return null;
        }

        if (sfx != null) sfx.Play("creak", 0.9f);
        beams.SetActive(true);
        Vector3 center = rig.transform.TransformPoint(new Vector3(0f, rig.TopY, 0f));
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.3f)
        {
            float e = EaseOutBack(t);
            HatchPivot.localRotation = Quaternion.Euler(openAngle * e, 0f, 0f);
            beamMat.color = new Color(1f, 0.85f, 0.5f, Mathf.Sin(t * Mathf.PI) * 0.9f);
            if (Random.value < 0.6f) Gfx.Burst(sparkles, center, rig.transform.up * 0.15f, 2, 0.07f);
            yield return null;
        }
        HatchPivot.localRotation = Quaternion.Euler(openAngle, 0f, 0f);
        beams.SetActive(false);
        IsOpen = true;
    }

    public IEnumerator Close()
    {
        float from = openAngle;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
        {
            HatchPivot.localRotation = Quaternion.Euler(Mathf.Lerp(from, 0f, t * t), 0f, 0f);
            yield return null;
        }
        HatchPivot.localRotation = Quaternion.identity;
        if (sfx != null) sfx.Play("bump", 0.9f);
        Interior.gameObject.SetActive(false);
        lid.SetActive(false);
        lockPlate.SetActive(false);
        IsOpen = false;
    }

    // Punt boven de opening.
    public Vector3 OpeningWorld(float lift = 0f) => rig.transform.TransformPoint(new Vector3(0f, rig.TopY + lift, 0f));

    // ------------------------------------------------------------------ Bouwstenen

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.4f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private Mesh BuildWallMesh(float W, float L, float depth)
    {
        var m = new Mesh { name = "InteriorWalls" };
        var v = new System.Collections.Generic.List<Vector3>();
        var c = new System.Collections.Generic.List<Color>();
        var n = new System.Collections.Generic.List<Vector3>();
        var tri = new System.Collections.Generic.List<int>();
        float hw = W / 2f, hl = L / 2f;
        // (hoekA, hoekB, normaal naar binnen)
        AddWall(v, c, n, tri, new Vector3(-hw, 0, -hl), new Vector3(-hw, 0, hl), Vector3.right, depth);
        AddWall(v, c, n, tri, new Vector3(hw, 0, hl), new Vector3(hw, 0, -hl), Vector3.left, depth);
        AddWall(v, c, n, tri, new Vector3(hw, 0, -hl), new Vector3(-hw, 0, -hl), Vector3.forward, depth);
        AddWall(v, c, n, tri, new Vector3(-hw, 0, hl), new Vector3(hw, 0, hl), Vector3.back, depth);
        m.SetVertices(v); m.SetColors(c); m.SetNormals(n); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }

    private void AddWall(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Color> c,
                         System.Collections.Generic.List<Vector3> n, System.Collections.Generic.List<int> tri,
                         Vector3 a, Vector3 b, Vector3 normal, float depth)
    {
        int bands = 6; // horizontale banden: lichter boven, donkerder onder (diepte-effect)
        int start = v.Count;
        for (int i = 0; i <= bands; i++)
        {
            float f = i / (float)bands;
            Vector3 down = Vector3.down * depth * f;
            Color col = Color.Lerp(wallTop, wallBottom, Mathf.Pow(f, 0.7f));
            v.Add(a + down); v.Add(b + down);
            c.Add(col); c.Add(col);
            n.Add(normal); n.Add(normal);
        }
        for (int i = 0; i < bands; i++)
        {
            int k = start + i * 2;
            // dubbelzijdig, zodat de kijkrichting nooit uitmaakt
            tri.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
            tri.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
        }
    }

    private void BuildOccluders(Transform parent, float W, float L)
    {
        float th = 0.002f, inset = 0.0008f;
        float bw = rig.Width, bl = rig.Length, bt = rig.Thickness, top = rig.TopY;
        float deep = interiorDepth + 0.012f;

        // Mouw rond de opening, tot onder de vloer (verbergt de "te diepe" binnenkant van opzij)
        Occ(parent, new Vector3(-W / 2f - th / 2f, top - deep / 2f - 0.0006f, 0f), new Vector3(th, deep, L + 2f * th));
        Occ(parent, new Vector3(W / 2f + th / 2f, top - deep / 2f - 0.0006f, 0f), new Vector3(th, deep, L + 2f * th));
        Occ(parent, new Vector3(0f, top - deep / 2f - 0.0006f, -L / 2f - th / 2f), new Vector3(W + 2f * th, deep, th));
        Occ(parent, new Vector3(0f, top - deep / 2f - 0.0006f, L / 2f + th / 2f), new Vector3(W + 2f * th, deep, th));
        Occ(parent, new Vector3(0f, top - deep - 0.0006f, 0f), new Vector3(W + 2f * th, th, L + 2f * th));

        // De echte doos zelf: zijkanten + bovenrand rond de opening + onderrand
        Occ(parent, new Vector3(-bw / 2f + inset, 0f, 0f), new Vector3(th, bt - 2f * inset, bl - 2f * inset));
        Occ(parent, new Vector3(bw / 2f - inset, 0f, 0f), new Vector3(th, bt - 2f * inset, bl - 2f * inset));
        Occ(parent, new Vector3(0f, 0f, -bl / 2f + inset), new Vector3(bw - 2f * inset, bt - 2f * inset, th));
        Occ(parent, new Vector3(0f, 0f, bl / 2f - inset), new Vector3(bw - 2f * inset, bt - 2f * inset, th));
        float fx = (bw - W) / 2f, fz = (bl - L) / 2f;
        foreach (float y in new[] { top - inset, -top + inset })
        {
            Occ(parent, new Vector3(-W / 2f - fx / 2f, y, 0f), new Vector3(fx, th, bl - 2f * inset));
            Occ(parent, new Vector3(W / 2f + fx / 2f, y, 0f), new Vector3(fx, th, bl - 2f * inset));
            Occ(parent, new Vector3(0f, y, -L / 2f - fz / 2f), new Vector3(W, th, fz));
            Occ(parent, new Vector3(0f, y, L / 2f + fz / 2f), new Vector3(W, th, fz));
        }
    }

    private static void Occ(Transform parent, Vector3 pos, Vector3 size)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Occluder";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = Gfx.DepthMask;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static Texture2D BeamTexture()
    {
        int w = 32, h = 64;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Beam" };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fx = 1f - Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                float fy = 1f - (y + 0.5f) / h;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Pow(fx, 1.5f) * fy * 255));
            }
        t.SetPixels32(px);
        t.Apply();
        return t;
    }
}
