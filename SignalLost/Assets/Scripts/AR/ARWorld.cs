using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// Plaatst de mast en storingsvelden in AR op basis van GPS + kompas.
public class ARWorld : MonoBehaviour
{
    [Header("Zendmast")]
    [Tooltip("De SignaalMast uit de scene (wordt automatisch gezocht als dit leeg is)")]
    public SignalBeacon beacon;
    [Tooltip("De mast verschijnt als je dichter dan dit (m) bij het waypoint bent")]
    public float beaconShowDistance = 45f;
    public float minPlaceDistance = 4f;
    public float maxPlaceDistance = 15f;

    [Header("Storingsvelden")]
    [Tooltip("Afbakening + waarschuwingsbord tonen (wordt op MOEILIJK uitgezet door de GameManager)")]
    public bool showDangerZones = true;
    public float zoneShowDistance = 70f;
    public float zoneMinPlaceDistance = 6f;
    public float zoneMaxPlaceDistance = 25f;

    [Header("Algemeen")]
    [Tooltip("Hoogte van de grond t.o.v. de camera (gsm in de hand ≈ 1,4 m)")]
    public float groundOffset = -1.4f;
    [Tooltip("In de Editor de camera laten meedraaien met Q/E van de simulatie")]
    public bool driveCameraInSimulation = true;
    [Tooltip("Wordt aan/uit gezet door de GameManager")]
    public bool active;

    public event Action BeaconTapped;

    private Camera cam;
    private PlayerLocation player;
    private WaypointManager waypoints;
    private DangerZoneManager zones;
    private float northOffset; // kompasrichting - camera-yaw
    private bool beaconWasLocked;
    private Vector3 lockedBeaconPosition;
    private class ZoneVisual
    {
        public GameObject root;
        public Transform sign;
        public TMP_Text text;
        public readonly List<Transform> posts = new List<Transform>();
        public LineRenderer ring;
    }
    private readonly Dictionary<DangerZone, ZoneVisual> zoneVisuals = new Dictionary<DangerZone, ZoneVisual>();
    private float glitchTimer;

    void Start()
    {
        FindReferences();
        if (beacon == null)
        {
            Debug.LogWarning("[ARWorld] Geen SignaalMast in de scene gevonden, er wordt er een gebouwd.");
            beacon = new GameObject("SignaalMast (auto)").AddComponent<SignalBeacon>();
            beacon.BuildModel();
        }
        beacon.gameObject.SetActive(false);
    }

    private void FindReferences()
    {
        if (cam == null) cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (player == null) player = FindFirstObjectByType<PlayerLocation>();
        if (waypoints == null) waypoints = FindFirstObjectByType<WaypointManager>();
        if (zones == null) zones = FindFirstObjectByType<DangerZoneManager>();
        if (beacon == null) beacon = FindFirstObjectByType<SignalBeacon>(FindObjectsInactive.Include);
    }

    // Koppel kompasrichting aan camerarichting.
    public void Calibrate()
    {
        FindReferences();
        if (cam == null || player == null || !player.IsReady) return;
        northOffset = player.Heading - cam.transform.eulerAngles.y;
        Debug.Log($"[ARWorld] Gekalibreerd: kompas {player.Heading:F0}°, camera {cam.transform.eulerAngles.y:F0}°");
    }

    public void SetBeaconColor(Color c)
    {
        FindReferences();
        if (beacon != null) beacon.SetAccent(c);
    }

    void LateUpdate()
    {
        FindReferences();
        if (cam == null || player == null || !player.IsReady) return;

        // In de Editor heeft de camera geen sensoren: laat ze meedraaien met de gesimuleerde kijkrichting
        if (player.IsSimulated && driveCameraInSimulation)
            cam.transform.rotation = Quaternion.Euler(0f, player.Heading - northOffset, 0f);

        UpdateBeacon();
        UpdateZoneMarkers();
        HandleTap();
    }

    // Wereldpositie voor een GPS-punt, op begrensde afstand van de camera.
    private Vector3 PlaceFor(double lat, double lon, float minDist, float maxDist)
    {
        Vector2 eastNorth = GeoUtils.ToLocalMeters(player.Latitude, player.Longitude, lat, lon);
        Vector3 dir = new Vector3(eastNorth.x, 0f, eastNorth.y);
        float realDistance = dir.magnitude;

        if (realDistance < 0.01f)
        {
            Vector3 fwd = cam.transform.forward;
            dir = new Vector3(fwd.x, 0f, fwd.z);
        }
        else
        {
            dir = Quaternion.Euler(0f, -northOffset, 0f) * dir; // ware noorden → Unity-wereld
        }

        float d = Mathf.Clamp(realDistance, minDist, maxDist);
        Vector3 pos = cam.transform.position + dir.normalized * d;
        pos.y = cam.transform.position.y + groundOffset;
        return pos;
    }

    // ------------------------------------------------------------------ Zendmast

    private void UpdateBeacon()
    {
        if (beacon == null) return;

        Waypoint wp = waypoints != null ? waypoints.Current : null;
        bool locked = waypoints != null && waypoints.HasReachedCurrent &&
                      wp != null && waypoints.DistanceToCurrent <= wp.reachRadius * 1.5f;

        // De mast verschijnt altijd ruim vóór je het signaal gevonden hebt,
        // ook als 'Beacon Show Distance' kleiner staat dan de bereikstraal.
        float showDistance = wp != null ? Mathf.Max(beaconShowDistance, wp.reachRadius + 10f) : beaconShowDistance;
        bool show = active && wp != null && (waypoints.DistanceToCurrent <= showDistance || locked);

        if (!show)
        {
            if (beacon.gameObject.activeSelf) beacon.gameObject.SetActive(false);
            beaconWasLocked = false;
            return;
        }

        beacon.IsLocked = locked;
        Vector3 target;
        if (locked)
        {
            // Signaal gevonden: de mast "materialiseert" eenmalig vóór je en blijft daar staan,
            // zodat je er zeker op kan tikken. (Op enkele meters is de GPS-richting toch te onnauwkeurig.)
            if (!beaconWasLocked)
            {
                Vector3 forward = cam.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                lockedBeaconPosition = cam.transform.position + forward.normalized * 7f;
                lockedBeaconPosition.y = cam.transform.position.y + groundOffset;
            }
            target = lockedBeaconPosition;
        }
        else
        {
            target = PlaceFor(wp.latitude, wp.longitude, minPlaceDistance, maxPlaceDistance);
        }

        beaconWasLocked = locked;

        if (!beacon.gameObject.activeSelf)
        {
            beacon.gameObject.SetActive(true);
            beacon.transform.position = target;
        }
        else
        {
            float speed = locked ? 2f : 3f; // zacht meebewegen (GPS springt soms)
            beacon.transform.position = Vector3.Lerp(beacon.transform.position, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
        }
    }

    // ------------------------------------------------------------------ Storingsvelden

    // Afbakening + waarschuwingsbord rond storingsvelden in de buurt (niet op MOEILIJK).
    private void UpdateZoneMarkers()
    {
        // Opruimen van zones die niet meer bestaan
        var toRemove = new List<DangerZone>();
        foreach (var kv in zoneVisuals)
            if (zones == null || !zones.zones.Contains(kv.Key)) toRemove.Add(kv.Key);
        foreach (var z in toRemove)
        {
            if (zoneVisuals[z].root != null) Destroy(zoneVisuals[z].root);
            zoneVisuals.Remove(z);
        }

        if (zones == null) return;

        glitchTimer -= Time.deltaTime;
        bool glitchNow = glitchTimer <= 0f;
        if (glitchNow) glitchTimer = 0.12f;

        foreach (var z in zones.zones)
        {
            float realCenter = (float)GeoUtils.Distance(player.Latitude, player.Longitude, z.latitude, z.longitude);
            bool show = active && showDangerZones && realCenter - z.radius <= zoneShowDistance;

            zoneVisuals.TryGetValue(z, out ZoneVisual v);
            if (!show)
            {
                if (v != null) v.root.SetActive(false);
                continue;
            }

            // Op de ECHTE plek en op ware grootte (niet dichterbij getrokken), zodat wat je ziet
            // overeenkomt met waar het storingsveld echt ligt.
            Vector3 target = PlaceFor(z.latitude, z.longitude, 0f, 10000f);
            if (v == null)
            {
                v = CreateZoneVisual();
                v.root.transform.position = target;
                zoneVisuals[z] = v;
            }
            if (!v.root.activeSelf)
            {
                v.root.SetActive(true);
                v.root.transform.position = target;
            }
            v.root.transform.position = Vector3.Lerp(v.root.transform.position, target, 1f - Mathf.Exp(-2f * Time.deltaTime));

            // Straal van de afbakening mee verkleinen met de (begrensde) plaatsingsafstand
            float ringRadius = z.radius;
            LayoutRing(v, ringRadius);

            // Bord naar de speler draaien (enkel rond de verticale as)
            Vector3 toSign = v.sign.position - cam.transform.position;
            toSign.y = 0f;
            if (toSign.sqrMagnitude > 0.01f) v.sign.rotation = Quaternion.LookRotation(toSign);

            // Ring pulseert, tekst hapert af en toe (het is tenslotte storing)
            Color ringColor = new Color(1f, 0.1f, 0.1f, 0.5f + 0.4f * Mathf.Sin(Time.time * 4f));
            if (v.ring != null) { v.ring.startColor = ringColor; v.ring.endColor = ringColor; }
            if (glitchNow && v.text != null)
                v.text.color = UnityEngine.Random.value < 0.12f ? new Color(0f, 0f, 0f, 0.2f) : new Color(0.1f, 0.05f, 0.05f);
        }
    }

    private ZoneVisual CreateZoneVisual()
    {
        var v = new ZoneVisual { root = new GameObject("StoringsZone") };
        Transform root = v.root.transform;

        // Rood-witte paaltjes rondom
        for (int i = 0; i < 32; i++)
        {
            Color c = i % 2 == 0 ? new Color(0.9f, 0.1f, 0.1f) : Color.white;
            v.posts.Add(ModelBuilder.Part(PrimitiveType.Cube, root, "Paaltje " + i, Vector3.zero,
                                          new Vector3(0.2f, 1.2f, 0.2f), Vector3.zero, c).transform);
        }

        // Pulserende ring op de grond
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            var ringGO = new GameObject("Ring");
            ringGO.transform.SetParent(root, false);
            ringGO.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            v.ring = ringGO.AddComponent<LineRenderer>();
            v.ring.material = new Material(shader);
            v.ring.useWorldSpace = false;
            v.ring.loop = true;
            v.ring.positionCount = 48;
            v.ring.widthMultiplier = 0.08f;
        }

        // Waarschuwingsbord in het midden
        v.sign = ModelBuilder.Pivot("Bord", root, Vector3.zero, Vector3.zero);
        v.sign.localScale = Vector3.one * 1.6f; // groter, zodat je het van ver ziet
        ModelBuilder.Part(PrimitiveType.Cylinder, v.sign, "Paal", new Vector3(0f, 0.9f, 0f), new Vector3(0.08f, 0.9f, 0.08f), Vector3.zero, new Color(0.5f, 0.5f, 0.55f));
        ModelBuilder.Part(PrimitiveType.Cube, v.sign, "Rand", new Vector3(0f, 1.95f, 0.01f), new Vector3(1.7f, 0.9f, 0.04f), Vector3.zero, new Color(0.85f, 0.1f, 0.1f));
        ModelBuilder.Part(PrimitiveType.Cube, v.sign, "Plaat", new Vector3(0f, 1.95f, -0.01f), new Vector3(1.5f, 0.72f, 0.04f), Vector3.zero, new Color(1f, 0.85f, 0.1f));

        var textGO = new GameObject("Tekst", typeof(RectTransform));
        textGO.transform.SetParent(v.sign, false);
        textGO.transform.localPosition = new Vector3(0f, 1.95f, -0.04f); // voorkant = kant naar de speler
        var tmp = textGO.AddComponent<TextMeshPro>();
        tmp.rectTransform.sizeDelta = new Vector2(1.4f, 0.65f);
        tmp.text = "<b>STORING!</b>\n<size=55%>niet betreden</size>";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.5f;
        tmp.fontSizeMax = 6f;
        tmp.color = new Color(0.1f, 0.05f, 0.05f);
        v.text = tmp;

        return v;
    }

    private static void LayoutRing(ZoneVisual v, float radius)
    {
        for (int i = 0; i < v.posts.Count; i++)
        {
            float a = i / (float)v.posts.Count * Mathf.PI * 2f;
            v.posts[i].localPosition = new Vector3(Mathf.Cos(a) * radius, 0.6f, Mathf.Sin(a) * radius);
        }
        if (v.ring != null)
        {
            for (int i = 0; i < v.ring.positionCount; i++)
            {
                float a = i / (float)v.ring.positionCount * Mathf.PI * 2f;
                v.ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }
    }

    // Alles weghalen (terug naar menu).
    public void ClearAll()
    {
        foreach (var v in zoneVisuals.Values) if (v.root != null) Destroy(v.root);
        zoneVisuals.Clear();
        if (beacon != null) beacon.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ Tikken op de mast

    private void HandleTap()
    {
        if (!active || beacon == null || !beacon.gameObject.activeSelf) return;
        if (!PointerPressedThisFrame(out Vector2 screenPos)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f) && hit.transform.IsChildOf(beacon.transform))
            BeaconTapped?.Invoke();
    }

    private static bool PointerPressedThisFrame(out Vector2 position)
    {
#if ENABLE_INPUT_SYSTEM
        var pointer = UnityEngine.InputSystem.Pointer.current;
        if (pointer != null && pointer.press.wasPressedThisFrame)
        {
            position = pointer.position.ReadValue();
            return true;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            position = Input.mousePosition;
            return true;
        }
#endif
        position = Vector2.zero;
        return false;
    }
}
