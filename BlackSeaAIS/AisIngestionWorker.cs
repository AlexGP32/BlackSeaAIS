using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Background worker managing the AISStream WebSocket lifecycle and automatic reconnection.
public class AisIngestionWorker : BackgroundService
{
    private readonly AiStreamClient _client;
    private readonly DatabaseService _db;
    private readonly ILogger<AisIngestionWorker> _logger;
    private readonly string _apiKey;

    public AisIngestionWorker(
        AiStreamClient client,
        DatabaseService db,
        IConfiguration configuration,
        ILogger<AisIngestionWorker> logger)
    {
        _client = client;
        _db = db;
        _logger = logger;
        _apiKey = configuration["AISSTREAM_API_KEY"]
            ?? throw new InvalidOperationException("AISSTREAM_API_KEY is not set");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AIS ingestion worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _client.ConnectAsync(stoppingToken);
                await _client.SubscribeAsync(_apiKey, stoppingToken);
                await _client.ReceiveMessagesAsync(_db, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown triggered by host (Ctrl+C or Docker SIGTERM).
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WebSocket connection lost. Reconnecting in 5 seconds...");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("AIS ingestion worker stopped cleanly.");
    }
}