using System;
using UnityEngine;
using Vuforia;

// Anker onder de Vuforia Multi Target; definieert het doos-assenstelsel.
[ExecuteAlways]
public class GenieBoxRig : MonoBehaviour
{
    public enum Axis { PlusY, MinusY, PlusZ, MinusZ, PlusX, MinusX }

    [Header("Afmetingen van je doos (cm) - meet ze met een lat!")]
    [Tooltip("Breedte van de voorkant")]
    public float widthCm = 19.2f;
    [Tooltip("Hoogte van de voorkant")]
    public float heightCm = 24.1f;
    [Tooltip("Dikte van de doos")]
    public float depthCm = 5.4f;

    [Header("Uitlijning met de Vuforia Multi Target")]
    [Tooltip("Welke as van de multi target uit de VOORKANT van de doos wijst")]
    public Axis frontFaceAxis = Axis.PlusY;
    [Tooltip("Draai rond de voorkant tot de gele pijl naar de bovenkant van de opdruk wijst (0/90/180/270)")]
    public float spinDegrees = 0f;

    [Header("Testen in de Editor")]
    [Tooltip("Doen alsof de doos altijd gevonden is (handig zonder webcam)")]
    public bool alwaysTrackedInEditor = false;

    public event Action<bool> TrackingChanged;

    public bool IsTracked { get; private set; }
    // Ziet Vuforia de doos nu echt (TRACKED)?
    public bool HasLivePose { get; private set; }
    public float Width => widthCm * 0.01f;
    public float Length => heightCm * 0.01f;
    public float Thickness => depthCm * 0.01f;
    public float TopY => Thickness * 0.5f;
    public Vector3 Size => new Vector3(Width, Thickness, Length);

    private ObserverBehaviour observer;
    private static readonly int LightDirId = Shader.PropertyToID("_GenieLightDir");

    private void Awake()
    {
        if (!Application.isPlaying) return;
        observer = GetComponentInParent<ObserverBehaviour>();
        if (observer != null)
        {
            observer.OnTargetStatusChanged += OnStatus;
            OnStatus(observer, observer.TargetStatus);
        }
        else Debug.LogWarning("[GenieBoxRig] Geen Vuforia ObserverBehaviour gevonden: hang dit object onder je Multi Target.");
    }

    private void OnDestroy()
    {
        if (observer != null) observer.OnTargetStatusChanged -= OnStatus;
    }

    private void OnStatus(ObserverBehaviour b, TargetStatus status)
    {
        bool tracked = status.Status == Status.TRACKED || status.Status == Status.EXTENDED_TRACKED;
        HasLivePose = status.Status == Status.TRACKED;
        SetTracked(tracked);
    }

    private void SetTracked(bool tracked)
    {
        if (tracked == IsTracked) return;
        IsTracked = tracked;
        TrackingChanged?.Invoke(tracked);
    }

    private void Update()
    {
        ApplyAlignment();
        // Licht van "boven de doos" (schuin), zodat schaduwkanten meedraaien met de doos
        Shader.SetGlobalVector(LightDirId, transform.TransformDirection(new Vector3(0.35f, 1f, -0.25f).normalized));

#if UNITY_EDITOR
        if (Application.isPlaying && alwaysTrackedInEditor) { HasLivePose = true; SetTracked(true); }
#endif
    }

    // Lijnt het assenstelsel uit met de target.
    public void ApplyAlignment()
    {
        Quaternion axis;
        switch (frontFaceAxis)
        {
            case Axis.MinusY: axis = Quaternion.Euler(180f, 0f, 0f); break;
            case Axis.PlusZ: axis = Quaternion.Euler(90f, 0f, 0f); break;
            case Axis.MinusZ: axis = Quaternion.Euler(-90f, 0f, 0f); break;
            case Axis.PlusX: axis = Quaternion.Euler(0f, 0f, -90f); break;
            case Axis.MinusX: axis = Quaternion.Euler(0f, 0f, 90f); break;
            default: axis = Quaternion.identity; break;
        }
        transform.localPosition = Vector3.zero;
        transform.localRotation = axis * Quaternion.Euler(0f, spinDegrees, 0f);

        // Schaal van de ouder compenseren: 1 eenheid hier = altijd 1 meter in de wereld
        Vector3 ps = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        if (ps.x > 1e-5f && ps.y > 1e-5f && ps.z > 1e-5f)
        {
            // Bij een ouder die gedraaid is werkt dit goed zolang die uniform geschaald is (zoals bij Vuforia)
            float s = (ps.x + ps.y + ps.z) / 3f;
            transform.localScale = Vector3.one / s;
        }
    }

    // Voorkant (u,v) naar lokale positie.
    public Vector3 FrontPoint(float u, float v, float lift = 0f) =>
        new Vector3((u - 0.5f) * Width, TopY + lift, (v - 0.5f) * Length);

    // Lokale positie naar (u,v).
    public Vector2 FrontUV(Vector3 local) => new Vector2(local.x / Width + 0.5f, local.z / Length + 0.5f);

    // Straal snijden met de voorkant.
    public bool RayToFront(Ray worldRay, out Vector3 local)
    {
        Vector3 o = transform.InverseTransformPoint(worldRay.origin);
        Vector3 d = transform.InverseTransformDirection(worldRay.direction);
        local = Vector3.zero;
        if (Mathf.Abs(d.y) < 1e-4f) return false;
        float t = (TopY - o.y) / d.y;
        if (t < 0f) return false;
        local = o + d * t;
        return true;
    }

    // Camerapositie in doos-ruimte.
    public Vector3 CameraLocal(Camera cam) => transform.InverseTransformPoint(cam.transform.position);

    // Maakt de kind-onderdelen aan.
    [ContextMenu("Maak onderdelen aan (Dust, KeyPuzzle, HollowBox, Maze, Genie, Wishes)")]
    private void CreateParts()
    {
        Ensure<DustCover>("Dust");
        Ensure<KeyPuzzle>("KeyPuzzle");
        Ensure<HollowBox>("HollowBox");
        Ensure<MazeGame>("Maze");
        Ensure<Genie>("Genie");
        Ensure<WishEffects>("Wishes");
    }

    private void Ensure<T>(string childName) where T : Component
    {
        if (GetComponentInChildren<T>(true) != null) return;
        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        go.AddComponent<T>();
    }

    private void OnDrawGizmos()
    {
        ApplyAlignment();
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, Size);
        // Voorkant (venster) gevuld tekenen
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.25f);
        Gizmos.DrawCube(new Vector3(0f, TopY, 0f), new Vector3(Width, 0.001f, Length));
        // Gele pijl: naar de bovenkant van de opdruk (lees-richting van "Kellogg's")
        Gizmos.color = Color.yellow;
        Vector3 a = new Vector3(0f, TopY + 0.002f, -Length * 0.3f), b = new Vector3(0f, TopY + 0.002f, Length * 0.45f);
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, b + new Vector3(-0.03f, 0f, -0.04f));
        Gizmos.DrawLine(b, b + new Vector3(0.03f, 0f, -0.04f));
    }
}
