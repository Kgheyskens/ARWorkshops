using System.Collections;
using UnityEngine;
using TMPro;

//checkt of  ballonen en kaarsjes uitzijn --> indien ja: Happy bday en confetti komt erop --> niet gebonden aan AR kaart
public class BirthdayFinale : MonoBehaviour
{
    [Header("Wat er gecheckt wordt")]
    [Tooltip("Sleep hier de CandleRing van de taart in.")]
    public CandleRing candleRing;
    [Tooltip("Leeg laten = automatisch alle Balloon-componenten zoeken onder 'Balloons Root'.")]
    public Balloon[] balloons;
    [Tooltip("Parent-object om in te zoeken als 'Balloons' hierboven leeg is (sleep hier het 'Balloons'-GameObject in).")]
    public Transform balloonsRoot;

    [Header("Happy Birthday tekst (UI)")]
    [Tooltip("Een TextMeshPro - Text (UI) element op een Canvas (Screen Space - Overlay). " +
             "Zie de uitleg die erbij geleverd wordt voor hoe je dit opzet.")]
    public TextMeshProUGUI messageText;
    [TextArea]
    public string message = "Happy Birthday!";
    [Tooltip("Hoeveel seconden er tussen elke letter zit.")]
    public float letterInterval = 0.09f;
    [Tooltip("Kleuren die na elkaar over de letters verdeeld worden (spaties tellen niet mee, dus 'Happy' " +
             "krijgt gewoon 5 opeenvolgende kleuren uit deze lijst). Leeg laten = gewone tekstkleur van het " +
             "TextMeshPro-object zelf, geen regenboog-effect.")]
    public Color[] letterColors = new Color[]
    {
        new Color(0.95f, 0.25f, 0.35f), // rood
        new Color(1f, 0.65f, 0.2f),     // oranje
        new Color(1f, 0.85f, 0.2f),     // geel
        new Color(0.35f, 0.85f, 0.45f), // groen
        new Color(0.3f, 0.55f, 0.95f),  // blauw
        new Color(0.75f, 0.4f, 0.95f),  // paars
    };

    [Header("Confetti over het scherm")]
    [Tooltip("Eigen particle-effect (optioneel). Leeg laten = een schermvullende confetti-regen " +
             "wordt automatisch gegenereerd, geen asset nodig.")]
    public ParticleSystem customConfettiSystem;
    [Tooltip("Camera waar de confetti voor verschijnt. Leeg = Camera.main.")]
    public Camera referenceCamera;
    [Tooltip("Hoe ver de confetti-regen voor de camera hangt (in dezelfde eenheden als de rest van je scene).")]
    public float confettiDistance = 0.6f;
    [Tooltip("Hoeveel seconden de confetti blijft vallen.")]
    public float confettiDuration = 6f;

    [Header("Debug (optioneel, om op te sporen wat er nog ontbreekt)")]
    [Tooltip("Print naar de Console welke kaarsjes en/of ballonnen er nog 'openstaan' zolang de finale nog " +
             "niet is afgegaan. Zet dit aan als de finale niet lijkt te triggeren, en kijk in de Console " +
             "(in de Editor tijdens Play Mode, of via Development Build + USB op je telefoon) welk kaarsje " +
             "of welke ballon er nog niet goed voor staat.")]
    public bool logBlockingState = false;
    public float logIntervalSeconds = 1f;

    bool _finaleTriggered;
    GameObject _confettiGo;
    float _lastLogTime;

    static Shader _particleShader;
    static Mesh _quadMesh;

    void Start()
    {
        if ((balloons == null || balloons.Length == 0) && balloonsRoot != null)
            balloons = balloonsRoot.GetComponentsInChildren<Balloon>(true);

        if (messageText != null)
        {
            messageText.text = "";
            messageText.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (_finaleTriggered) return;

        bool candlesOut = candleRing != null && candleRing.AllOut();
        bool balloonsPopped = AllBalloonsPopped();

        if (logBlockingState && !(candlesOut && balloonsPopped) && Time.time - _lastLogTime >= logIntervalSeconds)
        {
            _lastLogTime = Time.time;
            LogBlockingState(candlesOut, balloonsPopped);
        }

        if (!candlesOut || !balloonsPopped) return;

        _finaleTriggered = true;
        StartCoroutine(PlayFinale());
    }

    /// <summary>Print exact welke kaarsjes nog branden en/of welke ballonnen nog niet gepopt zijn,
    /// zodat je niet moet raden wat er nog ontbreekt.</summary>
    void LogBlockingState(bool candlesOut, bool balloonsPopped)
    {
        if (candleRing == null)
        {
            Debug.Log("[BirthdayFinale] 'Candle Ring' is niet toegewezen.");
        }
        else if (!candlesOut)
        {
            string stillLit = "";
            if (candleRing.candles != null)
                foreach (var c in candleRing.candles)
                    if (c != null && c.isLit)
                        stillLit += (stillLit == "" ? "" : ", ") + c.name;
            Debug.Log($"[BirthdayFinale] Nog kaarsjes AAN: {(stillLit == "" ? "(geen naam te achterhalen, maar AllOut() geeft false)" : stillLit)}");
        }

        if (!balloonsPopped)
        {
            if (balloons == null || balloons.Length == 0)
            {
                Debug.Log("[BirthdayFinale] 'Balloons' staat leeg — er is niets om te checken, dus de finale kan nooit afgaan.");
            }
            else
            {
                string stillThere = "";
                foreach (var b in balloons)
                    if (b != null && !b.IsPopped)
                        stillThere += (stillThere == "" ? "" : ", ") + b.name;
                Debug.Log($"[BirthdayFinale] Nog NIET gepopte ballonnen: {(stillThere == "" ? "(geen naam te achterhalen)" : stillThere)}");
            }
        }
    }

    bool AllBalloonsPopped()
    {
        // Geen ballonnen gevonden/toegewezen = we weten het niet zeker, dus nog niet triggeren
        if (balloons == null || balloons.Length == 0) return false;

        foreach (var b in balloons)
            if (b != null && !b.IsPopped) return false;
        return true;
    }

    IEnumerator PlayFinale()
    {
        if (customConfettiSystem != null)
            customConfettiSystem.Play();
        else
            StartScreenConfetti();

        if (messageText != null)
        {
            messageText.gameObject.SetActive(true);
            messageText.text = "";

            // Diagnostiek: activeInHierarchy houdt ook rekening met (in)actieve parents (bv. de
            // Canvas zelf) — als dit 'False' print ondanks SetActive(true), staat er ergens een
            // parent-object uitgeschakeld en zal de tekst nooit zichtbaar worden, hoe correct de
            // rest ook is.
            Debug.Log($"[BirthdayFinale] Tekst-animatie gestart. actief in hierarchy: {messageText.gameObject.activeInHierarchy}, " +
                      $"canvas: {(messageText.canvas != null ? messageText.canvas.name : "GEEN CANVAS GEVONDEN")}, " +
                      $"canvas actief: {(messageText.canvas != null && messageText.canvas.gameObject.activeInHierarchy)}");

            string builtText = "";
            int colorIndex = 0;
            foreach (char c in message)
            {
                if (letterColors != null && letterColors.Length > 0 && !char.IsWhiteSpace(c))
                {
                    string hex = ColorUtility.ToHtmlStringRGB(letterColors[colorIndex % letterColors.Length]);
                    builtText += $"<color=#{hex}>{c}</color>";
                    colorIndex++;
                }
                else
                {
                    builtText += c;
                }

                messageText.text = builtText;
                yield return new WaitForSeconds(letterInterval);
            }

            Debug.Log($"[BirthdayFinale] Tekst-animatie klaar. Eindtekst: {builtText}");
        }
        else
        {
            Debug.Log("[BirthdayFinale] 'Message Text' is niet toegewezen — kan de tekst niet tonen.");
        }
    }

    //confetti regen
    void StartScreenConfetti()
    {
        Camera cam = referenceCamera != null ? referenceCamera : Camera.main;
        if (cam == null) return;

        _confettiGo = new GameObject("ScreenConfetti");
        _confettiGo.transform.SetParent(cam.transform, false);
        _confettiGo.transform.localPosition = new Vector3(0f, 0f, confettiDistance);
        _confettiGo.transform.localRotation = Quaternion.identity;

        ParticleSystem ps = _confettiGo.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // De box zo groot maken dat hij exact het volledige camerabeeld dekt op deze afstand,
        // ongeacht schermverhouding of scene-schaal.
        float height = 2f * confettiDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * cam.aspect;

        var main = ps.main;
        main.duration = confettiDuration;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startSize = new ParticleSystem.MinMaxCurve(width * 0.02f, width * 0.035f);
        main.startColor = new ParticleSystem.MinMaxGradient(BuildConfettiGradient());
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.35f; // traag laten dwarrelen, geen snelle val
        main.stopAction = ParticleSystemStopAction.None;
        main.maxParticles = 500;

        var emission = ps.emission;
        emission.rateOverTime = 45f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(width * 1.05f, height * 1.05f, 0.02f);

        var rotationOverLifetime = ps.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-200f * Mathf.Deg2Rad, 200f * Mathf.Deg2Rad);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = fade;

        var renderer = _confettiGo.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetQuadMesh();
        renderer.material = new Material(GetParticleShader());

        ps.Play();
        StartCoroutine(StopConfettiAfter(ps, confettiDuration));
    }

    IEnumerator StopConfettiAfter(ParticleSystem ps, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        // Object zelf pas opruimen nadat ook de laatst gespawnde deeltjes uitgedoofd zijn.
        Destroy(ps.gameObject, 3.5f);
    }

    static Gradient BuildConfettiGradient()
    {
        Gradient g = new Gradient();
        g.mode = GradientMode.Fixed;
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.95f, 0.25f, 0.35f), 0f),
                new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.2f),
                new GradientColorKey(new Color(0.25f, 0.55f, 0.95f), 0.4f),
                new GradientColorKey(new Color(0.35f, 0.85f, 0.45f), 0.6f),
                new GradientColorKey(new Color(0.95f, 0.45f, 0.85f), 0.8f),
                new GradientColorKey(new Color(1f, 0.6f, 0.2f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return g;
    }

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

    //alles resetten
    public void ResetFinale()
    {
        _finaleTriggered = false;
        StopAllCoroutines();

        if (messageText != null)
        {
            messageText.text = "";
            messageText.gameObject.SetActive(false);
        }

        if (_confettiGo != null)
        {
            Destroy(_confettiGo);
            _confettiGo = null;
        }
    }
}