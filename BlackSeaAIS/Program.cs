using DotNetEnv;
using Npgsql;

Env.Load();

var connectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
    ?? throw new InvalidOperationException("SUPABASE_CONNECTION_STRING is not set");
var APIKey = Environment.GetEnvironmentVariable("AISSTREAM_API_KEY") ?? "";

await using var dataSource = NpgsqlDataSource.Create(connectionString);

var geo = new GeoValidationService();
var db = new DatabaseService(dataSource, geo);
var client = new AiStreamClient();

while (true)
{
    try
    {
        await client.ConnectAsync();
        await client.SubscribeAsync(APIKey);
        await client.ReceiveMessagesAsync(db);
    }
    catch (Exception ex)
    {
        Console.WriteLine("Conexiune pierduta: " + ex.Message);
    }

    Console.WriteLine("Reconectare in 5 secunde...");
    await Task.Delay(5000);
}