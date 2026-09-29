using Npgsql;

public class DatabaseService
{
    private readonly NpgsqlConnection _conn;
    private readonly IGeoValidationService _geoservice;

    public DatabaseService(NpgsqlConnection conn, IGeoValidationService geoservice)
    {
        _conn = conn;
        _geoservice = geoservice;
    }

    // Determines whether a reported position is plausible (i.e. not on land,
    // which would indicate GPS spoofing). Exposed as internal so it can be
    // unit tested independently of any database calls.
    internal bool DeterminePlausibility(PositionReport pr)
    {
        return !_geoservice.IsOnLand(pr.Latitude, pr.Longitude);
    }

    // Updates the ship's latest known state. Behavior depends on plausibility:
    // - Plausible position: full upsert, overwriting the ship's last known position.
    // - Implausible position (likely spoofed): only flags the ship as implausible
    //   and updates the timestamp, WITHOUT overwriting the last known good coordinates.
    //   This keeps the map showing the ship's last trustworthy location instead of
    //   a fake one on land.
    public async Task UpsertShip(MetaData meta, PositionReport pr, DateTime time)
    {
        bool isPlausible = DeterminePlausibility(pr);
        if (isPlausible)
        {
            var cmd = new NpgsqlCommand(@"INSERT INTO ships (mmsi, ship_name, last_latitude, last_longitude, sog, cog, true_heading, navigational_status, last_update, is_plausible) 
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
    is_plausible = EXCLUDED.is_plausible;",
     _conn);
            cmd.Parameters.AddWithValue("mmsi", meta.MMSI);
            cmd.Parameters.AddWithValue("name", meta.ShipName?.Trim() ?? "");
            cmd.Parameters.AddWithValue("lat", pr.Latitude);
            cmd.Parameters.AddWithValue("lon", pr.Longitude);
            cmd.Parameters.AddWithValue("sog", pr.Sog);
            cmd.Parameters.AddWithValue("cog", pr.Cog);
            cmd.Parameters.AddWithValue("heading", pr.TrueHeading);
            cmd.Parameters.AddWithValue("navstatus", pr.NavigationalStatus);
            cmd.Parameters.AddWithValue("updated", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("is_plausible", isPlausible);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            // last_latitude/last_longitude is not used here because we don't want to
            // overwrite the ship's last trustworthy position with a spoofed one.
            var cmd = new NpgsqlCommand(@"UPDATE ships SET is_plausible = @is_plausible, last_update = @updated WHERE mmsi = @mmsi;", _conn);
            cmd.Parameters.AddWithValue("mmsi", meta.MMSI);
            cmd.Parameters.AddWithValue("is_plausible", isPlausible);
            cmd.Parameters.AddWithValue("updated", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // Appends a new row to the full position history for this ship, regardless
    // of plausibility. Unlike UpsertShip, every received position is recorded here
    // (marked with is_plausible) so the full movement history stays available for
    // later analysis, even positions flagged as likely spoofed.
    public async Task InsertHistory(MetaData meta, PositionReport pr, DateTime time)
    {
        bool isPlausible = DeterminePlausibility(pr);
        var cmdHistory = new NpgsqlCommand(@"INSERT INTO position_history (mmsi, latitude, longitude, recorded_time, is_plausible) VALUES (@mmsi, @latitude, @longitude, @recorded_time, @is_plausible);", _conn);
        cmdHistory.Parameters.AddWithValue("mmsi", meta.MMSI);
        cmdHistory.Parameters.AddWithValue("latitude", pr.Latitude);
        cmdHistory.Parameters.AddWithValue("longitude", pr.Longitude);
        cmdHistory.Parameters.AddWithValue("recorded_time", time);
        cmdHistory.Parameters.AddWithValue("is_plausible", isPlausible);
        await cmdHistory.ExecuteNonQueryAsync();
    }
}