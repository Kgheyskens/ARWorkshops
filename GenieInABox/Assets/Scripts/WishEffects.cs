using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Wenseffecten: vuurwerk, makeover, ...
public class WishEffects : MonoBehaviour
{
    public static readonly string[] Makeovers =
        { "Kartonnen doos", "Maanvlokken", "Fruitringen", "Schatkist", "Goud", "Sterrenhemel", "Lava" };
    public const string Legendary = "Djinn-paleis";

    // Vrijgespeeld na de Wachter.
    public static bool LegendaryUnlocked;

    // Beschikbare wensen.
    public string[] AvailableMakeovers
    {
        get
        {
            if (!LegendaryUnlocked) return Makeovers;
            var list = new List<string>(Makeovers) { Legendary };
            return list.ToArray();
        }
    }

    private GenieBoxRig rig;
    private SoundFx sfx;
    private Camera cam;
    private ParticleSystem stars, willow, glitter, rocketTrail;
    private Transform shell;
    private Material flashMat;
    private readonly List<Material> lavaMats = new List<Material>();

    public string CurrentSkin { get; private set; } = "";

    [Header("Model (Assets/Models) - leeg = in code opbouwen")]
    [Tooltip("Assets/Models/MagicCarpet.prefab")]
    public GameObject carpetPrefab;

    private MagicCarpet carpet;
    private Transform crystalBall;
    private readonly List<int> usedFortunes = new List<int>();

    // Voorspellingen van de waarzegger.
    public static readonly string[] Fortunes =
    {
        "Ik zie... een 20 op 20. Voor een examen waarvan je dacht dat het slecht ging.",
        "Binnenkort vind je geld terug in een jas die je al maanden niet meer gedragen hebt.",
        "Je volgende code compileert in één keer. Zonder één enkele error. Ja, echt.",
        "Iemand gaat je deze week een compliment geven over iets waar je hard aan gewerkt hebt.",
        "Binnen zeven dagen lach je zo hard dat er melk uit je neus komt. Gebruik dus een rietje.",
        "Ik zie een groot avontuur... en snacks. Vooral heel veel snacks.",
        "Een docent gaat versteld staan van wat jij gemaakt hebt. Hihi, ik weet wel waarover.",
        "Je vindt eindelijk die tweede sok terug. Hij zat de hele tijd in je hoeslaken.",
        "Ik zie dat je batterij nooit leeg gaat op het moment dat je hem echt nodig hebt.",
        "Binnenkort krijg je het beste idee van je leven. Om drie uur 's nachts. Schrijf het meteen op!",
        "Je wifi zal nooit meer haperen tijdens een belangrijk moment. Nou ja... bijna nooit.",
        "Er komt een dag dat je kast precies genoeg cornflakes heeft. Niet te veel, niet te weinig. Perfect.",
        "Ik zie een feest. Jij danst. Iedereen kijkt. En het is... verrassend goed!",
        "Morgen smaakt je ontbijt beter dan ooit. Geloof me, ik woon in een ontbijtdoos.",
        "Ik zie... een geest die uit een doos wordt bevrijd. Oh wacht, dat was vandaag al.",
        "Volgende week wint jouw team iets. Misschien geen prijs, maar zeker een discussie.",
    };

    // Kiest een niet-recente voorspelling.
    public string NextFortune()
    {
        if (usedFortunes.Count >= Fortunes.Length) usedFortunes.Clear();
        int i;
        do i = Random.Range(0, Fortunes.Length); while (usedFortunes.Contains(i));
        usedFortunes.Add(i);
        return Fortunes[i];
    }

    // ------------------------------------------------------------------ Wens: vliegend tapijt

    // Vliegend tapijt rond de kamer.
    public IEnumerator CarpetWish(Genie genie)
    {
        ClearCarpet();
        carpet = MagicCarpet.Spawn(carpetPrefab, genie);
        yield return carpet.Fly(rig.transform, rig.transform.up, sfx);
    }

    // Tapijt verdwijnt na de landing.
    public IEnumerator CarpetVanish()
    {
        if (carpet != null) yield return carpet.Vanish(sfx);
        carpet = null;
    }

    public void ClearCarpet()
    {
        if (carpet != null) carpet.Remove();
        carpet = null;
    }

    // ------------------------------------------------------------------ Wens: waarzegger

    // Kristallen bol voor 'seconds'.
    public IEnumerator CrystalBall(float seconds)
    {
        ClearCrystalBall();
        if (cam == null) cam = Camera.main;
        // naast de geest, een beetje naar de speler toe
        Vector3 c = cam != null ? rig.CameraLocal(cam) : Vector3.back;
        Vector3 toCam = new Vector3(c.x, 0f, c.z).normalized;
        if (toCam.sqrMagnitude < 0.01f) toCam = Vector3.back;
        Vector3 side = Vector3.Cross(Vector3.up, toCam);
        Vector3 basePos = new Vector3(0f, rig.TopY, 0f) + toCam * 0.05f + side * 0.075f;

        crystalBall = Gfx.Group("CrystalBall", rig.transform);
        crystalBall.localPosition = basePos;
        // voetje
        var foot = new[] { new Vector2(0f, 0f), new Vector2(0.026f, 0f), new Vector2(0.024f, 0.006f), new Vector2(0.012f, 0.012f),
                           new Vector2(0.01f, 0.02f), new Vector2(0.022f, 0.026f), new Vector2(0f, 0.028f) };
        Gfx.MeshObj(MeshKit.Lathe(foot, 24, null, null, "BallStand"), crystalBall, Vector3.zero, Quaternion.identity, Vector3.one, new Color(1f, 0.78f, 0.3f), 0.5f, "Stand");
        Gfx.MeshObj(MeshKit.Torus(0.02f, 0.004f, 24, 8, "BallRing"), crystalBall, new Vector3(0f, 0.027f, 0f), Quaternion.identity, Vector3.one, new Color(1f, 0.85f, 0.4f), 0.8f, "Ring");
        // de bol zelf: donkere glazen kern + gloed + glazen rand (gloed en rand draaien mee met de camera)
        Transform orb = Gfx.Group("Orb", crystalBall);
        orb.localPosition = new Vector3(0f, 0.056f, 0f);
        Gfx.Prim(PrimitiveType.Sphere, orb, Vector3.zero, Vector3.one * 0.056f, new Color(0.3f, 0.18f, 0.7f), 1.2f, "Glass");
        var glowMat = Gfx.NewUnlit(Gfx.Blend.Additive, Gfx.SoftDot, 12);
        glowMat.color = new Color(0.6f, 0.4f, 1f, 0.9f);
        var glow = Gfx.Quad(orb, Vector3.zero, Quaternion.identity, Vector2.one * 0.11f, glowMat, "Glow");
        glow.AddComponent<Billboard>();
        var rimMat = Gfx.NewUnlit(Gfx.Blend.Additive, Gfx.Ring, 13);
        rimMat.color = new Color(0.75f, 0.95f, 1f, 0.8f);
        var rim = Gfx.Quad(orb, Vector3.zero, Quaternion.identity, Vector2.one * 0.066f, rimMat, "Rim");
        rim.AddComponent<Billboard>();
        // twee nevelvlekjes die binnenin rondkolken
        var mistMats = new[] { Gfx.NewUnlit(Gfx.Blend.Additive, Gfx.SoftDot, 14), Gfx.NewUnlit(Gfx.Blend.Additive, Gfx.SoftDot, 14) };
        mistMats[0].color = new Color(1f, 0.4f, 0.85f, 0.9f);
        mistMats[1].color = new Color(0.35f, 0.9f, 1f, 0.9f);
        var mists = new Transform[2];
        for (int i = 0; i < 2; i++) mists[i] = Gfx.Quad(orb, Vector3.zero, Quaternion.identity, Vector2.one * 0.03f, mistMats[i], "Mist").transform;

        if (sfx != null) { sfx.Play("sparkle", 0.9f, 0.6f); sfx.Play("poof", 0.6f, 1.3f); }
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float appear = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((seconds - t) / 0.6f);
            crystalBall.localScale = Vector3.one * (1f - Mathf.Pow(1f - appear, 3f));
            float pulse = 0.75f + 0.25f * Mathf.Sin(t * 3f);
            glowMat.color = new Color(0.6f, 0.4f, 1f, 0.9f * pulse);
            // nevel kolkt in het vlak van de camera
            Vector3 r = cam != null ? cam.transform.right : Vector3.right, u = cam != null ? cam.transform.up : Vector3.up;
            for (int i = 0; i < 2; i++)
            {
                float a = t * (2.2f + i * 0.9f) + i * Mathf.PI;
                mists[i].position = orb.position + (r * Mathf.Cos(a) + u * Mathf.Sin(a * 1.3f)) * 0.011f * crystalBall.localScale.x;
                if (cam != null) mists[i].rotation = Quaternion.LookRotation(mists[i].position - cam.transform.position, u);
            }
            if (Random.value < 0.35f)
                Gfx.Burst(glitter, orb.position + Random.insideUnitSphere * 0.02f, rig.transform.up * 0.02f, 1, 0.02f,
                          Random.value < 0.5f ? new Color(1f, 0.6f, 1f) : new Color(0.6f, 0.95f, 1f));
            yield return null;
        }
        ClearCrystalBall();
    }

    public void ClearCrystalBall()
    {
        if (crystalBall != null) Destroy(crystalBall.gameObject);
        crystalBall = null;
    }

    private void Start()
    {
        rig = GetComponentInParent<GenieBoxRig>();
        sfx = FindFirstObjectByType<SoundFx>();
        cam = Camera.main;

        stars = Gfx.Particles("FireworkStars", transform, rig.transform, Gfx.Sparkle, Gfx.Blend.Additive, 1.8f, 0.016f, 0.2f, Color.white, 0.03f, 3000);
        FireworkLook(stars, 1.6f, 0.14f);
        willow = Gfx.Particles("FireworkWillow", transform, rig.transform, Gfx.SoftDot, Gfx.Blend.Additive, 3.2f, 0.012f, 0.1f, new Color(1f, 0.75f, 0.3f), 0.06f, 1500);
        FireworkLook(willow, 1.2f, 0.45f);
        glitter = Gfx.Particles("FireworkGlitter", transform, rig.transform, Gfx.Sparkle, Gfx.Blend.Additive, 0.5f, 0.01f, 0.02f, Color.white, 0.02f, 2000);
        rocketTrail = Gfx.Particles("RocketTrail", transform, rig.transform, Gfx.SoftDot, Gfx.Blend.Additive, 0.5f, 0.012f, 0.01f, new Color(1f, 0.8f, 0.4f), 0f, 800);
        flashMat = Gfx.NewUnlit(Gfx.Blend.Additive, Gfx.SoftDot, 40);
    }

    // Trails en drag voor vuurwerk.
    private static void FireworkLook(ParticleSystem ps, float drag, float trailTime)
    {
        var lv = ps.limitVelocityOverLifetime;
        lv.enabled = true;
        lv.drag = new ParticleSystem.MinMaxCurve(drag);
        lv.multiplyDragByParticleSize = false;
        lv.multiplyDragByParticleVelocity = false;

        var col = ps.colorOverLifetime;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var tr = ps.trails;
        tr.enabled = true;
        tr.lifetime = new ParticleSystem.MinMaxCurve(trailTime);
        tr.minVertexDistance = 0.002f;
        tr.dieWithParticles = true;
        tr.inheritParticleColor = true;
        tr.widthOverTrail = new ParticleSystem.MinMaxCurve(0.6f);
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorOverTrail = fade;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.trailMaterial = Gfx.NewUnlit(Gfx.Blend.Additive, null, 9);
    }

    // ================================================================== VUURWERK

    private static readonly Color[] Palette =
    {
        new Color(1f, 0.25f, 0.35f), new Color(0.3f, 0.8f, 1f), new Color(1f, 0.85f, 0.3f),
        new Color(0.5f, 1f, 0.4f), new Color(0.85f, 0.4f, 1f), new Color(1f, 0.55f, 0.15f)
    };

    public IEnumerator Fireworks()
    {
        // 1. Opening: drie losse bommen
        StartCoroutine(Rocket(BurstType.Peony, Palette[0], 0.32f));
        yield return new WaitForSeconds(1.1f);
        StartCoroutine(Rocket(BurstType.Ring, Palette[1], 0.36f));
        yield return new WaitForSeconds(1.1f);
        StartCoroutine(Rocket(BurstType.Willow, Palette[2], 0.42f));
        yield return new WaitForSeconds(1.6f);

        // 2. Crossettes (sterren die opnieuw ontploffen) links en rechts
        StartCoroutine(Rocket(BurstType.Crossette, Palette[3], 0.34f, -0.08f));
        yield return new WaitForSeconds(0.35f);
        StartCoroutine(Rocket(BurstType.Crossette, Palette[4], 0.34f, 0.08f));
        yield return new WaitForSeconds(1.6f);

        // 3. Een hart (in de richting van de speler)
        StartCoroutine(Rocket(BurstType.Heart, new Color(1f, 0.3f, 0.55f), 0.38f));
        yield return new WaitForSeconds(2f);

        // 4. Finale: salvo + dubbele ring + grote gouden treurwilg + knetterregen
        for (int i = 0; i < 7; i++)
        {
            var type = i % 3 == 0 ? BurstType.Ring : BurstType.Peony;
            StartCoroutine(Rocket(type, Palette[i % Palette.Length], Random.Range(0.28f, 0.4f), Random.Range(-0.1f, 0.1f)));
            yield return new WaitForSeconds(0.18f);
        }
        yield return new WaitForSeconds(0.5f);
        StartCoroutine(Rocket(BurstType.BigWillow, new Color(1f, 0.8f, 0.35f), 0.45f));
        yield return new WaitForSeconds(3.2f);
    }

    private enum BurstType { Peony, Ring, Willow, Crossette, Heart, BigWillow }

    private IEnumerator Rocket(BurstType type, Color color, float height, float sideways = 0f)
    {
        if (sfx != null) sfx.Play("whistle", 0.45f, Random.Range(0.9f, 1.2f));
        Vector3 start = rig.FrontPoint(Random.Range(0.35f, 0.65f), Random.Range(0.35f, 0.65f), 0.005f);
        Vector3 end = start + new Vector3(sideways + Random.Range(-0.04f, 0.04f), height, Random.Range(-0.04f, 0.04f));
        float flight = 0.75f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / flight)
        {
            Vector3 p = Vector3.Lerp(start, end, 1f - (1f - t) * (1f - t)) + new Vector3(Mathf.Sin(t * 30f) * 0.003f, 0f, 0f);
            Gfx.Burst(rocketTrail, rig.transform.TransformPoint(p), Vector3.zero, 3, 0.01f);
            yield return null;
        }
        Vector3 w = rig.transform.TransformPoint(end);
        Flash(w, color, type == BurstType.BigWillow ? 0.45f : 0.28f);
        if (sfx != null) sfx.Play("bang", type == BurstType.BigWillow ? 1f : 0.8f, Random.Range(0.8f, 1.15f));

        switch (type)
        {
            case BurstType.Peony: Sphere(stars, w, color, 140, 0.32f); Sphere(stars, w, Color.white, 30, 0.12f); break;
            case BurstType.Ring: RingBurst(w, color, 110, 0.3f); Sphere(glitter, w, Color.white, 40, 0.06f); break;
            case BurstType.Willow: Sphere(willow, w, color, 160, 0.14f); break;
            case BurstType.BigWillow:
                Sphere(willow, w, color, 320, 0.2f);
                Sphere(stars, w, Color.white, 80, 0.3f);
                StartCoroutine(Crackle(w, 0.35f, 2.2f));
                break;
            case BurstType.Heart: Heart(w, color); break;
            case BurstType.Crossette: StartCoroutine(Crossette(w, color)); break;
        }
    }

    private void Sphere(ParticleSystem ps, Vector3 w, Color color, int count, float speed)
    {
        for (int i = 0; i < count; i++)
            Gfx.Burst(ps, w, Random.onUnitSphere * speed * Random.Range(0.85f, 1f), 1, 0.005f, Color.Lerp(color, Color.white, Random.value * 0.25f));
    }

    // Ring naar de camera gekanteld.
    private void RingBurst(Vector3 w, Color color, int count, float speed)
    {
        Vector3 toCam = cam != null ? (cam.transform.position - w).normalized : Vector3.back;
        Vector3 axis = Vector3.Slerp(toCam, Random.onUnitSphere, 0.35f).normalized;
        Vector3 a = Vector3.Cross(axis, Vector3.up).normalized;
        if (a.sqrMagnitude < 0.01f) a = Vector3.right;
        Vector3 b = Vector3.Cross(axis, a);
        for (int i = 0; i < count; i++)
        {
            float ang = i / (float)count * Mathf.PI * 2f;
            Gfx.Burst(stars, w, (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * speed, 1, 0.003f, color);
        }
    }

    // Hartvorm.
    private void Heart(Vector3 w, Color color)
    {
        Vector3 right = cam != null ? cam.transform.right : Vector3.right;
        Vector3 up = Vector3.ProjectOnPlane(rig.transform.up, cam != null ? cam.transform.forward : Vector3.forward).normalized;
        for (int i = 0; i < 160; i++)
        {
            float t = i / 160f * Mathf.PI * 2f;
            float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
            float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
            Vector3 v = (right * x + up * y) * 0.017f;
            Gfx.Burst(stars, w, v, 1, 0.002f, color);
        }
        Sphere(glitter, w, Color.white, 30, 0.05f);
    }

    // Crossette: sterren die opnieuw ontploffen.
    private IEnumerator Crossette(Vector3 w, Color color)
    {
        var dirs = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2f;
            dirs[i] = (rig.transform.right * Mathf.Cos(a) + rig.transform.forward * Mathf.Sin(a) + rig.transform.up * 0.3f).normalized;
            Gfx.Burst(stars, w, dirs[i] * 0.22f, 1, 0f, color, 2f);
        }
        yield return new WaitForSeconds(0.45f);
        if (sfx != null) sfx.Play("crackle", 0.8f);
        for (int i = 0; i < 6; i++)
        {
            // positie na 0.45 s met luchtweerstand ongeveer: v/drag * (1 - e^(-drag*t))
            Vector3 p = w + dirs[i] * 0.22f / 1.6f * (1f - Mathf.Exp(-1.6f * 0.45f));
            for (int k = 0; k < 4; k++)
            {
                float a = k / 4f * Mathf.PI * 2f + i;
                Vector3 v = (Quaternion.AngleAxis(a * Mathf.Rad2Deg, dirs[i]) * Vector3.Cross(dirs[i], Vector3.up).normalized) * 0.12f;
                Gfx.Burst(stars, p, v, 3, 0.02f, Color.Lerp(color, Color.white, 0.4f), 0.7f);
            }
            Flash(p, color, 0.08f);
        }
    }

    // Knetterregen.
    private IEnumerator Crackle(Vector3 w, float radius, float duration)
    {
        if (sfx != null) sfx.Play("crackle", 1f);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            for (int i = 0; i < 6; i++)
                Gfx.Burst(glitter, w + Random.insideUnitSphere * radius + Vector3.down * t * 0.08f, Vector3.zero, 1, 0.01f);
            yield return null;
        }
    }

    private void Flash(Vector3 w, Color color, float size)
    {
        var q = Gfx.Quad(transform, Vector3.zero, Quaternion.identity, Vector2.one, flashMat, "Flash");
        q.transform.position = w;
        StartCoroutine(FlashRoutine(q.transform, color, size));
    }

    private IEnumerator FlashRoutine(Transform q, Color color, float size)
    {
        var r = q.GetComponent<Renderer>();
        var mpb = new MaterialPropertyBlock();
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
        {
            if (cam != null) q.rotation = Quaternion.LookRotation(q.position - cam.transform.position);
            q.localScale = Vector3.one * size * (0.4f + t);
            mpb.SetColor(Shader.PropertyToID("_Color"), new Color(Mathf.Lerp(1f, color.r, t), Mathf.Lerp(1f, color.g, t), Mathf.Lerp(1f, color.b, t), (1f - t) * (1f - t)));
            r.SetPropertyBlock(mpb);
            yield return null;
        }
        Destroy(q.gameObject);
    }

    // ================================================================== MAKEOVER

    // Past de gekozen makeover toe.
    public IEnumerator Makeover(int index)
    {
        var options = AvailableMakeovers;
        string skin = options[Mathf.Clamp(index, 0, options.Length - 1)];
        BuildShell(skin);
        CurrentSkin = skin;
        if (sfx != null) { sfx.Play("poof", 0.8f); sfx.Play("sparkle", 1f); }

        // "Wipe" van onder naar boven: de nieuwe doos schuift over de oude
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1f)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            shell.localScale = new Vector3(1f, 1f, Mathf.Max(0.001f, e));
            shell.localPosition = new Vector3(0f, 0f, -rig.Length / 2f * (1f - e));
            Vector3 edge = new Vector3(Random.Range(-rig.Width, rig.Width) * 0.5f, rig.TopY + 0.003f, -rig.Length / 2f + rig.Length * e);
            Gfx.Burst(glitter, rig.transform.TransformPoint(edge), rig.transform.up * 0.05f, 4, 0.03f, new Color(1f, 0.9f, 0.6f));
            yield return null;
        }
        shell.localScale = Vector3.one;
        shell.localPosition = Vector3.zero;
    }

    public void ClearSkin()
    {
        if (shell != null) Destroy(shell.gameObject);
        shell = null;
        lavaMats.Clear();
        CurrentSkin = "";
    }

    private void Update()
    {
        foreach (var m in lavaMats) if (m != null) m.mainTextureOffset = new Vector2(0f, Time.time * 0.03f);
    }

    // Virtuele doos uit 6 vlakken.
    private void BuildShell(string skin)
    {
        ClearSkin();
        shell = Gfx.Group("Makeover_" + skin, transform);
        float W = rig.Width, L = rig.Length, T = rig.Thickness, o = 0.0015f;

        Texture2D front, side, end;
        switch (skin)
        {
            case "Kartonnen doos": front = side = end = Kraft(); break;
            case "Maanvlokken": front = CerealFront(new Color(0.05f, 0.08f, 0.3f), new Color(0.15f, 0.3f, 0.7f), true); side = end = Starry(new Color(0.08f, 0.12f, 0.4f)); break;
            case "Fruitringen": front = CerealFront(new Color(1f, 0.45f, 0.6f), new Color(1f, 0.7f, 0.3f), false); side = end = Dots(new Color(1f, 0.5f, 0.65f)); break;
            case "Schatkist": front = side = end = Wood(); break;
            case Legendary: front = side = end = Palace(); break;
            default: front = side = end = Simple(skin); break;
        }

        // (normaal naar buiten, positie, grootte, textuur)
        Face(Vector3.up, new Vector3(0f, T / 2f + o, 0f), new Vector2(W, L), front, skin);
        Face(Vector3.down, new Vector3(0f, -T / 2f - o, 0f), new Vector2(W, L), side, skin);
        Face(Vector3.left, new Vector3(-W / 2f - o, 0f, 0f), new Vector2(L, T), side, skin);
        Face(Vector3.right, new Vector3(W / 2f + o, 0f, 0f), new Vector2(L, T), side, skin);
        Face(Vector3.forward, new Vector3(0f, 0f, L / 2f + o), new Vector2(W, T), end, skin);
        Face(Vector3.back, new Vector3(0f, 0f, -L / 2f - o), new Vector2(W, T), end, skin);

        Decorate(skin, W, L, T, o);
    }

    private void Face(Vector3 normal, Vector3 pos, Vector2 size, Texture2D tex, string skin)
    {
        Vector3 up = Mathf.Abs(normal.y) > 0.5f ? Vector3.forward : Vector3.up;
        var mat = Gfx.NewToon(tex);
        mat.SetFloat("_Ambient", 0.8f);
        if (skin == "Goud") { mat.SetFloat("_Glow", 0.25f); mat.SetColor("_GlowColor", new Color(1f, 0.85f, 0.4f)); }
        if (skin == Legendary) { mat.SetFloat("_Glow", 0.35f); mat.SetColor("_GlowColor", new Color(0.7f, 0.4f, 1f)); }
        if (skin == "Lava") { mat.SetFloat("_Glow", 0.5f); mat.SetColor("_GlowColor", new Color(1f, 0.4f, 0.1f)); lavaMats.Add(mat); }
        Gfx.Quad(shell, pos, Quaternion.LookRotation(-normal, up), size, mat, "Face");
    }

    // Opschriften en extra's per makeover.
    private void Decorate(string skin, float W, float L, float T, float o)
    {
        float top = T / 2f + o + 0.0005f;
        switch (skin)
        {
            case "Kartonnen doos":
                // Bruine plakband over het midden, over de voorkant en beide uiteinden
                var tape = new Color(0.78f, 0.62f, 0.38f);
                Gfx.Prim(PrimitiveType.Cube, shell, new Vector3(0f, top, 0f), new Vector3(0.05f, 0.0004f, L + 0.004f), tape, 0.05f, "Tape");
                Gfx.Prim(PrimitiveType.Cube, shell, new Vector3(0f, 0f, L / 2f + o + 0.0005f), new Vector3(0.05f, T * 0.6f, 0.0004f), tape, 0.05f, "TapeEnd");
                Gfx.Prim(PrimitiveType.Cube, shell, new Vector3(0f, 0f, -L / 2f - o - 0.0005f), new Vector3(0.05f, T * 0.6f, 0.0004f), tape, 0.05f, "TapeEnd");
                Label("BREEKBAAR", new Vector3(-W * 0.22f, top + 0.0003f, L * 0.22f), new Vector2(W * 0.4f, 0.03f), new Color(0.75f, 0.1f, 0.1f), FontStyles.Bold, -8f);
                Label("DEZE KANT\nBOVEN", new Vector3(W * 0.25f, top + 0.0003f, -L * 0.25f), new Vector2(W * 0.38f, 0.045f), new Color(0.2f, 0.12f, 0.06f), FontStyles.Bold, 0f);
                Label("Genie & Co.\nverhuizingen", new Vector3(-W * 0.22f, top + 0.0003f, -L * 0.3f), new Vector2(W * 0.4f, 0.04f), new Color(0.25f, 0.15f, 0.08f, 0.8f), FontStyles.Italic, 0f);
                break;

            case "Maanvlokken":
                Label("MAAN\nVLOKKEN", new Vector3(0f, top + 0.0003f, L * 0.24f), new Vector2(W * 0.9f, L * 0.3f), new Color(1f, 0.88f, 0.3f), FontStyles.Bold, 0f);
                Label("met knapperige sterretjes!", new Vector3(0f, top + 0.0003f, L * 0.02f), new Vector2(W * 0.85f, 0.025f), Color.white, FontStyles.Italic, 0f);
                break;

            case "Fruitringen":
                Label("FRUIT\nRINGEN", new Vector3(0f, top + 0.0003f, L * 0.24f), new Vector2(W * 0.9f, L * 0.3f), Color.white, FontStyles.Bold, 0f);
                Label("nu met 5 smaken!", new Vector3(0f, top + 0.0003f, L * 0.02f), new Vector2(W * 0.8f, 0.025f), new Color(0.5f, 0.1f, 0.3f), FontStyles.Bold, 0f);
                break;

            case Legendary:
                // Gouden koepeltjes (uivormig) op de hoeken en een grote in het midden
                var g = new Color(1f, 0.8f, 0.3f);
                foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                    Dome(new Vector3(c.x * (W / 2f - 0.02f), top, c.y * (L / 2f - 0.02f)), 0.022f, g);
                Dome(new Vector3(0f, top, 0f), 0.05f, g);
                Label("DJINN-PALEIS", new Vector3(0f, top + 0.0003f, -L * 0.36f), new Vector2(W * 0.8f, 0.03f), new Color(1f, 0.85f, 0.4f), FontStyles.Bold, 0f);
                break;

            case "Schatkist":
                // Gouden slot en hoekbeslag
                var goldC = new Color(1f, 0.8f, 0.3f);
                Gfx.Prim(PrimitiveType.Cube, shell, new Vector3(0f, top + 0.002f, -L / 2f + 0.025f), new Vector3(0.04f, 0.004f, 0.035f), goldC, 0.4f, "Lock");
                Gfx.Prim(PrimitiveType.Cylinder, shell, new Vector3(0f, top + 0.0045f, -L / 2f + 0.025f), new Vector3(0.008f, 0.0005f, 0.008f), Color.black, 0f, "Keyhole");
                foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                    Gfx.Prim(PrimitiveType.Cube, shell, new Vector3(c.x * (W / 2f - 0.006f), top + 0.001f, c.y * (L / 2f - 0.006f)), new Vector3(0.016f, 0.002f, 0.016f), goldC, 0.4f, "Corner");
                break;
        }
    }

    // Uivormig koepeltje.
    private void Dome(Vector3 basePos, float r, Color color)
    {
        Gfx.Prim(PrimitiveType.Cylinder, shell, basePos + new Vector3(0f, r * 0.3f, 0f), new Vector3(r * 1.4f, r * 0.3f, r * 1.4f), new Color(0.45f, 0.2f, 0.6f), 0.2f, "DomeBase");
        Gfx.Prim(PrimitiveType.Sphere, shell, basePos + new Vector3(0f, r * 1.3f, 0f), new Vector3(r * 1.8f, r * 1.6f, r * 1.8f), color, 0.5f, "Dome");
        Gfx.Prim(PrimitiveType.Capsule, shell, basePos + new Vector3(0f, r * 2.4f, 0f), new Vector3(r * 0.3f, r * 0.5f, r * 0.3f), color, 0.8f, "Spire");
    }

    // Plat 3D-tekstje op de voorkant.
    private void Label(string text, Vector3 localPos, Vector2 size, Color color, FontStyles style, float angle)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(shell, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, angle);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.01f;
        tmp.fontSizeMax = 50f;
        tmp.lineSpacing = -20f;
        tmp.rectTransform.sizeDelta = size;
    }

    // ------------------------------------------------------------------ Texturen

    private static Texture2D NewTex(int w, int h, string name) =>
        new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = name };

    private static Texture2D Kraft()
    {
        int s = 256;
        var tex = NewTex(s, s, "Kraft");
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.04f, y * 0.04f) * 0.5f + Mathf.PerlinNoise(x * 0.4f, y * 0.05f) * 0.2f;
                float fiber = Mathf.PerlinNoise(x * 1.7f, y * 0.12f) > 0.78f ? -0.08f : 0f;
                Color c = new Color(0.72f, 0.55f, 0.36f) * (0.85f + n * 0.3f + fiber);
                // geribbelde golfkarton-lijntjes aan de rand
                if (y < 6 || y > s - 7) c *= 0.85f + 0.1f * Mathf.Sin(x * 0.8f);
                c.a = 1f;
                px[y * s + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // Arabeskenpatroon.
    private static Texture2D Palace()
    {
        int sz = 256;
        var tex = NewTex(sz, sz, "Palace");
        var px = new Color32[sz * sz];
        for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float u = (x % 64) / 64f - 0.5f, v = (y % 64) / 64f - 0.5f;
                float ring = Mathf.Abs(Mathf.Sqrt(u * u + v * v) - 0.32f);
                float diamond = Mathf.Abs(Mathf.Abs(u) + Mathf.Abs(v) - 0.5f);
                Color c = Color.Lerp(new Color(0.22f, 0.06f, 0.35f), new Color(0.38f, 0.12f, 0.55f), Mathf.PerlinNoise(x * 0.03f, y * 0.03f));
                if (ring < 0.025f || diamond < 0.02f) c = new Color(1f, 0.8f, 0.35f);
                if (Mathf.Sqrt(u * u + v * v) < 0.05f) c = new Color(0.4f, 0.9f, 1f);
                c.a = 1f;
                px[y * sz + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    private static Texture2D Wood()
    {
        int s = 256;
        var tex = NewTex(s, s, "Wood");
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int plank = y / 43;
                float grain = Mathf.Sin(x * 0.08f + Mathf.PerlinNoise(x * 0.02f, plank * 3.1f) * 8f) * 0.5f + 0.5f;
                Color c = Color.Lerp(new Color(0.35f, 0.2f, 0.1f), new Color(0.55f, 0.34f, 0.17f), grain * 0.7f + (plank % 2) * 0.15f);
                if (y % 43 < 2) c = new Color(0.15f, 0.08f, 0.04f);                   // naad tussen planken
                bool band = (x > 30 && x < 46) || (x > 210 && x < 226);               // ijzeren banden
                if (band) c = Color.Lerp(new Color(0.18f, 0.18f, 0.2f), new Color(0.35f, 0.35f, 0.38f), Mathf.PerlinNoise(x * 0.3f, y * 0.3f));
                if (band && y % 43 > 18 && y % 43 < 24 && (x % 16 > 5 && x % 16 < 11)) c = new Color(0.6f, 0.6f, 0.62f); // klinknagels
                c.a = 1f;
                px[y * s + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    private static Texture2D Starry(Color bg)
    {
        int s = 256;
        var tex = NewTex(s, s, "Starry");
        var px = new Color32[s * s];
        var rnd = new System.Random(7);
        for (int i = 0; i < px.Length; i++) { int y = i / s, x = i % s; Color c = bg * (0.8f + 0.4f * Mathf.PerlinNoise(x * 0.02f, y * 0.02f)); c.a = 1f; px[i] = c; }
        for (int i = 0; i < 160; i++) { int x = rnd.Next(s), y = rnd.Next(s); px[y * s + x] = new Color32(255, 250, 200, 255); }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    private static Texture2D Dots(Color bg)
    {
        int s = 256;
        var tex = NewTex(s, s, "Dots");
        var px = new Color32[s * s];
        Color[] cols = { new Color(1f, 0.9f, 0.3f), new Color(0.4f, 0.8f, 1f), new Color(0.5f, 1f, 0.5f), Color.white };
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int cx = x / 32, cy = y / 32;
                float dx = x % 32 - 16, dy = y % 32 - 16;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                Color c = bg;
                if (d < 9f && d > 4f) c = cols[(cx + cy * 3) % cols.Length];
                c.a = 1f;
                px[y * s + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // Ontbijtgranendoos-voorkant.
    private static Texture2D CerealFront(Color bottom, Color topC, bool moon)
    {
        int w = 256, h = 320;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "CerealFront" };
        var px = new Color32[w * h];
        var rnd = new System.Random(moon ? 3 : 4);
        Color[] ringCols = { new Color(1f, 0.9f, 0.2f), new Color(0.3f, 0.8f, 1f), new Color(0.4f, 1f, 0.4f), new Color(1f, 0.4f, 0.2f), new Color(0.8f, 0.4f, 1f) };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = y / (float)h;
                Color c = Color.Lerp(bottom, topC, v);
                // kom onderaan (ellips) met ontbijtgranen erin
                float bx = (x - w * 0.5f) / (w * 0.38f), by = (y - h * 0.2f) / (h * 0.13f);
                if (bx * bx + by * by < 1f && y < h * 0.24f) c = Color.Lerp(new Color(0.95f, 0.95f, 1f), new Color(0.7f, 0.75f, 0.85f), (h * 0.24f - y) / (h * 0.25f));
                if (bx * bx + by * by < 1f && y >= h * 0.24f && y < h * 0.3f) c = moon ? new Color(0.95f, 0.8f, 0.35f) : ringCols[(x / 14 + y / 10) % ringCols.Length];
                if (moon)
                {
                    float mx = x - w * 0.78f, my = y - h * 0.88f, md = Mathf.Sqrt(mx * mx + my * my);
                    if (md < 34f) c = Color.Lerp(new Color(1f, 0.95f, 0.7f), new Color(0.85f, 0.8f, 0.55f), Mathf.PerlinNoise(x * 0.15f, y * 0.15f));
                }
                c.a = 1f;
                px[y * w + x] = c;
            }
        if (moon) for (int i = 0; i < 90; i++) { int x = rnd.Next(w), y = rnd.Next((int)(h * 0.35f), h); px[y * w + x] = new Color32(255, 255, 220, 255); }
        else
            for (int i = 0; i < 14; i++)
            {
                int cx = rnd.Next(20, w - 20), cy = rnd.Next((int)(h * 0.35f), (int)(h * 0.5f));
                Color rc = ringCols[i % ringCols.Length];
                for (int y = cy - 12; y <= cy + 12; y++)
                    for (int x = cx - 12; x <= cx + 12; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        if (d > 5f && d < 11f && x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = rc;
                    }
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    private static Texture2D Simple(string skin)
    {
        int s = 256;
        var tex = NewTex(s, s, "Skin_" + skin);
        var px = new Color32[s * s];
        var rnd = new System.Random(skin.GetHashCode());
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = x / (float)s, v = y / (float)s;
                Color c;
                if (skin == "Goud")
                {
                    float n = Mathf.PerlinNoise(u * 6f, v * 6f);
                    float swirl = Mathf.Sin((u + v) * 25f + n * 6f) * 0.5f + 0.5f;
                    c = Color.Lerp(new Color(0.75f, 0.5f, 0.12f), new Color(1f, 0.88f, 0.45f), swirl * 0.7f + n * 0.3f);
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    if (edge < 0.04f) c = new Color(1f, 0.92f, 0.6f);
                    else if (edge < 0.055f) c = new Color(0.5f, 0.3f, 0.05f);
                }
                else if (skin == "Sterrenhemel")
                {
                    float n = Mathf.PerlinNoise(u * 4f + 10f, v * 4f);
                    c = Color.Lerp(new Color(0.03f, 0.03f, 0.15f), new Color(0.25f, 0.1f, 0.45f), n * n);
                }
                else
                {
                    float n = Mathf.PerlinNoise(u * 5f, v * 5f) + 0.5f * Mathf.PerlinNoise(u * 13f, v * 13f);
                    float cracks = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(n - 0.75f) * 6f), 2f);
                    c = Color.Lerp(new Color(0.12f, 0.05f, 0.04f), new Color(1f, 0.55f, 0.1f), cracks);
                }
                c.a = 1f;
                px[y * s + x] = c;
            }
        if (skin == "Sterrenhemel")
            for (int i = 0; i < 260; i++)
            {
                int x = rnd.Next(s), y = rnd.Next(s);
                byte b = (byte)rnd.Next(160, 255);
                px[y * s + x] = new Color32(b, b, 255, 255);
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }
}

// Billboard: quad naar de camera.
public class Billboard : MonoBehaviour
{
    private void LateUpdate()
    {
        var c = Camera.main;
        if (c != null) transform.rotation = Quaternion.LookRotation(transform.position - c.transform.position, c.transform.up);
    }
}
