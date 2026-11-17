using UnityEngine;

// Synthwave-menumuziek volledig in code, met Mira's motief als melodie.
public static class MenuMusic
{
    private const int SampleRate = 22050;
    private const float Bpm = 96f;

    public static AudioClip Create()
    {
        float beat = 60f / Bpm;
        float bar = beat * 4f;
        int bars = 8;
        int total = Mathf.CeilToInt(bar * bars * SampleRate);
        float[] data = new float[total];
        var rnd = new System.Random(7);

        // Akkoorden per maat (MIDI-noten): Am, F, C, G, Am, F, C, E (spanning terug naar Am)
        int[][] chords =
        {
            new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 },
            new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 52, 56, 59 },
        };
        int[] bassRoots = { 45, 41, 36, 43, 45, 41, 36, 40 };

        float padLp = 0f, bassLp = 0f, staticLp = 0f;

        for (int i = 0; i < total; i++)
        {
            float t = (float)i / SampleRate;
            int barIndex = Mathf.Min(bars - 1, (int)(t / bar));
            float tInBar = t - barIndex * bar;
            float tInBeat = tInBar % beat;
            float tInEighth = tInBar % (beat * 0.5f);
            float tInSixteenth = tInBar % (beat * 0.25f);
            int sixteenth = (int)(tInBar / (beat * 0.25f));
            int[] chord = chords[barIndex];

            // ---- Pad: 3 akkoordtonen, elk 2 licht ontstemde zaagtanden, zacht in/uit per maat
            float pad = 0f;
            foreach (int note in chord)
            {
                float f = Freq(note);
                pad += Saw(t * f * 1.003f) + Saw(t * f * 0.997f);
            }
            float padEnv = Mathf.Clamp01(tInBar / 0.4f) * Mathf.Clamp01((bar - tInBar) / 0.3f);
            padLp += 0.06f * (pad * padEnv - padLp); // low-pass → warm
            float padOut = padLp * 0.05f;

            // ---- Bas: achtsten op de grondtoon, plukkerig
            float bf = Freq(bassRoots[barIndex]);
            float bassEnv = Mathf.Exp(-tInEighth * 9f);
            float bass = (Mathf.Sin(2f * Mathf.PI * bf * t) + 0.5f * Square(t * bf)) * bassEnv;
            bassLp += 0.15f * (bass - bassLp);
            float bassOut = bassLp * 0.22f;

            // ---- Arpeggio: zestienden door de akkoordtonen, 2 octaven hoger
            int arpNote = chord[sixteenth % 3] + 12 + (sixteenth % 6 >= 3 ? 12 : 0);
            float arpEnv = Mathf.Exp(-tInSixteenth * 18f);
            float arp = Mathf.Sin(2f * Mathf.PI * Freq(arpNote) * t) * arpEnv * 0.06f;

            // ---- Melodie: Mira's drie tonen in maat 1-2 en 5-6 (tweede keer met echo)
            float melody = 0f;
            if (barIndex == 0 || barIndex == 4) melody += Motif(tInBar, beat, barIndex == 4 ? 0.35f : 0f);
            if (barIndex == 1 || barIndex == 5) melody += Motif(tInBar + bar, beat, barIndex == 5 ? 0.35f : 0f) * 0.4f; // naklank

            // ---- Drums: zachte kick op 1 en 3, hi-hat op de tussentellen
            int beatInBar = (int)(tInBar / beat);
            float kick = 0f;
            if (beatInBar == 0 || beatInBar == 2)
            {
                float kf = 50f + 90f * Mathf.Exp(-tInBeat * 30f);
                kick = Mathf.Sin(2f * Mathf.PI * kf * tInBeat) * Mathf.Exp(-tInBeat * 8f) * 0.35f;
            }
            float hat = 0f;
            if (tInBeat >= beat * 0.5f)
            {
                float th = tInBeat - beat * 0.5f;
                hat = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Exp(-th * 60f) * 0.04f;
            }

            // ---- Radio-sfeer: zachte ruis + af en toe een vinyl-tikje
            float white = (float)rnd.NextDouble() * 2f - 1f;
            staticLp += 0.2f * (white - staticLp);
            float crackle = rnd.NextDouble() < 0.0004 ? ((float)rnd.NextDouble() - 0.5f) * 0.5f : 0f;
            float radio = staticLp * 0.015f + crackle;

            float mix = padOut + bassOut + arp + melody + kick + hat + radio;
            data[i] = (float)System.Math.Tanh(mix * 1.4f) * 0.8f; // zachte saturatie
        }

        // Naadloze loop: begin en einde heel kort in elkaar laten overvloeien
        int fade = SampleRate / 50;
        for (int i = 0; i < fade; i++)
        {
            float a = (float)i / fade;
            data[i] *= a;
            data[total - 1 - i] *= a;
        }

        AudioClip clip = AudioClip.Create("MenuThema", total, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Mira's motief: E5 - A5 - E6 op tel 1, 2 en 3.
    private static float Motif(float tInBar, float beat, float echo)
    {
        float[] freqs = { 659.25f, 880f, 1318.5f };
        float sum = 0f;
        for (int n = 0; n < 3; n++)
        {
            float start = n * beat;
            float local = tInBar - start;
            if (local < 0f || local > beat * 3f) continue;
            float env = Mathf.Min(1f, local / 0.01f) * Mathf.Exp(-local * 2.2f);
            float f = freqs[n];
            float bell = Mathf.Sin(2f * Mathf.PI * f * local) + 0.25f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * local);
            sum += bell * env * 0.12f;

            if (echo > 0f && local > beat * 0.75f)
            {
                float l2 = local - beat * 0.75f;
                sum += Mathf.Sin(2f * Mathf.PI * f * l2) * Mathf.Exp(-l2 * 3f) * 0.12f * echo;
            }
        }
        return sum;
    }

    private static float Freq(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    private static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));
    private static float Square(float phase) => (phase - Mathf.Floor(phase)) < 0.5f ? 1f : -1f;
}
