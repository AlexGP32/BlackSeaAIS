using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Xunit.Abstractions;

public class ThresholdTuningTests_Agamemnon
{
    private readonly ITestOutputHelper _output;

    public ThresholdTuningTests_Agamemnon(ITestOutputHelper output)
    {
        _output = output;
    }

    // Real position reported by AGAMEMNON (MMSI 207133000), flagged as
    // implausible (FALSE) in the database. Investigates WHY: prints the
    // IsOnLand result and, ideally, the distance to the nearest land
    // polygon edge, so we can tell whether this is a genuine detection
    // (ship really on/very near land) or a threshold artifact.
    [Fact]
    public void AgamemnonPosition_InvestigateFlaggedResult()
    {
        double lat = 43.2018866666667;
        double lon = 27.88653;

        var geo = new GeoValidationService(LoadRealPolygons()); // default threshold, 0.002

        bool result = geo.IsOnLand(lat, lon);

        _output.WriteLine($"AGAMEMNON ({lat}, {lon}): IsOnLand = {result}");

        // If GeoValidationService exposes a way to get the distance to the
        // nearest land geometry (e.g. a DistanceToNearestLand method), call
        // it here too, to see exactly how close/far the point is from the
        // 0.002 threshold. This is the key number for deciding if the
        // threshold needs tuning.
        // Example (uncomment if such a method exists):
        // double distance = geo.DistanceToNearestLand(lat, lon);
        // _output.WriteLine($"Distance to nearest land polygon: {distance} degrees");
    }

    private static List<NetTopologySuite.Geometries.Geometry> LoadRealPolygons()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "black_sea_land.geojson");
        var json = File.ReadAllText(path);
        var reader = new NetTopologySuite.IO.GeoJsonReader();
        var featureCollection = reader.Read<NetTopologySuite.Features.FeatureCollection>(json);

        var polygons = new List<NetTopologySuite.Geometries.Geometry>();
        foreach (var feature in featureCollection)
            polygons.Add(feature.Geometry);
        return polygons;
    }
}