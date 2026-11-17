using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// De geest: model + animaties.
public class Genie : MonoBehaviour
{
    public Color skin = new Color(0.3f, 0.75f, 1f);
    public Color vest = new Color(0.55f, 0.2f, 0.75f);
    public Color gold = new Color(1f, 0.8f, 0.3f);
    [Tooltip("Grootte van de geest (1 = ±28 cm hoog)")]
    public float size = 1f;
    [Header("Model")]
    [Tooltip("Assets/Models/Genie.prefab (leeg = in code opbouwen)")]
    public GameObject modelPrefab;

    // Tekst van de geest.
    public event Action<string> Speech;

    public bool Visible { get; private set; }

    private GenieBoxRig rig;
    private SoundFx sfx;
    private Camera cam;
    private Transform model, armL, armR, head;
    private readonly List<Transform> tail = new List<Transform>();
    private readonly List<Transform> eyes = new List<Transform>();
    private readonly List<Transform> mouth = new List<Transform>();
    private ParticleSystem smoke, sparkles;
    private float nextBlink, blinkUntil, talkUntil, waveUntil, castUntil, angryUntil, slapUntil;
    private float yaw;
    private float scaleMul = 1f;
    private Transform slapHand;

    // Geest is (veel) groter dan normaal?
    public bool IsGiant => scaleMul > 1.5f;

    private void Start()
    {
        rig = GetComponentInParent<GenieBoxRig>();
        sfx = FindFirstObjectByType<SoundFx>();
        cam = Camera.main;
        transform.localPosition = new Vector3(0f, rig.TopY, 0f);
        Build();
        smoke = Gfx.Particles("Smoke", transform, rig.transform, Gfx.SoftDot, Gfx.Blend.Additive, 1.4f, 0.06f, 0.05f, new Color(0.4f, 0.7f, 1f, 0.5f), -0.03f, 400);
        sparkles = Gfx.Particles("GenieSparkles", transform, rig.transform, Gfx.Sparkle, Gfx.Blend.Additive, 1.2f, 0.02f, 0.15f, new Color(1f, 0.9f, 0.5f), 0.05f, 400);
        model.gameObject.SetActive(false);
    }

    // Prefab gebruiken of model in code bouwen.
    private void Build()
    {
        if (modelPrefab != null)
        {
            model = Instantiate(modelPrefab, transform, false).transform;
            model.name = "Model";
        }
        else model = BuildModel(transform, skin, vest, gold);

        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one * size;
        FindParts(model);
    }

    // Bewegende onderdelen opzoeken op naam.
    private void FindParts(Transform root)
    {
        tail.Clear(); eyes.Clear(); mouth.Clear();
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < 7; i++)
            foreach (var t in all) if (t.name == "Tail" + i) { tail.Add(t); break; }
        foreach (var t in all)
        {
            if (t.name == "Head") head = t;
            else if (t.name == "ArmL") armL = t;
            else if (t.name == "ArmR") armR = t;
            else if (t.name == "Eye") eyes.Add(t);
            else if (t.name == "Mouth") mouth.Add(t);
        }
        if (head == null || armL == null || armR == null || tail.Count == 0)
            Debug.LogWarning("[Genie] Het model mist onderdelen (Head, ArmL, ArmR, Tail0..6). Maak het prefab opnieuw aan.");
    }

    // Bouwt het geestmodel (statisch voor prefab-export).
    public static Transform BuildModel(Transform parent, Color skin, Color vest, Color gold)
    {
        Transform model = Gfx.Group("Model", parent);

        // Rookstaart: bollen die naar boven toe groter worden
        for (int i = 0; i < 7; i++)
        {
            float r = Mathf.Lerp(0.012f, 0.045f, i / 6f);
            Gfx.Prim(PrimitiveType.Sphere, model, new Vector3(0f, 0.012f + i * 0.017f, 0f), Vector3.one * r * 2f,
                     Color.Lerp(new Color(0.55f, 0.85f, 1f), skin, i / 6f), 0.6f - i * 0.06f, "Tail" + i);
        }

        // Romp, vestje, riem
        Gfx.Prim(PrimitiveType.Sphere, model, new Vector3(0f, 0.15f, 0f), new Vector3(0.085f, 0.095f, 0.065f), skin, 0.2f, "Torso");
        Gfx.Prim(PrimitiveType.Sphere, model, new Vector3(-0.026f, 0.155f, -0.002f), new Vector3(0.05f, 0.08f, 0.069f), vest, 0.05f, "VestL");
        Gfx.Prim(PrimitiveType.Sphere, model, new Vector3(0.026f, 0.155f, -0.002f), new Vector3(0.05f, 0.08f, 0.069f), vest, 0.05f, "VestR");
        Gfx.Prim(PrimitiveType.Cylinder, model, new Vector3(0f, 0.112f, 0f), new Vector3(0.078f, 0.006f, 0.062f), gold, 0.4f, "Belt");

        // Hoofd
        Transform head = Gfx.Group("Head", model);
        head.localPosition = new Vector3(0f, 0.235f, 0f);
        Gfx.Prim(PrimitiveType.Sphere, head, Vector3.zero, Vector3.one * 0.07f, skin, 0.2f, "Face");
        foreach (float x in new[] { -0.015f, 0.015f })
        {
            var eye = Gfx.Group("Eye", head);
            eye.localPosition = new Vector3(x, 0.006f, 0.029f);
            Gfx.Prim(PrimitiveType.Sphere, eye, Vector3.zero, new Vector3(0.017f, 0.02f, 0.01f), Color.white, 0.3f, "White");
            Gfx.Prim(PrimitiveType.Sphere, eye, new Vector3(0f, -0.001f, 0.004f), Vector3.one * 0.009f, new Color(0.05f, 0.05f, 0.15f), 0f, "Pupil");
            Gfx.Prim(PrimitiveType.Cube, head, new Vector3(x, 0.02f, 0.03f), new Vector3(0.014f, 0.003f, 0.004f), new Color(0.1f, 0.15f, 0.35f), 0f, "Brow")
                .transform.localRotation = Quaternion.Euler(0f, 0f, x < 0 ? -12f : 12f);
            // oren + gouden oorbel
            Gfx.Prim(PrimitiveType.Sphere, head, new Vector3(x * 2.4f, 0f, 0f), new Vector3(0.012f, 0.02f, 0.01f), skin, 0.2f, "Ear");
            Gfx.Prim(PrimitiveType.Sphere, head, new Vector3(x * 2.5f, -0.014f, 0f), Vector3.one * 0.007f, gold, 0.6f, "Earring");
        }
        for (int i = 0; i < 5; i++)
        {
            float a = Mathf.Lerp(-0.9f, 0.9f, i / 4f);
            Gfx.Prim(PrimitiveType.Sphere, head, new Vector3(Mathf.Sin(a) * 0.013f, -0.014f - Mathf.Cos(a) * 0.004f + 0.004f, 0.031f),
                     Vector3.one * 0.0045f, new Color(0.1f, 0.05f, 0.2f), 0f, "Mouth");
        }
        // Haarknot + paardenstaart
        Gfx.Prim(PrimitiveType.Sphere, head, new Vector3(0f, 0.04f, -0.005f), Vector3.one * 0.022f, new Color(0.1f, 0.1f, 0.25f), 0f, "Knot");
        Gfx.Prim(PrimitiveType.Cylinder, head, new Vector3(0f, 0.035f, -0.005f), new Vector3(0.026f, 0.002f, 0.026f), gold, 0.6f, "KnotRing");
        Gfx.Prim(PrimitiveType.Capsule, head, new Vector3(0f, 0.058f, -0.018f), new Vector3(0.012f, 0.022f, 0.012f), new Color(0.1f, 0.1f, 0.25f), 0f, "Ponytail")
            .transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);

        MakeArm(model, -1f, skin, gold);
        MakeArm(model, 1f, skin, gold);
        return model;
    }

    private static void MakeArm(Transform model, float side, Color skin, Color gold)
    {
        var shoulder = Gfx.Group(side < 0 ? "ArmL" : "ArmR", model);
        shoulder.localPosition = new Vector3(side * 0.04f, 0.18f, 0f);
        Gfx.Prim(PrimitiveType.Capsule, shoulder, new Vector3(side * 0.012f, -0.028f, 0f), new Vector3(0.022f, 0.03f, 0.022f), skin, 0.2f, "Arm");
        Gfx.Prim(PrimitiveType.Cylinder, shoulder, new Vector3(side * 0.016f, -0.05f, 0f), new Vector3(0.024f, 0.004f, 0.024f), gold, 0.5f, "Cuff");
        Gfx.Prim(PrimitiveType.Sphere, shoulder, new Vector3(side * 0.018f, -0.062f, 0f), Vector3.one * 0.02f, skin, 0.2f, "Hand");
    }

    // ------------------------------------------------------------------ Publieke acties

    public IEnumerator Appear()
    {
        Visible = true;
        scaleMul = 1f;
        if (cam == null) cam = Camera.main;
        yaw = TargetYaw();                                   // meteen naar de speler gedraaid
        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        model.gameObject.SetActive(true);
        model.localScale = Vector3.zero;
        if (sfx != null) { sfx.Play("poof", 0.9f); sfx.Play("fanfare", 0.8f); }
        Vector3 basePos = transform.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.3f)
        {
            float e = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f); // overshoot
            model.localScale = Vector3.one * size * Mathf.Max(0f, e);
            model.localRotation = Quaternion.Euler(0f, (1f - t) * 720f, 0f);
            if (UnityEngine.Random.value < 0.8f)
                Gfx.Burst(smoke, basePos + rig.transform.up * (0.02f + t * 0.15f), rig.transform.up * 0.05f, 2, 0.06f);
            if (UnityEngine.Random.value < 0.5f)
                Gfx.Burst(sparkles, basePos + rig.transform.up * 0.1f, rig.transform.up * 0.1f, 2, 0.15f);
            yield return null;
        }
        model.localScale = Vector3.one * size;
        model.localRotation = Quaternion.identity;
        Wave(1.5f);
    }

    public IEnumerator Disappear()
    {
        if (!Visible) yield break;
        if (sfx != null) sfx.Play("whoosh", 0.8f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
        {
            model.localScale = Vector3.one * size * scaleMul * (1f - t * t);
            model.localRotation = Quaternion.Euler(0f, t * 900f, 0f);
            model.localPosition = Vector3.down * t * 0.05f;
            if (UnityEngine.Random.value < 0.6f) Gfx.Burst(smoke, transform.position + rig.transform.up * 0.05f, -rig.transform.up * 0.03f, 1, 0.05f);
            yield return null;
        }
        model.localPosition = Vector3.zero;
        model.gameObject.SetActive(false);
        Visible = false;
        scaleMul = 1f;
        if (sfx != null) sfx.Play("bump", 0.5f, 1.5f);
    }

    public void Hide()
    {
        StopAllCoroutines();
        Visible = false;
        scaleMul = 1f;
        angryUntil = slapUntil = 0f;
        if (model != null) { model.gameObject.SetActive(false); model.localScale = Vector3.one * size; }
        if (slapHand != null) Destroy(slapHand.gameObject);
        slapHand = null;
    }

    // Kopie van het geestmodel.
    public Transform CreateRiderCopy(Transform parent)
    {
        Transform m = modelPrefab != null ? Instantiate(modelPrefab, parent, false).transform : BuildModel(parent, skin, vest, gold);
        m.name = "Rider";
        m.localScale = Vector3.one * size;
        m.gameObject.SetActive(true);
        return m;
    }

    // Groeien/krimpen (1 = normaal).
    public IEnumerator Resize(float target, float duration)
    {
        float from = scaleMul;
        if (sfx != null) sfx.Play(target > from ? "rumble" : "whoosh", 0.9f, target > from ? 0.7f : 1.2f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, duration))
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            float wobble = 1f + Mathf.Sin(t * 30f) * 0.04f * (1f - t);
            scaleMul = Mathf.Lerp(from, target, e) * wobble;
            model.localScale = Vector3.one * size * scaleMul;
            if (UnityEngine.Random.value < 0.8f)
                Gfx.Burst(smoke, transform.position + rig.transform.up * 0.01f, rig.transform.up * 0.08f * scaleMul, 3, 0.05f * scaleMul, null, Mathf.Sqrt(scaleMul));
            if (UnityEngine.Random.value < 0.5f)
                Gfx.Burst(sparkles, head.position, rig.transform.up * 0.1f, 2, 0.1f * scaleMul, null, Mathf.Sqrt(scaleMul));
            yield return null;
        }
        scaleMul = target;
        model.localScale = Vector3.one * size * scaleMul;
    }

    // Boze animatie.
    public void Angry(float duration)
    {
        angryUntil = Time.time + duration;
        if (sfx != null) sfx.Play("rumble", 0.7f, 1.2f);
    }

    // Klap met reuzenhand naar de camera.
    public IEnumerator Slap(Camera c)
    {
        if (c == null) c = Camera.main;
        slapUntil = Time.time + 10f;
        yield return new WaitForSeconds(0.7f);
        slapHand = BuildHand(null, skin, gold);
        Transform ct = c.transform;
        Vector3 start = ct.position + ct.right * 0.9f + ct.forward * 0.35f + ct.up * 0.15f;
        Vector3 end = ct.position + ct.forward * 0.07f;
        if (sfx != null) sfx.Play("whistle", 1f, 1.2f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.16f)
        {
            float e = t * t;
            slapHand.position = Vector3.Lerp(start, end, e);
            slapHand.rotation = Quaternion.LookRotation(ct.forward, ct.up) * Quaternion.Euler(0f, 0f, Mathf.Lerp(-50f, 10f, e));
            slapHand.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, e);
            yield return null;
        }
        slapHand.position = end;
    }

    // Reuzenhand.
    public static Transform BuildHand(Transform parent, Color skinColor, Color goldColor)
    {
        var hand = Gfx.Group("GiantHand", parent);
        Gfx.Prim(PrimitiveType.Sphere, hand, Vector3.zero, new Vector3(0.11f, 0.13f, 0.045f), skinColor, 0.15f, "Palm");
        for (int i = 0; i < 4; i++)
        {
            float x = -0.042f + i * 0.028f;
            float len = i == 1 || i == 2 ? 0.05f : 0.042f;
            Gfx.Prim(PrimitiveType.Capsule, hand, new Vector3(x, 0.065f + len, 0f), new Vector3(0.026f, len, 0.026f), skinColor, 0.15f, "Finger")
               .transform.localRotation = Quaternion.Euler(0f, 0f, (i - 1.5f) * -5f);
        }
        Gfx.Prim(PrimitiveType.Capsule, hand, new Vector3(0.07f, 0.0f, 0f), new Vector3(0.028f, 0.042f, 0.028f), skinColor, 0.15f, "Thumb")
           .transform.localRotation = Quaternion.Euler(0f, 0f, -50f);
        Gfx.MeshObj(MeshKit.Torus(0.05f, 0.013f, 24, 8, "Bracelet"), hand, new Vector3(0f, -0.085f, 0f), Quaternion.identity, new Vector3(1f, 1f, 0.6f), goldColor, 0.6f, "Bracelet");
        return hand;
    }

    // Geest laat iets zeggen.
    public void Say(string text)
    {
        talkUntil = Time.unscaledTime + Mathf.Min(4f, text.Length * 0.035f);
        Speech?.Invoke(text);
    }

    public void Wave(float duration) => waveUntil = Time.time + duration;

    // Toveranimatie bij een wens.
    public void Cast()
    {
        castUntil = Time.time + 1.2f;
        if (sfx != null) sfx.Play("sparkle", 0.9f);
        Gfx.Burst(sparkles, head.position + rig.transform.up * 0.05f, rig.transform.up * 0.12f, 40, 0.2f);
    }

    // ------------------------------------------------------------------ Animatie

    // Y-hoek om naar de camera te kijken (in ouder-ruimte).
    private float TargetYaw()
    {
        if (cam == null || transform.parent == null) return yaw;
        Vector3 c = transform.parent.InverseTransformPoint(cam.transform.position) - transform.localPosition;
        return Mathf.Atan2(c.x, c.z) * Mathf.Rad2Deg;
    }

    private void Update()
    {
        if (model == null || !model.gameObject.activeSelf) return;
        if (cam == null) cam = Camera.main;
        float t = Time.time;

        // Zweven (en trillen van woede)
        bool angry = t < angryUntil;
        model.localPosition = new Vector3(0f, Mathf.Sin(t * 1.8f) * 0.008f, 0f)
                            + (angry ? UnityEngine.Random.insideUnitSphere * 0.0025f * scaleMul : Vector3.zero);
        if (angry && UnityEngine.Random.value < 0.5f)
            Gfx.Burst(sparkles, head.position + UnityEngine.Random.insideUnitSphere * 0.03f * scaleMul, rig.transform.up * 0.08f * scaleMul, 1, 0.05f,
                      new Color(1f, 0.15f, 0.1f), Mathf.Sqrt(scaleMul));

        // Staart golft
        for (int i = 0; i < tail.Count; i++)
        {
            float k = i / (float)(tail.Count - 1);
            tail[i].localPosition = new Vector3(Mathf.Sin(t * 2.2f - i * 0.7f) * 0.008f * (1f - k),
                                                0.012f + i * 0.017f,
                                                Mathf.Cos(t * 1.7f - i * 0.6f) * 0.006f * (1f - k));
        }

        // Altijd naar de speler (camera) kijken: lichaam draait rond de verticale as,
        // het hoofd kantelt omhoog/omlaag naar de hoogte van de gsm.
        if (cam != null)
        {
            yaw = Mathf.LerpAngle(yaw, TargetYaw(), Time.deltaTime * 8f);
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Vector3 toCam = transform.InverseTransformDirection(cam.transform.position - head.position); // in lichaamsruimte (zonder eigen hoofdkanteling)
            float pitch = Mathf.Clamp(-Mathf.Atan2(toCam.y, new Vector2(toCam.x, toCam.z).magnitude) * Mathf.Rad2Deg, -40f, 30f);
            float tilt = Mathf.Sin(t * 1.3f) * 5f; // licht schuin, levendig
            head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(pitch, 0f, tilt), Time.deltaTime * 6f);
        }

        // Armen: klap, boos, zwaaien, toveren of rustig wiegen
        if (t < slapUntil)
        {
            armL.localRotation = Quaternion.Euler(0f, 0f, -60f);
            armR.localRotation = Quaternion.Euler(-50f, 0f, 165f); // ver naar achter: uithalen!
        }
        else if (angry)
        {
            armL.localRotation = Quaternion.Euler(Mathf.Sin(t * 25f) * 6f, 0f, -55f);
            armR.localRotation = Quaternion.Euler(-Mathf.Sin(t * 25f) * 6f, 0f, 55f);
        }
        else if (t < castUntil)
        {
            armL.localRotation = Quaternion.Euler(0f, 0f, -150f + Mathf.Sin(t * 20f) * 8f);
            armR.localRotation = Quaternion.Euler(0f, 0f, 150f - Mathf.Sin(t * 20f) * 8f);
        }
        else if (t < waveUntil)
        {
            armL.localRotation = Quaternion.Euler(0f, 0f, -20f);
            armR.localRotation = Quaternion.Euler(0f, 0f, 130f + Mathf.Sin(t * 14f) * 25f);
        }
        else
        {
            armL.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.5f) * 10f, 0f, -25f + Mathf.Sin(t * 1.1f) * 6f);
            armR.localRotation = Quaternion.Euler(-Mathf.Sin(t * 1.5f) * 10f, 0f, 25f - Mathf.Sin(t * 1.2f) * 6f);
        }

        // Knipperen
        if (t > nextBlink) { blinkUntil = t + 0.12f; nextBlink = t + UnityEngine.Random.Range(2.5f, 5f); }
        float blink = t < blinkUntil ? 0.1f : 1f;
        foreach (var e in eyes) e.localScale = new Vector3(1f, Mathf.MoveTowards(e.localScale.y, blink, Time.deltaTime * 20f), 1f);

        // Mond beweegt tijdens het praten
        bool talking = Time.unscaledTime < talkUntil;
        float open = talking ? Mathf.Abs(Mathf.Sin(t * 18f)) : 0f;
        for (int i = 0; i < mouth.Count; i++)
            mouth[i].localScale = new Vector3(0.0045f, 0.0045f * (1f + open * (i == 2 ? 2.5f : i == 1 || i == 3 ? 1.5f : 0f)), 0.0045f);

        // Af en toe een rookwolkje uit de staart
        if (UnityEngine.Random.value < 0.15f) Gfx.Burst(smoke, tail[0].position, -rig.transform.up * 0.01f, 1, 0.02f, null, 0.6f);
    }
}
