using System;
using UnityEngine;

// Geo-wiskunde in double voor precisie op meterniveau.
public static class GeoUtils
{
    public const double EarthRadius = 6371000.0; // meter

    public static double ToRad(double deg) => deg * Math.PI / 180.0;
    public static double ToDeg(double rad) => rad * 180.0 / Math.PI;

    // Haversine-afstand in meter.
    public static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadius * c;
    }

    // Kompasrichting van punt 1 naar 2 (0 = noord).
    public static double Bearing(double lat1, double lon1, double lat2, double lon2)
    {
        double phi1 = ToRad(lat1);
        double phi2 = ToRad(lat2);
        double dLon = ToRad(lon2 - lon1);

        double y = Math.Sin(dLon) * Math.Cos(phi2);
        double x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(dLon);

        return (ToDeg(Math.Atan2(y, x)) + 360.0) % 360.0;
    }

    // Positie t.o.v. oorsprong in meter (x = oost, y = noord), met cos(lat)-correctie.
    public static Vector2 ToLocalMeters(double originLat, double originLon, double lat, double lon)
    {
        double north = ToRad(lat - originLat) * EarthRadius;
        double east = ToRad(lon - originLon) * EarthRadius * Math.Cos(ToRad(originLat));
        return new Vector2((float)east, (float)north);
    }

    // Verschuif een GPS-punt een aantal meter noord/oost.
    public static void OffsetByMeters(double lat, double lon, double northMeters, double eastMeters,
                                      out double newLat, out double newLon)
    {
        newLat = lat + ToDeg(northMeters / EarthRadius);
        newLon = lon + ToDeg(eastMeters / (EarthRadius * Math.Cos(ToRad(lat))));
    }
}
