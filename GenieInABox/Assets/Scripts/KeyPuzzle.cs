using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Fase 2: sleutel vormen via perspectief (anamorfose).
public class KeyPuzzle : MonoBehaviour
{
    [Header("Sleutel")]
    [Tooltip("Breedte van de afgewerkte sleutel (meter)")]
    public float keyWidth = 0.14f;
    [Tooltip("Hoogte van het sleutelbeeld boven de doos (meter), willekeurig tussen min en max")]
    public float minKeyHeight = 0.08f, maxKeyHeight = 0.14f;
    public Color shardCold = new Color(0.35f, 0.55f, 1f);
    public Color shardWarm = new Color(1f, 0.78f, 0.25f);

    [Header("Geheim kijkpunt (elke keer willekeurig)")]
    [Tooltip("Afstand van het kijkpunt tot het sleutelbeeld (meter), min en max")]
    public float minViewDistance = 0.3f, maxViewDistance = 0.48f;
    [Tooltip("Van laag (bijna tafelhoogte) tot hoog (bijna recht van boven)")]
    public float minElevation = 15f, maxElevation = 72f;
    [Tooltip("Hoeveel graden je minstens moet rondstappen t.o.v. waar je stond")]
    public float minWalk = 55f;
    [Tooltip("Sleutel mag schuin of zelfs bijna ondersteboven verschijnen (graden)")]
    public float maxRoll = 70f;
    [Tooltip("Lengte van de sleutel als hij in het slot steekt (meter)")]
    public float insertedKeyLength = 0.055f;

    [Header("Oplossen")]
    [Tooltip("Hoek (graden) waarbinnen de sleutel 'klopt'")]
    public float alignAngle = 7f;
    public float holdTime = 0.6f;

    // Pixel-art sleutelontwerpen.
    private class KeyDesign
    {
        public string name; public string[] art; public int shaftTop, shaftBottom;
        public KeyDesign(string n, int top, int bottom, params string[] a) { name = n; shaftTop = top; shaftBottom = bottom; art = a; }
    }

    private static readonly KeyDesign[] Designs =
    {
        new KeyDesign("Klassiek", 2, 3,
            "..XXX............",
            ".X...X...........",
            "X.....XXXXXXXXXXX",
            "X.....XXXXXXXXXXX",
            ".X...X......X.X.X",
            "..XXX.......X.XXX"),
        new KeyDesign("Klaver", 2, 2,
            ".XX.XX...........",
            "X..X..X..........",
            "X.....XXXXXXXXXXX",
            "X..X..X......X.XX",
            ".XX.XX.......XXXX"),
        new KeyDesign("Ruit", 2, 2,
            "..X..............",
            ".XXX.............",
            "XX.XXXXXXXXXXXXXX",
            ".XXX........XX.XX",
            "..X.........X..XX"),
        new KeyDesign("Hart", 2, 2,
            ".XX.XX...........",
            "XXXXXXX..........",
            "XXXXXXXXXXXXXXXXX",
            ".XXXXX.......X.XX",
            "..XXX........XXXX",
            "...X............."),
        new KeyDesign("Dubbele baard", 2, 3,
            ".XXXX............",
            "XX..XX...........",
            "X....XXXXXXXXXXXX",
            "X....XXXXXXXXXXXX",
            "XX..XX....XX.X.XX",
            ".XXXX.....X..X..X"),
    };

    private KeyDesign design;
    private string[] KeyArt => design.art;

    private class Shard
    {
        public Transform t;
        public List<Renderer> renderers = new List<Renderer>();
        public Vector3 assembled;   // middelpunt in het sleutelvlak (lokaal t.o.v. de doos)
        public float s;             // diepte-factor langs de zichtlijn
        public float phase;
        public Vector3 restPos; public Quaternion restRot; public Vector2 restUV;
        public bool released;
        public float flight;        // 0..1 animatie van grond naar lucht
    }

    public int Total => shards.Count;
    public int Released { get; private set; }
    public bool AllReleased => Total > 0 && Released >= Total && flightsDone;
    // 0 = verkeerd, 1 = perfect.
    public float Score { get; private set; }
    // Gewenste camerarichting (camera-ruimte).
    public Vector3 GuideMove { get; private set; }
    public bool Solving { get; set; }
    public bool Solved { get; private set; }

    private GenieBoxRig rig;
    private SoundFx sfx;
    private Camera cam;
    private readonly List<Shard> shards = new List<Shard>();
    private Transform keyRoot;
    private Vector3 K0, V;          // middelpunt sleutelbeeld en geheim kijkpunt (lokaal)
    private float viewDistance, keyHeight;
    private Quaternion keyRot;      // oriëntatie van het sleutelvlak (kijkt naar V)
    private bool viewpointChosen, flightsDone;
    private float cell, alignedFor;
    private ParticleSystem glitter;

    private void Start()
    {
        rig = GetComponentInParent<GenieBoxRig>();
        sfx = FindFirstObjectByType<SoundFx>();
        cam = Camera.main;
        glitter = Gfx.Particles("Glitter", transform, rig.transform, Gfx.Sparkle, Gfx.Blend.Additive, 1.2f, 0.016f, 0.08f, Color.white, 0f, 400);
        ResetPuzzle();
    }

    // ------------------------------------------------------------------ Opbouw

    public void ResetPuzzle()
    {
        StopAllCoroutines();
        if (keyRoot != null) Destroy(keyRoot.gameObject);
        shards.Clear();
        Released = 0;
        Solved = false;
        Solving = false;
        viewpointChosen = false;
        flightsDone = false;
        alignedFor = 0f;

        keyRoot = Gfx.Group("Key", transform);
        design = Designs[Random.Range(0, Designs.Length)];
        int cols = KeyArt[0].Length, rows = KeyArt.Length;
        cell = keyWidth / cols;
        keyHeight = Random.Range(minKeyHeight, maxKeyHeight);
        viewDistance = Random.Range(minViewDistance, maxViewDistance);
        K0 = new Vector3(0f, rig.TopY + keyHeight, 0f);

        var groups = RandomChunks(cols, rows);
        var usedUV = new List<Vector2>();
        foreach (var group in groups)
        {
            var sh = new Shard();
            var go = new GameObject("Shard");
            sh.t = go.transform;
            sh.t.SetParent(keyRoot, false);

            // middelpunt van dit brokstuk (in sleutelcoördinaten: x naar rechts, y omhoog)
            Vector2 center = Vector2.zero;
            foreach (var cc in group) center += KeyCoord(cc, cols, rows);
            center /= group.Count;
            sh.assembled = new Vector3(center.x, center.y, 0f); // tijdelijk: in sleutelvlak-ruimte

            foreach (var cc in group)
            {
                Vector2 p = KeyCoord(cc, cols, rows) - center;
                var cube = Gfx.Prim(PrimitiveType.Cube, sh.t, new Vector3(p.x, p.y, 0f), new Vector3(cell * 0.96f, cell * 0.96f, cell * 0.45f), shardCold, 0.3f, "Block");
                sh.renderers.Add(cube.GetComponent<Renderer>());
            }

            // Rustplek onder het stof: willekeurig op de voorkant, niet te dicht bij elkaar
            Vector2 uv = Vector2.zero;
            for (int tries = 0; tries < 40; tries++)
            {
                uv = new Vector2(Random.Range(0.15f, 0.85f), Random.Range(0.12f, 0.88f));
                bool ok = true;
                foreach (var u in usedUV) if (Vector2.Distance(u, uv) < 0.15f) { ok = false; break; }
                if (ok) break;
            }
            usedUV.Add(uv);
            sh.restUV = uv;
            sh.restPos = rig.FrontPoint(uv.x, uv.y, cell * 0.45f * 0.5f + 0.0004f);
            sh.restRot = Quaternion.LookRotation(Vector3.down, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward);
            sh.phase = Random.Range(0f, 10f);
            sh.t.localPosition = sh.restPos;
            sh.t.localRotation = sh.restRot;
            sh.t.localScale = Vector3.one;
            shards.Add(sh);
        }
    }

    // Verdeelt blokjes in willekeurige brokstukken.
    private List<List<Vector2Int>> RandomChunks(int cols, int rows)
    {
        var cells = new List<Vector2Int>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (KeyArt[r][c] == 'X') cells.Add(new Vector2Int(c, r));
        for (int i = cells.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (cells[i], cells[j]) = (cells[j], cells[i]); }

        var owner = new Dictionary<Vector2Int, int>();
        var groups = new List<List<Vector2Int>>();
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var start in cells)
        {
            if (owner.ContainsKey(start)) continue;
            int target = Random.Range(2, 6);
            var group = new List<Vector2Int> { start };
            owner[start] = groups.Count;
            var frontier = new List<Vector2Int> { start };
            while (group.Count < target && frontier.Count > 0)
            {
                var from = frontier[Random.Range(0, frontier.Count)];
                var options = new List<Vector2Int>();
                foreach (var d in dirs)
                {
                    var n = from + d;
                    if (n.x >= 0 && n.y >= 0 && n.x < cols && n.y < rows && KeyArt[n.y][n.x] == 'X' && !owner.ContainsKey(n)) options.Add(n);
                }
                if (options.Count == 0) { frontier.Remove(from); continue; }
                var pick = options[Random.Range(0, options.Count)];
                owner[pick] = groups.Count;
                group.Add(pick);
                frontier.Add(pick);
            }
            groups.Add(group);
        }
        // Losse blokjes bij een buur-brokstuk voegen
        for (int g = groups.Count - 1; g >= 0; g--)
        {
            if (groups[g].Count > 1) continue;
            var c0 = groups[g][0];
            foreach (var d in dirs)
                if (owner.TryGetValue(c0 + d, out int other) && other != g && groups[other].Count > 0)
                {
                    groups[other].Add(c0);
                    owner[c0] = other;
                    groups[g].Clear();
                    break;
                }
        }
        groups.RemoveAll(gr => gr.Count == 0);
        return groups;
    }

    // Alles opruimen.
    public void ClearAll()
    {
        StopAllCoroutines();
        if (keyRoot != null) Destroy(keyRoot.gameObject);
        shards.Clear();
        Solving = false;
        if (sfx != null) sfx.SetHum(0f, 0f);
    }

    private Vector2 KeyCoord(Vector2Int c, int cols, int rows) =>
        new Vector2((c.x - (cols - 1) / 2f) * cell, ((rows - 1) / 2f - c.y) * cell);

    // Kiest het geheime kijkpunt.
    private void ChooseViewpoint()
    {
        viewpointChosen = true;
        float camAz = 180f;
        if (cam != null)
        {
            Vector3 c = rig.CameraLocal(cam);
            camAz = Mathf.Atan2(c.x, c.z) * Mathf.Rad2Deg;
        }
        // Ergens rondom (maar niet waar je nu staat), laag of hoog, met een willekeurige kanteling
        float az = camAz + (Random.value < 0.5f ? -1f : 1f) * Random.Range(minWalk, 180f);
        float el = Random.Range(minElevation, maxElevation);
        Vector3 dir = new Vector3(Mathf.Cos(el * Mathf.Deg2Rad) * Mathf.Sin(az * Mathf.Deg2Rad), Mathf.Sin(el * Mathf.Deg2Rad), Mathf.Cos(el * Mathf.Deg2Rad) * Mathf.Cos(az * Mathf.Deg2Rad));
        V = K0 + dir * viewDistance;
        keyRot = Quaternion.LookRotation(K0 - V, Vector3.up) * Quaternion.Euler(0f, 0f, Random.Range(-maxRoll, maxRoll));

        // Per brokstuk: plek in het sleutelvlak (lokaal) en een willekeurige diepte s
        float minY = rig.TopY + 0.035f;
        foreach (var sh in shards)
        {
            Vector3 P = K0 + keyRot * sh.assembled;
            // s zo kiezen dat het brokstuk niet in de tafel/doos zakt: y(s) = V.y + (P.y - V.y)*s >= minY
            float maxS = P.y < V.y ? Mathf.Min(1.9f, (V.y - minY) / (V.y - P.y)) : 1.9f;
            sh.s = Random.Range(0.4f, Mathf.Max(0.5f, maxS));
            sh.assembled = P; // vanaf nu: lokale positie in het afgewerkte sleutelvlak
        }
    }

    private Vector3 FloatPos(Shard sh, float t) => V + (sh.assembled - V) * FloatS(sh, t);
    private float FloatS(Shard sh, float t) => sh.s * (1f + 0.035f * Mathf.Sin(t * 1.3f + sh.phase));

    // ------------------------------------------------------------------ Elk frame

    // Brokstukken vrijgeven waar geen stof meer is.
    public void CheckReleases(DustCover dust)
    {
        foreach (var sh in shards)
        {
            if (sh.released) continue;
            if (dust.CoverAt(sh.restUV, 0.035f) < 0.35f || dust.Finished) Release(sh);
        }
    }

    private void Release(Shard sh)
    {
        if (!viewpointChosen) ChooseViewpoint();
        sh.released = true;
        Released++;
        if (sfx != null) sfx.PlayChime(0.6f);
        Gfx.Burst(glitter, rig.transform.TransformPoint(sh.restPos), rig.transform.up * 0.1f, 12, 0.08f);
        StartCoroutine(Fly(sh));
    }

    private IEnumerator Fly(Shard sh)
    {
        Vector3 from = sh.restPos;
        Quaternion fromRot = sh.restRot;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.6f)
        {
            float e = t * t * (3f - 2f * t);
            Vector3 to = FloatPos(sh, Time.time);
            Vector3 p = Vector3.Lerp(from, to, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 0.06f; // boogje
            sh.t.localPosition = p;
            sh.t.localRotation = Quaternion.Slerp(fromRot, keyRot, e) * Quaternion.Euler(0f, 0f, (1f - e) * 540f);
            sh.t.localScale = Vector3.one * Mathf.Lerp(1f, FloatS(sh, Time.time), e);
            sh.flight = e;
            yield return null;
        }
        sh.flight = 1f;
        bool all = true;
        foreach (var s in shards) if (s.flight < 1f) all = false;
        if (all && Released >= shards.Count) flightsDone = true;
    }

    private void Update()
    {
        if (cam == null) cam = Camera.main;
        float time = Time.time;

        // Zwevende brokstukken laten dobberen (langs hun zichtlijn -> illusie blijft kloppen)
        foreach (var sh in shards)
        {
            if (!sh.released || sh.flight < 1f || Solved) continue;
            sh.t.localPosition = FloatPos(sh, time);
            sh.t.localRotation = keyRot;
            sh.t.localScale = Vector3.one * FloatS(sh, time);
        }

        if (!Solving || Solved || !viewpointChosen || cam == null) { Score = 0f; return; }

        // Hoe goed kijk je vanuit het geheime punt?
        Vector3 c = rig.CameraLocal(cam);
        float angle = Vector3.Angle(c - K0, V - K0);
        float ratio = (c - K0).magnitude / viewDistance;
        float distOk = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Log(Mathf.Max(0.01f, ratio))) * 1.2f);
        Score = Mathf.Clamp01(1f - angle / 60f) * Mathf.Lerp(0.6f, 1f, distOk);

        // Richting voor de pijl op het scherm (in camera-ruimte)
        Vector3 deltaWorld = rig.transform.TransformPoint(V) - cam.transform.position;
        GuideMove = cam.transform.InverseTransformDirection(deltaWorld);

        // Kleur: koud blauw -> warm goud, met meer gloed
        Color col = Color.Lerp(shardCold, shardWarm, Score * Score);
        foreach (var sh in shards)
            foreach (var r in sh.renderers) Gfx.SetColor(r, col, 0.2f + Score * 1.3f, Color.Lerp(col, Color.white, 0.4f));

        bool aligned = angle < alignAngle && ratio > 0.6f && ratio < 1.6f;
#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame) { aligned = true; alignedFor = holdTime; } // test-snelkoppeling
#endif
        alignedFor = aligned ? alignedFor + Time.deltaTime : 0f;
        if (alignedFor >= holdTime)
        {
            Solved = true;
            Solving = false;
            if (sfx != null) sfx.SetHum(0f, 0f);
        }
    }

    // ------------------------------------------------------------------ Sleutel samenvoegen en in het slot steken

    public IEnumerator AssembleAndInsert(HollowBox box)
    {
        if (sfx != null) { sfx.SetHum(0f, 0f); sfx.Play("assemble", 1f); }

        // 1. Alle brokstukken schuiven langs hun zichtlijn naar het sleutelvlak (s -> 1)
        var startPos = new Vector3[shards.Count];
        var startS = new float[shards.Count];
        for (int i = 0; i < shards.Count; i++) { startPos[i] = shards[i].t.localPosition; startS[i] = shards[i].t.localScale.x; }
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
        {
            float e = t * t * (3f - 2f * t);
            for (int i = 0; i < shards.Count; i++)
            {
                var sh = shards[i];
                sh.t.localPosition = Vector3.Lerp(startPos[i], sh.assembled, e);
                sh.t.localScale = Vector3.one * Mathf.Lerp(startS[i], 1f, e);
                foreach (var r in sh.renderers) Gfx.SetColor(r, shardWarm, 1.5f + e, Color.white);
            }
            yield return null;
        }

        // 2. Flits: nu is het één sleutel
        Vector3 keyWorld = rig.transform.TransformPoint(K0);
        Gfx.Burst(glitter, keyWorld, Vector3.zero, 60, 0.25f, null, 1.5f);
        if (sfx != null) sfx.Play("sparkle", 1f);
        var pivot = Gfx.Group("KeyPivot", transform);
        pivot.localPosition = K0;
        pivot.localRotation = keyRot;
        foreach (var sh in shards) sh.t.SetParent(pivot, true);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            foreach (var sh in shards) foreach (var r in sh.renderers) Gfx.SetColor(r, shardWarm, Mathf.Lerp(2.5f, 0.5f, t * 2f), Color.white);
            yield return null;
        }

        // 3. Naar het slot vliegen. De sleutel krimpt tot slotformaat en draait zo dat:
        //    - de schacht (sleutel-X) recht omlaag wijst,
        //    - het platte blad (sleutel-XY-vlak) langs de sleuf van het sleutelgat ligt (doos-Z),
        //    - de tanden (sleutel -Y) in de smalle sleuf vallen (doos -Z).
        float scale = insertedKeyLength / keyWidth;
        float tipY = 0f;
        for (int r = design.shaftTop; r <= design.shaftBottom; r++) tipY += ((KeyArt.Length - 1) / 2f - r) * cell;
        tipY /= design.shaftBottom - design.shaftTop + 1;
        Vector3 tipLocal = new Vector3((KeyArt[0].Length - 1) / 2f * cell + cell * 0.5f, tipY, 0f) * scale;
        Quaternion insertRot = Quaternion.LookRotation(Vector3.left, Vector3.forward); // X->omlaag, Y->+Z, Z->-X
        Vector3 holeLocal = rig.transform.InverseTransformPoint(box.KeyholeWorld);
        float depth = 0.012f;
        Vector3 above = holeLocal + Vector3.up * 0.03f - insertRot * tipLocal;
        Vector3 inside = holeLocal + Vector3.down * depth - insertRot * tipLocal;

        Vector3 p0 = pivot.localPosition; Quaternion r0 = pivot.localRotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.1f)
        {
            float e = t * t * (3f - 2f * t);
            pivot.localPosition = Vector3.Lerp(p0, above, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 0.05f;
            pivot.localRotation = Quaternion.Slerp(r0, insertRot, e);
            pivot.localScale = Vector3.one * Mathf.Lerp(1f, scale, e);
            yield return null;
        }
        pivot.localScale = Vector3.one * scale;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
        {
            pivot.localPosition = Vector3.Lerp(above, inside, t * t);
            yield return null;
        }
        pivot.localPosition = inside;
        if (sfx != null) sfx.Play("bump", 0.7f);
        yield return new WaitForSeconds(0.25f);

        // 4. Een kwartslag draaien rond de schacht (verticale as door het sleutelgat) -> klik!
        if (sfx != null) sfx.Play("click", 1f);
        Quaternion turned = Quaternion.AngleAxis(-90f, Vector3.up) * insertRot;
        Vector3 tipPos = holeLocal + Vector3.down * depth;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
        {
            float e = t * t * (3f - 2f * t);
            Quaternion q = Quaternion.Slerp(insertRot, turned, e);
            pivot.localRotation = q;
            pivot.localPosition = tipPos - q * tipLocal;
            yield return null;
        }
        pivot.localRotation = turned;
        pivot.localPosition = tipPos - turned * tipLocal;
        if (sfx != null) sfx.Play("unlock", 1f);
        Gfx.Burst(glitter, box.KeyholeWorld, rig.transform.up * 0.1f, 30, 0.1f);

        // De sleutel blijft in het slot zitten en gaat mee met het luik
        pivot.SetParent(box.HatchPivot, true);
        Destroy(keyRoot.gameObject); // de lege groep van daarnet
        keyRoot = pivot;             // zodat ResetPuzzle de sleutel later opruimt
        yield return new WaitForSeconds(0.3f);
    }
}
