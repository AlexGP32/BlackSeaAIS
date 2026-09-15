using DotNetEnv;
using Npgsql;

Env.Load();
var geoService = new GeoValidationService();
var builder = new NpgsqlConnectionStringBuilder();
builder.Host = "db.meelehndrbaqdhmmvtip.supabase.co";
builder.Port = 5432;
builder.Database = "postgres";
builder.Username = "postgres";
builder.Password = Environment.GetEnvironmentVariable("PASSWORD");
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