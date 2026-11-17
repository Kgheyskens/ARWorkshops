using UnityEngine;

// 3D-radio onder de ARCamera die koud/warm/heet toont (naald, LEDs, antenne).
public class RadioDevice : MonoBehaviour
{
    [Header("Onderdelen (ingevuld door 'Bouw radio-model' of zelf slepen)")]
    public Transform needle;
    public Transform antenna;
    public Transform speaker;
    public Transform tuningKnob;
    public PartColor dialLight;
    public PartColor[] ledBar;
    public PartColor[] bodyParts;
    public PartColor[] trimParts;
    public PartColor[] accentParts;

    [Header("Beweging")]
    public float needleMinAngle = 60f;
    public float needleMaxAngle = -60f;
    public float antennaMinScale = 0.35f;
    public float antennaMaxScale = 1f;
    public float bobAmount = 0.004f;
    [Tooltip("Enkel tonen tijdens het zoeken (niet in de menu's)")]
    public bool onlyVisibleWhilePlaying = true;

    [Header("Responsive plaatsing")]
    [Tooltip("Radio automatisch op de juiste plek en grootte zetten voor elk schermformaat")]
    public bool autoFitToScreen = true;
    [Tooltip("Afstand voor de camera (meter)")]
    public float distance = 0.45f;
    [Tooltip("Hoogte op het scherm: 0 = onderkant, 0.5 = midden")]
    [Range(0f, 1f)] public float screenHeight = 0.36f;
    [Tooltip("Maximale breedte van de radio als deel van de schermbreedte")]
    [Range(0.1f, 1f)] public float maxScreenWidth = 0.6f;
    [Tooltip("Maximale hoogte van de radio als deel van de schermhoogte")]
    [Range(0.1f, 1f)] public float maxScreenHeightPart = 0.22f;
    [Tooltip("Afmetingen van het model in meter (breedte, hoogte incl. handvat/antenne)")]
    public Vector2 modelSize = new Vector2(0.25f, 0.25f);

    private SignalTracker tracker;
    private DangerZoneManager zones;
    private SignalAudio sfx;
    private GameManager game;
    private Renderer[] renderers;
    private bool visible = true;
    private Vector3 basePosition;
    private Camera cam;
    private Vector3 speakerBaseScale = Vector3.one;
    private float shownStrength;
    private float speakerPulse;
    private Gradient gradient;
    private static readonly Color LedOff = new Color(0.08f, 0.08f, 0.08f);

    void Start()
    {
        tracker = FindFirstObjectByType<SignalTracker>();
        zones = FindFirstObjectByType<DangerZoneManager>();
        sfx = FindFirstObjectByType<SignalAudio>();
        game = FindFirstObjectByType<GameManager>();
        renderers = GetComponentsInChildren<Renderer>(true);
        basePosition = transform.localPosition;
        cam = GetComponentInParent<Camera>();
        if (cam == null) cam = Camera.main;
        if (speaker != null) speakerBaseScale = speaker.localScale;
        if (sfx != null) sfx.Beeped += OnBeep;

        gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.30f, 0.55f, 1.00f), 0.00f),
                new GradientColorKey(new Color(0.30f, 0.90f, 1.00f), 0.30f),
                new GradientColorKey(new Color(1.00f, 0.90f, 0.30f), 0.55f),
                new GradientColorKey(new Color(1.00f, 0.55f, 0.15f), 0.78f),
                new GradientColorKey(new Color(1.00f, 0.20f, 0.15f), 1.00f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
    }

    void OnDestroy()
    {
        if (sfx != null) sfx.Beeped -= OnBeep;
    }

    private void OnBeep() => speakerPulse = 1f;

    // Kleuren van de gekozen radio toepassen.
    public void ApplyRadio(RadioDefinition radio)
    {
        if (radio == null) return;
        Color body = Color.Lerp(radio.background, Color.white, 0.18f);
        foreach (var p in bodyParts) if (p != null) p.SetColor(body);
        foreach (var p in trimParts) if (p != null) p.SetColor(radio.primary);
        foreach (var p in accentParts) if (p != null) p.SetColor(radio.accent);
    }

    void Update()
    {
        bool show = !onlyVisibleWhilePlaying || game == null || game.State == GameState.Playing;
        SetVisible(show);
        if (!show || tracker == null) return;

        bool inDanger = zones != null && zones.IsInside;
        float target = inDanger ? 0f : tracker.Strength;
        shownStrength = Mathf.Lerp(shownStrength, target, 1f - Mathf.Exp(-4f * Time.deltaTime));
        Color temperature = tracker.Level == SignalLevel.Lost ? Color.gray : gradient.Evaluate(shownStrength);

        // Naald
        if (needle != null)
        {
            float angle = Mathf.Lerp(needleMinAngle, needleMaxAngle, shownStrength);
            if (inDanger) angle += Random.Range(-25f, 25f);
            else angle += Mathf.Sin(Time.time * 13f) * 1.5f; // klein trillen, zoals een echte meter
            needle.localEulerAngles = new Vector3(0f, 0f, angle);
        }

        // Antenne
        if (antenna != null)
        {
            Vector3 s = antenna.localScale;
            s.y = Mathf.Lerp(antennaMinScale, antennaMaxScale, shownStrength);
            antenna.localScale = s;
        }

        // Luidspreker
        speakerPulse = Mathf.MoveTowards(speakerPulse, 0f, Time.deltaTime * 6f);
        if (speaker != null) speaker.localScale = speakerBaseScale * (1f + 0.12f * speakerPulse);

        // Afstemknop draait traag mee
        if (tuningKnob != null) tuningKnob.Rotate(0f, (20f + 200f * shownStrength) * Time.deltaTime, 0f, Space.Self);

        // Scherm + LED-balk
        if (dialLight != null)
            dialLight.SetColor(inDanger ? (Random.value < 0.5f ? Color.red : Color.black) : temperature);

        if (ledBar != null && ledBar.Length > 0)
        {
            int lit = Mathf.CeilToInt(shownStrength * ledBar.Length);
            for (int i = 0; i < ledBar.Length; i++)
            {
                if (ledBar[i] == null) continue;
                bool on = inDanger ? Random.value < 0.3f : i < lit;
                Color c = inDanger ? Color.red : gradient.Evaluate(ledBar.Length == 1 ? 1f : i / (float)(ledBar.Length - 1));
                ledBar[i].SetColor(on ? c : LedOff);
            }
        }

        if (autoFitToScreen) FitToScreen();

        // Wandelbeweging + schudden in storing
        Vector3 offset = new Vector3(0f, Mathf.Sin(Time.time * 4f) * bobAmount, 0f);
        if (inDanger) offset += new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * 0.004f;
        transform.localPosition = basePosition + offset;
    }

    // Zet de radio zo dat hij op elk schermformaat goed in beeld staat.
    private void FitToScreen()
    {
        if (cam == null || transform.parent != cam.transform) return;

        float viewHeight = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewWidth = viewHeight * cam.aspect;

        float scale = Mathf.Min(viewWidth * maxScreenWidth / modelSize.x,
                                viewHeight * maxScreenHeightPart / modelSize.y);
        transform.localScale = Vector3.one * scale;

        float y = (screenHeight - 0.5f) * viewHeight;
        basePosition = new Vector3(0f, y, distance);
    }

    private void SetVisible(bool v)
    {
        if (v == visible || renderers == null) return;
        visible = v;
        foreach (var r in renderers) if (r != null) r.enabled = v;
    }

    // ------------------------------------------------------------------ Model bouwen

    [ContextMenu("Bouw radio-model")]
    public void BuildModel()
    {
        ModelBuilder.ClearChildren(transform);

        Color body = new Color(0.25f, 0.22f, 0.18f);
        Color trim = new Color(0.95f, 0.65f, 0.25f);
        Color dark = new Color(0.08f, 0.08f, 0.09f);
        Color metal = new Color(0.7f, 0.72f, 0.75f);
        Color cream = new Color(0.95f, 0.9f, 0.75f);

        Transform t = transform;
        // Voorkant van de radio wijst naar -Z (naar de camera)
        var bodyPart = ModelBuilder.Part(PrimitiveType.Cube, t, "Behuizing", new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.13f, 0.07f), Vector3.zero, body);
        var topTrim = ModelBuilder.Part(PrimitiveType.Cube, t, "Rand boven", new Vector3(0f, 0.066f, 0f), new Vector3(0.226f, 0.006f, 0.074f), Vector3.zero, trim);
        var bottomTrim = ModelBuilder.Part(PrimitiveType.Cube, t, "Rand onder", new Vector3(0f, -0.066f, 0f), new Vector3(0.226f, 0.006f, 0.074f), Vector3.zero, trim);
        ModelBuilder.Part(PrimitiveType.Cube, t, "Frontpaneel", new Vector3(0f, 0f, -0.0355f), new Vector3(0.205f, 0.115f, 0.002f), Vector3.zero, Color.Lerp(body, Color.black, 0.3f));

        // Handvat
        var handleL = ModelBuilder.Part(PrimitiveType.Cube, t, "Handvat L", new Vector3(-0.07f, 0.085f, 0f), new Vector3(0.008f, 0.035f, 0.012f), Vector3.zero, trim);
        var handleR = ModelBuilder.Part(PrimitiveType.Cube, t, "Handvat R", new Vector3(0.07f, 0.085f, 0f), new Vector3(0.008f, 0.035f, 0.012f), Vector3.zero, trim);
        var handleTop = ModelBuilder.Part(PrimitiveType.Cube, t, "Handvat", new Vector3(0f, 0.102f, 0f), new Vector3(0.148f, 0.008f, 0.014f), Vector3.zero, trim);

        // Luidspreker links
        speaker = ModelBuilder.Pivot("Luidspreker", t, new Vector3(-0.055f, -0.002f, -0.037f), new Vector3(90f, 0f, 0f));
        ModelBuilder.Part(PrimitiveType.Cylinder, speaker, "Rooster", Vector3.zero, new Vector3(0.085f, 0.003f, 0.085f), Vector3.zero, dark);
        var speakerRing = ModelBuilder.Part(PrimitiveType.Cylinder, speaker, "Ring", new Vector3(0f, -0.001f, 0f), new Vector3(0.05f, 0.004f, 0.05f), Vector3.zero, trim);
        ModelBuilder.Part(PrimitiveType.Cylinder, speaker, "Kern", new Vector3(0f, -0.002f, 0f), new Vector3(0.022f, 0.004f, 0.022f), Vector3.zero, dark);

        // Schaal met naald rechtsboven
        dialLight = ModelBuilder.Part(PrimitiveType.Cube, t, "Schaal", new Vector3(0.052f, 0.027f, -0.037f), new Vector3(0.09f, 0.038f, 0.002f), Vector3.zero, cream);
        for (int i = 0; i < 7; i++)
        {
            float x = 0.016f + i * 0.012f;
            ModelBuilder.Part(PrimitiveType.Cube, t, "Streepje " + i, new Vector3(x, 0.04f, -0.0385f), new Vector3(0.0012f, i % 3 == 0 ? 0.01f : 0.006f, 0.001f), Vector3.zero, dark);
        }
        needle = ModelBuilder.Pivot("Naald", t, new Vector3(0.052f, 0.01f, -0.039f), Vector3.zero);
        ModelBuilder.Part(PrimitiveType.Cube, needle, "Wijzer", new Vector3(0f, 0.016f, 0f), new Vector3(0.0018f, 0.032f, 0.001f), Vector3.zero, new Color(0.85f, 0.1f, 0.1f));

        // LED-signaalbalk
        ledBar = new PartColor[5];
        for (int i = 0; i < ledBar.Length; i++)
        {
            float x = 0.022f + i * 0.015f;
            float h = 0.006f + i * 0.0025f;
            ledBar[i] = ModelBuilder.Part(PrimitiveType.Cube, t, "LED " + i, new Vector3(x, -0.016f + h / 2f, -0.037f), new Vector3(0.01f, h, 0.003f), Vector3.zero, LedOff);
        }

        // Knoppen
        tuningKnob = ModelBuilder.Pivot("Afstemknop", t, new Vector3(0.035f, -0.045f, -0.037f), new Vector3(90f, 0f, 0f));
        var knob1 = ModelBuilder.Part(PrimitiveType.Cylinder, tuningKnob, "Knop", Vector3.zero, new Vector3(0.022f, 0.006f, 0.022f), Vector3.zero, trim);
        ModelBuilder.Part(PrimitiveType.Cube, tuningKnob, "Streep", new Vector3(0f, -0.006f, 0.007f), new Vector3(0.003f, 0.002f, 0.008f), Vector3.zero, dark);
        var knob2 = ModelBuilder.Part(PrimitiveType.Cylinder, t, "Volumeknop", new Vector3(0.075f, -0.045f, -0.037f), new Vector3(0.018f, 0.005f, 0.018f), new Vector3(90f, 0f, 0f), trim);

        // Antenne (pivot onderaan → schaal in Y = uitschuiven)
        Transform antennaBase = ModelBuilder.Pivot("Antennevoet", t, new Vector3(0.09f, 0.068f, 0.015f), new Vector3(0f, 0f, -18f));
        ModelBuilder.Part(PrimitiveType.Sphere, antennaBase, "Scharnier", Vector3.zero, Vector3.one * 0.012f, Vector3.zero, metal);
        antenna = ModelBuilder.Pivot("Antenne", antennaBase, Vector3.zero, Vector3.zero);
        ModelBuilder.Part(PrimitiveType.Cylinder, antenna, "Staaf", new Vector3(0f, 0.08f, 0f), new Vector3(0.004f, 0.08f, 0.004f), Vector3.zero, metal);
        var tip = ModelBuilder.Part(PrimitiveType.Sphere, antenna, "Tip", new Vector3(0f, 0.162f, 0f), Vector3.one * 0.009f, Vector3.zero, trim);

        bodyParts = new[] { bodyPart };
        trimParts = new[] { topTrim, bottomTrim, handleL, handleR, handleTop, speakerRing, tip };
        accentParts = new[] { knob1, knob2 };

        Debug.Log("[RadioDevice] Radio-model gebouwd. Verplaats/draai het object om de positie in beeld te kiezen.");
    }
}
