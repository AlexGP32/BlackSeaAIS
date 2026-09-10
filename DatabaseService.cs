using Npgsql;

public class DatabaseService

{
    private readonly NpgsqlConnection _conn;

    public DatabaseService(NpgsqlConnection conn)
    {
        _conn = conn;
    }

    public async Task UpsertShip(MetaData meta, PositionReport pr, DateTime time)
    {
        var cmd = new NpgsqlCommand(@"INSERT INTO ships (mmsi, ship_name, last_latitude, last_longitude, sog, cog, true_heading, navigational_status, last_update) 
    VALUES (@mmsi, @name, @lat, @lon, @sog, @cog, @heading, @navstatus, @updated) 
    ON CONFLICT (mmsi) 
    DO UPDATE SET
    ship_name = EXCLUDED.ship_name,
    last_latitude = EXCLUDED.last_latitude,
    last_longitude = EXCLUDED.last_longitude,
    sog = EXCLUDED.sog,
    cog = EXCLUDED.cog,
    true_heading = EXCLUDED.true_heading,
    navigational_status = EXCLUDED.navigational_status,
    last_update = EXCLUDED.last_update;", _conn);
        cmd.Parameters.AddWithValue("mmsi", meta.MMSI);
        cmd.Parameters.AddWithValue("name", meta.ShipName?.Trim() ?? "");
        cmd.Parameters.AddWithValue("lat", pr.Latitude);
        cmd.Parameters.AddWithValue("lon", pr.Longitude);
        cmd.Parameters.AddWithValue("sog", pr.Sog);
        cmd.Parameters.AddWithValue("cog", pr.Cog);
        cmd.Parameters.AddWithValue("heading", pr.TrueHeading);
        cmd.Parameters.AddWithValue("navstatus", pr.NavigationalStatus);
        cmd.Parameters.AddWithValue("updated", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync();
    }
    public async Task InsertHistory(MetaData meta, PositionReport pr, DateTime time)
    {
        var cmdHistory = new NpgsqlCommand(@"INSERT INTO position_history (mmsi, latitude, longitude, recorded_time) VALUES (@mmsi, @latitude, @longitude, @recorded_time);", _conn);
        cmdHistory.Parameters.AddWithValue("mmsi", meta.MMSI);
        cmdHistory.Parameters.AddWithValue("latitude", pr.Latitude);
        cmdHistory.Parameters.AddWithValue("longitude", pr.Longitude);
        cmdHistory.Parameters.AddWithValue("recorded_time", time);
        await cmdHistory.ExecuteNonQueryAsync();
    }
}