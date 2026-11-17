using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Geheim portaal in de kamer (in- en uitgang Djinn-rijk).
public class SecretPortal : MonoBehaviour
{
    [Tooltip("Hoe ver achter je het portaal verschijnt (meter)")]
    public float distance = 1.3f;
    [Tooltip("Straal van de ring (meter)")]
    public float radius = 0.48f;
    public Color color = new Color(0.7f, 0.35f, 1f);

    // Speler is door het portaal.
    public event Action Entered;
    public bool IsOpen { get; private set; }
    public Vector3 Center => root != null ? root.position : transform.position;

    private Camera cam;
    private SoundFx sfx;
    private Transform root, swirlA, swirlB;
    private Transform[] beads;
    private Material swirlMat, voidMat;
    private ParticleSystem pull, trail;
    private Vector3 normal;
    private float lastSide, openTime;

    private void Start()
    {
        cam = Camera.main;
        sfx = FindFirstObjectByType<SoundFx>();
    }

    // Opent het portaal achter de speler.
    public void Open(Vector3 up, Vector3? trailFrom = null)
    {
        if (cam == null) cam = Camera.main;
        Close();
        Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, up).normalized;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(cam.transform.up, up).normalized;
        Vector3 center = cam.transform.position - fwd * distance + up * 0.05f;
        Build(center, up);
        IsOpen = true;
        openTime = Time.time;
        lastSide = Side();
        if (sfx != null) { sfx.Play("assemble", 0.8f); sfx.Play("whoosh", 0.6f); }
        if (trailFrom.HasValue) StartCoroutine(Trail(trailFrom.Value, center));
        StartCoroutine(Grow());
    }

    public void Close()
    {
        StopAllCoroutines();
        IsOpen = false;
        if (root != null) Destroy(root.gameObject);
        root = null;
    }

    private void Build(Vector3 center, Vector3 up)
    {
        root = new GameObject("SecretPortal_Ring").transform;
        root.position = center;
        // Het vlak van de ring staat rechtop en kijkt naar de speler
        normal = Vector3.ProjectOnPlane(cam.transform.position - center, up).normalized;
        root.rotation = Quaternion.LookRotation(-normal, up);

        // Ring van gloeiende bolletjes
        beads = new Transform[32];
        for (int i = 0; i < beads.Length; i++)
        {
            float a = i / (float)beads.Length * Mathf.PI * 2f;
            beads[i] = Gfx.Prim(PrimitiveType.Sphere, root, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius, Vector3.one * 0.06f,
                                Color.Lerp(color, Color.white, (i % 4) * 0.12f), 1.6f, "Bead").transform;
        }
        // Donkere "leegte" in het midden + twee draaiende draaikolken
        voidMat = Gfx.NewUnlit(Gfx.Blend.Alpha, Gfx.SoftDot, 1);
        voidMat.color = new Color(0.05f, 0.0f, 0.12f, 0.9f);
        Gfx.Quad(root, Vector3.zero, Quaternion.identity, Vector2.one * radius * 2.3f, voidMat, "Void");
        swirlMat = Gfx.NewUnlit(Gfx.Blend.Additive, Gfx.Ring, 3);
        swirlMat.color = color;
        swirlA = Gfx.Quad(root, new Vector3(0f, 0f, -0.002f), Quaternion.identity, Vector2.one * radius * 1.9f, swirlMat, "SwirlA").transform;
        swirlB = Gfx.Quad(root, new Vector3(0f, 0f, -0.004f), Quaternion.identity, Vector2.one * radius * 1.2f, swirlMat, "SwirlB").transform;

        pull = Gfx.Particles("Pull", root, root, Gfx.Sparkle, Gfx.Blend.Additive, 1.2f, 0.03f, 0.01f, Color.Lerp(color, Color.white, 0.3f), 0f, 400);
        trail = Gfx.Particles("Trail", root, null, Gfx.Sparkle, Gfx.Blend.Additive, 1.5f, 0.025f, 0.02f, new Color(1f, 0.9f, 0.6f), 0f, 600);
        root.localScale = Vector3.one * 0.01f;
    }

    private IEnumerator Grow()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.2f)
        {
            float e = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
            if (root != null) root.localScale = Vector3.one * Mathf.Max(0.01f, e);
            yield return null;
        }
        if (root != null) root.localScale = Vector3.one;
    }

    // Glinsterspoor naar het portaal.
    private IEnumerator Trail(Vector3 from, Vector3 to)
    {
        for (int k = 0; k < 3; k++)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.2f)
            {
                Vector3 p = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.3f;
                Gfx.Burst(trail, p, Vector3.zero, 3, 0.02f);
                yield return null;
            }
            yield return new WaitForSeconds(0.4f);
        }
    }

    // Kant van de camera (positief = speler-kant).
    private float Side() => root == null || cam == null ? 0f : Vector3.Dot(cam.transform.position - root.position, normal);

    private void Update()
    {
        if (!IsOpen || root == null) return;
        if (cam == null) cam = Camera.main;
        float t = Time.time;

        // Animatie: kralen golven, draaikolken draaien, deeltjes worden naar binnen gezogen
        for (int i = 0; i < beads.Length; i++)
        {
            float a = i / (float)beads.Length * Mathf.PI * 2f + t * 0.6f;
            float wobble = 1f + 0.05f * Mathf.Sin(t * 4f + i);
            beads[i].localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius * wobble;
        }
        swirlA.localRotation = Quaternion.Euler(0f, 0f, t * 140f);
        swirlB.localRotation = Quaternion.Euler(0f, 0f, -t * 220f);
        swirlMat.color = new Color(color.r, color.g, color.b, 0.7f + 0.3f * Mathf.Sin(t * 5f));
        if (UnityEngine.Random.value < 0.7f)
        {
            float a = UnityEngine.Random.value * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius * 1.4f;
            Gfx.Burst(pull, root.TransformPoint(p), root.TransformVector(-p * 1.1f), 1, 0.01f);
        }

        // Door het portaal gestapt? (van de ene naar de andere kant, binnen de ring)
        float side = Side();
        Vector3 rel = cam.transform.position - root.position;
        float lateral = (rel - normal * Vector3.Dot(rel, normal)).magnitude;
        bool crossed = Mathf.Sign(side) != Mathf.Sign(lastSide) && lateral < radius * 1.15f;
        bool inside = Mathf.Abs(side) < 0.12f && lateral < radius * 0.8f && Time.time - openTime > 1.5f;
        lastSide = side;
#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame) crossed = true; // test: P = door het portaal
#endif
        if ((crossed || inside) && Time.time - openTime > 1.2f)
        {
            if (sfx != null) sfx.Play("whoosh", 1f);
            Close();
            Entered?.Invoke();
        }
    }
}
