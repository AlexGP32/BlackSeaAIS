using NetTopologySuite.Geometries;
using Xunit;
using System.Collections.Generic;

public class GeoValidationServiceTests
{
    // A square "land" polygon from (0,0) to (1,1), in lon/lat coordinates
    private static GeoValidationService CreateServiceWithSquareLand()
    {
        var factory = new GeometryFactory();
        var square = factory.CreatePolygon(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(1, 0),
            new Coordinate(1, 1),
            new Coordinate(0, 1),
            new Coordinate(0, 0)
        });

        return new GeoValidationService(new List<Geometry> { square });
    }

    // A point well outside and far from the land polygon should not be flagged as on land.
    [Fact]
    public void PointFarFromLand_IsNotOnLand()
    {
        var service = CreateServiceWithSquareLand();
        Assert.False(service.IsOnLand(lat: 5, lon: 5));
    }

    // A point at the center of the land polygon, well past the threshold, should be flagged as on land.
    [Fact]
    public void PointDeepInsideLand_IsOnLand()
    {
        var service = CreateServiceWithSquareLand();
        Assert.True(service.IsOnLand(lat: 0.5, lon: 0.5));
    }

    [Fact]
    public void PointJustInsideBoundary_BelowThreshold_IsNotOnLand()
    {
        var service = CreateServiceWithSquareLand();
        // 0.001 degrees from the boundary, below the 0.002 threshold -> not considered "on land"
        Assert.False(service.IsOnLand(lat: 0.001, lon: 0.5));
    }

    [Fact]
    public void PointWellInsideBoundary_AboveThreshold_IsOnLand()
    {
        var service = CreateServiceWithSquareLand();
        // 0.01 degrees from the boundary, above the threshold -> considered "on land"
        Assert.True(service.IsOnLand(lat: 0.01, lon: 0.5));
    }

    // A point just outside the polygon, near the boundary, should not be flagged as on land.
    [Fact]
    public void PointOutsideLand_NearBoundary_IsNotOnLand()
    {
        var service = CreateServiceWithSquareLand();
        Assert.False(service.IsOnLand(lat: -0.001, lon: 0.5));
    }

    // With an empty list of land polygons, no point should ever be considered on land.
    [Fact]
    public void NoLandGeometries_IsNeverOnLand()
    {
        var service = new GeoValidationService(new List<Geometry>());
        Assert.False(service.IsOnLand(lat: 0.5, lon: 0.5));
    }

    [Fact]
    public void LatLonOrder_Matters()
    {
        // Rectangle: lon between 0 and 10, lat between 0 and 1 (asymmetric, catches lat/lon swaps)
        var factory = new GeometryFactory();
        var rect = factory.CreatePolygon(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 1),
            new Coordinate(0, 1),
            new Coordinate(0, 0)
        });
        var service = new GeoValidationService(new List<Geometry> { rect });

        Assert.True(service.IsOnLand(lat: 0.5, lon: 5));   // inside
        Assert.False(service.IsOnLand(lat: 5, lon: 0.5));  // swapped order -> outside
    }

    [Theory]
    [InlineData(0.5, 0.5, true)]
    [InlineData(0.5, 0.999, false)]   // near the east edge, below threshold
    [InlineData(0.999, 0.5, false)]   // near the north edge, below threshold
    public void PointsNearEveryEdge_RespectThreshold(double lat, double lon, bool expected)
    {
        var service = CreateServiceWithSquareLand();
        Assert.Equal(expected, service.IsOnLand(lat, lon));
    }
}