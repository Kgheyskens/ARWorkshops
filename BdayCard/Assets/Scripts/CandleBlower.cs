using UnityEngine;
using UnityEngine.Events;

/// Luistert naar de microfoon en dooft de kaarsjes op de taart (via CandleRing) zodra
/// er geblazen wordt. Hoe langer je in één adem blijft blazen, hoe meer kaarsjes er na
/// elkaar doven — te beginnen bij het kaarsje dat nu "vooraan" staat (zie CakeRotator
/// om de taart te draaien en zo te kiezen welk kaarsje eerst aan de beurt is).


public class CandleBlower : MonoBehaviour
{
    [Header("Kaarsjes")]
    [Tooltip("De ring van 5 kaarsjes op de taart.")]
    public CandleRing candleRing;

    [Header("Microfoon")]
    [Tooltip("Naam van het microfoon-toestel. Leeg = standaardmicrofoon van het toestel.")]
    public string microphoneDevice = "";
    [Tooltip("Opnamefrequentie in Hz.")]
    public int sampleRate = 44100;
    [Tooltip("Hoeveel samples er per check geanalyseerd worden.")]
    public int sampleWindow = 128;

    [Header("Blaas-detectie")]
    [Tooltip("RMS-geluidsniveau waarboven het als 'blazen' telt. Test dit uit op je eigen toestel/microfoon.")]
    [Range(0f, 1f)]
    public float blowThreshold = 0.15f;
    [Tooltip("Hoeveel seconden ONONDERBROKEN blazen nodig zijn om telkens 1 extra kaarsje te doven. " +
             "Bv. 0.6 => na 0.6s dooft kaarsje 1, na 1.2s ook kaarsje 2, enz.")]
    public float secondsPerCandle = 0.6f;
    [Tooltip("Kleine tolerantie: hoe lang het geluidsniveau even mag terugvallen zonder dat je adem als 'gestopt' telt " +
             "(vangt korte inzinkingen in het microfoonsignaal op).")]
    public float dropoutGrace = 0.15f;

    [Header("Geluid (optioneel, mag leeg blijven)")]
    [Tooltip("Geluid dat afgespeeld wordt telkens er een kaarsje dooft. Er moet een AudioSource op dit " +
             "object staan (of eentje wordt er automatisch aan toegevoegd) om dit af te spelen.")]
    public AudioClip blowOutSound;

    [Header("Events")]
    [Tooltip("Wordt aangeroepen telkens één kaarsje dooft (handig voor een 'poef'-geluidje per kaarsje).")]
    public UnityEvent onCandleBlownOut;
    [Tooltip("Wordt aangeroepen zodra het allerlaatste kaarsje ook gedoofd is (bv. om een 'Happy Birthday'-animatie/confetti te starten).")]
    public UnityEvent onAllCandlesBlownOut;

    [Header("Debug (alleen om uit te lezen)")]
    [Tooltip("Live RMS-geluidsniveau van de microfoon, elke frame bijgewerkt. Kijk hiernaar in de Inspector " +
             "tijdens Play Mode terwijl je blaast, en zet 'Blow Threshold' hierboven net onder de piek die je ziet.")]
    public float currentMicLevel;
    [Tooltip("True zolang het huidige niveau boven de threshold zit (dus 'telt' als blazen).")]
    public bool isBlowingNow;
    [Tooltip("Naam van de microfoon die effectief gebruikt wordt (handig als je meerdere microfoons hebt).")]
    public string activeMicrophoneName;

    [Tooltip("Print het geluidsniveau een paar keer per seconde naar de Console. Zet dit aan om op je " +
             "telefoon te kunnen meekijken: bouw met 'Development Build' aangevinkt (File > Build Settings) " +
             "en laat het toestel via USB aangesloten — de Console in de Editor ontvangt dan live de logs " +
             "van de app op je telefoon, ook al zie je de Inspector daar niet.")]
    public bool logMicLevelToConsole = false;
    [Tooltip("Hoeveel keer per seconde er gelogd wordt (enkel als 'Log Mic Level To Console' aanstaat).")]
    public float logIntervalSeconds = 0.3f;

    AudioClip _micClip;
    string _activeDevice;
    bool _micRunning;

    AudioSource _sfxAudioSource;

    float _continuousBlowTime;
    float _dropoutTimer;
    int _blownThisBreath;
    float _lastLogTime;

    void Start()
    {
        if (candleRing == null)
            candleRing = GetComponentInChildren<CandleRing>();

        if (blowOutSound != null)
        {
            _sfxAudioSource = GetComponent<AudioSource>();
            if (_sfxAudioSource == null)
                _sfxAudioSource = gameObject.AddComponent<AudioSource>();
            _sfxAudioSource.playOnAwake = false;
        }

        StartMicrophone();
    }

    void StartMicrophone()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("CandleBlower: geen microfoon gevonden op dit toestel.");
            return;
        }

        _activeDevice = string.IsNullOrEmpty(microphoneDevice) ? Microphone.devices[0] : microphoneDevice;
        _micClip = Microphone.Start(_activeDevice, true, 1, sampleRate);
        _micRunning = true;
        activeMicrophoneName = _activeDevice;
    }

    void OnDestroy()
    {
        if (_micRunning)
            Microphone.End(_activeDevice);
    }

    void Update()
    {
        if (!_micRunning)
            return;

        // Niveau altijd bijwerken (ook zonder CandleRing), zodat je in de Inspector kan
        // zien of de microfoon zelf al werkt, los van de rest van de setup.
        float level = GetMicLevel();
        currentMicLevel = level;
        isBlowingNow = level >= blowThreshold;

        if (logMicLevelToConsole && Time.time - _lastLogTime >= logIntervalSeconds)
        {
            _lastLogTime = Time.time;
            Debug.Log($"[CandleBlower] niveau: {level:F3}  (threshold: {blowThreshold:F3})  blazen: {isBlowingNow}");
        }

        if (candleRing == null)
            return;

        if (candleRing.AllOut())
        {
            _continuousBlowTime = 0f;
            _blownThisBreath = 0;
            return;
        }

        if (level >= blowThreshold)
        {
            _dropoutTimer = 0f;
            _continuousBlowTime += Time.deltaTime;

            int shouldBeOutThisBreath = Mathf.FloorToInt(_continuousBlowTime / secondsPerCandle);
            ExtinguishUpTo(shouldBeOutThisBreath);
        }
        else
        {
            _dropoutTimer += Time.deltaTime;
            if (_dropoutTimer > dropoutGrace)
            {
                // Adem echt gestopt: volgende keer blazen begint terug bij 0.
                _continuousBlowTime = 0f;
                _blownThisBreath = 0;
            }
        }
    }

    /// <summary>Doof kaarsjes (in ring-volgorde vanaf het actieve kaarsje) tot er 'total' gedoofd zijn deze adem.</summary>
    void ExtinguishUpTo(int totalForThisBreath)
    {
        while (_blownThisBreath < totalForThisBreath)
        {
            var order = candleRing.GetBlowOrder();
            if (order.Count == 0)
                break; // alles is al uit

            order[0].Extinguish();
            _blownThisBreath++;

            if (blowOutSound != null && _sfxAudioSource != null)
                _sfxAudioSource.PlayOneShot(blowOutSound);

            onCandleBlownOut?.Invoke();

            if (candleRing.AllOut())
            {
                onAllCandlesBlownOut?.Invoke();
                break;
            }
        }
    }

    /// <summary>Leest het meest recente stukje microfoon-audio en geeft het RMS-geluidsniveau terug (ruwweg 0..1).</summary>
    float GetMicLevel()
    {
        int micPosition = Microphone.GetPosition(_activeDevice) - sampleWindow;
        if (micPosition < 0)
            return 0f;

        float[] samples = new float[sampleWindow];
        _micClip.GetData(samples, micPosition);

        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];

        return Mathf.Sqrt(sum / samples.Length);
    }

    /// <summary>Roep dit aan vanuit je shake/dubbeltik-reset-script (samen met candleRing.RelightAll()).</summary>
    public void ResetDetection()
    {
        _continuousBlowTime = 0f;
        _dropoutTimer = 0f;
        _blownThisBreath = 0;
    }
}