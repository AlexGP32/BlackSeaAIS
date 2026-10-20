using System;
using Moq;
using Npgsql;
using Xunit;

public class DatabaseServiceTests
{
    private readonly Mock<IGeoValidationService> _geoMock;
    private readonly NpgsqlDataSource _dummyDataSource;
    private readonly DatabaseService _service;
    private readonly MetaData _testMeta;

    public DatabaseServiceTests()
    {
        _geoMock = new Mock<IGeoValidationService>();
        // Dummy data source avoids actual DB connections for logic-only tests
        _dummyDataSource = NpgsqlDataSource.Create("Host=dummy");
        _service = new DatabaseService(_dummyDataSource, _geoMock.Object);
        _testMeta = new MetaData { MMSI = 123456789, ShipName = "TEST SHIP" };
    }

    // Baseline: Plausibility strictly inverts IsOnLand when evaluated without history.
    [Theory]
    [InlineData(true, false)]   // On land -> not plausible
    [InlineData(false, true)]   // At sea -> plausible
    public void DeterminePlausibility_ReturnsInverseOfIsOnLand(bool isOnLand, bool expected)
    {
        // Arrange
        _geoMock.Setup(g => g.IsOnLand(It.IsAny<double>(), It.IsAny<double>())).Returns(isOnLand);
        var pr = new PositionReport { Latitude = 44.0, Longitude = 28.0 };

        // Act & Assert
        Assert.Equal(expected, _service.DeterminePlausibility(pr));
    }

    // Baseline: Ensures exact coordinate forwarding to the geo service (prevents lat/lon swapping).
    [Fact]
    public void DeterminePlausibility_PassesCoordinatesToGeoService()
    {
        // Arrange
        _geoMock.Setup(g => g.IsOnLand(44.0, 28.0)).Returns(false);
        var pr = new PositionReport { Latitude = 44.0, Longitude = 28.0 };

        // Act
        var result = _service.DeterminePlausibility(pr);

        // Assert
        Assert.True(result);
        _geoMock.Verify(g => g.IsOnLand(44.0, 28.0), Times.Once);
    }

    // Hybrid: Minor GPS fluctuations under 0.1 Nm should be ignored to prevent false high-speed spikes.
    [Fact]
    public void DeterminePlausibility_GpsNoise_ShouldReturnTrue()
    {
        // Arrange
        var t1 = DateTime.UtcNow;
        var t2 = t1.AddSeconds(5);

        _geoMock.Setup(g => g.IsOnLand(43.0, 30.0)).Returns(false);
        _service.RememberAccepted(_testMeta, new PositionReport { Latitude = 43.0, Longitude = 30.0 }, t1);

        var pr2 = new PositionReport { Latitude = 43.0001, Longitude = 30.0001 };

        // Act
        bool result = _service.DeterminePlausibility(_testMeta, pr2, t2);

        // Assert
        Assert.True(result);
    }

    // Hybrid: A 65 Nm jump in 1 minute must be rejected as spoofing, bypassing map checks.
    [Fact]
    public void DeterminePlausibility_ImpossibleSpeed_ShouldReturnFalse()
    {
        // Arrange
        var t1 = DateTime.UtcNow;
        var t2 = t1.AddMinutes(1);

        _service.RememberAccepted(_testMeta, new PositionReport { Latitude = 44.17, Longitude = 28.65 }, t1);
        var pr2 = new PositionReport { Latitude = 43.20, Longitude = 27.91 };

        // Act
        bool result = _service.DeterminePlausibility(_testMeta, pr2, t2);

        // Assert
        Assert.False(result);
    }

    // Hybrid: Valid kinematic speed (10 knots) should override low-res map land hits (e.g., Varna canal).
    [Fact]
    public void DeterminePlausibility_VarnaBypass_ShouldReturnTrueEvenOnLand()
    {
        // Arrange
        var t1 = DateTime.UtcNow;
        var t2 = t1.AddMinutes(30);

        _geoMock.Setup(g => g.IsOnLand(43.20, 28.0)).Returns(false);
        _service.RememberAccepted(_testMeta, new PositionReport { Latitude = 43.20, Longitude = 28.0 }, t1);

        var pr2 = new PositionReport { Latitude = 43.20, Longitude = 27.88 };
        _geoMock.Setup(g => g.IsOnLand(43.20, 27.88)).Returns(true); // Map resolution false-positive

        // Act
        bool result = _service.DeterminePlausibility(_testMeta, pr2, t2);

        // Assert
        Assert.True(result);
    }
}