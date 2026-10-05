using Moq;
using Npgsql;
using Xunit;

public class DatabaseServiceTests
{
    // Verifies that plausibility is always the logical inverse of IsOnLand,
    // regardless of the actual coordinates passed in.
    [Theory]
    [InlineData(true, false)]   // on land -> not plausible
    [InlineData(false, true)]   // at sea -> plausible
    public void DeterminePlausibility_ReturnsInverseOfIsOnLand(bool isOnLand, bool expected)
    {
        // Arrange: mock the geo service so we don't depend on real polygon data
        var geo = new Mock<IGeoValidationService>();
        geo.Setup(g => g.IsOnLand(It.IsAny<double>(), It.IsAny<double>()))
           .Returns(isOnLand);

        // Connection is never actually opened/used by DeterminePlausibility,
        // so a bare NpgsqlConnection instance is fine here.
        var dummyDataSource = NpgsqlDataSource.Create("Host=dummy");
        var db = new DatabaseService(dummyDataSource, geo.Object);
        var pr = new PositionReport { Latitude = 44.0, Longitude = 28.0 };

        // Act & Assert
        Assert.Equal(expected, db.DeterminePlausibility(pr));
    }

    // Verifies that the exact latitude/longitude from the PositionReport
    // are forwarded to the geo service unchanged (no swapping, rounding, etc.).
    [Fact]
    public void DeterminePlausibility_PassesCoordinatesToGeoService()
    {
        // Arrange
        var geo = new Mock<IGeoValidationService>();
        geo.Setup(g => g.IsOnLand(44.0, 28.0)).Returns(false);

        var dummyDataSource = NpgsqlDataSource.Create("Host=dummy");
        var db = new DatabaseService(dummyDataSource, geo.Object);

        // Act
        var result = db.DeterminePlausibility(new PositionReport { Latitude = 44.0, Longitude = 28.0 });

        // Assert
        Assert.True(result);
        // Confirms IsOnLand was called exactly once with the exact coordinates,
        // catching bugs like accidentally swapping lat/lon.
        geo.Verify(g => g.IsOnLand(44.0, 28.0), Times.Once);
    }
}