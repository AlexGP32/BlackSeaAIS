using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

public class GeoValidationService : IGeoValidationService
{
    private readonly List<Geometry> _landPolygons;
    private readonly List<Geometry> _safeZones;
    private readonly double _insideLandThresholdDegrees;

    // Minimum distance (in degrees) a point must be from the polygon boundary
    // to count as "on land". Absorbs coastline data simplification noise.
    private const double InsideLandThresholdDegrees = 0.002;

    public GeoValidationService()
        : this(
            LoadPolygonsFromFile(Path.Combine(AppContext.BaseDirectory, "Data", "black_sea_land.geojson")),
            LoadPolygonsFromFile(Path.Combine(AppContext.BaseDirectory, "Data", "safe_zones.geojson")))
    {
    }

    internal GeoValidationService(
        List<Geometry> landPolygons,
        List<Geometry> safeZones,
        double insideLandThresholdDegrees = InsideLandThresholdDegrees)
    {
        _landPolygons = landPolygons ?? new List<Geometry>();
        _safeZones = safeZones ?? new List<Geometry>();
        _insideLandThresholdDegrees = insideLandThresholdDegrees;
    }

    private static List<Geometry> LoadPolygonsFromFile(string path)
    {
        if (!File.Exists(path))
            return new List<Geometry>();

        var json = File.ReadAllText(path);
        var reader = new NetTopologySuite.IO.GeoJsonReader();
        var featureCollection = reader.Read<FeatureCollection>(json);

        return featureCollection.Select(f => f.Geometry).ToList();
    }

    public bool IsOnLand(double lat, double lon)
    {
        if (_landPolygons.Count == 0) return false;

        var point = new Point(lon, lat);
        return _landPolygons.Any(g => g.Contains(point) && g.Boundary.Distance(point) > _insideLandThresholdDegrees);
    }

    public bool IsInSafeZone(double lat, double lon)
    {
        if (_safeZones.Count == 0) return false;

        var point = new Point(lon, lat);
        // Safe zones are manually drawn strict boundaries; no noise threshold is needed here.
        return _safeZones.Any(g => g.Contains(point));
    }
}