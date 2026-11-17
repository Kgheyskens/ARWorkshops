using System.Collections.Generic;
using UnityEngine;

// Alle geluiden en muziek, in code gegenereerd.
public class SoundFx : MonoBehaviour
{
    private const int Rate = 22050;

    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.45f;
    public bool muted;

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private readonly List<AudioSource> pool = new List<AudioSource>();
    private AudioSource menuMusic, gameMusic;
    private float musicTarget;
    private bool gameTrack;
    private System.Random rnd = new System.Random(3);

    // Hijaz op D (Hz), twee octaven (voor tinkels en effecten)
    private static readonly float[] Hijaz = { 293.66f, 311.13f, 369.99f, 392.00f, 440.00f, 466.16f, 523.25f, 587.33f, 622.25f, 739.99f, 783.99f };
    // D-mineur pentatonisch (D F G A C), rustig en zonder spanning: voor de muziek
    private static readonly float[] Penta = { 146.83f, 174.61f, 196.00f, 220.00f, 261.63f, 293.66f, 349.23f, 392.00f, 440.00f, 523.25f, 587.33f, 698.46f, 783.99f, 880.00f };

    private void Awake()
    {
        Build();
        menuMusic = MakeSource("MenuMusic", true);
        menuMusic.clip = CalmMusic(false);
        menuMusic.volume = 0f;
        menuMusic.Play();
        gameMusic = MakeSource("GameMusic", true);
        gameMusic.clip = CalmMusic(true);
        gameMusic.volume = 0f;
        gameMusic.Play();
        for (int i = 0; i < 10; i++) pool.Add(MakeSource("Fx" + i, false));
    }

    private void Update()
    {
        float m = muted ? 0f : masterVolume;
        float speed = Time.unscaledDeltaTime * 0.35f; // traag in elkaar overvloeien
        menuMusic.volume = Mathf.MoveTowards(menuMusic.volume, gameTrack ? 0f : musicTarget * musicVolume * m, speed);
        gameMusic.volume = Mathf.MoveTowards(gameMusic.volume, gameTrack ? musicTarget * musicVolume * m : 0f, speed);
    }

    // ------------------------------------------------------------------ Publieke API

    // Muziekvolume (0..1).
    public void SetMusic(float level) => musicTarget = level;

    // true = ambient, false = menumuziek.
    public void UseGameMusic(bool game) => gameTrack = game;

    public void Play(string name, float volume = 1f, float pitch = 1f)
    {
        if (muted || !clips.TryGetValue(name, out AudioClip clip)) return;
        AudioSource src = pool.Find(s => !s.isPlaying) ?? pool[0];
        src.clip = clip;
        src.volume = volume * masterVolume * 0.8f;
        src.pitch = pitch;
        src.Play();
    }

    // Willekeurige tinkel.
    public void PlayChime(float volume = 0.7f)
    {
        Play("chime" + rnd.Next(0, 5), volume * 0.7f, 1f);
    }

    // Vroeger: blaas-, zoem- en rolgeluid. Bewust stil gemaakt (rustiger spelen);
    // de methodes blijven bestaan zodat de andere scripts niets hoeven te weten.
    public void SetWind(float strength) { }
    public void SetHum(float level, float closeness) { }
    public void SetRoll(float speed) { }
    public void StopLoops() { }

    // ------------------------------------------------------------------ Bronnen

    private AudioSource MakeSource(string n, bool loop)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        return s;
    }

    // ------------------------------------------------------------------ Klankgenerators

    private void Build()
    {
        // Tinkels (5 tonen uit de toonladder)
        int[] chimeNotes = { 4, 6, 7, 9, 10 };
        for (int i = 0; i < 5; i++) clips["chime" + i] = Bell(Hijaz[chimeNotes[i]] * 2f, 1.2f, 0.5f);

        clips["pling"] = Bell(1760f, 0.9f, 0.6f, 2.76f);
        clips["sparkle"] = Sparkle();
        clips["assemble"] = Assemble();
        clips["click"] = Clicks();
        clips["creak"] = Assemble(); // i.p.v. een krakend luik: een zacht magisch akkoord
        clips["whoosh"] = Whoosh(0.6f, false);
        clips["fall"] = Fall();
        clips["bump"] = SoftTap(330f, 0.25f);
        clips["gate"] = Rumble(0.9f, 0.6f);
        clips["rumble"] = Rumble(1.6f, 0.45f);
        clips["poof"] = Poof();
        clips["fanfare"] = Fanfare();
        clips["whistle"] = Whoosh(0.9f, true);
        clips["bang"] = Bang();
        clips["crackle"] = CrackleClip();
        clips["talk"] = Talk();
        clips["deny"] = SoftTap(196f, 0.4f);
        clips["unlock"] = Unlock();
    }

    private AudioClip Make(string name, float[] data)
    {
        // Zachte limiter zodat niets kraakt
        for (int i = 0; i < data.Length; i++) data[i] = (float)System.Math.Tanh(data[i] * 1.2f) * 0.9f;
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private float Rand() => (float)rnd.NextDouble() * 2f - 1f;

    private AudioClip Bell(float f, float len, float vol, float ratio = 2.01f)
    {
        int n = (int)(len * Rate);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 4f);
            d[i] = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * f * ratio * t) * Mathf.Exp(-t * 6f)
                    + 0.15f * Mathf.Sin(2f * Mathf.PI * f * 3.9f * t) * Mathf.Exp(-t * 10f)) * env * vol;
        }
        return Make("bell", d);
    }

    private AudioClip Sparkle()
    {
        int n = (int)(0.7f * Rate);
        var d = new float[n];
        for (int k = 0; k < 6; k++)
        {
            float f = Hijaz[4 + k % 6] * 4f, start = k * 0.07f;
            for (int i = (int)(start * Rate); i < n; i++)
            {
                float t = (float)i / Rate - start;
                d[i] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 9f) * 0.18f;
            }
        }
        return Make("sparkle", d);
    }

    private AudioClip Assemble()
    {
        int n = (int)(2.2f * Rate);
        var d = new float[n];
        int[] seq = { 0, 2, 3, 4, 6, 7, 9, 10 };
        for (int k = 0; k < seq.Length; k++)
        {
            float f = Hijaz[seq[k]] * 2f, start = k * 0.09f;
            for (int i = (int)(start * Rate); i < n; i++)
            {
                float t = (float)i / Rate - start;
                d[i] += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * f * t)) * Mathf.Exp(-t * 2.5f) * 0.14f;
            }
        }
        // glinsterende "swoosh" eronder
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            lp += 0.1f * (Rand() - lp);
            d[i] += lp * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.2f)) * 0.25f;
        }
        return Make("assemble", d);
    }

    private AudioClip Clicks()
    {
        int n = (int)(0.6f * Rate);
        var d = new float[n];
        float[] at = { 0f, 0.22f, 0.32f };
        foreach (float s in at)
            for (int i = (int)(s * Rate); i < Mathf.Min(n, (int)((s + 0.06f) * Rate)); i++)
            {
                float t = (float)i / Rate - s;
                d[i] += (Rand() * 0.6f + Mathf.Sin(2f * Mathf.PI * 2400f * t)) * Mathf.Exp(-t * 90f) * 0.8f;
            }
        return Make("click", d);
    }

    private AudioClip Creak()
    {
        int n = (int)(1.4f * Rate);
        var d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float f = 95f + 40f * Mathf.Sin(t * 3f) + 12f * Mathf.Sin(t * 37f);
            phase += f / Rate;
            float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
            float env = Mathf.Clamp01(t / 0.1f) * Mathf.Clamp01((1.1f - t) / 0.3f);
            d[i] = saw * env * 0.25f * (0.6f + 0.4f * Mathf.Sin(t * 60f));
            if (t > 1.1f) { float tt = t - 1.1f; d[i] += Mathf.Sin(2f * Mathf.PI * 70f * tt) * Mathf.Exp(-tt * 12f) * 0.9f; }
        }
        return Make("creak", d);
    }

    private AudioClip Whoosh(float len, bool whistle)
    {
        int n = (int)(len * Rate);
        var d = new float[n];
        float lp = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate, x = t / len;
            float cut = whistle ? 0.15f : 0.03f + 0.25f * Mathf.Sin(Mathf.PI * x);
            lp += cut * (Rand() - lp);
            float env = Mathf.Sin(Mathf.PI * x);
            d[i] = lp * env * 0.9f;
            if (whistle) { phase += (900f + 1600f * x) / Rate; d[i] += Mathf.Sin(2f * Mathf.PI * phase) * env * 0.2f; }
        }
        return Make("whoosh", d);
    }

    private AudioClip Fall()
    {
        int n = (int)(0.9f * Rate);
        var d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate, x = t / 0.9f;
            phase += Mathf.Lerp(900f, 120f, x) / Rate;
            d[i] = Mathf.Sin(2f * Mathf.PI * phase) * (1f - x) * 0.45f;
        }
        return Make("fall", d);
    }

    // Zacht tikje.
    private AudioClip SoftTap(float f, float vol)
    {
        int n = (int)(0.35f * Rate);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            d[i] = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * f * 2f * t)) * Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-t * 14f) * vol;
        }
        return Make("tap", d);
    }

    private AudioClip Thud(float f, float len)
    {
        int n = (int)(len * Rate);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            d[i] = (Mathf.Sin(2f * Mathf.PI * f * t * (1f - t)) + Rand() * 0.2f) * Mathf.Exp(-t * 25f);
        }
        return Make("thud", d);
    }

    private AudioClip Rumble(float len, float vol)
    {
        int n = (int)(len * Rate);
        var d = new float[n];
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            lp += 0.02f * (Rand() - lp);
            lp2 += 0.02f * (lp - lp2);
            float env = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((len - t) / 0.4f);
            float rattle = (Mathf.Sin(t * 70f) > 0.6f ? Rand() * 0.15f : 0f);
            d[i] = (lp2 * 12f + Mathf.Sin(2f * Mathf.PI * 48f * t) * 0.3f + rattle) * env * vol;
        }
        return Make("rumble", d);
    }

    private AudioClip Poof()
    {
        int n = (int)(0.8f * Rate);
        var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            lp += 0.12f * (Rand() - lp);
            d[i] = (lp * 1.4f + Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.5f) * Mathf.Exp(-t * 5f);
        }
        return Make("poof", d);
    }

    private AudioClip Fanfare()
    {
        int n = (int)(3f * Rate);
        var d = new float[n];
        int[] seq = { 0, 4, 7, 4, 7, 10 };
        for (int k = 0; k < seq.Length; k++)
        {
            float f = Hijaz[seq[k]], start = k * 0.16f, len = k == seq.Length - 1 ? 2.2f : 0.5f;
            for (int i = (int)(start * Rate); i < Mathf.Min(n, (int)((start + len) * Rate)); i++)
            {
                float t = (float)i / Rate - start;
                float env = Mathf.Min(1f, t / 0.02f) * Mathf.Exp(-t * (k == seq.Length - 1 ? 1.2f : 4f));
                float vib = 1f + 0.006f * Mathf.Sin(t * 30f);
                float tone = 0f;
                for (int h = 1; h <= 5; h++) tone += Mathf.Sin(2f * Mathf.PI * f * h * vib * t) / h;
                d[i] += tone * env * 0.16f;
            }
        }
        return Make("fanfare", d);
    }

    private AudioClip Bang()
    {
        int n = (int)(1.4f * Rate);
        var d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            lp += 0.3f * (Rand() - lp);
            d[i] = lp * Mathf.Exp(-t * 9f) * 1.5f + Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 7f) * 0.6f;
            if (t > 0.15f && rnd.NextDouble() < 0.004 * Mathf.Exp(-(t - 0.15f) * 2f)) d[i] += Rand() * 0.8f; // knetteren
        }
        return Make("bang", d);
    }

    // Knetterend vuurwerk.
    private AudioClip CrackleClip()
    {
        int n = (int)(1.6f * Rate);
        var d = new float[n];
        for (int k = 0; k < 220; k++)
        {
            float start = (float)rnd.NextDouble() * 1.4f;
            float vol = (1f - start / 1.6f) * (0.3f + (float)rnd.NextDouble() * 0.5f);
            int s0 = (int)(start * Rate), len = (int)(0.012f * Rate);
            for (int i = 0; i < len && s0 + i < n; i++)
                d[s0 + i] += Rand() * vol * Mathf.Exp(-i / (float)len * 5f);
        }
        return Make("crackle", d);
    }

    private AudioClip Talk()
    {
        // Kort "brabbel"-lettergreepje (wordt met willekeurige toonhoogte afgespeeld per letter)
        int n = (int)(0.09f * Rate);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float env = Mathf.Sin(Mathf.PI * t / 0.09f);
            float f = 380f;
            d[i] = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * f * 2f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 900f * t)) * env * 0.3f;
        }
        return Make("talk", d);
    }

    private AudioClip Deny()
    {
        int n = (int)(0.35f * Rate);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float sq = Mathf.Sin(2f * Mathf.PI * 140f * t) > 0 ? 1f : -1f;
            d[i] = sq * 0.25f * Mathf.Exp(-t * 6f);
        }
        return Make("deny", d);
    }

    private AudioClip Unlock()
    {
        int n = (int)(1.2f * Rate);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            d[i] = Mathf.Sin(2f * Mathf.PI * 80f * t) * Mathf.Exp(-t * 6f) * 0.8f;
            if (t > 0.05f) { float tt = t - 0.05f; d[i] += Mathf.Sin(2f * Mathf.PI * Hijaz[7] * 2f * tt) * Mathf.Exp(-tt * 3f) * 0.3f; }
        }
        return Make("unlock", d);
    }

    // ------------------------------------------------------------------ Muziek

    // Rustige muziek (lus, zonder drums).
    private AudioClip CalmMusic(bool ambient)
    {
        float len = 32f;
        int n = (int)(len * Rate);
        var d = new float[n];
        var r = new System.Random(ambient ? 21 : 12);

        // Akkoorden (frequenties), elk 8 seconden, met overlappende zachte in/uit-fade
        float[][] chords =
        {
            new[] { 146.83f, 220.00f, 261.63f, 329.63f },  // Dm9 (D A C E)
            new[] { 116.54f, 220.00f, 293.66f, 349.23f },  // Bbmaj7 (Bb A D F)
            new[] { 174.61f, 220.00f, 261.63f, 329.63f },  // Fmaj7 (F A C E)
            new[] { 110.00f, 196.00f, 261.63f, 329.63f },  // Am7 (A G C E)
        };
        float chordLen = len / chords.Length, fade = 2.5f;
        float padVol = ambient ? 0.05f : 0.06f;
        for (int c = 0; c < chords.Length; c++)
        {
            float start = c * chordLen - fade * 0.5f;
            foreach (float f in chords[c])
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / Rate;
                    // naadloze lus: wat voor het begin of na het einde valt, loopt rond
                    float local = t - start;
                    if (local < 0f) local += len;
                    if (local > len) local -= len;
                    if (local < 0f || local > chordLen + fade) continue;
                    float env = Mathf.SmoothStep(0f, 1f, local / fade) * Mathf.SmoothStep(0f, 1f, (chordLen + fade - local) / fade);
                    float tone = Mathf.Sin(2f * Mathf.PI * f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * f * 2.003f * t)
                               + 0.6f * Mathf.Sin(2f * Mathf.PI * f * 1.004f * t); // licht ontstemd = zacht "koor"
                    d[i] += tone * env * padVol * (0.85f + 0.15f * Mathf.Sin(t * 0.7f + f));
                }
        }

        // Lage grondtoon, ademend
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            d[i] += Mathf.Sin(2f * Mathf.PI * 73.42f * t) * (0.06f + 0.02f * Mathf.Sin(2f * Mathf.PI * t / 16f));
        }

        if (!ambient)
        {
            // Kalme melodie: één noot per 2 tellen (60 BPM), soms een rust
            int[] mel = { 8, 9, 7, 5, -1, 6, 7, 5, 8, 10, 9, 7, -1, 6, 5, -1 };
            for (int k = 0; k < mel.Length; k++)
            {
                if (mel[k] < 0) continue;
                Pluck(d, k * 2f + 0.2f, Penta[mel[k]], 2.6f, 0.13f, r);
            }
        }

        // Verre belletjes (in de ambient vaker, maar zachter)
        int bells = ambient ? 12 : 6;
        for (int k = 0; k < bells; k++)
        {
            float start = (k + (float)r.NextDouble() * 0.6f) * (len / bells);
            float f = Penta[9 + r.Next(0, 5)] * 2f;
            float vol = ambient ? 0.035f : 0.03f;
            for (int i = (int)(start * Rate); i < Mathf.Min(n, (int)((start + 3f) * Rate)); i++)
            {
                float t = (float)i / Rate - start;
                d[i] += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-t * 3f))
                        * Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 1.4f) * vol;
            }
        }

        for (int i = 0; i < n; i++) d[i] = (float)System.Math.Tanh(d[i] * 1.5f) * 0.8f;
        int edge = Rate / 40;
        for (int i = 0; i < edge; i++) { float a = (float)i / edge; d[i] *= a; d[n - 1 - i] *= a; }
        var clip = AudioClip.Create(ambient ? "AmbientMusic" : "MenuMusic", n, 1, Rate, false);
        clip.SetData(d, 0);
        return clip;
    }

    // Karplus-Strong snaar.
    private static void Pluck(float[] d, float start, float f, float len, float vol, System.Random r)
    {
        int period = Mathf.Max(2, Mathf.RoundToInt(Rate / f));
        var buf = new float[period];
        for (int i = 0; i < period; i++) buf[i] = (float)r.NextDouble() * 2f - 1f;
        int s = (int)(start * Rate), n = (int)(len * Rate), idx = 0;
        for (int i = 0; i < n && s + i < d.Length; i++)
        {
            float cur = buf[idx];
            int next = (idx + 1) % period;
            buf[idx] = 0.497f * (cur + buf[next]);
            idx = next;
            float env = Mathf.Clamp01((n - i) / (Rate * 0.05f));
            d[s + i] += cur * vol * env;
        }
    }
}
