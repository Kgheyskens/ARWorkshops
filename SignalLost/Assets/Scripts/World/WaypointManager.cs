using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class Waypoint
{
    public string name = "Zendmast";
    [Tooltip("Soort plek, bv. 'kerk', 'park' (uit OpenStreetMap)")]
    public string poiKind = "onbekende plek";
    public double latitude;
    public double longitude;
    [Tooltip("Binnen deze straal (meter) geldt het waypoint als bereikt")]
    public float reachRadius = 15f;
    [HideInInspector] public int nodeIndex = -1; // knooppunt in het wegennet (voor de wandelafstand)
}

// Beheert de route en het huidige waypoint.
public class WaypointManager : MonoBehaviour
{
    public List<Waypoint> route = new List<Waypoint>();

    [Tooltip("Enkel om te testen zonder GameManager: meteen door naar het volgende waypoint")]
    public bool autoAdvanceForTesting = false;

    public UnityEvent<int> onWaypointReached;
    public UnityEvent onRouteCompleted;

    public int CurrentIndex { get; private set; }
    public bool IsRouteComplete => CurrentIndex >= route.Count;
    public Waypoint Current => IsRouteComplete ? null : route[CurrentIndex];
    public double DistanceToCurrent { get; private set; } = double.PositiveInfinity;
    public double BearingToCurrent { get; private set; }
    public bool IsInReachRadius { get; private set; }
    // Al eens binnen de straal van het huidige waypoint geweest?
    public bool HasReachedCurrent { get; private set; }
    // Wandelafstand tot het doel (vogelvlucht zonder wegennet).
    public double RouteDistanceToCurrent { get; private set; } = double.PositiveInfinity;

    // Wegennet van de huidige route.
    public RoadNetwork network;
    private float[] distanceField;      // wandelafstand van elk knooppunt naar het huidige doel
    private int distanceFieldFor = -1;
    private float routeTimer;

    public event Action<int> WaypointReached;
    public event Action RouteCompleted;

    private PlayerLocation player;

    void Update()
    {
        if (player == null) player = FindFirstObjectByType<PlayerLocation>();
        if (player == null || !player.IsReady || IsRouteComplete) return;

        Waypoint wp = Current;
        DistanceToCurrent = GeoUtils.Distance(player.Latitude, player.Longitude, wp.latitude, wp.longitude);
        BearingToCurrent = GeoUtils.Bearing(player.Latitude, player.Longitude, wp.latitude, wp.longitude);
        IsInReachRadius = DistanceToCurrent <= wp.reachRadius;
        UpdateRouteDistance(wp);

        if (IsInReachRadius && !HasReachedCurrent)
        {
            HasReachedCurrent = true;
            Debug.Log($"[WaypointManager] Waypoint {CurrentIndex + 1} bereikt: {wp.name}");
            onWaypointReached?.Invoke(CurrentIndex);
            WaypointReached?.Invoke(CurrentIndex);

            if (autoAdvanceForTesting) Advance();
        }
    }

    private void UpdateRouteDistance(Waypoint wp)
    {
        if (network == null || wp.nodeIndex < 0 || wp.nodeIndex >= network.Count)
        {
            RouteDistanceToCurrent = DistanceToCurrent;
            return;
        }

        // Eén keer per doel: Dijkstra vanaf het doel → afstand van elk knooppunt tot het doel
        if (distanceFieldFor != CurrentIndex || distanceField == null)
        {
            distanceField = network.Dijkstra(wp.nodeIndex);
            distanceFieldFor = CurrentIndex;
            routeTimer = 0f;
        }

        // Twee keer per seconde: dichtstbijzijnde knooppunt van de speler opzoeken
        routeTimer -= Time.deltaTime;
        if (routeTimer > 0f && !double.IsInfinity(RouteDistanceToCurrent)) return;
        routeTimer = 0.5f;

        Vector2 me = network.ToLocal(player.Latitude, player.Longitude);
        int node = network.Nearest(me, out float offset, distanceField);
        double viaRoad = node >= 0 ? offset + distanceField[node] : double.PositiveInfinity;

        // Nooit minder dan vogelvlucht, en dichtbij gewoon de rechte afstand
        RouteDistanceToCurrent = DistanceToCurrent < wp.reachRadius * 2f || double.IsInfinity(viaRoad)
            ? DistanceToCurrent
            : System.Math.Max(viaRoad, DistanceToCurrent);
    }

    // Nieuwe route instellen.
    public void SetRoute(List<Waypoint> newRoute)
    {
        route = newRoute ?? new List<Waypoint>();
        distanceField = null;
        distanceFieldFor = -1;
        ResetRoute();
    }

    public void Advance()
    {
        if (IsRouteComplete) return;

        CurrentIndex++;
        HasReachedCurrent = false;
        IsInReachRadius = false;
        DistanceToCurrent = double.PositiveInfinity;
        RouteDistanceToCurrent = double.PositiveInfinity;

        if (IsRouteComplete)
        {
            Debug.Log("[WaypointManager] Route voltooid!");
            onRouteCompleted?.Invoke();
            RouteCompleted?.Invoke();
        }
    }

    public void ResetRoute()
    {
        CurrentIndex = 0;
        HasReachedCurrent = false;
        IsInReachRadius = false;
        DistanceToCurrent = double.PositiveInfinity;
        RouteDistanceToCurrent = double.PositiveInfinity;
    }

    [ContextMenu("Maak testroute rond simulatiestart")]
    private void CreateTestRoute()
    {
        PlayerLocation p = FindFirstObjectByType<PlayerLocation>();
        if (p == null) { Debug.LogWarning("Geen PlayerLocation in de scene gevonden."); return; }

        Vector2[] offsets = { new Vector2(40, 0), new Vector2(60, 50), new Vector2(10, 90), new Vector2(-30, 40) };
        route.Clear();
        for (int i = 0; i < offsets.Length; i++)
        {
            GeoUtils.OffsetByMeters(p.simStartLatitude, p.simStartLongitude, offsets[i].x, offsets[i].y,
                                    out double lat, out double lon);
            route.Add(new Waypoint { name = $"Testmast {i + 1}", latitude = lat, longitude = lon, reachRadius = 10f });
        }
        Debug.Log("[WaypointManager] Testroute aangemaakt.");
    }
}
