using Moq;
using Npgsql;
using Xunit;

public class DatabaseServiceTests
{
    [Theory]
    [InlineData(true, false)]   // pe uscat -> neplauzibil
    [InlineData(false, true)]   // pe mare -> plauzibil
    public void DeterminePlausibility_ReturnsInverseOfIsOnLand(bool isOnLand, bool expected)
    {
        var geo = new Mock<IGeoValidationService>();
        geo.Setup(g => g.IsOnLand(It.IsAny<double>(), It.IsAny<double>()))
           .Returns(isOnLand);

        var db = new DatabaseService(new NpgsqlConnection(), geo.Object);
        var pr = new PositionReport { Latitude = 44.0, Longitude = 28.0 };

        Assert.Equal(expected, db.DeterminePlausibility(pr));
    }

    [Fact]
    public void DeterminePlausibility_PassesCoordinatesToGeoService()
    {
        var geo = new Mock<IGeoValidationService>();
        geo.Setup(g => g.IsOnLand(44.0, 28.0)).Returns(false);

        var db = new DatabaseService(new NpgsqlConnection(), geo.Object);

        var result = db.DeterminePlausibility(new PositionReport { Latitude = 44.0, Longitude = 28.0 });

        Assert.True(result);
        geo.Verify(g => g.IsOnLand(44.0, 28.0), Times.Once);
    }
}