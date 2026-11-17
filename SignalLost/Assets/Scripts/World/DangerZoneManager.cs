using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DangerZone
{
    public string name = "Storingsveld";
    public double latitude;
    public double longitude;
    public float radius = 15f;
}

// Onzichtbare storingsvelden: wie erin loopt verliest tijd en signaal.
public class DangerZoneManager : MonoBehaviour
{
    public List<DangerZone> zones = new List<DangerZone>();
    [Tooltip("Binnen deze afstand (m) van de rand krijg je een waarschuwing")]
    public float warningMargin = 20f;
    [Tooltip("Enkel actief tijdens het spelen (wordt gezet door de GameManager)")]
    public bool active;

    public bool IsInside { get; private set; }
    public bool IsNear { get; private set; }
    public float NearestEdgeDistance { get; private set; } = float.PositiveInfinity;

    public event Action Entered;
    public event Action Exited;

    private PlayerLocation player;

    void Update()
    {
        if (player == null) player = FindFirstObjectByType<PlayerLocation>();

        if (!active || player == null || !player.IsReady || zones.Count == 0)
        {
            SetInside(false);
            IsNear = false;
            NearestEdgeDistance = float.PositiveInfinity;
            return;
        }

        float nearest = float.PositiveInfinity;
        foreach (var z in zones)
        {
            float edge = (float)GeoUtils.Distance(player.Latitude, player.Longitude, z.latitude, z.longitude) - z.radius;
            if (edge < nearest) nearest = edge;
        }

        NearestEdgeDistance = nearest;
        IsNear = nearest <= warningMargin;
        SetInside(nearest <= 0f);
    }

    private void SetInside(bool inside)
    {
        if (inside == IsInside) return;
        IsInside = inside;
        if (inside) Entered?.Invoke(); else Exited?.Invoke();
    }

    public void SetZones(List<DangerZone> newZones)
    {
        zones = newZones ?? new List<DangerZone>();
    }

    public void Clear()
    {
        zones = new List<DangerZone>();
        SetInside(false);
        IsNear = false;
    }
}
