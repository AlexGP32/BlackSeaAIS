using System.Text.Json;
using Xunit;

public class AisMessageTests
{
    [Fact]
    public void Deserialize_FullPositionReport_ParsesAllFieldsCorrectly()
    {
        string json = """
        {
            "MessageType": "PositionReport",
            "MetaData": {
                "time_utc": "2026-09-24 10:15:30.000000000",
                "MMSI": 271234567,
                "ShipName": "TEST VESSEL"
            },
            "Message": {
                "PositionReport": {
                    "Sog": 12.5,
                    "Cog": 180.3,
                    "TrueHeading": 179,
                    "NavigationalStatus": 0,
                    "Latitude": 44.1733,
                    "Longitude": 28.6383
                }
            }
        }
        """;

        var result = JsonSerializer.Deserialize<AisMessage>(json);

        Assert.NotNull(result);
        Assert.Equal("PositionReport", result.MessageType);

        Assert.NotNull(result.MetaData);
        Assert.Equal(271234567, result.MetaData.MMSI);
        Assert.Equal("TEST VESSEL", result.MetaData.ShipName);
        Assert.Equal("2026-09-24 10:15:30.000000000", result.MetaData.TimeUtc);

        Assert.NotNull(result.Message);
        Assert.NotNull(result.Message.PositionReport);
        Assert.Equal(12.5, result.Message.PositionReport.Sog);
        Assert.Equal(180.3, result.Message.PositionReport.Cog);
        Assert.Equal(179, result.Message.PositionReport.TrueHeading);
        Assert.Equal(0, result.Message.PositionReport.NavigationalStatus);
        Assert.Equal(44.1733, result.Message.PositionReport.Latitude);
        Assert.Equal(28.6383, result.Message.PositionReport.Longitude);
    }

    [Fact]
    public void Deserialize_MissingShipName_ShipNameIsNull()
    {
        // multe nave nu transmit ShipName pe fiecare mesaj
        string json = """
        {
            "MessageType": "PositionReport",
            "MetaData": {
                "time_utc": "2026-09-24 10:15:30.000000000",
                "MMSI": 271234567
            },
            "Message": {
                "PositionReport": {
                    "Sog": 0,
                    "Cog": 0,
                    "TrueHeading": 511,
                    "NavigationalStatus": 5,
                    "Latitude": 44.0,
                    "Longitude": 28.0
                }
            }
        }
        """;

        var result = JsonSerializer.Deserialize<AisMessage>(json);

        Assert.NotNull(result);
        Assert.NotNull(result.MetaData);
        Assert.Null(result.MetaData.ShipName);
        Assert.Equal(271234567, result.MetaData.MMSI);
    }

    [Fact]
    public void Deserialize_DifferentMessageType_PositionReportIsNull()
    {
        // alt tip de mesaj AIS (ex. ShipStaticData), fără PositionReport
        string json = """
        {
            "MessageType": "ShipStaticData",
            "MetaData": {
                "time_utc": "2026-09-24 10:15:30.000000000",
                "MMSI": 271234567,
                "ShipName": "TEST VESSEL"
            },
            "Message": {}
        }
        """;

        var result = JsonSerializer.Deserialize<AisMessage>(json);

        Assert.NotNull(result);
        Assert.Equal("ShipStaticData", result.MessageType);
        Assert.NotNull(result.Message);
        Assert.Null(result.Message.PositionReport);
    }

    [Fact]
    public void Deserialize_EmptyJson_AllPropertiesAreNull()
    {
        string json = "{}";

        var result = JsonSerializer.Deserialize<AisMessage>(json);

        Assert.NotNull(result);
        Assert.Null(result.MessageType);
        Assert.Null(result.MetaData);
        Assert.Null(result.Message);
    }

    [Theory]
    [InlineData(0)]      // sub navă, la Ecuator/Greenwich - caz de graniță valid
    [InlineData(511)]    // 511 = valoare specială AIS pentru "heading indisponibil"
    public void Deserialize_TrueHeadingEdgeValues_ParsesWithoutError(int heading)
    {
        string json = $$"""
        {
            "MessageType": "PositionReport",
            "MetaData": { "time_utc": "2026-09-24T10:00:00Z", "MMSI": 271000001 },
            "Message": {
                "PositionReport": {
                    "Sog": 0, "Cog": 0, "TrueHeading": {{heading}},
                    "NavigationalStatus": 0, "Latitude": 44.0, "Longitude": 28.0
                }
            }
        }
        """;

        var result = JsonSerializer.Deserialize<AisMessage>(json);

        Assert.NotNull(result?.Message?.PositionReport);
        Assert.Equal(heading, result.Message.PositionReport.TrueHeading);
    }

    [Fact]
    public void Deserialize_NegativeLongitude_ParsesCorrectly()
    {
        // longitudine negativă (emisferă vestică) - test de precizie double
        string json = """
        {
            "MessageType": "PositionReport",
            "MetaData": { "time_utc": "2026-09-24T10:00:00Z", "MMSI": 271000002 },
            "Message": {
                "PositionReport": {
                    "Sog": 5.2, "Cog": 90.0, "TrueHeading": 90,
                    "NavigationalStatus": 0, "Latitude": 41.0082, "Longitude": -0.001
                }
            }
        }
        """;

        var result = JsonSerializer.Deserialize<AisMessage>(json);

        Assert.NotNull(result?.Message?.PositionReport);
        Assert.Equal(-0.001, result.Message.PositionReport.Longitude);
    }
}