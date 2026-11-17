using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MazeDifficulty { Makkelijk, Normaal, Moeilijk, Extreem }

// Fase 3: kanteldoolhof met knikker.
public class MazeGame : MonoBehaviour
{
    [Serializable]
    public struct PartSettings
    {
        public int cols, rows, trapHoles, pathHoles;
        public bool gate;
        public PartSettings(int c, int r, int traps, int pathH, bool g) { cols = c; rows = r; trapHoles = traps; pathHoles = pathH; gate = g; }
    }

    [Header("Fysica")]
    // Besturing met een "curve": licht kantelen = traag en precies, hevig kantelen = heel snel.
    [Tooltip("Versnelling bij volle kanteling (m/s²)")]
    public float gravity = 6f;
    [Tooltip("Vanaf zoveel graden kanteling gaat de knikker op volle kracht")]
    public float fullTiltDegrees = 18f;
    [Tooltip("Vorm van de curve: 1 = recht evenredig, hoger = meer controle bij kleine kantelingen")]
    [Range(1f, 3f)] public float tiltCurve = 1.8f;
    [Tooltip("Graden kanteling die genegeerd worden (tegen trillen van de tracking)")]
    public float deadzoneDegrees = 0.6f;
    [Tooltip("Remt de knikker af (hoger = sneller stil, meer controle)")]
    public float rollingFriction = 1.4f;
    [Range(0f, 1f)] public float bounciness = 0.35f;
    [Tooltip("Topsnelheid bij een lichte kanteling (m/s)")]
    public float slowSpeed = 0.07f;
    [Tooltip("Topsnelheid bij volle kanteling (m/s)")]
    public float maxSpeed = 0.55f;
    [SerializeField, HideInInspector] private int tuningVersion = 0;

    // Oude scènes hebben nog de oude (te trage) waarden opgeslagen: één keer de nieuwe doorzetten.
    // Daarna (tuningVersion = 3) blijven jouw eigen aanpassingen in de Inspector gewoon behouden.
    private void ApplyTuning()
    {
        if (tuningVersion >= 3) return;
        gravity = 6f; fullTiltDegrees = 18f; tiltCurve = 1.8f; deadzoneDegrees = 0.6f;
        rollingFriction = 1.4f; slowSpeed = 0.07f; maxSpeed = 0.55f;
        tuningVersion = 3;
    }
    private void OnValidate() => ApplyTuning();

    [Header("Kleuren")]
    public Color wallColor = new Color(0.86f, 0.68f, 0.42f);
    public Color tileA = new Color(0.16f, 0.12f, 0.3f), tileB = new Color(0.2f, 0.15f, 0.36f);
    public Color ballColor = new Color(0.35f, 0.95f, 1f);
    public Color portalColor = new Color(0.75f, 0.35f, 1f);

    [Header("Modellen (Assets/Models) - leeg = in code opbouwen")]
    public GameObject wallPrefab;
    public GameObject gatePrefab;
    public GameObject tileAPrefab, tileBPrefab;
    public GameObject holePrefab;
    public GameObject switchPrefab;
    public GameObject ballPrefab;

    public const int LampsNeeded = 3;

    public bool Running { get; set; }
    public bool Escaped { get; private set; }
    public MazeDifficulty Difficulty { get; private set; }
    public int PartIndex { get; private set; }
    public int PartCount => parts.Count;
    public int Lamps { get; private set; }
    public int Falls { get; private set; }
    // Levens (0 = verloren).
    public int Lives { get; private set; }
    public int MaxLives { get; private set; }
    public bool Failed { get; private set; }
    // Sterrensplinter gevonden?
    public bool FoundStar { get; private set; }
    public float Elapsed { get; private set; }
    public Vector3 BallWorld => ball != null ? ball.position : transform.position;

    // Meldingen voor de UI.
    public event Action<string> Toast;

    // Omschrijving per moeilijkheid.
    public static string Describe(MazeDifficulty d)
    {
        switch (d)
        {
            case MazeDifficulty.Makkelijk: return "Klein doolhof, 1 valkuil - 5 levens";
            case MazeDifficulty.Normaal: return "Groter, gaten en een poort - 5 levens";
            case MazeDifficulty.Moeilijk: return "2 delen met een portaal - 3 levens";
            default: return "3 delen, veel gaten, terug naar deel 1 als je valt - 3 levens";
        }
    }

    public static int LivesFor(MazeDifficulty d) => d == MazeDifficulty.Makkelijk || d == MazeDifficulty.Normaal ? 5 : 3;

    private static PartSettings[] Preset(MazeDifficulty d)
    {
        switch (d)
        {
            case MazeDifficulty.Makkelijk: return new[] { new PartSettings(4, 5, 1, 0, false) };
            case MazeDifficulty.Normaal: return new[] { new PartSettings(5, 7, 2, 1, true) };
            case MazeDifficulty.Moeilijk: return new[] { new PartSettings(5, 6, 2, 1, false), new PartSettings(6, 8, 3, 1, true) };
            default: return new[] { new PartSettings(5, 7, 2, 1, true), new PartSettings(6, 8, 3, 2, true), new PartSettings(7, 9, 4, 2, true) };
        }
    }

    // ------------------------------------------------------------------ Data per deel

    private class Wall { public Vector2 min, max; public bool gate; public Transform go; public bool active = true; }
    private class Hole { public Vector2 pos; public float r; }
    private class Lamp { public Vector2 pos; public bool taken; public Transform go; public float phase; }

    private class Part
    {
        public int cols, rows;
        public float cell, ballR, wallH;
        public Vector2 origin, start, exit, switchPos;
        public bool hasSwitch, switchPressed, isLast;
        public bool hasStar, starTaken;
        public Vector2 starPos;
        public Transform starGo;
        public Wall gate;
        public readonly List<Wall> walls = new List<Wall>();
        public readonly List<Hole> holes = new List<Hole>();
        public readonly List<Lamp> lamps = new List<Lamp>();
        public bool LampsDone { get { foreach (var l in lamps) if (!l.taken) return false; return true; } }
    }

    private GenieBoxRig rig;
    private HollowBox box;
    private TiltInput tilt;
    private SoundFx sfx;

    private readonly List<Part> parts = new List<Part>();
    private readonly Dictionary<Part, Vector2> starCandidates = new Dictionary<Part, Vector2>();
    private Part P => parts[PartIndex];
    private Transform root, partRoot, ball, exitPortal, switchPlate;
    private Material portalMat;
    private ParticleSystem trail, sparkles;
    private float floorY;
    private Vector2 pos, vel;
    private float bumpCooldown, denyCooldown;
    private bool busy; // vallen of van deel wisselen

    private void Start()
    {
        ApplyTuning();
        rig = GetComponentInParent<GenieBoxRig>();
        box = FindFirstObjectByType<HollowBox>();
        tilt = FindFirstObjectByType<TiltInput>();
        sfx = FindFirstObjectByType<SoundFx>();
    }

    // ================================================================== Opbouw

    public void Clear()
    {
        StopAllCoroutines();
        Running = false;
        busy = false;
        if (root != null) Destroy(root.gameObject);
        root = partRoot = ball = null;
        parts.Clear();
        starCandidates.Clear();
    }

    // Nieuw doolhof (alle delen).
    public void Build(MazeDifficulty difficulty)
    {
        Clear();
        if (rig == null) Start();
        Difficulty = difficulty;
        Escaped = false;
        Failed = false;
        FoundStar = false;
        Lamps = 0;
        Falls = 0;
        Elapsed = 0f;
        PartIndex = 0;
        MaxLives = Lives = LivesFor(difficulty);

        root = Gfx.Group("Maze", box.Interior);
        floorY = box.FloorLocalY;
        var rnd = new System.Random();
        PartSettings[] preset = Preset(difficulty);
        int[] lampSplit = preset.Length == 1 ? new[] { 3 } : preset.Length == 2 ? new[] { 1, 2 } : new[] { 1, 1, 1 };
        for (int i = 0; i < preset.Length; i++)
        {
            var part = GeneratePart(preset[i], lampSplit[i], rnd);
            part.isLast = i == preset.Length - 1;
            parts.Add(part);
        }

        // Geheim: één verborgen sterrensplinter in een willekeurig deel
        var starPart = parts[rnd.Next(parts.Count)];
        if (starPart.hasStar == false && starCandidates.TryGetValue(starPart, out Vector2 sp)) { starPart.hasStar = true; starPart.starPos = sp; }

        ball = Place(Spawn(ballPrefab, () => BuildBall(null, ballColor)), Vector3.zero, Vector3.one).transform;
        trail = Gfx.Particles("BallTrail", root, rig.transform, Gfx.Sparkle, Gfx.Blend.Additive, 0.6f, 0.01f, 0.01f, new Color(0.5f, 1f, 1f), 0f, 300);
        sparkles = Gfx.Particles("MazeSparkles", root, rig.transform, Gfx.Sparkle, Gfx.Blend.Additive, 1.2f, 0.008f, 0.1f, new Color(1f, 0.9f, 0.5f), -0.05f, 400);

        ShowPart(0, false);
        ResetBall();
    }

    // ================================================================== Generatie (enkel data)

    private Part GeneratePart(PartSettings s, int lampCount, System.Random rnd)
    {
        var part = new Part { cols = s.cols, rows = s.rows };
        Vector2 win = box.WindowSize - new Vector2(0.008f, 0.008f);
        part.cell = Mathf.Min(win.x / s.cols, win.y / s.rows);
        part.origin = new Vector2(-s.cols * part.cell / 2f, -s.rows * part.cell / 2f);
        part.ballR = part.cell * 0.28f;
        part.wallH = part.cell * 0.55f;
        int cols = s.cols, rows = s.rows;
        float cell = part.cell;

        Generate(rnd, cols, rows, out bool[,] hOpen, out bool[,] vOpen);

        var start = new Vector2Int(cols / 2, 0); // vooraan in het midden (dichtst bij de speler)
        int[,] dist = Bfs(start, cols, rows, hOpen, vOpen, null, out Vector2Int[,] parent);
        Vector2Int exit = start;
        for (int c = 0; c < cols; c++) for (int r = 0; r < rows; r++) if (dist[c, r] > dist[exit.x, exit.y]) exit = new Vector2Int(c, r);
        var path = new List<Vector2Int>();
        for (var p = exit; p != start; p = parent[p.x, p.y]) path.Add(p);
        path.Add(start);
        path.Reverse();
        var onPath = new HashSet<Vector2Int>(path);

        var deadEnds = new List<Vector2Int>();
        for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
            {
                var v = new Vector2Int(c, r);
                if (!onPath.Contains(v) && Openings(v, cols, rows, hOpen, vOpen) == 1) deadEnds.Add(v);
            }
        Shuffle(deadEnds, rnd);
        deadEnds.Sort((a, b) => dist[b.x, b.y].CompareTo(dist[a.x, a.y]));

        var used = new HashSet<Vector2Int> { start, exit };
        part.start = CellCenter(part, start);
        part.exit = CellCenter(part, exit);

        // ---- Poort + drukplaat
        Vector2Int gateA = new Vector2Int(-1, -1), gateB = new Vector2Int(-1, -1);
        if (s.gate && path.Count > 5)
        {
            int k = Mathf.Clamp(Mathf.RoundToInt(path.Count * 0.55f), 2, path.Count - 3);
            var ga = path[k]; var gb = path[k + 1];
            int[,] reach = Bfs(start, cols, rows, hOpen, vOpen, new[] { ga, gb }, out _);
            Vector2Int sw = new Vector2Int(-1, -1);
            foreach (var d in deadEnds) if (reach[d.x, d.y] >= 0 && !used.Contains(d)) { sw = d; break; }
            if (sw.x >= 0)
            {
                gateA = ga; gateB = gb;
                part.hasSwitch = true;
                part.switchPos = CellCenter(part, sw);
                used.Add(sw);
            }
        }

        // ---- Lampjes in doodlopende gangen
        var lampCells = new List<Vector2Int>();
        foreach (var d in deadEnds) { if (lampCells.Count >= lampCount) break; if (!used.Contains(d)) { lampCells.Add(d); used.Add(d); } }
        for (int i = path.Count - 2; i > 0 && lampCells.Count < lampCount; i -= 2)
            if (!used.Contains(path[i])) { lampCells.Add(path[i]); used.Add(path[i]); }
        foreach (var lc in lampCells) part.lamps.Add(new Lamp { pos = CellCenter(part, lc), phase = (float)rnd.NextDouble() * 6f });

        // ---- Valkuilen in de andere doodlopende gangen
        int traps = 0;
        foreach (var d in deadEnds)
        {
            if (traps >= s.trapHoles) break;
            if (used.Contains(d)) continue;
            part.holes.Add(new Hole { pos = CellCenter(part, d), r = cell * 0.3f });
            used.Add(d);
            traps++;
        }

        // ---- Kandidaat-plek voor de geheime sterrensplinter: een vrije doodlopende gang
        foreach (var d in deadEnds)
            if (!used.Contains(d)) { starCandidates[part] = CellCenter(part, d); used.Add(d); break; }
        if (!starCandidates.ContainsKey(part))
            for (int c = 0; c < cols && !starCandidates.ContainsKey(part); c++)
                for (int r = 0; r < rows; r++)
                {
                    var v = new Vector2Int(c, r);
                    if (!onPath.Contains(v) && !used.Contains(v)) { starCandidates[part] = CellCenter(part, v); used.Add(v); break; }
                }

        // ---- Gaten OP de route, opzij tegen een muur
        int pathHoles = 0;
        for (int i = 2; i < path.Count - 2 && pathHoles < s.pathHoles; i += 3)
        {
            var prev = path[i - 1]; var cur = path[i]; var next = path[i + 1];
            if (used.Contains(cur) || cur == gateA || cur == gateB) continue;
            Vector2Int dir = next - cur;
            if (cur - prev != dir) continue;
            Vector2Int side = new Vector2Int(dir.y, dir.x);
            if (IsOpen(cur, cur + side, cols, rows, hOpen, vOpen)) side = -side;
            if (IsOpen(cur, cur + side, cols, rows, hOpen, vOpen)) continue;
            part.holes.Add(new Hole { pos = CellCenter(part, cur) + (Vector2)side * cell * 0.27f, r = cell * 0.27f });
            used.Add(cur);
            pathHoles++;
        }

        // ---- Muren (als rechthoeken in het vlak van de doos)
        float t = cell * 0.12f;
        for (int c = 0; c < cols; c++)
            for (int r = 0; r <= rows; r++)
            {
                if (r > 0 && r < rows && hOpen[c, r - 1]) continue;
                float z = part.origin.y + r * cell, x0 = part.origin.x + c * cell;
                part.walls.Add(new Wall { min = new Vector2(x0 - t / 2f, z - t / 2f), max = new Vector2(x0 + cell + t / 2f, z + t / 2f) });
            }
        for (int c = 0; c <= cols; c++)
            for (int r = 0; r < rows; r++)
            {
                if (c > 0 && c < cols && vOpen[c - 1, r]) continue;
                float x = part.origin.x + c * cell, z0 = part.origin.y + r * cell;
                part.walls.Add(new Wall { min = new Vector2(x - t / 2f, z0 - t / 2f), max = new Vector2(x + t / 2f, z0 + cell + t / 2f) });
            }
        if (part.hasSwitch)
        {
            Vector2 a = CellCenter(part, gateA), b = CellCenter(part, gateB), mid = (a + b) * 0.5f;
            Vector2 half = gateA.y != gateB.y ? new Vector2(cell / 2f, t / 2f) : new Vector2(t / 2f, cell / 2f);
            part.gate = new Wall { min = mid - half, max = mid + half, gate = true };
            part.walls.Add(part.gate);
        }
        return part;
    }

    // ================================================================== Visueel (per deel)

    // Bouwt een deel op.
    private void ShowPart(int index, bool rise)
    {
        if (partRoot != null) Destroy(partRoot.gameObject);
        PartIndex = index;
        Part part = P;
        partRoot = Gfx.Group("Part" + (index + 1), root);
        float cell = part.cell;

        for (int c = 0; c < part.cols; c++)
            for (int r = 0; r < part.rows; r++)
            {
                Vector2 cc = part.origin + new Vector2((c + 0.5f) * cell, (r + 0.5f) * cell);
                bool even = (c + r) % 2 == 0;
                Color tint = index == 0 ? (even ? tileA : tileB) : Color.Lerp(even ? tileA : tileB, portalColor * 0.4f, 0.25f * index);
                Place(Spawn(even ? tileAPrefab : tileBPrefab, () => BuildTile(null, tint)), new Vector3(cc.x, floorY + 0.0005f, cc.y), new Vector3(cell, 0.001f, cell));
            }

        foreach (var w in part.walls)
        {
            if (w.gate && !w.active) { w.go = null; continue; }
            Vector2 size = w.max - w.min, c = (w.min + w.max) * 0.5f;
            var go = Place(Spawn(w.gate ? gatePrefab : wallPrefab, () => w.gate ? BuildGate(null) : BuildWall(null, wallColor)),
                           new Vector3(c.x, floorY + part.wallH / 2f, c.y), new Vector3(size.x, part.wallH, size.y));
            w.go = go.transform;
        }

        switchPlate = null;
        if (part.hasSwitch)
            switchPlate = Place(Spawn(switchPrefab, () => BuildSwitch(null)), new Vector3(part.switchPos.x, floorY + (part.switchPressed ? 0.0005f : 0.0015f), part.switchPos.y),
                                new Vector3(cell * 0.5f, 0.0012f, cell * 0.5f)).transform;

        foreach (var hole in part.holes)
            Place(Spawn(holePrefab, () => BuildHole(null)), new Vector3(hole.pos.x, floorY, hole.pos.y), new Vector3(hole.r * 2f, 1f, hole.r * 2f));

        foreach (var l in part.lamps)
        {
            l.go = null;
            if (l.taken) continue;
            l.go = BuildLamp(partRoot, new Vector3(l.pos.x, floorY + cell * 0.25f, l.pos.y), cell * 0.35f);
        }

        part.starGo = null;
        if (part.hasStar && !part.starTaken) part.starGo = BuildStar(partRoot, new Vector3(part.starPos.x, floorY + cell * 0.3f, part.starPos.y), cell * 0.4f);

        portalMat = Gfx.NewUnlit(Gfx.Blend.Additive, Gfx.Ring, 12);
        exitPortal = Gfx.Quad(partRoot, new Vector3(part.exit.x, floorY + 0.002f, part.exit.y), Gfx.FaceUp, Vector2.one * cell * (part.isLast ? 0.85f : 0.95f), portalMat, part.isLast ? "Exit" : "Portal").transform;
        if (!part.isLast)
        {
            // tweede, tegendraaiende ring voor een "draaikolk"
            var inner = Gfx.Quad(exitPortal, new Vector3(0f, 0f, -0.0005f), Quaternion.identity, Vector2.one * 0.6f, portalMat, "PortalInner");
            inner.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
        }

        var startMat = Gfx.NewUnlit(Gfx.Blend.Additive, Gfx.SoftDot, 12);
        startMat.color = new Color(0.3f, 0.9f, 1f, 0.5f);
        Gfx.Quad(partRoot, new Vector3(part.start.x, floorY + 0.0015f, part.start.y), Gfx.FaceUp, Vector2.one * cell * 0.7f, startMat, "StartGlow");

        // knikker en spoor passen zich aan de grootte van dit deel aan
        if (ball != null) ball.localScale = Vector3.one * part.ballR * 2f;
        if (trail != null) { var m = trail.main; m.startSize = new ParticleSystem.MinMaxCurve(part.ballR * 0.45f, part.ballR * 0.9f); }

        if (rise) StartCoroutine(RiseWalls(part));
    }

    private Transform BuildLamp(Transform parent, Vector3 localPos, float sz)
    {
        var lampRoot = Gfx.Group("Lamp", parent);
        lampRoot.localPosition = localPos;
        Gfx.Prim(PrimitiveType.Sphere, lampRoot, Vector3.zero, new Vector3(sz, sz * 0.55f, sz * 0.75f), new Color(1f, 0.8f, 0.3f), 0.5f, "Body");
        Gfx.Prim(PrimitiveType.Cylinder, lampRoot, new Vector3(sz * 0.55f, sz * 0.12f, 0f), new Vector3(sz * 0.18f, sz * 0.3f, sz * 0.18f), new Color(1f, 0.75f, 0.25f), 0.5f, "Spout")
            .transform.localRotation = Quaternion.Euler(0f, 0f, -60f);
        var flameMat = Gfx.NewUnlit(Gfx.Blend.Additive, Gfx.SoftDot, 15);
        flameMat.color = new Color(1f, 0.6f, 0.2f, 1f);
        Gfx.Quad(lampRoot, new Vector3(sz * 0.75f, sz * 0.4f, 0f), Quaternion.identity, Vector2.one * sz * 0.6f, flameMat, "Flame");
        return lampRoot;
    }

    // Gouden ster.
    private Transform BuildStar(Transform parent, Vector3 localPos, float size)
    {
        var star = Gfx.Group("StarShard", parent);
        star.localPosition = localPos;
        var gold = new Color(1f, 0.85f, 0.35f);
        Gfx.Prim(PrimitiveType.Cube, star, Vector3.zero, new Vector3(size, size, size * 0.2f), gold, 1.4f, "A").transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Gfx.Prim(PrimitiveType.Cube, star, Vector3.zero, new Vector3(size, size, size * 0.2f), gold, 1.4f, "B");
        var glowMat = Gfx.NewUnlit(Gfx.Blend.Additive, Gfx.SoftDot, 15);
        glowMat.color = new Color(1f, 0.8f, 0.3f, 0.8f);
        Gfx.Quad(star, Vector3.zero, Quaternion.identity, Vector2.one * size * 2.2f, glowMat, "Glow");
        return star;
    }

    private IEnumerator RiseWalls(Part part)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            float h = part.wallH * Mathf.SmoothStep(0f, 1f, t);
            foreach (var w in part.walls)
            {
                if (w.go == null) continue;
                w.go.localScale = new Vector3(w.go.localScale.x, Mathf.Max(0.0001f, h), w.go.localScale.z);
                w.go.localPosition = new Vector3(w.go.localPosition.x, floorY + h / 2f, w.go.localPosition.z);
            }
            yield return null;
        }
        foreach (var w in part.walls)
        {
            if (w.go == null) continue;
            w.go.localScale = new Vector3(w.go.localScale.x, part.wallH, w.go.localScale.z);
            w.go.localPosition = new Vector3(w.go.localPosition.x, floorY + part.wallH / 2f, w.go.localPosition.z);
        }
    }

    private IEnumerator SinkWalls(Part part)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.45f)
        {
            float h = part.wallH * (1f - t * t);
            foreach (var w in part.walls)
            {
                if (w.go == null) continue;
                w.go.localScale = new Vector3(w.go.localScale.x, Mathf.Max(0.0001f, h), w.go.localScale.z);
                w.go.localPosition = new Vector3(w.go.localPosition.x, floorY + h / 2f, w.go.localPosition.z);
            }
            yield return null;
        }
    }

    // ================================================================== Onderdelen (prefab of code)

    // Prefab of code-onderdeel.
    private GameObject Spawn(GameObject prefab, Func<GameObject> build)
    {
        GameObject go = prefab != null ? Instantiate(prefab) : build();
        go.transform.SetParent(partRoot != null ? partRoot : root, false);
        return go;
    }

    private static GameObject Place(GameObject go, Vector3 localPos, Vector3 localScale)
    {
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        return go;
    }

    // Bouwstenen op eenheidsgrootte (1 m): het doolhof schaalt ze naar de juiste maat.
    // Statisch, zodat de editor-knop "Genie in a Box/Maak modellen" er prefabs van kan maken.
    public static GameObject BuildWall(Transform parent, Color color) =>
        Gfx.Prim(PrimitiveType.Cube, parent, Vector3.zero, Vector3.one, color, 0.08f, "MazeWall");

    public static GameObject BuildGate(Transform parent) =>
        Gfx.Prim(PrimitiveType.Cube, parent, Vector3.zero, Vector3.one, new Color(1f, 0.25f, 0.3f), 0.9f, "MazeGate");

    public static GameObject BuildTile(Transform parent, Color color) =>
        Gfx.Prim(PrimitiveType.Cube, parent, Vector3.zero, Vector3.one, color, 0f, "MazeTile");

    public static GameObject BuildSwitch(Transform parent) =>
        Gfx.Prim(PrimitiveType.Cylinder, parent, Vector3.zero, Vector3.one, new Color(1f, 0.3f, 0.3f), 0.8f, "MazeSwitch");

    public static GameObject BuildBall(Transform parent, Color color) =>
        Gfx.Prim(PrimitiveType.Sphere, parent, Vector3.zero, Vector3.one, color, 1.2f, "MazeBall");

    // Gat met diameter 1.
    public static GameObject BuildHole(Transform parent)
    {
        var holeRoot = Gfx.Group("MazeHole", parent);
        Gfx.Prim(PrimitiveType.Cylinder, holeRoot, new Vector3(0f, 0.0011f, 0f), new Vector3(1.1f, 0.0002f, 1.1f), new Color(0.4f, 0.2f, 0.5f), 0.4f, "HoleRim");
        Gfx.Prim(PrimitiveType.Cylinder, holeRoot, new Vector3(0f, 0.0013f, 0f), new Vector3(1f, 0.0002f, 1f), Color.black, 0f, "HoleDark");
        return holeRoot.gameObject;
    }

    // ================================================================== Doolhof-algoritmes

    private static void Generate(System.Random rnd, int cols, int rows, out bool[,] hOpen, out bool[,] vOpen)
    {
        hOpen = new bool[cols, rows]; // opening tussen (c,r) en (c,r+1)
        vOpen = new bool[cols, rows]; // opening tussen (c,r) en (c+1,r)
        var visited = new bool[cols, rows];
        var stack = new Stack<Vector2Int>();
        var first = new Vector2Int(rnd.Next(cols), rnd.Next(rows));
        visited[first.x, first.y] = true;
        stack.Push(first);
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (stack.Count > 0)
        {
            var cur = stack.Peek();
            var options = new List<Vector2Int>();
            foreach (var d in dirs)
            {
                var n = cur + d;
                if (n.x >= 0 && n.y >= 0 && n.x < cols && n.y < rows && !visited[n.x, n.y]) options.Add(n);
            }
            if (options.Count == 0) { stack.Pop(); continue; }
            var next = options[rnd.Next(options.Count)];
            if (cur.x == next.x) hOpen[cur.x, Mathf.Min(cur.y, next.y)] = true;
            else vOpen[Mathf.Min(cur.x, next.x), cur.y] = true;
            visited[next.x, next.y] = true;
            stack.Push(next);
        }
    }

    private static bool IsOpen(Vector2Int a, Vector2Int b, int cols, int rows, bool[,] hOpen, bool[,] vOpen)
    {
        if (b.x < 0 || b.y < 0 || b.x >= cols || b.y >= rows) return false;
        if (a.x == b.x) return hOpen[a.x, Mathf.Min(a.y, b.y)];
        return vOpen[Mathf.Min(a.x, b.x), a.y];
    }

    private static int Openings(Vector2Int c, int cols, int rows, bool[,] hOpen, bool[,] vOpen)
    {
        int n = 0;
        foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            if (IsOpen(c, c + d, cols, rows, hOpen, vOpen)) n++;
        return n;
    }

    private static int[,] Bfs(Vector2Int start, int cols, int rows, bool[,] hOpen, bool[,] vOpen, Vector2Int[] blocked, out Vector2Int[,] parent)
    {
        var dist = new int[cols, rows];
        parent = new Vector2Int[cols, rows];
        for (int c = 0; c < cols; c++) for (int r = 0; r < rows; r++) dist[c, r] = -1;
        var q = new Queue<Vector2Int>();
        dist[start.x, start.y] = 0;
        q.Enqueue(start);
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            foreach (var d in dirs)
            {
                var n = cur + d;
                if (!IsOpen(cur, n, cols, rows, hOpen, vOpen) || dist[n.x, n.y] >= 0) continue;
                if (blocked != null && ((cur == blocked[0] && n == blocked[1]) || (cur == blocked[1] && n == blocked[0]))) continue;
                dist[n.x, n.y] = dist[cur.x, cur.y] + 1;
                parent[n.x, n.y] = cur;
                q.Enqueue(n);
            }
        }
        return dist;
    }

    private static void Shuffle<T>(List<T> list, System.Random rnd)
    {
        for (int i = list.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
    }

    private static Vector2 CellCenter(Part p, Vector2Int c) => p.origin + new Vector2((c.x + 0.5f) * p.cell, (c.y + 0.5f) * p.cell);

    // ================================================================== Spel

    private void ResetBall()
    {
        pos = P.start;
        vel = Vector2.zero;
        if (ball != null)
        {
            ball.gameObject.SetActive(true);
            ball.localPosition = new Vector3(pos.x, floorY + P.ballR, pos.y);
            ball.localScale = Vector3.one * P.ballR * 2f;
        }
    }

    private bool ExitOpenHere => P.isLast ? Lamps >= LampsNeeded : P.LampsDone;

    private void Update()
    {
        if (root == null || parts.Count == 0) return;
        float time = Time.time;
        Part part = P;

        // Animaties: lampjes dobberen en draaien, portaal draait en pulseert
        foreach (var l in part.lamps)
        {
            if (l.taken || l.go == null) continue;
            l.go.localPosition = new Vector3(l.pos.x, floorY + part.cell * 0.25f + Mathf.Sin(time * 2.2f + l.phase) * part.cell * 0.06f, l.pos.y);
            l.go.localRotation = Quaternion.Euler(0f, time * 50f + l.phase * 30f, 0f);
        }
        if (part.starGo != null)
        {
            part.starGo.localPosition = new Vector3(part.starPos.x, floorY + part.cell * 0.3f + Mathf.Sin(time * 3f) * part.cell * 0.05f, part.starPos.y);
            part.starGo.localRotation = Quaternion.Euler(0f, time * 120f, 0f);
        }
        if (exitPortal != null)
        {
            bool open = ExitOpenHere;
            exitPortal.localRotation = Gfx.FaceUp * Quaternion.Euler(0f, 0f, time * (open ? 160f : 25f));
            float pulse = 0.7f + 0.3f * Mathf.Sin(time * (open ? 6f : 2f));
            Color c = part.isLast ? new Color(1f, 0.85f, 0.4f) : portalColor;
            portalMat.color = open ? new Color(c.r, c.g, c.b, pulse) : new Color(0.5f, 0.4f, 0.7f, 0.3f * pulse);
            if (open && UnityEngine.Random.value < 0.35f)
                Gfx.Burst(sparkles, root.TransformPoint(new Vector3(part.exit.x, floorY + 0.004f, part.exit.y)), rig.transform.up * 0.06f, 1, 0.02f, part.isLast ? (Color?)null : portalColor);
        }

        if (!Running || busy || ball == null) { if (sfx != null) sfx.SetRoll(0f); return; }

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;
        Elapsed += dt;
        bumpCooldown -= dt;
        denyCooldown -= dt;

        // ---- Zwaartekracht in het vlak van de doos
        Vector3 g = tilt != null ? tilt.LocalGravity(rig.transform) : Vector3.down;
        Vector2 gPlane = new Vector2(g.x, g.z);
        float mag = gPlane.magnitude;
        float tiltDeg = Mathf.Asin(Mathf.Clamp01(mag)) * Mathf.Rad2Deg;
        // 0 = net boven de dode zone, 1 = volle kanteling; de curve maakt kleine kantelingen extra zacht
        float tilt01 = Mathf.Clamp01((tiltDeg - deadzoneDegrees) / Mathf.Max(1f, fullTiltDegrees - deadzoneDegrees));
        float power = Mathf.Pow(tilt01, tiltCurve);
        Vector2 accel = tilt01 > 0f && mag > 0.0001f ? gPlane / mag * power * gravity : Vector2.zero;
        // topsnelheid hangt af van hoe ver je kantelt: licht = kruipen, hevig = racen
        float speedCap = Mathf.Lerp(slowSpeed, maxSpeed, power);

        // ---- Integreren in kleine stapjes (geen tunnelen door muren)
        float ballR = part.ballR;
        int steps = Mathf.Clamp(Mathf.CeilToInt(vel.magnitude * dt / (ballR * 0.4f)), 2, 12);
        float h = dt / steps, impact = 0f;
        for (int s = 0; s < steps; s++)
        {
            vel += accel * h;
            vel *= Mathf.Exp(-rollingFriction * h);
            float sp = vel.magnitude;
            if (sp > speedCap) vel = vel / sp * Mathf.MoveTowards(sp, speedCap, 1.5f * h); // zacht afremmen, geen ruk
            if (vel.magnitude > maxSpeed) vel = vel.normalized * maxSpeed;
            pos += vel * h;
            foreach (var w in part.walls)
            {
                if (!w.active) continue;
                Vector2 closest = new Vector2(Mathf.Clamp(pos.x, w.min.x, w.max.x), Mathf.Clamp(pos.y, w.min.y, w.max.y));
                Vector2 d = pos - closest;
                float dist = d.magnitude;
                if (dist >= ballR) continue;
                Vector2 n;
                if (dist < 1e-6f)
                {
                    Vector2 c = (w.min + w.max) * 0.5f, he = (w.max - w.min) * 0.5f, o = pos - c;
                    n = (he.x - Mathf.Abs(o.x) < he.y - Mathf.Abs(o.y)) ? new Vector2(Mathf.Sign(o.x), 0f) : new Vector2(0f, Mathf.Sign(o.y));
                    dist = 0f;
                }
                else n = d / dist;
                pos += n * (ballR - dist);
                float vn = Vector2.Dot(vel, n);
                if (vn < 0f) { vel -= (1f + bounciness) * vn * n; impact = Mathf.Max(impact, -vn); }
            }
        }
        if (float.IsNaN(pos.x) || float.IsNaN(pos.y)) ResetBall();

        // (bewust geen botsgeluid: rustiger spelen)

        // ---- Knikker tonen + laten rollen
        Vector3 prevLocal = ball.localPosition;
        ball.localPosition = new Vector3(pos.x, floorY + ballR, pos.y);
        Vector3 moved = ball.localPosition - prevLocal;
        if (moved.sqrMagnitude > 1e-10f)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, moved.normalized);
            ball.localRotation = Quaternion.AngleAxis(moved.magnitude / ballR * Mathf.Rad2Deg, axis) * ball.localRotation;
        }
        float speed = vel.magnitude;
        if (sfx != null) sfx.SetRoll(speed);
        if (speed > 0.03f && UnityEngine.Random.value < speed * 6f) Gfx.Burst(trail, ball.position, Vector3.zero, 1, 0.005f);

        CheckPickups(part);
    }

    private void CheckPickups(Part part)
    {
        // Lampjes
        foreach (var l in part.lamps)
        {
            if (l.taken || Vector2.Distance(pos, l.pos) > part.ballR + part.cell * 0.2f) continue;
            l.taken = true;
            Lamps++;
            if (sfx != null) sfx.Play("pling", 1f, 1f + Lamps * 0.12f);
            if (l.go != null)
            {
                Gfx.Burst(sparkles, l.go.position, rig.transform.up * 0.08f, 25, 0.08f);
                StartCoroutine(LampFly(l.go));
            }
            if (ExitOpenHere)
            {
                Toast?.Invoke(part.isLast ? "Alle lampjes! De uitgang is open!" : $"Lampje {Lamps}/{LampsNeeded} - het portaal gaat open!");
                if (sfx != null) sfx.Play("sparkle", 1f);
            }
            else Toast?.Invoke($"Lampje {Lamps}/{LampsNeeded}");
        }

        // Geheime sterrensplinter
        if (part.hasStar && !part.starTaken && Vector2.Distance(pos, part.starPos) < part.ballR + part.cell * 0.2f)
        {
            part.starTaken = true;
            FoundStar = true;
            if (sfx != null) { sfx.Play("sparkle", 1f); sfx.Play("pling", 0.8f, 1.6f); }
            if (part.starGo != null) { Gfx.Burst(sparkles, part.starGo.position, rig.transform.up * 0.1f, 40, 0.1f); StartCoroutine(LampFly(part.starGo)); }
            Toast?.Invoke("Wat glinstert daar...? Een STERRENSPLINTER!");
        }

        // Drukplaat -> poort zakt weg
        if (part.hasSwitch && !part.switchPressed && Vector2.Distance(pos, part.switchPos) < part.cell * 0.3f)
        {
            part.switchPressed = true;
            if (sfx != null) { sfx.Play("click", 0.8f); sfx.Play("gate", 0.9f); }
            Toast?.Invoke("Klik! Een poort gaat open...");
            StartCoroutine(OpenGate(part));
        }

        // Gaten
        foreach (var hole in part.holes)
            if (Vector2.Distance(pos, hole.pos) < hole.r * 0.75f) { StartCoroutine(Fall(hole)); return; }

        // Uitgang / portaal
        if (Vector2.Distance(pos, part.exit) < part.cell * 0.3f)
        {
            if (ExitOpenHere)
            {
                if (part.isLast)
                {
                    Escaped = true;
                    Running = false;
                    if (sfx != null) sfx.SetRoll(0f);
                }
                else StartCoroutine(ThroughPortal(PartIndex + 1));
            }
            else if (denyCooldown <= 0f)
            {
                int missing = 0;
                foreach (var l in part.lamps) if (!l.taken) missing++;
                Toast?.Invoke(part.isLast ? $"De uitgang is nog dicht: zoek {LampsNeeded - Lamps} lampje(s)"
                                          : $"Het portaal is nog dicht: zoek {missing} lampje(s) in dit deel");
                if (sfx != null) sfx.Play("deny", 0.5f);
                denyCooldown = 1.5f;
            }
        }
    }

    private IEnumerator LampFly(Transform lamp)
    {
        Vector3 from = lamp.localPosition;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
        {
            if (lamp == null) yield break;
            lamp.localPosition = from + Vector3.up * t * 0.08f;
            lamp.localScale = Vector3.one * (1f - t);
            lamp.Rotate(0f, Time.deltaTime * 720f, 0f);
            yield return null;
        }
        if (lamp != null) lamp.gameObject.SetActive(false);
    }

    private IEnumerator OpenGate(Part part)
    {
        if (switchPlate != null) switchPlate.localPosition += Vector3.down * 0.001f;
        Transform g = part.gate != null ? part.gate.go : null;
        if (g != null)
        {
            Vector3 from = g.localPosition, to = from + Vector3.down * g.localScale.y;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
            {
                if (g == null) break;
                g.localPosition = Vector3.Lerp(from, to, t) + new Vector3(Mathf.Sin(t * 80f) * 0.0004f, 0f, 0f);
                yield return null;
            }
            if (g != null) g.gameObject.SetActive(false);
        }
        if (part.gate != null) part.gate.active = false;
    }

    // Overgang naar het volgende deel.
    private IEnumerator ThroughPortal(int next)
    {
        busy = true;
        if (sfx != null) { sfx.SetRoll(0f); sfx.Play("whoosh", 1f); sfx.Play("sparkle", 0.8f); }
        Part cur = P;
        Vector3 center = new Vector3(cur.exit.x, floorY + cur.ballR, cur.exit.y);
        Vector3 from = ball.localPosition;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
        {
            float a = t * 720f * Mathf.Deg2Rad, r = (1f - t) * cur.cell * 0.25f;
            ball.localPosition = Vector3.Lerp(from, center, t) + new Vector3(Mathf.Cos(a) * r, -t * cur.ballR * 2f, Mathf.Sin(a) * r);
            ball.localScale = Vector3.one * cur.ballR * 2f * (1f - t);
            if (UnityEngine.Random.value < 0.6f) Gfx.Burst(sparkles, ball.position, rig.transform.up * 0.05f, 2, 0.03f, portalColor);
            yield return null;
        }
        ball.gameObject.SetActive(false);
        yield return SinkWalls(cur);
        yield return EnterPart(next, $"Deel {next + 1}/{PartCount}");
    }

    private IEnumerator EnterPart(int index, string message)
    {
        if (sfx != null) sfx.Play("gate", 0.8f);
        ShowPart(index, true);
        Toast?.Invoke(message);
        yield return new WaitForSeconds(0.6f);
        ResetBall();
        ball.localScale = Vector3.zero;
        if (sfx != null) sfx.Play("poof", 0.6f);
        Gfx.Burst(sparkles, ball.position, rig.transform.up * 0.05f, 20, 0.05f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
        {
            ball.localScale = Vector3.one * P.ballR * 2f * t;
            yield return null;
        }
        ball.localScale = Vector3.one * P.ballR * 2f;
        vel = Vector2.zero;
        busy = false;
    }

    private IEnumerator Fall(Hole hole)
    {
        busy = true;
        Falls++;
        Lives--;
        if (sfx != null) { sfx.SetRoll(0f); sfx.Play("fall", 0.9f); }
        Part part = P;
        Vector3 from = ball.localPosition, to = new Vector3(hole.pos.x, floorY - part.ballR * 3f, hole.pos.y);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
        {
            ball.localPosition = Vector3.Lerp(from, to, t * t);
            ball.localScale = Vector3.one * part.ballR * 2f * (1f - t * 0.7f);
            yield return null;
        }
        yield return new WaitForSeconds(0.3f);

        if (Lives <= 0)
        {
            // Geen levens meer: de geest blijft gevangen
            ball.gameObject.SetActive(false);
            Failed = true;
            Running = false;
            yield break;
        }

        if (PartIndex > 0)
        {
            // Gevallen in een dieper deel: helemaal terug naar deel 1!
            ball.gameObject.SetActive(false);
            yield return SinkWalls(part);
            yield return EnterPart(0, $"Gevallen! Terug naar deel 1... ({Lives} {(Lives == 1 ? "leven" : "levens")} over)");
            yield break;
        }

        Toast?.Invoke(Lives == 1 ? "Oeps! Nog maar 1 leven..." : $"Oeps! Nog {Lives} levens");
        ResetBall();
        ball.localScale = Vector3.zero;
        if (sfx != null) sfx.Play("poof", 0.6f);
        Gfx.Burst(sparkles, ball.position, rig.transform.up * 0.05f, 20, 0.05f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
        {
            ball.localScale = Vector3.one * part.ballR * 2f * t;
            yield return null;
        }
        ball.localScale = Vector3.one * part.ballR * 2f;
        vel = Vector2.zero;
        busy = false;
    }

    // Knikker zweeft uit de doos.
    public IEnumerator BallRise(Vector3 targetWorld)
    {
        if (ball == null) yield break;
        if (sfx != null) sfx.Play("whoosh", 0.8f);
        Vector3 from = ball.position;
        float baseScale = P.ballR * 2f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.4f)
        {
            float e = t * t * (3f - 2f * t);
            Vector3 p = Vector3.Lerp(from, targetWorld, e);
            p += rig.transform.right * Mathf.Sin(t * 12f) * 0.015f * (1f - t) + rig.transform.forward * Mathf.Cos(t * 12f) * 0.015f * (1f - t);
            ball.position = p;
            ball.localScale = Vector3.one * baseScale * (1f + e * 1.5f);
            Gfx.Burst(trail, p, Vector3.zero, 2, 0.02f);
            yield return null;
        }
        ball.gameObject.SetActive(false);
    }
}
