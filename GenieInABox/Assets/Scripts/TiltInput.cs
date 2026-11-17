using UnityEngine;
using UnityEngine.InputSystem;

// Zwaartekracht in doos-ruimte voor het doolhof.
public class TiltInput : MonoBehaviour
{
    public enum Source { Auto, PhoneSensor, VuforiaWorld }

    public Source source = Source.Auto;
    [Tooltip("Als Vuforia de doos even niet echt ziet: laat de knikker dan reageren op het kantelen van je gsm")]
    public bool followPhoneWhenBoxLost = false;
    [Tooltip("Hoe snel de kanteling volgt (hoger = directer, lager = rustiger)")]
    public float responsiveness = 18f;
    [Tooltip("Graden kanteling per pijltjestoets (Editor)")]
    public float editorTiltSpeed = 25f;
    public float editorMaxTilt = 20f;

    [SerializeField, HideInInspector] private int tuningVersion = 0;
    private void ApplyTuning()
    {
        if (tuningVersion >= 2) return;
        followPhoneWhenBoxLost = false; responsiveness = 18f;
        tuningVersion = 2;
    }
    private void OnValidate() => ApplyTuning();
    private void Awake() => ApplyTuning();

    public bool UsingSensor { get; private set; }
    // Doos-pose bevroren?
    public bool BoxPoseStale { get; private set; }

    private Camera cam;
    private Vector2 editorTilt;
    private Vector3 smoothedLocal = Vector3.down;
    private Quaternion boxInCam = Quaternion.identity;
    private bool haveBoxInCam;
    private float staleSince = -1f;
    private int lastFrame = -1;
    private Vector3 lastLocal = Vector3.down;

    private void Start()
    {
        cam = Camera.main;
        InputSystem.settings.compensateForScreenOrientation = true;
        if (GravitySensor.current != null) InputSystem.EnableDevice(GravitySensor.current);
        if (Accelerometer.current != null) InputSystem.EnableDevice(Accelerometer.current);
    }

    // Zwaartekracht in wereld.
    public Vector3 WorldGravity()
    {
        if (cam == null) cam = Camera.main;
        Vector3 g = Vector3.down;
        UsingSensor = false;

        bool trySensor = source == Source.PhoneSensor || (source == Source.Auto && Application.isMobilePlatform);
        if (trySensor && cam != null)
        {
            Vector3 dev = Vector3.zero;
            if (GravitySensor.current != null && GravitySensor.current.enabled) dev = GravitySensor.current.gravity.ReadValue();
            else if (Accelerometer.current != null && Accelerometer.current.enabled) dev = Accelerometer.current.acceleration.ReadValue();

            if (dev.sqrMagnitude > 0.01f)
            {
                // Gsm-assen: x = rechts, y = boven, z = uit het scherm naar je toe.
                // Camera-assen: x = rechts, y = boven, z = de kijkrichting (= in het scherm). Dus z omdraaien.
                Vector3 camLocal = new Vector3(dev.x, dev.y, -dev.z);
                g = cam.transform.TransformDirection(camLocal).normalized;
                UsingSensor = true;
            }
        }
        return g;
    }

    // Zwaartekracht in doos-ruimte.
    public Vector3 LocalGravity(Transform box)
    {
        if (Time.frameCount == lastFrame) return lastLocal; // één keer per frame rekenen
        lastFrame = Time.frameCount;
        if (cam == null) cam = Camera.main;

        var rig = box.GetComponent<GenieBoxRig>();
        bool live = rig == null || rig.HasLivePose;
        Quaternion boxRot = box.rotation;

        if (live || cam == null)
        {
            // Goede pose: onthouden hoe de doos t.o.v. de gsm stond
            if (cam != null) { boxInCam = Quaternion.Inverse(cam.transform.rotation) * box.rotation; haveBoxInCam = true; }
            staleSince = -1f;
            BoxPoseStale = false;
        }
        else
        {
            if (staleSince < 0f) staleSince = Time.unscaledTime;
            // heel kort wegvallen (een flikkering) negeren we; daarna sturen met de gsm
            bool useFallback = followPhoneWhenBoxLost && haveBoxInCam && Time.unscaledTime - staleSince > 0.15f;
            if (useFallback) boxRot = cam.transform.rotation * boxInCam;
            BoxPoseStale = Time.unscaledTime - staleSince > 0.6f;
        }

        Vector3 local = (Quaternion.Inverse(boxRot) * WorldGravity()).normalized;
        float k = 1f - Mathf.Exp(-responsiveness * Mathf.Max(Time.unscaledDeltaTime, 0.001f));
        smoothedLocal = Vector3.Slerp(smoothedLocal, local, k).normalized;

        Vector3 result = smoothedLocal;
        if (editorTilt.sqrMagnitude > 0.0001f)
            result = Quaternion.Euler(-editorTilt.y, 0f, editorTilt.x) * result; // rechts = knikker naar +X, boven = naar +Z
        lastLocal = result;
        return result;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        Vector2 input = Vector2.zero;
        if (kb.leftArrowKey.isPressed) input.x -= 1f;
        if (kb.rightArrowKey.isPressed) input.x += 1f;
        if (kb.upArrowKey.isPressed) input.y += 1f;
        if (kb.downArrowKey.isPressed) input.y -= 1f;
        if (input != Vector2.zero)
            editorTilt = Vector2.ClampMagnitude(editorTilt + input * editorTiltSpeed * Time.unscaledDeltaTime, editorMaxTilt);
        else
            editorTilt = Vector2.MoveTowards(editorTilt, Vector2.zero, editorTiltSpeed * 0.5f * Time.unscaledDeltaTime);
    }
}
