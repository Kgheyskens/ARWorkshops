using System;
using System.Collections.Generic;
using UnityEngine;

public enum SignalLevel { Lost, IceCold, Cold, Lukewarm, Warm, Hot, Locked }
public enum SignalTrend { Steady, Warmer, Colder }

// Zet afstand tot het waypoint om naar Strength, Level en Trend.
public class SignalTracker : MonoBehaviour
{
    [Header("Afstandsdrempels (meter)")]
    [Tooltip("Verder dan dit = sterkte 0 (wordt vergroot door sommige radio's)")]
    public float maxRange = 250f;
    public float coldDistance = 150f;     // verder = IJSKOUD
    public float lukewarmDistance = 80f;  // verder = KOUD
    public float warmDistance = 40f;      // verder = LAUW
    public float hotDistance = 20f;       // verder = WARM, dichter = HEET

    [Header("Trend (warmer/kouder)")]
    public float trendWindow = 3f;
    public float trendDeadband = 1.5f;

    // 0..1, versnelt dichtbij.
    public float Strength { get; private set; }
    // 0..1, lineair met de afstand.
    public float Clarity { get; private set; }
    public SignalLevel Level { get; private set; } = SignalLevel.Lost;
    public SignalTrend Trend { get; private set; } = SignalTrend.Steady;
    public double Distance { get; private set; } = double.PositiveInfinity;

    // (oud niveau, nieuw niveau)
    public event Action<SignalLevel, SignalLevel> LevelChanged;

    private PlayerLocation player;
    private WaypointManager waypoints;
    private readonly Queue<(float time, double dist)> history = new Queue<(float, double)>();
    private int lastWaypointIndex = -1;

    void Update()
    {
        if (player == null) player = FindFirstObjectByType<PlayerLocation>();
        if (waypoints == null) waypoints = FindFirstObjectByType<WaypointManager>();

        if (player == null || waypoints == null || !player.IsReady ||
            waypoints.IsRouteComplete || double.IsInfinity(waypoints.DistanceToCurrent))
        {
            Strength = 0f;
            Clarity = 0f;
            Trend = SignalTrend.Steady;
            SetLevel(SignalLevel.Lost);
            return;
        }

        if (waypoints.CurrentIndex != lastWaypointIndex)
        {
            lastWaypointIndex = waypoints.CurrentIndex;
            history.Clear();
        }

        // Wandelafstand over de weg (zoals een routeplanner), niet in vogelvlucht
        double d = double.IsInfinity(waypoints.RouteDistanceToCurrent) ? waypoints.DistanceToCurrent : waypoints.RouteDistanceToCurrent;
        float reach = waypoints.Current.reachRadius;
        Distance = d;

        float linear = Mathf.Clamp01(1f - ((float)d - reach) / Mathf.Max(1f, maxRange - reach));
        Clarity = linear;
        Strength = Mathf.Pow(linear, 1.5f);

        SignalLevel newLevel;
        if (waypoints.IsInReachRadius) newLevel = SignalLevel.Locked;
        else if (d <= hotDistance) newLevel = SignalLevel.Hot;
        else if (d <= warmDistance) newLevel = SignalLevel.Warm;
        else if (d <= lukewarmDistance) newLevel = SignalLevel.Lukewarm;
        else if (d <= coldDistance) newLevel = SignalLevel.Cold;
        else newLevel = SignalLevel.IceCold;
        SetLevel(newLevel);

        float now = Time.time;
        history.Enqueue((now, d));
        while (history.Count > 1 && now - history.Peek().time > trendWindow)
            history.Dequeue();

        double diff = history.Peek().dist - d;
        if (diff > trendDeadband) Trend = SignalTrend.Warmer;
        else if (diff < -trendDeadband) Trend = SignalTrend.Colder;
        else Trend = SignalTrend.Steady;
    }

    private void SetLevel(SignalLevel newLevel)
    {
        if (newLevel == Level) return;
        SignalLevel old = Level;
        Level = newLevel;
        LevelChanged?.Invoke(old, newLevel);
    }

    public static string Label(SignalLevel level)
    {
        switch (level)
        {
            case SignalLevel.IceCold: return "IJSKOUD";
            case SignalLevel.Cold: return "KOUD";
            case SignalLevel.Lukewarm: return "LAUW";
            case SignalLevel.Warm: return "WARM";
            case SignalLevel.Hot: return "HEET!";
            case SignalLevel.Locked: return "SIGNAAL GEVONDEN";
            default: return "GEEN SIGNAAL";
        }
    }
}
