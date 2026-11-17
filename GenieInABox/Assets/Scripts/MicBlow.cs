using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

// Meet blaaskracht via de microfoon (spatie in Editor).
public class MicBlow : MonoBehaviour
{
    [Tooltip("Hoger = sneller reageren op zacht blazen")]
    [Range(0.5f, 3f)] public float sensitivity = 1f;
    [Tooltip("Minimaal volume dat als blazen telt")]
    public float minThreshold = 0.03f;
    public int sampleRate = 16000;

    // Blaaskracht 0..1.
    public float Strength { get; private set; }
    // Ruw volume (RMS).
    public float Level { get; private set; }
    public bool IsBlowing => Strength > 0.08f;
    public bool HasMicrophone { get; private set; }
    public bool Listening => clip != null;

    private AudioClip clip;
    private string device;
    private readonly float[] buffer = new float[1024];
    private float baseline = 0.008f;

    public void StartListening()
    {
        if (clip == null) StartCoroutine(Begin());
    }

    public void StopListening()
    {
        if (clip == null) return;
        Microphone.End(device);
        clip = null;
    }

    private IEnumerator Begin()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            float waited = 0f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && waited < 20f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }
#endif
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[MicBlow] Geen microfoon gevonden - gebruik de spatiebalk om te blazen.");
            HasMicrophone = false;
            yield break;
        }
        device = Microphone.devices[0];
        clip = Microphone.Start(device, true, 1, sampleRate);
        float t = 0f;
        while (Microphone.GetPosition(device) <= 0 && t < 2f) { t += Time.unscaledDeltaTime; yield return null; }
        HasMicrophone = true;
    }

    private void Update()
    {
        float target = 0f;

        if (clip != null)
        {
            int pos = Microphone.GetPosition(device);
            int start = pos - buffer.Length;
            if (start < 0) start += clip.samples;
            clip.GetData(buffer, start); // leest door tot het begin van de (lussende) clip

            double sum = 0;
            for (int i = 0; i < buffer.Length; i++) sum += buffer[i] * buffer[i];
            float rms = Mathf.Sqrt((float)(sum / buffer.Length));
            Level = rms;

            float threshold = Mathf.Max(minThreshold, baseline * 3f) / sensitivity;
            // Achtergrondgeluid traag bijhouden, enkel als er NIET geblazen wordt
            if (rms < threshold) baseline = Mathf.Lerp(baseline, rms, Time.unscaledDeltaTime * 0.5f);
            target = Mathf.Clamp01((rms - threshold) / (threshold * 3f));
        }

        // Spatiebalk = blazen (testen in de Editor)
        if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) target = 1f;

        // Snel omhoog, iets trager terug (voelt natuurlijker)
        float speed = target > Strength ? 12f : 4f;
        Strength = Mathf.MoveTowards(Strength, target, Time.unscaledDeltaTime * speed);
    }

    private void OnDisable() => StopListening();
}
