using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Beeldruis over de camera als visuele aanwijzer: minder ruis = juiste richting.
public class SignalMeter : MonoBehaviour
{
    [Header("UI-referenties")]
    public TMP_Text levelText;   // statusmelding (groot)
    public TMP_Text trendText;   // extra regel (klein)
    public RawImage staticOverlay;

    [Header("Ruis")]
    [Tooltip("Ruis ver van de mast als je stilstaat")]
    [Range(0f, 1f)] public float baseStaticAlpha = 0.45f;
    [Tooltip("Extra ruis als je de verkeerde kant op loopt")]
    [Range(0f, 1f)] public float wrongWayBoost = 0.35f;
    [Tooltip("Zoveel ruis verdwijnt als je de goede kant op loopt")]
    [Range(0f, 1f)] public float rightWayReduction = 0.2f;
    [Tooltip("Hoe snel de ruis reageert op je richting (hoger = sneller)")]
    public float reactionSpeed = 2f;
    [Range(0f, 1f)] public float dangerStaticAlpha = 0.7f;
    public int noiseWidth = 120;
    public int noiseHeight = 214;
    public float noiseFps = 24f;

    [Header("Overig")]
    [Tooltip("Exacte afstand tonen (perk van de Scanner X-9)")]
    public bool showDistance = false;

    private SignalTracker tracker;
    private DangerZoneManager zones;
    private Texture2D noiseTex;
    private Color32[] pixels;
    private float noiseTimer;
    private float shownAlpha;
    private float trendOffset;
    private float rollingBand;
    private uint rng = 2463534242;

    void Start()
    {
        // Zwarte rand rond de tekst (pas hier: de tekst moet eerst actief/geïnitialiseerd zijn)
        foreach (var t in new[] { levelText, trendText })
        {
            if (t == null) continue;
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(0, 0, 0, 255);
        }

        if (staticOverlay != null)
        {
            noiseTex = new Texture2D(noiseWidth, noiseHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat
            };
            pixels = new Color32[noiseWidth * noiseHeight];
            staticOverlay.texture = noiseTex;
            staticOverlay.raycastTarget = false;
        }
    }

    void Update()
    {
        if (tracker == null) tracker = FindFirstObjectByType<SignalTracker>();
        if (zones == null) zones = FindFirstObjectByType<DangerZoneManager>();
        if (tracker == null) return;

        bool inDanger = zones != null && zones.IsInside;
        bool locked = tracker.Level == SignalLevel.Locked;
        bool lost = tracker.Level == SignalLevel.Lost;

        UpdateTexts(inDanger, locked);

        // --- Hoeveel ruis? ---
        float targetTrend = 0f;
        if (tracker.Trend == SignalTrend.Colder) targetTrend = wrongWayBoost;
        else if (tracker.Trend == SignalTrend.Warmer) targetTrend = -rightWayReduction;
        trendOffset = Mathf.MoveTowards(trendOffset, targetTrend, Time.deltaTime * reactionSpeed * 0.5f);

        float target;
        if (inDanger) target = dangerStaticAlpha;
        else if (locked || lost) target = 0f;
        else
        {
            float distanceNoise = baseStaticAlpha * (1f - tracker.Clarity); // dichterbij = zuiverder
            target = Mathf.Clamp(distanceNoise + trendOffset * (1f - tracker.Clarity * 0.5f), 0.03f, 0.85f);
        }
        shownAlpha = Mathf.MoveTowards(shownAlpha, target, Time.deltaTime * reactionSpeed * 0.4f);

        // --- Tekenen ---
        if (staticOverlay == null || noiseTex == null) return;

        Color tint = inDanger ? new Color(1f, 0.3f, 0.25f) : Color.white;
        tint.a = shownAlpha;
        staticOverlay.color = tint;

        noiseTimer -= Time.deltaTime;
        if (noiseTimer <= 0f && shownAlpha > 0.005f)
        {
            noiseTimer = 1f / noiseFps;
            GenerateNoise(shownAlpha);
        }
    }

    private void UpdateTexts(bool inDanger, bool locked)
    {
        if (levelText == null || trendText == null) return;

        if (inDanger)
        {
            levelText.text = "STORING!";
            levelText.color = new Color(1f, 0.25f, 0.2f);
            trendText.text = "loop weg uit het storingsveld";
        }
        else if (locked)
        {
            levelText.text = "SIGNAAL GEVONDEN";
            levelText.color = new Color(0.4f, 1f, 0.5f);
            trendText.text = "tik op de mast of op AFSTEMMEN";
        }
        else if (tracker.Level == SignalLevel.Lost || double.IsInfinity(tracker.Distance))
        {
            levelText.text = "";
            trendText.text = "";
        }
        else
        {
            // Afstand tot het doel. Gewone radio's: afgerond op 10 m, Scanner X-9: tot op de meter.
            double d = tracker.Distance;
            string meters = showDistance ? $"{d:F0} m" : $"± {System.Math.Max(10.0, System.Math.Round(d / 10.0) * 10.0):F0} m";
            levelText.text = meters;
            levelText.color = Color.white;
            trendText.text = "tot het signaal";
        }
        trendText.color = Color.white;

        float pulse = (inDanger || locked) ? 1f + 0.05f * Mathf.Sin(Time.time * 10f) : 1f;
        levelText.rectTransform.localScale = Vector3.one * pulse;
    }

    // Analoge tv-sneeuw, met rollende band bij veel ruis.
    private void GenerateNoise(float intensity)
    {
        rollingBand = Mathf.Repeat(rollingBand + 0.015f, 1f);
        int bandY = (int)(rollingBand * noiseHeight);
        bool heavy = intensity > 0.5f;

        for (int y = 0; y < noiseHeight; y++)
        {
            int distToBand = Mathf.Abs(y - bandY);
            int band = distToBand < 10 ? (10 - distToBand) * 6 : 0;        // zachte lichte band die rolt
            int shift = heavy && (Next() % 100 < 4) ? (int)(Next() % 12) : 0; // af en toe een verschoven lijn

            for (int x = 0; x < noiseWidth; x++)
            {
                int v = (int)(Next() % 180) + 40 + band;
                if (shift > 0 && x < shift) v = 255;
                byte b = (byte)Mathf.Min(255, v);
                pixels[y * noiseWidth + x] = new Color32(b, b, b, 255);
            }
        }
        noiseTex.SetPixels32(pixels);
        noiseTex.Apply(false);
    }

    private uint Next()
    {
        rng ^= rng << 13;
        rng ^= rng >> 17;
        rng ^= rng << 5;
        return rng;
    }

    void OnDestroy()
    {
        if (noiseTex != null) Destroy(noiseTex);
    }
}
