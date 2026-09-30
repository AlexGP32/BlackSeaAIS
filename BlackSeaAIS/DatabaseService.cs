using Npgsql;

public class DatabaseService
{
    // NpgsqlDataSource is thread-safe and pools connections, so each operation
    // opens its own short-lived connection instead of sharing a single one.
    private readonly NpgsqlDataSource _dataSource;
    private readonly IGeoValidationService _geoservice;

    public DatabaseService(NpgsqlDataSource dataSource, IGeoValidationService geoservice)
    {
        _dataSource = dataSource;
        _geoservice = geoservice;
    }

    // A position is plausible if it is not on land; a ship reporting a position
    // on land usually indicates GPS spoofing or bad data.
    // Internal so it can be unit tested without touching the database.
    internal bool DeterminePlausibility(PositionReport pr)
    {
        return !_geoservice.IsOnLand(pr.Latitude, pr.Longitude);
    }

    // Npgsql requires DateTime values with Kind=Utc for timestamptz columns.
    private static DateTime AsUtc(DateTime time) =>
        time.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(time, DateTimeKind.Utc)
            : time.ToUniversalTime();

    // Entry point for every received position report. Plausibility is computed
    // once (the geographic check can be expensive) and reused by both writes.
    public async Task SavePosition(MetaData meta, PositionReport pr, DateTime time)
    {
        bool isPlausible = DeterminePlausibility(pr);
        await UpsertShip(meta, pr, time, isPlausible);
        await InsertHistory(meta, pr, time, isPlausible);
    }

    // Updates the ship's current state (table "ships"):
    // - Plausible position: full upsert, overwriting the last known position.
    // - Implausible position: only flags the ship and updates the timestamp,
    //   keeping the last trustworthy coordinates so the map never shows a fake
    //   location. If the ship has no row yet, nothing is updated, so it appears
    //   only after its first plausible report.
    public async Task UpsertShip(MetaData meta, PositionReport pr, DateTime time, bool isPlausible)
    {
        var updated = AsUtc(time);

        if (isPlausible)
        {
            await using var cmd = _dataSource.CreateCommand(@"
                INSERT INTO ships (mmsi, ship_name, last_latitude, last_longitude, sog, cog, true_heading, navigational_status, last_update, is_plausible)
                VALUES (@mmsi, @name, @lat, @lon, @sog, @cog, @heading, @navstatus, @updated, @is_plausible)
                ON CONFLICT (mmsi)
                DO UPDATE SET
                    ship_name = EXCLUDED.ship_name,
                    last_latitude = EXCLUDED.last_latitude,
                    last_longitude = EXCLUDED.last_longitude,
                    sog = EXCLUDED.sog,
                    cog = EXCLUDED.cog,
                    true_heading = EXCLUDED.true_heading,
                    navigational_status = EXCLUDED.navigational_status,
                    last_update = EXCLUDED.last_update,
                    is_plausible = EXCLUDED.is_plausible;");
            cmd.Parameters.AddWithValue("mmsi", meta.MMSI);
            cmd.Parameters.AddWithValue("name", meta.ShipName?.Trim() ?? "");
            cmd.Parameters.AddWithValue("lat", pr.Latitude);
            cmd.Parameters.AddWithValue("lon", pr.Longitude);
            cmd.Parameters.AddWithValue("sog", pr.Sog);
            cmd.Parameters.AddWithValue("cog", pr.Cog);
            cmd.Parameters.AddWithValue("heading", pr.TrueHeading);
            cmd.Parameters.AddWithValue("navstatus", pr.NavigationalStatus);
            cmd.Parameters.AddWithValue("updated", updated);
            cmd.Parameters.AddWithValue("is_plausible", isPlausible);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            await using var cmd = _dataSource.CreateCommand(
                "UPDATE ships SET is_plausible = @is_plausible, last_update = @updated WHERE mmsi = @mmsi;");
            cmd.Parameters.AddWithValue("mmsi", meta.MMSI);
            cmd.Parameters.AddWithValue("is_plausible", isPlausible);
            cmd.Parameters.AddWithValue("updated", updated);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // Appends every received position to the full history (table "position_history"),
    // flagged with its plausibility, so spoofed positions remain available for analysis.
    public async Task InsertHistory(MetaData meta, PositionReport pr, DateTime time, bool isPlausible)
    {
        await using var cmd = _dataSource.CreateCommand(
            "INSERT INTO position_history (mmsi, latitude, longitude, recorded_time, is_plausible) " +
            "VALUES (@mmsi, @latitude, @longitude, @recorded_time, @is_plausible);");
        cmd.Parameters.AddWithValue("mmsi", meta.MMSI);
        cmd.Parameters.AddWithValue("latitude", pr.Latitude);
        cmd.Parameters.AddWithValue("longitude", pr.Longitude);
        cmd.Parameters.AddWithValue("recorded_time", AsUtc(time));
        cmd.Parameters.AddWithValue("is_plausible", isPlausible);
        await cmd.ExecuteNonQueryAsync();
    }
}