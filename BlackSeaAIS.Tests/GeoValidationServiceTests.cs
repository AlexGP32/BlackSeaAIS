using NetTopologySuite.Geometries;
using Xunit;
using System.Collections.Generic;

public class GeoValidationServiceTests
{
    private static readonly GeometryFactory _factory = new GeometryFactory();

    // Helper: Creates a rectangular polygon (X=Lon, Y=Lat)
    private static Polygon CreateRectangle(double minLon, double minLat, double maxLon, double maxLat)
    {
        return _factory.CreatePolygon(new[]
        {
            new Coordinate(minLon, minLat),
            new Coordinate(maxLon, minLat),
            new Coordinate(maxLon, maxLat),
            new Coordinate(minLon, maxLat),
            new Coordinate(minLon, minLat)
        });
    }

    // Helper: Instantiates the service with a 1x1 land polygon and empty safe zones
    private static GeoValidationService CreateServiceWithSquareLand()
    {
        var square = CreateRectangle(0, 0, 1, 1);
        return new GeoValidationService(
            landPolygons: new List<Geometry> { square },
            safeZones: new List<Geometry>());
    }

    // --- IsOnLand Tests (Baseline) ---

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
        // 0.001 degrees from boundary, below 0.002 threshold -> ignores noise
        Assert.False(service.IsOnLand(lat: 0.001, lon: 0.5));
    }

    [Fact]
    public void PointWellInsideBoundary_AboveThreshold_IsOnLand()
    {
        var service = CreateServiceWithSquareLand();
        // 0.01 degrees from boundary, above threshold -> actual land hit
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
        var service = new GeoValidationService(new List<Geometry>(), new List<Geometry>());
        Assert.False(service.IsOnLand(lat: 0.5, lon: 0.5));
    }

    [Fact]
    public void LatLonOrder_Matters()
    {
        // Asymmetric rectangle catches lat/lon inversion bugs
        var rect = CreateRectangle(0, 0, 10, 1);
        var service = new GeoValidationService(new List<Geometry> { rect }, new List<Geometry>());

        Assert.True(service.IsOnLand(lat: 0.5, lon: 5));   // Inside
        Assert.False(service.IsOnLand(lat: 5, lon: 0.5));  // Swapped order -> Outside
    }

    [Theory]
    [InlineData(0.5, 0.5, true)]
    [InlineData(0.5, 0.999, false)]   // Near East edge, below threshold
    [InlineData(0.999, 0.5, false)]   // Near North edge, below threshold
    public void PointsNearEveryEdge_RespectThreshold(double lat, double lon, bool expected)
    {
        var service = CreateServiceWithSquareLand();
        Assert.Equal(expected, service.IsOnLand(lat, lon));
    }

    // --- IsInSafeZone Tests (New Hybrid Logic) ---

    [Fact]
    public void IsInSafeZone_PointInsidePolygon_ReturnsTrue()
    {
        // Arrange: Safe zone (e.g., Varna) between lon 27-28 and lat 43-44
        var safeZone = CreateRectangle(27.0, 43.0, 28.0, 44.0);
        var service = new GeoValidationService(
            landPolygons: new List<Geometry>(),
            safeZones: new List<Geometry> { safeZone });

        // Act & Assert: Point exactly in the middle
        Assert.True(service.IsInSafeZone(43.5, 27.5));
    }

    [Fact]
    public void IsInSafeZone_PointOutsidePolygon_ReturnsFalse()
    {
        var safeZone = CreateRectangle(27.0, 43.0, 28.0, 44.0);
        var service = new GeoValidationService(
            landPolygons: new List<Geometry>(),
            safeZones: new List<Geometry> { safeZone });

        Assert.False(service.IsInSafeZone(45.0, 29.0));
    }

    [Fact]
    public void IsInSafeZone_EmptySafeZonesList_ReturnsFalse()
    {
        // Simulates missing or empty safe_zones.geojson file
        var service = new GeoValidationService(new List<Geometry>(), new List<Geometry>());

        Assert.False(service.IsInSafeZone(43.5, 27.5));
    }
    [Fact]
    public void IsOnLand_PointInSafeZoneInsideLand_OverridesLandAndReturnsFalse()
    {
        // Arrange: create a land polygon that covers a huge area
        var landSquare = CreateRectangle(0, 0, 10, 10);

        // Create a safe zone polygon that is completely inside the land polygon
        var safeZoneChannel = CreateRectangle(2, 2, 4, 4);

        var service = new GeoValidationService(
            landPolygons: new List<Geometry> { landSquare },
            safeZones: new List<Geometry> { safeZoneChannel });

        // Coordinates that are inside both the land polygon and the safe zone
        double testLat = 3.0;
        double testLon = 3.0;

        // Act & Assert
        // 1. 
        Assert.True(service.IsInSafeZone(testLat, testLon));

        // 2. Verificăm că IsOnLand returnează FALSE (adică e pe apă, nu pe uscat, datorită priorității SafeZone)
        Assert.False(service.IsOnLand(testLat, testLon), "SafeZone-ul trebuie să aibă prioritate și să suprascrie poligonul de uscat.");
    }
}

