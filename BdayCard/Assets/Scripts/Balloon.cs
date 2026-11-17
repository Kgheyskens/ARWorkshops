using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// colliders nodig op balon zodat een raycast gestuurd kan worden om hem te doen poppen
[RequireComponent(typeof(Collider))]
public class Balloon : MonoBehaviour
{
    [Header("Knal-animatie")]
    [Tooltip("Hoe lang de ballon opzwelt vlak voor hij verdwijnt, in seconden. Kort houden (bv. 0.08-0.15) " +
             "voelt het meest als een echte knal.")]
    public float popDuration = 0.12f;

    [Tooltip("Hoe groot de ballon wordt op het hoogtepunt, vlak voor hij verdwijnt (1 = geen opzwelling).")]
    public float popPunchScale = 1.35f;

    [Header("Eigen effect (optioneel)")]
    [Tooltip("Eigen particle-effect. Leeg laten = confetti + rubberscherven worden automatisch gegenereerd " +
             "(zie hieronder), geen asset nodig.")]
    public ParticleSystem popParticlePrefab;

    [Header("Confetti (automatisch, kleurrijk)")]
    [Tooltip("Aantal confetti-snippers.")]
    public int confettiCount = 26;
    [Tooltip("Hoe hard de confetti wegvliegt.")]
    public float confettiSpeed = 3.5f;
    [Tooltip("Grootte van de confetti-snippers.")]
    public float confettiSize = 0.06f;

    [Header("Rubberscherven (automatisch, kleur van de ballon zelf)")]
    [Tooltip("Aantal grotere scherven in de kleur van de ballon zelf — geeft het gevoel dat de ballon " +
             "zelf openspat, niet enkel confetti errond.")]
    public int shredCount = 9;
    [Tooltip("Hoe hard de scherven wegvliegen.")]
    public float shredSpeed = 2.2f;
    [Tooltip("Grootte van de scherven.")]
    public float shredSize = 0.13f;

    [Header("Geluid (optioneel, mag leeg blijven)")]
    [Tooltip("Geluid dat afgespeeld wordt bij het knallen. Er moet een AudioSource op dit object " +
             "staan (of eentje wordt er automatisch aan toegevoegd) om dit af te spelen.")]
    public AudioClip popSound;

    [Header("Na het knallen")]
    [Tooltip("Aan: de ballon wordt volledig verwijderd (Destroy) na het knallen. " +
             "Uit: de ballon wordt enkel uitgeschakeld (SetActive(false)), handig als je later " +
             "via een reset-script alle ballonnen wil terugzetten met ResetBalloon().")]
    public bool destroyOnPop = false;

    [Tooltip("Wordt aangeroepen op het moment dat deze ballon knapt (handig om bv. een score te verhogen).")]
    public UnityEvent onPopped;

    AudioSource _audioSource;
    Vector3 _baseScale;
    bool _popped;

    // True zodra deze ballon geknapt is (of aan het knappen is). --> count voor eindanimatie
    public bool IsPopped => _popped;

    static Shader _particleShader;
    static Mesh _quadMesh;

    void Awake()
    {
        _baseScale = transform.localScale;
        if (popSound != null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
    }

    // popping van deballon 
    public void Pop()
    {
        if (_popped) return;
        _popped = true;
        StartCoroutine(PopRoutine());
    }

    IEnumerator PopRoutine()
    {
        // collider uitzetten zodat je niet twee keer op dezelfde ballon kan tikken
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (popSound != null && _audioSource != null)
            _audioSource.PlayOneShot(popSound);

        if (popParticlePrefab != null)
        {
            Instantiate(popParticlePrefab, transform.position, transform.rotation);
        }
        else
        {
            SpawnConfettiBurst();
            SpawnRubberShredBurst();
        }

        onPopped?.Invoke();

        // Heel kort opzwellen en dan in één klap verdwijnen (geen ineenschrompeling) —
        // dat gecombineerd met de explosie hierboven geeft het gevoel van een echte knal.
        float t = 0f;
        while (t < popDuration)
        {
            t += Time.deltaTime;
            float f = popDuration > 0f ? t / popDuration : 1f;
            transform.localScale = Vector3.Lerp(_baseScale, _baseScale * popPunchScale, f);
            yield return null;
        }

        if (destroyOnPop)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    // kleur uitlezing van ballon om e scherven overeen te laten komen met zijn kleur
    Color GetBalloonColor()
    {
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null && rend.sharedMaterial != null && rend.sharedMaterial.HasProperty("_BaseColor"))
            return rend.sharedMaterial.GetColor("_BaseColor");
        if (rend != null && rend.sharedMaterial != null)
            return rend.sharedMaterial.color;
        return Color.white;
    }

    //confetti snippers bij de pop
    void SpawnConfettiBurst()
    {
        GameObject go = new GameObject("BalloonConfetti");
        go.transform.position = transform.position;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.6f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
        main.startSpeed = confettiSpeed;
        main.startSize = confettiSize;
        main.startColor = new ParticleSystem.MinMaxGradient(BuildConfettiGradient());
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.1f;
        main.stopAction = ParticleSystemStopAction.None;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)confettiCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.03f;

        // Laat elke snipper tuimelen tijdens het vallen, zoals echte confetti.
        var rotationOverLifetime = ps.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-540f * Mathf.Deg2Rad, 540f * Mathf.Deg2Rad);

        // Kleur blijft behouden (multiply met wit), enkel de alpha dooft uit op het einde.
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = fade;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetQuadMesh();
        renderer.material = new Material(GetParticleShader());

        ps.Play();
        Destroy(go, main.duration + 1.3f);
    }

    //gekleurde rubberen scherven voor de ballon
    void SpawnRubberShredBurst()
    {
        GameObject go = new GameObject("BalloonShreds");
        go.transform.position = transform.position;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Color color = GetBalloonColor();

        var main = ps.main;
        main.duration = 0.6f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = shredSpeed;
        main.startColor = color;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(shredSize * 0.5f, shredSize * 0.9f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(shredSize * 1.4f, shredSize * 2.2f);
        main.startSizeZ = new ParticleSystem.MinMaxCurve(shredSize * 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 2f; // rubber is zwaarder dan confetti, valt sneller/directer naar beneden
        main.stopAction = ParticleSystemStopAction.None;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)shredCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.02f;

        var rotationOverLifetime = ps.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-400f * Mathf.Deg2Rad, 400f * Mathf.Deg2Rad);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetQuadMesh();
        renderer.material = new Material(GetParticleShader());

        ps.Play();
        Destroy(go, main.duration + 1.2f);
    }

    //vaste kleur voor de snippers zodat het geen mix van kleur kan worden
    static Gradient BuildConfettiGradient()
    {
        Gradient g = new Gradient();
        g.mode = GradientMode.Fixed;
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.95f, 0.25f, 0.35f), 0f),   // rood
                new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.2f),     // geel
                new GradientColorKey(new Color(0.25f, 0.55f, 0.95f), 0.4f), // blauw
                new GradientColorKey(new Color(0.35f, 0.85f, 0.45f), 0.6f), // groen
                new GradientColorKey(new Color(0.95f, 0.45f, 0.85f), 0.8f), // roze
                new GradientColorKey(new Color(1f, 0.6f, 0.2f), 1f),        // oranje
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return g;
    }

    //vierkante mesh voor confetti
    static Mesh GetQuadMesh()
    {
        if (_quadMesh == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quadMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
        }
        return _quadMesh;
    }

    static Shader GetParticleShader()
    {
        if (_particleShader == null)
            _particleShader = Shader.Find("Sprites/Default");
        return _particleShader;
    }

    //reset van de ballon na een dubbele tik
    public void ResetBalloon()
    {
        _popped = false;
        transform.localScale = _baseScale;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
        gameObject.SetActive(true);
    }
}