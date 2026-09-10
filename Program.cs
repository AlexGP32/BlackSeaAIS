using DotNetEnv;
using System.Data.SqlTypes;
using Npgsql;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.InteropServices;
using System.Linq.Expressions;
Env.Load();
var builder = new NpgsqlConnectionStringBuilder();
builder.Host = "db.meelehndrbaqdhmmvtip.supabase.co";
builder.Port = 5432;
builder.Database = "postgres";
builder.Username = "postgres";
builder.Password = Environment.GetEnvironmentVariable("PASSWORD");
var APIKey = Environment.GetEnvironmentVariable("AISSTREAM_API_KEY") ?? "";
await using var conn = new NpgsqlConnection(builder.ToString());
await conn.OpenAsync();
var db = new DatabaseService(conn);
var client = new AiStreamClient();
while (true)
{
    await client.ConnectAsync();
    await client.SubscribeAsync(APIKey);
    await client.ReceiveMessagesAsync(db);
    await Task.Delay(5000);
}