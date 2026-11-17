using UnityEngine;

//flickering van de kaarsjes
[DisallowMultipleComponent]
public class CandleFlicker : MonoBehaviour
{
    [Header("Referenties (leeg laten = automatisch zoeken)")]
    [Tooltip("Het Light-component dat de gloed van de vlam voorstelt. Leeg = zoekt eerst op dit object, dan in de children.")]
    public Light flameLight;

    [Tooltip("De Transform van de vlam-mesh zelf, voor de wiebel/schaal-animatie. Leeg = dit object.")]
    public Transform flameMesh;

    [Header("Licht-flikkering")]
    [Tooltip("Gemiddelde helderheid van het licht.")]
    public float baseIntensity = 2.5f;

    [Range(0f, 1f)]
    [Tooltip("Hoeveel de helderheid rond het gemiddelde schommelt (0 = geen flikkering, 1 = zeer sterk).")]
    public float intensityVariation = 0.35f;

    [Tooltip("Gemiddeld bereik (range) van het licht.")]
    public float baseRange = 1.5f;

    [Range(0f, 1f)]
    public float rangeVariation = 0.25f;

    [Tooltip("Hoe snel de flikkering beweegt. Hoger = nerveuzer, lager = rustiger.")]
    public float flickerSpeed = 3.5f;

    [Header("Vlam-wiebel (mesh)")]
    [Tooltip("Ook de vlam-mesh zelf lichtjes laten wiebelen/schalen, niet enkel het licht.")]
    public bool animateFlameMesh = true;

    [Range(0f, 0.5f)]
    public float scaleVariation = 0.12f;

    [Range(0f, 15f)]
    public float tiltDegrees = 6f;

    [Header("Status")]
    public bool isLit = true;

    Vector3 _baseScale;
    float _noiseOffsetA, _noiseOffsetB, _noiseOffsetC;

    void Awake()
    {
        if (flameLight == null) flameLight = GetComponent<Light>();
        if (flameLight == null) flameLight = GetComponentInChildren<Light>();
        if (flameMesh == null) flameMesh = transform;

        _baseScale = flameMesh.localScale;

        // Elke vlam krijgt zijn eigen "ritme", zodat meerdere kaarsjes (mocht je die
        // ooit toevoegen) niet allemaal perfect synchroon flikkeren.
        _noiseOffsetA = Random.Range(0f, 1000f);
        _noiseOffsetB = Random.Range(0f, 1000f);
        _noiseOffsetC = Random.Range(0f, 1000f);
    }

    void Update()
    {
        if (!isLit) return;

        float t = Time.time * flickerSpeed;

        // Perlin noise geeft een vloeiende, natuurlijke flikkering
        // (geen harde sprongen zoals je met puur Random.Range zou krijgen).
        float nA = Mathf.PerlinNoise(_noiseOffsetA, t) * 2f - 1f;        // -1..1
        float nB = Mathf.PerlinNoise(_noiseOffsetB, t * 0.7f) * 2f - 1f;
        float nC = Mathf.PerlinNoise(_noiseOffsetC, t * 1.3f) * 2f - 1f;

        if (flameLight != null)
        {
            flameLight.intensity = baseIntensity * (1f + nA * intensityVariation);
            flameLight.range = baseRange * (1f + nB * rangeVariation);
        }

        if (animateFlameMesh && flameMesh != null)
        {
            float heightFactor = 1f + nB * scaleVariation;
            flameMesh.localScale = new Vector3(
                _baseScale.x * (1f + nC * scaleVariation * 0.5f),
                _baseScale.y * heightFactor,
                _baseScale.z * (1f + nA * scaleVariation * 0.5f)
            );
            flameMesh.localRotation = Quaternion.Euler(nA * tiltDegrees, 0f, nC * tiltDegrees);
        }
    }

    /// <summary>Doof de vlam: licht + mesh uit. Roep dit aan vanuit je blaas-script.</summary>
    public void Extinguish()
    {
        isLit = false;
        if (flameLight != null) flameLight.enabled = false;
        if (flameMesh != null) flameMesh.gameObject.SetActive(false);
    }

    /// <summary>Steek de vlam opnieuw aan (bv. bij shake/dubbeltik-reset).</summary>
    public void Relight()
    {
        if (flameMesh != null) flameMesh.gameObject.SetActive(true);
        if (flameLight != null) flameLight.enabled = true;
        isLit = true;
    }
}