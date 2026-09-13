using NetTopologySuite.Geometries;
using NetTopologySuite.Features;
using System.Linq;

public class GeoValidationService
{
    private readonly List<Geometry> _landPolygons;
    private const double LandThresholdDegrees = 0.01;

    public GeoValidationService()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "black_sea_land.geojson");
        var json = File.ReadAllText(path);
        var reader = new NetTopologySuite.IO.GeoJsonReader();
        var featureCollection = reader.Read<FeatureCollection>(json);

        _landPolygons = new List<Geometry>();
        foreach (var feature in featureCollection)
            _landPolygons.Add(feature.Geometry);
    }

    public bool IsOnLand(double lat, double lon)
    {
        var point = new Point(lon, lat);
        return _landPolygons.Any(g => g.Distance(point) < LandThresholdDegrees);
    }
}