using System;

// Small geographic helpers. Kept separate from DatabaseService so they can be
// unit tested on their own, with no database or map data involved.
public static class GeoMath
{
    // Mean Earth radius expressed in nautical miles (1 nm = 1852 m).
    private const double EarthRadiusNm = 3440.065;

    // Great-circle distance between two lat/lon points (Haversine formula), in nautical miles.
    public static double DistanceNm(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusNm * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }

    // Speed in knots (nautical miles per hour) needed to cover the distance in the given time.
    // Returns 0 when the time span is zero or negative, because no speed can be computed then.
    public static double SpeedKnots(double distanceNm, TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero) return 0;
        return distanceNm / elapsed.TotalHours;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}