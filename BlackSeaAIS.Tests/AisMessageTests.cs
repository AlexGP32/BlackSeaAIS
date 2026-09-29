using System.Text.Json;
using Xunit;

// Tests for deserializing AIS (Automatic Identification System) messages
// received as JSON and mapped onto the AisMessage object.
public class AisMessageTests
{
    // Happy path: complete JSON, all fields present.
    // Verifies correct mapping of every property, across all 3 levels
    // (AisMessage -> MetaData / Message -> PositionReport).
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

        // Level 1 checks: the top-level object
        Assert.NotNull(result);
        Assert.Equal("PositionReport", result.MessageType);

        // Level 2 checks: MetaData (ship identity + timestamp)
        Assert.NotNull(result.MetaData);
        Assert.Equal(271234567, result.MetaData.MMSI);          // unique ship ID
        Assert.Equal("TEST VESSEL", result.MetaData.ShipName);
        Assert.Equal("2026-09-24 10:15:30.000000000", result.MetaData.TimeUtc);

        // Level 3 checks: PositionReport (navigation/position data)
        Assert.NotNull(result.Message);
        Assert.NotNull(result.Message.PositionReport);
        Assert.Equal(12.5, result.Message.PositionReport.Sog);          // Speed over ground
        Assert.Equal(180.3, result.Message.PositionReport.Cog);         // Course over ground (direction of travel)
        Assert.Equal(179, result.Message.PositionReport.TrueHeading);   // Bow direction (compass heading)
        Assert.Equal(0, result.Message.PositionReport.NavigationalStatus); // 0 = "under way using engine"
        Assert.Equal(44.1733, result.Message.PositionReport.Latitude);
        Assert.Equal(28.6383, result.Message.PositionReport.Longitude);
    }

    // Many ships don't transmit ShipName on every AIS message (only
    // periodically, via ShipStaticData). This test verifies that a missing
    // field doesn't cause an error, it simply leaves the property null.
    [Fact]
    public void Deserialize_MissingShipName_ShipNameIsNull()
    {
        // many ships don't transmit ShipName on every message
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
        Assert.Null(result.MetaData.ShipName);       // field absent from JSON -> null, not an exception
        Assert.Equal(271234567, result.MetaData.MMSI);
    }

    // An AIS message can be a type other than PositionReport (e.g. static
    // ship data). In that case "Message" is empty, so PositionReport must
    // be null, without throwing during deserialization.
    [Fact]
    public void Deserialize_DifferentMessageType_PositionReportIsNull()
    {
        // a different AIS message type (e.g. ShipStaticData), no PositionReport
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
        Assert.NotNull(result.Message);           // the "Message" object exists...
        Assert.Null(result.Message.PositionReport); // ...but has no position data inside
    }

    // Robustness test: completely empty JSON. The object must still be
    // created without errors, with every property left null (nothing populated).
    [Fact]
    public void Deserialize_EmptyJson_AllPropertiesAreNull()
    {
        string json = "{}";

        var result = JsonSerializer.Deserialize<AisMessage>(json);

        Assert.NotNull(result);          // the object itself exists...
        Assert.Null(result.MessageType); // ...but no property is populated
        Assert.Null(result.MetaData);
        Assert.Null(result.Message);
    }

    // Verifies two edge-case values for TrueHeading (the ship's bow direction):
    // 0 = north (a valid boundary case) and 511 = special AIS code meaning
    // "heading not available" (not a real direction, just a conventional
    // sentinel value). Both must be read correctly, without error.
    [Theory]
    [InlineData(0)]      // 0 = heading due north, a valid value (boundary case)
    [InlineData(511)]    // 511 = special AIS value for "heading not available"
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

    // Verifies deserialization precision for a small negative decimal value
    // (longitude in the western hemisphere). Checks that the sign and fine
    // decimals (e.g. -0.001) aren't lost or rounded incorrectly during parsing.
    [Fact]
    public void Deserialize_NegativeLongitude_ParsesCorrectly()
    {
        // negative longitude (western hemisphere) - double precision test
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