using System;
using UnityEngine;

// Alle geluid in code: piepjes, radioruis en effecten.
public class SignalAudio : MonoBehaviour
{
    [Header("Piep")]
    public float farInterval = 2.0f;
    public float nearInterval = 0.12f;
    public float farPitch = 0.8f;
    public float nearPitch = 1.8f;
    [Range(0f, 1f)] public float beepVolume = 0.6f;

    [Header("Ruis")]
    [Range(0f, 1f)] public float maxStaticVolume = 0.35f;
    [Range(0f, 1f)] public float minStaticVolume = 0.02f;
    [Range(0f, 1f)] public float dangerStaticVolume = 0.6f;

    // Bij elke piep.
    public event Action Beeped;

    private SignalTracker tracker;
    private DangerZoneManager zones;
    private WaypointManager waypoints;
    private AudioSource beepSource, staticSource, fxSource, musicSource;
    private GameManager game;

    [Header("Menumuziek")]
    [Range(0f, 1f)] public float musicVolume = 0.55f;
    private AudioClip beepClip, staticClip, lockedClip, alarmClip, errorClip, successClip, clickClip, winClip, failClip;
    private float beepTimer;

    private const int SampleRate = 44100;

    void Awake()
    {
        beepSource = gameObject.AddComponent<AudioSource>();
        beepSource.playOnAwake = false;

        staticSource = gameObject.AddComponent<AudioSource>();
        staticSource.playOnAwake = false;
        staticSource.loop = true;
        staticSource.volume = 0f;

        fxSource = gameObject.AddComponent<AudioSource>();
        fxSource.playOnAwake = false;

        // Thema-muziekje voor het startscherm (in code gegenereerd, zie MenuMusic)
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = 0f;
        musicSource.clip = MenuMusic.Create();
        musicSource.Play();

        beepClip = Notes("Beep", new[] { 880f }, 0.09f, 25f, 0.6f, false);
        staticClip = CreateStatic();
        lockedClip = Notes("Locked", new[] { 660f, 880f, 1320f }, 0.14f, 6f, 0.5f, false);
        alarmClip = Notes("Alarm", new[] { 440f, 330f, 440f, 330f }, 0.15f, 2f, 0.25f, true);
        errorClip = Notes("Error", new[] { 200f, 150f }, 0.15f, 4f, 0.3f, true);
        successClip = Notes("Success", new[] { 880f, 1320f }, 0.1f, 8f, 0.5f, false);
        clickClip = Notes("Click", new[] { 1500f }, 0.03f, 60f, 0.3f, false);
        winClip = Notes("Win", new[] { 523f, 659f, 784f, 1047f }, 0.18f, 4f, 0.5f, false);
        failClip = Notes("Fail", new[] { 392f, 330f, 262f, 196f }, 0.25f, 3f, 0.5f, false);

        staticSource.clip = staticClip;
        staticSource.Play();
    }

    void Start()
    {
        tracker = FindFirstObjectByType<SignalTracker>();
        zones = FindFirstObjectByType<DangerZoneManager>();
        waypoints = FindFirstObjectByType<WaypointManager>();
        if (tracker != null) tracker.LevelChanged += OnLevelChanged;
        if (waypoints != null) waypoints.WaypointReached += OnWaypointReached;
    }

    void OnDestroy()
    {
        if (tracker != null) tracker.LevelChanged -= OnLevelChanged;
        if (waypoints != null) waypoints.WaypointReached -= OnWaypointReached;
    }

    // Eigen piepgeluid per radio.
    public void ApplyRadio(RadioDefinition radio)
    {
        if (radio == null) return;
        beepClip = Notes("Beep", new[] { radio.beepFrequency }, 0.09f, 25f, 0.6f, false);
    }

    void Update()
    {
        UpdateMusic();
        if (tracker == null) return;

        bool inDanger = zones != null && zones.IsInside;
        bool active = tracker.Level != SignalLevel.Lost;
        float s = tracker.Strength;

        // --- Ruis ---
        float targetStatic;
        if (inDanger) targetStatic = dangerStaticVolume;
        else if (!active || tracker.Level == SignalLevel.Locked) targetStatic = 0f;
        else targetStatic = Mathf.Lerp(maxStaticVolume, minStaticVolume, s);

        staticSource.pitch = inDanger ? 0.6f : 1f; // lager en dreigender in een storingsveld
        staticSource.volume = Mathf.MoveTowards(staticSource.volume, targetStatic, Time.deltaTime);

        // --- Piepjes (niet in een storingsveld: daar ben je je signaal kwijt) ---
        if (!active || inDanger || tracker.Level == SignalLevel.Locked) return;

        beepTimer -= Time.deltaTime;
        if (beepTimer <= 0f)
        {
            beepSource.pitch = Mathf.Lerp(farPitch, nearPitch, s);
            beepSource.PlayOneShot(beepClip, beepVolume);
            Beeped?.Invoke();
            beepTimer = farInterval * Mathf.Pow(nearInterval / farInterval, s); // exponentieel = natuurlijker
        }
    }

    // Menumuziek; zacht weg tijdens het zoeken.
    private void UpdateMusic()
    {
        if (musicSource == null) return;
        if (game == null) game = FindFirstObjectByType<GameManager>();
        bool inMenus = game == null || game.State == GameState.Menu || game.State == GameState.Loading ||
                       game.State == GameState.Intro || game.State == GameState.Ended;
        float target = inMenus ? musicVolume : 0f;
        musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, Time.unscaledDeltaTime * 0.5f);
    }

    private void OnWaypointReached(int index)
    {
        fxSource.PlayOneShot(lockedClip, 0.8f);
        Haptics.Vibrate();
    }

    private void OnLevelChanged(SignalLevel oldLevel, SignalLevel newLevel)
    {
        if (newLevel == SignalLevel.Hot && oldLevel < SignalLevel.Hot && oldLevel != SignalLevel.Lost)
            Haptics.Vibrate();
    }

    public void PlayAlarm() => fxSource.PlayOneShot(alarmClip);
    public void PlayError() => fxSource.PlayOneShot(errorClip);
    public void PlaySuccess() => fxSource.PlayOneShot(successClip);
    public void PlayClick() => fxSource.PlayOneShot(clickClip, 0.6f);
    public void PlayWin() => fxSource.PlayOneShot(winClip);
    public void PlayFail() => fxSource.PlayOneShot(failClip);

    // ------------------------------------------------------- Geluid genereren

    // Reeks tonen met korte aanslag en uitsterven.
    private static AudioClip Notes(string name, float[] freqs, float noteLength, float decay, float volume, bool square)
    {
        int perNote = (int)(SampleRate * noteLength);
        float[] data = new float[perNote * freqs.Length + SampleRate / 10];

        for (int k = 0; k < freqs.Length; k++)
        {
            for (int i = 0; i < perNote; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * decay);
                float phase = 2f * Mathf.PI * freqs[k] * t;
                float wave = square ? Mathf.Sign(Mathf.Sin(phase)) * 0.5f
                                    : Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 3f);
                data[k * perNote + i] += wave * env * volume;
            }
        }

        AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CreateStatic()
    {
        int n = SampleRate * 2;
        float[] data = new float[n];
        var rnd = new System.Random(42);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += 0.35f * (white - lp); // low-pass → krakend i.p.v. scherp sissen
            float crackle = rnd.NextDouble() < 0.0008 ? (float)(rnd.NextDouble() * 2.0 - 1.0) : 0f;
            data[i] = Mathf.Clamp(lp * 0.8f + crackle, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("Static", n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
