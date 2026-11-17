using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

//Reset alle objecten en animaties naar 0 bij een dubbel click
public class ResetTrigger : MonoBehaviour
{
    [Header("Wat er gereset moet worden")]
    [Tooltip("Sleep hier de CandleRing van de taart in.")]
    public CandleRing candleRing;
    [Tooltip("Sleep hier de CandleBlower in.")]
    public CandleBlower candleBlower;
    [Tooltip("Leeg laten = automatisch alle Balloon-componenten zoeken onder 'Balloons Root' hieronder.")]
    public Balloon[] balloons;
    [Tooltip("Parent-object om in te zoeken als 'Balloons' hierboven leeg is (bv. sleep hier het 'Balloons'-GameObject in).")]
    public Transform balloonsRoot;
    [Tooltip("Optioneel: sleep hier BirthdayFinale in, zodat een reset ook de 'Happy Birthday'-tekst en confetti " +
             "wegneemt en de eindanimatie opnieuw kan afgaan.")]
    public BirthdayFinale birthdayFinale;

    [Header("Schudden")]
    public bool enableShake = true;
    [Tooltip("Hoe hard er geschud moet worden (in g's, versnelling incl. zwaartekracht). Rustig vasthouden geeft " +
             "ongeveer 1.0; test op je eigen toestel uit hoeveel een normale 'schud'-beweging geeft en zet de " +
             "waarde er net onder.")]
    public float shakeThreshold = 2.5f;
    [Tooltip("Hoeveel seconden na een geldige shake genegeerd wordt, zodat 1 schudbeweging niet meteen " +
             "meerdere keren telt.")]
    public float shakeCooldown = 1.5f;

    [Header("Dubbeltikken")]
    public bool enableDoubleTap = true;
    [Tooltip("Hoeveel seconden er maximaal tussen de twee tikken mag zitten om als dubbeltik te tellen.")]
    public float doubleTapMaxInterval = 0.35f;

    [Header("Events")]
    [Tooltip("Wordt aangeroepen telkens er gereset wordt (handig voor een extra geluidje/animatie).")]
    public UnityEvent onReset;

    float _shakeCooldownTimer;
    float _lastTapTime = -10f;

    void OnEnable()
    {
        if (Accelerometer.current != null)
            InputSystem.EnableDevice(Accelerometer.current);
    }

    void OnDisable()
    {
        if (Accelerometer.current != null)
            InputSystem.DisableDevice(Accelerometer.current);
    }

    void Start()
    {
        if ((balloons == null || balloons.Length == 0) && balloonsRoot != null)
            balloons = balloonsRoot.GetComponentsInChildren<Balloon>(true);
    }

    void Update()
    {
        if (_shakeCooldownTimer > 0f)
            _shakeCooldownTimer -= Time.deltaTime;

        if (enableShake) CheckShake();
        if (enableDoubleTap) CheckDoubleTap();
    }

    void CheckShake()
    {
        if (Accelerometer.current == null || _shakeCooldownTimer > 0f) return;

        Vector3 acc = Accelerometer.current.acceleration.ReadValue();
        if (acc.sqrMagnitude > shakeThreshold * shakeThreshold)
        {
            _shakeCooldownTimer = shakeCooldown;
            ResetAll();
        }
    }

    void CheckDoubleTap()
    {
        bool tapped = (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                   || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
        if (!tapped) return;

        float now = Time.time;
        if (now - _lastTapTime <= doubleTapMaxInterval)
        {
            _lastTapTime = -10f; // voorkomt dat een 3e snelle tik meteen weer als nieuwe dubbeltik telt
            ResetAll();
        }
        else
        {
            _lastTapTime = now;
        }
    }

    /// <summary>Zet alles terug: kaarsjes aan, blaas-detectie gereset, ballonnen heel.
    /// Kan ook los aangeroepen worden, bv. vanuit een UI-knop.</summary>
    public void ResetAll()
    {
        if (candleRing != null) candleRing.RelightAll();
        if (candleBlower != null) candleBlower.ResetDetection();

        if (balloons != null)
            foreach (var b in balloons)
                if (b != null) b.ResetBalloon();

        if (birthdayFinale != null) birthdayFinale.ResetFinale();

        onReset?.Invoke();
    }
}