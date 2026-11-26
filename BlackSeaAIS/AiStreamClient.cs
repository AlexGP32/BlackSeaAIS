using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

public class AiStreamClient : IDisposable
{
    private readonly ILogger<AiStreamClient> _logger;
    private ClientWebSocket _client = new();

    public AiStreamClient(ILogger<AiStreamClient> logger)
    {
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        // A new ClientWebSocket instance is required on every (re)connect —
        // a closed/aborted ClientWebSocket cannot be reopened.
        _client.Dispose();
        _client = new ClientWebSocket();
        var uri = new Uri("wss://stream.aisstream.io/v0/stream");
        await _client.ConnectAsync(uri, ct);
        _logger.LogInformation("Connected to AISStream. State: {State}", _client.State);
    }

    public async Task SubscribeAsync(string apiKey, CancellationToken ct = default)
    {
        // Black Sea bounding box (northwest corner to southeast corner).
        var subscription = new
        {
            APIKey = apiKey,
            BoundingBoxes = new[] { new[] { new[] { 46.850846, 27.165694 }, new[] { 40.768865, 41.843375 } } }
        };
        string json = JsonSerializer.Serialize(subscription);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        _logger.LogInformation("Subscribed to Black Sea bounding box.");
    }

    public async Task ReceiveMessagesAsync(DatabaseService db, CancellationToken ct = default)
    {
        var buffer = new byte[8192];

        while (_client.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogWarning("Server closed WebSocket connection.");
                break;
            }

            var info = Encoding.UTF8.GetString(buffer, 0, result.Count);
            AisMessage? aisMessage;

            try
            {
                aisMessage = JsonSerializer.Deserialize<AisMessage>(info);
            }
            catch (Exception ex)
            {
                // Skip malformed messages without breaking the receive loop.
                _logger.LogWarning(ex, "Failed to deserialize AIS message.");
                continue;
            }

            try
            {
                // Process only position reports that include a ship name.
                if (aisMessage?.MessageType == "PositionReport"
                    && aisMessage.Message?.PositionReport != null
                    && !string.IsNullOrWhiteSpace(aisMessage.MetaData?.ShipName?.Trim()))
                {
                    var pr = aisMessage.Message.PositionReport;
                    var meta = aisMessage.MetaData;

                    // Strip trailing " UTC" suffix so DateTime.Parse accepts the timestamp.
                    var timeUtc = meta.TimeUtc?.Replace(" UTC", "");
                    DateTime time = timeUtc != null ? DateTime.Parse(timeUtc) : DateTime.UtcNow;

                    await db.SavePosition(meta, pr, time, ct);
                    _logger.LogDebug("Saved position for {ShipName} ({MMSI})", meta.ShipName.Trim(), meta.MMSI);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Keep processing subsequent messages if a single DB write fails.
                _logger.LogError(ex, "Database error while saving position.");
            }
        }
    }

    public void Dispose() => _client.Dispose();
}