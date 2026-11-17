using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Geheim: Djinn-rijk en gevecht tegen de Wachter.
public class GuardBattle : MonoBehaviour
{
    [Header("Gevecht")]
    public int playerHearts = 5;
    public int guardHp = 60;
    [Tooltip("Afstand van de Wachter tot de speler (meter)")]
    public float guardDistance = 3f;
    public float fireballSpeed = 1.9f;
    public float boltSpeed = 7f;
    [Tooltip("Hoeveel lager (meter) dan bij de start je gsm moet zijn om onder de zwaardzwaai door te bukken")]
    public float duckDepth = 0.3f;
    [Tooltip("Tempo van de aanvallen: 1 = normaal, hoger = moeilijker, lager = makkelijker")]
    [Range(0.5f, 2f)] public float aggression = 1f;
    [SerializeField, HideInInspector] private int tuningVersion = 0;

    // Oude scènes hebben nog de oude (makkelijkere) waarden opgeslagen: één keer de nieuwe doorzetten.
    private void ApplyTuning()
    {
        if (tuningVersion >= 3) return;
        guardHp = 60;
        if (tuningVersion < 2) fireballSpeed = 1.9f;
        tuningVersion = 3;
    }
    private void OnValidate() => ApplyTuning();

    [Header("Model (Assets/Models) - leeg = in code opbouwen")]
    [Tooltip("Assets/Models/Guard.prefab")]
    public GameObject guardPrefab;

    public event Action<string> Speech;
    public event Action PlayerHit;
    public event Action<bool> Finished; // true = gewonnen

    public bool Active { get; private set; }
    public int Hearts { get; private set; }
    public int MaxHearts => playerHearts;
    public float GuardHealth01 => maxHp > 0 ? hp / (float)maxHp : 0f;
    // Waarschuwingstekst (leeg = niets).
    public string Warning => Active && !ending && Time.time < warningUntil ? warningText : "";
    // Sta je nu in de gevarenzone?
    public bool InDanger { get; private set; }

    private enum Move { Fireball, Volley, Homing, SwordSweep, SwordCleave, Meteors, Wall, Teleport, Combo }
    private class Projectile { public Transform t; public Vector3 vel; public float life; public bool deflected, homing; }
    private class Wave { public Transform t; public Vector3 dir; public float speed, life; public bool vertical, passed; }

    private Camera cam;
    private SoundFx sfx;
    private GameUI ui;
    private Transform world, guard, guardBody, armR, armL, shield, chargeOrb;
    private Quaternion armRRest = Quaternion.identity, armLRest = Quaternion.identity;
    private readonly List<Wave> waves = new List<Wave>();
    private readonly List<GameObject> telegraphs = new List<GameObject>();
    private Coroutine attackRoutine;
    private Move lastMove = Move.Teleport;
    private float nextAttack, lastHurt = -10f, homeAngle, closeIn, closeTarget, warningUntil;
    private string warningText = "";
    private bool dangerNext;
    private static Mesh slashMesh;
    private readonly List<Projectile> fireballs = new List<Projectile>();
    private readonly List<Projectile> bolts = new List<Projectile>();
    private readonly List<Transform> rocks = new List<Transform>();
    private ParticleSystem sparks, embers;
    private Material shieldMat;
    private Vector3 origin, up, fwd, editorOffset;
    private int hp, maxHp;
    private float shieldUntil, hitFlash, nextShield;
    private bool ending, intro;

    private void Start()
    {
        cam = Camera.main;
        sfx = FindFirstObjectByType<SoundFx>();
        ui = FindFirstObjectByType<GameUI>();
    }

    // ================================================================== Start / einde

    public void Begin(Vector3 worldUp)
    {
        if (cam == null) cam = Camera.main;
        End();
        Active = true;
        ending = false;
        up = worldUp.normalized;
        origin = cam.transform.position;
        fwd = Vector3.ProjectOnPlane(cam.transform.forward, up).normalized;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(cam.transform.up, up).normalized;
        editorOffset = Vector3.zero;
        ApplyTuning();
        Hearts = playerHearts;
        maxHp = hp = guardHp;
        shieldUntil = 0f;
        nextShield = Time.time + 8f;
        homeAngle = 0f; closeIn = closeTarget = 0f;
        lastHurt = -10f; warningUntil = 0f; InDanger = dangerNext = false;
        attackRoutine = null;
        lastMove = Move.Teleport;

        BuildWorld();
        BuildGuard();
        StartCoroutine(Intro());
    }

    public void End()
    {
        StopAllCoroutines();
        Active = false;
        ClearProjectiles();
        rocks.Clear();
        InDanger = false;
        if (world != null) Destroy(world.gameObject);
        world = null;
    }

    private IEnumerator Intro()
    {
        intro = true;
        nextAttack = float.MaxValue;
        Vector3 target = GuardHome(0f);
        guard.position = target - up * 2.2f;
        if (sfx != null) sfx.Play("rumble", 0.8f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.8f)
        {
            guard.position = Vector3.Lerp(target - up * 2.2f, target, Mathf.SmoothStep(0f, 1f, t));
            if (UnityEngine.Random.value < 0.6f) Gfx.Burst(embers, guard.position - up * 0.6f, up * 0.4f, 3, 0.4f);
            yield return null;
        }
        Speech?.Invoke("WIE DURFT HET DJINN-RIJK TE BETREDEN?! Niemand bevrijdt de geest zonder MIJ te verslaan!");
        yield return TapOrWait(2.5f);
        Speech?.Invoke("Tik om magie te schieten. Ontwijk mijn vuur, mijn zwaard en mijn meteoren... als je kan! Stap opzij, BUK, en let op de rode tekens!");
        yield return TapOrWait(2.5f);
        intro = false;
        nextAttack = Time.time + 0.8f;
    }

    // Wacht op tik (of enkele seconden zonder UI).
    private IEnumerator TapOrWait(float seconds)
    {
        if (ui == null) ui = FindFirstObjectByType<GameUI>();
        if (ui != null) yield return ui.WaitForTap();
        else yield return new WaitForSeconds(seconds);
    }

    private Vector3 GuardHome(float t)
    {
        Vector3 f = Quaternion.AngleAxis(homeAngle, up) * fwd;
        Vector3 r = Vector3.Cross(up, f);
        float dist = guardDistance * (1f - 0.45f * closeIn); // bij zwaardaanvallen komt hij dichterbij
        return origin + f * dist + r * Mathf.Sin(t * 0.5f) * 0.8f * (1f - closeIn) + up * (0.15f + Mathf.Sin(t * 1.3f) * 0.08f);
    }

    // Gsm-hoogte t.o.v. start (negatief = gebukt).
    private float PlayerHeight => Vector3.Dot(PlayerPos - origin, up);

    // Spelerpositie.
    private Vector3 PlayerPos => cam.transform.position + editorOffset;

    // Punt op de grond onder p.
    private Vector3 FloorBelow(Vector3 p) => p - up * (Vector3.Dot(p - origin, up) + 1.33f);

    private Vector3 Flat(Vector3 v) => Vector3.ProjectOnPlane(v, up);

    // ================================================================== Wereld

    private void BuildWorld()
    {
        world = new GameObject("DjinnWorld").transform;
        world.position = origin;
        world.rotation = Quaternion.LookRotation(fwd, up);

        // Koepel (binnenkant van een bol, dubbelzijdig, ondoorzichtig) met hemeltextuur
        var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(dome.GetComponent<Collider>());
        dome.name = "Dome";
        dome.transform.SetParent(world, false);
        dome.transform.localScale = Vector3.one * 16f;
        var domeMat = Gfx.NewUnlit(Gfx.Blend.Alpha, SkyTexture(), 0);
        domeMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        domeMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        domeMat.SetFloat("_ZWrite", 1f);
        domeMat.renderQueue = 1990;
        dome.GetComponent<Renderer>().sharedMaterial = domeMat;

        // Zandvlakte en duinen
        float floor = -1.35f;
        Gfx.Prim(PrimitiveType.Cylinder, world, new Vector3(0f, floor - 0.01f, 0f), new Vector3(15f, 0.01f, 15f), new Color(0.75f, 0.55f, 0.35f), 0.05f, "Sand");
        for (int i = 0; i < 9; i++)
        {
            float a = i / 9f * Mathf.PI * 2f + 0.3f;
            Vector3 p = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * UnityEngine.Random.Range(5.2f, 6.5f);
            p.y = floor - 0.3f;
            Gfx.Prim(PrimitiveType.Sphere, world, p, new Vector3(UnityEngine.Random.Range(2.5f, 4f), UnityEngine.Random.Range(0.8f, 1.6f), UnityEngine.Random.Range(1.5f, 2.5f)),
                     new Color(0.8f, 0.55f, 0.32f) * UnityEngine.Random.Range(0.85f, 1.05f), 0.05f, "Dune");
        }

        // Ring van zuilen met gloeiende bollen
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f + Mathf.PI / 8f;
            Vector3 p = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 3.6f;
            Gfx.Prim(PrimitiveType.Cylinder, world, p + new Vector3(0f, floor + 1.1f, 0f), new Vector3(0.28f, 1.1f, 0.28f), new Color(0.85f, 0.75f, 0.6f), 0.05f, "Pillar");
            Gfx.Prim(PrimitiveType.Sphere, world, p + new Vector3(0f, floor + 2.35f, 0f), Vector3.one * 0.25f, new Color(1f, 0.7f, 0.3f), 2f, "Flame");
        }

        // Maan en zwevende rotsen
        Gfx.Prim(PrimitiveType.Sphere, world, new Vector3(-3f, 4.5f, 5.5f), Vector3.one * 1.4f, new Color(1f, 0.95f, 0.8f), 1.5f, "Moon");
        for (int i = 0; i < 7; i++)
        {
            var rock = Gfx.Prim(PrimitiveType.Sphere, world, Vector3.zero,
                                new Vector3(UnityEngine.Random.Range(0.3f, 0.7f), UnityEngine.Random.Range(0.2f, 0.4f), UnityEngine.Random.Range(0.3f, 0.6f)),
                                new Color(0.45f, 0.35f, 0.5f), 0.1f, "Rock").transform;
            rocks.Add(rock);
        }

        sparks = Gfx.Particles("Sparks", world, null, Gfx.Sparkle, Gfx.Blend.Additive, 0.8f, 0.05f, 0.5f, new Color(1f, 0.8f, 0.4f), 0.2f, 1500);
        embers = Gfx.Particles("Embers", world, null, Gfx.SoftDot, Gfx.Blend.Additive, 1.6f, 0.08f, 0.2f, new Color(1f, 0.4f, 0.15f, 0.8f), -0.05f, 1500);
    }

    // Prefab gebruiken of Wachter in code bouwen.
    private void BuildGuard()
    {
        if (guardPrefab != null)
        {
            guard = Instantiate(guardPrefab, world, false).transform;
            guard.name = "Guard";
        }
        else guard = BuildGuardModel(world);
        guardBody = FindChild(guard, "Body");
        armR = FindChild(guard, "ArmR");
        armL = FindChild(guard, "ArmL");
        if (armR != null) armRRest = armR.localRotation;
        if (armL != null) armLRest = armL.localRotation;
        if (guardBody == null || armR == null) Debug.LogWarning("[GuardBattle] Het Wachter-model mist 'Body' of 'ArmR'. Maak het prefab opnieuw aan.");

        // Schild (onzichtbaar tot het opgezet wordt) - niet in het prefab, want het gebruikt een textuur uit code
        shieldMat = Gfx.NewUnlit(Gfx.Blend.Additive, Gfx.Ring, 20);
        shield = Gfx.Quad(guard, new Vector3(0f, 0.1f, 0.5f), Quaternion.identity, Vector2.one * 1.5f, shieldMat, "Shield").transform;
        shield.gameObject.SetActive(false);
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    // Bouwt de Wachter (statisch; 'Body' en 'ArmR' niet hernoemen).
    public static Transform BuildGuardModel(Transform parent)
    {
        Transform guard = Gfx.Group("Guard", parent);
        Transform body = Gfx.Group("Body", guard);

        Color skin = new Color(0.62f, 0.1f, 0.13f), skinDark = new Color(0.18f, 0.02f, 0.12f);
        Color gold = new Color(1f, 0.76f, 0.28f), metal = new Color(0.25f, 0.24f, 0.3f);
        Color ruby = new Color(1f, 0.15f, 0.25f), eyeGlow = new Color(1f, 0.85f, 0.2f);
        Color bone = new Color(0.92f, 0.86f, 0.74f);
        Color white = Color.white;

        // ---------------- Rookstaart (licht S-vormig, kleurverloop van onder naar boven)
        var tailProfile = new[]
        {
            new Vector2(0f, -1.05f), new Vector2(0.025f, -1.0f), new Vector2(0.05f, -0.9f), new Vector2(0.08f, -0.78f),
            new Vector2(0.11f, -0.62f), new Vector2(0.14f, -0.47f), new Vector2(0.17f, -0.33f), new Vector2(0.2f, -0.21f),
            new Vector2(0.22f, -0.13f), new Vector2(0f, -0.1f)
        };
        var tailColors = new Color[tailProfile.Length];
        var tailBend = new float[tailProfile.Length];
        for (int i = 0; i < tailProfile.Length; i++)
        {
            float k = i / (float)(tailProfile.Length - 1);
            tailColors[i] = Color.Lerp(skinDark, skin, k * k);
            tailBend[i] = Mathf.Sin(k * Mathf.PI * 1.3f) * 0.12f * (1f - k);
        }
        Gfx.MeshObj(MeshKit.Lathe(tailProfile, 20, tailColors, tailBend, "GuardTail"), body, Vector3.zero, Quaternion.identity, Vector3.one, white, 0.35f, "Tail", true);
        // vlammetjes rond de heup
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * 360f;
            var rot = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(150f, 0f, 0f);
            Vector3 pos = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, -0.15f, 0.19f);
            Gfx.MeshObj(MeshKit.Cone(0.04f, 0.18f, 10, "Flame"), body, pos, rot, Vector3.one, new Color(1f, 0.45f, 0.1f), 2f, "HipFlame");
        }

        // ---------------- Romp (V-vorm), afgeplat van voor naar achter
        var torso = new[]
        {
            new Vector2(0f, -0.18f), new Vector2(0.2f, -0.17f), new Vector2(0.19f, -0.05f), new Vector2(0.21f, 0.05f),
            new Vector2(0.27f, 0.18f), new Vector2(0.3f, 0.28f), new Vector2(0.26f, 0.36f), new Vector2(0.13f, 0.42f), new Vector2(0f, 0.44f)
        };
        Gfx.MeshObj(MeshKit.Lathe(torso, 24, null, null, "GuardTorso"), body, Vector3.zero, Quaternion.identity, new Vector3(1f, 1f, 0.72f), skin, 0.12f, "Torso");
        // borstspieren
        Gfx.Prim(PrimitiveType.Sphere, body, new Vector3(-0.1f, 0.25f, 0.15f), new Vector3(0.2f, 0.14f, 0.1f), skin, 0.12f, "PecL");
        Gfx.Prim(PrimitiveType.Sphere, body, new Vector3(0.1f, 0.25f, 0.15f), new Vector3(0.2f, 0.14f, 0.1f), skin, 0.12f, "PecR");

        // ---------------- Gouden harnas
        Gfx.Prim(PrimitiveType.Cube, body, new Vector3(0f, 0.12f, 0.17f), new Vector3(0.05f, 0.45f, 0.02f), gold, 0.35f, "StrapL").transform.localRotation = Quaternion.Euler(0f, 0f, 32f);
        Gfx.Prim(PrimitiveType.Cube, body, new Vector3(0f, 0.12f, 0.17f), new Vector3(0.05f, 0.45f, 0.02f), gold, 0.35f, "StrapR").transform.localRotation = Quaternion.Euler(0f, 0f, -32f);
        Gfx.Prim(PrimitiveType.Cylinder, body, new Vector3(0f, 0.12f, 0.185f), new Vector3(0.11f, 0.012f, 0.11f), gold, 0.5f, "Medallion").transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Gfx.Prim(PrimitiveType.Sphere, body, new Vector3(0f, 0.12f, 0.2f), Vector3.one * 0.045f, ruby, 2.2f, "MedallionGem");
        Gfx.MeshObj(MeshKit.Torus(0.21f, 0.035f, 28, 10, "Belt"), body, new Vector3(0f, -0.12f, 0f), Quaternion.identity, new Vector3(1f, 1f, 0.75f), gold, 0.35f, "Belt");
        Gfx.Prim(PrimitiveType.Sphere, body, new Vector3(0f, -0.12f, 0.165f), Vector3.one * 0.05f, ruby, 2.2f, "BeltGem");
        Gfx.MeshObj(MeshKit.Torus(0.12f, 0.028f, 24, 8, "Collar"), body, new Vector3(0f, 0.43f, 0f), Quaternion.identity, Vector3.one, gold, 0.4f, "Collar");

        // ---------------- Schouderplaten met punten
        var dome = MeshKit.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.15f, 0f), new Vector2(0.14f, 0.06f), new Vector2(0.1f, 0.11f), new Vector2(0f, 0.13f) }, 18, null, null, "Pauldron");
        foreach (float side in new[] { -1f, 1f })
        {
            var pad = Gfx.Group(side < 0 ? "PauldronL" : "PauldronR", body);
            pad.localPosition = new Vector3(side * 0.33f, 0.31f, 0f);
            pad.localRotation = Quaternion.Euler(0f, 0f, -side * 28f);
            Gfx.MeshObj(dome, pad, Vector3.zero, Quaternion.identity, Vector3.one, gold, 0.3f, "Plate");
            Gfx.MeshObj(MeshKit.Torus(0.15f, 0.015f, 20, 6, "PadRim"), pad, Vector3.zero, Quaternion.identity, Vector3.one, metal, 0.1f, "Rim");
            for (int k = -1; k <= 1; k++)
                Gfx.MeshObj(MeshKit.Cone(0.022f, 0.1f, 8, "Spike"), pad, new Vector3(k * 0.05f, 0.1f - Mathf.Abs(k) * 0.03f, 0f), Quaternion.Euler(0f, 0f, -k * 20f), Vector3.one, metal, 0.2f, "Spike");
        }

        // ---------------- Hoofd
        var head = Gfx.Group("Head", body);
        head.localPosition = new Vector3(0f, 0.6f, 0f);
        Gfx.Prim(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.25f, 0.29f, 0.25f), skin, 0.12f, "Skull");
        Gfx.Prim(PrimitiveType.Sphere, head, new Vector3(0f, -0.1f, 0.03f), new Vector3(0.19f, 0.13f, 0.19f), skin, 0.12f, "Jaw");
        // boze, schuine ogen + wenkbrauwen
        foreach (float side in new[] { -1f, 1f })
        {
            Gfx.Prim(PrimitiveType.Sphere, head, new Vector3(side * 0.055f, 0.03f, 0.115f), new Vector3(0.07f, 0.024f, 0.02f), eyeGlow, 3.5f, side < 0 ? "EyeL" : "EyeR")
                .transform.localRotation = Quaternion.Euler(0f, 0f, side * 18f);
            Gfx.Prim(PrimitiveType.Cube, head, new Vector3(side * 0.06f, 0.07f, 0.112f), new Vector3(0.09f, 0.02f, 0.035f), skinDark, 0f, "Brow")
                .transform.localRotation = Quaternion.Euler(0f, 0f, side * 24f);
            // puntoren
            Gfx.MeshObj(MeshKit.Cone(0.03f, 0.11f, 8, "Ear"), head, new Vector3(side * 0.12f, 0.02f, -0.01f), Quaternion.Euler(0f, side * 20f, -side * 80f), Vector3.one, skin, 0.12f, "Ear");
            // slagtanden
            Gfx.MeshObj(MeshKit.Cone(0.009f, 0.03f, 6, "Fang"), head, new Vector3(side * 0.024f, -0.058f, 0.125f), Quaternion.Euler(180f, 0f, 0f), Vector3.one, white, 0.3f, "Fang");
            // gebogen hoorns: naar buiten, omhoog en naar achter; bot -> zwart naar de punt
            var path = new[]
            {
                new Vector3(side * 0.08f, 0.09f, 0f), new Vector3(side * 0.16f, 0.17f, -0.03f), new Vector3(side * 0.23f, 0.29f, -0.08f),
                new Vector3(side * 0.245f, 0.41f, -0.15f), new Vector3(side * 0.21f, 0.5f, -0.22f), new Vector3(side * 0.16f, 0.55f, -0.27f)
            };
            var radii = new[] { 0.05f, 0.042f, 0.032f, 0.022f, 0.012f, 0f };
            var hornCols = new[] { bone, bone, Color.Lerp(bone, skinDark, 0.3f), Color.Lerp(bone, skinDark, 0.6f), skinDark, skinDark };
            Gfx.MeshObj(MeshKit.Tube(path, radii, hornCols, 10, "Horn"), head, Vector3.zero, Quaternion.identity, Vector3.one, white, 0.1f, side < 0 ? "HornL" : "HornR", true);
        }
        Gfx.Prim(PrimitiveType.Cube, head, new Vector3(0f, -0.055f, 0.122f), new Vector3(0.08f, 0.012f, 0.02f), new Color(0.08f, 0.01f, 0.03f), 0f, "Mouth");
        // gevlochten baard
        Gfx.MeshObj(MeshKit.Cone(0.065f, 0.3f, 12, "Beard"), head, new Vector3(0f, -0.15f, 0.08f), Quaternion.Euler(160f, 0f, 0f), Vector3.one, new Color(0.1f, 0.02f, 0.07f), 0.05f, "Beard");
        Gfx.MeshObj(MeshKit.Torus(0.032f, 0.011f, 16, 6, "BeardRing"), head, new Vector3(0f, -0.27f, 0.12f), Quaternion.Euler(15f, 0f, 0f), Vector3.one, gold, 0.5f, "BeardRing");
        // kroon met punten, juweel en een vlam als haarknot
        Gfx.MeshObj(MeshKit.Torus(0.105f, 0.018f, 24, 8, "Crown"), head, new Vector3(0f, 0.09f, 0f), Quaternion.identity, Vector3.one, gold, 0.5f, "Crown");
        for (int k = 0; k < 7; k++)
        {
            float a = (k - 3) * 25f;
            Vector3 pos = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.1f, 0.105f);
            Gfx.MeshObj(MeshKit.Cone(0.018f, k == 3 ? 0.1f : 0.065f, 8, "CrownSpike"), head, pos, Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(-12f, 0f, 0f), Vector3.one, gold, 0.5f, "CrownSpike");
        }
        Gfx.Prim(PrimitiveType.Sphere, head, new Vector3(0f, 0.095f, 0.122f), Vector3.one * 0.035f, ruby, 2.5f, "CrownGem");
        Gfx.MeshObj(MeshKit.Cone(0.055f, 0.18f, 12, "TopFlame"), head, new Vector3(0f, 0.13f, -0.01f), Quaternion.identity, Vector3.one, new Color(1f, 0.4f, 0.08f), 2.2f, "TopFlame");
        Gfx.MeshObj(MeshKit.Cone(0.03f, 0.12f, 10, "TopFlameCore"), head, new Vector3(0f, 0.135f, 0f), Quaternion.identity, Vector3.one, new Color(1f, 0.9f, 0.4f), 3f, "TopFlameCore");

        // ---------------- Armen (gespierd, met gouden armbanden)
        var armProfile = new[]
        {
            new Vector2(0f, -0.44f), new Vector2(0.055f, -0.42f), new Vector2(0.06f, -0.32f), new Vector2(0.075f, -0.2f),
            new Vector2(0.088f, -0.09f), new Vector2(0.078f, 0f), new Vector2(0f, 0.03f)
        };
        var armMesh = MeshKit.Lathe(armProfile, 16, null, null, "GuardArm");
        var bracer = MeshKit.Torus(0.064f, 0.02f, 20, 8, "Bracer");
        foreach (float side in new[] { -1f, 1f })
        {
            var arm = Gfx.Group(side < 0 ? "ArmL" : "ArmR", body);
            arm.localPosition = new Vector3(side * 0.37f, 0.27f, 0f);
            arm.localRotation = Quaternion.Euler(0f, 0f, side * 14f);
            Gfx.MeshObj(armMesh, arm, Vector3.zero, Quaternion.identity, Vector3.one, skin, 0.12f, "Arm");
            Gfx.MeshObj(bracer, arm, new Vector3(0f, -0.31f, 0f), Quaternion.identity, Vector3.one, gold, 0.4f, "Bracer");
            Gfx.MeshObj(bracer, arm, new Vector3(0f, -0.37f, 0f), Quaternion.identity, Vector3.one * 0.92f, gold, 0.4f, "Bracer");
            Gfx.Prim(PrimitiveType.Sphere, arm, new Vector3(0f, -0.47f, 0f), new Vector3(0.11f, 0.12f, 0.11f), skin, 0.12f, "Fist");

            if (side < 0)
            {
                // Kromzwaard: greep, gouden pareerstang, gebogen blad naar voren-onder
                var sword = Gfx.Group("Scimitar", arm);
                sword.localPosition = new Vector3(0f, -0.47f, 0f);
                sword.localRotation = Quaternion.Euler(-60f, 0f, 0f);
                Gfx.Prim(PrimitiveType.Cylinder, sword, new Vector3(0f, 0.04f, 0f), new Vector3(0.025f, 0.07f, 0.025f), new Color(0.3f, 0.12f, 0.08f), 0f, "Grip");
                Gfx.Prim(PrimitiveType.Sphere, sword, new Vector3(0f, 0.12f, 0f), Vector3.one * 0.04f, gold, 0.5f, "Pommel");
                Gfx.Prim(PrimitiveType.Cube, sword, new Vector3(0f, -0.04f, 0f), new Vector3(0.14f, 0.02f, 0.03f), gold, 0.5f, "Guard");
                Gfx.MeshObj(MeshKit.Blade(0.62f, 0.055f, 0.16f, 0.005f, "Blade"), sword, new Vector3(0f, -0.05f, 0f), Quaternion.Euler(0f, 90f, 0f), Vector3.one, white, 0.6f, "Blade", true);
            }
        }
        return guard;
    }

    // ================================================================== Elk frame

    private void Update()
    {
        if (!Active || world == null || cam == null) return;
        float t = Time.time;
        InDanger = dangerNext && !ending;
        dangerNext = false;
        closeIn = Mathf.MoveTowards(closeIn, closeTarget, Time.deltaTime * 1.6f);

        foreach (var rock in rocks)
        {
            int i = rocks.IndexOf(rock);
            float a = t * 0.08f + i * 0.9f;
            rock.localPosition = new Vector3(Mathf.Sin(a) * (4.2f + i * 0.2f), 0.4f + i * 0.25f + Mathf.Sin(t + i) * 0.1f, Mathf.Cos(a) * (4.2f + i * 0.2f));
            rock.localRotation = Quaternion.Euler(t * 10f + i * 30f, t * 15f, 0f);
        }
        if (UnityEngine.Random.value < 0.3f)
            Gfx.Burst(embers, origin + UnityEngine.Random.insideUnitSphere * 4f - up * 1f, up * 0.15f, 1, 0.05f, null, 0.4f);

        if (guard != null && !ending)
        {
            if (!intro) guard.position = Vector3.Lerp(guard.position, GuardHome(t), Time.deltaTime * 3f);
            Vector3 look = Vector3.ProjectOnPlane(PlayerPos - guard.position, up);
            if (look.sqrMagnitude > 0.01f) guard.rotation = Quaternion.Slerp(guard.rotation, Quaternion.LookRotation(look, up), Time.deltaTime * 5f);
            // geraakt: even opzij knikken en rood/wit flitsen
            hitFlash = Mathf.MoveTowards(hitFlash, 0f, Time.deltaTime * 4f);
            guardBody.localRotation = Quaternion.Euler(-hitFlash * 18f, 0f, Mathf.Sin(t * 2f) * 3f);

            bool shielded = t < shieldUntil;
            shield.gameObject.SetActive(shielded);
            if (shielded)
            {
                shield.localRotation = Quaternion.Euler(0f, 0f, t * 90f);
                shieldMat.color = new Color(1f, 0.85f, 0.4f, 0.6f + 0.3f * Mathf.Sin(t * 10f));
            }

            if (!intro) Attack(t);
        }

#if UNITY_EDITOR
        EditorMove();
#endif
        UpdateFireballs();
        UpdateWaves();
        UpdateBolts();
        HandleInput();
    }

#if UNITY_EDITOR
    // Editor-besturing: WASD + C.
    private void EditorMove()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        Vector3 f = Flat(cam.transform.forward).sqrMagnitude > 0.01f ? Flat(cam.transform.forward).normalized : fwd;
        Vector3 r = Vector3.Cross(up, f);
        Vector3 flat = Flat(editorOffset);
        if (kb.aKey.isPressed) flat -= r * 2f * Time.deltaTime;
        if (kb.dKey.isPressed) flat += r * 2f * Time.deltaTime;
        if (kb.wKey.isPressed) flat += f * 2f * Time.deltaTime;
        if (kb.sKey.isPressed) flat -= f * 2f * Time.deltaTime;
        flat = Vector3.ClampMagnitude(flat, 2f);
        editorOffset = flat + up * (kb.cKey.isPressed ? -(duckDepth + 0.15f) : 0f);
    }
#endif

    // ================================================================== Aanvallen van de Wachter

    private int Phase => GuardHealth01 > 0.66f ? 1 : GuardHealth01 > 0.33f ? 2 : 3;

    private void Attack(float t)
    {
        // Schild opzetten (vaker in latere fases)
        if (t > nextShield)
        {
            shieldUntil = t + (Phase == 1 ? 1.6f : 2.2f);
            nextShield = t + UnityEngine.Random.Range(Phase == 3 ? 4f : 6f, Phase == 3 ? 6f : 9f);
            if (sfx != null) sfx.Play("sparkle", 0.6f, 0.7f);
            Speech?.Invoke(Phase == 3 ? "Je magie kan me niets doen!" : "Ha! Mijn schild!");
        }

        if (attackRoutine == null && t > nextAttack) attackRoutine = StartCoroutine(RunAttack(PickMove()));
    }

    // Kiest de volgende aanval.
    private Move PickMove()
    {
        var pool = new List<Move> { Move.Fireball, Move.Fireball, Move.SwordSweep, Move.SwordSweep, Move.Meteors };
        if (Phase >= 2) pool.AddRange(new[] { Move.Volley, Move.SwordCleave, Move.SwordCleave, Move.Teleport, Move.Wall, Move.Meteors });
        if (Phase >= 3) pool.AddRange(new[] { Move.Homing, Move.Homing, Move.Combo, Move.Combo, Move.Wall });
        Move m = pool[UnityEngine.Random.Range(0, pool.Count)];
        for (int tries = 0; m == lastMove && tries < 10; tries++) m = pool[UnityEngine.Random.Range(0, pool.Count)];
        lastMove = m;
        return m;
    }

    private float Pace => Mathf.Clamp(aggression, 0.5f, 2f) * (Phase == 3 ? 1.3f : Phase == 2 ? 1.12f : 1f);

    private IEnumerator RunAttack(Move m)
    {
        switch (m)
        {
            case Move.Fireball: yield return Throw(1, false); break;
            case Move.Volley: yield return Throw(3, false); break;
            case Move.Homing: yield return Throw(UnityEngine.Random.value < 0.5f ? 1 : 2, true); break;
            case Move.SwordSweep: yield return SwordAttack(false); break;
            case Move.SwordCleave: yield return SwordAttack(true); break;
            case Move.Combo:
                Speech?.Invoke("Probeer DIT maar eens te ontwijken!");
                yield return SwordAttack(false);
                yield return SwordAttack(true, 0.7f);
                break;
            case Move.Meteors: yield return MeteorRain(Phase == 1 ? 1 : Phase == 2 ? 3 : 5); break;
            case Move.Wall: yield return FireWall(); break;
            case Move.Teleport: yield return TeleportMove(); break;
        }
        ResetArms();
        closeTarget = 0f;
        float rest = (Phase == 1 ? 1.4f : Phase == 2 ? 1.0f : 0.7f) / Mathf.Clamp(aggression, 0.5f, 2f);
        nextAttack = Time.time + rest + UnityEngine.Random.Range(0f, 0.4f);
        attackRoutine = null;
    }

    private void Warn(string text, float duration)
    {
        warningText = text;
        warningUntil = Time.time + duration;
    }

    private void ResetArms()
    {
        if (armR != null) armR.localRotation = armRRest;
        if (armL != null) armL.localRotation = armLRest;
    }

    // ---------------------------------------------------------------- Vuurballen

    // Vuurbal(len) gooien.
    private IEnumerator Throw(int count, bool homing)
    {
        if (homing) Warn("ZOEKENDE VUURBAL: WACHT... EN STAP DAN OPZIJ!", 2.2f);
        chargeOrb = Gfx.Prim(PrimitiveType.Sphere, world, Vector3.zero, Vector3.one * 0.02f, homing ? new Color(0.8f, 0.3f, 1f) : new Color(1f, 0.45f, 0.1f), 3f, "Charge").transform;
        float charge = (count > 1 ? 0.85f : 0.7f) / Pace;
        for (float c = 0f; c < 1f; c += Time.deltaTime / charge)
        {
            if (ending || armR == null || chargeOrb == null) yield break;
            armR.localRotation = armRRest * Quaternion.Euler(-120f * c, 0f, 30f * c);
            chargeOrb.position = armR.TransformPoint(new Vector3(0f, -0.5f, 0f));
            chargeOrb.localScale = Vector3.one * Mathf.Lerp(0.03f, 0.16f, c);
            if (UnityEngine.Random.value < 0.5f) Gfx.Burst(embers, chargeOrb.position, Vector3.zero, 1, 0.1f);
            yield return null;
        }
        Vector3 from = chargeOrb.position;
        Destroy(chargeOrb.gameObject);
        chargeOrb = null;
        armR.localRotation = armRRest * Quaternion.Euler(30f, 0f, 0f);
        if (sfx != null) sfx.Play("whoosh", 0.7f, 0.8f);

        Vector3 target = PlayerPos - up * 0.05f;
        Vector3 side = Vector3.Cross(up, Flat(target - from).normalized);
        float speed = fireballSpeed * (Phase == 3 ? 1.3f : Phase == 2 ? 1.12f : 1f) * (homing ? 0.8f : 1f);
        for (int i = 0; i < count; i++)
        {
            float spread = count == 1 ? 0f : (i - (count - 1) / 2f) * (count == 2 ? 0.6f : 0.45f); // meter opzij bij het doel
            Vector3 dir = (target + side * spread - from).normalized;
            SpawnFireball(from, dir * speed, homing);
        }
        if (Phase == 3 && !homing && UnityEngine.Random.value < 0.35f)
        {
            // trage ring van vuurbollen rondom je
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * Mathf.PI * 2f;
                Vector3 aim = target + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * 0.55f;
                SpawnFireball(from, (aim - from).normalized * speed * 0.7f, false);
            }
        }
        yield return new WaitForSeconds(homing ? 1.2f : 0.3f);
    }

    // ---------------------------------------------------------------- Zwaard

    // Zwaardaanval: zwaai = bukken, klief = opzij.
    private IEnumerator SwordAttack(bool vertical, float teleMul = 1f)
    {
        closeTarget = 1f;
        float tele = (vertical ? 1.0f : 1.1f) * teleMul / Pace;
        Warn(vertical ? "STAP OPZIJ!" : "BUK!", tele + 0.7f);
        if (sfx != null) sfx.Play("whistle", 0.45f, vertical ? 0.8f : 0.6f);

        // Bij de klief ligt de lijn vast op de plek waar je NU staat
        Vector3 lockDir = Flat(PlayerPos - guard.position).normalized;
        Vector3 lockFrom = guard.position;
        GameObject line = null;
        Material lineMat = null;
        if (vertical)
        {
            lineMat = Gfx.NewUnlit(Gfx.Blend.Additive, Gfx.SoftDot, 6);
            Vector3 a = FloorBelow(lockFrom), b = FloorBelow(PlayerPos) + lockDir * 1.5f;
            line = Gfx.Quad(world, Vector3.zero, Quaternion.identity, new Vector2(0.55f, Vector3.Distance(a, b)), lineMat, "CleaveLine");
            line.transform.position = (a + b) * 0.5f + up * 0.02f;
            line.transform.rotation = Quaternion.LookRotation(-up, lockDir); // plat op de grond, lang in de slagrichting
            telegraphs.Add(line);
        }

        Transform tip = armL != null ? FindChild(armL, "Blade") : null;
        for (float c = 0f; c < 1f; c += Time.deltaTime / tele)
        {
            if (ending || guard == null) yield break;
            if (armL != null)
                armL.localRotation = armLRest * (vertical ? Quaternion.Euler(-165f * Mathf.SmoothStep(0f, 1f, c), 0f, 0f)
                                                          : Quaternion.Euler(-25f * c, 0f, -100f * Mathf.SmoothStep(0f, 1f, c)));
            if (tip != null && UnityEngine.Random.value < 0.7f)
                Gfx.Burst(sparks, tip.position, Vector3.zero, 1, 0.15f, vertical ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.75f, 0.3f));
            if (lineMat != null) lineMat.color = new Color(1f, 0.15f, 0.1f, 0.35f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 12f)));

            float lateral = Vector3.Dot(PlayerPos - lockFrom, Vector3.Cross(up, lockDir));
            if (vertical ? Mathf.Abs(lateral) < 0.32f : PlayerHeight > -duckDepth) dangerNext = true;
            yield return null;
        }

        // Slaan!
        if (armL != null) armL.localRotation = armLRest * (vertical ? Quaternion.Euler(35f, 0f, 0f) : Quaternion.Euler(-80f, 0f, 60f));
        if (sfx != null) sfx.Play("whoosh", 1f, vertical ? 0.55f : 0.45f);
        Vector3 dir = vertical ? lockDir : Flat(PlayerPos - guard.position).normalized;
        Vector3 start = vertical ? lockFrom : guard.position;
        start += up * (Vector3.Dot(origin - start, up) + (vertical ? -0.15f : -0.05f)) + dir * 0.4f;
        SpawnWave(start, dir, vertical);
        if (lineMat != null) lineMat.color = new Color(1f, 0.15f, 0.1f, 0.8f);
        if (line != null) { Destroy(line, 1.2f); telegraphs.Remove(line); }
        yield return new WaitForSeconds(0.55f);
        if (armL != null) armL.localRotation = armLRest;
    }

    private void SpawnWave(Vector3 pos, Vector3 dir, bool vertical)
    {
        if (slashMesh == null)
        {
            // Sikkel: gebogen buis, dik in het midden en puntig aan de uiteinden; wit-heet in het midden
            int n = 14;
            var path = new Vector3[n];
            var radii = new float[n];
            var cols = new Color[n];
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1), x = Mathf.Lerp(-1.6f, 1.6f, k);
                path[i] = new Vector3(x, 0f, 0.35f * (1f - (x / 1.6f) * (x / 1.6f)));
                radii[i] = i == 0 || i == n - 1 ? 0f : 0.075f * Mathf.Sin(Mathf.PI * k) + 0.008f;
                cols[i] = Color.Lerp(new Color(1f, 0.35f, 0.1f), new Color(1f, 0.95f, 0.75f), Mathf.Sin(Mathf.PI * k));
            }
            slashMesh = MeshKit.Tube(path, radii, cols, 8, "Slash");
        }
        var root = Gfx.Group(vertical ? "Cleave" : "Sweep", world);
        root.position = pos;
        root.rotation = Quaternion.LookRotation(dir, up);
        Gfx.MeshObj(slashMesh, root, Vector3.zero, vertical ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity,
                    vertical ? new Vector3(0.8f, 1f, 1f) : Vector3.one, Color.white, 3f, "Slash", true);
        waves.Add(new Wave { t = root, dir = dir, speed = (vertical ? 3.6f : 3.2f) * Mathf.Sqrt(Pace), life = 3f, vertical = vertical });
    }

    private void UpdateWaves()
    {
        Vector3 camPos = PlayerPos;
        for (int i = waves.Count - 1; i >= 0; i--)
        {
            var w = waves[i];
            w.t.position += w.dir * w.speed * Time.deltaTime;
            w.life -= Time.deltaTime;
            Vector3 side = Vector3.Cross(up, w.dir);
            if (UnityEngine.Random.value < 0.9f)
            {
                float u = UnityEngine.Random.Range(-1.4f, 1.4f);
                Vector3 p = w.t.position + (w.vertical ? up * u * 0.8f : side * u);
                Gfx.Burst(embers, p, -w.dir * 0.3f, 1, 0.05f);
            }

            Vector3 rel = camPos - w.t.position;
            float lateral = Vector3.Dot(rel, side);
            if (!w.passed)
            {
                bool wouldHit = w.vertical ? Mathf.Abs(lateral) < 0.32f : (Mathf.Abs(lateral) < 1.5f && PlayerHeight > -duckDepth);
                if (wouldHit) dangerNext = true;
                if (Vector3.Dot(rel, w.dir) < 0.05f)
                {
                    w.passed = true;
                    if (wouldHit) HurtPlayer(camPos + w.dir * 0.25f);
                    else if (sfx != null) sfx.Play("pling", 0.3f, 1.3f); // netjes ontweken
                }
            }
            if (w.life <= 0f)
            {
                Destroy(w.t.gameObject);
                waves.RemoveAt(i);
            }
        }
    }

    // ---------------------------------------------------------------- Meteoren

    // Meteoren op rode cirkels.
    private IEnumerator MeteorRain(int count)
    {
        float tele = (count > 1 ? 1.5f : 1.3f) / Pace;
        Warn(count > 1 ? "METEOREN! STAP UIT DE RODE CIRKELS!" : "STAP UIT DE RODE CIRKEL!", tele + 0.4f);
        Speech?.Invoke(count > 3 ? "Laat de hemel BRANDEN!" : "Kijk omhoog, sterveling!");
        if (sfx != null) sfx.Play("rumble", 0.6f, 1.3f);

        const float radius = 0.5f;
        var spots = new List<Vector3>();
        Vector3 me = FloorBelow(PlayerPos);
        Vector3 f = Flat(cam.transform.forward).sqrMagnitude > 0.01f ? Flat(cam.transform.forward).normalized : fwd;
        Vector3 r = Vector3.Cross(up, f);
        spots.Add(me);
        for (int k = 1; k < count; k++)
        {
            // rond je heen, maar niet allemaal op dezelfde plek (er blijft altijd een veilige uitweg)
            float a = (k - 1) / (float)Mathf.Max(1, count - 1) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
            float d = UnityEngine.Random.Range(1.0f, 1.6f);
            spots.Add(me + (f * Mathf.Cos(a) + r * Mathf.Sin(a)) * d);
        }

        var rings = new List<Material>();
        var fills = new List<Transform>();
        foreach (var p in spots)
        {
            var ringMat = Gfx.NewUnlit(Gfx.Blend.Additive, Gfx.Ring, 6);
            var ring = Gfx.Quad(world, Vector3.zero, Quaternion.identity, Vector2.one * radius * 2.2f, ringMat, "MeteorRing");
            ring.transform.position = p + up * 0.02f;
            ring.transform.rotation = Quaternion.LookRotation(-up, f);
            telegraphs.Add(ring);
            rings.Add(ringMat);
            var fillMat = Gfx.NewUnlit(Gfx.Blend.Additive, Gfx.SoftDot, 6);
            fillMat.color = new Color(1f, 0.2f, 0.1f, 0.6f);
            var fill = Gfx.Quad(world, Vector3.zero, Quaternion.identity, Vector2.one * 0.05f, fillMat, "MeteorFill");
            fill.transform.position = p + up * 0.03f;
            fill.transform.rotation = ring.transform.rotation;
            telegraphs.Add(fill);
            fills.Add(fill.transform);
            // lichtzuil, zodat je de plek ook ziet als je niet naar de grond kijkt
            var beamMat = Gfx.NewUnlit(Gfx.Blend.Additive, Gfx.SoftDot, 5);
            beamMat.color = new Color(1f, 0.25f, 0.1f, 0.35f);
            for (int q = 0; q < 2; q++)
            {
                var beam = Gfx.Quad(world, Vector3.zero, Quaternion.identity, new Vector2(0.35f, 5f), beamMat, "MeteorBeam");
                beam.transform.position = p + up * 2.5f;
                beam.transform.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(q * 90f, up) * f, up);
                telegraphs.Add(beam);
            }
        }

        // armen omhoog, cirkels vullen zich (aftellen)
        for (float c = 0f; c < 1f; c += Time.deltaTime / tele)
        {
            if (ending) yield break;
            if (armR != null) armR.localRotation = armRRest * Quaternion.Euler(-170f * Mathf.Min(1f, c * 2f), 0f, 0f);
            if (armL != null) armL.localRotation = armLRest * Quaternion.Euler(-170f * Mathf.Min(1f, c * 2f), 0f, 0f);
            for (int k = 0; k < rings.Count; k++)
            {
                rings[k].color = new Color(1f, 0.2f, 0.1f, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * (6f + 10f * c))));
                fills[k].localScale = new Vector3(radius * 2.2f * c, radius * 2.2f * c, 1f);
            }
            if (InSpot(spots, radius)) dangerNext = true;
            yield return null;
        }

        // inslag: meteoren vallen razendsnel
        var rocksDown = new List<Transform>();
        foreach (var p in spots)
        {
            var m = Gfx.Prim(PrimitiveType.Sphere, world, Vector3.zero, Vector3.one * 0.32f, new Color(1f, 0.4f, 0.1f), 3f, "Meteor").transform;
            m.position = p + up * 5f;
            rocksDown.Add(m);
        }
        if (sfx != null) sfx.Play("whistle", 0.6f, 0.5f);
        for (float c = 0f; c < 1f; c += Time.deltaTime / 0.35f)
        {
            if (ending) break;
            for (int k = 0; k < rocksDown.Count; k++)
            {
                rocksDown[k].position = spots[k] + up * Mathf.Lerp(5f, 0.1f, c * c);
                Gfx.Burst(embers, rocksDown[k].position, up * 0.5f, 2, 0.1f);
            }
            if (InSpot(spots, radius)) dangerNext = true;
            yield return null;
        }
        foreach (var m in rocksDown) if (m != null) Destroy(m.gameObject);
        foreach (var p in spots) Gfx.Burst(sparks, p + up * 0.1f, up * 0.8f, 30, 0.8f, new Color(1f, 0.5f, 0.15f));
        if (sfx != null) sfx.Play("bang", 0.7f, 0.8f);
        if (!ending && InSpot(spots, radius)) HurtPlayer(PlayerPos - up * 0.3f);
        ClearTelegraphs();
        yield return new WaitForSeconds(0.3f);
    }

    private bool InSpot(List<Vector3> spots, float radius)
    {
        Vector3 me = FloorBelow(PlayerPos);
        foreach (var p in spots) if (Flat(me - p).magnitude < radius) return true;
        return false;
    }

    // ---------------------------------------------------------------- Vuurmuur

    // Vuurmuur met één opening.
    private IEnumerator FireWall()
    {
        Warn("VUURMUUR! ZOEK DE OPENING!", 2.5f);
        Speech?.Invoke("Er is geen ontsnappen aan!");
        float charge = 0.8f / Pace;
        for (float c = 0f; c < 1f; c += Time.deltaTime / charge)
        {
            if (ending) yield break;
            if (armR != null) armR.localRotation = armRRest * Quaternion.Euler(-90f * c, 0f, 60f * c);
            if (armL != null) armL.localRotation = armLRest * Quaternion.Euler(-90f * c, 0f, -60f * c);
            yield return null;
        }
        if (sfx != null) sfx.Play("whoosh", 0.9f, 0.5f);

        Vector3 camPos = PlayerPos;
        Vector3 g = guard.position - up * Vector3.Dot(guard.position - origin, up); // guard op ooghoogte-vlak
        Vector3 dir = Flat(camPos - g).normalized;
        Vector3 side = Vector3.Cross(up, dir);
        const float spacing = 0.38f;
        int gapCol = (UnityEngine.Random.value < 0.5f ? -1 : 1) * UnityEngine.Random.Range(2, 4); // 2 of 3 kolommen naast je
        float[] rows = { -0.8f, -0.45f, -0.1f, 0.25f };
        float speed = 1.15f * Mathf.Sqrt(Pace);
        Vector3 start = g + dir * 0.5f + side * Vector3.Dot(camPos - g, side);
        for (int col = -4; col <= 4; col++)
        {
            if (col == gapCol) continue;
            foreach (float h in rows)
                SpawnFireball(start + side * (col * spacing) + up * h, dir * speed, false);
        }

        // Wachten tot de muur voorbij is (en ondertussen tonen of je goed staat)
        float travel = Vector3.Dot(camPos - start, dir) / speed + 0.3f;
        for (float c = 0f; c < travel; c += Time.deltaTime)
        {
            if (ending) yield break;
            float lat = Vector3.Dot(PlayerPos - start, side);
            if (Mathf.Abs(lat - gapCol * spacing) > 0.16f) dangerNext = true;
            yield return null;
        }
    }

    // ---------------------------------------------------------------- Teleport

    // Teleport naast de speler.
    private IEnumerator TeleportMove()
    {
        Gfx.Burst(sparks, guard.position, Vector3.zero, 60, 0.8f, new Color(0.8f, 0.3f, 1f));
        if (sfx != null) sfx.Play("poof", 0.8f, 0.8f);
        guard.gameObject.SetActive(false);
        yield return new WaitForSeconds(0.45f / Pace);
        if (ending) yield break;

        float side = homeAngle > 10f ? -1f : homeAngle < -10f ? 1f : (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        homeAngle = Mathf.Clamp(homeAngle + side * UnityEngine.Random.Range(55f, 80f), -80f, 80f);
        guard.position = GuardHome(Time.time);
        Vector3 look = Flat(PlayerPos - guard.position);
        if (look.sqrMagnitude > 0.01f) guard.rotation = Quaternion.LookRotation(look, up);
        guard.gameObject.SetActive(true);
        Gfx.Burst(sparks, guard.position, Vector3.zero, 60, 0.8f, new Color(0.8f, 0.3f, 1f));
        if (sfx != null) sfx.Play("poof", 0.8f, 1.1f);
        Warn(side > 0 ? "HIJ IS RECHTS VAN JE!" : "HIJ IS LINKS VAN JE!", 1.4f); // positieve hoek rond "up" = naar rechts (Unity)
        Speech?.Invoke("Hier ben ik!");
        yield return new WaitForSeconds(0.35f);
        yield return Throw(Phase == 3 ? 3 : 1, false);
    }

    private void SpawnFireball(Vector3 from, Vector3 vel, bool homing)
    {
        var fb = Gfx.Prim(PrimitiveType.Sphere, world, Vector3.zero, Vector3.one * 0.14f, homing ? new Color(0.8f, 0.3f, 1f) : new Color(1f, 0.45f, 0.1f), 3f, "Fireball").transform;
        fb.position = from;
        fireballs.Add(new Projectile { t = fb, vel = vel, life = homing ? 7f : 5f, homing = homing });
    }

    // Speler geraakt.
    private void HurtPlayer(Vector3 at)
    {
        if (ending || Time.time - lastHurt < 0.8f) return;
        lastHurt = Time.time;
        Hearts--;
        PlayerHit?.Invoke();
        if (sfx != null) sfx.Play("bang", 0.6f, 1.4f);
#if UNITY_ANDROID && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
        Gfx.Burst(sparks, at, Vector3.zero, 30, 0.6f, new Color(1f, 0.5f, 0.2f));
        if (Hearts <= 0) StartCoroutine(Lose());
    }

    private void UpdateFireballs()
    {
        Vector3 camPos = PlayerPos;
        for (int i = fireballs.Count - 1; i >= 0; i--)
        {
            var f = fireballs[i];
            if (f.homing)
            {
                // buigt naar je toe, maar steeds minder naarmate hij dichterbij komt: laat opzij stappen werkt
                Vector3 to = camPos - f.t.position;
                float dist = to.magnitude;
                if (dist > 0.9f && Vector3.Dot(to, f.vel) > 0f)
                    f.vel = Vector3.RotateTowards(f.vel, to.normalized * f.vel.magnitude, 1.1f * Time.deltaTime, 0f);
            }
            f.t.position += f.vel * Time.deltaTime;
            f.t.localScale = Vector3.one * (0.14f + 0.02f * Mathf.Sin(Time.time * 30f + i));
            f.life -= Time.deltaTime;
            if (UnityEngine.Random.value < 0.8f) Gfx.Burst(embers, f.t.position, -f.vel * 0.1f, 1, 0.05f);

            bool hit = Vector3.Distance(f.t.position, camPos) < 0.22f;
            bool gone = f.life <= 0f || Vector3.Dot(f.t.position - camPos, f.vel) > 1.2f; // ver voorbij de speler
            if (hit) HurtPlayer(f.t.position);
            else if (!gone && Vector3.Distance(f.t.position, camPos) < 0.9f && Vector3.Dot(camPos - f.t.position, f.vel) > 0f)
            {
                // komt hij op je af? (afstand tot de baan)
                Vector3 toCam = camPos - f.t.position;
                if (Vector3.ProjectOnPlane(toCam, f.vel.normalized).magnitude < 0.25f) dangerNext = true;
            }
            if (hit || gone)
            {
                Destroy(f.t.gameObject);
                fireballs.RemoveAt(i);
            }
        }
    }

    // ================================================================== Speler schiet

    private void HandleInput()
    {
        if (ending || intro) return;
        Vector2? tap = null;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            tap = Touchscreen.current.primaryTouch.position.ReadValue();
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            tap = Mouse.current.position.ReadValue();
        if (!tap.HasValue || bolts.Count > 6) return;

        Ray ray = cam.ScreenPointToRay(tap.Value);
        Vector3 start = cam.transform.position - cam.transform.up * 0.08f + cam.transform.forward * 0.15f;
        Vector3 aimPoint = ray.origin + ray.direction * Vector3.Distance(PlayerPos, guard.position);
        var bolt = Gfx.Prim(PrimitiveType.Sphere, world, Vector3.zero, Vector3.one * 0.07f, new Color(0.4f, 0.9f, 1f), 3f, "Bolt").transform;
        bolt.position = start;
        bolts.Add(new Projectile { t = bolt, vel = (aimPoint - start).normalized * boltSpeed, life = 1.5f });
        if (sfx != null) sfx.Play("pling", 0.35f, 1.8f);
    }

    private void UpdateBolts()
    {
        for (int i = bolts.Count - 1; i >= 0; i--)
        {
            var b = bolts[i];
            b.t.position += b.vel * Time.deltaTime;
            b.life -= Time.deltaTime;
            Gfx.Burst(sparks, b.t.position, Vector3.zero, 1, 0.02f, new Color(0.5f, 0.9f, 1f), 0.6f);

            bool remove = b.life <= 0f;
            if (!b.deflected && guard != null && guard.gameObject.activeSelf && !ending && Vector3.Distance(b.t.position, guard.position + up * 0.1f) < 0.5f)
            {
                if (Time.time < shieldUntil)
                {
                    // afketsen op het schild
                    b.deflected = true;
                    b.vel = Vector3.Reflect(b.vel, (b.t.position - guard.position).normalized) * 0.6f;
                    if (sfx != null) sfx.Play("click", 0.6f, 1.5f);
                    Gfx.Burst(sparks, b.t.position, Vector3.zero, 15, 0.5f, new Color(1f, 0.85f, 0.4f));
                }
                else
                {
                    hp--;
                    hitFlash = 1f;
                    remove = true;
                    if (sfx != null) sfx.Play("bump", 0.8f, 0.8f);
                    Gfx.Burst(sparks, b.t.position, Vector3.zero, 25, 0.7f, new Color(0.6f, 0.95f, 1f));
                    if (hp == Mathf.CeilToInt(maxHp * 0.66f)) Speech?.Invoke("Grrr... Je bent sterker dan je eruitziet!");
                    else if (hp == Mathf.CeilToInt(maxHp * 0.33f)) Speech?.Invoke("GENOEG! Nu wordt het menens!");
                    if (hp <= 0) StartCoroutine(Win());
                }
            }
            if (remove)
            {
                Destroy(b.t.gameObject);
                bolts.RemoveAt(i);
            }
        }
    }

    // ================================================================== Einde

    private IEnumerator Win()
    {
        ending = true;
        StopAttack();
        ClearProjectiles();
        Speech?.Invoke("NEEEE... Hoe kan een sterveling... mij...");
        if (sfx != null) sfx.Play("rumble", 1f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.6f)
        {
            guard.position += UnityEngine.Random.insideUnitSphere * 0.02f;
            if (UnityEngine.Random.value < 0.6f) Gfx.Burst(sparks, guard.position + UnityEngine.Random.insideUnitSphere * 0.5f, Vector3.zero, 6, 0.4f, new Color(1f, 0.8f, 0.4f));
            yield return null;
        }
        if (sfx != null) { sfx.Play("bang", 1f, 0.7f); sfx.Play("fanfare", 0.8f); }
        for (int k = 0; k < 200; k++)
            Gfx.Burst(sparks, guard.position, UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(0.5f, 2.5f), 1, 0f, Color.Lerp(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.3f, 0.2f), UnityEngine.Random.value), 1.5f);
        Destroy(guard.gameObject);
        guard = null;
        yield return new WaitForSeconds(1.5f);
        Finished?.Invoke(true);
    }

    private IEnumerator Lose()
    {
        ending = true;
        StopAttack();
        ClearProjectiles();
        Speech?.Invoke("HAHAHA! Terug naar je eigen wereld, sterveling!");
        if (sfx != null) sfx.Play("rumble", 0.9f);
        yield return new WaitForSeconds(2.5f);
        Finished?.Invoke(false);
    }

    private void StopAttack()
    {
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = null;
        ResetArms();
        closeTarget = 0f;
        if (guard != null) guard.gameObject.SetActive(true);
    }

    private void ClearProjectiles()
    {
        foreach (var f in fireballs) if (f.t != null) Destroy(f.t.gameObject);
        foreach (var b in bolts) if (b.t != null) Destroy(b.t.gameObject);
        foreach (var w in waves) if (w.t != null) Destroy(w.t.gameObject);
        fireballs.Clear();
        bolts.Clear();
        waves.Clear();
        ClearTelegraphs();
        if (chargeOrb != null) Destroy(chargeOrb.gameObject);
        chargeOrb = null;
    }

    private void ClearTelegraphs()
    {
        foreach (var g in telegraphs) if (g != null) Destroy(g);
        telegraphs.Clear();
    }

    // ================================================================== Hemel

    // Hemeltextuur.
    private static Texture2D SkyTexture()
    {
        int w = 512, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, name = "DjinnSky" };
        var px = new Color32[w * h];
        var rnd = new System.Random(9);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = y / (float)h; // 0 = onderaan, 1 = bovenaan
                Color c;
                if (v < 0.5f) c = Color.Lerp(new Color(0.35f, 0.15f, 0.2f), new Color(1f, 0.55f, 0.3f), v / 0.5f);
                else c = Color.Lerp(new Color(0.75f, 0.35f, 0.45f), new Color(0.04f, 0.03f, 0.15f), Mathf.Pow((v - 0.5f) / 0.5f, 0.6f));
                // nevel
                float neb = Mathf.PerlinNoise(x * 0.012f, y * 0.03f) * Mathf.PerlinNoise(x * 0.03f + 5f, y * 0.02f);
                if (v > 0.55f) c += new Color(0.3f, 0.1f, 0.4f) * neb * 0.6f;
                c.a = 1f;
                px[y * w + x] = c;
            }
        for (int i = 0; i < 500; i++)
        {
            int x = rnd.Next(w), y = rnd.Next(h / 2 + 10, h);
            byte b = (byte)rnd.Next(170, 255);
            px[y * w + x] = new Color32(b, b, 255, 255);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
