using System.Collections.Generic;
using UnityEngine;

// Animaties van de AR-zendmast (schotel, knipperlicht, signaalringen).
public class SignalBeacon : MonoBehaviour
{
    [Header("Onderdelen")]
    public Transform dish;
    public PartColor blinkLight;
    public PartColor[] accentParts;
    public Transform ringOrigin;

    [Header("Animatie")]
    public float dishSpeed = 90f;
    public int ringCount = 3;
    public float ringMaxRadius = 2.5f;

    // Speler staat binnen bereik (gezet door ARWorld).
    public bool IsLocked { get; set; }

    private readonly List<LineRenderer> rings = new List<LineRenderer>();
    private Color ringColor = new Color(1f, 0.65f, 0.25f);

    void Awake()
    {
        if (GetComponentInChildren<Collider>() == null)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.75f, 0f);
            box.size = new Vector3(2f, 3.8f, 2f);
        }
        CreateRings();
    }

    private void CreateRings()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        Material mat = new Material(shader);
        Transform origin = ringOrigin != null ? ringOrigin : transform;

        for (int i = 0; i < ringCount; i++)
        {
            var go = new GameObject("Signaalring " + i);
            go.transform.SetParent(origin, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = mat;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 32;
            lr.widthMultiplier = 0.05f;
            rings.Add(lr);
        }
    }

    public void SetAccent(Color c)
    {
        ringColor = c;
        if (accentParts == null) return;
        foreach (var p in accentParts) if (p != null) p.SetColor(c);
    }

    void Update()
    {
        if (dish != null) dish.Rotate(0f, dishSpeed * Time.deltaTime, 0f, Space.World);

        if (blinkLight != null)
        {
            float speed = IsLocked ? 8f : 2f;
            bool on = Mathf.Repeat(Time.time * speed, 1f) < 0.5f;
            blinkLight.SetColor(on ? new Color(1f, 0.15f, 0.1f) : new Color(0.25f, 0.02f, 0.02f));
        }

        for (int i = 0; i < rings.Count; i++)
        {
            float t = Mathf.Repeat(Time.time * (IsLocked ? 1.2f : 0.6f) + i / (float)rings.Count, 1f);
            float radius = Mathf.Lerp(0.2f, ringMaxRadius, t);
            for (int p = 0; p < rings[i].positionCount; p++)
            {
                float a = p / (float)rings[i].positionCount * Mathf.PI * 2f;
                rings[i].SetPosition(p, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            Color c = ringColor;
            c.a = 1f - t;
            rings[i].startColor = c;
            rings[i].endColor = c;
        }
    }

    [ContextMenu("Bouw mast-model")]
    public void BuildModel()
    {
        ModelBuilder.ClearChildren(transform);

        Color steel = new Color(0.4f, 0.42f, 0.45f);
        Color dark = new Color(0.12f, 0.12f, 0.14f);
        Color accent = new Color(1f, 0.65f, 0.25f);
        Transform t = transform;

        ModelBuilder.Part(PrimitiveType.Cylinder, t, "Voet", new Vector3(0f, 0.05f, 0f), new Vector3(1.2f, 0.05f, 1.2f), Vector3.zero, dark);
        ModelBuilder.Part(PrimitiveType.Cylinder, t, "Mast", new Vector3(0f, 1.6f, 0f), new Vector3(0.12f, 1.6f, 0.12f), Vector3.zero, steel);

        // Vakwerk: schuine poten + dwarsbalken
        for (int i = 0; i < 3; i++)
        {
            float angle = i * 120f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            ModelBuilder.Part(PrimitiveType.Cube, t, "Poot " + i, dir * 0.35f + Vector3.up * 1.2f, new Vector3(0.06f, 2.4f, 0.06f),
                              new Vector3(-8f, angle, 0f) , steel);
        }
        for (int i = 0; i < 4; i++)
        {
            float y = 0.5f + i * 0.7f;
            float w = 0.9f - i * 0.15f;
            ModelBuilder.Part(PrimitiveType.Cube, t, "Balk " + i, new Vector3(0f, y, 0f), new Vector3(w, 0.05f, 0.05f), new Vector3(0f, 45f * i, 0f), steel);
        }

        // Draaiende schotel
        dish = ModelBuilder.Pivot("Schotel", t, new Vector3(0f, 3.0f, 0f), Vector3.zero);
        var dishPart = ModelBuilder.Part(PrimitiveType.Sphere, dish, "Schotel", new Vector3(0.35f, 0f, 0f), new Vector3(0.15f, 0.8f, 0.8f), Vector3.zero, accent);
        ModelBuilder.Part(PrimitiveType.Cylinder, dish, "Arm", new Vector3(0.15f, 0f, 0f), new Vector3(0.06f, 0.18f, 0.06f), new Vector3(0f, 0f, 90f), steel);

        // Knipperlicht + plek voor de signaalringen
        blinkLight = ModelBuilder.Part(PrimitiveType.Sphere, t, "Knipperlicht", new Vector3(0f, 3.45f, 0f), Vector3.one * 0.28f, Vector3.zero, Color.red);
        ringOrigin = ModelBuilder.Pivot("Ringen", t, new Vector3(0f, 3.45f, 0f), Vector3.zero);

        accentParts = new[] { dishPart };
        rings.Clear();
        if (Application.isPlaying) CreateRings();
        Debug.Log("[SignalBeacon] Mast-model gebouwd.");
    }
}
