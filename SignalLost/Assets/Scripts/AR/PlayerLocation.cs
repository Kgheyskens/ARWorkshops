using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

// Gesmoothe positie + kijkrichting: GPS/kompas op gsm, WASD-simulatie in de Editor.
public class PlayerLocation : MonoBehaviour
{
    public enum LocationError { None = 0, NotEnabled = 1, Timeout = 2, Failed = 3, PermissionDenied = 4 }

    [Header("Simulatie (enkel in de Editor)")]
    public bool simulateInEditor = true;
    public double simStartLatitude = 51.164076;
    public double simStartLongitude = 4.961670;
    public float walkSpeed = 1.4f;   // m/s
    public float runSpeed = 6f;      // m/s (Shift)
    public float turnSpeed = 90f;    // graden/s (Q/E)

    [Header("GPS")]
    public float desiredAccuracyMeters = 3f;
    public float updateDistanceMeters = 0.5f;
    [Tooltip("Metingen met een slechtere nauwkeurigheid (m) worden genegeerd")]
    public float maxAcceptedAccuracy = 35f;
    [Tooltip("Hoe zwaar een nieuwe meting doorweegt (0 = nooit, 1 = meteen overnemen)")]
    [Range(0.05f, 1f)] public float positionSmoothing = 0.5f;
    [Tooltip("Hoe snel de kompasrichting volgt (hoger = sneller, maar zenuwachtiger)")]
    [Range(1f, 20f)] public float headingSmoothing = 6f;

    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double StartLatitude { get; private set; }
    public double StartLongitude { get; private set; }
    public float Heading { get; private set; }       // 0 = noord, gesmoothed
    public float StartHeading { get; private set; }
    public float Accuracy { get; private set; }      // meter
    public bool IsReady { get; private set; }
    public bool IsSimulated { get; private set; }
    public LocationError Error { get; private set; }

    private double lastTimestamp;

    IEnumerator Start()
    {
#if UNITY_EDITOR
        if (simulateInEditor)
        {
            StartSimulation();
            yield break;
        }
#endif

#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
            float waitedForPermission = 0f;
            while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) && waitedForPermission < 15f)
            {
                waitedForPermission += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Error = LocationError.PermissionDenied;
                yield break;
            }
        }
#endif

        if (!Input.location.isEnabledByUser)
        {
            Error = LocationError.NotEnabled;
            yield break;
        }

        EnableCompass();
        Input.location.Start(desiredAccuracyMeters, updateDistanceMeters);

        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }
        if (maxWait <= 0) { Error = LocationError.Timeout; yield break; }
        if (Input.location.status == LocationServiceStatus.Failed) { Error = LocationError.Failed; yield break; }

        // Wacht (max 10 s) op een eerste fatsoenlijke meting i.p.v. blind 2 s te wachten
        float waited = 0f;
        while (Input.location.lastData.horizontalAccuracy > maxAcceptedAccuracy && waited < 10f)
        {
            yield return new WaitForSeconds(0.5f);
            waited += 0.5f;
        }

        LocationInfo d = Input.location.lastData;
        Latitude = d.latitude;
        Longitude = d.longitude;
        Accuracy = d.horizontalAccuracy;
        lastTimestamp = d.timestamp;

        yield return new WaitForSeconds(1f); // kompas even laten stabiliseren
        Heading = ReadCompassHeading();

        StartLatitude = Latitude;
        StartLongitude = Longitude;
        StartHeading = Heading;
        IsReady = true;
    }

    void Update()
    {
        if (!IsReady) return;

        if (IsSimulated)
        {
            UpdateSimulation();
            return;
        }

        // --- Positie: enkel verwerken als er echt een nieuwe meting is ---
        LocationInfo d = Input.location.lastData;
        if (d.timestamp != lastTimestamp)
        {
            lastTimestamp = d.timestamp;

            if (d.horizontalAccuracy <= maxAcceptedAccuracy)
            {
                // Nauwkeurige meting telt zwaarder door dan een vage meting
                float trust = Mathf.Clamp(5f / Mathf.Max(d.horizontalAccuracy, 0.1f), 0.2f, 1f);
                double w = positionSmoothing * trust;

                Latitude += (d.latitude - Latitude) * w;
                Longitude += (d.longitude - Longitude) * w;
                Accuracy = d.horizontalAccuracy;
            }
        }

        // --- Richting: kompas smoothen (LerpAngle gaat correct om met 359° -> 0°) ---
        float t = 1f - Mathf.Exp(-headingSmoothing * Time.deltaTime);
        Heading = Mathf.Repeat(Mathf.LerpAngle(Heading, ReadCompassHeading(), t), 360f);
    }

    // ---------------------------------------------------------------- Kompas

    private bool legacyCompassAvailable = true;

    // Kompas aanzetten, met AttitudeSensor als fallback.
    private void EnableCompass()
    {
        try { Input.compass.enabled = true; }
        catch (System.InvalidOperationException) { legacyCompassAvailable = false; }
#if ENABLE_INPUT_SYSTEM
        var attitude = UnityEngine.InputSystem.AttitudeSensor.current;
        if (attitude != null && !attitude.enabled) UnityEngine.InputSystem.InputSystem.EnableDevice(attitude);
#endif
    }

    // Kompasrichting in graden (0 = noord).
    private float ReadCompassHeading()
    {
        if (legacyCompassAvailable)
        {
            try { return Input.compass.trueHeading; }
            catch (System.InvalidOperationException) { legacyCompassAvailable = false; }
        }
#if ENABLE_INPUT_SYSTEM
        // Fallback: oriëntatie van de gsm (rotatievector, t.o.v. het noorden) omzetten naar een kijkrichting
        var attitude = UnityEngine.InputSystem.AttitudeSensor.current;
        if (attitude != null)
        {
            Quaternion q = attitude.attitude.ReadValue();
            Quaternion device = Quaternion.Euler(90f, 0f, 0f) * new Quaternion(q.x, q.y, -q.z, -q.w);
            return Mathf.Repeat(device.eulerAngles.y, 360f);
        }
#endif
        return Heading;
    }

    void OnDisable()
    {
        if (!IsSimulated && Input.location.status == LocationServiceStatus.Running)
            Input.location.Stop();
    }

    // ---------------------------------------------------------------- Simulatie

    private void StartSimulation()
    {
        IsSimulated = true;
        Latitude = StartLatitude = simStartLatitude;
        Longitude = StartLongitude = simStartLongitude;
        Heading = StartHeading = 0f;
        Accuracy = 1f;
        IsReady = true;
        Debug.Log("[PlayerLocation] Simulatiemodus: WASD = wandelen, Shift = lopen, Q/E = draaien");
    }

    private void UpdateSimulation()
    {
        float forward = 0f, strafe = 0f, turn = 0f;
        bool run = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) forward += 1f;
            if (kb.sKey.isPressed) forward -= 1f;
            if (kb.dKey.isPressed) strafe += 1f;
            if (kb.aKey.isPressed) strafe -= 1f;
            if (kb.eKey.isPressed) turn += 1f;
            if (kb.qKey.isPressed) turn -= 1f;
            run = kb.leftShiftKey.isPressed;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.W)) forward += 1f;
        if (Input.GetKey(KeyCode.S)) forward -= 1f;
        if (Input.GetKey(KeyCode.D)) strafe += 1f;
        if (Input.GetKey(KeyCode.A)) strafe -= 1f;
        if (Input.GetKey(KeyCode.E)) turn += 1f;
        if (Input.GetKey(KeyCode.Q)) turn -= 1f;
        run = Input.GetKey(KeyCode.LeftShift);
#endif

        Heading = Mathf.Repeat(Heading + turn * turnSpeed * Time.deltaTime, 360f);

        float step = (run ? runSpeed : walkSpeed) * Time.deltaTime;
        float h = Heading * Mathf.Deg2Rad;
        // vooruit = (oost sin h, noord cos h), rechts = (oost cos h, noord -sin h)
        double north = (forward * Mathf.Cos(h) - strafe * Mathf.Sin(h)) * step;
        double east = (forward * Mathf.Sin(h) + strafe * Mathf.Cos(h)) * step;

        if (north != 0 || east != 0)
        {
            GeoUtils.OffsetByMeters(Latitude, Longitude, north, east, out double lat, out double lon);
            Latitude = lat;
            Longitude = lon;
        }
    }
}
