using DotNetEnv;
using Npgsql;

Env.Load();
var builder = new NpgsqlConnectionStringBuilder();
var connectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING");
var APIKey = Environment.GetEnvironmentVariable("AISSTREAM_API_KEY") ?? "";
await using var conn = new NpgsqlConnection(builder.ToString());
await conn.OpenAsync();
var geo = new GeoValidationService();
var db = new DatabaseService(conn, geo);
var client = new AiStreamClient();
while (true)
{
    await client.ConnectAsync();
    await client.SubscribeAsync(APIKey);
    await client.ReceiveMessagesAsync(db);
    await Task.Delay(5000);
}