using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

// Loads local .env file when running outside Docker (no-op if missing).
Env.Load();

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration["SUPABASE_CONNECTION_STRING"]
    ?? throw new InvalidOperationException("SUPABASE_CONNECTION_STRING is not set");

// Shared thread-safe connection pool for PostgreSQL.
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

builder.Services.AddSingleton<IGeoValidationService, GeoValidationService>();
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<AiStreamClient>();
builder.Services.AddHostedService<AisIngestionWorker>();

using var host = builder.Build();
await host.RunAsync();