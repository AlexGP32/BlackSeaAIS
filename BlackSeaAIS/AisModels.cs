using System.Text.Json.Serialization;

public class AisMessage
{
    [JsonPropertyName("MessageType")]
    public string? MessageType { get; set; }

    [JsonPropertyName("MetaData")]
    public MetaData? MetaData { get; set; }

    [JsonPropertyName("Message")]
    public MessageWrapper? Message { get; set; }
}

public class MetaData
{
    [JsonPropertyName("time_utc")]
    public string? TimeUtc { get; set; }

    [JsonPropertyName("MMSI")]
    public long MMSI { get; set; }

    [JsonPropertyName("ShipName")]
    public string? ShipName { get; set; }
}

public class MessageWrapper
{
    [JsonPropertyName("PositionReport")]
    public PositionReport? PositionReport { get; set; }
}

public class PositionReport
{
    [JsonPropertyName("Sog")]
    public double Sog { get; set; }

    [JsonPropertyName("Cog")]
    public double Cog { get; set; }

    [JsonPropertyName("TrueHeading")]
    public int TrueHeading { get; set; }

    [JsonPropertyName("NavigationalStatus")]
    public int NavigationalStatus { get; set; }

    [JsonPropertyName("Latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("Longitude")]
    public double Longitude { get; set; }
}