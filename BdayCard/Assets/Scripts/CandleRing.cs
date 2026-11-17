using System.Collections.Generic;
using UnityEngine;
 
//kaarsjes op de taart en bepaald de volgorde van de kaarsjes
public class CandleRing : MonoBehaviour
{
    [Tooltip("Alle kaarsjes. Volgorde onderling maakt niet uit voor een middenkaarsje (zie hieronder) — " +
             "voor de kaarsjes rond de rand telt wel hun relatieve volgorde in deze lijst als ring-volgorde.")]
    public CandleFlicker[] candles = new CandleFlicker[5];
 
    [Tooltip("Transform die ronddraait (bv. de CakePivot). Leeg = dit object zelf.")]
    public Transform cakeTransform;
 
    [Tooltip("Camera die bepaalt wat 'vooraan' is (het kaarsje het dichtst bij de camera is actief). " +
             "Leeg = Camera.main (in een AR-scene meestal de AR-camera, en dat is meestal wat je wil).")]
    public Camera referenceCamera;
 
    [Tooltip("Kaarsjes dichter dan deze afstand bij de draai-as worden beschouwd als een 'middenkaarsje': " +
             "daarvoor bestaat geen zinvolle voorkant, dus die doven pas nadat alle randkaarsjes uit zijn. " +
             "Sta het te laag/hoog? Verhoog dit als je middenkaars toch als randkaars behandeld wordt, " +
             "of verlaag het als een randkaars per ongeluk als middenkaars behandeld wordt.")]
    public float centerCandleRadiusThreshold = 0.05f;
 
    [Tooltip("Enkel gebruikt als er geen camera gevonden wordt: vaste lokale voorwaartse as van cakeTransform " +
             "als 'voorkant'. Let op: als cakeTransform hetzelfde object is dat ronddraait, verandert deze " +
             "richting mee met de kaarsjes en zal draaien dan NIETS uitmaken — gebruik dit dus enkel als noodgreep.")]
    public bool useForwardAxis = false;
 
    /// <summary>Het kaarsje dat nu het dichtst bij de voorkant (camera) staat en nog brandt, of een brandend
    /// middenkaarsje als er geen brandend randkaarsje meer is. Null als alles al uit is.</summary>
    public CandleFlicker ActiveCandle { get; private set; }
 
    void Reset()
    {
        cakeTransform = transform;
    }
 
    void Update()
    {
        UpdateActiveCandle();
    }
 
    /// <summary>True als dit kaarsje dicht genoeg bij de draai-as staat om als 'middenkaarsje' te tellen.</summary>
    bool IsCenterCandle(CandleFlicker c, Transform reference)
    {
        float radius = Vector3.ProjectOnPlane(c.transform.position - reference.position, reference.up).magnitude;
        return radius < centerCandleRadiusThreshold;
    }
 
    void UpdateActiveCandle()
    {
        Transform reference = cakeTransform != null ? cakeTransform : transform;
 
        Vector3 frontDir;
        Camera cam = referenceCamera != null ? referenceCamera : Camera.main;
        if (cam != null)
        {
            frontDir = Vector3.ProjectOnPlane(cam.transform.position - reference.position, reference.up);
        }
        else
        {
            frontDir = useForwardAxis ? reference.forward : -reference.forward;
        }
        if (frontDir.sqrMagnitude < 0.0001f)
        {
            ActiveCandle = null;
            return;
        }
        frontDir.Normalize();
 
        CandleFlicker bestOuter = null;
        float bestDot = -2f;
        CandleFlicker firstLitCenter = null;
 
        foreach (var c in candles)
        {
            if (c == null || !c.isLit) continue;
 
            if (IsCenterCandle(c, reference))
            {
                if (firstLitCenter == null) firstLitCenter = c;
                continue;
            }
 
            Vector3 dirToCandle = Vector3.ProjectOnPlane(c.transform.position - reference.position, reference.up);
            if (dirToCandle.sqrMagnitude < 0.0001f) continue;
            dirToCandle.Normalize();
 
            float dot = Vector3.Dot(frontDir, dirToCandle);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestOuter = c;
            }
        }
 
        // Enkel als er geen enkel randkaarsje meer brandt, wordt een middenkaarsje "actief".
        ActiveCandle = bestOuter != null ? bestOuter : firstLitCenter;
    }
 
    /// <summary>
    /// Geeft de nog brandende kaarsjes terug, in de volgorde waarin ze gedoofd moeten worden:
    /// eerst de randkaarsjes, beginnend bij het actieve (voorste) kaarsje en dan verder rond de
    /// ring, en pas daarna de middenkaarsjes (die geen 'voorkant' hebben).
    /// </summary>
    public List<CandleFlicker> GetBlowOrder()
    {
        var result = new List<CandleFlicker>();
        if (candles == null || candles.Length == 0)
            return result;
 
        Transform reference = cakeTransform != null ? cakeTransform : transform;
 
        // Twee groepen opbouwen in hun oorspronkelijke array-volgorde (ook de reeds gedoofde
        // kaarsjes blijven erin, zodat de index van ActiveCandle betrouwbaar terug te vinden is).
        var outerAll = new List<CandleFlicker>();
        var centerAll = new List<CandleFlicker>();
        foreach (var c in candles)
        {
            if (c == null) continue;
            if (IsCenterCandle(c, reference)) centerAll.Add(c);
            else outerAll.Add(c);
        }
 
        if (outerAll.Count > 0)
        {
            int startIndex = ActiveCandle != null ? outerAll.IndexOf(ActiveCandle) : -1;
            if (startIndex < 0) startIndex = 0;
 
            for (int i = 0; i < outerAll.Count; i++)
            {
                var c = outerAll[(startIndex + i) % outerAll.Count];
                if (c.isLit) result.Add(c);
            }
        }
 
        foreach (var c in centerAll)
            if (c.isLit) result.Add(c);
 
        return result;
    }
 
    public bool AllOut()
    {
        if (candles == null) return true;
        foreach (var c in candles)
            if (c != null && c.isLit) return false;
        return true;
    }
 
    /// <summary>Steek alle kaarsjes opnieuw aan (bv. voor je shake/dubbeltik-reset).</summary>
    public void RelightAll()
    {
        if (candles == null) return;
        foreach (var c in candles)
            if (c != null) c.Relight();
    }
}