using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

public class GeoValidationService : IGeoValidationService
{
    private readonly List<Geometry> _landPolygons;
    private readonly double _insideLandThresholdDegrees;

    // Minimum distance (in degrees) a point must be from the polygon boundary
    // to count as "on land". This absorbs simplification noise in the coastline
    // data (1:50m resolution) so points right at the shoreline aren't flagged
    // as GPS spoofing false positives.
    private const double InsideLandThresholdDegrees = 0.002;

    // Public constructor used by the running application: loads the real
    // coastline data from disk.
    public GeoValidationService()
        : this(LoadPolygonsFromFile(Path.Combine(AppContext.BaseDirectory, "Data", "black_sea_land.geojson")))
    {
    }

    // Internal constructor used by unit tests: lets tests inject simple,
    // controlled polygons instead of depending on the real GeoJSON file.
    internal GeoValidationService(List<Geometry> landPolygons, double insideLandThresholdDegrees = InsideLandThresholdDegrees)
    {
        _landPolygons = landPolygons;
        _insideLandThresholdDegrees = insideLandThresholdDegrees;
    }

    // Reads and parses the land polygons from a GeoJSON file on disk.
    private static List<Geometry> LoadPolygonsFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var reader = new NetTopologySuite.IO.GeoJsonReader();
        var featureCollection = reader.Read<FeatureCollection>(json);

        var polygons = new List<Geometry>();
        foreach (var feature in featureCollection)
            polygons.Add(feature.Geometry);
        return polygons;
    }

    // Returns true if the given position falls inside a land polygon,
    // beyond the noise threshold from its boundary. Note: NetTopologySuite
    // uses (x, y) = (longitude, latitude) ordering, not (lat, lon).
    public bool IsOnLand(double lat, double lon)
    {
        var point = new Point(lon, lat);
        return _landPolygons.Any(g => g.Contains(point) && g.Boundary.Distance(point) > _insideLandThresholdDegrees);
    }
}