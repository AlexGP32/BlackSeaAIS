using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

public class GeoValidationService : IGeoValidationService
{
    private readonly List<Geometry> _landPolygons;
    private const double InsideLandThresholdDegrees = 0.002;

    public GeoValidationService()
        : this(LoadPolygonsFromFile(Path.Combine(AppContext.BaseDirectory, "Data", "black_sea_land.geojson")))
    {
    }

    internal GeoValidationService(List<Geometry> landPolygons)
    {
        _landPolygons = landPolygons;
    }

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

    public bool IsOnLand(double lat, double lon)
    {
        var point = new Point(lon, lat);
        return _landPolygons.Any(g => g.Contains(point) && g.Boundary.Distance(point) > InsideLandThresholdDegrees);
    }
}