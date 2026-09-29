using System.Data.Common;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

public class AiStreamClient
{
    private ClientWebSocket _client = new ClientWebSocket();

    public async Task ConnectAsync()
    {
        // A new ClientWebSocket instance is required on every (re)connect —
        // a closed/aborted ClientWebSocket cannot be reopened.
        _client = new ClientWebSocket();
        var uri = new Uri("wss://stream.aisstream.io/v0/stream");
        await _client.ConnectAsync(uri, CancellationToken.None);
        Console.WriteLine("Conectat: " + _client.State);
    }

    public async Task SubscribeAsync(string APIKey)
    {
        // BoundingBoxes format per aisstream.io: an array of [lat, lon] corner pairs
        // defining the region to receive AIS messages for. Here: the Black Sea area,
        // from the northwest corner to the southeast corner.
        var conexiune = new
        {
            APIKey,
            BoundingBoxes = new[] { new[] { new[] { 46.850846, 27.165694 }, new[] { 40.768865, 41.843375 } } }
        };
        string jsonstring = JsonSerializer.Serialize(conexiune);
        Console.WriteLine(jsonstring);
        var bytes = Encoding.UTF8.GetBytes(jsonstring);
        await _client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task ReceiveMessagesAsync(DatabaseService db)
    {
        var buffer = new byte[8192];
        Console.WriteLine("Stare inainte de while: " + _client.State);
        while (_client.State == WebSocketState.Open)
        {
            var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            var info = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine(info);
            AisMessage? aisMessage = null;
            try
            {
                aisMessage = JsonSerializer.Deserialize<AisMessage>(info);
            }
            catch (Exception ex)
            {
                // Malformed or unexpected message shape: skip this message but
                // keep the connection alive rather than crashing the whole loop.
                Console.WriteLine("Eroare la deserializare: " + ex.Message);
            }

            try
            {
                // Only process actual position reports that include a ship name.
                // Messages without a ShipName are skipped here (e.g. some position
                // reports arrive before the ship's static data has been received).
                if (aisMessage?.MessageType == "PositionReport" && aisMessage.Message?.PositionReport != null && !string.IsNullOrWhiteSpace(aisMessage.MetaData?.ShipName?.Trim() ?? ""))
                {
                    var pr = aisMessage.Message.PositionReport;
                    var meta = aisMessage.MetaData;

                    // aisstream.io timestamps arrive with a trailing " UTC" suffix
                    // that DateTime.Parse doesn't accept, so it's stripped first.
                    var timeUtc = meta?.TimeUtc?.Replace(" UTC", "");
                    DateTime time = timeUtc != null ? DateTime.Parse(timeUtc) : DateTime.UtcNow;
                    await db.UpsertShip(meta!, pr, time);
                    await db.InsertHistory(meta!, pr, time);
                }
            }
            catch (Exception ex)
            {
                // A DB failure for one message shouldn't kill the receive loop —
                // log it and keep processing the next incoming message.
                Console.WriteLine("Eroare la baza de date: " + ex.Message);
            }
        }
    }
}