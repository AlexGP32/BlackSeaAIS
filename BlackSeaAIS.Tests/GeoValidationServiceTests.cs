using NetTopologySuite.Geometries;
using Xunit;
using System.Collections.Generic;

public class GeoValidationServiceTests
{
    // Pătrat de "uscat" de la (0,0) la (1,1), în coordonate lon/lat
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

    [Fact]
    public void PointFarFromLand_IsNotOnLand()
    {
        var service = CreateServiceWithSquareLand();
        Assert.False(service.IsOnLand(lat: 5, lon: 5));
    }

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
        // la 0.001 grade de graniță, sub pragul de 0.002 -> nu se consideră "pe uscat"
        Assert.False(service.IsOnLand(lat: 0.001, lon: 0.5));
    }

    [Fact]
    public void PointWellInsideBoundary_AboveThreshold_IsOnLand()
    {
        var service = CreateServiceWithSquareLand();
        // la 0.01 grade de graniță, peste prag -> se consideră "pe uscat"
        Assert.True(service.IsOnLand(lat: 0.01, lon: 0.5));
    }

    [Fact]
    public void PointOutsideLand_NearBoundary_IsNotOnLand()
    {
        var service = CreateServiceWithSquareLand();
        Assert.False(service.IsOnLand(lat: -0.001, lon: 0.5));
    }

    [Fact]
    public void NoLandGeometries_IsNeverOnLand()
    {
        var service = new GeoValidationService(new List<Geometry>());
        Assert.False(service.IsOnLand(lat: 0.5, lon: 0.5));
    }

    [Fact]
    public void LatLonOrder_Matters()
    {
        // Dreptunghi: lon între 0 și 10, lat între 0 și 1 (asimetric, prinde inversarea)
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

        Assert.True(service.IsOnLand(lat: 0.5, lon: 5));   // în interior
        Assert.False(service.IsOnLand(lat: 5, lon: 0.5));  // ordine inversată -> în afară
    }

    [Theory]
    [InlineData(0.5, 0.5, true)]
    [InlineData(0.5, 0.999, false)]   // lângă marginea de est, sub prag
    [InlineData(0.999, 0.5, false)]   // lângă marginea de nord, sub prag
    public void PointsNearEveryEdge_RespectThreshold(double lat, double lon, bool expected)
    {
        var service = CreateServiceWithSquareLand();
        Assert.Equal(expected, service.IsOnLand(lat, lon));
    }
}